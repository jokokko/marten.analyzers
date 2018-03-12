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
		public async void CanIdentifyTasksThatCloseOverSession()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks;
using Marten;

class TestClass { 
	
	void TestMethod() 
	{
		IDocumentStore store = null;

		using (var session = store.OpenSession())
		{
			Task.Factory.StartNew(() => {
				session.Load<TestClass>(1);
				var session2 = store.OpenSession();
				session2.Load<TestClass>(1);
			});
            new Task(() => {		
		        session.Load<TestClass>(1);
	        });
            new Task<int>(() => {		
		        session.Load<TestClass>(1);
                var session2 = store.OpenSession();
                session2.Load<TestClass>(1);
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
	}
}