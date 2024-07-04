using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using PropertyChanged;

namespace LegendaryExplorerCore.Save
{
    /// <summary>
    /// Defines a profile setting for LE2/LE3 Local_Profile/GamerProfile
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    [DebuggerDisplay("ProfileSetting {Id} = {Data}")]

    public class ProfileSetting
    {
        /// <summary>
        /// The type of data the setting data reprsents
        /// </summary>
        public enum EProfileSettingType
        {
            NONE,
            INT,
            INT64, // Serialized as 32bit, in memory treated as 64bit. No idea why
            DOUBLE,
            STRING,
            FLOAT,
            BLOB,
            DATETIME
        }

        /// <summary>
        /// The data type for the profile - may be 32bit or 64bit
        /// </summary>
        public EProfileSettingType IdType { get; set; }

        /// <summary>
        /// The SFXProfileSettings Id. This is not stored as an enum because we want to be able to add additional values that may not exist in the enum currently
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The type of data the <see cref="Data"/> object holds.
        /// </summary>
        public EProfileSettingType DataType { get; set; }

        /// <summary>
        /// The data value of the setting
        /// </summary>
        public object Data { get; set; }

        // Various convenient methods
        public int DataAsInt => (int)Data;
        public double DataAsDouble => (double)Data;
        public string DataAsString => (string)Data;
        public float DataAsFloat => (float)Data;
        public byte[] DataAsBlob => (byte[])Data;
        public Tuple<int, int> DataAsDateTime => (Tuple<int, int>)Data;

        public void Serialize(EndianWriter ew, bool useUnicodeStrings = false)
        {
            ew.WriteByte((byte)IdType);
            ew.WriteInt32(Id);
            ew.WriteByte((byte)DataType);
            switch (DataType)
            {
                case EProfileSettingType.INT:
                case EProfileSettingType.INT64:
                    ew.WriteInt32(DataAsInt);
                    break;
                case EProfileSettingType.DOUBLE:
                    ew.WriteDouble(DataAsDouble);
                    break;
                case EProfileSettingType.STRING:
                    if (DataAsString.IsLatin1())
                    {
                        ew.WriteInt32(DataAsString.Length);
                        ew.WriteStringLatin1(DataAsString);
                    }
                    else
                    {
                        ew.WriteUnrealStringUnicode(DataAsString);
                    }
                    break;
                case EProfileSettingType.FLOAT:
                    ew.WriteFloat(DataAsFloat);
                    break;
                case EProfileSettingType.BLOB:
                    ew.WriteInt32(DataAsBlob.Length);
                    ew.Write(DataAsBlob);
                    break;
                case EProfileSettingType.DATETIME:
                    ew.WriteInt32(DataAsDateTime.Item1);
                    ew.WriteInt32(DataAsDateTime.Item2);
                    break;
            }
            ew.WriteByte(0); // EMPTY
        }

        public void Deserialize(EndianReader profileReader)
        {
            // Read ID
            IdType = (ProfileSetting.EProfileSettingType)profileReader.ReadByte();
#if AZURE
                if (IdType != ProfileSetting.EProfileSettingType.INT && IdType != ProfileSetting.EProfileSettingType.INT64)
                {
                    // This will be 0x1 INT
                    throw new Exception($@"Profile ID is not marked as type INT or INT64 at position 0x{(profileReader.Position - 1):X8}! Value: {IdType}");
                }
#endif

            Id = profileReader.ReadInt32();

            // Read Value
            DataType = (ProfileSetting.EProfileSettingType)profileReader.ReadByte();
            switch (DataType)
            {
                case ProfileSetting.EProfileSettingType.NONE:
                    break;
                case ProfileSetting.EProfileSettingType.INT:
                case ProfileSetting.EProfileSettingType.INT64: // This seems to be 32bit still
                    Data = profileReader.ReadInt32();
                    break;
                case ProfileSetting.EProfileSettingType.DOUBLE:
                    Data = profileReader.ReadDouble();
                    break;
                case ProfileSetting.EProfileSettingType.STRING:
                    Data = profileReader.ReadUnrealString();
                    break;
                case ProfileSetting.EProfileSettingType.FLOAT:
                    Data = profileReader.ReadFloat();
                    break;
                case ProfileSetting.EProfileSettingType.BLOB:
                    var blobSize = profileReader.ReadInt32();
                    Data = profileReader.ReadToBuffer(blobSize);
                    break;
                case ProfileSetting.EProfileSettingType.DATETIME:
                    // Output is formatted as follows:
                    // Printf("%08X%08X",Val1, Val2);
                    // Not really sure what that means here....
                    Data = new Tuple<int, int>(profileReader.ReadInt32(), profileReader.ReadInt32());
                    break;
                default:
                    Debug.WriteLine($"ERROR: Invalid type encountered at 0x{(profileReader.Position - 1):X8}");
#if AZURE
                        throw new Exception($@"LocalProfile encounted invalid type: {DataType}");
#endif
                    break;
            }

            // Read Empty value
            profileReader.ReadByte(); // Will be 0x0 EMPTY
        }
    }

