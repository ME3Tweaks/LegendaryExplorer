using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioCodexMap : ObjectBinary
    {
        public List<BioCodexPage> Pages;
        public List<BioCodexSection> Sections;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Sections, SCExt.Serialize);
            sc.Serialize(ref Pages, SCExt.Serialize);
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

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref BioCodexPage page)
        {
            if (sc.IsLoading) page = new BioCodexPage();
            sc.Serialize(ref page.ID);
            sc.Serialize(ref page.InstanceVersion);
            sc.Serialize(ref page.Title);
            sc.Serialize(ref page.Description);
            sc.Serialize(ref page.TextureIndex);
            sc.Serialize(ref page.Priority);

            switch (page.InstanceVersion)
            {
                case 4:
                    sc.Serialize(ref page.CodexSound);
                    sc.Serialize(ref page.Section);
                    break;
                case 2:
                    sc.Serialize(ref page.Section);
                    sc.Serialize(ref page.CodexSoundString);
                    break;

                case 3:
                    if (sc.Game is MEGame.LE1)
                    {
                        sc.Serialize(ref page.LE1Unk);
                        sc.Serialize(ref page.Section);
                        sc.Serialize(ref page.CodexSoundString);
                    }
                    else sc.Serialize(ref page.Section);
                    break;
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioCodexSection section)
        {
            if (sc.IsLoading) section = new BioCodexSection();
            sc.Serialize(ref section.ID);
            sc.Serialize(ref section.InstanceVersion);
            sc.Serialize(ref section.Title);
            sc.Serialize(ref section.Description);
            sc.Serialize(ref section.TextureIndex);
            sc.Serialize(ref section.Priority);

            if(section.InstanceVersion >= 3)
            {
                sc.Serialize(ref section.CodexSound);
            }
            sc.Serialize(ref section.IsPrimary);
        }
    }
}
