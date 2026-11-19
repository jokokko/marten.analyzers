using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Marten.Analyzers.Infrastructure
{
	public static class TypeSymbolMixins
	{
		private const string QuerySession = "Marten.IQuerySession";

		// Without the nullable modifier: 'IDocumentSession?' is to match 'IDocumentSession'
		private static readonly SymbolDisplayFormat Format = new SymbolDisplayFormat(
			SymbolDisplayGlobalNamespaceStyle.Omitted,
			SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
			SymbolDisplayGenericsOptions.IncludeTypeParameters,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

		public static string ToQualifiedName(this ITypeSymbol type)
		{
			return type.ToDisplayString(Format);
		}

		// IQuerySession or what derives from it: IDocumentSession, IDocumentOperations, ITenantOperations...
		public static bool IsSession(this ITypeSymbol type)
		{
			return type.ToQualifiedName().Equals(QuerySession, StringComparison.Ordinal)
			       || type.AllInterfaces.Any(x => x.ToQualifiedName().Equals(QuerySession, StringComparison.Ordinal));
		}
	}
}
