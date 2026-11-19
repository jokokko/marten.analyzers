using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class TaskClosesOverSessionAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new TaskClosesOverSessionAnalyzer();

		[Fact]
		public async Task CanIdentifyTasksThatCloseOverSession()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks;
using Marten;

class TestClass {

	void TestMethod()
	{
		IDocumentStore store = null;

		using (var session = store.LightweightSession())
		{
			Task.Factory.StartNew(() => {
				session.LoadAsync<TestClass>(1);
				var session2 = store.LightweightSession();
				session2.LoadAsync<TestClass>(1);
			});
            new Task(() => {
		        session.LoadAsync<TestClass>(1);
	        });
            new Task<int>(() => {
		        session.LoadAsync<TestClass>(1);
                var session2 = store.LightweightSession();
                session2.LoadAsync<TestClass>(1);
                return 1;
	        });
		}
	}
}");

			Assert.Collection(diagnostics, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(12, d.Location.GetLineSpan().StartLinePosition.Line);
			}, d =>
            {
                Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
            }, d =>
            {
                Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                Assert.Equal(20, d.Location.GetLineSpan().StartLinePosition.Line);
            });
		}

		[Fact]
		public async Task CanIdentifyTasksThatCloseOverSessionInNullableContext()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentStore store)
	{
		await using var session = store.LightweightSession();

		await Task.Run(async () => {
			await session.LoadAsync<TestClass>(1);
		});
		await Task.Run(async () => {
			await using var session2 = store.LightweightSession();
			await session2.LoadAsync<TestClass>(1);
		});
		await Task.Run(async () => {
			await using var session3 = store.LightweightSession();
			Func<Task> inner = () => session3.LoadAsync<TestClass>(1);
			await inner();
		});
		await Task.Factory.StartNew(() => session.LoadAsync<TestClass>(1));
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1007", d.Id));
			Assert.Equal(new[] { 11, 23 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifyTasksThatCloseOverSessionsOfAnyKind()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Threading.Tasks;
using Marten;

class Handler(IDocumentSession primary, IQuerySession primaryQuery)
{
	async Task TestMethod(IDocumentStore store, IDocumentSession parameter, IQuerySession query, IDocumentOperations operations)
	{
		await Task.Run(() => primary.LoadAsync<Handler>(1));
		await Task.Run(() => primaryQuery.LoadAsync<Handler>(1));
		await Task.Run(() => parameter.LoadAsync<Handler>(1));
		await Task.Run(() => query.LoadAsync<Handler>(1));
		await Task.Run(() => operations.LoadAsync<Handler>(1));
		var local = store.QuerySession();
		await Task.Run(() => local.LoadAsync<Handler>(1));
		var tenant = parameter.ForTenant(""tenant"");
		await Task.Factory.StartNew(() => tenant.LoadAsync<Handler>(1));
		Func<IQuerySession, Task> lambda = s => Task.Run(() => s.LoadAsync<Handler>(1));
		async Task LocalFunction(IQuerySession s) { await Task.Run(() => s.LoadAsync<Handler>(1)); }
		var task = new Task(() => query.LoadAsync<Handler>(1));
		await Task.Run(() => store.QuerySession().LoadAsync<Handler>(1));
		await Task.Run(async () => { await using var inner = store.QuerySession(); await inner.LoadAsync<Handler>(1); });
		await Task.Run(() => {
			Func<IQuerySession, Task> nested = s => Task.Factory.StartNew(() => s.LoadAsync<Handler>(1));
			return nested;
		});
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1007", d.Id));
			Assert.Equal(new[] { 9, 10, 11, 12, 13, 15, 17, 18, 19, 20, 24 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
			Assert.Equal("Task closes over session 'primary'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 9).GetMessage());
			Assert.Equal("Task closes over session 'parameter'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 11).GetMessage());
			Assert.Equal("Task closes over session 'local'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 15).GetMessage());
		}

		[Fact]
		public async Task CanIdentifyTasksThatUseSessionsOfInstance()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Threading.Tasks;
using Marten;

class Base
{
	protected IDocumentSession Inherited { get; set; } = null!;
}

class Handler : Base
{
	private readonly IDocumentSession field = null!;
	private static IQuerySession shared = null!;
	private readonly Handler other = null!;
	private readonly IDocumentStore store = null!;

	IQuerySession Property => field;

	async Task TestMethod()
	{
		await Task.Run(() => field.LoadAsync<Handler>(1));
		await Task.Run(() => this.field.LoadAsync<Handler>(1));
		await Task.Run(() => Property.LoadAsync<Handler>(1));
		await Task.Run(() => Inherited.LoadAsync<Handler>(1));
		await Task.Run(() => base.Inherited.LoadAsync<Handler>(1));
		await Task.Run(() => shared.LoadAsync<Handler>(1));
		await Task.Run(() => Handler.shared.LoadAsync<Handler>(1));
		await Task.Factory.StartNew(() => field.Events.FetchStreamAsync(Guid.NewGuid()));
		var task = new Task(() => field.LoadAsync<Handler>(1));
		await Task.Run(() => { field.LoadAsync<Handler>(1); field.LoadAsync<Handler>(2); });
		await Task.Run(() => { async Task Local() { await field.LoadAsync<Handler>(1); } return Local(); });
		await Task.Run(() => other.field.LoadAsync<Handler>(1));
		await Task.Run(() => new Handler { Inherited = store.LightweightSession() });
		await Task.Run(() => nameof(field));
		await Task.Run(() => this is { Property: null });
		await Task.Factory.StartNew(s => ((IDocumentSession)s!).LoadAsync<Handler>(1), field);
		await Task.Run(() => store.QuerySession().LoadAsync<Handler>(1));
		await Task.Run(async () => { await using var session = store.QuerySession(); await session.LoadAsync<Handler>(1); });
		var loaded = await field.LoadAsync<Handler>(1);
		await Task.Run(() => loaded);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1007", d.Id));
			Assert.Equal(new[] { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
			Assert.Equal("Task closes over session 'Handler.field'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 21).GetMessage());
			Assert.Equal("Task closes over session 'Handler.Property'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 23).GetMessage());
			Assert.Equal("Task closes over session 'Base.Inherited'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 24).GetMessage());
			Assert.Equal("Task closes over session 'Handler.shared'.", diagnostics.Single(d => d.Location.GetLineSpan().StartLinePosition.Line == 26).GetMessage());
		}

		[Fact]
		public async Task CanIdentifyTargetTypedTasksThatCloseOverSession()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System.Threading.Tasks;
using Marten;

class TestClass {

	void TestMethod(IQuerySession session)
	{
		Task task = new(() => session.LoadAsync<TestClass>(1));
		Task<int> result = new(() => { session.LoadAsync<TestClass>(1); return 1; });
		object other = new();
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1007", d.Id));
			Assert.Equal(new[] { 7, 8 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
