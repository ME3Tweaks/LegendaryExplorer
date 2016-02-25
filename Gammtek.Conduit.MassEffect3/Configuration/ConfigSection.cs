using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class ConfigSection : IDictionary<string, ConfigProperty>, IEquatable<ConfigSection>
	{
		private readonly IDictionary<string, ConfigProperty> _properties;

		public ConfigSection(IDictionary<string, ConfigProperty> properties = null, StringComparison comparisonType = ConfigFile.DefaultStringComparison)
		{
			_properties = properties ?? new Dictionary<string, ConfigProperty>(comparisonType.GetStringComparer());
			ComparisonType = comparisonType;
		}

		public StringComparison ComparisonType { get; protected set; }

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

		public ICollection<ConfigProperty> Values
		{
			get { return _properties.Values; }
		}

		public ConfigProperty this[string key]
		{
			get
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				return _properties[key];
			}
			set
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				_properties[key] = value;
			}
		}

		public void Add(KeyValuePair<string, ConfigProperty> item)
		{
			_properties.Add(item);
		}

		public void Add(string name, ConfigProperty property)
		{
			_properties.Add(name, property);
		}

		public void Clear()
		{
			_properties.Clear();
		}

		public virtual void Combine(ConfigSection other)
		{
			if (other == null)
			{
				return;
			}

			foreach (var pair in other)
			{
				var otherName = pair.Key;
				var otherProperty = pair.Value;

				foreach (var configValue in otherProperty)
				{
					ConfigProperty property;
					var value = configValue.Value;
					var parseAction = configValue.ParseAction;

					switch (parseAction)
					{
						case ConfigParseAction.Add:
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new ConfigProperty();
								Add(otherName, property);
							}

							property.Add(configValue);

							break;
						}
						case ConfigParseAction.AddUnique:
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new ConfigProperty();
								Add(otherName, property);
							}

							property.AddUnique(configValue);

							break;
						}
						case ConfigParseAction.New:
						{
							if (!TryGetValue(otherName, out property))
							{
								property = new ConfigProperty();
								Add(otherName, property);
							}

							property.Clear();
							property.Add(configValue);

							break;
						}
						case ConfigParseAction.Remove:
						{
							if (TryGetValue(otherName, out property))
							{
								//var values = property.Where(v => v.Equals(value));
								property.RemoveAll(v => v.Equals(value));

								/*foreach (var v in values)
								{
									property.Remove(v);
								}*/
							}

							break;
						}
						case ConfigParseAction.RemoveProperty:
						{
							Remove(otherName);

							break;
						}
					}
				}
			}
		}

		public bool Contains(KeyValuePair<string, ConfigProperty> item)
		{
			return _properties.Contains(item);
		}

		public bool ContainsKey(string key)
		{
			return _properties.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, ConfigProperty>[] array, int arrayIndex)
		{
			_properties.CopyTo(array, arrayIndex);
		}

		public bool Equals(ConfigSection other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			return ReferenceEquals(this, other) || _properties.SequenceEqual(other._properties);
		}

		public override bool Equals(object obj)
		{
			return obj is ConfigSection && Equals((ConfigSection) obj);
		}

		public IEnumerator<KeyValuePair<string, ConfigProperty>> GetEnumerator()
		{
			return _properties.GetEnumerator();
		}

		public override int GetHashCode()
		{
			return _properties.GetHashCode();
		}

		public bool Remove(KeyValuePair<string, ConfigProperty> item)
		{
			return _properties.Remove(item);
		}

		public bool Remove(string name)
		{
			return _properties.Remove(name);
		}

		public bool TryGetValue(string name, out ConfigProperty property)
		{
			return _properties.TryGetValue(name, out property);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _properties).GetEnumerator();
		}
	}
}
