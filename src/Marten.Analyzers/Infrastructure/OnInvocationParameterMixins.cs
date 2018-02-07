using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Marten.Analyzers.Infrastructure
{
	public static class OnInvocationParameterMixins
	{
		public static bool MatchInvocationParameters(this IOnMethodParameter instance, IMethodSymbol methodSymbol)
		{
			return methodSymbol.Parameters.Any(x => instance.OnParameters.Any(t => t.Equals(x.ToDisplayString(), StringComparison.Ordinal)));
		}
	}
}