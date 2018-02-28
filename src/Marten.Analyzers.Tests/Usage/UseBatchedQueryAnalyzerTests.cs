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
        public async void CanIdentifyInterdependentParameters()
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
		var session2 = store.OpenSession();
		TestClass item;
		using (var session = store.OpenSession())
		{
            item = session.Load<TestClass>(1);
            var id1 = item.Id;
			var id2 = id1;            
            var id3 = id2;
            var item2 = session.Load<TestClass>(id3);
            var item4 = session.Load<TestClass>(item2.Id);
            var n = 1;
            var item5 = session.LoadMany<TestClass>(n, n);            
            var item6 = session.Query<TestClass>();
            var ids = item6.Select(x => x.Id).ToArray();
            var item7 = session.LoadMany<TestClass>(ids);
            var item8 = session.Query<TestClass>().Where(x => x.Id == id1).ToList();
            var item9 = session.Query<TestClass>().Where(x => x.Id == 1).ToList();
            using (var session3 = store.OpenSession())
		    {
                session3.Load<TestClass>(1);
            }
        }
        new Action<IDocumentSession>(s => {
            item = s.Load<TestClass>(1);
            var n = 1;
            var item2 = s.Load<TestClass>(n);            
        })(session2);
	}
}");

            Assert.Collection(diagnostics, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(16, d.Location.GetLineSpan().StartLinePosition.Line);
                },
                d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(23, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(24, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(28, d.Location.GetLineSpan().StartLinePosition.Line);
                }, d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(35, d.Location.GetLineSpan().StartLinePosition.Line);
                } , d =>
                {
                    Assert.Equal(DiagnosticSeverity.Info, d.Severity);
                    Assert.Equal(37, d.Location.GetLineSpan().StartLinePosition.Line);
                }
                );
        }

        [Fact]
        public async void CanIdentifyLoadWithinQuery()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Marten;

class TestClass { 
	
    public int Id { get; set; }

    public IEnumerable<T> PerformDbQuery<T>(Func<IQuerySession, IEnumerable<T>> func)
    {
        return null;
    }

	IEnumerable<TestClass> TestMethod(int id) 
	{
        var n= PerformDbQuery<TestClass>(session => {
            var item = session.Load<TestClass>(id);
            var item2 = session.Load<TestClass>(id);

            return session.Query<TestClass>().Where(x => x.Id > 0).ToList()
                .Select(x => new TestClass() {
                    Id = session.Load<TestClass>(x.Id).Id
                });            
        });

        return n;
	}
}");
            
            Assert.Equal(4, diagnostics.Length);
        }

        // TODO: LambdaExpressionSyntax & invocation chains
        [Fact]
        public async void CanScopeSessionsWhenUsingFields()
        {
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Marten;

class Provider {
    public IDocumentSession Session { get; set; }
}

class TestClass { 
	
    public int Id { get; set; }
    private Provider provider;

    public IEnumerable<T> PerformDbQuery<T>(Func<IQuerySession, IEnumerable<T>> func)
    {
        return null;
    }

	IEnumerable<TestClass> TestMethod(int id) 
	{
        var s = provider.Session;

        var n= PerformDbQuery<TestClass>(session => {
            var item = provider.Session.Load<TestClass>(id);
            var item2 = s.Load<TestClass>(id);

            return session.Query<TestClass>().Where(x => x.Id > 0).ToList()
                .Select(x => new TestClass() {
                    Id = provider.Session.Load<TestClass>(x.Id).Id
                });            
        });

        return n;
	}
}");

            Assert.Equal(2, diagnostics.Length);
        }
    }
}