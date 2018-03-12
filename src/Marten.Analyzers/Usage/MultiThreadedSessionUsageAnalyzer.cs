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
	public sealed class MultiThreadedSessionUsageAnalyzer : MartenInvocationAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
			ImmutableArray.Create(Descriptors.Marten1006MultithreadedSessionAccess);

		public MultiThreadedSessionUsageAnalyzer() : base(
			"Parallel.ForEach", "ThreadPool.QueueUserWorkItem")
		{
		}

		private sealed class AccessAndSymbol
		{
			public ISymbol Symbol;
			public MemberAccessExpressionSyntax MemberAccess;
			public ISymbol Accessed;
		}

		protected override void Analyze(SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocationExpressionSyntax,
			IMethodSymbol methodSymbol)
		{
			var memberAccess = invocationExpressionSyntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

			var flow = context.SemanticModel.AnalyzeDataFlow(invocationExpressionSyntax);
		    var vars = flow.VariablesDeclared;

			var sessionAccess = memberAccess.TakeWhile(_ => !context.CancellationToken.IsCancellationRequested).Aggregate(new List<AccessAndSymbol>(), (list, syntax) =>
			{
				var symbol = context.SemanticModel.GetSymbolInfo(syntax);
				var accessed = context.SemanticModel.GetSymbolInfo(syntax.Expression).Symbol;
				if (symbol.Symbol != null && symbol.Symbol.ContainingType.ToDisplayString().Equals("Marten.IQuerySession", StringComparison.Ordinal) && (accessed == null || !vars.Contains(accessed)))
				{					
					list.Add(new AccessAndSymbol { Symbol = symbol.Symbol, MemberAccess = syntax, Accessed = accessed});
				}

				return list;
			});

			foreach (var sa in sessionAccess)
			{
				if (context.CancellationToken.IsCancellationRequested)
				{
					return;
				}

				context.ReportDiagnostic(Diagnostic.Create(
					SupportedDiagnostics[0],
					sa.MemberAccess.GetLocation(), SymbolDisplay.ToDisplayString(sa.Accessed,
						SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
							SymbolDisplayParameterOptions.None))));
			}			
		}
	}
}