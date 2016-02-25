using System;
using System.Collections.Generic;
using System.Windows.Markup;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	[MarkupExtensionReturnType(typeof (Type))]
	public class EnumerableTypeExtension : TypeExtension
	{
		private static readonly Type GenericEnumerable = typeof (IEnumerable<object>).GetGenericTypeDefinition();

		public EnumerableTypeExtension() {}

		public EnumerableTypeExtension(string typeName)
			: base(typeName) {}

		public EnumerableTypeExtension(Type type)
			: base(type) {}

		private Type ParseType(IServiceProvider serviceProvider)
		{
			if (Type == null)
			{
				var xamlTypeResolver = serviceProvider.GetService(typeof (IXamlTypeResolver)) as IXamlTypeResolver;

				if (xamlTypeResolver != null)
				{
					return xamlTypeResolver.Resolve(TypeName);
				}
			}
			else
			{
				return Type;
			}
			return typeof (IEnumerable<object>);
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return GenericEnumerable.MakeGenericType(ParseType(serviceProvider));
		}
	}
}
