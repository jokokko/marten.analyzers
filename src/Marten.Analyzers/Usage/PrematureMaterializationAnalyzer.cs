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
    public sealed class PrematureMaterializationAnalyzer : MartenInvocationAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.Marten1004PrematureMaterialization);

        public PrematureMaterializationAnalyzer() : base(
            "IQuerySession.Query",
            "IQuerySession.QueryAsync",
            "IBatchedQuery.Query"
        )
        {
        }

        private static readonly string[] Methods = {
            "AsEnumerable",
            "ToList",
            "ToArray"
        };

        protected override void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax,
            IMethodSymbol methodSymbol)
        {
            var nextInvocation = invocationExpressionSyntax.Parent.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            if (nextInvocation == null)
            {
                return;
            }

            var name = nextInvocation.ChildNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault()?.Name?.ToString();
            
            // ReSharper disable once InvertIf
            if (Methods.Contains(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SupportedDiagnostics[0],
                    invocationExpressionSyntax.GetLocation(), SymbolDisplay.ToDisplayString(methodSymbol,
                        SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
                            SymbolDisplayParameterOptions.None))));
            }
        }
    }
}