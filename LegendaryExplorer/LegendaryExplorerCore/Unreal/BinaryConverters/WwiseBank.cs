using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class WwiseBank : ObjectBinary
    {
        public uint Unk1;//ME2
        public uint Unk2;//ME2

        private uint[] bkhdUnks;
        public uint Version; //If 0, this Bank is serialized empty. When creating a bank, make sure to set this!
        public uint ID;

        public UMultiMap<uint, byte[]> EmbeddedFiles = new(); //TODO: Make this a UMap?
        public UMultiMap<uint, HIRCObject> HIRCObjects = new(); //TODO: Make this a UMap?
        public UMultiMap<uint, string> ReferencedBanks = new(); //TODO: Make this a UMap?

        public WwiseStateManagement InitStateManagement;//Only present in Init bank. ME3 version
        private byte[] ME2STMGFallback; //STMG chunk for ME2 isn't decoded yet
        private byte[] ENVS_Chunk;//Unparsed
        private byte[] FXPR_Chunk;//Unparsed, ME2 only
        private byte[] INIT_Chunk;//Unparsed, ME2 only
        public string Platform;

        #region Serialization

        private static readonly uint bkhd = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("BKHD"), 0);
        private static readonly uint stmg = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("STMG"), 0);
        private static readonly uint didx = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("DIDX"), 0);
        private static readonly uint data = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("DATA"), 0);
        private static readonly uint hirc = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("HIRC"), 0);
        private static readonly uint stid = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("STID"), 0);
        private static readonly uint envs = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("ENVS"), 0);
        private static readonly uint fxpr = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("FXPR"), 0);
        private static readonly uint init = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("INIT"), 0);
        private static readonly uint plat = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("PLAT"), 0);

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game != MEGame.ME3 && sc.Game != MEGame.ME2 && sc.Game != MEGame.LE3 && sc.Game != MEGame.LE2)
            {
                throw new Exception($"WwiseBank is not a valid class for {sc.Game}!");
            }
            if (sc.Game.IsGame2())
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (Unk1 == 0 && Unk2 == 0)
                {
                    return; //not sure what's going on here
                }
            }
            sc.SerializeConstInt(0);//BulkDataFlags
            var dataSizePos = sc.ms.Position; //come back to write size at the end
            int dataSize = 0;
            sc.Serialize(ref dataSize);
            sc.Serialize(ref dataSize);
            sc.SerializeFileOffset();
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

            if (Version is 38 || //strangely formatted Wwisebank, unused maybe? We're going to ignore it.
                sc.Game is MEGame.LE2 && Version is 44) //temporary hack. Todo: WwiseBank parsing should be refactored to parse based on Version, not game
            {
                if (sc.IsLoading)
                {
                    var amountRead = (sc.ms.Position - dataSizePos - 12);
                    var amountRemaining = dataSize - amountRead;
                    ME2STMGFallback = sc.ms.ReadBytes((int)amountRemaining);
                }
                else
                {
                    sc.ms.Writer.WriteFromBuffer(ME2STMGFallback);
                    var endPos = sc.ms.Position;
                    sc.ms.JumpTo(dataSizePos);
                    sc.ms.Writer.WriteInt32((int)(endPos - dataSizePos - 12));
                    sc.ms.Writer.WriteInt32((int)(endPos - dataSizePos - 12));
                    sc.ms.JumpTo(endPos);
                }
            }
            else
            {
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
        }

        public static WwiseBank Create()
        {
            return new()
            {
                bkhdUnks = Array.Empty<uint>()
            };
        }

        private void ReadChunks(SerializingContainer2 sc)
        {
            while (sc.ms.Position < sc.ms.Length)
            {
                // It looks like on consoles this is not endian
                string chunkID = sc.ms.BaseStream.ReadStringLatin1(4);

                int chunkSize = sc.ms.ReadInt32();
                switch (chunkID)
                {
                    case "STMG":
                        {
                            if (sc.Game.IsGame2() || sc.Game is MEGame.LE3)
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
                            InitStateManagement.StateGroups = new();
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
                            InitStateManagement.SwitchGroups = new();
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
                            InitStateManagement.GameParameterDefaultValues = new();
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
                        if (sc.ms.BaseStream.ReadUInt32() != data) //not endian swapped
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
                            var ho = HIRCObject.Create(sc);
                            HIRCObjects.Add(ho.ID, ho);
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
                    case "INIT":
                        //no idea what's in this chunk
                        INIT_Chunk = sc.ms.ReadBytes(chunkSize);
                        break;
                    case "PLAT":
                        //no idea what's in this chunk
                        Platform = sc.ms.ReadUnrealString();
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
                using var dataChunk = MemoryManager.GetMemoryStream();
                foreach ((uint id, byte[] bytes) in EmbeddedFiles)
                {
                    dataChunk.WriteZeros((int)(dataChunk.Position.Align(16) - dataChunk.Position)); //files must be 16-byte aligned in the data chunk
                    writer.WriteUInt32(id); //writing to DIDX
                    writer.WriteInt32((int)dataChunk.Position); //Writing to DIDX
                    writer.WriteInt32(bytes.Length); //Writing to DIDX
                    dataChunk.WriteFromBuffer(bytes); //Writing to DATA
                }

                writer.WriteUInt32(data);
                writer.WriteInt32((int)dataChunk.Length);
                writer.WriteFromBuffer(dataChunk.ToArray());
            }

            if (INIT_Chunk != null)
            {
                writer.WriteUInt32(init);
                writer.WriteInt32(INIT_Chunk.Length);
                writer.WriteFromBuffer(INIT_Chunk);
            }

            if (ME2STMGFallback != null)
            {
                writer.WriteUInt32(stmg);
                writer.WriteInt32(ME2STMGFallback.Length);
                writer.WriteFromBuffer(ME2STMGFallback);
            }

            if (InitStateManagement != null)
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
                    writer.WriteFromBuffer(h.ToBytes(sc.Game));
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
                    writer.WriteStringLatin1(name);
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
                writer.WriteFromBuffer(FXPR_Chunk);
            }

            if (ENVS_Chunk != null)
            {
                writer.WriteUInt32(envs);
                writer.WriteInt32(ENVS_Chunk.Length);
                writer.WriteFromBuffer(ENVS_Chunk);
            }

            if (Platform != null)
            {
                writer.WriteUInt32(plat);
                if (Platform.Length > 0)
                {
                    writer.WriteInt32(Platform.Length + 5);
                }
                else
                {
                    writer.WriteInt32(4);
                }
                writer.WriteUnrealStringLatin1(Platform);
            }
        }

        #endregion

        /// <summary>
        /// Utility method: Writes the raw bytes of a bank to an export's binary.
        /// </summary>
        /// <param name="bankData"></param>
        /// <param name="exp"></param>
        public static void WriteBankRaw(byte[] bankData, ExportEntry exp)
        {
            MemoryStream outStream = new MemoryStream((exp.Game == MEGame.LE2 ? 24 : 16) + bankData.Length); // This must exist or GetBuffer() will return the wrong size.

            if (exp.Game == MEGame.LE2)
            {
                // Write Bulk Data header
                outStream.WriteInt32(0x1); // Unknown
                outStream.WriteInt32(0x1); // Unknown
            }

            // Write Bulk Data header
            outStream.WriteInt32(0); // Local
            outStream.WriteInt32((int)bankData.Length); // Compressed size
            outStream.WriteInt32((int)bankData.Length); // Decompressed size
            outStream.WriteInt32(0); // Data offset - this is not external so this is not used

            outStream.Write(bankData);
            exp.WriteBinary(outStream.GetBuffer());
        }

        public class HIRCObject
        {
            public HIRCType Type;
            public uint ID;
            public virtual int DataLength(MEGame game) => unparsed.Length + 4;
            public byte[] unparsed;

            public static HIRCObject Create(SerializingContainer2 sc)
            {
                HIRCType type = (HIRCType)((sc.Game is MEGame.ME2) ? (byte)sc.ms.ReadInt32() : sc.ms.ReadByte());
                int len = sc.ms.ReadInt32();
                uint id = sc.ms.ReadUInt32();
                return type switch
                {
                    //TODO: figure out SoundSXFSoundVoice for LE
                    HIRCType.SoundSXFSoundVoice when sc.Game.IsOTGame() => SoundSFXVoice.Create(sc, id, len),
                    HIRCType.Event => Event.Create(sc, id),
                    HIRCType.EventAction => EventAction.Create(sc, id, len),
                    _ => new HIRCObject
                    {
                        Type = type,
                        ID = id,
                        unparsed = sc.ms.ReadBytes(len - 4)
                    }
                };
            }

            public virtual byte[] ToBytes(MEGame game)
            {
                using MemoryStream ms = WriteHIRCObjectHeader(game);
                ms.WriteFromBuffer(unparsed);
                return ms.ToArray();
            }

            protected MemoryStream WriteHIRCObjectHeader(MEGame game)
            {
                var ms = MemoryManager.GetMemoryStream();
                if (game is MEGame.ME2)
                {
                    ms.WriteInt32((byte)Type);
                }
                else
                {
                    ms.WriteByte((byte)Type);
                }

                ms.WriteInt32(DataLength(game));
                ms.WriteUInt32(ID);
                return ms;
            }

            public virtual HIRCObject Clone()
            {
                HIRCObject clone = (HIRCObject)MemberwiseClone();
                clone.unparsed = unparsed?.ArrayClone();
                return clone;
            }
        }

        public class SoundSFXVoice : HIRCObject
        {
            public uint Unk1;
            public SoundState State;
            public uint AudioID;
            public uint SourceID;
            public int UnkType;
            public int UnkPrefetchLength;
            public SoundType SoundType; //0=SFX, 1=Voice

            private int ParsedLength => 21 + (State == SoundState.Streamed ? 0 : 8);
            public override int DataLength(MEGame game) => unparsed.Length + ParsedLength;

            public override byte[] ToBytes(MEGame game)
            {
                using MemoryStream ms = WriteHIRCObjectHeader(game);
                ms.WriteUInt32(Unk1);
                ms.WriteUInt32((uint)State);
                ms.WriteUInt32(AudioID);
                ms.WriteUInt32(SourceID);
                if (State != SoundState.Streamed)
                {
                    ms.WriteInt32(UnkType);
                    ms.WriteInt32(UnkPrefetchLength);
                }
                ms.WriteByte((byte)SoundType);
                ms.WriteFromBuffer(unparsed);
                return ms.ToArray();
            }

            public static SoundSFXVoice Create(SerializingContainer2 sc, uint id, int len)
            {
                SoundSFXVoice sfxVoice = new SoundSFXVoice
                {
                    Type = HIRCType.SoundSXFSoundVoice,
                    ID = id,
                    Unk1 = sc.ms.ReadUInt32(),
                    State = (SoundState)sc.ms.ReadUInt32(),
                    AudioID = sc.ms.ReadUInt32(),
                    SourceID = sc.ms.ReadUInt32()
                };
                if (sfxVoice.State != SoundState.Streamed)
                {
                    sfxVoice.UnkType = sc.ms.ReadInt32();
                    sfxVoice.UnkPrefetchLength = sc.ms.ReadInt32();
                }
                sfxVoice.SoundType = (SoundType)sc.ms.ReadByte();
                sfxVoice.unparsed = sc.ms.ReadBytes(len - sfxVoice.ParsedLength);
                return sfxVoice;
            }
        }

        public enum SoundType : byte
        {
            SFX = 0,
            Voice = 1
        }

        public enum SoundState : uint
        {
            Embed = 0,
            Streamed = 1,
            StreamPrefetched = 2
        }

        //public string[] ActionTypes = {"Stop", "Pause", "Resume", "Play", "Trigger", "Mute", "UnMute", "Set Voice Pitch", "Reset Voice Pitch", "Set Voice Volume", "Reset Voice Volume", "Set Bus Volume", "Reset Bus Volume", "Set Voice Low-pass Filter", "Reset Voice Low-pass Filter", "Enable State" , "Disable State", "Set State", "Set Game Parameter", "Reset Game Parameter", "Set Switch", "Enable Bypass or Disable Bypass", "Reset Bypass Effect", "Break", "Seek"};
        //public string[] EventScopes = { "Game object: Switch or Trigger", "Global", "Game object: by ID", "Game object: State", "All", "All Except ID" };
        public class Event : HIRCObject
        {
            public List<uint> EventActions;

            public override int DataLength(MEGame game) => (game.IsLEGame() ? 5 : 8) + EventActions.Count * 4;

            public override byte[] ToBytes(MEGame game)
            {
                using MemoryStream ms = WriteHIRCObjectHeader(game);
                if (game.IsLEGame())
                {
                    ms.WriteByte((byte)EventActions.Count);
                }
                else
                {
                    ms.WriteInt32(EventActions.Count);
                }
                foreach (uint eventAction in EventActions)
                {
                    ms.WriteUInt32(eventAction);
                }
                return ms.ToArray();
            }

            public override HIRCObject Clone()
            {
                var clone = (Event)MemberwiseClone();
                clone.EventActions = EventActions.Clone();
                return clone;
            }
            public static Event Create(SerializingContainer2 sc, uint id)
            {
                var list = new List<uint>();
                int count = sc.Game.IsLEGame() ? sc.ms.ReadByte() : sc.ms.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    list.Add(sc.ms.ReadUInt32());
                }
                return new Event
                {
                    Type = HIRCType.Event,
                    ID = id,
                    EventActions = list
                };
            }
        }

        public enum EventActionScope : byte
        {
            Global = 0x10,
            GameAction = 0x11,
            Global_LE = 0x02,
            GameObject_LE = 0x03
        }

        public enum EventActionType : byte
        {
            Play = 0x40,
            Stop = 0x10,
            SetVolume = 0xA0,
            ResetVolume = 0xB0,
            SetLPF = 0xE0,
            ResetLPF = 0xF0,
            Stop_LE = 0x01,
            Pause_LE = 0x02,
            Play_LE = 0x04,
            SetVolume_LE = 0x0A,
            ResetVolume_LE = 0x0B,
            SetLPF_LE = 0x0E,
            ResetLPF_LE = 0x0F,
            Break_LE = 0x1C
        }

        public enum EventActionFadeCurve : byte
        {
            Log_Base3 = 0x00,
            Sine = 0x01,
            Log_Base141 = 0x02,
            InvertedSCurve = 0x03,
            Linear = 0x04,
            SCurve = 0x05,
            Exponential_141 = 0x06,
            ReciprocalSine = 0x07,
            Exponential_3 = 0x08
        }

        public class EventAction : HIRCObject
        {
            public EventActionScope Scope;
            public EventActionType ActionType;
            public ushort Unk1;
            public uint ReferencedObjectID;

            public override int DataLength(MEGame game) => (game.IsOTGame() ? 12 : 10) + unparsed.Length;

            public override byte[] ToBytes(MEGame game)
            {
                using MemoryStream ms = WriteHIRCObjectHeader(game);
                ms.WriteByte((byte)Scope);
                ms.WriteByte((byte)ActionType);
                if (game.IsOTGame())
                {
                    ms.WriteUInt16(Unk1);
                }
                ms.WriteUInt32(ReferencedObjectID);
                ms.WriteFromBuffer(unparsed);
                return ms.ToArray();
            }
            public static EventAction Create(SerializingContainer2 sc, uint id, int len)
            {
                var action = new EventAction
                {
                    Type = HIRCType.EventAction,
                    ID = id,
                    Scope = (EventActionScope)sc.ms.ReadByte(),
                    ActionType = (EventActionType)sc.ms.ReadByte()
                };
                int unparsedLength = len - 10;
                if (sc.Game.IsOTGame())
                {
                    unparsedLength -= 2;
                    action.Unk1 = sc.ms.ReadUInt16();
                }
                action.ReferencedObjectID = sc.ms.ReadUInt32();
                action.unparsed = sc.ms.ReadBytes(unparsedLength);
                return action;
            }
        }
    }

    public enum HIRCType : byte
    {
        Settings = 0x1,
        SoundSXFSoundVoice = 0x2,
        EventAction = 0x3,
        Event = 0x4,
        RandomOrSequenceContainer = 0x5,
        SwitchContainer = 0x6,
        ActorMixer = 0x7,
        AudioBus = 0x8,
        BlendContainer = 0x9,
        MusicSegment = 0xA,
        MusicTrack = 0xB,
        MusicSwitchContainer = 0xC,
        MusicPlaylistContainer = 0xD,
        Attenuation = 0xE,
        DialogueEvent = 0xF,
        MotionBus = 0x10,
        MotionFX = 0x11,
        Effect = 0x12,
        AuxiliaryBus = 0x13
    }

    public class WwiseStateManagement
    {
        public float VolumeThreshold;
        public ushort MaxVoiceInstances;
        public UMultiMap<uint, StateGroup> StateGroups; //TODO: Make this a UMap?
        public UMultiMap<uint, SwitchGroup> SwitchGroups; //TODO: Make this a UMap?
        public UMultiMap<uint, float> GameParameterDefaultValues; //TODO: Make this a UMap?

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
