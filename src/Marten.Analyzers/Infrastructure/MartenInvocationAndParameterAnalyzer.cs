using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Infrastructure
{
	public abstract class MartenInvocationAndParameterAnalyzer : MartenAnalyzer, IOnMethodInvocation, IOnMethodParameter
	{
		public abstract HashSet<string> OnMethods { get; }
		public abstract HashSet<string> OnParameters { get; }

		protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
		{
			ctx.RegisterSyntaxNodeAction(context =>
			{
				var none = (InvocationExpressionSyntax)context.Node;
				var symbol = context.SemanticModel.GetSymbolInfo(none);

				if (symbol.Symbol?.Kind != SymbolKind.Method)
				{
					return;
				}

				var methodSymbol = (IMethodSymbol)symbol.Symbol;

				if (this.MatchInvocation(methodSymbol) && this.MatchInvocationParameters(methodSymbol))
				{
					Analyze(context, none, methodSymbol);
				}

			}, SyntaxKind.InvocationExpression);
		}

		protected abstract void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax, IMethodSymbol methodSymbol);
	}
}