using System.Collections.Generic;

namespace Marten.Analyzers.Infrastructure
{
	public interface IOnMethodParameter
	{
		HashSet<string> OnParameters { get; }
	}
}