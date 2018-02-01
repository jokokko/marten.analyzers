using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Infrastructure
{    
    public abstract class MartenInvocationAnalyzer : MartenAnalyzer
    {
        private readonly HashSet<string> onMethods;

        protected MartenInvocationAnalyzer(params string[] methods)
        {
            onMethods = new HashSet<string>(methods, StringComparer.Ordinal);
        }

        protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
        {
            ctx.RegisterSyntaxNodeAction(context =>
            {
                var none = (InvocationExpressionSyntax) context.Node;
                var symbol = context.SemanticModel.GetSymbolInfo(none);

                if (symbol.Symbol?.Kind != SymbolKind.Method)
                {
                    return;
                }

                var methodSymbol = (IMethodSymbol) symbol.Symbol;

                if (onMethods.Contains($"{methodSymbol.ContainingType.Name}.{methodSymbol.Name}"))
                {
                    Analyze(context, none, methodSymbol);
                }
            }, SyntaxKind.InvocationExpression);
        }
        protected abstract void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax, IMethodSymbol methodSymbol);
    }
}