    /// <summary>
    /// Serializer and deserializer for Local_Profile files
    /// </summary>
    [DebuggerDisplay("LocalProfile with {ProfileSettings.Count} settings")]
    public class LocalProfile
    {
        public static LocalProfile DeserializeLocalProfile(string filePath, MEGame game, bool isCompressed = true, bool usesVersionSetting = true)
        {
            using var fs = File.OpenRead(filePath);
            return new LocalProfile(fs, game, isCompressed, usesVersionSetting);
        }

        public Dictionary<int, ProfileSetting> ProfileSettings = new(100); // ME3 uses 100 keys so it's likely we'll hit that much normally

        /// <summary>
        /// If this profile object uses a setting value to store the version information
        /// </summary>
        public bool UsesVersionSetting { get; set; }

        /// <summary>
        /// If this profile object uses compression and SHA verification
        /// </summary>
        public bool UsesCompression { get; set; }


        /// <summary>
        /// The version number of the profile settings file
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Deserializes a Local_Profile file from a stream
        /// </summary>
        /// <param name="stream">Stream to deserialize</param>
        /// <param name="game">Game the stream is for</param>
        /// <param name="isCompressed">If this profile is compressed</param>
        /// <param name="hasVersionSetting">If this profile uses a setting to denote its version</param>
        /// <param name="useUnicodeStrings">If strings should be read as unicode, latin1 or determined on first read</param>
        private LocalProfile(Stream stream, MEGame game, bool isCompressed = true, bool hasVersionSetting = true)
        {
            UsesCompression = isCompressed;
            UsesVersionSetting = hasVersionSetting;

            byte[] decompressedData = null;
            EndianReader reader = null; //isCompressed only
            byte[] checksum = null; //isCompressed only
            if (UsesCompression)
            {
                reader = new EndianReader(stream) { Endian = Endian.Big }; // LE3 uses big endian

                // 1. Decompress data
                checksum = reader.ReadToBuffer(0x14); // 20 bytes
                var decompressedSize = reader.ReadInt32();
                var compressedProfileData = reader.ReadToBuffer((int)reader.Length - 0x18); // offset 24

                if (reader.Position != reader.Length)
                {
                    throw new Exception(@"Failed to read to end of profile file!");
                }

                if (game.IsLEGame())
                {
                    OodleHelper.EnsureOodleDll();
                }

                decompressedData = new byte[decompressedSize];
                if (game.IsLEGame())
                {
                    if (OodleHelper.Decompress(compressedProfileData, decompressedData) != decompressedSize)
                    {
                        throw new Exception(@"Decompression of profile data did not yield correct amount of bytes");
                    }
                }
                else
                {
                    // Handle other games here
                    if (Zlib.Decompress(compressedProfileData, decompressedData) != decompressedSize)
                    {
                        throw new Exception(@"Decompression of profile data did not yield correct amount of bytes");
                    }
                }
            }
            else
            {
                decompressedData = stream.ReadFully();
            }

            // 2. Deserialize settings
            var profileStream = new MemoryStream(decompressedData);
            var profileReader = new EndianReader(profileStream) { Endian = Endian.Big };

            var numSettings = profileReader.ReadInt32();
            for (int i = 0; i < numSettings; i++)
            {
                ProfileSetting setting = new ProfileSetting();
                setting.Deserialize(profileReader);

                //Debug.WriteLine($@"Setting {setting.Id} VALUE: {setting.Data}");

                if (UsesVersionSetting && setting.Id == (int)ELE3ProfileSetting.Setting_ProfileVersionNum) // 26, same in LE2
                {
                    // The version of the settings format
                    // LE3 = 50 ?
                    // LE2 = 24 ?
                    Version = setting.DataAsInt;
                }
                else
                {
                    ProfileSettings[(int)setting.Id] = setting;
                }
            }

            if (UsesCompression)
            {
                // 3. Verify checksum
                var expectedSHA = BitConverter.ToString(checksum).Replace(@"-", "").ToLowerInvariant();
                reader.Position = 0x14; // Read the compressed original data for verification. This includes decompressed size header on compressed data.
                var verifySha = BitConverter.ToString(SHA1.Create().ComputeHash(reader.BaseStream)).Replace(@"-", "").ToLowerInvariant();

                if (verifySha != expectedSHA)
                {
                    throw new Exception("The SHA for this local profile did not verify, the file is likely corrupted");
                }
            }
#if AZURE
            // For testing
            if (profileReader.Position != profileReader.Length)
            {
                throw new Exception($"Deserialization of Local_Profile failed. We read to 0x{profileReader.Position:X8} but data ends at 0x{profileReader.Length:X8}");
            }
#endif
        }

