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
        public async void CanIdentifyQueriesWithinIterations()
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

		using (var session = store.OpenSession())
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
    }
}