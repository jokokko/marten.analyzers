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
            var batch = session.CreateBatchQuery();
			foreach (var n in new[] { 1, 2, 3})
			{
				var item = session.Load<TestClass>(n);
			}
            for (var n=0;n<10;n++)
			{
				session.LoadMany<TestClass>(n);
			}	
            while (true)
			{
				session.LoadMany<TestClass>(1);
			}
            do
			{
				session.LoadMany<TestClass>(1);
                batch.Query<TestClass>();
			} while (true);	
            session.Load<TestClass>(1);
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
            },
            d =>
            {
                Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                Assert.Equal(27, d.Location.GetLineSpan().StartLinePosition.Line);
            });
        }
    }
}
