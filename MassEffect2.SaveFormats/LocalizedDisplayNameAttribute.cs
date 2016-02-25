using System;
using System.ComponentModel;

namespace MassEffect2.SaveFormats
{
	[AttributeUsage(AttributeTargets.Property |
					AttributeTargets.Event |
					AttributeTargets.Class |
					AttributeTargets.Method)]
	internal class LocalizedDisplayNameAttribute : DisplayNameAttribute
	{
		private readonly LocalizedString _DisplayName = new LocalizedString();

		public LocalizedDisplayNameAttribute(string propertyName, Type resourceType)
			: this(propertyName != null ? "[FIX ME] " + propertyName : null, propertyName, resourceType)
		{}

		public LocalizedDisplayNameAttribute(string defaultValue, string propertyName, Type resourceType)
			: base(defaultValue)
		{
			if (resourceType == null)
			{
				throw new ArgumentNullException("resourceType");
			}

			if (string.IsNullOrEmpty(propertyName))
			{
				throw new ArgumentNullException("propertyName");
			}

			_DisplayName.ResourceType = resourceType;
			_DisplayName.PropertyName = propertyName + "_DisplayName";
		}

		public override string DisplayName
		{
			get { return _DisplayName.GetLocalizedValue() ?? base.DisplayName; }
		}
	}
}