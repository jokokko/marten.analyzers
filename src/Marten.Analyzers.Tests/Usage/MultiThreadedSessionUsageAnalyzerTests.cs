using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class MultiThreadedSessionUsageAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new MultiThreadedSessionUsageAnalyzer();

		[Fact]
		public async Task CanIdentifySessionAccessInParallelForeach()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks; using System.Threading;
using Marten;

class TestClass {

	void TestMethod()
	{
		IDocumentStore store = null;

		using (var session = store.LightweightSession())
		{
			session.LoadAsync<TestClass>(1);
			var items = new int[] { };
			Parallel.ForEach(items, n => {
				session.LoadAsync<TestClass>(n);
				using (var session2 = store.LightweightSession())
				{
					session2.LoadAsync<TestClass>(n);
				}
			});
	        ThreadPool.QueueUserWorkItem(_ => {
		        session.LoadAsync<TestClass>(1);
	        });
			session.LoadAsync<TestClass>(1);
		}
	}
}");

			Assert.Collection(diagnostics, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(15, d.Location.GetLineSpan().StartLinePosition.Line);
			}, d =>
			{
			    Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
			    Assert.Equal(22, d.Location.GetLineSpan().StartLinePosition.Line);
			});
		}

		[Fact]
		public async Task CanIdentifySessionAccessInParallelForeachAsync()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentStore store, List<Guid> ids, CancellationToken token)
	{
		await using var session = store.LightweightSession();

		await Parallel.ForEachAsync(ids, token, async (id, ct) => {
			await session.LoadAsync<TestClass>(id, ct);
			await session.LoadManyAsync<TestClass>(ids, ct);
			System.Console.WriteLine(id);
			await using var session2 = store.LightweightSession();
			await session2.LoadAsync<TestClass>(id, ct);
		});
		await session.LoadAsync<TestClass>(ids[0], token);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1006", d.Id));
			Assert.Equal(new[] { 14, 15 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifyAnyAccessToSharedSession()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	private readonly IDocumentSession field = null!;

	Task<IQuerySession> OpenAsync() => null!;

	async Task TestMethod(IDocumentStore store, IQuerySession[] sessions, List<Guid> ids, CancellationToken token)
	{
		await using var session = store.LightweightSession();

		await Parallel.ForEachAsync(ids, token, async (id, ct) => {
			session.Store(new TestClass());
			session.Delete<TestClass>(id);
			session.QueueSqlCommand(""select 1"");
			await session.Events.FetchStreamAsync(id, token: ct);
			await session.SaveChangesAsync(ct);
			await field.LoadAsync<TestClass>(id, ct);
			await store.QuerySession().LoadAsync<TestClass>(id, ct);
			await (await OpenAsync()).LoadAsync<TestClass>(id, ct);
			await sessions[0].LoadAsync<TestClass>(id, ct);
			_ = nameof(session.TenantId);
			ThreadPool.QueueUserWorkItem(_ => field.LoadAsync<TestClass>(id, ct));
		});

		Parallel.ForEach(await session.LoadManyAsync<TestClass>(ids, token), item => {
			Console.WriteLine(item);
		});
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1006", d.Id));
			// 28: the nested work item, reported once
			Assert.Equal(new[] { 18, 19, 20, 21, 22, 23, 28 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
