using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class XmlConfigSection : ConfigSection
	{
		public XmlConfigSection(IDictionary<string, ConfigProperty> properties = null, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
			: base(properties, comparisonType) {}
	}
}
