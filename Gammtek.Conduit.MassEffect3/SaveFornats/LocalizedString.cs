using System;
using System.Reflection;

namespace MassEffect3.SaveFormats
{
	internal class LocalizedString
	{
		private Func<string> _CachedValue;
		private string _PropertyName;
		private Type _ResourceType;

		public Type ResourceType
		{
			get { return _ResourceType; }
			set
			{
				if (_ResourceType == value)
				{
					return;
				}

				ResetCache();
				_ResourceType = value;
			}
		}

		public string PropertyName
		{
			get { return _PropertyName; }
			set
			{
				if (_PropertyName == value)
				{
					return;
				}

				ResetCache();
				_PropertyName = value;
			}
		}

		private void ResetCache()
		{
			_CachedValue = null;
		}

		public string GetLocalizedValue()
		{
			if (_CachedValue != null)
			{
				return _CachedValue();
			}

			if (_ResourceType == null ||
				_PropertyName == null)
			{
				_CachedValue = () => null;
				return _CachedValue();
			}

			if (_ResourceType.IsVisible == false)
			{
				_CachedValue =
					() =>
					{
						throw new InvalidOperationException(string.Format("{0} is not visible",
							_ResourceType.FullName));
					};
				return _CachedValue();
			}

			var property = _ResourceType.GetProperty(_PropertyName);
			if (property == null)
			{
				/*
                this._CachedValue =
                    () =>
                    {
                        throw new InvalidOperationException(string.Format("{0} does not have a public property {1}",
                                                                          this._ResourceType.FullName,
                                                                          this._PropertyName));
                    };
                */
				_CachedValue = () => null;
				return _CachedValue();
			}

			if (property.PropertyType != typeof (string))
			{
				_CachedValue =
					() =>
					{
						throw new InvalidOperationException(string.Format("{0} {1} is not a string",
							_ResourceType.FullName,
							property.Name));
					};
				return _CachedValue();
			}

			var getMethod = property.GetGetMethod();
			if (getMethod == null ||
				getMethod.IsPublic == false ||
				getMethod.IsStatic == false)
			{
				_CachedValue =
					() =>
					{
						throw new InvalidOperationException(string.Format("{0} {1} getter is not public",
							_ResourceType.FullName,
							property.Name));
					};
				return _CachedValue();
			}

			_CachedValue = () => (string) property.GetValue(null, null);
			return _CachedValue();
		}
	}
}