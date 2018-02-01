using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using static Marten.Analyzers.Infrastructure.Constants;

namespace Marten.Analyzers.Infrastructure
{
    public sealed class MartenContext
    {        
        public readonly Version Version;
        public MartenContext(Compilation compilation)
        {                        
            Version = compilation.ReferencedAssemblyNames
                .FirstOrDefault(a => a.Name.Equals(MartenAssembly, StringComparison.OrdinalIgnoreCase))
                ?.Version;            
        }        
    }
}