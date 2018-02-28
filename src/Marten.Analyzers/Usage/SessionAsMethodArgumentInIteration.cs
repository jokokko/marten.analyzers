using System;
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
	public sealed class SessionAsMethodArgumentInIteration : MartenInvocationParameterAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
			ImmutableArray.Create(Descriptors.Marten1002SessionAsMethodArgumentInIteration);

		public SessionAsMethodArgumentInIteration() : base(
            "Marten.IQuerySession",
		    "Marten.IDocumentSession")
		{
		}

		protected override void Analyze(SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocationExpressionSyntax,
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
		        var args = invocationExpressionSyntax.ArgumentList.Arguments.Select(x => context.SemanticModel.GetTypeInfo(x.Expression, context.CancellationToken))
		            .ToArray();

			    if (context.CancellationToken.IsCancellationRequested)
			    {
				    return;
			    }

		        if (!args.Any(x => OnParameters.Any(y => y.Equals(x.Type.ToDisplayString(), StringComparison.Ordinal))))
		        {
		            return;
		        }
		        context.ReportDiagnostic(Diagnostic.Create(
		            SupportedDiagnostics[0],
		            invocationExpressionSyntax.GetLocation(), SymbolDisplay.ToDisplayString(methodSymbol,
		                SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
		                    SymbolDisplayParameterOptions.None))));
		    }
        }
	}
}