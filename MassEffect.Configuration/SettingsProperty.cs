using System;

namespace MassEffect.Configuration
{
	public class SettingsProperty<T>
	{
		public virtual string Name { get; set; }

		public virtual bool IsReadOnly { get; set; }

		public virtual T DefaultValue { get; set; }

		public virtual Type PropertyType
		{
			get { return typeof (T); }
		}

		//public virtual SettingsSerializeAs SerializeAs { get; set; }
		//public virtual SettingsProvider Provider { get; set; }
		//public virtual SettingsAttributeDictionary Attributes { get; set; }
		//public bool ThrowOnErrorDeserializing { get; set; }
		//public bool ThrowOnErrorSerializing { get; set; }
	}

	//public class SettingsAttributeDictionary {}

	//public class SettingsSerializeAs { }

	//public class SettingsProvider {}
}
