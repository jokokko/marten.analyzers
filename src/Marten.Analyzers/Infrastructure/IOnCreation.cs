using System.Collections.Generic;

namespace Marten.Analyzers.Infrastructure
{
    public interface IOnCreation
    {
        HashSet<string> OnCreation { get; }
    }
}