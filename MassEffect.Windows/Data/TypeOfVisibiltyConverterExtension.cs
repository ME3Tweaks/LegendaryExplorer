using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MassEffect.Windows.Data
{
	[ValueConversion(typeof(Type), typeof(Visibility))]
	public class TypeOfVisibiltyConverterExtension : MarkupExtension
	{
		private TypeExtension _targetTypeExtension;

		public TypeOfVisibiltyConverterExtension()
		{
			_targetTypeExtension = new TypeExtension();
		}

		public TypeOfVisibiltyConverterExtension(Type targetType)
		{
			_targetTypeExtension = new TypeExtension(targetType);
		}

		[ConstructorArgument("targetType")]
		public Type TargetType
		{
			get { return _targetTypeExtension.Type; }
			set { _targetTypeExtension.Type = value; }
		}

		[ConstructorArgument("targetTypeName")]
		public string TargetTypeName
		{
			get { return _targetTypeExtension.TypeName; }
			set { _targetTypeExtension.TypeName = value; }
		}

		#region Overrides of MarkupExtension

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			Type targetType = null;

			if ((_targetTypeExtension.Type != null) || (_targetTypeExtension.TypeName != null))
			{
				targetType = _targetTypeExtension.ProvideValue(serviceProvider) as Type;
			}

			// just let the TypeExtensions do the type resolving via the service provider
			return new TypeOfVisibiltyConverter(targetType);
		}

		#endregion
	}
}
