using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Infrastructure
{
	public abstract class MartenInvocationParameterAnalyzer : MartenAnalyzer
	{
	    protected readonly HashSet<string> OnParameters;

		protected MartenInvocationParameterAnalyzer(params string[] arguments)
		{
			OnParameters = new HashSet<string>(arguments, StringComparer.Ordinal);
		}

		protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
		{
			ctx.RegisterSyntaxNodeAction(context =>
			{
				var node = (InvocationExpressionSyntax) context.Node;
				var symbol = context.SemanticModel.GetSymbolInfo(node);

				if (symbol.Symbol?.Kind != SymbolKind.Method)
				{
					return;
				}

				var methodSymbol = (IMethodSymbol) symbol.Symbol;			   
                
                if (methodSymbol.Parameters.Any(x => OnParameters.Any(t => t.Equals(x.ToDisplayString(), StringComparison.Ordinal))))
                {
					Analyze(context, node, methodSymbol);
				}
			}, SyntaxKind.InvocationExpression);
		}
		protected abstract void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax, IMethodSymbol methodSymbol);
	}
}