        public MemoryStream Serialize(bool inOrder = false)
        {
            // Prepare the compressed data
            using MemoryStream ms = new MemoryStream();
            EndianWriter ew = new EndianWriter(ms) { Endian = Endian.Big };
            var count = ProfileSettings.Count;
            if (UsesVersionSetting)
                count++;
            ew.WriteInt32(count); // +1 for VERSION

            // 12/22/2023 - Support writing settings in-order
            var vals = inOrder ? ProfileSettings.Values.OrderBy(x => x.Id).ToList() : ProfileSettings.Values.ToList();
            foreach (var profileSetting in vals)
            {
                if (UsesVersionSetting && profileSetting.Id == (byte)ELE3ProfileSetting.Setting_ProfileVersionNum) // Same as LE2, 26
                    continue;
                profileSetting.Serialize(ew);
            }

            // Write version at the end.
            if (UsesVersionSetting)
            {
                new ProfileSetting()
                {
                    IdType = ProfileSetting.EProfileSettingType.INT64,
                    Id = (int)ELE3ProfileSetting.Setting_ProfileVersionNum,
                    DataType = ProfileSetting.EProfileSettingType.INT,
                    Data = Version
                }.Serialize(ew);
            }

            if (UsesCompression)
            {
                // Compress the data
                var compressedData = OodleHelper.Compress(ew.ToArray());

                // Prepare the uncompressed data
                MemoryStream finalStream = new MemoryStream();
                EndianWriter finalEw = new EndianWriter(finalStream) { Endian = Endian.Big };
                finalEw.WriteZeros(0x14); // SHA placeholder
                finalEw.WriteInt32((int)ew.BaseStream.Length);
                finalEw.Write(compressedData);

                // Generate SHA1 checksum
                finalStream.Position = 0x14; // SHA ends here
                var shaBytes = SHA1.Create().ComputeHash(finalStream);
                finalStream.Position = 0;
                finalStream.Write(shaBytes);
                return finalStream;
            }

            // Return the uncompressed version
            MemoryStream uncompStream = new MemoryStream();
            ew.BaseStream.Position = 0;
            ew.BaseStream.CopyTo(uncompStream);
            uncompStream.Position = 0;
            return uncompStream;
        }

        // Enums

        public enum ELE2ProfileSetting
        {
            Setting_Unknown = 0,
            Setting_ControllerVibration = 1,
            Setting_YInversion = 2,
            Setting_GamerCred = 3,
            Setting_GamerRep = 4,
            Setting_VoiceMuted = 5,
            Setting_VoiceThruSpeakers = 6,
            Setting_VoiceVolume = 7,
            Setting_GamerPictureKey = 8,
            Setting_GamerMotto = 9,
            Setting_GamerTitlesPlayed = 10,
            Setting_GamerAchievementsEarned = 11,
            Setting_GameDifficulty = 12,
            Setting_ControllerSensitivity = 13,
            Setting_PreferredColor1 = 14,
            Setting_PreferredColor2 = 15,
            Setting_AutoAim = 16,
            Setting_AutoCenter = 17,
            Setting_MovementControl = 18,
            Setting_RaceTransmission = 19,
            Setting_RaceCameraLocation = 20,
            Setting_RaceBrakeControl = 21,
            Setting_RaceAcceleratorControl = 22,
            Setting_GameCredEarned = 23,
            Setting_GameAchievementsEarned = 24,
            Setting_EndLiveIds = 25,
            Setting_ProfileVersionNum = 26,
            Setting_ProfileSaveCount = 27,
            Setting_StickConfiguration = 28,
            Setting_TriggerConfiguration = 29,
            Setting_Subtitles = 30,
            Setting_AimAssist = 31,
            Setting_Difficulty = 32,
            Setting_AutoLevel = 33,
            Setting_SquadPowers = 34,
            Setting_AutoSave = 35,
            Setting_MusicVolume = 36,
            Setting_FXVolume = 37,
            Setting_DialogVolume = 38,
            Setting_MotionBlur = 39,
            Setting_FilmGrain = 40,
            Setting_SelectedDeviceID = 41,
            Setting_CurrentCareer = 42,
            Setting_DaysSinceRegistration = 43,
            Setting_AutoLogin = 44,
            Setting_LoginInfo = 45,
            Setting_PersonaID = 46,
            Setting_NucleusRefused = 47,
            Setting_NucleusSuccessful = 48,
            Setting_CerberusRefused = 49,
            Setting_Achievement_FieldA = 50,
            Setting_Achievement_FieldB = 51,
            Setting_Achievement_FieldC = 52,
            Setting_TelemetryCollectionEnabled = 53,
            Setting_KeyBindings = 54,
            Setting_DisplayGamma = 55,
            Setting_CurrentSaveGame = 56,
            Setting_VerticalSync = 57,
            Setting_AntiAliasing = 58,
            Setting_NumHeadshots = 59,
            Setting_NumPowerCombos = 60,
            Setting_NumScreams = 61,
            Setting_NumShieldsDisrupted = 62,
            Setting_NumBarriersWarped = 63,
            Setting_NumArmourIncinerated = 64,
            Setting_NumN7MissionsCompleted = 65,
            Setting_NumCodex = 66,
            Setting_ShowHints = 67,
            Setting_NumTrainingVidsWatched = 68,
            Setting_WatchedVid1 = 69,
            Setting_WatchedVid2 = 70,
            Setting_WatchedVid3 = 71,
            Setting_MorinthNotSamara = 72,
            Setting_MaxWeaponUpgradeCount = 73,
            Setting_LastFinishedCareer = 74,
            Setting_SwapTriggersShoulders = 75,
            Setting_PS3_RedeemedProductCode = 76,
            Setting_HDREnabled = 77,
            Setting_HDRBrightness = 78,
            Setting_HDRContrast = 79,
            Setting_DynamicResolution = 80,
            Setting_AmbientOcclusion = 81,
        }

