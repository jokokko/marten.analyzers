using Microsoft.CodeAnalysis;

namespace Marten.Analyzers.Infrastructure
{
    public static class OnCreationMixins
    {
        private static readonly SymbolDisplayFormat Format = new SymbolDisplayFormat(
            SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.None,
            SymbolDisplayMemberOptions.IncludeContainingType);
        public static bool MatchCreation(this IOnCreation instance, SymbolInfo node)
        {
            if (node.Symbol == null)
            {
                return false;

            }

            var fqn = node.Symbol.ToDisplayString(Format);

            return instance.OnCreation.Contains(fqn);
        }
    }
}