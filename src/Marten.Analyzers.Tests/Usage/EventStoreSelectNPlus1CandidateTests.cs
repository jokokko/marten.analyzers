using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class EventStoreSelectNPlus1CandidateTests
	{
		private readonly DiagnosticAnalyzer analyzer = new EventStoreSelectNPlus1Candidate();

		[Fact]
		public async void CanIdentifyQueriesWithinIterations()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using Marten;

class TestClass { 
	
	void TestMethod() 
	{
		IDocumentStore store = null;

		using (var session = store.OpenSession())
		{
			foreach (var n in new[] { 1, 2, 3})
			{
				session.Events.Load<TestClass>(Guid.NewGuid());
			}
            for (var n=0;n<10;n++)
			{
				session.Events.LoadAsync<TestClass>(Guid.NewGuid());
			}	
            while (true)
			{
				session.Events.AggregateStream<TestClass>(Guid.NewGuid());
			}
            do
			{
				session.Events.AggregateStreamAsync<TestClass>(Guid.NewGuid());
			} while (true);	
            session.Events.Load<TestClass>(Guid.NewGuid());
		}   
	}
}");

			Assert.Collection(diagnostics, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(13, d.Location.GetLineSpan().StartLinePosition.Line);
				}, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(21, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(25, d.Location.GetLineSpan().StartLinePosition.Line);
				});
		}
	}
}