        /// <summary>
        /// Setting IDs for LE3
        /// </summary>
        public enum ELE3ProfileSetting
        {
            Setting_Unknown = 0,
            Setting_ControllerVibration = 1,
            Setting_YInversion = 2,
            Setting_GamerCred = 3,
            Setting_GamerRep = 4,
            Setting_VoiceMuted = 5,
            Setting_VoiceThruSpeakers = 6,
            Setting_VoiceVolume = 7,
            Setting_GamerPictureKey = 8,
            Setting_GamerMotto = 9,
            Setting_GamerTitlesPlayed = 10,
            Setting_GamerAchievementsEarned = 11,
            Setting_GameDifficulty = 12,
            Setting_ControllerSensitivity = 13,
            Setting_PreferredColor1 = 14,
            Setting_PreferredColor2 = 15,
            Setting_AutoAim = 16,
            Setting_AutoCenter = 17,
            Setting_MovementControl = 18,
            Setting_RaceTransmission = 19,
            Setting_RaceCameraLocation = 20,
            Setting_RaceBrakeControl = 21,
            Setting_RaceAcceleratorControl = 22,
            Setting_GameCredEarned = 23,
            Setting_GameAchievementsEarned = 24,
            Setting_EndLiveIds = 25,
            Setting_ProfileVersionNum = 26,
            Setting_ProfileSaveCount = 27,
            Setting_StickConfiguration = 28,
            Setting_TriggerConfiguration = 29,
            Setting_Subtitles = 30,
            Setting_AimAssist = 31,
            Setting_Difficulty = 32,
            Setting_InitialGameDifficulty = 33,
            Setting_AutoLevel = 34,
            Setting_SquadPowers = 35,
            Setting_AutoSave = 36,
            Setting_MusicVolume = 37,
            Setting_FXVolume = 38,
            Setting_DialogVolume = 39,
            Setting_MotionBlur = 40,
            Setting_FilmGrain = 41,
            Setting_SelectedDeviceID = 42,
            Setting_CurrentCareer = 43,
            Setting_DaysSinceRegistration = 44,
            Setting_AutoLogin = 45,
            Setting_LoginInfo = 46,
            Setting_PersonaID = 47,
            Setting_NucleusRefused = 48,
            Setting_NucleusSuccessful = 49,
            Setting_CerberusRefused = 50,
            Setting_Achievement_FieldA = 51,
            Setting_Achievement_FieldB = 52,
            Setting_Achievement_FieldC = 53,
            Setting_TelemetryCollectionEnabled = 54,
            Setting_KeyBindings = 55,
            Setting_DisplayGamma = 56,
            Setting_CurrentSaveGame = 57,
            Setting_HideCinematicHelmet = 58,
            Setting_ActionIconUIHints = 59,
            Setting_AmbientOcclusion = 60,
            Setting_NumGameCompletions = 61,
            Setting_ShowHints = 62,
            Setting_MorinthNotSamara = 63,
            Setting_MaxWeaponUpgradeCount = 64,
            Setting_LastFinishedCareer = 65,
            Setting_SwapTriggersShoulders = 66,
            Setting_PS3_RedeemedProductCode = 67,
            Setting_LastSelectedPawn = 68,
            Setting_ShowScoreIndicators = 69,
            Setting_Accomplishment_FieldA = 70,
            Setting_Accomplishment_FieldB = 71,
            Setting_Accomplishment_FieldC = 72,
            Setting_Accomplishment_FieldD = 73,
            Setting_Accomplishment_FieldE = 74,
            Setting_Accomplishment_FieldF = 75,
            Setting_Accomplishment_FieldG = 76,
            Setting_Accomplishment_FieldH = 77,
            Setting_NumSalvageFound = 78,
            Setting_SPLevel = 79,
            Setting_NumKills = 80,
            Setting_NumMeleeKills = 81,
            Setting_NumShieldsOverloaded = 82,
            Setting_NumEnemiesFlying = 83,
            Setting_NumEnemiesOnFire = 84,
            Setting_CachedDisconnectError = 85,
            Setting_CachedDisconnectFromState = 86,
            Setting_CachedDisconnectToState = 87,
            Setting_CachedDisconnectSessionId = 88,
            Setting_Language_VO = 89,
            Setting_Language_Text = 90,
            Setting_Language_Speech = 91,
            Setting_MPAutoLevel = 92,
            Setting_GalaxyAtWarLevel = 93,
            Setting_N7Rating_LocalUser = 94,
            Setting_N7Rating_FriendBlob = 95,
            Setting_AutoReplyMode = 96,
            Setting_BonusPower = 97,
            Setting_NumGuardianHeadKilled = 98,
            Setting_MPCreateNewMatchPrivacySetting = 99,
            Setting_MPCreateNewMatchMapName = 100,
            Setting_MPCreateNewMatchEnemyType = 101,
            Setting_MPCreateNewMatchDifficulty = 102,
            Setting_HenchmenHelmetOption = 103,
            Setting_AudioDynamicRange = 104,
            Setting_NumPowerCombos = 105,
            Setting_SPMaps = 106,
            Setting_SPMapsCount = 107,
            Setting_NumArmorBought = 108,
            Setting_WeaponLevel = 109,
            Setting_SPMapsInsane = 110,
            Setting_SPMapsInsaneCount = 111,
            Setting_PowerLevel = 112,
            Setting_MPQuickMatchMapName = 113,
            Setting_MPQuickMatchEnemyType = 114,
            Setting_MPQuickMatchDifficulty = 115,
            Setting_KinectTutorialPromptViewed = 116,
            Setting_ChallengePoints_FriendBlob_LastSeen = 117,
            Setting_ChallengePoints_LocalUser_LastSeen = 118,
            Setting_HDREnabled = 119,
            Setting_HDRBrightness = 120,
            Setting_HDRContrast = 121,
            Setting_DynamicResolution = 122,
            Setting_AntiAliasing = 123,
            Setting_DynamicShadows = 124,
        };

