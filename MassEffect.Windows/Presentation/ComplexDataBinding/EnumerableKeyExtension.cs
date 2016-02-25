using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	[MarkupExtensionReturnType(typeof (string))]
	public class EnumerableKeyExtension : MarkupExtension
	{
		private static readonly Type GenericEnumerable = typeof (IEnumerable<object>).GetGenericTypeDefinition();

		public EnumerableKeyExtension() {}

		public EnumerableKeyExtension(string typeName)
		{
			TypeName = typeName;
		}

		public EnumerableKeyExtension(Type type)
		{
			Type = type;
		}

		public Type Type { get; set; }

		public string TypeName { get; set; }

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
			return new DataTemplateKey(GenericEnumerable.MakeGenericType(ParseType(serviceProvider)));
		}
	}
}
