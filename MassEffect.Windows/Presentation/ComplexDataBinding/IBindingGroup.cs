using System;
using System.Collections;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	public interface IBindingGroup
	{
		Type ElementType { get; }

		IEnumerable Items { get; }

		string Parameter { get; }
	}
}
