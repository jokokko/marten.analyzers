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

			// Registrations taking a lifecycle
			public HashSet<string> OnMethods { get; } = new HashSet<string>(new[]
			{
				// StoreOptions.Projections
				"ProjectionGraph.Add",
				"ProjectionGraph.Register",
				"ProjectionOptions.Snapshot",
				"ProjectionOptions.AddGlobalProjection",
				// IServiceCollection, AddMarten(), AddMartenStore<T>()
				"MartenServiceCollectionExtensions.AddProjectionWithServices",
				"MartenConfigurationExpression.AddProjectionWithServices",
				"MartenStoreExpression.AddProjectionWithServices",
				// MyProjection.Register<MyProjection>(services, lifecycle, ...)
				"SingleStreamProjection.Register",
				"MultiStreamProjection.Register",
				"EventProjection.Register"
			});

			private const string LifecycleParameter = "lifecycle";
			private const string Inline = "Inline";
			private const string Async = "Async";

			// Parameters of a registration that take the projection as an instance
			private static readonly string[] ProjectionParameters = { "projection", "source" };

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
				ConcurrentBag<ProjectionCtx> collection;

				switch (Lifecycle(context, node, method))
				{
					case Inline:
						collection = syncProjections;
						break;
					case Async:
						collection = asyncProjections;
						break;
					// Live projections, or a lifecycle not known at compile time
					default:
						return;
				}

				ITypeSymbol t;

				var projection = method.Parameters.FirstOrDefault(x => ProjectionParameters.Contains(x.Name, StringComparer.Ordinal));

				if (projection != null)
				{
					var p = node.ArgumentFor(projection);

					t = p == null ? null : context.SemanticModel.GetTypeInfo(p.Expression, context.CancellationToken).Type;

					// Unrelated projections share the abstraction they are passed as
					if (t != null && (t.TypeKind == TypeKind.Interface || t.IsAbstract))
					{
						return;
					}
				}
				else
				{
					t = method.TypeArguments.FirstOrDefault();
				}

				// ReSharper disable once InvertIf
				if (t != null && t.TypeKind != TypeKind.Error)
				{
					var ctx = new ProjectionCtx
					{
						Type = t,
						Invocation = node
					};

					collection.Add(ctx);
				}
			}

			// Name of the enum member passed as the lifecycle of the projection
			private static string Lifecycle(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol method)
			{
				var parameter = method.Parameters.FirstOrDefault(x => x.Name.Equals(LifecycleParameter, StringComparison.Ordinal) && x.Type.TypeKind == TypeKind.Enum);

				if (parameter == null)
				{
					return null;
				}

				var argument = node.ArgumentFor(parameter);

				if (argument == null)
				{
					return null;
				}

				var value = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

				if (!value.HasValue)
				{
					return null;
				}

				return parameter.Type.GetMembers().OfType<IFieldSymbol>()
					.FirstOrDefault(x => x.HasConstantValue && Equals(x.ConstantValue, value.Value))?.Name;
			}

			public void End(CompilationAnalysisContext context)
			{
				var items = from s in syncProjections
					from a in asyncProjections
					where SymbolEqualityComparer.Default.Equals(s.Type, a.Type)
					from i in new[] { s, a }
					select i;

				foreach (var i in items.Distinct())
				{
					Report(context, i.Invocation, i.Type);
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
