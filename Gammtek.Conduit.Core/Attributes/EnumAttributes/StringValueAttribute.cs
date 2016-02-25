using System;

namespace Gammtek.Conduit.Attributes.EnumAttributes
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	public class StringValueAttribute : Attribute
	{
		public StringValueAttribute([CanBeNull] string value = null)
		{
			StringValue = value;
		}

		public string StringValue { get; protected set; }
	}
}
