using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Marten.Analyzers.Infrastructure
{
	public static class IterationMixins
	{
		// Evaluated by each iteration of a loop?
		public static bool IsWithinIteration(this SyntaxNode node)
		{
			for (var current = node; current.Parent != null; current = current.Parent)
			{
				switch (current.Parent)
				{
					// foreach (var item in <once>) <each>
					case CommonForEachStatementSyntax forEach when current == forEach.Statement:
						return true;
					// for (<once>; <each>; <each>) <each>
					case ForStatementSyntax loop when current == loop.Statement || current == loop.Condition || current is ExpressionSyntax expression && loop.Incrementors.Contains(expression):
						return true;
					// while (<each>) <each>, do <each> while (<each>)
					case WhileStatementSyntax _:
					case DoStatementSyntax _:
						return true;
				}
			}

			return false;
		}
	}
}
