using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ME3Explorer.Unreal;
using static ME3Explorer.Unreal.UnrealFlags;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    public sealed class UDKPackage : UnrealPackageFile, IMEPackage
    {
        public MEGame Game => MEGame.UDK;

        static int headerSize = 0x8E;

        public bool Loaded = false;

        private byte[] header;

        public byte[] getHeader()
        {
            return header;
        }

        public List<ME1Explorer.Unreal.Classes.TalkFile> LocalTalkFiles { get; } = new List<ME1Explorer.Unreal.Classes.TalkFile>();
        public int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } } //this may be able to be optimized. It is used a lot during package load

        public EPackageFlags Flags => (EPackageFlags)BitConverter.ToUInt32(header, 16 + nameSize);
        public bool IsCompressed
        {
            get => (Flags & EPackageFlags.Compressed) != 0;
            protected set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                {
                    //Toolkit never should never set this flag as we do not support compressing files.
                    Buffer.BlockCopy(BitConverter.GetBytes((uint)(Flags | EPackageFlags.Compressed)), 0, header, 16 + nameSize, sizeof(int));
                }
                else // else set to false
                {
                    Buffer.BlockCopy(BitConverter.GetBytes((uint)(Flags & ~EPackageFlags.Compressed)), 0, header, 16 + nameSize, sizeof(int));
                }
            }
        }
        int idxOffsets { get { if (Flags.HasFlag(EPackageFlags.Cooked)) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34

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
            get => BitConverter.ToInt32(header, idxOffsets);
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        public int NameOffset
        {
            get => BitConverter.ToInt32(header, idxOffsets + 4);
            private set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        public override int ExportCount
        {
            get => BitConverter.ToInt32(header, idxOffsets + 8);
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        public int ExportOffset { get => BitConverter.ToInt32(header, idxOffsets + 12);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int));
        }
        public override int ImportCount { get => BitConverter.ToInt32(header, idxOffsets + 16);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int));
        }
        public int ImportOffset { get => BitConverter.ToInt32(header, idxOffsets + 20);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int));
        }
        public int DependencyTableOffset
        { get => BitConverter.ToInt32(header, idxOffsets + 24);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int));
        }
        int FreeZoneEnd { get => BitConverter.ToInt32(header, idxOffsets + 28);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int));
        }

        public Guid PackageGuid
        {
            get => new Guid(header.Slice(idxOffsets + 44, 16));
            set => Buffer.BlockCopy(value.ToByteArray(), 0, header, idxOffsets + 44, 16);
        }

        public bool CanReconstruct => false;

        /// <summary>
        ///     UDKPackage class constructor. It also load namelist, importlist and exportinfo (not exportdata) from udk file
        /// </summary>
        /// <param name="UDKPackagePath">full path + file name of desired udk file.</param>
        public UDKPackage(string UDKPackagePath)
        {
            string path = UDKPackagePath;
            FilePath = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(FilePath))
                throw new FileNotFoundException("UPK file not found");
            using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(FilePath);
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
            header = tempStream.ReadToBuffer(headerSize);
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
                throw new FileLoadException("Compressed UDK packages are not supported.");
            }
            else
            {
                listsStream = tempStream;
            }

            names = new List<string>();
            listsStream.Seek(NameOffset, SeekOrigin.Begin);

            for (int i = 0; i < NameCount; i++)
            {
                try
                {
                    int len = listsStream.ReadInt32();
                    string s = listsStream.ReadStringASCII(len - 1);
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

            exports = new List<ExportEntry>();
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ExportEntry exp = new ExportEntry(this, listsStream);
                exp.Index = i;
                exp.PropertyChanged += exportChanged;
                exports.Add(exp);
            }
        }

        /// <summary>
        ///     Not supported for UDK files
        /// </summary>
        public void save()
        {
            //Saving is not supported for UDK files.
        }

        /// <summary>
        ///     Not supported for UDK files
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
        {
            //Saving is not supported for UDK files.
        }
    }
}
