using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class AKWwiseBank : ObjectBinary
    {
        public uint Unk1;//ME2
        public uint Unk2;//ME2
        public uint Unk3;

        private uint DataOffset;

        private uint[] bkhdUnks;
        public uint Version; //If 0, this Bank is serialized empty. When creating a bank, make sure to set this!
        public uint ID;

        public OrderedMultiValueDictionary<uint, byte[]> EmbeddedFiles = new OrderedMultiValueDictionary<uint, byte[]>();
        public OrderedMultiValueDictionary<uint, HIRCObject> HIRCObjects = new OrderedMultiValueDictionary<uint, HIRCObject>();
        public OrderedMultiValueDictionary<uint, string> ReferencedBanks = new OrderedMultiValueDictionary<uint, string>();

        public WwiseStateManagement InitStateManagement;//Only present in Init bank. ME3 version
        private byte[] ME2STMGFallback; //STMG chunk for ME2 isn't decoded yet
        private byte[] ENVS_Chunk;//Unparsed
        private byte[] FXPR_Chunk;//Unparsed, ME2 only

        private static readonly uint bkhd = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("BKHD"), 0);
        private static readonly uint stmg = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("STMG"), 0);
        private static readonly uint didx = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("DIDX"), 0);
        private static readonly uint data = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("DATA"), 0);
        private static readonly uint hirc = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("HIRC"), 0);
        private static readonly uint stid = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("STID"), 0);
        private static readonly uint envs = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("ENVS"), 0);
        private static readonly uint fxpr = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("FXPR"), 0);

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game != MEGame.ME3 && sc.Game != MEGame.ME2)
            {
                throw new Exception($"WwiseBank is not a valid class for {sc.Game}!");
            }
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (Unk1 == 0 && Unk2 == 0)
                {
                    return; //not sure what's going on here
                }
            }
            sc.Serialize(ref Unk3);
            var dataSizePos = sc.ms.Position; //come back to write size at the end
            int dataSize = 0;
            sc.Serialize(ref dataSize);
            sc.Serialize(ref dataSize);
            sc.Serialize(ref DataOffset);
            //sc.SerializeFileOffset();
            if (sc.IsLoading && dataSize == 0 || sc.IsSaving && Version == 0)
            {
                return;
            }

            if (sc.IsLoading)
            {
                sc.ms.Skip(4);
            }
            else
            {
                sc.ms.Writer.WriteUInt32(bkhd);
            }

            int bkhdLen = 8 + (bkhdUnks?.Length ?? 0) * 4;
            sc.Serialize(ref bkhdLen);
            sc.Serialize(ref Version);
            sc.Serialize(ref ID);
            if (sc.IsLoading)
            {
                bkhdUnks = new uint[(bkhdLen - 8) / 4];
            }

            for (int i = 0; i < bkhdUnks.Length; i++)
            {
                sc.Serialize(ref bkhdUnks[i]);
            }

            if (sc.IsLoading)
            {
                ReadChunks(sc);
            }
            else
            {
                WriteChunks(sc);
                var endPos = sc.ms.Position;
                sc.ms.JumpTo(dataSizePos);
                sc.ms.Writer.WriteInt32((int)(endPos - dataSizePos - 12));
                sc.ms.Writer.WriteInt32((int)(endPos - dataSizePos - 12));
                sc.ms.JumpTo(endPos);
            }
        }

        private void ReadChunks(SerializingContainer2 sc)
        {
            while (sc.ms.Position < sc.ms.Length)
            {
                string chunkID = sc.ms.ReadEndianASCIIString(4);
                int chunkSize = sc.ms.ReadInt32();
                switch (chunkID)
                {
                    case "STMG":
                    {
                        if (sc.Game == MEGame.ME2)
                        {
                            ME2STMGFallback = sc.ms.ReadBytes(chunkSize);
                            break;
                        }
                        InitStateManagement = new WwiseStateManagement
                        {
                            VolumeThreshold = sc.ms.ReadFloat(),
                            MaxVoiceInstances = sc.ms.ReadUInt16()
                        };
                        int stateGroupCount = sc.ms.ReadInt32();
                        InitStateManagement.StateGroups = new OrderedMultiValueDictionary<uint, WwiseStateManagement.StateGroup>();
                        for (int i = 0; i < stateGroupCount; i++)
                        {
                            uint id = sc.ms.ReadUInt32();
                            var stateGroup = new WwiseStateManagement.StateGroup
                            {
                                ID = id,
                                DefaultTransitionTime = sc.ms.ReadUInt32(),
                                CustomTransitionTimes = new List<WwiseStateManagement.CustomTransitionTime>()
                            };
                            int transTimesCount = sc.ms.ReadInt32();
                            for (int j = 0; j < transTimesCount; j++)
                            {
                                stateGroup.CustomTransitionTimes.Add(new WwiseStateManagement.CustomTransitionTime
                                {
                                    FromStateID = sc.ms.ReadUInt32(),
                                    ToStateID = sc.ms.ReadUInt32(),
                                    TransitionTime = sc.ms.ReadUInt32(),
                                });
                            }
                            InitStateManagement.StateGroups.Add(id, stateGroup);
                        }

                        int switchGroupCount = sc.ms.ReadInt32();
                        InitStateManagement.SwitchGroups = new OrderedMultiValueDictionary<uint, WwiseStateManagement.SwitchGroup>();
                        for (int i = 0; i < switchGroupCount; i++)
                        {
                            uint id = sc.ms.ReadUInt32();
                            var switchGroup = new WwiseStateManagement.SwitchGroup
                            {
                                ID = id,
                                GameParamID = sc.ms.ReadUInt32(),
                                Points = new List<WwiseStateManagement.SwitchPoint>()
                            };
                            int pointsCount = sc.ms.ReadInt32();
                            for (int j = 0; j < pointsCount; j++)
                            {
                                switchGroup.Points.Add(new WwiseStateManagement.SwitchPoint
                                {
                                    GameParamValue = sc.ms.ReadFloat(),
                                    SwitchID = sc.ms.ReadUInt32(),
                                    CurveShape = sc.ms.ReadUInt32()
                                });
                            }
                            InitStateManagement.SwitchGroups.Add(id, switchGroup);
                        }

                        int gameParamsCount = sc.ms.ReadInt32();
                        InitStateManagement.GameParameterDefaultValues = new OrderedMultiValueDictionary<uint, float>();
                        for (int i = 0; i < gameParamsCount; i++)
                        {
                            InitStateManagement.GameParameterDefaultValues.Add(sc.ms.ReadUInt32(), sc.ms.ReadFloat());
                        }
                        break;
                    }
                    case "DIDX":
                        int numFiles = chunkSize / 12;
                        var infoPos = sc.ms.Position;
                        sc.ms.Skip(chunkSize);
                        if (sc.ms.ReadUInt32() != data)
                        {
                            throw new Exception("DIDX chunk is not followed by DATA chunk in WwiseBank!");
                        }

                        var dataBytes = sc.ms.ReadBytes(sc.ms.ReadInt32());
                        var dataEndPos = sc.ms.Position;
                        sc.ms.JumpTo(infoPos);

                        for (int i = 0; i < numFiles; i++)
                        {
                            EmbeddedFiles.Add(sc.ms.ReadUInt32(), dataBytes.Slice(sc.ms.ReadInt32(), sc.ms.ReadInt32()));
                        }
                        sc.ms.JumpTo(dataEndPos);
                        break;
                    case "HIRC":
                        int count = sc.ms.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            byte type = sc.Game == MEGame.ME3 ? sc.ms.ReadByte() : (byte)sc.ms.ReadInt32();
                            int len = sc.ms.ReadInt32() - 4;
                            uint id = sc.ms.ReadUInt32();
                            HIRCObjects.Add(id, new HIRCObject
                            {
                                Type = type,
                                ID = id,
                                Raw = sc.ms.ReadBytes(len)
                            });
                        }
                        break;
                    case "STID":
                        sc.ms.Skip(4);//unknown uint (always 1)
                        int numBanks = sc.ms.ReadInt32();
                        for (int i = 0; i < numBanks; i++)
                        {
                            uint id = sc.ms.ReadUInt32();
                            byte strLen = sc.ms.ReadByte();
                            ReferencedBanks.Add(id, sc.ms.ReadStringASCII(strLen));
                        }
                        break;
                    case "FXPR":
                        //no idea what's in this chunk
                        FXPR_Chunk = sc.ms.ReadBytes(chunkSize);
                        break;
                    case "ENVS":
                        //no idea what's in this chunk
                        ENVS_Chunk = sc.ms.ReadBytes(chunkSize);
                        break;
                    default:
                        throw new Exception($"Unknown Chunk: {sc.ms.ReadEndianASCIIString(4)} at {sc.ms.Position - 4}");
                }
            }
        }

        private void WriteChunks(SerializingContainer2 sc)
        {
            EndianWriter writer = sc.ms.Writer;
            if (EmbeddedFiles.Count > 0)
            {
                writer.WriteUInt32(didx);
                writer.WriteInt32(EmbeddedFiles.Count * 12);
                var dataChunk = new MemoryStream();
                foreach ((uint id, byte[] bytes) in EmbeddedFiles)
                {
                    dataChunk.WriteZeros((int)(dataChunk.Position.Align(16) - dataChunk.Position)); //files must be 16-byte aligned in the data chunk

                    writer.WriteUInt32(id);
                    writer.WriteInt32((int)dataChunk.Position);
                    writer.WriteInt32(bytes.Length);
                    dataChunk.WriteBytes(bytes);
                }

                writer.WriteUInt32(data);
                writer.WriteInt32((int)dataChunk.Length);
                writer.WriteBytes(dataChunk.ToArray());
            }

            if (sc.Game == MEGame.ME2 && ME2STMGFallback != null)
            {
                writer.WriteUInt32(stmg);
                writer.WriteInt32(ME2STMGFallback.Length);
                writer.WriteBytes(ME2STMGFallback);
            }

            if (sc.Game == MEGame.ME3 && InitStateManagement != null)
            {
                writer.WriteUInt32(stmg);
                var lenPos = sc.ms.Position;
                writer.WriteUInt32(0);
                writer.WriteFloat(InitStateManagement.VolumeThreshold);
                writer.WriteUInt16(InitStateManagement.MaxVoiceInstances);
                writer.WriteInt32(InitStateManagement.StateGroups.Count);
                foreach ((uint _, WwiseStateManagement.StateGroup stateGroup) in InitStateManagement.StateGroups)
                {
                    writer.WriteUInt32(stateGroup.ID);
                    writer.WriteUInt32(stateGroup.DefaultTransitionTime);
                    writer.WriteInt32(stateGroup.CustomTransitionTimes.Count);
                    foreach (var transTime in stateGroup.CustomTransitionTimes)
                    {
                        writer.WriteUInt32(transTime.FromStateID);
                        writer.WriteUInt32(transTime.ToStateID);
                        writer.WriteUInt32(transTime.TransitionTime);
                    }
                }
                writer.WriteInt32(InitStateManagement.SwitchGroups.Count);
                foreach ((uint _, WwiseStateManagement.SwitchGroup switchGroup) in InitStateManagement.SwitchGroups)
                {
                    writer.WriteUInt32(switchGroup.ID);
                    writer.WriteUInt32(switchGroup.GameParamID);
                    writer.WriteInt32(switchGroup.Points.Count);
                    foreach (var point in switchGroup.Points)
                    {
                        writer.WriteFloat(point.GameParamValue);
                        writer.WriteUInt32(point.SwitchID);
                        writer.WriteUInt32(point.CurveShape);
                    }
                }
                writer.WriteInt32(InitStateManagement.GameParameterDefaultValues.Count);
                foreach ((uint id, float defaultValue) in InitStateManagement.GameParameterDefaultValues)
                {
                    writer.WriteUInt32(id);
                    writer.WriteFloat(defaultValue);
                }
                var endPos = sc.ms.Position;
                sc.ms.JumpTo(lenPos);
                writer.WriteInt32((int)(endPos - lenPos - 4));
                sc.ms.JumpTo(endPos);
            }

            if (HIRCObjects.Count > 0)
            {
                writer.WriteUInt32(hirc);
                var lengthPos = sc.ms.Position;
                writer.WriteUInt32(0);
                writer.WriteInt32(HIRCObjects.Count);
                foreach ((uint _, HIRCObject h) in HIRCObjects)
                {
                    if (sc.Game == MEGame.ME3)
                    {
                        writer.WriteByte(h.Type);
                    }
                    else
                    {
                        writer.WriteInt32(h.Type);
                    }

                    writer.WriteInt32(h.Raw.Length + 4);
                    writer.WriteUInt32(h.ID);
                    writer.WriteBytes(h.Raw);
                }

                var endPos = sc.ms.Position;
                sc.ms.JumpTo(lengthPos);
                writer.WriteInt32((int)(endPos - lengthPos - 4));
                sc.ms.JumpTo(endPos);
            }

            if (ReferencedBanks.Count > 0)
            {
                writer.WriteUInt32(stid);
                var lengthPos = sc.ms.Position;
                writer.WriteUInt32(0);
                writer.WriteUInt32(1);
                writer.WriteInt32(ReferencedBanks.Count);
                foreach ((uint id, string name) in ReferencedBanks)
                {
                    writer.WriteUInt32(id);
                    writer.WriteByte((byte)name.Length);
                    writer.WriteStringASCII(name);
                }

                var endPos = sc.ms.Position;
                sc.ms.JumpTo(lengthPos);
                writer.WriteInt32((int)(endPos - lengthPos - 4));
                sc.ms.JumpTo(endPos);
            }

            if (FXPR_Chunk != null)
            {
                writer.WriteUInt32(fxpr);
                writer.WriteInt32(FXPR_Chunk.Length);
                writer.WriteBytes(FXPR_Chunk);
            }

            if (ENVS_Chunk != null)
            {
                writer.WriteUInt32(envs);
                writer.WriteInt32(ENVS_Chunk.Length);
                writer.WriteBytes(ENVS_Chunk);
            }
        }

        public class HIRCObject
        {
            public byte Type;
            public uint ID;
            public byte[] Raw;
        }

    }
    public class WwiseStateManagement
    {
        public float VolumeThreshold;
        public ushort MaxVoiceInstances;
        public OrderedMultiValueDictionary<uint, StateGroup> StateGroups;
        public OrderedMultiValueDictionary<uint, SwitchGroup> SwitchGroups;
        public OrderedMultiValueDictionary<uint, float> GameParameterDefaultValues;

        public class CustomTransitionTime
        {
            public uint FromStateID;
            public uint ToStateID;
            public uint TransitionTime; //in milliseconds
        }

        public class StateGroup
        {
            public uint ID;
            public uint DefaultTransitionTime;
            public List<CustomTransitionTime> CustomTransitionTimes;
        }

        public class SwitchPoint
        {
            public float GameParamValue;
            public uint SwitchID; //id of Switch  set when Game Parameter >= GameParamValue
            public uint CurveShape; //Always 9? 9 = constant
        }

        public class SwitchGroup
        {
            public uint ID;
            public uint GameParamID;
            public List<SwitchPoint> Points;
        }
    }
}
