using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gammtek.Conduit.UnrealEngine.Configuration
{
	public class UConfigSection : UConfigSectionMap
	{
		public UConfigSection()
		{}

		public UConfigSection(int capacity)
			: base(capacity)
		{}

		public UConfigSection(IEqualityComparer<string> comparer)
			: base(comparer)
		{}

		public UConfigSection(int capacity, IEqualityComparer<string> comparer)
			: base(capacity, comparer)
		{}

		public UConfigSection([NotNull] IDictionary<string, string> dictionary)
			: base(dictionary)
		{}

		public UConfigSection([NotNull] IDictionary<string, string> dictionary, IEqualityComparer<string> comparer)
			: base(dictionary, comparer)
		{}

		protected UConfigSection(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{}

		public bool HasQuotes(string test)
		{
			return false;
		}
	}
}
