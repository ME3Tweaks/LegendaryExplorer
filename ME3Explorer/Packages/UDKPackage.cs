using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using ME3Explorer.Unreal;
using System.Windows;
using KFreonLib.Debugging;

namespace ME3Explorer.Packages
{
    public sealed class UDKPackage : MEPackage, IMEPackage
    {
        public MEGame Game { get { return MEGame.UDK; } }

        static int headerSize = 0x8E;
        byte[] extraNamesList = null;

        //public bool isModified { get { return Exports.Any(entry => entry.hasChanged == true); } }
        public bool bDLCStored = false;
        public bool bExtraNamesList { get { return extraNamesList != null; } }
        public bool Loaded = false;

        int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34

        static bool isInitialized;
        internal static Func<string, UDKPackage> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(UDKPackage) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return f => new UDKPackage(f);
            }
        }

        public override int NameCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets); }
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        int NameOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 4); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        public override int ExportCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 8); }
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        public override int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } protected set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }
        uint FreeZoneStart { get { return BitConverter.ToUInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(uint)); } }
        uint FreeZoneEnd { get { return BitConverter.ToUInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(uint)); } }

        int expInfoEndOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }
        new int expDataBegOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 28); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int));
            }
        }

        public struct NameEntry
        {
            public string name;
            public int unk;
            public int flags;
        }

        public interface IEntry
        {
            string ClassName { get; }
            string GetFullPath { get; }
            int idxLink { get; }
            int idxObjectName { get; }
            string ObjectName { get; }
            string PackageFullName { get; }
            string PackageName { get; }
        }

        /*public class ImportEntry : IEntry
        {
            public static int byteSize = 28;
            internal byte[] header = new byte[byteSize];
            internal UDKPackage udkRef;

            public int idxPackageFile { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
            public int idxClassName { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
            public int idxLink { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
            public int idxObjectName { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }

            public string ClassName { get { return udkRef.getName(idxClassName); } }
            public string PackageFile { get { return udkRef.Names[idxPackageFile] + ".udk"; } }
            public string ObjectName { get { return udkRef.getName(idxObjectName); } }
            public string PackageName
            {
                get
                {
                    int val = idxLink;
                    if (val != 0)
                    {
                        IEntry entry = udkRef.getEntry(val);
                        return udkRef.getName(entry.idxObjectName);
                    }
                    else return "Package";
                }
            }
            public string PackageFullName
            {
                get
                {
                    string result = PackageName;
                    int idxNewPackName = idxLink;

                    while (idxNewPackName != 0)
                    {
                        string newPackageName = udkRef.getEntry(idxNewPackName).PackageName;
                        if (newPackageName != "Package")
                            result = newPackageName + "." + result;
                        idxNewPackName = udkRef.getEntry(idxNewPackName).idxLink;
                    }
                    return result;
                }
            }

            public string GetFullPath
            {
                get
                {
                    string s = "";
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += ObjectName;
                    return s;
                }
            }

            public ImportEntry(UDKPackage UDKPackage, byte[] importData)
            {
                udkRef = UDKPackage;
                header = (byte[])importData.Clone();
            }

            public ImportEntry(UDKPackage UDKPackage, Stream importData)
            {
                udkRef = UDKPackage;
                header = new byte[ImportEntry.byteSize];
                importData.Read(header, 0, header.Length);
            }

            public ImportEntry Clone()
            {
                ImportEntry newImport = (ImportEntry)MemberwiseClone();
                newImport.header = (byte[])this.header.Clone();
                return newImport;
            }
        } */

        /*
    public class ExportEntry : IEntry // class containing info about export entry (header info + data)
    {
        internal byte[] header; // holds data about export header, not the export data.
        public UDKPackage udkRef;
        public uint offset { get; set; }

        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxPackageName { get { return BitConverter.ToInt32(header, 8) - 1; } set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, header, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 24, sizeof(long)); } }

        public string ObjectName { get { return udkRef.getName(idxObjectName); } }
        public string ClassName { get { int val = idxClass; if (val != 0) return udkRef.getName(udkRef.getEntry(val).idxObjectName); else return "Class"; } }
        public string ClassParent { get { int val = idxClassParent; if (val != 0) return udkRef.getName(udkRef.getEntry(val).idxObjectName); else return "Class"; } }
        public string PackageName { get { int val = idxPackageName; if (val >= 0) return udkRef.getName(udkRef.Exports[val].idxObjectName); else return "Package"; } }
        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxPackageName;

                while (idxNewPackName >= 0)
                {
                    string newPackageName = udkRef.Exports[idxNewPackName].PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = udkRef.Exports[idxNewPackName].idxPackageName;
                }
                return result;
            }
        }

        public string ContainingPackage
        {
            get
            {
                string result = PackageName;
                if (result.EndsWith(ObjectName))
                {
                    result = "";
                }
                int idxNewPackName = idxPackageName;

                while (idxNewPackName >= 0)
                {
                    string newPackageName = udkRef.Exports[idxNewPackName].PackageName;
                    if (newPackageName != "Package")
                    {
                        if (!result.Equals(""))
                        {
                            result = newPackageName + "." + result;
                        }
                        else
                        {
                            result = newPackageName;
                        }
                    }
                    idxNewPackName = udkRef.Exports[idxNewPackName].idxPackageName;
                }
                return result;
            }
        }

        public string GetFullPath
        {
            get
            {
                string s = "";
                if (PackageFullName != "Class" && PackageFullName != "Package")
                    s += PackageFullName + ".";
                s += ObjectName;
                return s;
            }
        }
        public string ArchtypeName { get { int val = idxArchtype; if (val != 0) return udkRef.(udkRef.getEntry(val).idxObjectName); else return "None"; } }

        public int DataSize { get { return BitConverter.ToInt32(header, 32); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(header, 36); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 36, sizeof(int)); } }
        public int DataOffsetTmp;
        byte[] _data = null;
        public byte[] Data // holds data about export data
        {
            get
            {
                // if data isn't loaded then fill it from udk file (load-on-demand)
                if (_data == null)
                {
                    udkRef.getData(DataOffset, this);
                }
                return _data;
            }

            set { _data = value; hasChanged = true; }
        }
        public bool likelyCoalescedVal
        {
            get
            {
                return (Data.Length < 25) ? false : (Data[25] == 64); //0x40
            }
            set { }
        }
        public bool hasChanged { get; internal set; }

        public ExportEntry(UDKPackage UDKPackage, byte[] importData, uint exportOffset)
        {
            udkRef = UDKPackage;
            header = (byte[])importData.Clone();
            offset = exportOffset;
            hasChanged = false;
        }

        public ExportEntry()
        {
            // TODO: Complete member initialization
        }

        public ExportEntry Clone()
        {
            ExportEntry newExport = (ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                         // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (ExportEntry ent in udkRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }*/

        /// <summary>
        ///     UDKPackage class constructor. It also load namelist, importlist and exportinfo (not exportdata) from udk file
        /// </summary>
        /// <param name="UDKPackagePath">full path + file name of desired udk file.</param>
        public UDKPackage(string UDKPackagePath, bool fullFileInMemory = false)
        {
            string path = UDKPackagePath;
            DebugOutput.PrintLn("Load file : " + path);
            FileName = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(FileName))
                throw new FileNotFoundException("UPK file not found");
            using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(FileName);
                tempStream.WriteFromStream(fs, tempInfo.Length);
                if (tempStream.Length != tempInfo.Length)
                {
                    throw new FileLoadException("File not fully read in. Try again later");
                }
            }

            //tempStream.Seek(12, SeekOrigin.Begin);
            //int tempNameSize = tempStream.ReadValueS32();
            //tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            //int tempGenerations = tempStream.ReadValueS32();
            //tempStream.Seek(36 + tempGenerations * 12, SeekOrigin.Current);
            //int tempPos = (int)tempStream.Position;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(headerSize);
            tempStream.Seek(0, SeekOrigin.Begin);

            MemoryStream listsStream;
            if (IsCompressed)
            {
                /*DebugOutput.PrintLn("File is compressed");
                {
                    listsStream = CompressionHelper.DecompressME1orME2(tempStream);

                    //Correct the header
                    IsCompressed = false;
                    listsStream.Seek(0, SeekOrigin.Begin);
                    listsStream.WriteBytes(header);

                    //Set numblocks to zero
                    listsStream.WriteValueS32(0);
                    //Write the magic number
                    listsStream.WriteValueS32(1026281201);
                    //Write 8 bytes of 0
                    listsStream.WriteValueS32(0);
                    listsStream.WriteValueS32(0);
                }*/
                throw new FileLoadException("Compressed UPK packages are not supported.");
            }
            else
            {
                DebugOutput.PrintLn("File already decompressed. Reading decompressed data.");
                listsStream = tempStream;
            }

            names = new List<string>();
            listsStream.Seek(NameOffset, SeekOrigin.Begin);

            for (int i = 0; i < NameCount; i++)
            {
                try
                {
                    Debug.WriteLine("Reading string at 0x" + listsStream.Position.ToString("X8"));
                    int len = listsStream.ReadValueS32();
                    string s = listsStream.ReadString((uint)(len - 1));
                    //skipping irrelevant data

                    listsStream.Seek(9, SeekOrigin.Current); // 8 + 1 for terminator character
                    names.Add(s);

                }
                catch (Exception e)
                {
                    Debugger.Break();
                    throw e;
                }
            }

            imports = new List<ImportEntry>();
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry import = new ImportEntry(this, listsStream);
                import.Index = i;
                import.PropertyChanged += importChanged;
                imports.Add(import);
            }

            exports = new List<IExportEntry>();
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                UDKExportEntry exp = new UDKExportEntry(this, listsStream);
                exp.Index = i;
                exp.PropertyChanged += exportChanged;
                exports.Add(exp);
            }

            /*


            Loaded = true;
            FileName = Path.GetFullPath(UDKPackagePath);
            using (FileStream udkStream = File.OpenRead(FileName))
            {
            `                Names = new List<NameEntry>();
                Imports = new List<ImportEntry>();
                Exports = new List<ExportEntry>();

                udkStream.Read(header, 0, header.Length);

                //unsure about magic number. for now just let it try anything
                if (magic != 2653586369)
                {
                    //throw new FormatException("not a udk file");
                }

                //again, unsure of what versions ought to be supported
                if (lowVers != 684 && highVers != 0)
                {
                    //throw new FormatException("unsupported version");
                }

                Stream listsStream;
                listsStream = udkStream;
                headerEnd = NameOffset;

                // fill names list
                listsStream.Seek(NameOffset, SeekOrigin.Begin);
                for (int i = 0; i < NameCount; i++)
                {
                    long currOffset = listsStream.Position;
                    int strLength = listsStream.ReadValueS32();
                    NameEntry n = new NameEntry();
                    if (strLength < 0)
                    {
                        n.name = listsStream.ReadString(strLength * -2, true, Encoding.Unicode);
                    }
                    else
                    {
                        n.name = listsStream.ReadString(strLength, true, Encoding.ASCII);
                    }
                    n.unk = listsStream.ReadValueS32();
                    n.flags = listsStream.ReadValueS32();
                    Names.Add(n);
                }
                //Debug.WriteLine("Names done. Current offset: "+listsStream.Position);
                //Debug.WriteLine("Import Offset: " + ImportOffset);

                // fill import list
                //Console.Out.WriteLine("IMPORT OFFSET: " + ImportOffset);
                listsStream.Seek(ImportOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[ImportEntry.byteSize];
                for (int i = 0; i < ImportCount; i++)
                {

                    long offset = listsStream.Position;
                    ImportEntry e = new ImportEntry(this, listsStream);
                    Imports.Add(e);
                    //Debug.WriteLine("Read import " + i + " " + e.ObjectName + ", offset: " + offset);
                };

                // fill export list (only the headers, not the data)
                listsStream.Seek(ExportOffset, SeekOrigin.Begin);
                //Console.Out.WriteLine("Export OFFSET: " + ImportOffset);
                for (int i = 0; i < ExportCount; i++)
                {
                    uint expInfoOffset = (uint)listsStream.Position;

                    listsStream.Seek(44, SeekOrigin.Current);
                    int count = listsStream.ReadValueS32();
                    listsStream.Seek(-48, SeekOrigin.Current);

                    int expInfoSize = 68 + (count * 4);
                    buffer = new byte[expInfoSize];

                    listsStream.Read(buffer, 0, buffer.Length);
                    ExportEntry e = new ExportEntry(this, buffer, expInfoOffset);
                    //Debug.WriteLine("Read export " + i + " " + e.ObjectName + ", offset: " + expInfoOffset+ ", size: "+expInfoSize); 
                    Exports.Add(e);
                }
            }*/
            Debug.WriteLine(getMetadataString());
        }

        public UDKPackage()
        {
        }

        /// <summary>
        ///     save PCC to same file by reconstruction if possible, append if not
        /// </summary>
        public void save()
        {
            save(FileName);
        }

        /// <summary>
        ///     save PCC by reconstruction if possible, append if not
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
        {
            //Saving is not supported for UPK files.
            return; 
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void saveByReconstructing(string path)
        {
            //Saving is not supported for UPK files.
            return;
            
        }

       

        /// <summary>
        /// This method is an alternate way of saving PCCs
        /// Instead of reconstructing the PCC from the data taken, it instead copies across the existing
        /// data, appends new exports, updates the export list, changes the namelist location and updates the
        /// value in the header
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        /// <param name="attemptOverwrite">Do you wish to attempt to overwrite the existing export</param>
        public string appendSave(string newFileName, bool attemptOverwrite, int HeadeNameOffset = 34)
        {
            //Saving UDK files is not supported.
            return "";
        }

        public string getMetadataString()
        {
            string str = "UDK File Metadata";
            str += "\nNames Offset: " + this.NameOffset;
            str += "\nImports Offset: " + this.ImportOffset;
            str += "\nExport Offset: " + this.ExportOffset;
            return str;

        }
        /*
        public bool canClone()
        {
            if (!canReconstruct)
            {
                var res = MessageBox.Show("This file contains a SeekFreeShaderCache. Cloning will cause a crash when ME3 attempts to load this file.\n" +
                    "Do you want to visit a forum thread with more information and a possible solution?",
                    "I'm sorry, Dave. I'm afraid I can't do that.", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                if (res == DialogResult.Yes)
                {
                    Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-udk-into-a-vanilla-one-t2264.html");
                }
                return false;
            }
            return true;
        }*/
    }
}
