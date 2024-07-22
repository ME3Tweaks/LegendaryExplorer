using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Coalesced
{
	[DebuggerDisplay("CoalesceSection {Name} with {_properties.Count} unique property names")]
	public class CoalesceSection : IDictionary<string, CoalesceProperty>
	{
		private readonly IDictionary<string, CoalesceProperty> _properties;

		public CoalesceSection(string name = null, IDictionary<string, CoalesceProperty> properties = null)
		{
			_properties = properties ?? new CaseInsensitiveDictionary<CoalesceProperty>();
			Name = name ?? string.Empty;
		}

		public int Count
		{
			get { return _properties.Count; }
		}

		public bool IsReadOnly
		{
			get { return _properties.IsReadOnly; }
		}

		public ICollection<string> Keys
		{
			get { return _properties.Keys; }
		}

		public string Name { get; set; }

		public ICollection<CoalesceProperty> Values
		{
			get { return _properties.Values; }
		}

		public CoalesceProperty this[string key]
		{
			get { return _properties[key]; }
			set { _properties[key] = value; }
		}

		public void Add(KeyValuePair<string, CoalesceProperty> item)
		{
			_properties.Add(item);
		}

		public void Add(string key, CoalesceProperty value)
		{
			_properties.Add(key, value);
		}

		public void Clear()
		{
			_properties.Clear();
		}

		public void Combine(CoalesceSection other)
		{
			if (other == null)
			{
				throw new ArgumentNullException(nameof(other));
			}

			foreach (var pair in other)
			{
				var otherName = pair.Key;
				var otherProperty = pair.Value;

				foreach (var otherValue in otherProperty)
				{
					CoalesceProperty property;
					var value = otherValue.Value;
					var valueType = otherValue.ValueType;
					
					switch (valueType)
					{
						case -1: // Custom: Clear
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							property.Clear();
							property.Add(otherValue);

							break;
						}
						case 0: // New
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							property.Clear();
							property.Add(otherValue);

							break;
						}
						case 1: // RemoveProperty
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							property.Clear();
							property.Add(new CoalesceValue(value, valueType));

							break;
						}
						case 2: // Add
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							property.Add(new CoalesceValue(value, valueType));

							break;
						}
						case 3: // AddUnique
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							if (!property.Any(v => v.Equals(value) && v.ValueType != 4))
							{
								property.Add(new CoalesceValue(value, valueType));
							}

							break;
						}
						case 4: // Remove
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new CoalesceProperty(otherName);
								Add(otherName, property);
							}

							property.RemoveAll(v => v.Equals(value));
							property.Add(new CoalesceValue(value, valueType));

							break;
						}
					}
				}
			}
		}

		public bool Contains(KeyValuePair<string, CoalesceProperty> item)
		{
			return _properties.Contains(item);
		}

		public bool ContainsKey(string key)
		{
			return _properties.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, CoalesceProperty>[] array, int arrayIndex)
		{
			_properties.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<string, CoalesceProperty>> GetEnumerator()
		{
			return _properties.GetEnumerator();
		}

		public bool Remove(KeyValuePair<string, CoalesceProperty> item)
		{
			return _properties.Remove(item);
		}

		public bool Remove(string key)
		{
			return _properties.Remove(key);
		}

		public bool TryGetValue(string key, out CoalesceProperty value)
		{
			return _properties.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _properties).GetEnumerator();
		}

		/// <summary>
		/// Merges a property into the list of properties if it already exists, otherwise adding a new one.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
        public void AddEntry(CoalesceProperty value)
        {
            if (_properties.TryGetValue(value.Name, out var existing))
            {
                _properties[value.Name].AddRange(value);
            }
			else
            {
                _properties[value.Name] = value;
            }
		}

		/// <summary>
		/// Adds a value to a property if the property doesn't contain exist with the specified value
		/// </summary>
		/// <param name="value"></param>
        public void AddEntryIfUnique(CoalesceProperty value)
        {
            if (_properties.TryGetValue(value.Name, out var existing))
            {
				if (!_properties[value.Name].Contains(value[0]))
                {
                    _properties[value.Name].AddRange(value);
                }
            }
            else
            {
                _properties[value.Name] = value;
            }
		}

		/// <summary>
		/// Removes all properties with the specified name, or all if none is provided.
		/// </summary>
		/// <param name="asset"></param>
		/// <param name="keyName"></param>
        public void RemoveAllNamedEntries(string keyName = null)
        {
            if (keyName != null)
            {
                _properties.RemoveAll(x => x.Key == keyName);
            }
            else
            {
                _properties.Clear();
            }
        }
    }
}
