using System.Linq;
using System.Threading.Tasks;
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
		public async Task CanIdentifyQueriesWithinIterations()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod()
	{
		IDocumentStore store = null;

		await using (var session = store.LightweightSession())
		{
			foreach (var n in new[] { 1, 2, 3})
			{
				await session.Events.LoadAsync<TestClass>(Guid.NewGuid());
			}
            for (var n=0;n<10;n++)
			{
				await session.Events.LoadAsync(Guid.NewGuid());
			}
            while (true)
			{
				await session.Events.AggregateStreamAsync<TestClass>(Guid.NewGuid());
			}
            do
			{
				await session.Events.AggregateStreamAsync<TestClass>(""stream"");
			} while (true);
            await session.Events.LoadAsync<TestClass>(Guid.NewGuid());
		}
	}
}");

			Assert.Collection(diagnostics, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(14, d.Location.GetLineSpan().StartLinePosition.Line);
				}, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(18, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(22, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(26, d.Location.GetLineSpan().StartLinePosition.Line);
				});
		}

		[Fact]
		public async Task CanIdentifyStreamAndAggregateFetchesWithinIterations()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentSession session, IQuerySession query, List<Guid> ids, CancellationToken token)
	{
		foreach (var id in ids)
		{
			await session.Events.FetchStreamAsync(id, token: token);
			await session.Events.FetchStreamStateAsync(id, token);
			await session.Events.AggregateStreamToLastKnownAsync<TestClass>(id, token: token);
			await session.Events.FetchLatest<TestClass>(id, token);
			await session.Events.ProjectLatest<TestClass>(id, token);
			await session.Events.FetchForWriting<TestClass>(id, token);
			await session.Events.FetchForExclusiveWriting<TestClass>(id, token);
			await query.Events.AggregateStreamAsync<TestClass>(id, token: token);
			await query.Events.FetchStreamAsync(id, token: token);
			session.Events.Append(id, new object());
		}
		await session.Events.FetchManyForWriting<TestClass>(ids, token);
		await session.Events.FetchLatest<TestClass>(ids[0], token);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1001", d.Id));
			Assert.Equal(new[] { 13, 14, 15, 16, 17, 18, 19, 20, 21 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifyQueriesByTagsAndWritesToAggregatesWithinIterations()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JasperFx.Events.Tags;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentSession session, IQuerySession query, List<Guid> ids, EventTagQuery tags, Stream stream, CancellationToken token)
	{
		foreach (var id in ids)
		{
			await query.Events.QueryAllRawEvents().ToListAsync(token);
			await query.Events.QueryRawEventDataOnly<TestClass>().ToListAsync(token);
			await query.Events.QueryByTagsAsync(tags, token);
			await query.Events.AggregateByTagsAsync<TestClass>(tags, token);
			await query.Events.EventsExistAsync(tags, token);
			await session.Events.QueryAllRawEvents().ToListAsync(token);
			await session.Events.QueryByTagsAsync(tags, token);
			await session.Events.AggregateByTagsAsync<TestClass>(tags, token);
			await session.Events.EventsExistAsync(tags, token);
			await session.Events.StreamLatestJson<TestClass>(id, stream, token);
			await session.Events.FetchForWritingByTags<TestClass>(tags, token);
			await session.Events.FetchManyForWriting<TestClass>(ids, token);
			await session.Events.WriteToAggregate<TestClass>(id, s => { }, token);
			await session.Events.WriteExclusivelyToAggregate<TestClass>(id, s => { }, token);
		}

		foreach (var e in await query.Events.FetchStreamAsync(ids[0], token: token)) { }
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1001", d.Id));
			Assert.Equal(new[] { 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
