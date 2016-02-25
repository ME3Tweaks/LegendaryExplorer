using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public abstract class ConfigCacheBase : IDictionary<string, ConfigFile>
	{
		private readonly IDictionary<string, ConfigFile> _configFiles;

		protected ConfigCacheBase(IDictionary<string, ConfigFile> configFiles = null)
		{
			_configFiles = configFiles ?? new Dictionary<string, ConfigFile>();
		}

		public int Count
		{
			get { return _configFiles.Count; }
		}

		public bool IsReadOnly
		{
			get { return _configFiles.IsReadOnly; }
		}

		public ICollection<string> Keys
		{
			get { return _configFiles.Keys; }
		}

		public ICollection<ConfigFile> Values
		{
			get { return _configFiles.Values; }
		}

		public ConfigFile this[string key]
		{
			get
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				return _configFiles[key];
			}
			set
			{
				if (key == null)
				{
					throw new ArgumentNullException(nameof(key));
				}

				_configFiles[key] = value;
			}
		}

		public void Add(KeyValuePair<string, ConfigFile> item)
		{
			_configFiles.Add(item);
		}

		public void Add(string key, ConfigFile value)
		{
			_configFiles.Add(key, value);
		}

		public void Clear()
		{
			_configFiles.Clear();
		}

		public virtual void Combine(ConfigCacheBase other)
		{
			if (other == null)
			{
				return;
			}

			foreach (var pair in other)
			{
				var otherName = pair.Key;
				var otherConfigFile = pair.Value;

				ConfigFile configFile;

				if (!TryGetValue(otherName, out configFile))
				{
					configFile = new ConfigFile();
					Add(otherName, configFile);
				}

				configFile.Combine(otherConfigFile);
			}
		}

		public bool Contains(KeyValuePair<string, ConfigFile> item)
		{
			return _configFiles.Contains(item);
		}

		public bool ContainsKey(string key)
		{
			return _configFiles.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, ConfigFile>[] array, int arrayIndex)
		{
			_configFiles.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<string, ConfigFile>> GetEnumerator()
		{
			return _configFiles.GetEnumerator();
		}

		public bool Remove(KeyValuePair<string, ConfigFile> item)
		{
			return _configFiles.Remove(item);
		}

		public bool Remove(string key)
		{
			return _configFiles.Remove(key);
		}

		public abstract void Save(string path);

		public abstract void Save(Stream output);

		public bool TryGetValue(string key, out ConfigFile value)
		{
			return _configFiles.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _configFiles).GetEnumerator();
		}
	}
}
