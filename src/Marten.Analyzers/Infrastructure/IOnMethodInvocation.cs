using System.Collections.Generic;

namespace Marten.Analyzers.Infrastructure
{
	public interface IOnMethodInvocation
	{
		HashSet<string> OnMethods { get; }
	}
}