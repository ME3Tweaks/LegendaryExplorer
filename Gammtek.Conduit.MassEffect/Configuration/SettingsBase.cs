using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Gammtek.Conduit.ComponentModel;

// ReSharper disable once ExplicitCallerInfoArgument

namespace Gammtek.Conduit.MassEffect.Configuration
{
	public abstract class SettingsBase<T> : BindableBase
	{
		protected SettingsBase(IDictionary<string, T> properties = null)
		{
			Properties = properties ?? new SortedList<string, T>();
		}

		public T this[string name]
		{
			get { return GetValue(name); }
			set { SetValue(value, name); }
		}

		public ICollection<string> Keys
		{
			get { return Properties.Keys; }
		}

		public IDictionary<string, T> Properties { get; protected set; }

		public ICollection<T> Values
		{
			get { return Properties.Values; }
		}

		protected T GetValue(string propertyName = null)
		{
			if (propertyName == null)
			{
				throw new ArgumentNullException("propertyName");
			}

			T result;

			Properties.TryGetValue(propertyName, out result);

			return result;
		}

		[NotifyPropertyChangedInvocator]
		protected bool SetValue(T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(Properties[propertyName], value))
			{
				return false;
			}

			Properties[propertyName] = value;
			NotifyOfPropertyChange(propertyName);

			return true;
		}
	}

	public class SettingsBase : SettingsBase<object>
	{
		protected T GetValue<T>([CallerMemberName] string propertyName = null)
		{
			return (T) GetValue(propertyName);
		}

		[NotifyPropertyChangedInvocator]
		protected bool SetValue<T>(T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(Properties[propertyName], value))
			{
				return false;
			}

			Properties[propertyName] = value;
			NotifyOfPropertyChange(propertyName);

			return true;
		}
	}
}
