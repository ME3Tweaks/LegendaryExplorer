using System;

namespace MassEffect2.SaveFormats
{
	public class OriginalNameAttribute : Attribute
	{
		public readonly string Name;

		public OriginalNameAttribute(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentNullException("name");
			}

			Name = name;
		}
	}
}