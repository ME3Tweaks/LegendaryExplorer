using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class FaceFXAsset : ObjectBinary
    {
        public int Version;
        private List<HNode> HNodes;
        public List<string> Names;
        public List<FaceFXBoneNode> BoneNodes;
        public List<FxNode> CombinerNodes;
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

            sc.SerializeFaceFXHeader(ref Version);

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

            // Serialize length (at the start of the binary)
            var endingPosition = sc.ms.Position;
            length = (int)(endingPosition - startPos - 4);
            sc.ms.JumpTo(startPos);
            sc.Serialize(ref length);

            // Come back to the end to finish serialization
            sc.ms.JumpTo(endingPosition);
            sc.Serialize(ref int0);
            if (sc.Game is MEGame.LE1 or MEGame.LE2)
            {
                sc.Serialize(ref EndingInts, SCExt.Serialize);
            }

            if (sc.IsLoading)
            {
                foreach (var line in Lines)
                {
                    line.NameAsString = Names[line.NameIndex];
                }
            }
        }

        public static FaceFXAsset Create(MEGame game)
        {
            return new()
            {
                HNodes = new List<HNode>(),
                Names = new List<string>(),
                BoneNodes = new List<FaceFXBoneNode>(),
                CombinerNodes = new List<FxNode>(),
                TableC = Array.Empty<FXATableCElement>(),
                Lines = new List<FaceFXLine>(),
                TableD = new List<FXATableDElement>(),
                LipSyncPhonemeNames = new List<int>(),
                EndingInts = new List<int>(),
                Version = game switch
                {
                    MEGame.ME1 => 1710,
                    MEGame.ME2 => 1610,
                    _ => 1731
                }
            };
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
        public int CombinerIndex; // Index into combiner node table - I think?
        public int ParentName;
        public float[] unkFloats = new float[10];
    }

    public enum FxNodeType : int
    {
        FxCombinerNode = 0,
        FxDeltaNode = 1,
        FxCurrentTimeNode = 2,
        FxBonePoseNode = 4,
        FxMorphTargetNode = 5,
        FxEmotionsWeightNode = 8
    }

    public enum FxInputOperation : int
    {
        SumInputs = 0,
        MultiplyInputs = 1
    }

    public class FxNode
    {
        public int format;
        public FxNodeType Format
        {
            get => (FxNodeType) format;
            set => format = (int)value;
        }
        public int Name;
        public float MinVal;
        public float unk1;
        public float MaxVal;
        public float unk2;
        public int inputOp;
        public FxInputOperation InputOperation
        {
            get => (FxInputOperation)inputOp;
            set => inputOp= (int)value;
        }
        public List<FxNodeParentLink> ChildLinks;
        public List<FxNodeParameter> Parameters;
    }

    public enum FxLinkFunction : int
    {
        Linear = 1,
        Quadratic = 2,
        Cubic = 3,
        SquareRoot = 4,
        Negate = 5,
        Inverse = 6,
        OneClamp = 7,
        Constant = 8,
        Corrective = 9,
        ClampedLinear = 10
    }

    public class FxNodeParentLink
    {
        public int NodeIndex;
        public int linkFunction;

        public FxLinkFunction LinkFunction
        {
            get => (FxLinkFunction) linkFunction;
            set => linkFunction = (int)value;
        }
        public List<float> FunctionSettings; // Each function has a different number of float settings
    }

    public enum FxNodeParamFormat
    {
        Integer = 0,
        String = 3
    }
    
    public class FxNodeParameter
    {
        public int Name;
        public int paramFormat;
        public FxNodeParamFormat Format
        {
            get => (FxNodeParamFormat) paramFormat;
            set => paramFormat = (int)value;
        }

        public int IntParameter;
        public float FloatParameter;
        public int unk2;
        public string StringParameter;
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
            sc.Serialize(ref node.CombinerIndex);
            sc.Serialize(ref node.ParentName);

            for (int i = 0; i < node.unkFloats.Length; i++)
            {
                sc.Serialize(ref node.unkFloats[i]);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref FxNode node)
        {
            if (sc.IsLoading) node = new FxNode();

            sc.Serialize(ref node.format);
            sc.Serialize(ref node.Name);
            sc.Serialize(ref node.MinVal);
            sc.Serialize(ref node.unk1);
            sc.Serialize(ref node.MaxVal);
            sc.Serialize(ref node.unk2);
            sc.Serialize(ref node.inputOp);
            sc.Serialize(ref node.ChildLinks, Serialize);
            sc.Serialize(ref node.Parameters, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref FxNodeParentLink node)
        {
            if (sc.IsLoading)
            {
                node = new FxNodeParentLink();
            }
            sc.Serialize(ref node.NodeIndex);
            sc.Serialize(ref node.linkFunction);
            sc.Serialize(ref node.FunctionSettings, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref FxNodeParameter param)
        {
            if (sc.IsLoading)
            {
                param = new FxNodeParameter();
            }
            sc.Serialize(ref param.Name);
            sc.Serialize(ref param.paramFormat);
            sc.Serialize(ref param.IntParameter);
            sc.Serialize(ref param.FloatParameter);
            sc.Serialize(ref param.unk2);
            if (param.paramFormat == 3)
            {
                sc.SerializeFaceFXString(ref param.StringParameter);
            }
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