using System;
using System.IO;
using System.Reflection;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public sealed class BuildSettings
	{
		private static readonly Lazy<BuildSettings> CurrentInstance = new Lazy<BuildSettings>(() => new BuildSettings());

		public BuildSettings(string assetNamespace = "")
		{
			AssetNamespace = assetNamespace ?? "uri:gammtek:conduit:masseffect:coalesce";
			ExePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
		}

		public string AssetNamespace { get; set; }

		public string BuildConfigurationName { get; set; }

		public string DataPath { get; set; }

		public string[] DataPaths { get; set; }

		public string DataRoot { get; set; }

		public string DefaultDataPaths { get; set; }

		public string ExePath { get; set; }

		public Uri ExeUri { get; set; }

		public string InputPath { get; set; }

		public string OutputDirectory { get; set; }

		public string OutputPath { get; set; }

		public string SchemaPath { get; set; }

		public string SourceDirectory { get; set; }

		public string SourcePath { get; set; }

		public static BuildSettings Current
		{
			get { return CurrentInstance.Value; }
		}
	}
}
