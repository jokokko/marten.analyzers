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
    public class SelectNPlus1Candidate : MartenInvocationAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.Marten1000SelectNPlus1Candidate);

        public SelectNPlus1Candidate() : base(
            "IQuerySession.Load",
            "IQuerySession.LoadAsync",
            "IQuerySession.LoadMany",
            "IQuerySession.LoadManyAsync",
            "IQuerySession.Query",
            "IQuerySession.QueryAsync")
        {
        }
        protected override void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax,
            IMethodSymbol methodSymbol)
        {
            var ancestors = invocationExpressionSyntax.Ancestors().Any(x =>
            {
                switch (x)
                {
                    case ForEachStatementSyntax _:
                    case ForStatementSyntax _:
                    case WhileStatementSyntax _:
                    case DoStatementSyntax _:
                        return true;
                }

                return false;
            });

            // ReSharper disable once InvertIf
            if (ancestors)
            {
                var builder = ImmutableDictionary.CreateBuilder<string, string>();
                builder["MethodName"] = methodSymbol.Name;

                context.ReportDiagnostic(Diagnostic.Create(
                    SupportedDiagnostics[0],
                    invocationExpressionSyntax.GetLocation(), builder.ToImmutable(), SymbolDisplay.ToDisplayString(methodSymbol,
                        SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
                            SymbolDisplayParameterOptions.None))));
            }
        }
    }
}