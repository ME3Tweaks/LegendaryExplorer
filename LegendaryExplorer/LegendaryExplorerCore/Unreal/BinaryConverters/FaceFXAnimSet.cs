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

        protected override void Serialize(SerializingContainer sc)
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
                sc.Serialize(ref HNodes, sc.Serialize);
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
                sc.Serialize(ref Names, sc.SerializeFaceFXString);
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

            sc.Serialize(ref Lines, sc.Serialize);

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

    public partial class SerializingContainer
    {
        public void SerializeFaceFXString(ref string str)
        {
            if (IsLoading)
            {
                str = ms.ReadStringASCII(ms.ReadInt32());
            }
            else
            {
                ms.Writer.WriteInt32(str.Length);
                ms.Writer.WriteStringLatin1(str);
            }
        }

        public void SerializeFaceFXHeader(ref int version)
        {
            int int0 = 0;
            short short1 = 1;

            uint FACE = 1162035526U;
            Serialize(ref FACE);
            Serialize(ref version);
            if (version == 1731)
            {
                Serialize(ref int0);
            }
            else if (version == 1610)
            {
                Serialize(ref short1);
            }

            string licensee = "Unreal Engine 3 Licensee";
            string project = "Unreal Engine 3 Project";
            if (IsSaving && version == 1731)
            {
                licensee += '\0';
                project += '\0';
            }
            SerializeFaceFXString(ref licensee);
            if (Game == MEGame.ME2)
            {
                Serialize(ref short1);
            }
            SerializeFaceFXString(ref project);
            int version2 = Game switch
            {
                MEGame.ME1 => 1000,
                _ => 1100
            };
            Serialize(ref version2);
            if (Game == MEGame.ME2)
            {
                Serialize(ref int0);
            }
            else
            {
                Serialize(ref short1);
            }
        }

        public void Serialize(ref FaceFXControlPoint point)
        {
            if (IsLoading)
            {
                point = new FaceFXControlPoint();
            }
            Serialize(ref point.time);
            Serialize(ref point.weight);
            Serialize(ref point.inTangent);
            Serialize(ref point.leaveTangent);
        }
        public void Serialize(ref FaceFXLine line)
        {
            int int0 = 0;
            short short0 = 0;
            short short1 = 1;
            if (IsLoading)
            {
                line = new FaceFXLine();
            }

            if (Game == MEGame.ME2)
            {
                Serialize(ref int0);
                Serialize(ref short1);
            }
            Serialize(ref line.NameIndex);
            if (Game == MEGame.ME2)
            {
                int unk6 = 6;
                Serialize(ref unk6);
            }
            //AnimationNames
            {
                int count = line.AnimationNames?.Count ?? 0;
                Serialize(ref count);
                int int1 = 1;
                if (IsLoading)
                {
                    line.AnimationNames = new List<int>(count);
                }
                for (int i = 0; i < count; i++)
                {
                    if (Game == MEGame.ME2)
                    {
                        Serialize(ref int0);
                        Serialize(ref short1);
                    }
                    if (IsLoading)
                    {
                        int tmp = default;
                        Serialize(ref tmp);
                        line.AnimationNames.Add(tmp);
                    }
                    else
                    {
                        int tmp = line.AnimationNames[i];
                        Serialize(ref tmp);
                    }
                    if (Game == MEGame.ME2)
                    {
                        Serialize(ref int1);
                        Serialize(ref short0);
                    }
                    else
                    {
                        Serialize(ref int0);
                    }
                }
            }

            Serialize(ref line.Points, Serialize);
            if (line.Points.Any())
            {
                if (Game == MEGame.ME2)
                {
                    Serialize(ref short0);
                }
                Serialize(ref line.NumKeys, Serialize);
            }
            else if (IsLoading)
            {
                line.NumKeys = Enumerable.Repeat(0, line.AnimationNames.Count).ToList();
            }
            Serialize(ref line.FadeInTime);
            Serialize(ref line.FadeOutTime);
            Serialize(ref int0);
            if (Game == MEGame.ME2)
            {
                Serialize(ref short0);
                Serialize(ref short1);
            }
            SerializeFaceFXString(ref line.Path);
            if (Game == MEGame.ME2)
            {
                Serialize(ref short1);
            }
            SerializeFaceFXString(ref line.ID);
            Serialize(ref line.Index);
        }
    }
}