using System;
using System.Collections;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	public class BindingGroup : IEnumerable, IBindingGroup
	{
		public BindingGroup(IEnumerable items, string parameter)
		{
			Items = items;
			Parameter = parameter;
		}

		public string Parameter { get; private set; }

		public IEnumerable Items { get; private set; }

		public Type ElementType
		{
			get { return GetElementType(Items); }
		}

		public static Type GetElementType(IEnumerable enumerable)
		{
			var enumerableType = enumerable.GetType();
			Type elementType = null;

			if (enumerableType.IsGenericType)
			{
				var genericArguments = enumerableType.GetGenericArguments();

				if (genericArguments.Length > 0)
				{
					elementType = genericArguments[0];
				}
			}

			if (elementType != null)
			{
				return elementType;
			}

			var enumItems = enumerable.GetEnumerator();

			if (!enumItems.MoveNext())
			{
				return elementType;
			}

			if (enumItems.Current != null)
			{
				elementType = enumItems.Current.GetType();
			}

			return elementType;
		}

		public override string ToString()
		{
			return string.Format("{{BindingGroup of {0}}}", ElementType.FullName);
		}

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Items.GetEnumerator();
		}

		#endregion
	}
}
