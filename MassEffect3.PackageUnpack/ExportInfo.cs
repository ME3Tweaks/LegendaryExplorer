using System.IO;

namespace MassEffect3.PackageUnpack
{
	internal class ExportInfo : IResource
	{
		public IResource Class;
		public uint DataOffset;
		public uint DataSize;
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
					name = Path.Combine(Outer.LocalPath, name);
				}
				return name;
			}
		}

		public string FullPath
		{
			get { return Path.Combine(PackageName, LocalPath); }
		}

		public override string ToString()
		{
			return ObjectName;
		}
	}
}