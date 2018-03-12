using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Marten.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskClosesOverSessionAnalyzer : MartenAnalyzer, IOnMethodInvocation, IOnCreation
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.Marten1007TaskClosesOverSession);

        public HashSet<string> OnMethods => new HashSet<string>(new[]
        {
            "TaskFactory.StartNew"
        });

        public HashSet<string> OnCreation => new HashSet<string>(new[]
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

            ctx.RegisterSyntaxNodeAction(context =>
            {
                var node = (ObjectCreationExpressionSyntax)context.Node;
                var symbol = context.SemanticModel.GetSymbolInfo(node);

                if (this.MatchCreation(symbol))
                {
                    AnalyzeCreation(context, node);
                }
            }, SyntaxKind.ObjectCreationExpression);
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

        private void AnalyzeCaptured(SyntaxNodeAnalysisContext context, ExpressionSyntax invocationExpressionSyntax)
        {
            var flow = context.SemanticModel.AnalyzeDataFlow(invocationExpressionSyntax);
            var captured = flow.Captured.OfType<ILocalSymbol>().Where(x => x.Type.ToDisplayString()
                .Equals("Marten.IDocumentSession", StringComparison.Ordinal)).ToArray();

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