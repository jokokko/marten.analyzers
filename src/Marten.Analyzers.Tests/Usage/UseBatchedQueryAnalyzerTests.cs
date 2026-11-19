using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
    public sealed class UseBatchedQueryAnalyzerTests
    {
        private readonly DiagnosticAnalyzer analyzer = new UseBatchedQueryAnalyzer();

        [Fact]
        public async Task CanIdentifyInterdependentParameters()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Marten;

class TestClass {

    public int Id { get; set; }

	async Task TestMethod()
	{
		IDocumentStore store = null;
		var session2 = store.LightweightSession();
		TestClass item;
		await using (var session = store.LightweightSession())
		{
            item = await session.LoadAsync<TestClass>(1);
            var id1 = item.Id;
			var id2 = id1;
            var id3 = id2;
            var item2 = await session.LoadAsync<TestClass>(id3);
            var item4 = await session.LoadAsync<TestClass>(item2.Id);
            var n = 1;
            var item5 = await session.LoadManyAsync<TestClass>(n, n);
            var item6 = session.Query<TestClass>();
            var ids = (await item6.Select(x => x.Id).ToListAsync()).ToArray();
            var item7 = await session.LoadManyAsync<TestClass>(ids);
            var item8 = await session.Query<TestClass>().Where(x => x.Id == id1).ToListAsync();
            var item9 = await session.Query<TestClass>().Where(x => x.Id == 1).ToListAsync();
            await using (var session3 = store.LightweightSession())
		    {
                await session3.LoadAsync<TestClass>(1);
            }
        }
        await new Func<IDocumentSession, Task>(async s => {
            item = await s.LoadAsync<TestClass>(1);
            var n = 1;
            var item2 = await s.LoadAsync<TestClass>(n);
        })(session2);
	}
}");

            Assert.Collection(diagnostics, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
                },
                d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(24, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(25, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(29, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(36, d.Location.GetLineSpan().StartLinePosition.Line);
                } , d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(38, d.Location.GetLineSpan().StartLinePosition.Line);
                }
                );
        }

        [Fact]
        public async Task CanIdentifyLoadWithinQuery()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Marten;

class TestClass {

    public int Id { get; set; }

    public Task<IEnumerable<T>> PerformDbQuery<T>(Func<IQuerySession, Task<IEnumerable<T>>> func)
    {
        return null;
    }

	async Task<IEnumerable<TestClass>> TestMethod(int id)
	{
        var n= await PerformDbQuery<TestClass>(async session => {
            var item = await session.LoadAsync<TestClass>(id);
            var item2 = await session.LoadAsync<TestClass>(id);

            return (await session.Query<TestClass>().Where(x => x.Id > 0).ToListAsync())
                .Select(x => new TestClass() {
                    Id = session.LoadAsync<TestClass>(x.Id).Result.Id
                });
        });

        return n;
	}
}");

            Assert.Equal(4, diagnostics.Length);
        }

        // TODO: LambdaExpressionSyntax & invocation chains
        [Fact]
        public async Task CanScopeSessionsWhenUsingFields()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Marten;

class Provider {
    public IDocumentSession Session { get; set; }
}

class TestClass {

    public int Id { get; set; }
    private Provider provider;

    public Task<IEnumerable<T>> PerformDbQuery<T>(Func<IQuerySession, Task<IEnumerable<T>>> func)
    {
        return null;
    }

	async Task<IEnumerable<TestClass>> TestMethod(int id)
	{
        var s = provider.Session;

        var n= await PerformDbQuery<TestClass>(async session => {
            var item = await provider.Session.LoadAsync<TestClass>(id);
            var item2 = await s.LoadAsync<TestClass>(id);

            return (await session.Query<TestClass>().Where(x => x.Id > 0).ToListAsync())
                .Select(x => new TestClass() {
                    Id = provider.Session.LoadAsync<TestClass>(x.Id).Result.Id
                });
        });

        return n;
	}
}");

            Assert.Equal(2, diagnostics.Length);
        }

        [Fact]
        public async Task CanIdentifyQueriesInNullableContext()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

    public Guid Id { get; set; }

	async Task TestMethod(IQuerySession session, List<Guid> ids, CancellationToken token)
	{
        var item1 = await session.LoadManyAsync<TestClass>(ids, token);
        var item2 = await session.LoadManyAsync<TestClass>(token, ids);
        var item3 = await session.LoadAsync<TestClass>(ids[0], token);
        var item4 = await session.LoadAsync<TestClass>(item3!.Id, token);
	}
}");

            Assert.All(diagnostics, d => Assert.Equal("Marten1005", d.Id));
            Assert.Equal(new[] { 13, 14, 15 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }

        [Fact]
        public async Task SessionsWithoutSymbolAreNotAnalyzed()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IDocumentStore store)
	{
        var item1 = await (await Open(store)).LoadAsync<TestClass>(1);
        var item2 = await (await Open(store)).LoadAsync<TestClass>(2);
	}

    Task<IQuerySession> Open(IDocumentStore store) => Task.FromResult(store.QuerySession());
}");

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task CanAnalyzeQueriesReadingWhatIsDeclaredInAnotherFile()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System.Threading.Tasks;
using Marten;

partial class TestClass
{
    async Task TestMethod(IQuerySession session)
    {
        var item1 = await session.LoadAsync<TestClass>(id);
        var item2 = await session.LoadAsync<TestClass>(id);
    }
}",
                @"using System;

partial class TestClass(Guid id) { }");

            Assert.All(diagnostics, d => Assert.Equal("Marten1005", d.Id));
            Assert.Equal(new[] { 7, 8 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }

        [Fact]
        public async Task QueriesDependingOnAnAssignmentWithinADeclarationAreNotIndependent()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks;
using Marten;

class TestClass {

    public int Id { get; set; }

    Task<T> Wrap<T>(Func<Task<T>> func) => func();

    async Task TestMethod(IQuerySession session)
    {
        var outer = await Wrap(async () =>
        {
            TestClass item;
            item = await session.LoadAsync<TestClass>(1);
            var item2 = await session.LoadAsync<TestClass>(item.Id);
            return item2;
        });
    }
}");

            Assert.Empty(diagnostics);
        }
    }
}
