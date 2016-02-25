using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public class XmlConfigCache : ConfigCacheBase
	{
		public XmlConfigCache(IDictionary<string, ConfigFile> configFiles = null)
			: base(configFiles) {}

		public static XmlConfigCache Load(string uri)
		{
			if (uri == null)
			{
				throw new ArgumentNullException(nameof(uri));
			}

			var sourcePath = Path.GetFullPath(uri);

			if (!File.Exists(sourcePath))
			{
				Console.WriteLine(@"Warning: {0} not found!", uri);

				return null;
			}

			var doc = XDocument.Load(uri);

			var root = doc.Root;

			if (root == null)
			{
				return null;
			}

			var id = (string) root.Attribute("id");
			var name = (string) root.Attribute("name");
			var source = (string) root.Attribute("source");

			var result = new XmlConfigCache();

			return result;
		}

		public override void Save(string path)
		{
			throw new NotImplementedException();
		}

		public override void Save(Stream output)
		{
			throw new NotImplementedException();
		}
	}
}
