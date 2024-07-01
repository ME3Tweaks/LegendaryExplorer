using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Flags that can be set in a Game 2 Mount.dlc file to represent DLC loading options
    /// </summary>
    [Flags]
    public enum EME2MountFileFlag
    {
        // ME2
        /// <summary>
        /// DLC is not required in game save
        /// </summary>
        //NoSaveFileDependency = 0x0,

        /// <summary>
        /// DLC has an authentication check
        /// </summary>
        CerberusNetworkRequired = 0x1, // Used for some kind of auth

        /// <summary>
        /// When a game is saved while this DLC is loaded, the save is marked as requiring this DLC
        /// </summary>
        SaveFileDependency = 0x2,
    }

    /// <summary>
    /// Flags that can be set in a Game 3 Mount.dlc file to represent DLC loading options
    /// </summary>
    [Flags]
    public enum EME3MountFileFlag
    {
        /// <summary>
        /// DLC is not required in game save
        /// </summary>
        //NoSaveFileDependency = 0x0,

        /// <summary>
        /// When a game is saved while this DLC is loaded, the save is marked as requiring this DLC
        /// </summary>
        SaveFileDependency = 0x01,
        /// <summary>
        /// If networked clients can have differing DLC setups. Not sure how that works on a single DLC
        /// </summary>
        DLCSetupCanMisMatch = 0x02,
        /// <summary>
        /// This DLC loads in multiplayer
        /// </summary>
        LoadsInMultiplayer = 0x04,
        /// <summary>
        /// This DLC loads in singleplayer
        /// </summary>
        LoadsInSingleplayer = 0x08,
        /// <summary>
        /// This DLC is used in matchmaking in MP
        /// </summary>
        UsedInMatchMaking = 0x10,
        /// <summary>
        /// Requires Patch 1.04 and above features
        /// </summary>
        UsesGoBigFeatures = 0x20,
    }

    // ME1 (custom implementation)
    // Not actually used
    //ME1_SaveFileDependency = 0x100, //not an actual value.
    //ME1_NoSaveFileDependency = 0x101 //not an actual value.

    /// <summary>
    /// Interposer for the mount flag enum types
    /// </summary>
    public class MountFlag : INotifyPropertyChanged
    {
        protected bool Equals(MountFlag other)
        {
            return IsME2 == other.IsME2 && ME2Flag == other.ME2Flag && ME3Flag == other.ME3Flag;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MountFlag) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsME2, (int) ME2Flag, (int) ME3Flag);
        }

        private bool IsME2;
        private EME2MountFileFlag ME2Flag;
        private EME3MountFileFlag ME3Flag;

        /// <summary>
        /// Instantiates a mount flag with the input value for either Game 2 or Game 3
        /// </summary>
        /// <param name="flag">Initial flag</param>
        /// <param name="isME2">True for Game 2, false for Game 3</param>
        public MountFlag(int flag, bool isME2)
        {
            IsME2 = isME2;
            if (IsME2)
                ME2Flag = (EME2MountFileFlag)flag;
            else
                ME3Flag = (EME3MountFileFlag)flag;
        }

        /// <summary>
        /// Instantiates a mount flag for Game 2 with the given <see cref="EME2MountFileFlag"/> value
        /// </summary>
        /// <param name="flag">Mount flag</param>
        public MountFlag(EME2MountFileFlag flag)
        {
            IsME2 = true;
            ME2Flag = flag;
        }

        /// <summary>
        /// Instantiates a mount flag for Game 3 with the given <see cref="EME3MountFileFlag"/> value
        /// </summary>
        /// <param name="flag">Mount flag</param>
        public MountFlag(EME3MountFileFlag flag)
        {
            ME3Flag = flag;
        }

        /// <summary>
        /// Gets the bit-set flag value
        /// </summary>
        public int FlagValue
        {
            get
            {
                if (IsME2) return (int)ME2Flag;
                return (int)ME3Flag;
            }
        }

        /// <summary>
        /// Sets the specified flag bit for the current game
        /// </summary>
        /// <param name="flag">Flag to set</param>
        public void SetFlagBit(int flag)
        {
            if (IsME2) ME2Flag |= (EME2MountFileFlag)flag;
            else ME3Flag |= (EME3MountFileFlag)flag;
        }

        /// <summary>
        /// Sets the specified flag bit from a <see cref="EME2MountFileFlag"/>
        /// </summary>
        /// <param name="flag">Flag to set</param>
        public void SetFlagBit(EME2MountFileFlag flag)
        {
            ME2Flag |= flag;
        }

        /// <summary>
        /// Sets the specified flag bit from a <see cref="EME3MountFileFlag"/>
        /// </summary>
        /// <param name="flag">Flag to set</param>
        public void SetFlagBit(EME3MountFileFlag flag)
        {
            ME3Flag |= flag;
        }

        public bool IsUISelected { get; set; }

        /// <summary>
        /// Gets the single name of this flag. Do not use if this flag has multiple bits set
        /// </summary>
        public string DisplayString => ToString();

        /// <summary>
        /// Returns the single name of this flag. Do not use if this flag has multiple bits set
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (IsME2) return Enum.GetName(ME2Flag);
            return Enum.GetName(ME3Flag);
        }

        /// <summary>
        /// Converts this mount flag to a human readable full set of flags, separated by spaces
        /// </summary>
        /// <returns></returns>
        public string ToHumanReadableString()
        {
            List<string> setFlags = new();
            if (IsME2)
            {
                var flagset = Enum.GetValues<EME2MountFileFlag>();
                foreach (var f in flagset)
                {
                    if (ME2Flag.Has(f))
                        setFlags.Add(f.ToString());
                }
            }
            else
            {
                var flagset = Enum.GetValues<EME3MountFileFlag>();
                foreach (var f in flagset)
                {
                    if (ME3Flag.Has(f))
                        setFlags.Add(f.ToString());
                }
            }

            return string.Join(' ', setFlags);
        }

        // Disable warnings for Fody
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Represents a Mount.dlc file for a Game2 or Game3 DLC
    /// </summary>
    public class MountFile
    {
        /// <summary>Game that this mount file is for</summary>
        public MEGame Game { get; set; }
        /// <summary>Mount priority of this DLC. This number controls when this DLC will be loaded in comparison to other DLCs.</summary>
        public int MountPriority { get; set; }
        /// <summary>DLC folder name</summary>
        /// <remarks>This field is only used in Game 2</remarks>
        public string ME2Only_DLCFolderName { get; set; }
        /// <summary>Internal name of DLC</summary>
        /// <remarks>This field is only used in Game 2</remarks>
        public string ME2Only_DLCHumanName { get; set; }
        /// <summary>TLK string ref for internal name of DLC</summary>
        public int TLKID { get; set; }
        /// <summary>Mount flags for this DLC</summary>
        public MountFlag MountFlags { get; set; }
        /// <summary>
        /// Instantiates an empty mount file. Used for creating a new mount.
        /// </summary>
        public MountFile()
        { }

        /// <summary>
        /// Instantiates a mount file from a mount file on disk.
        /// </summary>
        /// <param name="filepath">Mountfile to load</param>
        public MountFile(string filepath)
        {
            using var ms = new MemoryStream(File.ReadAllBytes(filepath));
            byte b = (byte)ms.ReadByte();
            switch (b)
            {
                case 0:
                    Game = MEGame.ME2;
                    LoadMountFileME2(ms);
                    break;
                case 0xAC:
                    Game = MEGame.LE2;
                    LoadMountFileME2(ms);
                    break;
                default:
                    ms.JumpTo(0x4);
                    Game = ms.ReadInt32() == 0x2AC ? MEGame.ME3 : MEGame.LE3;
                    LoadMountFileME3(ms);
                    break;
            }
        }
        /// <summary>
        /// Static method to get the mount priority of the Mount.dlc file at the given filepath
        /// </summary>
        /// <param name="filepath">Path to a Mount.dlc file</param>
        /// <returns>Mount priority</returns>
        public static int GetMountPriority(string filepath) => new MountFile(filepath).MountPriority;

        private void LoadMountFileME2(MemoryStream ms)
        {
            ms.Seek(0x28, SeekOrigin.Begin);
            MountFlags = new MountFlag(ms.ReadInt32(), true);
            ms.Seek(0xC, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x2C, SeekOrigin.Begin);
            ME2Only_DLCHumanName = ms.ReadUnrealString();
            TLKID = ms.ReadInt32();
            ME2Only_DLCFolderName = ms.ReadUnrealString();
        }

        private void LoadMountFileME3(MemoryStream ms)
        {
            ms.Seek(0x10, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x18, SeekOrigin.Begin);
            MountFlags = new MountFlag(ms.ReadInt32(), false);
            ms.Seek(0x1C, SeekOrigin.Begin);
            TLKID = ms.ReadInt32();
        }

        /// <summary>
        /// Writes this mount object to the specified stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public void WriteMountFileToStream(Stream stream)
        {
            switch (Game)
            {
                case MEGame.ME2:
                    WriteME2Mount(stream);
                    break;
                case MEGame.ME3:
                    WriteME3Mount(stream);
                    break;
                case MEGame.LE2:
                    WriteLE2Mount(stream);
                    break;
                case MEGame.LE3:
                    WriteLE3Mount(stream);
                    break;
                default:
                    throw new Exception($"Cannot write a mount file for {Game}!");
            }
        }

        /// <summary>
        /// Writes this Mountfile to the specified path.
        /// </summary>
        /// <param name="path">Path to write mount file to</param>
        public void WriteMountFile(string path)
        {
            using MemoryStream ms = MemoryManager.GetMemoryStream();
            WriteMountFileToStream(ms);
            ms.WriteToFile(path);
        }

        private void WriteLE3Mount(Stream ms)
        {
            ms.WriteInt32(0x1); // MountingInfoVersion
            ms.WriteInt32(0x2AD); // PackageFileVersion
            ms.WriteInt32(0xCD); // PackageLicenseeVersion
            ms.WriteInt32(0x3006B); // PackageFileCookedContentVersion

            //@ 0x10 - Mount Priority
            ms.WriteInt32(MountPriority);
            ms.WriteInt32(0x0); // Version

            //@ 0x18 - Mount Flag
            ms.WriteInt32(MountFlags.FlagValue);

            //@ 0x1C - TLK ID (x2)
            ms.WriteInt32(TLKID); // Content Name
            ms.WriteInt32(TLKID); // Package Name

            // Todo: Implement proper loading and saving of these so if you load -> save an official mount, it doesn't change
            ms.WriteZeros(0x48); // Build version, GUIDs
        }

        private void WriteME3Mount(Stream ms)
        {
            ms.WriteInt32(0x1); //MountingInfoVersion
            ms.WriteInt32(0x2AC); // PackageFileVersion
            ms.WriteInt32(0xC2); // PackageLicenseeVersion
            ms.WriteInt32(0x3006B); // PackageFileCookedContentVersion

            //@ 0x10 - Mount Priority
            ms.WriteInt32(MountPriority);
            ms.WriteInt32(0x0); // Version

            //@ 0x18 - Mount Flag
            ms.WriteInt32(MountFlags.FlagValue);

            //@ 0x1C - TLK ID (x2)
            ms.WriteInt32(TLKID); // Content Name
            ms.WriteInt32(TLKID); // Package Name
            ms.WriteInt32(0x0); // Build Number Major
            ms.WriteInt32(0x0); // Build Number Minor

            // Todo: Implement proper loading and saving of these so if you load -> save an official mount, it doesn't change
            // TFC GUIDS
            // BASE
            // CHAR
            // LIGHTING
            // MOVIE
            ms.WriteZeros(0x40); // 4 GUIDs
        }

        private void WriteLE2Mount(Stream ms)
        {
            ms.WriteInt32(0x2AC); // 0x0 Version Package
            ms.WriteInt32(0xA8); // 0x4 Version Licensee
            ms.WriteInt32(0x1006B); // 0x8 Version Cooked
            ms.WriteInt32(MountPriority); // 0xC Mount Priority ("Module ID")
            ms.WriteInt32(0); // 0x10 Version DLC Mounting Info

            // TFC GUID - Zeros lead to no check
            // We might want to write out a read-in mount so incoming mount = outgoing mount, just for data preservation
            ms.WriteZeros(16); // 0x14 TFC GUID (4 DWORD)
            /* Old code
            //TODO: check all LE Mount.dlc files to ensure this is the correct condition
            if (MountFlags is not EMountFileFlag.ME2_UNKNOWNMOUNTFLAG)
            {
                //@ 0x14 - Appears to be a GUID. Common across all DLC though. Maybe some sort of magic GUID or something.
                var guidbytes = new byte[] { 0x94, 0x38, 0x77, 0xF4, 0x81, 0x35, 0xA7, 0x46, 0x91, 0xC3, 0xFD, 0xEB, 0x7D, 0x7E, 0xE6, 0x53 };
                ms.WriteFromBuffer(guidbytes);
            }
            else
            {
                ms.WriteZeros(16);
            }*/

            ms.WriteInt32(0x0); // 0x24 Version DLC
            ms.WriteInt32(MountFlags.FlagValue); // 0x28 Mount Flags
            ms.WriteUnrealStringUnicode(ME2Only_DLCHumanName); // 0x2C Friendly Name

            ms.WriteInt32(TLKID); // 0x00 after Friendly Name: srFriendly Name

            //@ 0x00 After TLKID - FolderName
            ms.WriteUnrealStringLatin1(ME2Only_DLCFolderName); // CodeName
            ms.WriteInt32(0x0); // Min Build Version
            ms.WriteInt32(TLKID); // Package Name
        }

        private void WriteME2Mount(Stream ms)
        {
            // Todo: Update, check if same as LE2 except for versions
            ms.WriteByte(0x0);

            //@ 0x01 - Mount Flag (not actually)
            ms.WriteByte(0x2);
            ms.WriteInt16(0x0);

            //@ 0x04
            ms.WriteInt32(0x82);
            ms.WriteInt32(0x40);

            //@ 0x0C - Mount Priority
            ms.WriteInt32(MountPriority);

            //@ 0x10
            ms.WriteInt32(0x03);

            //@ 0x14 - Appears to be a GUID. Common across all DLC though. Maybe some sort of magic GUID or something.
            var guidbytes = new byte[] { 0xAE, 0x0F, 0x43, 0xDD, 0x0B, 0x52, 0x5D, 0x4C, 0x9E, 0x28, 0x0D, 0x77, 0x6D, 0x86, 0x91, 0x55 };
            ms.WriteFromBuffer(guidbytes);
            ms.WriteInt32(0x0);

            //@ 0x28 - Mount Flag
            ms.WriteInt32(MountFlags.FlagValue);

            //@ 0x2C - Common Name
            //ms.WriteInt32(commonname.Length);
            ms.WriteUnrealStringLatin1(ME2Only_DLCHumanName);

            //@ 0x00 After CommonName - TLK ID
            ms.WriteInt32(TLKID);

            //@ 0x00 After TLKID - FolderName
            //ms.WriteInt32(dlcfolder.Length);
            ms.WriteUnrealStringLatin1(ME2Only_DLCFolderName);

            //@ Final 4 bytes
            ms.WriteInt32(0x0);
        }
    }
}