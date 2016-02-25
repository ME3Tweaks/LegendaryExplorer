using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Gammtek.Conduit.CommandLine;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.IO;
using MassEffect3.Coalesce.Xml;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Coalesced;

namespace MassEffect3.Coalesce
{
	public static class Program
	{
		public static readonly SortedSet<string> ProperNames =
			new SortedSet<string>
			{
				"BioAI",
				"BioCompat",
				"BioCredits",
				"BioDifficulty",
				"BioEngine",
				"BioGame",
				"BioInput",
				"BioLightmass",
				"BioTest",
				"BioUI",
				"BioWeapon",
				"Core",
				"Descriptions",
				"EditorTips",
				"Engine",
				"GFxUI",
				"IpDrv",
				"Launch",
				"OnlineSubsystemGamespy",
				"Startup",
				"Subtitles",
				"UnrealEd",
				"WinDrv",
				"XWindow"
			};

		public static readonly Dictionary<string, string> SpecialCharacters =
			new Dictionary<string, string>
			{
				{ "\t", "\\t" },
				{ "\r", "\\r" },
				{ "\n", "\\n" }
			};

		public static void Main(string[] args)
		{
			/*var helpWriter = new StringWriter();
			var parser = new Parser(
				settings =>
				{
					settings.EnableDashDash = true;
					settings.HelpWriter = helpWriter;
				});

			var result = parser.ParseArguments<ProgramOptions>(args);
			var options = result.Value;*/
			var parser = ProgramOptions.Create(args);
			var result = parser.Parse(args);

			if (result.HasErrors)
			{
				parser.HelpOption.ShowHelp(parser.Options);

				return;
			}

			var options = parser.Object;

			if (options.CoalesceMode == ProgramCoalesceMode.Unknown && !options.Source.IsNullOrWhiteSpace())
			{
				if (File.Exists(options.Source))
				{
					if (options.Source.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
					{
						options.CoalesceMode = ProgramCoalesceMode.ToBin;
					}
					else if (options.Source.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
					{
						options.CoalesceMode = ProgramCoalesceMode.ToXml;
					}
				}
			}

			if (options.CoalesceMode == ProgramCoalesceMode.ToXml)
			{
				var sourcePath = options.Source;
				var destinationPath = options.Destination;

				if (destinationPath.IsNullOrWhiteSpace())
				{
					destinationPath = Path.ChangeExtension(sourcePath, null);
				}

				if (!Path.IsPathRooted(destinationPath))
				{
					destinationPath = Path.Combine(GetExePath(), destinationPath);
				}

				if (!File.Exists(sourcePath))
				{
					return;
				}

				BuildSettings.Current.SourcePath = sourcePath;
				BuildSettings.Current.SourceDirectory = Path.GetDirectoryName(sourcePath);
				BuildSettings.Current.OutputPath = destinationPath;
				BuildSettings.Current.OutputDirectory = Path.GetDirectoryName(destinationPath);

				using (var input = File.OpenRead(sourcePath))
				{
					var coal = new CoalescedFileXml();
					coal.Deserialize(input);

					var inputId = Path.GetFileNameWithoutExtension(sourcePath) ?? "Coalesced";
					var inputName = Path.GetFileName(sourcePath) ?? "Coalesced.bin";

					var setup =
						new Setup
						{
							Endian = coal.ByteOrder,
							Version = coal.Version
						};

					XDocument xDoc;
					XElement rootElement;

					if (!Directory.Exists(destinationPath))
					{
						Directory.CreateDirectory(destinationPath);
					}

					foreach (var file in coal.Files)
					{
						var fileId = Path.GetFileNameWithoutExtension(file.Name);
						fileId = ProperNames.FirstOrDefault(s => s.Equals(fileId, StringComparison.InvariantCultureIgnoreCase));

						var iniPath = string.Format("{0}/{1}.xml", destinationPath, fileId);

						setup.Files.Add(Path.GetFileName(iniPath));

						xDoc = new XDocument();

						rootElement = new XElement("CoalesceAsset");
						rootElement.SetAttributeValue("id", fileId);
						rootElement.SetAttributeValue("name", Path.GetFileName(file.Name));
						rootElement.SetAttributeValue("source", file.Name);

						var sectionsElement = new XElement("Sections");

						foreach (var section in file.Sections)
						{
							var sectionElement = new XElement("Section");
							sectionElement.SetAttributeValue("name", section.Key);

							//
							//var classes = Namespace.FromStrings(section.Value.Keys);

							//

							foreach (var property in section.Value)
							{
								var propertyElement = new XElement("Property");
								propertyElement.SetAttributeValue("name", property.Key);

								if (property.Value.Count > 1)
								{
									foreach (var value in property.Value)
									{
										var valueElement = new XElement("Value");
										var propertyValue = value.Value;
										valueElement.SetAttributeValue("type", value.Type);

										if (!propertyValue.IsNullOrWhiteSpace())
										{
											propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
										}

										valueElement.SetValue(propertyValue ?? "null");

										propertyElement.Add(valueElement);
									}
								}
								else
								{
									switch (property.Value.Count)
									{
										case 1:
										{
											propertyElement.SetAttributeValue("type", property.Value[0].Type);
											var propertyValue = property.Value[0].Value;

											if (!propertyValue.IsNullOrWhiteSpace())
											{
												propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
											}

											propertyElement.SetValue(propertyValue ?? "null");
											break;
										}
										case 0:
										{
											propertyElement.SetAttributeValue("type", CoalesceProperty.DefaultValueType);
											propertyElement.SetValue("");
											break;
										}
									}
								}

								sectionElement.Add(propertyElement);
							}

							sectionsElement.Add(sectionElement);
						}

						rootElement.Add(sectionsElement);
						xDoc.Add(rootElement);

						//
						using (var writer = new XmlTextWriter(iniPath, Encoding.UTF8))
						{
							writer.IndentChar = '\t';
							writer.Indentation = 1;
							writer.Formatting = Formatting.Indented;

							xDoc.Save(writer);
						}

						//

						//xDoc.Save(iniPath, SaveOptions.None);
					}

					xDoc = new XDocument();

					rootElement = new XElement("CoalesceFile");
					rootElement.SetAttributeValue("id", inputId);
					rootElement.SetAttributeValue("name", inputName);

					//rootElement.SetAttributeValue("Source", "");

					var assetsElement = new XElement("Assets");

					foreach (var file in setup.Files)
					{
						var assetElement = new XElement("Asset");
						var path = string.Format("{0}", file);
						assetElement.SetAttributeValue("source", path);

						assetsElement.Add(assetElement);
					}

					rootElement.Add(assetsElement);
					xDoc.Add(rootElement);

					//
					using (var writer = new XmlTextWriter(Path.Combine(destinationPath, string.Format("{0}.xml", inputId)), Encoding.UTF8))
					{
						writer.IndentChar = '\t';
						writer.Indentation = 1;
						writer.Formatting = Formatting.Indented;

						xDoc.Save(writer);
					}

					//

					//xDoc.Save(Path.Combine(destinationPath, string.Format("{0}.xml", inputId)), SaveOptions.None);
				}
			}
			else if (options.CoalesceMode == ProgramCoalesceMode.ToXmlNamed)
			{
				var sourcePath = options.Source;
				var destinationPath = options.Destination;

				if (destinationPath.IsNullOrWhiteSpace())
				{
					destinationPath = Path.ChangeExtension(sourcePath, null);
				}

				if (!Path.IsPathRooted(destinationPath))
				{
					destinationPath = Path.Combine(GetExePath(), destinationPath);
				}

				if (!File.Exists(sourcePath))
				{
					return;
				}

				BuildSettings.Current.SourcePath = sourcePath;
				BuildSettings.Current.SourceDirectory = Path.GetDirectoryName(sourcePath);
				BuildSettings.Current.OutputPath = destinationPath;
				BuildSettings.Current.OutputDirectory = Path.GetDirectoryName(destinationPath);

				using (var input = File.OpenRead(sourcePath))
				{
					var coal = new CoalescedFileXml();
					coal.Deserialize(input);

					var inputId = Path.GetFileNameWithoutExtension(sourcePath) ?? "Coalesced";
					var inputName = Path.GetFileName(sourcePath) ?? "Coalesced.bin";

					var setup =
						new Setup
						{
							Endian = coal.ByteOrder,
							Version = coal.Version
						};

					XDocument xDoc;
					XElement rootElement;

					if (!Directory.Exists(destinationPath))
					{
						Directory.CreateDirectory(destinationPath);
					}

					foreach (var file in coal.Files)
					{
						var fileId = Path.GetFileNameWithoutExtension(file.Name);
						fileId = ProperNames.FirstOrDefault(s => s.Equals(fileId, StringComparison.InvariantCultureIgnoreCase));

						var iniPath = string.Format("{0}/{1}.xml", destinationPath, fileId);

						setup.Files.Add(Path.GetFileName(iniPath));

						//
						// Name, IsClass, IsProperty, IsValue
						var sectionKeys = new SortedSet<Tuple<string, bool, bool, bool>>();

						// section (class) -> property ->
						foreach (var sectionClass in file.Sections)
						{
							var className = sectionClass.Key.Replace(" ", "--");

							sectionKeys.Add(new Tuple<string, bool, bool, bool>(className, true, false, false));

							foreach (var classProperty in sectionClass.Value)
							{
								var propertyName = classProperty.Key;

								if (propertyName.Length > 0 && propertyName[0].IsDigit())
								{
									propertyName = propertyName.Insert(0, "__") + "__";
								}

								//propertyName = string.Format("{0}.{1}", className, classProperty.Key);
								propertyName = string.Format("{0}.{1}", className, propertyName);

								sectionKeys.Add(new Tuple<string, bool, bool, bool>(propertyName, false, true, false));

								/*foreach (var propertyValue in classProperty.Value)
								{
									var valueName = string.Format("{0}=>{1}", propertyName, propertyValue.Value);

									//sectionKeys.Add(new Tuple<string, bool, bool, bool>(valueName, false, false, true));
								}*/
							}
						}

						var elementNames = Namespace.FromStrings(sectionKeys.Select(tuple => tuple.Item1));
						var elements = new XElement("BioGame");
						Namespace.ToXElements(elements, elementNames);
						elements.Save("Sections.xml");

						//

						xDoc = new XDocument();

						rootElement = new XElement("CoalesceAsset");
						rootElement.SetAttributeValue("id", fileId);
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
										var propertyValue = value.Value;
										valueElement.SetAttributeValue("type", value.Type);

										if (!propertyValue.IsNullOrWhiteSpace())
										{
											propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
										}

										valueElement.SetValue(propertyValue ?? "null");

										propertyElement.Add(valueElement);
									}
								}
								else
								{
									switch (property.Value.Count)
									{
										case 1:
										{
											propertyElement.SetAttributeValue("type", property.Value[0].Type);
											var propertyValue = property.Value[0].Value;

											if (!propertyValue.IsNullOrWhiteSpace())
											{
												propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
											}

											propertyElement.SetValue(propertyValue ?? "null");
											break;
										}
										case 0:
										{
											propertyElement.SetAttributeValue("type", CoalesceProperty.DefaultValueType);
											propertyElement.SetValue("");
											break;
										}
									}
								}

								sectionElement.Add(propertyElement);
							}

							sectionsElement.Add(sectionElement);
						}

						rootElement.Add(sectionsElement);
						xDoc.Add(rootElement);

						//
						using (var writer = new XmlTextWriter(iniPath, Encoding.UTF8))
						{
							writer.IndentChar = '\t';
							writer.Indentation = 1;
							writer.Formatting = Formatting.Indented;

							xDoc.Save(writer);
						}

						//

						//xDoc.Save(iniPath, SaveOptions.None);
					}

					xDoc = new XDocument();

					rootElement = new XElement("CoalesceFile");
					rootElement.SetAttributeValue("id", inputId);
					rootElement.SetAttributeValue("name", inputName);

					//rootElement.SetAttributeValue("Source", "");

					var assetsElement = new XElement("Assets");

					foreach (var file in setup.Files)
					{
						var assetElement = new XElement("Asset");
						var path = string.Format("{0}", file);
						assetElement.SetAttributeValue("source", path);

						assetsElement.Add(assetElement);
					}

					rootElement.Add(assetsElement);
					xDoc.Add(rootElement);

					//
					using (var writer = new XmlTextWriter(Path.Combine(destinationPath, string.Format("{0}.xml", inputId)), Encoding.UTF8))
					{
						writer.IndentChar = '\t';
						writer.Indentation = 1;
						writer.Formatting = Formatting.Indented;

						xDoc.Save(writer);
					}

					//

					//xDoc.Save(Path.Combine(destinationPath, string.Format("{0}.xml", inputId)), SaveOptions.None);
				}
			}
			else if (options.CoalesceMode == ProgramCoalesceMode.ToCSharp) {}
			else
			{
				var inputPath = Path.IsPathRooted(options.Source) ? options.Source : Path.Combine(GetExePath(), options.Source);
				var outputPath = !options.Destination.IsNullOrWhiteSpace() ? options.Destination : Path.ChangeExtension(inputPath, ".bin");

				if (!Path.IsPathRooted(outputPath))
				{
					outputPath = Path.Combine(GetExePath(), outputPath);
				}

				if (!File.Exists(inputPath))
				{
					return;
				}

				BuildSettings.Current.SourcePath = inputPath;
				BuildSettings.Current.SourceDirectory = Path.GetDirectoryName(inputPath);
				BuildSettings.Current.OutputPath = outputPath;
				BuildSettings.Current.OutputDirectory = outputPath;

				var file = XmlCoalesceFile.Load(inputPath);

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

				foreach (var asset in file.Assets)
				{
					var entry =
						new FileEntry(asset.Source)
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

								var valueValue = value.Value;

								if (!valueValue.IsNullOrWhiteSpace())
								{
									valueValue = SpecialCharacters.Aggregate(valueValue, (current, c) => current.Replace(c.Value, c.Key));
								}

								eProperty.Add(new PropertyValue(value.ValueType, valueValue));
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

		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		private static string GetExePath()
		{
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}
	}
}
