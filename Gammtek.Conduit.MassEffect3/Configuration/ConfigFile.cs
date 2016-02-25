using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gammtek.Conduit.Extensions;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class ConfigFile : IDictionary<string, ConfigSection>, IEquatable<ConfigFile>
	{
		public const StringComparison DefaultStringComparison = StringComparison.InvariantCultureIgnoreCase;
		private readonly IDictionary<string, ConfigSection> _sections;

		public ConfigFile(IDictionary<string, ConfigSection> sections = null, StringComparison comparisonType = DefaultStringComparison)
		{
			_sections = sections ?? new Dictionary<string, ConfigSection>(comparisonType.GetStringComparer());
			ComparisonType = comparisonType;
		}

		public StringComparison ComparisonType { get; protected set; }

		public int Count
		{
			get { return _sections.Count; }
		}

		public bool IsReadOnly
		{
			get { return _sections.IsReadOnly; }
		}

		public ICollection<string> Keys
		{
			get { return _sections.Keys; }
		}

		public ICollection<ConfigSection> Values
		{
			get { return _sections.Values; }
		}

		public ConfigSection this[string key]
		{
			get
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				return _sections[key];
			}
			set
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				_sections[key] = value;
			}
		}

		public void Add(string key, ConfigSection value)
		{
			_sections.Add(key, value);
		}

		public void Add(KeyValuePair<string, ConfigSection> item)
		{
			_sections.Add(item);
		}

		public void Clear()
		{
			_sections.Clear();
		}

		public virtual void Combine(ConfigFile other)
		{
			if (other == null)
			{
				return;
			}

			foreach (var pair in other)
			{
				var otherName = pair.Key;
				var otherSection = pair.Value;
				ConfigSection section;

				if (!TryGetValue(otherName, out section))
				{
					section = new ConfigSection();
					Add(otherName, section);
				}

				section.Combine(otherSection);
			}
		}

		public bool Contains(KeyValuePair<string, ConfigSection> item)
		{
			return _sections.Contains(item);
		}

		public bool ContainsKey(string key)
		{
			return _sections.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, ConfigSection>[] array, int arrayIndex)
		{
			_sections.CopyTo(array, arrayIndex);
		}

		public bool Equals(ConfigFile other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			return ReferenceEquals(this, other) || _sections.SequenceEqual(other._sections);
		}

		public override bool Equals(object obj)
		{
			return obj is ConfigFile && Equals((ConfigFile) obj);
		}

		public IEnumerator<KeyValuePair<string, ConfigSection>> GetEnumerator()
		{
			return _sections.GetEnumerator();
		}

		public override int GetHashCode()
		{
			return _sections.GetHashCode();
		}

		public bool Remove(string key)
		{
			return _sections.Remove(key);
		}

		public bool Remove(KeyValuePair<string, ConfigSection> item)
		{
			return _sections.Remove(item);
		}

		public bool TryGetValue(string name, out ConfigSection section)
		{
			return _sections.TryGetValue(name, out section);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _sections).GetEnumerator();
		}
	}
}
