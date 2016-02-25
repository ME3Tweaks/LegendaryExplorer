using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.Collections.Generic;

namespace Gammtek.Conduit.MassEffect.Coalesce.Xml
{
	public class XmlCoalesceDocument : CoalesceDocument
	{
		//private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
		private static readonly Regex SpecialCharactersPattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);
		private const string DefaultPointer = "/CoalesceDocument/Sections";

		public XmlCoalesceDocument(string name = null, CoalesceSections sections = null, IList<string> includes = null)
			: base(name, sections)
		{
			Includes = includes ?? new List<string>();
		}

		public string BaseUri { get; protected set; }

		public IList<string> Includes { get; set; }

		public string LogicalSourcePath { get; protected set; }

		public string SourceDirectory { get; protected set; }

		public string SourcePath { get; protected set; }

		public static XmlCoalesceDocument Load(string path)
		{
			if (path.IsNullOrWhiteSpace())
			{
				throw new ArgumentNullException(nameof(path));
			}

			var sourcePath = Path.GetFullPath(path);

			if (!File.Exists(sourcePath))
			{
				Console.WriteLine($"Warning: {path} not found!");

				return null;
			}

			var doc = XDocument.Load(path);

			var root = doc.Root;

			if (root == null)
			{
				return null;
			}

			var id = (string) root.Attribute("id");
			var name = (string) root.Attribute("name");
			var source = (string) root.Attribute("source");

			var result = new XmlCoalesceDocument(name)
			{
				BaseUri = doc.BaseUri.IsNullOrWhiteSpace() ? doc.BaseUri : new Uri(sourcePath).AbsoluteUri,
				Id = id,
				Source = source,
				SourcePath = sourcePath,
				SourceDirectory = Path.GetDirectoryName(sourcePath)
			};

			// Read includes before the sections
			//result.ReadIncludes(root);
			result.ReadSections(root);

			return result;
		}

		private void Parse(XDocument document)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			Parse(document.Root);
		}

		private void Parse(XElement element)
		{
			if (element == null)
			{
				throw new ArgumentNullException(nameof(element));
			}

			ParseIncludes(element);
		}

		private void ParseIncludes(XDocument document)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			ParseIncludes(document.Root);
		}

		private void ParseIncludes(XElement element)
		{
			if (element == null)
			{
				throw new ArgumentNullException(nameof(element));
			}

			var includes = from el in element.Descendants("Include") select el;

			foreach (var include in includes)
			{
				var source = (string)include.Attribute("source");

				if (source.IsNullOrWhiteSpace())
				{
					continue;
				}

				var pointer = (string)include.Attribute("pointer") ?? DefaultPointer;
				var required = (bool?)include.Attribute("required") ?? false;

				var sourcePath = Path.Combine(SourceDirectory, source);
				sourcePath = Path.GetFullPath(sourcePath);

				if (required && !File.Exists(sourcePath))
				{
					// Break due to error
				}

				if (!Includes.Contains(sourcePath))
				{
					Includes.Add(sourcePath);
				}
			}

			foreach (var include in Includes)
			{
				var asset = Load(include);

				if (asset == null)
				{
					continue;
				}

				Combine(asset);
			}
		}

		/*public void ReadIncludes(XElement root)
		{
			if (root == null)
			{
				throw new ArgumentNullException(nameof(root));
			}

			var includesElement = root.Element("Includes");

			if (includesElement == null)
			{
				return;
			}

			var includes = from el in includesElement.Elements("Include") select el;
			var test = new Queue<XmlCoalesceInclude>();
			
			foreach (var include in includes)
			{
				var source = (string) include.Attribute("source");

				if (source.IsNullOrWhiteSpace())
				{
					continue;
				}

				var required = (bool?) include.Attribute("required") ?? false;

				var sourcePath = Path.Combine(SourceDirectory, source);
				sourcePath = Path.GetFullPath(sourcePath);

				if (required && !File.Exists(sourcePath))
				{
					// Break due to error
				}

				Includes.Enqueue(new XmlCoalesceInclude(sourcePath));
			}

			foreach (var include in Includes)
			{
				var asset = Load(include.Source);

				if (asset == null)
				{
					continue;
				}

				Combine(asset);
			}
		}*/

		public CoalesceProperty ReadProperty(XElement propertyElement)
		{
			if (propertyElement == null)
			{
				throw new ArgumentNullException(nameof(propertyElement));
			}

			var propertyName = (string) propertyElement.Attribute("name");

			if (propertyName.IsNullOrWhiteSpace())
			{
				return null;
			}

			var property = new CoalesceProperty(propertyName);

			if (propertyElement.HasElements)
			{
				var valueElements = from el in propertyElement.Elements("Value") select el;

				// Loop through the current properties values
				foreach (var valueElement in valueElements)
				{
					var value = valueElement.Value;
					var type = (int?) valueElement.Attribute("type");

					if (!value.IsNullOrWhiteSpace())
					{
						value = SpecialCharactersPattern.Replace(value.Trim(), "");
					}

					property.Add(new CoalesceValue(value, type));
				}
			}
			else
			{
				var value = propertyElement.Value;
				var type = (int?) propertyElement.Attribute("type");

				if (!value.IsNullOrWhiteSpace())
				{
					value = SpecialCharactersPattern.Replace(value.Trim(), "");
				}

				property.Add(new CoalesceValue(value, type));
			}

			return property;
		}

		public void ReadSections(XElement root)
		{
			if (root == null)
			{
				throw new ArgumentNullException(nameof(root));
			}

			var sectionsElement = root.Element("Sections");

			if (sectionsElement == null)
			{
				return;
			}

			var sectionElements = from el in sectionsElement.Elements("Section") select el;

			// Loop through the sections
			foreach (var sectionElement in sectionElements)
			{
				CoalesceSection section;
				var currentSection = new CoalesceSection();
				var sectionName = (string) sectionElement.Attribute("name");

				// Make sure the section has a name
				if (sectionName.IsNullOrEmpty())
				{
					continue;
				}

				var propertyElements = from el in sectionElement.Elements("Property") select el;

				// Loop through the current sections properties
				foreach (var propertyElement in propertyElements)
				{
					var currentProperty = ReadProperty(propertyElement);
					CoalesceProperty property;

					if (currentProperty == null)
					{
						continue;
					}

					if (!currentSection.TryGetValue(currentProperty.Name, out property))
					{
						property = new CoalesceProperty();
						currentSection.Add(currentProperty.Name, property);
					}

					property.AddRange(currentProperty);
				}

				if (!Sections.TryGetValue(sectionName, out section))
				{
					section = new CoalesceSection();
					Sections.Add(sectionName, section);
				}

				section.Combine(currentSection);
			}
		}
	}
}
