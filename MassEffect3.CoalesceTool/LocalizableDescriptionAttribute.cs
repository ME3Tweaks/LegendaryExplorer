using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace MassEffect3.CoalesceTool
{
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class LocalizableDescriptionAttribute : DescriptionAttribute
	{
		private readonly Type _resourcesType;
		private bool _isLocalized;

		public LocalizableDescriptionAttribute
			(string description, Type resourcesType)
			: base(description)
		{
			_resourcesType = resourcesType;
		}

		public override string Description
		{
			get
			{
				if (_isLocalized)
				{
					return DescriptionValue;
				}

				var resMan =
					_resourcesType.InvokeMember(
						@"ResourceManager",
						BindingFlags.GetProperty | BindingFlags.Static |
						BindingFlags.Public | BindingFlags.NonPublic,
						null,
						null,
						new object[] {}) as ResourceManager;

				var culture =
					_resourcesType.InvokeMember(
						@"Culture",
						BindingFlags.GetProperty | BindingFlags.Static |
						BindingFlags.Public | BindingFlags.NonPublic,
						null,
						null,
						new object[] {}) as CultureInfo;

				_isLocalized = true;

				if (resMan != null)
				{
					DescriptionValue =
						resMan.GetString(DescriptionValue, culture);
				}

				return DescriptionValue;
			}
		}
	}
}
