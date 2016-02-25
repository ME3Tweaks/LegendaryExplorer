using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	public class BinaryBioCodexMap : BioCodexMap
	{
		private long _pagesOffset;
		private long _sectionsOffset;

		public BinaryBioCodexMap(IDictionary<int, BioCodexSection> sections = null, IDictionary<int, BioCodexPage> pages = null)
			: base(sections, pages) {}

		//public Stream BaseStream { get; protected set; }

		public long PagesOffset
		{
			get { return _pagesOffset; }
			set { SetProperty(ref _pagesOffset, value); }
		}

		public long SectionsOffset
		{
			get { return _sectionsOffset; }
			set { SetProperty(ref _sectionsOffset, value); }
		}

		public static BioCodexMap Load(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			return !File.Exists(path) ? null : Load(File.Open(path, FileMode.Open));
		}

		public static BioCodexMap Load(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var reader = new BioCodexMapReader(stream))
			{
				var map = new BinaryBioCodexMap();

				// Sections
				var sectionsCount = reader.ReadInt32();
				map.Sections = new Dictionary<int, BioCodexSection>();

				for (var i = 0; i < sectionsCount; i++)
				{
					var id = reader.ReadInt32();
					var section = reader.ReadCodexSection();

					if (!map.Sections.ContainsKey(id))
					{
						map.Sections.Add(id, section);
					}
					else
					{
						map.Sections[id] = section;
					}
				}

				// Pages
				var pagesCount = reader.ReadInt32();
				map.Pages = new Dictionary<int, BioCodexPage>();

				for (var i = 0; i < pagesCount; i++)
				{
					var id = reader.ReadInt32();
					var page = reader.ReadCodexPage();

					if (!map.Pages.ContainsKey(id))
					{
						map.Pages.Add(id, page);
					}
					else
					{
						map.Pages[id] = page;
					}
				}

				return map;
			}
		}

		public void Save(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			Save(File.Open(path, FileMode.Create));
		}

		public void Save(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var writer = new BioCodexMapWriter(stream))
			{
				// Sections
				writer.Write(Sections.Count);

				foreach (var section in Sections)
				{
					writer.Write(section.Key);
					writer.Write(section.Value);
				}

				// Pages
				writer.Write(Pages.Count);

				foreach (var page in Pages)
				{
					writer.Write(page.Key);
					writer.Write(page.Value);
				}
			}
		}

		public class BioCodexMapReader : DataReader
		{
			public BioCodexMapReader(Stream stream)
				: base(stream) { }

			protected void ReadCodexEntry(BioCodexEntry entry)
			{
				if (entry == null)
				{
					throw new ArgumentNullException(nameof(entry));
				}

				//entry.Id = ReadInt32();
				entry.InstanceVersion = ReadInt32();
				entry.Title = ReadInt32();
				entry.Description = ReadInt32();
				entry.TextureIndex = ReadInt32();
				entry.Priority = ReadInt32();

				entry.CodexSound = entry.InstanceVersion == 4 ? ReadInt32() : 0;
			}

			public BioCodexPage ReadCodexPage()
			{
				var page = new BioCodexPage();

				ReadCodexEntry(page);

				page.Section = ReadInt32();

				return page;
			}

			public BioCodexSection ReadCodexSection()
			{
				var section = new BioCodexSection();

				ReadCodexEntry(section);

				section.IsPrimary = ReadInt32().ToBoolean();

				return section;
			}
		}

		public class BioCodexMapWriter : DataWriter
		{
			public new static readonly BioCodexMapWriter Null = new BioCodexMapWriter();

			protected BioCodexMapWriter() { }

			public BioCodexMapWriter(Stream output)
				: base(output) { }

			protected void WriteCodexEntry(BioCodexEntry entry)
			{
				if (entry == null)
				{
					throw new ArgumentNullException(nameof(entry));
				}

				//Write(entry.Id);
				Write(entry.InstanceVersion);
				Write(entry.Title);
				Write(entry.Description);
				Write(entry.TextureIndex);
				Write(entry.Priority);

				if (entry.InstanceVersion == 4)
				{
					Write(entry.CodexSound);
				}
			}

			public void Write(BioCodexPage page)
			{
				WriteCodexEntry(page);

				Write(page.Section);
			}

			public void Write(BioCodexSection section)
			{
				WriteCodexEntry(section);

				Write(section.IsPrimary.ToInt32());
			}
		}
	}
}
