using System;
using System.Collections;
using System.Windows.Markup;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	[MarkupExtensionReturnType(typeof (IEnumerable))]
	public class EnumValuesExtension : MarkupExtension
	{
		public EnumValuesExtension() {}

		public EnumValuesExtension(Type enumType)
		{
			EnumType = enumType;
		}

		[ConstructorArgument("enumType")]
		public Type EnumType { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (EnumType == null)
			{
				throw new ArgumentException("The enum type is not set");
			}

			return Enum.GetValues(EnumType);
		}
	}
}
