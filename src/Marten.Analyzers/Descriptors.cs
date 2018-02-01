using Marten.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Marten.Analyzers
{
    internal static class Descriptors
    {
        private static DiagnosticDescriptor Rule(string id, string title, RuleCategory category, DiagnosticSeverity defaultSeverity, string messageFormat, string description = null)
        {            
            return new DiagnosticDescriptor(id, title, messageFormat, category.Name, defaultSeverity, true, description, $"https://jokokko.github.io/marten.analyzers/rules/{id}");
        }

	    internal static readonly DiagnosticDescriptor Marten1000SelectNPlus1Candidate = Rule("Marten1000", "Session queried within an iteration", RuleCategory.Usage, DiagnosticSeverity.Warning, "Session queried within an iteration");
    }
}