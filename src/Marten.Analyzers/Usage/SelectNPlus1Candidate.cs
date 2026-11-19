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
    public sealed class SelectNPlus1Candidate : MartenInvocationAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.Marten1000SelectNPlus1Candidate);

        public SelectNPlus1Candidate() : base(
            "IQuerySession.LoadAsync",
            "IQuerySession.LoadManyAsync",
            "IQuerySession.Query",
            "IQuerySession.QueryAsync",
            "IQuerySession.QueryForNonStaleData",
            "IQuerySession.QueryByPlanAsync",
            "IQuerySession.CheckExistsAsync",
            "IQuerySession.MetadataForAsync",
            "IQuerySession.StreamJson",
            "IQuerySession.StreamJsonOne",
            "IQuerySession.StreamJsonMany",
            "IQuerySession.ToJsonOne",
            "IQuerySession.ToJsonMany",
            "IQuerySession.SearchAsync",
            "IQuerySession.PlainTextSearchAsync",
            "IQuerySession.PhraseSearchAsync",
            "IQuerySession.WebStyleSearchAsync",
            "IQuerySession.PrefixSearchAsync",
            // QueryAsync(Type type, string sql, ...)
            "QuerySessionExtensions.QueryAsync",
            // Overloads declared in JasperFx.Events, e.g. LoadManyAsync(ids, token)
            "IDocumentReadOperations.LoadAsync",
            "IDocumentReadOperations.LoadManyAsync",
            "IDocumentReadOperations.Query",
            // IQuerySession.Json
            "IJsonLoader.FindByIdAsync",
            "IJsonLoader.StreamById",
            "IAdvancedSql.QueryAsync",
            "IAdvancedSql.StreamAsync",
            "IBatchedQuery.Load",
            "IBatchedQuery.LoadMany",
            "IBatchedQuery.Query",
            "IBatchedQuery.QueryByPlan"
            )
        {
        }
        protected override void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax,
            IMethodSymbol methodSymbol)
        {
            var ancestors = invocationExpressionSyntax.IsWithinIteration();

            // ReSharper disable once InvertIf
            if (ancestors)
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