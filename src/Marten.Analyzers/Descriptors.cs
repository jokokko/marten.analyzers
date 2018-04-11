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

        internal static readonly DiagnosticDescriptor Marten1000SelectNPlus1Candidate = Rule("Marten1000", "Session queried within an iteration", RuleCategory.Usage, DiagnosticSeverity.Warning, "Session queried within an iteration with '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1001EventStoreSelectNPlus1Candidate = Rule("Marten1001", "Event store queried within an iteration", RuleCategory.Usage, DiagnosticSeverity.Warning, "Event store queried within an iteration with '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1002SessionAsMethodArgumentInIteration = Rule("Marten1002", "Session used as a method argument within an iteration", RuleCategory.Usage, DiagnosticSeverity.Warning, "Session used as a method argument within an iteration in '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1003SqlInjection = Rule("Marten1003", "Possible site for SQL injection", RuleCategory.Usage, DiagnosticSeverity.Warning, "Possible site for SQL injection in '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1004PrematureMaterialization = Rule("Marten1004", "Possible premature query materialization", RuleCategory.Usage, DiagnosticSeverity.Warning, "Possible premature query materialization in '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1005UseBatchedQuery = Rule("Marten1005", "Consider using batched query", RuleCategory.Usage, DiagnosticSeverity.Info, "Consider enlisting query in batched query of '{0}'.");
        internal static readonly DiagnosticDescriptor Marten1006MultithreadedSessionAccess = Rule("Marten1006", "Session accessed in possibly multithreaded context", RuleCategory.Usage, DiagnosticSeverity.Warning, "Session '{0}' accessed in possibly multithreaded context.");
        internal static readonly DiagnosticDescriptor Marten1007TaskClosesOverSession = Rule("Marten1007", "Tasks closes over session", RuleCategory.Usage, DiagnosticSeverity.Warning, "Task closes over session '{0}'.");
	    internal static readonly DiagnosticDescriptor Marten1008ProjectionAsSyncAndAsync = Rule("Marten1008", "Projection wired as synchronous and asynchronous", RuleCategory.Usage, DiagnosticSeverity.Warning, "Projection '{0}' wired as synchronous and asynchronous.");
	}
}