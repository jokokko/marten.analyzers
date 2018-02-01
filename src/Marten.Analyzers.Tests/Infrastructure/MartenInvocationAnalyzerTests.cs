using Xunit;

namespace Marten.Analyzers.Tests.Infrastructure
{
    public sealed class MartenInvocationAnalyzerTests
    {
        [Fact]
        public async void CodeNotUsingMartenShouldNotGetAnalyzed()
        {
            object ctx = null;
            var analyzer = new DummyAnalyzer(context => ctx = context);
            var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
                @"using System.Diagnostics;

class TestClass { 

	void TestMethod() 
	{		
		foreach (var n in new[] { 1, 2, 3})
		{
			Debug.WriteLine(n);
		}
        for (var n=0;n<10;n++)
		{
			Debug.WriteLine(n);
		}	
        while (true)
		{
			Debug.WriteLine(1);
		}
        do
		{
			Debug.WriteLine(1);
		} while (true);        
	}
}");

            Assert.Null(ctx);
            Assert.Empty(diagnostics);
        }
    }
}