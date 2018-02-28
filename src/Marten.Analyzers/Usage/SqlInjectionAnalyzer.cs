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
	public sealed class SqlInjectionAnalyzer : MartenInvocationAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
			ImmutableArray.Create(Descriptors.Marten1003SqlInjection);

		public SqlInjectionAnalyzer() : base(
			"IQuerySession.Query",
			"IQuerySession.QueryAsync",
			"IBatchedQuery.Query")
		{
		}

		protected override void Analyze(SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocationExpressionSyntax,
			IMethodSymbol methodSymbol)
		{
			var p = methodSymbol.Parameters.FirstOrDefault();

			if (p == null || !p.ToDisplayString().Equals("string", StringComparison.Ordinal))
			{
				return;
			}

			var arg = invocationExpressionSyntax.ArgumentList.Arguments.FirstOrDefault();

			// ReSharper disable once UsePatternMatching
			var expr = arg?.Expression as BinaryExpressionSyntax;

			if (expr == null)
			{
				return;
			}
			
			if (arg.Expression.DescendantNodes().Aggregate(new byte(), (t, x) =>
			{
				switch (x)
				{
					case LiteralExpressionSyntax e:
						t |= e.IsKind(SyntaxKind.StringLiteralExpression) ? (byte)0x1 : (byte)0x0;
						break;
					case IdentifierNameSyntax _:
						t |= 0x2;
						break;
				}

				return t;
			}) != 0x3 || context.CancellationToken.IsCancellationRequested)
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