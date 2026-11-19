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
			// What a session is passed as to read documents
			"JasperFx.Events.Documents.IDocumentReadOperations")
		{
		}

		protected override bool Matches(ITypeSymbol type)
		{
			return type.IsSession() || base.Matches(type);
		}

		protected override void Analyze(SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocationExpressionSyntax,
			IMethodSymbol methodSymbol)
		{
		    var ancestors = invocationExpressionSyntax.IsWithinIteration();

		    // ReSharper disable once InvertIf
		    if (ancestors)
		    {
		        var args = invocationExpressionSyntax.ArgumentList.Arguments.Select(x => context.SemanticModel.GetTypeInfo(x.Expression, context.CancellationToken))
		            .ToArray();

			    if (context.CancellationToken.IsCancellationRequested)
			    {
				    return;
			    }

		        // null and default have no type
		        if (!args.Any(x => x.Type != null && Matches(x.Type)))
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