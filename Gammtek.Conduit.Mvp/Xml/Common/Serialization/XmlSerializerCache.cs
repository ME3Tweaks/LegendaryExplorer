using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Gammtek.Conduit.Mvp.Xml.Common.Serialization
{
	public class XmlSerializerCache : IDisposable
	{
		private Dictionary<string, XmlSerializer> _serializers;

		private object _syncRoot;

		public XmlSerializerCache()
		{
			_syncRoot = new object();
			//stats = new PerfCounterManager();
			_serializers = new Dictionary<string, XmlSerializer>();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool isDisposing)
		{
			if (!isDisposing)
			{
				return;
			}

			//stats.Dispose();
			_syncRoot = null;
			_serializers = null;
		}

		public XmlSerializer GetSerializer(Type type, String defaultNamespace)
		{
			return GetSerializer(type, null, new Type[0], null, defaultNamespace);
		}

		public XmlSerializer GetSerializer(Type type, XmlRootAttribute root)
		{
			return GetSerializer(type, null, new Type[0], root, null);
		}

		public XmlSerializer GetSerializer(Type type, XmlAttributeOverrides overrides)
		{
			return GetSerializer(type, overrides, new Type[0], null, null);
		}

		public XmlSerializer GetSerializer(Type type, Type[] types)
		{
			return GetSerializer(type, null, types, null, null);
		}

		public XmlSerializer GetSerializer(Type type, XmlAttributeOverrides overrides, Type[] types, XmlRootAttribute root,
			String defaultNamespace)
		{
			var key = CacheKeyFactory.MakeKey(type, overrides, types, root, defaultNamespace);

			XmlSerializer serializer;

			var isCacheHit = false;

			if (!_serializers.TryGetValue(key, out serializer))
			{
				lock (_syncRoot)
				{
					if (!_serializers.TryGetValue(key, out serializer))
					{
						serializer = new XmlSerializer(type, overrides, types, root, defaultNamespace);

						_serializers.Add(key, serializer);

						if (null != NewSerializer)
						{
							NewSerializer(type, overrides, types, root, defaultNamespace);
						}
					}
					else
					{
						isCacheHit = true;
					}
				}
			}
			else
			{
				isCacheHit = true;
			}

			if (isCacheHit && CacheHit != null)
			{
				// Tell the listeners that we already 
				// had a serializer that matched the attributes

				CacheHit(type, overrides, types, root, defaultNamespace);
			}

			Debug.Assert(null != serializer);
			return serializer;
		}

		public event SerializerCacheDelegate NewSerializer;

		public event SerializerCacheDelegate CacheHit;

		~XmlSerializerCache()
		{
			Dispose(false);
		}
	}
}
