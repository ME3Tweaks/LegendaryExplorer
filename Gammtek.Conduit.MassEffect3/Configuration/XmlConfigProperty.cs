using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class XmlConfigProperty : ConfigProperty
	{
		public XmlConfigProperty(IList<ConfigValue> values = null, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
			: base(values, comparisonType) {}

		public XmlConfigProperty(IEnumerable<ConfigValue> values, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
			: base(values, comparisonType) {}
	}
}
