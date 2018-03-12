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
		public async void CanIdentifySessionAccessInParallelForeach()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Threading.Tasks; using System.Threading;
using Marten;

class TestClass { 
	
	void TestMethod() 
	{
		IDocumentStore store = null;

		using (var session = store.OpenSession())
		{
			session.Load<TestClass>(1);
			var items = new int[] { };
			Parallel.ForEach(items, n => {
				session.Load<TestClass>(n);
				using (var session2 = store.OpenSession())
				{
					session2.Load<TestClass>(n);
				}
			});
	        ThreadPool.QueueUserWorkItem(_ => {
		        session.Load<TestClass>(1);		
	        });
			session.Load<TestClass>(1);
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
	}
}