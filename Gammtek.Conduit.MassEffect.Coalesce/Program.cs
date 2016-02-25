using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Gammtek.Conduit.IO;
using Gammtek.Conduit.MassEffect.Coalesce.Xml;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Coalesced;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		private static string GetExePath()
		{
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}

		public void Main(string[] args)
		{
			var mode = Mode.Unknown;

			var showHelp = false;

			/*var options = new OptionSet
			{
				{
					"b|xml2bin",
					"convert xml to bin",
					v => mode = v != null ? Mode.XmlToBin : Mode.Unknown
				},
				{
					"x|bin2xml",
					"convert bin to xml",
					v => mode = v != null ? Mode.BinToXml : Mode.Unknown
				},
				{
					"h|help",
					"show this message and exit",
					v => showHelp = v != null
				}
			};*/

			/*args = new[]
			{
				@"C:\Users\Matthew\Documents\Gammtek\Mass Effect\Shared\GammtekMod\build\BioGame.bin",
				"-b",
				@"C:\Users\Matthew\Documents\Gammtek\Mass Effect\Shared\GammtekMod\src\Mass Effect 3\Coalesce\BioGame\Coalesced.xml",
				@"C:\Users\Matthew\Documents\Gammtek\Mass Effect\Shared\GammtekMod\build\BioGame.bin"
			};*/

			List<string> extras = new List<string>();

			/*try
			{
				extras = options.Parse(args);
			}
			catch (OptionException e)
			{
				Console.Write("{0}: ", GetExecutableName());
				Console.WriteLine(e.Message);
				Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());

				return;
			}*/

			if (mode == Mode.Unknown && extras.Count >= 1)
			{
				var testPath = extras[0];

				/*if (Directory.Exists(testPath))
				{
					mode = Mode.XmlToBin;
				}*/
				
				if (File.Exists(testPath))
				{
					if (testPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
					{
						mode = Mode.XmlToBin;
					}
					else if (testPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
					{
						mode = Mode.BinToXml;
					}
				}
			}

			if (extras.Count < 1 || extras.Count > 2 || showHelp || mode == Mode.Unknown)
			{
				Console.WriteLine("Usage: {0} [OPTIONS]+ -x input_bin [output_dir]", GetExecutableName());
				Console.WriteLine("       {0} [OPTIONS]+ -b input_dir [output_bin]", GetExecutableName());
				Console.WriteLine();
				Console.WriteLine("Options:");
				//options.WriteOptionDescriptions(Console.Out);

				return;
			}

			if (mode == Mode.BinToXml)
			{
				var inputPath = extras[0];
				var outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, null);

				//var inputPath = Path.IsPathRooted(extras[0]) ? extras[0] : Path.Combine(GetExePath(), extras[0]);
				//var outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, ".bin");

				if (!Path.IsPathRooted(outputPath))
				{
					outputPath = Path.Combine(GetExePath(), outputPath);
				}

				if (!File.Exists(inputPath))
				{
					return;
				}

				BuildSettings.Current.InputPath = inputPath;
				BuildSettings.Current.SourcePath = Path.GetDirectoryName(inputPath);
				BuildSettings.Current.OutputDirectory = outputPath;

				using (var input = File.OpenRead(inputPath))
				{
					var coal = new CoalescedFileXml();
					coal.Deserialize(input);

					var setup = new Setup
					{
						Endian = coal.ByteOrder,
						Version = coal.Version,
					};

					XDocument xDoc;
					XElement rootElement;

					foreach (var file in coal.Files)
					{
						var iniPath = string.Format("{0}", Path.GetFileNameWithoutExtension(file.Name));

						iniPath = Path.Combine(outputPath, Path.ChangeExtension(iniPath, ".xml"));

						var iniPathDir = Path.GetDirectoryName(iniPath);

						setup.Files.Add(Path.GetFileName(iniPath));

						if (iniPathDir != null)
						{
							Directory.CreateDirectory(iniPathDir);
						}

						xDoc = new XDocument();

						rootElement = new XElement("CoalesceAsset");
						rootElement.SetAttributeValue("id", Path.GetFileNameWithoutExtension(file.Name));
						rootElement.SetAttributeValue("name", Path.GetFileName(file.Name));
						rootElement.SetAttributeValue("source", file.Name);

						var sectionsElement = new XElement("Sections");

						foreach (var section in file.Sections)
						{
							var sectionElement = new XElement("Section");
							sectionElement.SetAttributeValue("name", section.Key);

							foreach (var property in section.Value)
							{
								var propertyElement = new XElement("Property");
								propertyElement.SetAttributeValue("name", property.Key);

								if (property.Value.Count > 1)
								{
									foreach (var value in property.Value)
									{
										var valueElement = new XElement("Value");
										valueElement.SetAttributeValue("type", value.Type);
										valueElement.SetValue(value.Value ?? "null");

										propertyElement.Add(valueElement);
									}
								}
								else if (property.Value.Count == 1)
								{
									propertyElement.SetAttributeValue("type", property.Value[0].Type);
									propertyElement.SetValue(property.Value[0].Value ?? "null");
								}
								else if (property.Value.Count == 0)
								{
									propertyElement.SetAttributeValue("type", CoalesceProperty.DefaultValueType);
									propertyElement.SetValue("");
								}

								sectionElement.Add(propertyElement);
							}

							sectionsElement.Add(sectionElement);
						}

						rootElement.Add(sectionsElement);
						xDoc.Add(rootElement);
						xDoc.Save(iniPath, SaveOptions.None);
					}

					Directory.CreateDirectory(outputPath);

					xDoc = new XDocument();

					rootElement = new XElement("CoalesceFile");
					rootElement.SetAttributeValue("id", "");
					rootElement.SetAttributeValue("name", "");
					rootElement.SetAttributeValue("source", "");

					var assetsElement = new XElement("Assets");

					foreach (var file in setup.Files)
					{
						var assetElement = new XElement("Asset");
						assetElement.SetAttributeValue("source", file);

						assetsElement.Add(assetElement);
					}

					rootElement.Add(assetsElement);
					xDoc.Add(rootElement);
					xDoc.Save(Path.Combine(outputPath, "Coalesced.xml"), SaveOptions.None);
				}
			}
			else
			{
				var inputPath = Path.IsPathRooted(extras[0]) ? extras[0] : Path.Combine(GetExePath(), extras[0]);
				var outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, ".bin");

				if (!Path.IsPathRooted(outputPath))
				{
					outputPath = Path.Combine(GetExePath(), outputPath);
				}

				if (!File.Exists(inputPath))
				{
					return;
				}

				BuildSettings.Current.InputPath = inputPath;
				BuildSettings.Current.SourcePath = Path.GetDirectoryName(inputPath);
				BuildSettings.Current.OutputDirectory = outputPath;

				var file = XmlCoalesceFile.Load(extras[0]);

				var setup = new Setup
				{
					Endian = ByteOrder.LittleEndian,
					Version = 1
				};

				var coal = new CoalescedFileXml
				{
					ByteOrder = setup.Endian,
					Version = 1
				};

				foreach (var asset in file.Documents)
				{
					var entry = new FileEntry(asset.Source)
					{
						Sections = new Dictionary<string, Dictionary<string, List<PropertyValue>>>()
					};

					foreach (var section in asset.Sections)
					{
						var eSection = new Dictionary<string, List<PropertyValue>>();

						foreach (var property in section.Value)
						{
							var eProperty = new List<PropertyValue>();

							foreach (var value in property.Value)
							{
								if (!file.Settings.CompileTypes.Contains(value.ValueType))
								{
									continue;
								}

								eProperty.Add(new PropertyValue(value.ValueType, value.Value));
							}

							eSection.Add(property.Key, eProperty);
						}

						entry.Sections.Add(section.Key, eSection);
					}

					coal.Files.Add(entry);
				}

				using (var output = File.Create(outputPath))
				{
					if (file.Settings != null)
					{
						coal.OverrideCompileValueTypes = file.Settings.OverrideCompileValueTypes;
						coal.CompileTypes = file.Settings.CompileTypes;
					}

					coal.Serialize(output);
				}
			}
		}
	}
}
