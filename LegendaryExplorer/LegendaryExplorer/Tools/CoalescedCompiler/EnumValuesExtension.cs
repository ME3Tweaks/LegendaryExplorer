using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Markup;

namespace LegendaryExplorer.Tools.CoalescedCompiler
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

	public enum CoalescedType
	{
		//[Display(Name = "Binary")]
		[Description("Binary Coalesced file.")]
		Binary,

		//[Display(Name = "Xml")]
		[Description("Xml Coalesced file.")]
		Xml,

        [Description("Unpacked LE Coalesced.")]
        ExtractedBin
	}
}
