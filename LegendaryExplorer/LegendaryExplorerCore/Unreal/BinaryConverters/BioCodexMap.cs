using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioCodexMap : ObjectBinary
    {
        public List<BioCodexPage> Pages;
        public List<BioCodexSection> Sections;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Sections, sc.Serialize);
            sc.Serialize(ref Pages, sc.Serialize);
        }

        public static BioCodexMap Create()
        {
            return new()
            {
                Pages = new List<BioCodexPage>(),
                Sections = new List<BioCodexSection>()
            };
        }
    }

    public class BioCodexPage
    {
        public int ID;
        public int Title;
        public int Description;
        public int TextureIndex;
        public int Priority;
        public int CodexSound;
        public string CodexSoundString;
        public int InstanceVersion;
        public int Section;
        public int LE1Unk;
        public string TitleAsString { get; set; }

        public BioCodexPage Clone(int newID)
        {
            BioCodexPage clone = (BioCodexPage)MemberwiseClone();
            clone.ID = newID;
            return clone;
        }
    }

    public class BioCodexSection
    {
        public int ID;
        public int Title;
        public int Description;
        public bool IsPrimary;
        public int TextureIndex;
        public int Priority;
        public int CodexSound;
        public int InstanceVersion;
        public string TitleAsString { get; set; }

        public BioCodexSection Clone(int newID)
        {
            BioCodexSection clone = (BioCodexSection)MemberwiseClone();
            clone.ID = newID;
            return clone;
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref BioCodexPage page)
        {
            if (IsLoading) page = new BioCodexPage();
            Serialize(ref page.ID);
            Serialize(ref page.InstanceVersion);
            Serialize(ref page.Title);
            Serialize(ref page.Description);
            Serialize(ref page.TextureIndex);
            Serialize(ref page.Priority);

            switch (page.InstanceVersion)
            {
                case 4:
                    Serialize(ref page.CodexSound);
                    Serialize(ref page.Section);
                    break;
                case 2:
                    Serialize(ref page.Section);
                    Serialize(ref page.CodexSoundString);
                    break;

                case 3:
                    if (Game is MEGame.LE1)
                    {
                        Serialize(ref page.LE1Unk);
                        Serialize(ref page.Section);
                        Serialize(ref page.CodexSoundString);
                    }
                    else Serialize(ref page.Section);
                    break;
            }
        }

        public void Serialize(ref BioCodexSection section)
        {
            if (IsLoading) section = new BioCodexSection();
            Serialize(ref section.ID);
            Serialize(ref section.InstanceVersion);
            Serialize(ref section.Title);
            Serialize(ref section.Description);
            Serialize(ref section.TextureIndex);
            Serialize(ref section.Priority);

            if(section.InstanceVersion >= 3)
            {
                Serialize(ref section.CodexSound);
            }
            Serialize(ref section.IsPrimary);
        }
    }
}
