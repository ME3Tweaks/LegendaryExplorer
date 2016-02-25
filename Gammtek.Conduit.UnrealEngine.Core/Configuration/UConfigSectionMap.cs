using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gammtek.Conduit.UnrealEngine.Configuration
{
	public class UConfigSectionMap : Dictionary<string, string>
	{
		public UConfigSectionMap()
		{}

		public UConfigSectionMap(int capacity)
			: base(capacity)
		{}

		public UConfigSectionMap(IEqualityComparer<string> comparer)
			: base(comparer)
		{}

		public UConfigSectionMap(int capacity, IEqualityComparer<string> comparer)
			: base(capacity, comparer)
		{}

		public UConfigSectionMap([NotNull] IDictionary<string, string> dictionary)
			: base(dictionary)
		{}

		public UConfigSectionMap([NotNull] IDictionary<string, string> dictionary, IEqualityComparer<string> comparer)
			: base(dictionary, comparer)
		{}

		protected UConfigSectionMap(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{}
	}
}
