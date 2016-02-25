using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gammtek.Conduit.Extensions.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class ConfigProperty : IList<ConfigValue>, IEquatable<ConfigProperty>
	{
		private readonly IList<ConfigValue> _values;

		public ConfigProperty(IList<ConfigValue> values = null, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
		{
			_values = values ?? new List<ConfigValue>();
			ComparisonType = comparisonType;
		}

		public ConfigProperty(IEnumerable<ConfigValue> values, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
		{
			if (values == null)
			{
				throw new ArgumentNullException(nameof(values));
			}

			_values = new List<ConfigValue>(values);
			ComparisonType = comparisonType;
		}

		public StringComparison ComparisonType { get; protected set; }

		public int Count
		{
			get { return _values.Count; }
		}

		public bool IsReadOnly
		{
			get { return _values.IsReadOnly; }
		}

		public ConfigValue this[int index]
		{
			get
			{
				if ((uint) index >= (uint) _values.Count)
				{
					throw new ArgumentOutOfRangeException(nameof(index));
				}

				return _values[index];
			}
			set
			{
				if ((uint) index >= (uint) _values.Count)
				{
					throw new ArgumentOutOfRangeException(nameof(index));
				}

				_values[index] = value;
			}
		}

		public void Add(ConfigValue item)
		{
			_values.Add(item);
		}

		public void AddUnique(ConfigValue value)
		{
			if (_values.Contains(value))
			{
				return;
			}

			Add(value);
		}

		public void Clear()
		{
			_values.Clear();
		}

		public bool Contains(ConfigValue item)
		{
			return _values.Contains(item);
		}

		public void CopyTo(ConfigValue[] array, int arrayIndex)
		{
			_values.CopyTo(array, arrayIndex);
		}

		public override bool Equals(object obj)
		{
			return obj is ConfigProperty && Equals((ConfigProperty) obj);
		}

		public bool Equals(ConfigProperty other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			return ReferenceEquals(this, other) || _values.SequenceEqual(other._values);
		}

		public ConfigValue Find(ConfigValue value)
		{
			return _values.FirstOrDefault(configValue => configValue == value);
		}

		public IEnumerable<ConfigValue> FindAll(ConfigValue value)
		{
			return _values.Where(configValue => configValue == value);
		}

		public int FindIndex(Func<ConfigValue, bool> predicate)
		{
			return _values.FindIndex(predicate);
		}

		public ConfigValue FindLast(ConfigValue value)
		{
			return _values.LastOrDefault(configValue => configValue == value);
		}

		public IEnumerator<ConfigValue> GetEnumerator()
		{
			return _values.GetEnumerator();
		}

		public override int GetHashCode()
		{
			return _values.GetHashCode();
		}

		public int IndexOf(ConfigValue item)
		{
			return _values.IndexOf(item);
		}

		public void Insert(int index, ConfigValue item)
		{
			_values.Insert(index, item);
		}

		public bool Remove(ConfigValue item)
		{
			return _values.Remove(item);
		}

		public int RemoveAll(ConfigValue item)
		{
			return _values.RemoveAll(item);
		}

		public void RemoveAt(int index)
		{
			_values.RemoveAt(index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _values).GetEnumerator();
		}
	}
}
