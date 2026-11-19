using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
    public sealed class PrematureMaterializationAnalyzerTests
    {
        private readonly DiagnosticAnalyzer analyzer = new PrematureMaterializationAnalyzer();

        [Fact]
        public async Task CanIdentifyPrematureMaterialization()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Linq;
using System.Linq.Expressions;
using Marten;

class TestClass {

    public int Id { get; set; }

	void TestMethod()
	{
		IDocumentStore store = null;

		using (var session = store.LightweightSession())
		{
            var item1 = session.Query<TestClass>().AsEnumerable();
            var item2 = session.Query<TestClass>().ToList();
            var item3 = session.Query<TestClass>().ToArray();
            var item4 = session.Query<TestClass>().Where(x => x.Id > 0).ToArray();
            var item5 = session.Query<TestClass>();
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
                    Assert.Equal(16, d.Location.GetLineSpan().StartLinePosition.Line);
                },
                d =>
                {
                    Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                    Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
                });
        }

        [Fact]
        public async Task CanIdentifyPrematureAsynchronousMaterialization()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

    public int Id { get; set; }

	async Task TestMethod(IQuerySession session, CancellationToken token)
	{
        var item1 = await session.Query<TestClass>().ToListAsync(token);
        var item2 = session.Query<TestClass>().ToAsyncEnumerable(token);
        var item3 = await session.QueryForNonStaleData<TestClass>(TimeSpan.FromSeconds(1)).ToListAsync(token);
        var item4 = (await session.Query<TestClass>().ToListAsync(token)).Where(x => x.Id > 0);
        var item5 = await session.Query<TestClass>().Where(x => x.Id > 0).ToListAsync(token);
        var item6 = session.Query<TestClass>().Where(x => x.Id > 0).ToAsyncEnumerable(token);
        var item7 = await session.Query<TestClass>().CountAsync(token);
        var batch = session.CreateBatchQuery();
        var item8 = batch.Query<TestClass>().ToList();
        var item9 = batch.Query<TestClass>().Where(x => x.Id > 0).ToList();
        await batch.Execute(token);
	}
}");

            Assert.All(diagnostics, d => Assert.Equal("Marten1004", d.Id));
            Assert.Equal(new[] { 14, 15, 16, 17, 22 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }

        [Fact]
        public async Task ResultsOfSqlQueriesAreNotPrematurelyMaterialized()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

    async Task TestMethod(IQuerySession session, CancellationToken token)
    {
        var items = (await session.QueryAsync<TestClass>(""where true"")).ToList();
        var array = (await session.QueryAsync<TestClass>(""where true"", token)).ToArray();
    }
}");

            Assert.Empty(diagnostics);
        }
    }
}
