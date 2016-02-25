using System;

namespace MassEffect3.SaveFormats
{
	public class OriginalNameAttribute : Attribute
	{
		public readonly string Name;

		public OriginalNameAttribute(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentNullException(nameof(name));
			}

			Name = name;
		}
	}
}