        public void SetProfileSettingById(int id, object data)
        {
            if (ProfileSettings.TryGetValue(id, out var existing))
            {
                existing.Data = data;
            }
            else
            {
                ProfileSettings[id] = new ProfileSetting()
                {
                    IdType = ProfileSetting.EProfileSettingType.INT64,
                    Id = id,
                    DataType = GetDataType(data),
                    Data = Version
                };
            }
        }

        /// <summary>
        /// Gets the data type for a profile to store this type of data. Defaults to INT.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private ProfileSetting.EProfileSettingType GetDataType(object data)
        {
            if (data is null)
            {
                return ProfileSetting.EProfileSettingType.NONE;
            }
            if (data is DateTime)
            {
                return ProfileSetting.EProfileSettingType.DATETIME;
            }
            if (data is double)
            {
                return ProfileSetting.EProfileSettingType.DOUBLE;
            }
            if (data is float)
            {
                return ProfileSetting.EProfileSettingType.FLOAT;
            }
            if (data is string)
            {
                return ProfileSetting.EProfileSettingType.STRING;
            }
            if (data is byte[])
            {
                return ProfileSetting.EProfileSettingType.BLOB;
            }
            if (data is long)
            {
                return ProfileSetting.EProfileSettingType.INT64;
            }

            if (data is int)
            {
                return ProfileSetting.EProfileSettingType.INT;
            }

            throw new Exception($"Unsupported data type for {data} object");
        }
    }
}
