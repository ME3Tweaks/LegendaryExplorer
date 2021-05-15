using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	public class XmlBioCodexMap : BioCodexMap
	{
		public XmlBioCodexMap(IDictionary<int, BioCodexSection> sections = null, IDictionary<int, BioCodexPage> pages = null)
			: base(sections, pages) {}

		
		public static BioCodexMap Load( string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			var map = new BioCodexMap();

			if (!File.Exists(path))
			{
				return map;
			}

			var doc = XDocument.Load(path);
			var root = doc.Root;

			if (root == null)
			{
				return map;
			}

			var pagesElement = root.Element("CodexPages");

			if (pagesElement != null)
			{
				var xCodexPages = from el in pagesElement.Elements("Page") select el;

				foreach (var xCodexPage in xCodexPages)
				{
					var id = (int?) xCodexPage.Attribute("Id");

					if (id == null)
					{
						continue;
					}

					var codexPage = new BioCodexPage
					{
						CodexSound = (int?) xCodexPage.Attribute("CodexSound") ?? BioCodexEntry.DefaultCodexSound,
                        CodexSoundString = (string)xCodexPage.Attribute("CodexSoundString") ?? BioCodexEntry.DefaultCodexSoundString,
                        Description = (int?) xCodexPage.Attribute("Description") ?? BioCodexEntry.DefaultDescription,
						InstanceVersion = (int?) xCodexPage.Attribute("InstanceVersion") ?? BioCodexEntry.DefaultInstanceVersion,
						Priority = (int?) xCodexPage.Attribute("Priority") ?? BioCodexEntry.DefaultPriority,
						Section = (int?) xCodexPage.Attribute("Section") ?? BioCodexPage.DefaultSection,
						TextureIndex = (int?) xCodexPage.Attribute("TextureIndex") ?? BioCodexEntry.DefaultTextureIndex,
						Title = (int?) xCodexPage.Attribute("Title") ?? BioCodexEntry.DefaultTitle
					};

					map.Pages.Add((int) id, codexPage);
				}
			}

			var sectionsElement = root.Element("CodexSections");

			if (sectionsElement != null)
			{
				var xCodexSections = from el in sectionsElement.Elements("Section") select el;

				foreach (var xCodexSection in xCodexSections)
				{
					var id = (int?) xCodexSection.Attribute("Id");

					if (id == null)
					{
						continue;
					}

					var codexSection = new BioCodexSection
					{
						CodexSound = (int?) xCodexSection.Attribute("CodexSound") ?? BioCodexEntry.DefaultCodexSound,
                        CodexSoundString = (string)xCodexSection.Attribute("CodexSoundString") ?? BioCodexEntry.DefaultCodexSoundString,
                        Description = (int?) xCodexSection.Attribute("Description") ?? BioCodexEntry.DefaultDescription,
						InstanceVersion = (int?) xCodexSection.Attribute("InstanceVersion") ?? BioCodexEntry.DefaultInstanceVersion,
						IsPrimary = (bool?) xCodexSection.Attribute("IsPrimary") ?? BioCodexSection.DefaultIsPrimary,
						Priority = (int?) xCodexSection.Attribute("Priority") ?? BioCodexEntry.DefaultPriority,
						TextureIndex = (int?) xCodexSection.Attribute("TextureIndex") ?? BioCodexEntry.DefaultTextureIndex,
						Title = (int?) xCodexSection.Attribute("Title") ?? BioCodexEntry.DefaultTitle
					};

					map.Sections.Add((int) id, codexSection);
				}
			}

			return map;
		}

		public void Save( string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			var doc = new XDocument();
			var rootElement = new XElement("CodexMap");
			var sectionsElement = new XElement("CodexSections");
			var pagesElement = new XElement("CodexPages");

			foreach (var section in Sections)
			{
				sectionsElement.Add(new XElement("Section",
					new XAttribute("Id", section.Key),
					new XAttribute("Title", section.Value.Title),
					new XAttribute("Description", section.Value.Description),
					new XAttribute("IsPrimary", section.Value.IsPrimary),
					new XAttribute("TextureIndex", section.Value.TextureIndex),
					new XAttribute("Priority", section.Value.Priority),
					new XAttribute("CodexSound", section.Value.CodexSound),
                    new XAttribute("CodexSoundString", section.Value.CodexSoundString),
                    new XAttribute("InstanceVersion", section.Value.InstanceVersion)));
			}

			rootElement.Add(sectionsElement);

			foreach (var section in Pages)
			{
				sectionsElement.Add(new XElement("Section",
					new XAttribute("Id", section.Key),
					new XAttribute("Title", section.Value.Title),
					new XAttribute("Description", section.Value.Description),
					new XAttribute("Section", section.Value.Section),
					new XAttribute("TextureIndex", section.Value.TextureIndex),
					new XAttribute("Priority", section.Value.Priority),
					new XAttribute("CodexSound", section.Value.CodexSound),
                    new XAttribute("CodexSoundString", section.Value.CodexSoundString),
                    new XAttribute("InstanceVersion", section.Value.InstanceVersion)));
			}

			rootElement.Add(pagesElement);
			doc.Add(rootElement);

			using (var writer = new XmlTextWriter(path, Encoding.UTF8))
			{
				writer.IndentChar = '\t';
				writer.Indentation = 1;
				writer.Formatting = Formatting.Indented;

				doc.Save(writer);
			}
		}
	}
}
