using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;

namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
    public class BinaryBioCodexMap : BioCodexMap
    {
        private long _pagesOffset;
        private long _sectionsOffset;

        public BinaryBioCodexMap(IDictionary<int, BioCodexSection> sections = null, IDictionary<int, BioCodexPage> pages = null)
            : base(sections, pages) { }

        //public Stream BaseStream { get; protected set; }

        public long PagesOffset
        {
            get => _pagesOffset;
            set => SetProperty(ref _pagesOffset, value);
        }

        public long SectionsOffset
        {
            get => _sectionsOffset;
            set => SetProperty(ref _sectionsOffset, value);
        }

        public static BioCodexMap Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
            //This does not account for encoding. We will default to UTF8.
            return !File.Exists(path) ? null : Load(File.Open(path, FileMode.Open), Encoding.UTF8);
        }

        public static BioCodexMap Load(Stream stream, Encoding encoding)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new BioCodexMapReader(stream, encoding))
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
            if (string.IsNullOrEmpty(path))
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
            public BioCodexMapReader(Stream stream, Encoding encoding)
                : base(stream, encoding: encoding) { }

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

                // set entry to defaults as varies between Page and section
                entry.CodexSound = 0;
                entry.CodexSoundString = "";
            }

            public BioCodexPage ReadCodexPage()
            {
                var page = new BioCodexPage();

                ReadCodexEntry(page);
                if (page.InstanceVersion == 4) //if 4 then read object
                {
                    page.CodexSound = ReadInt32();
                    page.Section = ReadInt32();
                }
                else if (page.InstanceVersion == 3) //if 3 & LE read section then string else just section
                {
                    int unknown = ReadInt32();
                    if (unknown == 0) // This is only in LE
                    {
                        page.IsLE1 = true;
                        page.Section = ReadInt32();
                        int strLength = ReadInt32();
                        page.CodexSoundString = new string(ReadChars(strLength));
                    }
                    else
                    {
                        page.Section = unknown;
                    }
                }
                else if (page.InstanceVersion == 2) //if 2 read section then string else just section
                {
                    page.Section = ReadInt32();
                    int strLength = ReadInt32();
                    page.CodexSoundString = new string(ReadChars(strLength));
                }
                else
                { page.Section = ReadInt32(); }
                return page;
            }

            public BioCodexSection ReadCodexSection()
            {
                var section = new BioCodexSection();

                ReadCodexEntry(section);
                if (section.InstanceVersion >= 3) //if 3 / 4 then read object
                {
                    section.CodexSound = ReadInt32();
                }

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

            }

            public void Write(BioCodexPage page)
            {
                WriteCodexEntry(page);

                if (page.InstanceVersion == 4)
                {
                    Write(page.CodexSound);
                    Write(page.Section);
                }
                else if (page.InstanceVersion == 3)
                {
                    if (page.IsLE1)
                    {
                        Write(0);
                        Write(page.Section);
                        int length = page.CodexSoundString?.Length ?? 0;
                        Write(length);
                        if (!string.IsNullOrEmpty(page.CodexSoundString))
                        {
                            Write(page.CodexSoundString.ToCharArray(), 0, length);
                        }
                    }
                    else
                    {
                        Write(page.Section);
                    }
                }
                else if (page.InstanceVersion == 2)
                {
                    Write(page.Section);
                    int length = page.CodexSoundString?.Length ?? 0;
                    Write(length);
                    if (!string.IsNullOrEmpty(page.CodexSoundString))
                    {
                        Write(page.CodexSoundString.ToCharArray(), 0, length);
                    }
                }
                else
                {
                    Write(page.Section);
                }
            }

            public void Write(BioCodexSection section)
            {
                WriteCodexEntry(section);

                if (section.InstanceVersion >= 3)
                {
                    Write(section.CodexSound);
                }

                Write(section.IsPrimary.ToInt32());
            }
        }
    }
}
