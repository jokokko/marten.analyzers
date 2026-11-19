using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
    public sealed class SelectNPlus1CandidateTests
    {
        private readonly DiagnosticAnalyzer analyzer = new SelectNPlus1Candidate();

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
            var batch = session.CreateBatchQuery();
			foreach (var n in new[] { 1, 2, 3})
			{
				var item = await session.LoadAsync<TestClass>(n);
			}
            for (var n=0;n<10;n++)
			{
				await session.LoadManyAsync<TestClass>(n);
			}
            while (true)
			{
				await session.LoadManyAsync<TestClass>(1);
			}
            do
			{
				await session.LoadManyAsync<TestClass>(1);
                batch.Query<TestClass>();
			} while (true);
            await session.LoadAsync<TestClass>(1);
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
                Assert.Equal(19, d.Location.GetLineSpan().StartLinePosition.Line);
            },
            d =>
            {
                Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                Assert.Equal(23, d.Location.GetLineSpan().StartLinePosition.Line);
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

        [Fact]
        public async Task CanIdentifyQueriesOfAllKindsWithinIterations()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(IQuerySession session, IAsyncEnumerable<Guid> stream, List<Guid> ids, CancellationToken token)
	{
		var batch = session.CreateBatchQuery();
		await foreach (var id in stream)
		{
			await session.LoadManyAsync<TestClass>(ids, token);
			await session.Query<TestClass>().ToListAsync(token);
			await session.QueryAsync<TestClass>(""where data ->> 'Id' = ?"", id);
			await session.QueryForNonStaleData<TestClass>(TimeSpan.FromSeconds(1)).ToListAsync(token);
			await session.QueryByPlanAsync<TestClass>(null!, token);
			await session.AdvancedSql.QueryAsync<TestClass>(""select id, data from mt_doc_testclass"", token);
			await session.ForTenant(""tenant"").LoadAsync<TestClass>(id, token);
			_ = batch.Load<TestClass>(id);
			_ = batch.LoadMany<TestClass>().ById(id);
			_ = batch.QueryByPlan<TestClass>(null!);
			_ = session.CorrelationId;
		}
		await session.LoadManyAsync<TestClass>(ids, token);
		await batch.Execute(token);
	}
}");

            Assert.All(diagnostics, d => Assert.Equal("Marten1000", d.Id));
            Assert.Equal(new[] { 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }

        [Fact]
        public async Task CanIdentifyQueriesOfFurtherKindsWithinIterations()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Linq;

class TestClass {

    async Task TestMethod(IQuerySession session, List<Guid> ids, Stream stream, ICompiledQuery<TestClass, TestClass> one, ICompiledQuery<TestClass, IEnumerable<TestClass>> many, CancellationToken token)
    {
        foreach (var id in ids)
        {
            await session.CheckExistsAsync<TestClass>(id, token);
            await session.MetadataForAsync(this, token);
            await session.StreamJson<TestClass>(stream, token, ""where data ->> 'Id' = ?"", id);
            await session.StreamJsonOne(one, stream, token);
            await session.StreamJsonMany(many, stream, token);
            await session.ToJsonOne(one, token);
            await session.ToJsonMany(many, token);
            await session.SearchAsync<TestClass>(""text"", ""english"", token);
            await session.PlainTextSearchAsync<TestClass>(""text"", ""english"", token);
            await session.PhraseSearchAsync<TestClass>(""text"", ""english"", token);
            await session.WebStyleSearchAsync<TestClass>(""text"", ""english"", token);
            await session.PrefixSearchAsync<TestClass>(""text"", ""english"", token);
            await session.QueryAsync(typeof(TestClass), ""where data ->> 'Id' = ?"", id);
            await session.Json.FindByIdAsync<TestClass>(id, token);
            await session.Json.StreamById<TestClass>(id, stream, token);
            await foreach (var item in session.AdvancedSql.StreamAsync<TestClass>(""select id, data from mt_doc_testclass"", token)) { }
        }
    }
}");

            Assert.All(diagnostics, d => Assert.Equal("Marten1000", d.Id));
            Assert.Equal(new[] { 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }

        [Fact]
        public async Task QueriesProvidingTheItemsOfAnIterationAreNotWithinIt()
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

    public Guid Id { get; set; }

    async Task TestMethod(IQuerySession session, List<Guid> ids, CancellationToken token)
    {
        foreach (var item in await session.Query<TestClass>().Where(x => x.Id != Guid.Empty).ToListAsync(token)) { }
        await foreach (var item in session.AdvancedSql.StreamAsync<TestClass>(""select id, data from mt_doc_testclass"", token)) { }
        for (var items = await session.LoadManyAsync<TestClass>(ids, token); items.Count > 100;) { break; }

        while (await session.LoadAsync<TestClass>(ids[0], token) != null) { }
        for (var n = 0; await session.LoadAsync<TestClass>(ids[n], token) != null; n++) { }
        foreach (var id in ids)
        {
            foreach (var item in await session.Query<TestClass>().Where(x => x.Id == id).ToListAsync(token)) { }
        }
    }
}");

            Assert.All(diagnostics, d => Assert.Equal("Marten1000", d.Id));
            Assert.Equal(new[] { 18, 19, 22 },
                diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
        }
    }
}
