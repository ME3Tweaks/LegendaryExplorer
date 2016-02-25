using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gammtek.Conduit.UnrealEngine.Configuration
{
	public class UConfigFile : Dictionary<string, UConfigSection>
	{
		public UConfigFile()
		{}

		public UConfigFile(int capacity)
			: base(capacity)
		{}

		public UConfigFile(IEqualityComparer<string> comparer)
			: base(comparer)
		{}

		public UConfigFile(int capacity, IEqualityComparer<string> comparer)
			: base(capacity, comparer)
		{}

		public UConfigFile([NotNull] IDictionary<string, UConfigSection> dictionary)
			: base(dictionary)
		{}

		public UConfigFile([NotNull] IDictionary<string, UConfigSection> dictionary, IEqualityComparer<string> comparer)
			: base(dictionary, comparer)
		{}

		protected UConfigFile(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{}

		//TArray<FConfigCommandlineOverride> CommandlineOptions;

		public string Name { get; set; }

		public UConfigFile SourceConfigFile { get; set; }
		public List<UIniFilename> SourceIniHierarchy { get; set; }

		public static bool operator ==(UConfigFile file1, UConfigFile file2)
		{
			throw new NotImplementedException();
		}

		public static bool operator !=(UConfigFile file1, UConfigFile file2)
		{
			return !(file1 == file2);
		}

		public bool Combine(string filename)
		{
			throw new NotImplementedException();
		}

		public bool Combine(UConfigFile configFile)
		{
			throw new NotImplementedException();
		}
	}
}
