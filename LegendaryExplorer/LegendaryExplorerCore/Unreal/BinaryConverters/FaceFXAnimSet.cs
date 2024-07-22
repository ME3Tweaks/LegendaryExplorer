using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class FaceFXAnimSet : ObjectBinary
    {
        public int Version;
        private FaceFXAsset.HNode[] HNodes;
        public List<string> Names;
        public List<FaceFXLine> Lines;

        protected override void Serialize(SerializingContainer2 sc)
        {
            var startPos = sc.ms.Position;//come back here to serialize length at the end
            int length = 0;
            sc.Serialize(ref length);

            #region  Header
            int int0 = 0;
            int int1 = 1;

            sc.SerializeFaceFXHeader(ref Version);

            if (sc.Game != MEGame.ME2)
            {
                if (sc.IsSaving && HNodes is null)
                {
                    FixNodeTable();
                }
                sc.Serialize(ref HNodes, SCExt.Serialize);
            }

            if (sc.Game == MEGame.ME2)
            {
                int count = Names?.Count ?? 0;
                sc.Serialize(ref count);
                int unk = 65536;
                if (sc.IsLoading)
                {
                    Names = new List<string>(count);
                    for (int i = 0; i < count; i++)
                    {
                        sc.Serialize(ref unk);
                        string tmp = default;
                        sc.SerializeFaceFXString(ref tmp);
                        Names.Add(tmp);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        sc.Serialize(ref unk);
                        string tmp = Names[i];
                        sc.SerializeFaceFXString(ref tmp);
                    }
                }
            }
            else
            {
                sc.Serialize(ref Names, SCExt.SerializeFaceFXString);
            }

            sc.Serialize(ref int0);
            sc.Serialize(ref int0);
            sc.Serialize(ref int1);
            sc.Serialize(ref int0);
            if (sc.Game == MEGame.ME2)
            {
                int int65537 = 65537;
                int int65536 = 65536;
                sc.Serialize(ref int65537);
                sc.Serialize(ref int0);
                sc.Serialize(ref int65536);
                sc.Serialize(ref int0);
                sc.Serialize(ref int0);
            }

            #endregion

            sc.Serialize(ref Lines, SCExt.Serialize);

            var endingPosition = sc.ms.Position;
            length = (int)(endingPosition - startPos - 4);
            sc.ms.JumpTo(startPos);
            sc.Serialize(ref length);

            sc.ms.JumpTo(endingPosition);
            int zero = 0;
            sc.Serialize(ref zero);

            if (sc.IsLoading)
            {
                foreach (var line in Lines)
                {
                    line.NameAsString = Names[line.NameIndex];
                }
            }
        }

        public static FaceFXAnimSet Create(MEGame game)
        {
            return new()
            {
                HNodes = FaceFXAsset.HNode.GetFXANodeTable(),
                Names = new List<string>(),
                Lines = new List<FaceFXLine>(),
                Version = game switch
                {
                    MEGame.ME1 => 1710,
                    MEGame.ME2 => 1610,
                    _ => 1731
                }
            };
        }

        public void FixNodeTable()
        {
            HNodes = FaceFXAsset.HNode.GetFXANodeTable();
        }
    }

    public class FaceFXLine
    {
        public int NameIndex;
        public string NameAsString { get; set; }
        public List<int> AnimationNames;
        public List<FaceFXControlPoint> Points;
        public List<int> NumKeys;
        public float FadeInTime;
        public float FadeOutTime;
        public string Path;
        public string ID;
        public int Index;

        public FaceFXLine Clone()
        {
            FaceFXLine clone = (FaceFXLine)MemberwiseClone();
            clone.AnimationNames = AnimationNames.Clone();
            clone.Points = Points.Clone();
            clone.NumKeys = NumKeys.Clone();
            return clone;
        }
    }

    public struct FaceFXControlPoint
    {
        public float time;
        public float weight;
        public float inTangent;
        public float leaveTangent;
    }

    public static partial class SCExt
    {
        public static void SerializeFaceFXString(this SerializingContainer2 sc, ref string str)
        {
            if (sc.IsLoading)
            {
                str = sc.ms.ReadStringASCII(sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.Writer.WriteInt32(str.Length);
                sc.ms.Writer.WriteStringLatin1(str);
            }
        }

        public static void SerializeFaceFXHeader(this SerializingContainer2 sc, ref int version)
        {
            int int0 = 0;
            short short1 = 1;

            uint FACE = 1162035526U;
            sc.Serialize(ref FACE);
            sc.Serialize(ref version);
            if (version == 1731)
            {
                sc.Serialize(ref int0);
            }
            else if (version == 1610)
            {
                sc.Serialize(ref short1);
            }

            string licensee = "Unreal Engine 3 Licensee";
            string project = "Unreal Engine 3 Project";
            if (sc.IsSaving && version == 1731)
            {
                licensee += '\0';
                project += '\0';
            }
            sc.SerializeFaceFXString(ref licensee);
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref short1);
            }
            sc.SerializeFaceFXString(ref project);
            int version2 = sc.Game switch
            {
                MEGame.ME1 => 1000,
                _ => 1100
            };
            sc.Serialize(ref version2);
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref int0);
            }
            else
            {
                sc.Serialize(ref short1);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref FaceFXControlPoint point)
        {
            if (sc.IsLoading)
            {
                point = new FaceFXControlPoint();
            }
            sc.Serialize(ref point.time);
            sc.Serialize(ref point.weight);
            sc.Serialize(ref point.inTangent);
            sc.Serialize(ref point.leaveTangent);
        }
        public static void Serialize(this SerializingContainer2 sc, ref FaceFXLine line)
        {
            int int0 = 0;
            short short0 = 0;
            short short1 = 1;
            if (sc.IsLoading)
            {
                line = new FaceFXLine();
            }

            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref int0);
                sc.Serialize(ref short1);
            }
            sc.Serialize(ref line.NameIndex);
            if (sc.Game == MEGame.ME2)
            {
                int unk6 = 6;
                sc.Serialize(ref unk6);
            }
            //AnimationNames
            {
                int count = line.AnimationNames?.Count ?? 0;
                sc.Serialize(ref count);
                int int1 = 1;
                if (sc.IsLoading)
                {
                    line.AnimationNames = new List<int>(count);
                }
                for (int i = 0; i < count; i++)
                {
                    if (sc.Game == MEGame.ME2)
                    {
                        sc.Serialize(ref int0);
                        sc.Serialize(ref short1);
                    }
                    if (sc.IsLoading)
                    {
                        int tmp = default;
                        sc.Serialize(ref tmp);
                        line.AnimationNames.Add(tmp);
                    }
                    else
                    {
                        int tmp = line.AnimationNames[i];
                        sc.Serialize(ref tmp);
                    }
                    if (sc.Game == MEGame.ME2)
                    {
                        sc.Serialize(ref int1);
                        sc.Serialize(ref short0);
                    }
                    else
                    {
                        sc.Serialize(ref int0);
                    }
                }
            }

            sc.Serialize(ref line.Points, Serialize);
            if (line.Points.Any())
            {
                if (sc.Game == MEGame.ME2)
                {
                    sc.Serialize(ref short0);
                }
                sc.Serialize(ref line.NumKeys, SCExt.Serialize);
            }
            else if (sc.IsLoading)
            {
                line.NumKeys = Enumerable.Repeat(0, line.AnimationNames.Count).ToList();
            }
            sc.Serialize(ref line.FadeInTime);
            sc.Serialize(ref line.FadeOutTime);
            sc.Serialize(ref int0);
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref short0);
                sc.Serialize(ref short1);
            }
            sc.SerializeFaceFXString(ref line.Path);
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref short1);
            }
            sc.SerializeFaceFXString(ref line.ID);
            sc.Serialize(ref line.Index);
        }
    }
}