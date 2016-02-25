using System;
using System.ComponentModel;

namespace MassEffect2.SaveFormats
{
	[AttributeUsage(AttributeTargets.All)]
	internal class LocalizedCategoryAttribute : CategoryAttribute
	{
		private readonly LocalizedString _Category = new LocalizedString();

		public LocalizedCategoryAttribute(string propertyName, Type resourceType)
			: this(propertyName != null ? "[FIX ME] " + propertyName : null, propertyName, resourceType)
		{}

		public LocalizedCategoryAttribute(string defaultValue, string propertyName, Type resourceType)
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

			_Category.ResourceType = resourceType;
			_Category.PropertyName = propertyName + "_Category";
		}

		// why does this function have an argument? :wtc:
		protected override string GetLocalizedString(string value)
		{
			return _Category.GetLocalizedValue() ?? base.GetLocalizedString(value);
		}
	}
}