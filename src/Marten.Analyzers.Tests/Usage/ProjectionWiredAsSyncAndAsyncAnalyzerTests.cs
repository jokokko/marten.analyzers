using System.Linq;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class ProjectionWiredAsSyncAndAsyncAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new ProjectionWiredAsSyncAndAsyncAnalyzer();

		[Fact]
		public async void CanIdentifyProjectionsWiredAsSyncAndAsync()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Marten.Events.Projections.Async;
using Marten.Storage;

class T { }

class TestClass { 
	
	void TestMethod() 
	{
		DocumentStore.For(c =>
        {                
			c.Events.AsyncProjections.Add(new Projection());
			c.Events.AsyncProjections.AggregateStreamsWith<TestClass>();			
			c.Events.AsyncProjections.AggregateStreamsWith<T>();
		});
	}

	void TestMethod2() 
	{
		DocumentStore.For(c =>
        {                
			c.Events.InlineProjections.Add(new Projection());			
			c.Events.InlineProjections.AggregateStreamsWith<TestClass>();
			c.Events.AsyncProjections.AggregateStreamsWith<T>();
		});
	}

	class Projection : IProjection
	{
		public void Apply(IDocumentSession session, EventPage page)
		{
			throw new NotImplementedException();
		}

		public Task ApplyAsync(IDocumentSession session, EventPage page, CancellationToken token)
		{
			throw new NotImplementedException();
		}

		public void EnsureStorageExists(ITenant tenant)
		{
			throw new NotImplementedException();
		}

		public Type[] Consumes { get; }
		public AsyncOptions AsyncOptions { get; }
	}
}");

			Assert.Collection(diagnostics.OrderBy(x => x.Location.GetLineSpan().StartLinePosition.Line), d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
				}, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(18, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(27, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(28, d.Location.GetLineSpan().StartLinePosition.Line);
				});
		}
	}
}