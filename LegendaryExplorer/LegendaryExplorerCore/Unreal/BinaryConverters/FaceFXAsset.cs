using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class FaceFXAsset : ObjectBinary
    {
        private List<HNode> HNodes;
        public List<string> Names;
        public List<FaceFXBoneNode> BoneNodes;
        public List<FaceFXCombinerNode> CombinerNodes;
        private FXATableCElement[] TableC;
        private int unk1;
        private int Name;
        public List<FaceFXLine> Lines;
        private List<FXATableDElement> TableD;
        public List<int> LipSyncPhonemeNames;
        private List<int> EndingInts;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game == MEGame.ME2) throw new NotSupportedException("ME2 FaceFXAsset parsing is not supported");

            int int0 = 0;

            var startPos = sc.ms.Position;//come back here to serialize length at the end
            int length = 0;
            sc.Serialize(ref length);

            sc.SerializeFaceFXHeader();

            sc.Serialize(ref HNodes, SCExt.Serialize);
            sc.Serialize(ref Names, SCExt.SerializeFaceFXString);
            sc.Serialize(ref int0);
            sc.Serialize(ref int0);

            sc.Serialize(ref BoneNodes, SCExt.Serialize);
            sc.Serialize(ref CombinerNodes, SCExt.Serialize);

            // Do not serialize TableC count - it is the same length as combiner nodes (except not really)
            // Table C sucks. That's all i'm gonna say about it
            if (sc.IsLoading)
            {
                TableC = new FXATableCElement[CombinerNodes.Count];
            }
            for (int i = 0; i < TableC.Length; i++)
            {
                if (sc.IsSaving && TableC[i] == null) continue;
                sc.Serialize(ref TableC[i]);
                if (TableC[i].StringTuples.Length > 1)
                {
                    i += TableC[i].StringTuples.Length - 1;
                }
            }

            sc.Serialize(ref unk1);
            sc.Serialize(ref Name);

            sc.Serialize(ref Lines, SCExt.Serialize);
            sc.Serialize(ref int0);

            sc.Serialize(ref TableD, SCExt.Serialize);
            sc.Serialize(ref LipSyncPhonemeNames, SCExt.Serialize);

            length = (int)(sc.ms.Position - startPos - 4);
            sc.Serialize(ref int0);
            if (sc.Game != MEGame.ME1)
            {
                sc.Serialize(ref EndingInts, SCExt.Serialize);
            }
            sc.ms.JumpTo(startPos);
            sc.Serialize(ref length);

            if (sc.IsLoading)
            {
                foreach (var line in Lines)
                {
                    line.NameAsString = Names[line.NameIndex];
                }
            }
        }

        public class HNode
        {
            public int unk1;
            public (string, ushort)[] Names;

            /// <summary>
            /// Returns the node table used in FaceFXAnimsets
            /// </summary>
            /// <returns></returns>
            public static HNode[] GetFXANodeTable()
            {
                return new HNode[]
                {
                    new HNode {unk1 = 0x1A, Names=new (string, ushort)[] { ("FxObject", 0) }},
                    new HNode {unk1 = 0x48, Names=new (string, ushort)[] { ("FxAnim", 6) }},
                    new HNode {unk1 = 0x54, Names=new (string, ushort)[] { ("FxAnimSet", 0) }},
                    new HNode {unk1 = 0x5F, Names=new (string, ushort)[] { ("FxNamedObject", 0) }},
                    new HNode {unk1 = 0x64, Names=new (string, ushort)[] { ("FxName", 1) }},
                    new HNode {unk1 = 0x6D, Names=new (string, ushort)[] { ("FxAnimCurve", 1) }},
                    new HNode {unk1 = 0x75, Names=new (string, ushort)[] { ("FxAnimGroup", 0) }}
                };
            }
        }

        public class FXATableCElement
        {
            public int Name;
            public int unk1;
            public (int, string)[] StringTuples; // First string has no int, but doing it this way makes things "easier"
        }

        public class FXATableDElement
        {
            public int Name1;
            public int Name2;
            public float unk1;
        }
    }

    public class FaceFXBoneNode
    {
        public int BoneName;
        public float X;
        public float Y;
        public float Z;
        // As we figure out what these do, we can take them out of the array
        public float[] unkFloats = new float[13];


        public List<FaceFXBoneNodeChild> Children;
    }

    public class FaceFXBoneNodeChild
    {
        public int LinkName;
        public int ParentName;
        public float[] unkFloats = new float[10];
    }

    public class FaceFXCombinerNode
    {
        public int Format;
        public int Name;
        public int Flag;
        public float unk1;
        public float unk2;
        public float unk3;
        public int unk4;
        public List<FaceFXCombinerNodeChildLink> ChildLinks;

        public static FaceFXCombinerNode GetNodeTypeFromFormat(int format)
        {
            return format switch
            {
                6 => new CombinerNodeType6(),
                8 => new CombinerNodeType8(),
                _ => new CombinerNodeType0()
            };
        }
    }

    public class CombinerNodeType0 : FaceFXCombinerNode
    {
        public float unk5;
    }

    public class CombinerNodeType6 : FaceFXCombinerNode
    {
        public MaterialSlotID MaterialSlot;
        public ParameterName Parameter;
        public class MaterialSlotID
        {
            public int unk;
            public int Name;
            public float unk1;
            public float unk3;
            public float unk2;
        }

        public class ParameterName
        {
            public int unk;
            public int Name;
            public List<float> Floats;
            public string Parameter;
        }
        
    }

    public class CombinerNodeType8 : FaceFXCombinerNode
    {
        public List<Type8FXAInfo> FXAInfo;
        public class Type8FXAInfo
        {
            public int Name;
            public List<float> Floats;
            public string Path;
        }
    }


    public class FaceFXCombinerNodeChildLink
    {
        public int Name;
        public int unkInt;
        public List<float> floats;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref FaceFXAsset.HNode node)
        {
            if (sc.IsLoading)
            {
                node = new FaceFXAsset.HNode();
            }
            sc.Serialize(ref node.unk1);
            if (sc.IsLoading)
            {
                node.Names = new (string, ushort)[sc.ms.ReadInt32()];
            }
            else
            {
                sc.ms.Writer.WriteInt32(node.Names.Length);
            }

            for (int i = 0; i < node.Names.Length; i++)
            {
                (string, ushort) name = node.Names[i];
                sc.SerializeFaceFXString(ref name.Item1);
                sc.Serialize(ref name.Item2);
                node.Names[i] = name;
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXBoneNode node)
        {
            if (sc.IsLoading)
            {
                node = new FaceFXBoneNode();
            }
            sc.Serialize(ref node.BoneName);
            sc.Serialize(ref node.X);
            sc.Serialize(ref node.Y);
            sc.Serialize(ref node.Z);

            for (int i = 0; i < node.unkFloats.Length; i++)
            {
                sc.Serialize(ref node.unkFloats[i]);
            }
            sc.Serialize(ref node.Children, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXBoneNodeChild node)
        {
            if (sc.IsLoading)
            {
                node = new FaceFXBoneNodeChild();
            }
            sc.Serialize(ref node.LinkName);
            sc.Serialize(ref node.ParentName);

            for (int i = 0; i < node.unkFloats.Length; i++)
            {
                sc.Serialize(ref node.unkFloats[i]);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXCombinerNode node)
        {
            if (sc.IsLoading)
            {
                int fmt = sc.ms.ReadInt32();
                node = FaceFXCombinerNode.GetNodeTypeFromFormat(fmt);
                node.Format = fmt;
            }
            else
            {
                sc.Serialize(ref node.Format);
            }
            sc.Serialize(ref node.Name);
            sc.Serialize(ref node.Flag);
            sc.Serialize(ref node.unk1);
            sc.Serialize(ref node.unk2);
            sc.Serialize(ref node.unk3);
            sc.Serialize(ref node.unk4);
            sc.Serialize(ref node.ChildLinks, Serialize);

            switch (node)
            {
                case CombinerNodeType0 t0:
                    sc.Serialize(ref t0.unk5);
                    break;
                case CombinerNodeType6 t6:
                    sc.Serialize(ref t6.MaterialSlot);
                    sc.Serialize(ref t6.Parameter);
                    break;
                case CombinerNodeType8 t8:
                    sc.Serialize(ref t8.FXAInfo, Serialize);
                    break;
            }

        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXCombinerNodeChildLink node)
        {
            if (sc.IsLoading)
            {
                node = new FaceFXCombinerNodeChildLink();
            }
            sc.Serialize(ref node.Name);
            sc.Serialize(ref node.unkInt);
            sc.Serialize(ref node.floats, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref CombinerNodeType8.Type8FXAInfo info)
        {
            if (sc.IsLoading)
            {
                info = new CombinerNodeType8.Type8FXAInfo();
            }
            sc.Serialize(ref info.Name);
            sc.Serialize(ref info.Floats, Serialize);
            sc.SerializeFaceFXString(ref info.Path);
        }

        public static void Serialize(this SerializingContainer2 sc, ref CombinerNodeType6.MaterialSlotID node)
        {
            if (sc.IsLoading)
            {
                node = new CombinerNodeType6.MaterialSlotID();
            }
            sc.Serialize(ref node.unk);
            sc.Serialize(ref node.Name);
            sc.Serialize(ref node.unk1);
            sc.Serialize(ref node.unk2);
            sc.Serialize(ref node.unk3);
        }

        public static void Serialize(this SerializingContainer2 sc, ref CombinerNodeType6.ParameterName node)
        {
            if (sc.IsLoading)
            {
                node = new CombinerNodeType6.ParameterName();
            }
            sc.Serialize(ref node.unk);
            sc.Serialize(ref node.Name);
            sc.Serialize(ref node.Floats, Serialize);
            sc.SerializeFaceFXString(ref node.Parameter);
        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXAsset.FXATableCElement el)
        {
            if (sc.IsLoading)
            {
                el = new FaceFXAsset.FXATableCElement();
            }
            sc.Serialize(ref el.Name);
            sc.Serialize(ref el.unk1);
            if (sc.IsLoading)
            {
                el.StringTuples = new (int, string)[sc.ms.ReadInt32()];
            }
            else sc.ms.Writer.WriteInt32(el.StringTuples.Length);

            for (int i = 0; i < el.StringTuples.Length; i++)
            {
                (int, string) entry = el.StringTuples[i];

                // Format is String, Int String, Int String...
                // Store them as int,string tuples and don't serializing the first int.
                // Every extra string takes one away from the outer loop of TableC entries. I have no idea why.
                if (i > 0)
                {
                    sc.Serialize(ref entry.Item1);
                }
                sc.SerializeFaceFXString(ref entry.Item2);
                el.StringTuples[i] = entry;
            }

        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXAsset.FXATableDElement el)
        {
            if (sc.IsLoading)
            {
                el = new FaceFXAsset.FXATableDElement();
            }
            sc.Serialize(ref el.Name1);
            sc.Serialize(ref el.Name2);
            sc.Serialize(ref el.unk1);
        }
    }
}