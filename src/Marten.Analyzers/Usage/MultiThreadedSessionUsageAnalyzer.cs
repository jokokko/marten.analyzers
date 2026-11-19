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
			"Parallel.ForEach", "Parallel.ForEachAsync", "ThreadPool.QueueUserWorkItem")
		{
		}

		private sealed class AccessAndSymbol
		{
			public MemberAccessExpressionSyntax MemberAccess;
			public ISymbol Accessed;
		}

		// Held by a variable, a parameter, a field or a property. store.QuerySession().LoadAsync<T>(id) is not shared.
		private static bool IsSession(ISymbol symbol)
		{
			switch (symbol)
			{
				case ILocalSymbol local:
					return local.Type.IsSession();
				case IParameterSymbol parameter:
					return parameter.Type.IsSession();
				case IFieldSymbol field:
					return field.Type.IsSession();
				case IPropertySymbol property:
					return !property.IsIndexer && property.Type.IsSession();
				default:
					return false;
			}
		}

		// Within a delegate. The rest, e.g. the source of Parallel.ForEach, is evaluated by the calling thread.
		private bool IsWorkOf(SyntaxNodeAnalysisContext context, SyntaxNode node, InvocationExpressionSyntax invocation)
		{
			var withinDelegate = false;

			foreach (var ancestor in node.Ancestors().TakeWhile(x => x != invocation))
			{
				switch (ancestor)
				{
					case AnonymousFunctionExpressionSyntax _:
						withinDelegate = true;
						break;
					// nameof(session.TenantId)
					case InvocationExpressionSyntax nameOf when nameOf.Expression is IdentifierNameSyntax name
					                                            && name.Identifier.ValueText.Equals("nameof", StringComparison.Ordinal)
					                                            && context.SemanticModel.GetConstantValue(nameOf, context.CancellationToken).HasValue:
						return false;
					// Reported for the nested Parallel.ForEach etc.
					case InvocationExpressionSyntax nested when withinDelegate
					                                            && context.SemanticModel.GetSymbolInfo(nested, context.CancellationToken).Symbol is IMethodSymbol method
					                                            && method.ContainingType != null
					                                            && OnMethods.Contains($"{method.ContainingType.Name}.{method.Name}"):
						return false;
				}
			}

			return withinDelegate;
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
				var accessed = context.SemanticModel.GetSymbolInfo(syntax.Expression, context.CancellationToken).Symbol;

				// Declared by the delegate: not shared
				if (IsSession(accessed) && !vars.Contains(accessed, SymbolEqualityComparer.Default) && IsWorkOf(context, syntax, invocationExpressionSyntax))
				{
					list.Add(new AccessAndSymbol { MemberAccess = syntax, Accessed = accessed });
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