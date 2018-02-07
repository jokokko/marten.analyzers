using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class SqlInjectionAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new SqlInjectionAnalyzer();

		[Fact]
		public async void CanIdentifySqlInjectionCandidates()
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
			string n = "" and 1 = 1 ; drop table mt_doc_testclass; "";
			session.Query<TestClass>(""where data->> 'Id' = 'ABC' "" + n);			
			session.QueryAsync<TestClass>(""where data->> 'Id' = 'ABC' "" + n);
			session.QueryAsync<TestClass>(n + ""where data->> 'Id' = 'ABC' "");
			session.Query<TestClass>(""where data->> 'Id' = 'ABC'"");
			session.Query<TestClass>(""where data->> 'Id' = ?"", ""ABC"");
			session.Query<TestClass>();
			session.Query<TestClass>(""where data->> 'Id' = 'ABC' "" + "" and 1 = 1 "");
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
				Assert.Equal(13, d.Location.GetLineSpan().StartLinePosition.Line);
			}, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(14, d.Location.GetLineSpan().StartLinePosition.Line);
			});
		}
	}
}