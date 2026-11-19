using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Marten.Analyzers.Infrastructure
{
	public static class ArgumentMixins
	{
		// Argument for a parameter, by name or by position. Null if not supplied.
		public static ArgumentSyntax ArgumentFor(this InvocationExpressionSyntax invocation, IParameterSymbol parameter)
		{
			var arguments = invocation.ArgumentList.Arguments;

			foreach (var argument in arguments)
			{
				if (argument.NameColon != null && argument.NameColon.Name.Identifier.ValueText.Equals(parameter.Name, StringComparison.Ordinal))
				{
					return argument;
				}
			}

			// Extension methods invoked as such exclude the receiver: ordinals align with the arguments
			return parameter.Ordinal < arguments.Count && arguments[parameter.Ordinal].NameColon == null
				? arguments[parameter.Ordinal]
				: null;
		}
	}
}
