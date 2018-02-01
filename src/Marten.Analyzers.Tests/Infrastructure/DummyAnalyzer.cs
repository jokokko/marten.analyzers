using System;
using System.Collections.Immutable;
using Marten.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Tests.Infrastructure
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DummyAnalyzer : MartenInvocationAnalyzer
    {
        private readonly Action<SyntaxNodeAnalysisContext> onAnalysis;

        public DummyAnalyzer(Action<SyntaxNodeAnalysisContext> onAnalysis) : base ("IQuerySession.Load")
        {
            this.onAnalysis = onAnalysis;
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(new DiagnosticDescriptor("Dummy", "Dummy", "Dummy", "Dummy", DiagnosticSeverity.Error, true));


        protected override void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax,
            IMethodSymbol methodSymbol)
        {
            onAnalysis(context);
        }
    }
}