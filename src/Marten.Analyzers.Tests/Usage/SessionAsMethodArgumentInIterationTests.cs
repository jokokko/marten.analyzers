using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class SessionAsMethodParameterInIterationTests
	{
		private readonly DiagnosticAnalyzer analyzer = new SessionAsMethodArgumentInIteration();

		[Fact]
		public async void CanIdentifySessionArgumentWithinIterations()
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
			foreach (var n in new[] { 1, 2, 3})
			{
				UseSession(session);
			}
            for (var n=0;n<10;n++)
			{
				UseSession(session);
			}	
            while (true)
			{
				UseSession(session);
			}
            do
			{
				UseSession(session);
			} while (true);	
            UseSession(session);
            while (true)
			{
				UseSession();
			}
		}   
	}

    void UseSession(IDocumentSession session = null)
    {
    
    }
}");

			Assert.Collection(diagnostics, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(13, d.Location.GetLineSpan().StartLinePosition.Line);
				}, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(21, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(25, d.Location.GetLineSpan().StartLinePosition.Line);
				});
		}
	}
}