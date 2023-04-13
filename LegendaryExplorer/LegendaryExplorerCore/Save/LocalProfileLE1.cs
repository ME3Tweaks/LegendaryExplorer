using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SixLabors.ImageSharp.PixelFormats;

namespace LegendaryExplorerCore.Save
{
    /// <summary>
    /// Serializer for LE1's GamerProfile.pcsav file.
    /// </summary>
    public class LocalProfileLE1
    {
        /// <summary>
        /// The version of the SaveGame. LE1's value is 50.
        /// </summary>
        public int SaveGameVersion { get; set; }

        /// <summary>
        /// The profile data for this local profile
        /// </summary>
        public GamerProfileSaveRecord GamerProfile { get; set; }

        /// <summary>
        /// Binary blob of keybinds. If none are set this will be empty.
        /// </summary>
        public byte[] Keybinds { get; set; }

        /// <summary>
        /// The CRC that was read when the file was deserialized. This is not used in serialization.
        /// </summary>
        public uint CRC { get; set; }


        public MemoryStream Serialize()
        {
            var ms = new MemoryStream();
            ms.WriteInt32(SaveGameVersion);
            GamerProfile.Serialize(ms);
            ms.WriteInt32(Keybinds.Length);
            ms.Write(Keybinds);

            // Write the CRC
            ms.WriteUInt32(Crc32.Compute(ms.ToArray())); // Use CRC method for coalesced. It seems to work
            return ms;
        }


        public static LocalProfileLE1 DeserializeLocalProfile(string filePath)
        {
            using var fs = File.OpenRead(filePath);
            return new LocalProfileLE1(fs);
        }

        public static LocalProfileLE1 DeserializeLocalProfile(Stream profileDataStream)
        {
            return new LocalProfileLE1(profileDataStream);
        }

        private LocalProfileLE1(Stream stream)
        {
            SaveGameVersion = stream.ReadInt32(); // Should be 50

            // Deserialize Profile Record
            GamerProfile = GamerProfileSaveRecord.Deserialize(stream);

            // Deserialize Keybinds
            Keybinds = stream.ReadToBuffer(stream.ReadInt32());

            // Read checksum (CRC)
            CRC = stream.ReadUInt32();
        }

    }

    /// <summary>
    /// Base class for all serializable LE1 save record types
    /// </summary>
    public abstract class SaveRecordSerializable
    {
        public abstract void Serialize(Stream stream);
    }

    public class GamerProfileSaveRecord : SaveRecordSerializable
    {
        public List<ProfileBoolSaveRecord> BoolVariables = new();
        public List<ProfileIntSaveRecord> IntVariables = new();
        public List<ProfileFloatSaveRecord> FloatVariables = new();
        public List<PlotManagerAchievementSaveRecord> PlotManagerAchievementMaps = new();

        public int LastUsedPlaythroughID;
        public List<ProfilePlaythroughSaveRecord> Playthroughs = new();

        public float LowestPlaythroughDamageTaken;
        public int MostMoneyAccumulated;
        public int MostPlaythroughPlayerKills;
        public int LowestPlaythroughPlayerDeaths;
        public float FastestPlaythroughTime;

        public List<ProfileRewardSaveRecord> RewardStats = new();

        public List<int> AchievementStates = new();

        public List<BonusTalentSaveRecord> UnlockedBonusTalents = new();
        public List<BonusTalentSaveRecord> PassiveBonusTalents = new();

        public List<int> IntStats = new();
        public List<float> FloatStats = new();

        public List<CharacterProfileSaveRecord> CharacterProfiles = new();

        public GameOptionsSaveRecord GameOptions;

        public string LastPlayedCharacterID;
        public string LastSaveGame;

