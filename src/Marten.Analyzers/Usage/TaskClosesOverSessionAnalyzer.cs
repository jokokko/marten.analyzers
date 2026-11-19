using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Marten.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Marten.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskClosesOverSessionAnalyzer : MartenAnalyzer, IOnMethodInvocation, IOnCreation
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.Marten1007TaskClosesOverSession);

        public HashSet<string> OnMethods { get; } = new HashSet<string>(new[]
        {
            "TaskFactory.StartNew",
            "Task.Run"
        });

        public HashSet<string> OnCreation { get; } = new HashSet<string>(new[]
        {
            "System.Threading.Tasks.Task.Task"
        });

        protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
        {
            ctx.RegisterSyntaxNodeAction(context =>
            {
                var node = (InvocationExpressionSyntax)context.Node;
                var symbol = context.SemanticModel.GetSymbolInfo(node);

                if (symbol.Symbol?.Kind != SymbolKind.Method)
                {
                    return;
                }

                var method = (IMethodSymbol)symbol.Symbol;
                if (this.MatchInvocation(method))
                {
                    AnalyzeInvocation(context, node);
                }
            }, SyntaxKind.InvocationExpression);

            // new Task(...), Task task = new(...)
            ctx.RegisterSyntaxNodeAction(context =>
            {
                var node = (BaseObjectCreationExpressionSyntax)context.Node;
                var symbol = context.SemanticModel.GetSymbolInfo(node);

                if (this.MatchCreation(symbol))
                {
                    AnalyzeCreation(context, node);
                }
            }, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
        }

        private void AnalyzeCreation(SyntaxNodeAnalysisContext context, ExpressionSyntax node)
        {
            AnalyzeCaptured(context, node);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocationExpressionSyntax)
        {
            AnalyzeCaptured(context, invocationExpressionSyntax);
        }

        // Closed over as a local or as a parameter
        private static bool IsSession(ISymbol symbol)
        {
            switch (symbol)
            {
                case ILocalSymbol local:
                    return local.Type.IsSession();
                case IParameterSymbol parameter:
                    return !parameter.IsThis && parameter.Type.IsSession();
                default:
                    return false;
            }
        }

        // A field or a property is not closed over, the instance is: identified by its use within the task
        private static IEnumerable<ISymbol> SessionsOfInstance(SyntaxNodeAnalysisContext context, ExpressionSyntax task)
        {
            var operation = context.SemanticModel.GetOperation(task, context.CancellationToken);

            if (operation == null)
            {
                return Enumerable.Empty<ISymbol>();
            }

            return operation.Descendants().OfType<IMemberReferenceOperation>()
                .Where(x => IsSessionOfInstance(x) && IsUsedByLambda(x, operation))
                .Select(x => x.Member)
                .Distinct(SymbolEqualityComparer.Default);
        }

        private static bool IsSessionOfInstance(IMemberReferenceOperation reference)
        {
            ITypeSymbol type;

            switch (reference.Member)
            {
                case IFieldSymbol field:
                    type = field.Type;
                    break;
                case IPropertySymbol property:
                    type = property.Type;
                    break;
                default:
                    return false;
            }

            if (!type.IsSession())
            {
                return false;
            }

            if (reference.Instance == null)
            {
                return reference.Member.IsStatic;
            }

            // this.session, base.Session. Not other.session, new Handler { Session = ... }, is { Session: null }
            return reference.Instance is IInstanceReferenceOperation instance
                   && instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance;
        }

        private static bool IsUsedByLambda(IOperation reference, IOperation task)
        {
            var lambda = false;

            for (var parent = reference.Parent; parent != null && parent != task; parent = parent.Parent)
            {
                switch (parent)
                {
                    // nameof(session)
                    case INameOfOperation _:
                        return false;
                    case IAnonymousFunctionOperation _:
                        lambda = true;
                        break;
                }
            }

            return lambda;
        }

        private void AnalyzeCaptured(SyntaxNodeAnalysisContext context, ExpressionSyntax invocationExpressionSyntax)
        {
            var flow = context.SemanticModel.AnalyzeDataFlow(invocationExpressionSyntax);
            // Captured lists the captures of the whole member: of interest are those of the task, declared outside of it
            var captured = flow.CapturedInside.Except(flow.VariablesDeclared, SymbolEqualityComparer.Default)
                .Where(IsSession)
                .Concat(SessionsOfInstance(context, invocationExpressionSyntax))
                .ToArray();

            foreach (var sa in captured)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    SupportedDiagnostics[0],
                    invocationExpressionSyntax.GetLocation(), SymbolDisplay.ToDisplayString(sa,
                        SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
                            SymbolDisplayParameterOptions.None))));
            }
        }
    }
}