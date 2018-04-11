using System;
using System.Collections.Concurrent;
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
	public sealed class ProjectionWiredAsSyncAndAsyncAnalyzer : MartenAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.Marten1008ProjectionAsSyncAndAsync);

		protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
		{
			var analyzer = new Analyzer(this);
			ctx.RegisterSyntaxNodeAction(analyzer.Analyze, SyntaxKind.InvocationExpression);
			ctx.RegisterCompilationEndAction(analyzer.End);
		}

		private sealed class Analyzer : IOnMethodInvocation
		{
			private readonly ProjectionWiredAsSyncAndAsyncAnalyzer host;

			public HashSet<string> OnMethods => new HashSet<string>(new[]
			{
				"ProjectionCollection.AggregateStreamsWith",
				"ProjectionCollection.Add"
			});
			
			public void Analyze(SyntaxNodeAnalysisContext context)
			{

				var node = (InvocationExpressionSyntax)context.Node;
				var symbol = context.SemanticModel.GetSymbolInfo(node);

				if (symbol.Symbol?.Kind != SymbolKind.Method)
				{
					return;
				}

				var method = (IMethodSymbol)symbol.Symbol;
				if (this.MatchInvocation(method))
				{
					AnalyzeInvocation(context, node, method);
				}
			}

			private readonly ConcurrentBag<ProjectionCtx> syncProjections = new ConcurrentBag<ProjectionCtx>();
			private readonly ConcurrentBag<ProjectionCtx> asyncProjections = new ConcurrentBag<ProjectionCtx>();

			public Analyzer(ProjectionWiredAsSyncAndAsyncAnalyzer host)
			{
				this.host = host;
			}

			private sealed class ProjectionCtx
			{
				public ITypeSymbol Type;
				public InvocationExpressionSyntax Invocation;
			}

			private void AnalyzeInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol method)
			{
				var memberAccess = node.DescendantNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();

				if (memberAccess == null)
				{
					return;
				}

				var member = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);

				if (member.Symbol == null)
				{
					return;
				}

				var collection = member.Symbol.Name.Equals("AsyncProjections") ? asyncProjections : syncProjections;

			    ITypeSymbol t = null;

			    if (method.Name.Equals("AggregateStreamsWith", StringComparison.Ordinal))
			    {
			        t = method.TypeArguments.FirstOrDefault();
			    }
			    else if (method.Name.Equals("Add", StringComparison.Ordinal))
			    {
			        var p = node.ArgumentList.Arguments.FirstOrDefault();
			        t = context.SemanticModel.GetTypeInfo(p.Expression).Type;
			    }

			    // ReSharper disable once InvertIf
			    if (t != null)
			    {
			        var ctx = new ProjectionCtx
			        {
                        Type = t,
                        Invocation = node
			        };

					collection.Add(ctx);
				}
			}

			public void End(CompilationAnalysisContext context)
			{				
				//var items = syncProjections.Intersect(asyncProjections, new ProjectionCtxComparer());

				var items = from s in syncProjections
					from a in asyncProjections
					where s.Type.Equals(a.Type)
					select new {s, a};

				foreach (var i in items)
				{
					Report(context, i.s.Invocation, i.s.Type);
					Report(context, i.a.Invocation, i.a.Type);
				}
			}

			private void Report(CompilationAnalysisContext context, InvocationExpressionSyntax invocation, ITypeSymbol type)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					host.SupportedDiagnostics[0],
					invocation.GetLocation(), SymbolDisplay.ToDisplayString(type,
						SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
							SymbolDisplayParameterOptions.None))));
			}
		}
	}
}