        /// <summary>
        /// Deserializes a <see cref="ProfileBoolSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfileBoolSaveRecord"/> object</returns>
        public static GamerProfileSaveRecord Deserialize(Stream stream)
        {
            GamerProfileSaveRecord gpsr = new GamerProfileSaveRecord();
            DeserializeArray(stream, x => gpsr.BoolVariables.Add(ProfileBoolSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.IntVariables.Add(ProfileIntSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.FloatVariables.Add(ProfileFloatSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.PlotManagerAchievementMaps.Add(PlotManagerAchievementSaveRecord.Deserialize(x)));
            gpsr.LastUsedPlaythroughID = stream.ReadInt32();
            DeserializeArray(stream, x => gpsr.Playthroughs.Add(ProfilePlaythroughSaveRecord.Deserialize(x)));
            gpsr.LowestPlaythroughDamageTaken = stream.ReadFloat();
            gpsr.MostMoneyAccumulated = stream.ReadInt32();
            gpsr.MostPlaythroughPlayerKills = stream.ReadInt32();
            gpsr.LowestPlaythroughPlayerDeaths = stream.ReadInt32();
            gpsr.FastestPlaythroughTime = stream.ReadFloat();
            DeserializeArray(stream, x => gpsr.RewardStats.Add(ProfileRewardSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.AchievementStates.Add(x.ReadInt32()));
            DeserializeArray(stream, x => gpsr.UnlockedBonusTalents.Add(BonusTalentSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.PassiveBonusTalents.Add(BonusTalentSaveRecord.Deserialize(x)));
            DeserializeArray(stream, x => gpsr.IntStats.Add(x.ReadInt32()));
            DeserializeArray(stream, x => gpsr.FloatStats.Add(x.ReadFloat()));
            DeserializeArray(stream, x => gpsr.CharacterProfiles.Add(CharacterProfileSaveRecord.Deserialize(x)));
            gpsr.GameOptions = GameOptionsSaveRecord.Deserialize(stream);
            gpsr.LastPlayedCharacterID = stream.ReadUnrealString();
            gpsr.LastSaveGame = stream.ReadUnrealString();

            return gpsr;
        }

        private static void DeserializeArray(Stream bin, Action<Stream> deserializer, bool debugPoint = false)
        {
            if (debugPoint)
            {
                Debug.WriteLine("Breakpoint on me");
            }
            int count = bin.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                if (debugPoint)
                    Debug.WriteLine($"debugPoint {i} at 0x{bin.Position:X8}");
                deserializer(bin);
            }
        }

        private static void SerializeArray(Stream bin, List<SaveRecordSerializable> arrayToSerialize, bool debugPoint = false)
        {
            if (debugPoint)
            {
                Debug.WriteLine("Breakpoint on me");
            }

            bin.WriteInt32(arrayToSerialize.Count);
            for (int i = 0; i < arrayToSerialize.Count; i++)
            {
                if (debugPoint)
                    Debug.WriteLine($"debugPoint {i} at 0x{bin.Position:X8}");
                arrayToSerialize[i].Serialize(bin);
            }
        }
        private static void SerializeArray(Stream bin, List<int> arrayToSerialize, bool debugPoint = false)
        {
            if (debugPoint)
            {
                Debug.WriteLine("Breakpoint on me");
            }

            bin.WriteInt32(arrayToSerialize.Count);
            for (int i = 0; i < arrayToSerialize.Count; i++)
            {
                if (debugPoint)
                    Debug.WriteLine($"debugPoint {i} at 0x{bin.Position:X8}");
                bin.WriteInt32(arrayToSerialize[i]);
            }
        }

        private static void SerializeArray(Stream bin, List<float> arrayToSerialize, bool debugPoint = false)
        {
            if (debugPoint)
            {
                Debug.WriteLine("Breakpoint on me");
            }

            bin.WriteInt32(arrayToSerialize.Count);
            for (int i = 0; i < arrayToSerialize.Count; i++)
            {
                if (debugPoint)
                    Debug.WriteLine($"debugPoint {i} at 0x{bin.Position:X8}");
                bin.WriteFloat(arrayToSerialize[i]);
            }
        }


        public override void Serialize(Stream stream)
        {
            SerializeArray(stream, BoolVariables.ToList<SaveRecordSerializable>());
            SerializeArray(stream, IntVariables.ToList<SaveRecordSerializable>());
            SerializeArray(stream, FloatVariables.ToList<SaveRecordSerializable>());
            SerializeArray(stream, PlotManagerAchievementMaps.ToList<SaveRecordSerializable>());
            stream.WriteInt32(LastUsedPlaythroughID);
            SerializeArray(stream, Playthroughs.ToList<SaveRecordSerializable>());
            stream.WriteFloat(LowestPlaythroughDamageTaken);
            stream.WriteInt32(MostMoneyAccumulated);
            stream.WriteInt32(MostPlaythroughPlayerKills);
            stream.WriteInt32(LowestPlaythroughPlayerDeaths);
            stream.WriteFloat(FastestPlaythroughTime);
            SerializeArray(stream, RewardStats.ToList<SaveRecordSerializable>());
            SerializeArray(stream, AchievementStates);
            SerializeArray(stream, UnlockedBonusTalents.ToList<SaveRecordSerializable>());
            SerializeArray(stream, PassiveBonusTalents.ToList<SaveRecordSerializable>());
            SerializeArray(stream, IntStats);
            SerializeArray(stream, FloatStats);
            SerializeArray(stream, CharacterProfiles.ToList<SaveRecordSerializable>());
            GameOptions.Serialize(stream);
            stream.WriteUnrealString(LastPlayedCharacterID, MEGame.LE1);
            stream.WriteUnrealString(LastSaveGame, MEGame.LE1);
        }
    }

    public class ProfileBoolSaveRecord : SaveRecordSerializable
    {
        public int PlotManagerIndex { get; set; }
        public int PlotManagerValue { get; set; }

        /// <summary>
        /// Deserializes a <see cref="ProfileBoolSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfileBoolSaveRecord"/> object</returns>
        public static ProfileBoolSaveRecord Deserialize(Stream stream)
        {
            return new ProfileBoolSaveRecord()
            {
                PlotManagerIndex = stream.ReadInt32(),
                PlotManagerValue = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(PlotManagerIndex);
            stream.WriteInt32(PlotManagerValue);
        }
    }

    // Technically this is identical to Bool...
    public class ProfileIntSaveRecord : SaveRecordSerializable
    {
        public int PlotManagerIndex { get; set; }
        public int PlotManagerValue { get; set; }

        /// <summary>
        /// Deserializes a <see cref="ProfileIntSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfileIntSaveRecord"/> object</returns>
        public static ProfileIntSaveRecord Deserialize(Stream stream)
        {
            return new ProfileIntSaveRecord()
            {
                PlotManagerIndex = stream.ReadInt32(),
                PlotManagerValue = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(PlotManagerIndex);
            stream.WriteInt32(PlotManagerValue);
        }
    }

    public class ProfileFloatSaveRecord : SaveRecordSerializable
    {
        public int PlotManagerIndex { get; set; }
        public float PlotManagerValue { get; set; }

        /// <summary>
        /// Deserializes a <see cref="ProfileFloatSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfileFloatSaveRecord"/> object</returns>
        public static ProfileFloatSaveRecord Deserialize(Stream stream)
        {
            return new ProfileFloatSaveRecord()
            {
                PlotManagerIndex = stream.ReadInt32(),
                PlotManagerValue = stream.ReadFloat()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(PlotManagerIndex);
            stream.WriteFloat(PlotManagerValue);
        }
    }

    public class PlotManagerAchievementSaveRecord : SaveRecordSerializable
    {
        public int PlotManagerIndex { get; set; }
        public int AcheivementId { get; set; }
        public int UnlockedAt { get; set; }

        /// <summary>
        /// Deserializes a <see cref="PlotManagerAchievementSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="PlotManagerAchievementSaveRecord"/> object</returns>
        public static PlotManagerAchievementSaveRecord Deserialize(Stream stream)
        {
            return new PlotManagerAchievementSaveRecord()
            {
                PlotManagerIndex = stream.ReadInt32(),
                AcheivementId = stream.ReadInt32(),
                UnlockedAt = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(PlotManagerIndex);
            stream.WriteInt32(AcheivementId);
            stream.WriteInt32(UnlockedAt);
        }
    }

    public class ProfilePlaythroughSaveRecord : SaveRecordSerializable
    {
        public int PlaythroughID { get; set; }
        public int DifficultySetting { get; set; }

        /// <summary>
        /// Deserializes a <see cref="ProfilePlaythroughSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfilePlaythroughSaveRecord"/> object</returns>
        public static ProfilePlaythroughSaveRecord Deserialize(Stream stream)
        {
            return new ProfilePlaythroughSaveRecord()
            {
                PlaythroughID = stream.ReadInt32(),
                DifficultySetting = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(PlaythroughID);
            stream.WriteInt32(DifficultySetting);
        }
    }

    public class ProfileRewardSaveRecord : SaveRecordSerializable
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public int UnlockedAt { get; set; }
        public int AchievementId { get; set; }
        public int IsAchievementUnlocked { get; set; }
        public int TalentTreeID { get; set; }

        /// <summary>
        /// Deserializes a <see cref="ProfileRewardSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="ProfileRewardSaveRecord"/> object</returns>
        public static ProfileRewardSaveRecord Deserialize(Stream stream)
        {
            return new ProfileRewardSaveRecord()
            {
                Name = stream.ReadUnrealString(),
                Value = stream.ReadInt32(),
                UnlockedAt = stream.ReadInt32(),
                AchievementId = stream.ReadInt32(),
                IsAchievementUnlocked = stream.ReadInt32(), // Not 0 = true
                TalentTreeID = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteUnrealString(Name, MEGame.LE1);
            stream.WriteInt32(Value);
            stream.WriteInt32(UnlockedAt);
            stream.WriteInt32(AchievementId);
            stream.WriteInt32(IsAchievementUnlocked);
            stream.WriteInt32(TalentTreeID);
        }
    }

    public class BonusTalentSaveRecord : SaveRecordSerializable
    {
        public int AchievementId { get; set; }
        public int BonusTalentID { get; set; }

        /// <summary>
        /// Deserializes a <see cref="BonusTalentSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="BonusTalentSaveRecord"/> object</returns>
        public static BonusTalentSaveRecord Deserialize(Stream stream)
        {
            return new BonusTalentSaveRecord()
            {
                AchievementId = stream.ReadInt32(),
                BonusTalentID = stream.ReadInt32()
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(AchievementId);
            stream.WriteInt32(BonusTalentID);
        }
    }

    public class CharacterProfileSaveRecord : SaveRecordSerializable
    {
        public enum EBioPartyMemberClassBase
        {
            BIO_PARTY_MEMBER_CLASS_BASE_SOLDIER,
            BIO_PARTY_MEMBER_CLASS_BASE_ENGINEER,
            BIO_PARTY_MEMBER_CLASS_BASE_ADEPT,
            BIO_PARTY_MEMBER_CLASS_BASE_INFILTRATOR,
            BIO_PARTY_MEMBER_CLASS_BASE_SAVANT,
            BIO_PARTY_MEMBER_CLASS_BASE_REAVER,
            BIO_PARTY_MEMBER_CLASS_BASE_ASARI_SCIENTIST,
            BIO_PARTY_MEMBER_CLASS_BASE_KROGAN_OLD_ONE,
            BIO_PARTY_MEMBER_CLASS_BASE_TURIAN_SPECTRE,
            BIO_PARTY_MEMBER_CLASS_BASE_QUARIAN_TINKER,
            BIO_PARTY_MEMBER_CLASS_BASE_SUPERSOLDIER,
            BIO_PARTY_MEMBER_CLASS_BASE_WOMAN_VETERAN,
            BIO_PARTY_MEMBER_CLASS_BASE_MAN_THINKER,
        };

        public enum EBioPlayerCharacterBackgroundOrigin
        {
            BIO_PLAYER_CHARACTER_BACKGROUND_ORIGIN_NONE,
            BIO_PLAYER_CHARACTER_BACKGROUND_ORIGIN_SPACER,
            BIO_PLAYER_CHARACTER_BACKGROUND_ORIGIN_COLONY,
            BIO_PLAYER_CHARACTER_BACKGROUND_ORIGIN_EARTHBORN,
        }

        public enum EBioPlayerCharacterBackgroundNotoriety
        {
            BIO_PLAYER_CHARACTER_BACKGROUND_NOTORIETY_NONE,
            BIO_PLAYER_CHARACTER_BACKGROUND_NOTORIETY_SURVIVOR,
            BIO_PLAYER_CHARACTER_BACKGROUND_NOTORIETY_WARHERO,
            BIO_PLAYER_CHARACTER_BACKGROUND_NOTORIETY_RUTHLESS,
        };

        public string CharacterID { get; set; }
        public string FullName { get; set; }
        public CharacterStatisticsSaveRecord CharacterStatistics { get; set; }
        public EBioPartyMemberClassBase ClassBase { get; set; }
        public EBioPlayerCharacterBackgroundOrigin Origin { get; set; }
        public EBioPlayerCharacterBackgroundNotoriety Reputation { get; set; }
        public int LastPlayedSlot { get; set; }
        public int CharacterLevel { get; set; }
        public int CreationYear { get; set; }
        public int CreationMonth { get; set; }
        public int CreationDayOfWeek { get; set; }
        public int CreationDay { get; set; }
        public int CreationHour { get; set; }
        public int CreationMin { get; set; }
        public int CreationSec { get; set; }
        public int CreationMSec { get; set; }
        public int PlayedHours { get; set; }
        public int PlayedMin { get; set; }
        public int PlayedSec { get; set; }

        /// <summary>
        /// Deserializes a <see cref="CharacterProfileSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="CharacterProfileSaveRecord"/> object</returns>
        public static CharacterProfileSaveRecord Deserialize(Stream stream)
        {
            return new CharacterProfileSaveRecord()
            {
                CharacterID = stream.ReadUnrealString(),
                FullName = stream.ReadUnrealString(),
                CharacterStatistics = CharacterStatisticsSaveRecord.Deserialize(stream),
                ClassBase = (EBioPartyMemberClassBase)stream.ReadByte(),
                Origin = (EBioPlayerCharacterBackgroundOrigin)stream.ReadByte(),
                Reputation = (EBioPlayerCharacterBackgroundNotoriety)stream.ReadByte(),
                LastPlayedSlot = stream.ReadInt32(),
                CharacterLevel = stream.ReadInt32(),
                CreationYear = stream.ReadInt32(),
                CreationMonth = stream.ReadInt32(),
                CreationDayOfWeek = stream.ReadInt32(),
                CreationDay = stream.ReadInt32(),
                CreationHour = stream.ReadInt32(),
                CreationMin = stream.ReadInt32(),
                CreationSec = stream.ReadInt32(),
                CreationMSec = stream.ReadInt32(),
                PlayedHours = stream.ReadInt32(),
                PlayedMin = stream.ReadInt32(),
                PlayedSec = stream.ReadInt32(),
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteUnrealString(CharacterID, MEGame.LE1);
            stream.WriteUnrealString(FullName, MEGame.LE1);
            CharacterStatistics.Serialize(stream);
            stream.WriteByte((byte)ClassBase);
            stream.WriteByte((byte)Origin);
            stream.WriteByte((byte)Reputation);
            stream.WriteInt32(LastPlayedSlot);
            stream.WriteInt32(CharacterLevel);
            stream.WriteInt32(CreationYear);
            stream.WriteInt32(CreationMonth);
            stream.WriteInt32(CreationDayOfWeek);
            stream.WriteInt32(CreationDay);
            stream.WriteInt32(CreationHour);
            stream.WriteInt32(CreationMin);
            stream.WriteInt32(CreationSec);
            stream.WriteInt32(CreationMSec);
            stream.WriteInt32(PlayedHours);
            stream.WriteInt32(PlayedMin);
            stream.WriteInt32(PlayedSec);

        }
    }

    public class CharacterStatisticsSaveRecord : SaveRecordSerializable
    {
        public int Stamina { get; set; }
        public int Focus { get; set; }
        public int Precision { get; set; }
        public int Coordination { get; set; }

        /// <summary>
        /// Deserializes a <see cref="CharacterStatisticsSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="CharacterStatisticsSaveRecord"/> object</returns>
        public static CharacterStatisticsSaveRecord Deserialize(Stream stream)
        {
            return new CharacterStatisticsSaveRecord()
            {
                Stamina = stream.ReadInt32(),
                Focus = stream.ReadInt32(),
                Precision = stream.ReadInt32(),
                Coordination = stream.ReadInt32(),
            };
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(Stamina);
            stream.WriteInt32(Focus);
            stream.WriteInt32(Precision);
            stream.WriteInt32(Coordination);
        }
    }

    public class GameOptionsSaveRecord : SaveRecordSerializable
    {
        public enum EOption
        {
            OPTION_TYPE_COMBAT_DIFFICULTY = 0,
            OPTION_TYPE_DIALOG_MODE = 1,
            OPTION_TYPE_AUTO_LEVELUP = 2,
            OPTION_TYPE_AUTO_EQUIP = 3,
            OPTION_TYPE_TUTORIAL_FLAG = 4,
            OPTION_TYPE_SUBTITLES = 5,
            OPTION_TYPE_AUTOPAUSE_ENEMY_SIGHTED = 6,
            OPTION_TYPE_AUTOPAUSE_SQUADMEMBER_DOWN = 7,
            OPTION_TYPE_BRIGHTNESS = 8,
            OPTION_TYPE_DISPLAY_SETTING = 9,
            OPTION_TYPE_MUSIC_VOLUME = 10,
            OPTION_TYPE_FX_VOLUME = 11,
            OPTION_TYPE_DIALOG_VOLUME = 12,
            OPTION_TYPE_INVERT_YAXIS = 13,
            OPTION_TYPE_SOUTHPAW_FLAG = 14,
            OPTION_TYPE_TARGET_ASSIST_MODE = 15,
            OPTION_TYPE_H_COMBAT_SENSITIVITY = 16,
            OPTION_TYPE_V_COMBAT_SENSITIVITY = 17,
            OPTION_TYPE_H_EXPLORATION_SENSITIVITY = 18,
            OPTION_TYPE_V_EXPLORATION_SENSITIVITY = 19,
            OPTION_TYPE_RUMBLE_FLAG = 20,
            OPTION_TYPE_AUTOPAUSE_BLEEDOUT = 21,
            OPTION_TYPE_MOTION_BLUR = 22,
            OPTION_TYPE_FILM_GRAIN = 23,
            OPTION_TYPE_SQUAD_POWER_USE = 24,
            OPTION_TYPE_AUTO_SAVE = 25,
            OPTION_TYPE_STICK_CONFIGURATION = 26,
            OPTION_TYPE_TRIGGER_CONFIGURATION = 27,
            OPTION_TYPE_MOUSE_SENSITIVITY = 28,
            OPTION_TYPE_ANALOG_SENSITIVITY = 29,
            OPTION_TYPE_MAKO_STEERING = 30,
            OPTION_TYPE_ANTI_ALIASING = 31,
            OPTION_TYPE_VSYNC = 32,
            OPTION_TYPE_DYNAMIC_SHADOWS = 33,
            OPTION_TYPE_TEXTURE_DETAIL = 34,
            OPTION_TYPE_DYNAMIC_RESOLUTION = 35,
            OPTION_TYPE_HDR_ENABLE = 36,
            OPTION_TYPE_HDR_CONTRAST = 37,
            OPTION_TYPE_HDR_BRIGHTNESS = 38,
            OPTION_TYPE_AMBIENT_OCCLUSION = 39,
            OPTION_TYPE_LEGACY_LEVELUP = 40,
            OPTION_TYPE_SELECTED_MONITOR = 41,
            OPTION_TYPE_UNCAPPED_FRAMERATE = 42,
            OPTION_TYPE_MAX = 43,
        };

        public Dictionary<EOption, int> Options = new();

        /// <summary>
        /// Deserializes a <see cref="GameOptionsSaveRecord"/> from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>A <see cref="GameOptionsSaveRecord"/> object</returns>
        public static GameOptionsSaveRecord Deserialize(Stream stream)
        {
            var options = new GameOptionsSaveRecord();
            var count = stream.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                options.Options[(EOption)i] = stream.ReadInt32();
            }

            return options;
        }

        /// <summary>
        /// Serializes this record to the stream
        /// </summary>
        /// <param name="stream">The stream to serialize to</param>
        public override void Serialize(Stream stream)
        {
            stream.WriteInt32(Options.Count);
            foreach (var option in Options)
            {
                stream.WriteInt32(option.Value);
            }
        }
    }
}
