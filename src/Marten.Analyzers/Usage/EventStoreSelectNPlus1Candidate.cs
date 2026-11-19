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
	public class EventStoreSelectNPlus1Candidate : MartenInvocationAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.Marten1001EventStoreSelectNPlus1Candidate);

		// Declared in JasperFx.Events
		public EventStoreSelectNPlus1Candidate() : base(
			"IQueryEventStore.LoadAsync",
			"IQueryEventStore.AggregateStreamAsync",
			"IQueryEventStore.AggregateStreamToLastKnownAsync",
			"IQueryEventStore.FetchStreamAsync",
			"IQueryEventStore.FetchStreamStateAsync",
			"IQueryEventStore.QueryAllRawEvents",
			"IQueryEventStore.QueryRawEventDataOnly",
			"IQueryEventStore.QueryByTagsAsync",
			"IQueryEventStore.AggregateByTagsAsync",
			"IQueryEventStore.EventsExistAsync",
			// Redeclared for IDocumentSession.Events
			"IEventStoreOperations.QueryByTagsAsync",
			"IEventStoreOperations.AggregateByTagsAsync",
			"IEventStoreOperations.EventsExistAsync",
			"IEventStoreOperations.FetchLatest",
			"IEventStoreOperations.ProjectLatest",
			"IEventStoreOperations.StreamLatestJson",
			"IEventStoreOperations.FetchForWriting",
			"IEventStoreOperations.FetchForExclusiveWriting",
			"IEventStoreOperations.FetchForWritingByTags",
			"IEventStoreOperations.FetchManyForWriting",
			"IEventStoreOperations.WriteToAggregate",
			"IEventStoreOperations.WriteExclusivelyToAggregate")
		{
		}
		protected override void Analyze(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpressionSyntax,
			IMethodSymbol methodSymbol)
		{
			var ancestors = invocationExpressionSyntax.IsWithinIteration();

			// ReSharper disable once InvertIf
			if (ancestors)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SupportedDiagnostics[0],
					invocationExpressionSyntax.GetLocation(), SymbolDisplay.ToDisplayString(methodSymbol,
						SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
							SymbolDisplayParameterOptions.None))));
			}
		}
	}
}