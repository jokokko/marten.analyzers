using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class SessionAsMethodArgumentInIterationTests
	{
		private readonly DiagnosticAnalyzer analyzer = new SessionAsMethodArgumentInIteration();

		[Fact]
		public async Task CanIdentifySessionArgumentWithinIterations()
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
				await UseSession(session);
			}
            for (var n=0;n<10;n++)
			{
				await UseSession(session);
			}
            while (true)
			{
				await UseSession(session);
			}
            do
			{
				await UseSession(session);
			} while (true);
            await UseSession(session);
            while (true)
			{
				await UseSession();
			}
		}
	}

    Task UseSession(IDocumentSession session = null)
    {
        return Task.CompletedTask;
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
		public async Task CanIdentifySessionArgumentWithinIterationsInNullableContext()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentStore store, IQuerySession query, IDocumentOperations operations, IAsyncEnumerable<int> items, IDocumentSession? maybe)
	{
		await using var session = store.LightweightSession();

		await foreach (var n in items)
		{
			await UseSession(session);
			await UseSession(maybe);
			await UseSession(null);
			UseQuery(query);
			UseOperations(operations);
			UseNothing(n);
		}
		await UseSession(session);
	}

	Task UseSession(IDocumentSession? session) => Task.CompletedTask;
	void UseQuery(IQuerySession session) { }
	void UseOperations(IDocumentOperations operations) { }
	void UseNothing(int n) { }
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1002", d.Id));
			Assert.Equal(new[] { 14, 15, 17, 18 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifySessionsOfAnyKindAsArguments()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using System.Threading.Tasks;
using JasperFx.Events.Documents;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentSession session, IQuerySession query)
	{
		var tenant = session.ForTenant(""t"");
		var tenantQuery = query.ForTenant(""t"");

		foreach (var n in Numbers(session))
		{
			await UseQuery(tenant);
			await UseQuery(tenantQuery);
			await UseQuery(session.ForTenant(""t""));
			await UseOperations(tenant);
			await UseReadOperations(session);
			UseNothing(n);
		}
	}

	int[] Numbers(IQuerySession session) => new[] { 1, 2 };
	Task UseQuery(IQuerySession session) => Task.CompletedTask;
	Task UseOperations(IDocumentOperations operations) => Task.CompletedTask;
	Task UseReadOperations(IDocumentReadOperations operations) => Task.CompletedTask;
	void UseNothing(int n) { }
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1002", d.Id));
			Assert.Equal(new[] { 14, 15, 16, 17, 18 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
