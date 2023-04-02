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

    [AddINotifyPropertyChangedInterface]
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
        /// The type of data the <see cref="Data"/> object holds.
        /// </summary>
        public EProfileSettingType DataType { get; set; }

        /// <summary>
        /// The SFXProfileSettings Id
        /// </summary>
        public int Id { get; set; }

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
    }


    /// <summary>
    /// Serializer and deserializer for Local_Profile files
    /// </summary>
    public class LocalProfile
    {
        public static LocalProfile DeserializeLocalProfile(string filePath, MEGame game)
        {
            using var fs = File.OpenRead(filePath);
            return new LocalProfile(fs, game);
        }

        public Dictionary<int, object> ProfileSettings = new(100); // ME3 uses 100 keys so it's likely we'll hit that much normally

        /// <summary>
        /// The version number of the profile settings file
        /// </summary>
        public int Version { get; set; }
        /// <summary>
        /// Deserializes a Local_Profile file from a stream
        /// </summary>
        /// <param name="stream">Stream to deserialize</param>
        /// <param name="game">Game the stream is for</param>
        private LocalProfile(Stream stream, MEGame game)
        {

            var reader = new EndianReader(stream) { Endian = Endian.Big }; // LE3 uses big endian

            // 1. Decompress data
            var checksum = reader.ReadToBuffer(0x14); // 20 bytes
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

            byte[] decompressedData = new byte[decompressedSize];
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
            }

            // 2. Deserialize
            var profileStream = new MemoryStream(decompressedData);
            profileStream.WriteToFile(@"B:\UserProfile\Documents\BioWare\Mass Effect Legendary Edition\Save\ME3\Local_Profile_decompressed.bin");
            var profileReader = new EndianReader(profileStream) { Endian = Endian.Big };

            var numSettings = profileReader.ReadInt32();
            for (int i = 0; i < numSettings; i++)
            {
                ProfileSetting setting = new ProfileSetting();

                // Read ID
                var idType = profileReader.ReadByte();
#if AZURE
                if (idType != 1 && idType != 2)
                {
                    // This will be 0x1 INT
                    throw new Exception($@"Profile ID is not marked as type INT or INT64 at position 0x{(profileReader.Position - 1):X8}! Value: {idType}");
            }
#endif

                setting.Id = profileReader.ReadInt32();

                // Read Value
                setting.DataType = (ProfileSetting.EProfileSettingType)profileReader.ReadByte();
                switch (setting.DataType)
                {
                    case ProfileSetting.EProfileSettingType.NONE:
                        break;
                    case ProfileSetting.EProfileSettingType.INT:
                    case ProfileSetting.EProfileSettingType.INT64: // This seems to be 32bit still
                        setting.Data = profileReader.ReadInt32();
                        break;
                    case ProfileSetting.EProfileSettingType.DOUBLE:
                        setting.Data = profileReader.ReadDouble();
                        break;
                    case ProfileSetting.EProfileSettingType.STRING:
                        setting.Data = profileReader.ReadUnrealString();
                        break;
                    case ProfileSetting.EProfileSettingType.FLOAT:
                        setting.Data = profileReader.ReadFloat();
                        break;
                    case ProfileSetting.EProfileSettingType.BLOB:
                        var blobSize = profileReader.ReadInt32();
                        setting.Data = profileReader.ReadToBuffer(blobSize);
                        break;
                    case ProfileSetting.EProfileSettingType.DATETIME:
                        // Output is formatted as follows:
                        // Printf("%08X%08X",Val1, Val2);
                        // Not really sure what that means here....
                        setting.Data = new Tuple<int, int>(profileReader.ReadInt32(), profileReader.ReadInt32());
                        break;
                    default:
                        Debug.WriteLine($"ERROR: Invalid type encountered at 0x{(profileReader.Position - 1):X8}");
#if AZURE
                        throw new Exception($@"LocalProfile encounted invalid type: {setting.DataType}");
#endif
                        break;
                }

                // Read Empty value
                profileReader.ReadByte(); // Will be 0x0 EMPTY
                Debug.WriteLine($@"Setting {setting.Id} VALUE: {setting.Data}");

                if (setting.Id == 26)
                {
                    // Test LE1, LE2
                    // LE3 uses this to determine version number it seems
                    Version = setting.DataAsInt;
                }
                else
                {
                    ProfileSettings[setting.Id] = setting;
                }
            }

            // Verify checksum
            // Trying to figure out which one of these works...
            var expectedSHA = BitConverter.ToString(checksum).Replace(@"-", "").ToLowerInvariant();

            reader.Position = 0x14; // Read the compressed original data for verification. This includes decompressed size header on compressed data.
            var verifySha = BitConverter.ToString(SHA1.Create().ComputeHash(reader.BaseStream)).Replace(@"-", "").ToLowerInvariant();


            if (verifySha != expectedSHA)
            {
                throw new Exception("The SHA for this local profile did not verify, the file is likely corrupted");
            }
#if AZURE
            // For testing
            if (profileReader.Position != profileReader.Length)
            {
                throw new Exception($"Deserialization of Local_Profile failed. We read to 0x{profileReader.Position:X8} but data ends at 0x{profileReader.Length:X8}")
            }
#endif
        }
    }
}
