using System.IO;

namespace MassEffect3.PackageUnpack
{
	internal class ImportInfo : IResource
	{
		public string ObjectName;
		public IResource Outer;
		public string PackageName;

		public string LocalName
		{
			get
			{
				string name = ObjectName;
				if (Outer != null)
				{
					name = Outer.LocalName + "." + name;
				}
				return name;
			}
		}

		public string FullName
		{
			get { return PackageName + "." + LocalName; }
		}

		public string LocalPath
		{
			get
			{
				string name = ObjectName;
				if (name.ToLowerInvariant() == "con")
				{
					name += "_";
				}
				if (Outer != null)
				{
					name = Path.Combine(Outer.LocalName, name);
				}
				return name;
			}
		}

		public string FullPath
		{
			get { return Path.Combine(PackageName, LocalName); }
		}

		public override string ToString()
		{
			return ObjectName;
		}
	}
}