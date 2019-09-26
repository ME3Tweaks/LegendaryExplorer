using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
using StreamHelpers;
using UsefulThings;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer.Packages
{
    public enum PackageChange
    {
        ExportData,
        ExportHeader,
        Import,
        Names,
        ExportAdd,
        ImportAdd,
        ExportRemove,
        ImportRemove
    }

    [DebuggerDisplay("PackageUpdate | {change} on index {index}")]
    public struct PackageUpdate
    {
        /// <summary>
        /// Details on what piece of data has changed
        /// </summary>
        public PackageChange change;
        /// <summary>
        /// 0-based index of what item has changed in this package -1 = import 0, 0 = export 0
        /// </summary>
        public int index;
    }

    public sealed class MEPackage : UnrealPackageFile, IMEPackage, IDisposable
    {
        public MEGame Game { get; private set; } //can only be ME1, ME2, or ME3. UDK is a seperate class

        public bool CanReconstruct =>
            Game == MEGame.ME3 ||
            Game == MEGame.ME2 ||
            Game == MEGame.ME1 && !exports.Any(x => x.IsTexture() && x.idxLink == 0 && Texture2D.GetTexture2DMipInfos(x, null).Any(mip => mip.storageType == StorageTypes.pccLZO ||
                                                                                                                                          mip.storageType == StorageTypes.pccZlib));
        public int FullHeaderSize { get; private set; }
        public EPackageFlags Flags { get; private set; }

        public override int NameCount { get; protected set; }
        public int NameOffset { get; private set; }
        public override int ExportCount { get; protected set; }
        public int ExportOffset { get; private set; }
        public override int ImportCount { get; protected set; }
        public int ImportOffset { get; private set; }
        public int DependencyTableOffset { get; private set; }
        public Guid PackageGuid { get; set; }

        public byte[] getHeader()
        {
            var ms = new MemoryStream();
            WriteHeader(ms);
            return ms.ToArray();
        }

        public bool IsCompressed
        {
            get => Flags.HasFlag(EPackageFlags.Compressed);
        }

        public enum CompressionType
        {
            None = 0,
            Zlib,
            LZO
        }

        #region HeaderMisc
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private int ImportExportGuidsOffset;
        //private int ImportGuidsCount;
        //private int ExportGuidsCount;
        //private int ThumbnailTableOffset;
        private uint packageSource;
        private int unknown4;
        private int unknown6;
        #endregion

        static bool isInitialized;
        public static Func<string, MEGame, MEPackage> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            isInitialized = true;
            return (f, g) => new MEPackage(f, g);
        }

        private MEPackage(string filePath, MEGame forceGame = MEGame.Unknown) : base(Path.GetFullPath(filePath))
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"MEPackage {Path.GetFileName(filePath)}", new WeakReference(this));

            if (forceGame != MEGame.Unknown)
            {
                //new Package
                Game = forceGame;
                //reasonable defaults?
                Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
                return;
            }

            using (var fs = File.OpenRead(filePath))
            {
                #region Header

                uint magic = fs.ReadUInt32();
                if (magic != packageTag)
                {
                    throw new FormatException("Not an Unreal package!");
                }
                ushort unrealVersion = fs.ReadUInt16();
                ushort licenseeVersion = fs.ReadUInt16();
                switch (unrealVersion)
                {
                    case 491 when licenseeVersion == 1008:
                        Game = MEGame.ME1;
                        break;
                    case 512 when licenseeVersion == 130:
                        Game = MEGame.ME2;
                        break;
                    case 684 when licenseeVersion == 194:
                        Game = MEGame.ME3;
                        break;
                    default:
                        throw new FormatException("Not a Mass Effect Package!");
                }
                FullHeaderSize = fs.ReadInt32();
                int foldernameStrLen = fs.ReadInt32();
                //always "None", so don't bother saving result
                if (foldernameStrLen > 0)
                    fs.ReadStringASCIINull(foldernameStrLen);
                else
                    fs.ReadStringUnicodeNull(foldernameStrLen * -2);

                Flags = (EPackageFlags)fs.ReadUInt32();

                if (Game == MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
                {
                    fs.SkipInt32(); //always 0
                }

                NameCount = fs.ReadInt32();
                NameOffset = fs.ReadInt32();
                ExportCount = fs.ReadInt32();
                ExportOffset = fs.ReadInt32();
                ImportCount = fs.ReadInt32();
                ImportOffset = fs.ReadInt32();
                DependencyTableOffset = fs.ReadInt32();

                if (Game == MEGame.ME3)
                {
                    ImportExportGuidsOffset = fs.ReadInt32();
                    fs.SkipInt32(); //ImportGuidsCount always 0
                    fs.SkipInt32(); //ExportGuidsCount always 0
                    fs.SkipInt32(); //ThumbnailTableOffset always 0
                }

                PackageGuid = fs.ReadGuid();
                uint generationsTableCount = fs.ReadUInt32();
                if (generationsTableCount > 0)
                {
                    generationsTableCount--;
                    Gen0ExportCount = fs.ReadInt32();
                    Gen0NameCount = fs.ReadInt32();
                    Gen0NetworkedObjectCount = fs.ReadInt32();
                }
                //should never be more than 1 generation, but just in case
                fs.Skip(generationsTableCount * 12);

                fs.SkipInt32();//engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
                fs.SkipInt32();//cookedContentVersion   so we don't have to read them in

                if (Game == MEGame.ME2 || Game == MEGame.ME1)
                {
                    fs.SkipInt32(); //always 0
                    fs.SkipInt32(); //always 47699
                    unknown4 = fs.ReadInt32();
                    fs.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
                }

                unknown6 = fs.ReadInt32();
                fs.SkipInt32(); //always -1 in ME1 and ME2, always 145358848 in ME3

                if (Game == MEGame.ME1)
                {
                    fs.SkipInt32(); //always -1
                }

                //skip compression type chunks. Decompressor will handle that
                fs.SkipInt32();
                int numChunks = fs.ReadInt32();
                fs.Skip(numChunks * 16);

                packageSource = fs.ReadUInt32();

                if (Game == MEGame.ME2 || Game == MEGame.ME1)
                {
                    fs.SkipInt32(); //always 0
                }

                //Doesn't need to be written out, so it doesn't need to be read in
                //keep this here in case one day we learn that this has a purpose
                /*if (Game == MEGame.ME2 || Game == MEGame.ME3)
                {
                    int additionalPackagesToCookCount = fs.ReadInt32();
                    var additionalPackagesToCook = new string[additionalPackagesToCookCount];
                    for (int i = 0; i < additionalPackagesToCookCount; i++)
                    {
                        int strLen = fs.ReadInt32();
                        if (strLen > 0)
                        {
                            additionalPackagesToCook[i] = fs.ReadStringASCIINull(strLen);
                        }
                        else
                        {
                            additionalPackagesToCook[i] = fs.ReadStringUnicodeNull(strLen * -2);
                        }
                    }
                }*/
                #endregion

                Stream inStream = fs;
                if (IsCompressed && numChunks > 0)
                {
                    inStream = Game == MEGame.ME3 ? CompressionHelper.DecompressME3(fs) : CompressionHelper.DecompressME1orME2(fs);
                }

                //read namelist
                inStream.JumpTo(NameOffset);
                for (int i = 0; i < NameCount; i++)
                {
                    names.Add(inStream.ReadUnrealString());
                    if (Game == MEGame.ME1)
                        inStream.Skip(8);
                    else if (Game == MEGame.ME2)
                        inStream.Skip(4);
                }

                //read importTable
                inStream.JumpTo(ImportOffset);
                for (int i = 0; i < ImportCount; i++)
                {
                    ImportEntry imp = new ImportEntry(this, inStream) { Index = i };
                    imp.PropertyChanged += importChanged;
                    imports.Add(imp);
                }

                //read exportTable (ExportEntry constructor reads export data)
                inStream.JumpTo(ExportOffset);
                for (int i = 0; i < ExportCount; i++)
                {
                    ExportEntry e = new ExportEntry(this, inStream) { Index = i };
                    e.PropertyChanged += exportChanged;
                    exports.Add(e);
                }

                if (Game == MEGame.ME1)
                {
                    ReadLocalTLKs();
                }
            }
        }

        public void Save()
        {
            Save(FilePath);
        }

        public void Save(string path)
        {
            bool compressed = IsCompressed;
            Flags &= ~EPackageFlags.Compressed;
            bool isSaveAs = path != FilePath;
            try
            {
                if (CanReconstruct)
                {
                    saveByReconstructing(path, isSaveAs);
                }
                else
                {
                    throw new Exception($"Cannot save ME1 packages with compressed textures. Please make an issue on github: {App.BugReportURL}");
                }
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk
                if (isSaveAs && compressed)
                {
                    Flags |= EPackageFlags.Compressed;
                }
            }
        }

        private void saveByReconstructing(string path, bool isSaveAs)
        {
            try
            {
                var ms = new MemoryStream();
            
                //just for positioning. We write over this later when the header values have been updated
                WriteHeader(ms);

                //name table
                NameOffset = (int)ms.Position;
                NameCount = Gen0NameCount = names.Count;
                foreach (string name in names)
                {
                    switch (Game)
                    {
                        case MEGame.ME1:
                            ms.WriteUnrealStringASCII(name);
                            ms.WriteInt32(0);
                            ms.WriteInt32(458768);
                            break;
                        case MEGame.ME2:
                            ms.WriteUnrealStringASCII(name);
                            ms.WriteInt32(-14);
                            break;
                        case MEGame.ME3:
                            ms.WriteUnrealStringUnicode(name);
                            break;
                    }
                }

                //import table
                ImportOffset = (int)ms.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry e in imports)
                {
                    ms.WriteFromBuffer(e.Header);
                }

                //export table
                ExportOffset = (int)ms.Position;
                ExportCount = Gen0ExportCount = exports.Count;
                foreach (ExportEntry e in exports)
                {
                    e.HeaderOffset = (uint)ms.Position;
                    ms.WriteFromBuffer(e.Header);
                }

                DependencyTableOffset = (int) ms.Position;
                ms.WriteInt32(0);//zero-count DependencyTable
                FullHeaderSize = ImportExportGuidsOffset = (int) ms.Position;

                //export data
                foreach (ExportEntry e in exports)
                {
                    switch (Game)
                    {
                        case MEGame.ME1:
                            UpdateME1Offsets(e, (int)ms.Position);
                            break;
                        case MEGame.ME2:
                            UpdateME2Offsets(e, (int)ms.Position);
                            break;
                        case MEGame.ME3:
                            UpdateME3Offsets(e, (int)ms.Position);
                            break;
                    }

                    e.DataOffset = (int)ms.Position;


                    ms.WriteFromBuffer(e.Data);
                    //update size and offset in already-written header
                    long pos = ms.Position;
                    ms.JumpTo(e.HeaderOffset + 32);
                    ms.WriteInt32(e.DataSize); //DataSize might have been changed by UpdateOffsets
                    ms.WriteInt32(e.DataOffset);
                    ms.JumpTo(pos);
                }

                //re-write header with updated values
                ms.JumpTo(0);
                WriteHeader(ms);


                File.WriteAllBytes(path, ms.ToArray());
                if (!isSaveAs)
                {
                    AfterSave();
                }
            }
            catch (Exception ex) when(!App.IsDebug)
            {
                MessageBox.Show($"Error saving {FilePath}:\n{ExceptionHandlerDialogWPF.FlattenException(ex)}");
            }
        }

        private void WriteHeader(Stream ms)
        {
            ms.WriteUInt32(packageTag);
            //version
            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteUInt16(491);
                    ms.WriteUInt16(1008);
                    break;
                case MEGame.ME2:
                    ms.WriteUInt16(512);
                    ms.WriteUInt16(130);
                    break;
                case MEGame.ME3:
                    ms.WriteUInt16(684);
                    ms.WriteUInt16(194);
                    break;
            }
            ms.WriteInt32(FullHeaderSize);
            if (Game == MEGame.ME3)
            {
                ms.WriteUnrealStringUnicode("None");
            }
            else
            {
                ms.WriteUnrealStringASCII("None");
            }

            ms.WriteUInt32((uint)Flags);

            if (Game == MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
            {
                ms.WriteInt32(0);
            }

            ms.WriteInt32(NameCount);
            ms.WriteInt32(NameOffset);
            ms.WriteInt32(ExportCount);
            ms.WriteInt32(ExportOffset);
            ms.WriteInt32(ImportCount);
            ms.WriteInt32(ImportOffset);
            ms.WriteInt32(DependencyTableOffset);

            if (Game == MEGame.ME3)
            {
                ms.WriteInt32(ImportExportGuidsOffset);
                ms.WriteInt32(0); //ImportGuidsCount
                ms.WriteInt32(0); //ExportGuidsCount
                ms.WriteInt32(0); //ThumbnailTableOffset
            }
            ms.WriteGuid(PackageGuid);

            //Write 1 generation
            ms.WriteInt32(1);
            ms.WriteInt32(Gen0ExportCount);
            ms.WriteInt32(Gen0NameCount);
            ms.WriteInt32(Gen0NetworkedObjectCount);

            //engineVersion and cookedContentVersion
            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteInt32(3240);
                    ms.WriteInt32(47);
                    break;
                case MEGame.ME2:
                    ms.WriteInt32(3607);
                    ms.WriteInt32(64);
                    break;
                case MEGame.ME3:
                    ms.WriteInt32(6383);
                    ms.WriteInt32(196715);
                    break;
            }


            if (Game == MEGame.ME2 || Game == MEGame.ME1)
            {
                ms.WriteInt32(0);
                ms.WriteInt32(47699); //No idea what this is, but it's always 47699
                switch (Game)
                {
                    case MEGame.ME1:
                        ms.WriteInt32(0);
                        ms.WriteInt32(1);
                        break;
                    case MEGame.ME2:
                        ms.WriteInt32(unknown4);
                        ms.WriteInt32(1966080);
                        break;
                }
            }

            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteInt32(0);
                    ms.WriteInt32(-1);
                    break;
                case MEGame.ME2:
                    ms.WriteInt32(-1);
                    ms.WriteInt32(-1);
                    break;
                case MEGame.ME3:
                    ms.WriteInt32(unknown6);
                    ms.WriteInt32(145358848);
                    break;
            }

            if (Game == MEGame.ME1)
            {
                ms.WriteInt32(-1);
            }

            ms.WriteUInt32((uint)CompressionType.None);
            ms.WriteInt32(0);//numChunks

            ms.WriteUInt32(packageSource);

            if (Game == MEGame.ME2 || Game == MEGame.ME1)
            {
                ms.WriteInt32(0);
            }

            if (Game == MEGame.ME3 || Game == MEGame.ME2)
            {
                ms.WriteInt32(0);//empty additionalPackagesToCook array
            }
        }

        public List<ME1Explorer.Unreal.Classes.TalkFile> LocalTalkFiles { get; } = new List<ME1Explorer.Unreal.Classes.TalkFile>();
        private void ReadLocalTLKs()
        {
            LocalTalkFiles.Clear();
            List<ExportEntry> tlkFileSets = Exports.Where(x => x.ClassName == "BioTlkFileSet" && !x.IsDefaultObject).ToList();
            var exportsToLoad = new List<ExportEntry>();
            foreach (var tlkFileSet in tlkFileSets)
            {
                MemoryStream r = new MemoryStream(tlkFileSet.Data);
                r.Position = tlkFileSet.propsEnd();
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int langRef = r.ReadInt32();
                    r.ReadInt32(); //second half of name
                    string lang = GetNameEntry(langRef);
                    int numTlksForLang = r.ReadInt32(); //I believe this is always 2. Hopefully I am not wrong.
                    int maleTlk = r.ReadInt32();
                    int femaleTlk = r.ReadInt32();

                    if (Properties.Settings.Default.TLKLanguage.Equals(lang, StringComparison.InvariantCultureIgnoreCase))
                    {
                        exportsToLoad.Add(GetUExport(Properties.Settings.Default.TLKGender_IsMale ? maleTlk : femaleTlk));
                        break;
                    }

                    //r.ReadInt64();
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), true, langRef, index));
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), false, langRef, index));
                }
            }

            foreach (var exp in exportsToLoad)
            {
                //Debug.WriteLine("Loading local TLK: " + exp.GetIndexedFullPath);
                LocalTalkFiles.Add(new ME1Explorer.Unreal.Classes.TalkFile(exp));
            }
        }

        private static void UpdateME1Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            if (export.IsTexture())
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                binData.Skip(12);
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    var storageFlags = (StorageFlags)binData.ReadInt32();
                    if (!storageFlags.HasFlag(StorageFlags.externalFile)) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadInt32();
                        int compressedSize = binData.ReadInt32();
                        binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek((storageFlags == StorageFlags.noFlags ? uncompressedSize : compressedSize) + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }
                export.setBinaryData(binData.ToArray());
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            int bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 4 + 16);
                            break;
                    }
                }
            }
        }

        private static void UpdateME2Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if (export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null)
            {
                byte[] binData = export.getBinaryData();
                if (binData.Length < 44)
                {
                    return; //¯\_(ツ)_ /¯
                }
                binData.OverwriteRange(44, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 48));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            else if (export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(20, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 24));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            //else if (export.IsTexture())
            //{
            //    int baseOffset = newDataOffset + export.propsEnd();
            //    MemoryStream binData = new MemoryStream(export.getBinaryData());
            //    binData.Skip(12);
            //    binData.WriteInt32(baseOffset + (int)binData.Position + 4);
            //    for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
            //    {
            //        var storageFlags = (StorageFlags)binData.ReadInt32();
            //        if (!storageFlags.HasFlag(StorageFlags.externalFile)) //pcc-stored
            //        {
            //            int uncompressedSize = binData.ReadInt32();
            //            int compressedSize = binData.ReadInt32();
            //            binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
            //            binData.Seek((storageFlags == StorageFlags.noFlags ? uncompressedSize : compressedSize) + 8, SeekOrigin.Current); //skip texture and width + height values
            //        }
            //        else
            //        {
            //            binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
            //        }
            //    }
            //    export.setBinaryData(binData.ToArray());
            //}
            else if (export.ClassName == "ShaderCache")
            {
                int oldDataOffset = export.DataOffset;

                MemoryStream binData = new MemoryStream(export.Data);
                binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

                int nameList1Count = binData.ReadInt32();
                binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                int shaderCount = binData.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    binData.Seek(24, SeekOrigin.Current);
                    int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextShaderOffset + newDataOffset);
                    binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                }

                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);

                int materialShaderMapCount = binData.ReadInt32();
                for (int i = 0; i < materialShaderMapCount; i++)
                {
                    binData.Seek(16, SeekOrigin.Current);

                    int switchParamCount = binData.ReadInt32();
                    binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                    int componentMaskParamCount = binData.ReadInt32();
                    binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            int bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 4 + 16);
                            break;
                    }
                }
            }
        }

        private static void UpdateME3Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if ((export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null) || export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored movies in texturemovies
            else if (export.ClassName == "TextureMovie" && export.GetProperty<NameProperty>("TextureFileCacheName") == null)
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            //Keeping around just in case I somehow forgot something. -Mgamerz
            //else if (export.IsTexture())
            //{
            //    int baseOffset = newDataOffset + export.propsEnd();
            //    MemoryStream binData = new MemoryStream(export.getBinaryData());
            //    for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
            //    {
            //        if (binData.ReadInt32() == (int)StorageTypes.pccUnc) //pcc-stored
            //        {
            //            int uncompressedSize = binData.ReadInt32();
            //            binData.Seek(4, SeekOrigin.Current); //skip compressed size
            //            binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
            //            binData.Seek(uncompressedSize + 8, SeekOrigin.Current); //skip texture and width + height values
            //        }
            //        else
            //        {
            //            binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
            //        }
            //    }
            //    export.setBinaryData(binData.ToArray());
            //}
            else if (export.ClassName == "ShaderCache")
            {
                int oldDataOffset = export.DataOffset;

                MemoryStream binData = new MemoryStream(export.Data);
                binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

                int nameList1Count = binData.ReadInt32();
                binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                int namelist2Count = binData.ReadInt32();//namelist2
                binData.Seek(namelist2Count * 12, SeekOrigin.Current);

                int shaderCount = binData.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    binData.Seek(24, SeekOrigin.Current);
                    int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextShaderOffset + newDataOffset);
                    binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                }

                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);

                int materialShaderMapCount = binData.ReadInt32();
                for (int i = 0; i < materialShaderMapCount; i++)
                {
                    binData.Seek(16, SeekOrigin.Current);

                    int switchParamCount = binData.ReadInt32();
                    binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                    int componentMaskParamCount = binData.ReadInt32();
                    binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                    int normalParams = binData.ReadInt32();
                    binData.Seek(normalParams * 29, SeekOrigin.Current);

                    binData.Seek(8, SeekOrigin.Current);

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    int bulkDataSize;
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 3 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 3 + 16);
                            break;
                        case 3:
                            bin.Skip(8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(24);
                            break;
                        case 4:
                        case 6:
                            bin.Skip(124);
                            break;
                        case 5:
                            bin.Skip(4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12);
                            break;
                    }
                }
            }
        }


        public void ConvertTo(MEGame newGame)
        {
            MEGame oldGame = Game;
            var prePropBinary = new List<byte[]>(ExportCount);
            var propCollections = new List<PropertyCollection>(ExportCount);
            var postPropBinary = new List<ObjectBinary>(ExportCount);

            if (oldGame == MEGame.ME1 && newGame != MEGame.ME1)
            {
                int idx = names.IndexOf("BIOC_Base");
                if (idx >= 0)
                {
                    names[idx] = "SFXGame";
                }
            }
            else if (newGame == MEGame.ME1)
            {
                int idx = names.IndexOf("SFXGame");
                if (idx >= 0)
                {
                    names[idx] = "BIOC_Base";
                }
            }

            //fix up Default_ imports
            if (newGame == MEGame.ME3)
            {
                using (IMEPackage core = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc")))
                using (IMEPackage engine = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc")))
                using (IMEPackage sfxGame = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc")))
                {
                    foreach (ImportEntry defImp in imports.Where(imp => imp.ObjectName.Name.StartsWith("Default_")).ToList())
                    {
                        string packageName = defImp.FullPath.Split('.')[0];
                        IMEPackage pck = packageName == "Core" ? core : packageName == "Engine" ? engine : packageName == "SFXGame" ? sfxGame : null;
                        if (pck != null && pck.Exports.FirstOrDefault(exp => exp.ObjectName == defImp.ObjectName) is ExportEntry defExp)
                        {
                            var impChildren = defImp.GetChildren();
                            var expChildren = defExp.GetChildren();
                            foreach (IEntry expChild in expChildren)
                            {
                                if (impChildren.FirstOrDefault(imp => imp.ObjectName == expChild.ObjectName) is ImportEntry matchingImp)
                                {
                                    impChildren.Remove(matchingImp);
                                }
                                else
                                {
                                    AddImport(new ImportEntry(this)
                                    {
                                        idxLink = defImp.UIndex,
                                        ClassName = expChild.ClassName,
                                        ObjectName = expChild.ObjectName,
                                        PackageFile = defImp.PackageFile
                                    });
                                }
                            }

                            foreach (IEntry impChild in impChildren)
                            {
                                EntryPruner.TrashEntries(this, impChild.GetAllDescendants().Prepend(impChild));
                            }
                        }
                    }
                }
            }

            //purge MaterialExpressions
            if (newGame == MEGame.ME3)
            {
                var entriesToTrash = new List<IEntry>();
                foreach (ExportEntry mat in exports.Where(exp => exp.ClassName == "Material").ToList())
                {
                    entriesToTrash.AddRange(mat.GetAllDescendants());
                }
                EntryPruner.TrashEntries(this, entriesToTrash.ToHashSet());
            }

            EntryPruner.TrashIncompatibleEntries(this, oldGame, newGame);

            foreach (ExportEntry export in exports)
            {
                //convert stack, or just get the pre-prop binary if no stack
                prePropBinary.Add(ExportBinaryConverter.ConvertPrePropBinary(export, newGame));

                if (export.ClassName == "Class")
                {
                    propCollections.Add(null);
                }
                else
                {
                    //read in all properties in the old format, and remove ones that are incompatible with newGame
                    propCollections.Add(EntryPruner.RemoveIncompatibleProperties(this, export.GetProperties(), export.ClassName, newGame));
                }

                //convert binary data
                postPropBinary.Add(ExportBinaryConverter.ConvertPostPropBinary(export, newGame));

                //writes header in whatever format is correct for newGame
                export.RegenerateHeader(newGame);
            }

            Game = newGame;

            for (int i = 0; i < exports.Count; i++)
            {
                var newData = new MemoryStream();
                newData.WriteFromBuffer(prePropBinary[i]);
                //write back properties in new format
                propCollections[i]?.WriteTo(newData, this);

                postPropBinary[i].WriteTo(newData, this, exports[i].DataOffset + exports[i].propsEnd()); //should do this again during Save to get offsets correct
                                                                                                                      //might not matter though

                exports[i].Data = newData.ToArray();
            }

            if (newGame == MEGame.ME3)
            {
                //change all materials to default material, but try to preserve diff and norm textures
                using (var resourcePCC = MEPackageHandler.OpenME3Package(Path.Combine(App.ExecFolder, "ME3Resources.pcc")))
                {
                    var normDiffMat = resourcePCC.Exports.First(exp => exp.ObjectName == "NormDiffMaterial");

                    foreach (ExportEntry mat in exports.Where(exp => exp.ClassName == "Material" || exp.ClassName  == "MaterialInstanceConstant"))
                    {
                        UIndex[] textures = Array.Empty<UIndex>();
                        if (mat.ClassName == "Material")
                        {
                            textures = ObjectBinary.From<Material>(mat).SM3MaterialResource.UniformExpressionTextures;
                        }
                        else if(mat.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                        {
                            textures = ObjectBinary.From<MaterialInstance>(mat).SM3StaticPermutationResource.UniformExpressionTextures;
                        }
                        else if (mat.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> texParams)
                        {
                            textures = texParams.Select(structProp => new UIndex(structProp.GetProp<ObjectProperty>("ParameterValue")?.Value ?? 0)).ToArray();
                        }
                        else if(mat.GetProperty<ObjectProperty>("Parent") is ObjectProperty parentProp && GetEntry(parentProp.Value) is ExportEntry parent && parent.ClassName == "Material")
                        {
                            textures = ObjectBinary.From<Material>(parent).SM3MaterialResource.UniformExpressionTextures;
                        }

                        EntryImporter.ReplaceExportDataWithAnother(normDiffMat, mat);
                        int norm = 0;
                        int diff = 0;
                        foreach (UIndex texture in textures)
                        {
                            if (GetEntry(texture) is IEntry tex)
                            {
                                if (diff == 0 && tex.ObjectName.Name.Contains("diff", StringComparison.OrdinalIgnoreCase))
                                {
                                    diff = texture;
                                }
                                else if (norm == 0 && tex.ObjectName.Name.Contains("norm", StringComparison.OrdinalIgnoreCase))
                                {
                                    norm = texture;
                                }
                            }
                        }
                        if (diff == 0)
                        {
                            diff = EntryImporter.GetOrAddCrossImportOrPackage("EngineMaterials.DefaultDiffuse", resourcePCC, this).UIndex;
                        }

                        var matBin = ObjectBinary.From<Material>(mat);
                        matBin.SM3MaterialResource.UniformExpressionTextures = new UIndex[] { norm, diff };
                        mat.setBinaryData(matBin.ToBytes(this));
                        mat.Class = imports.First(imp => imp.ObjectName == "Material");
                    }
                }
            }

            if (newGame != MEGame.ME3)
            {
                foreach (ExportEntry texport in exports.Where(exp => exp.IsTexture()))
                {
                    texport.WriteProperty(new BoolProperty(true, "NeverStream"));
                }
            }
            else if (exports.Any(exp => exp.IsTexture() && Texture2D.GetTexture2DMipInfos(exp, null)
                                                                                .Any(mip => mip.storageType == StorageTypes.pccLZO
                                                                                         || mip.storageType == StorageTypes.pccZlib)))
            {
                //ME3 can't deal with compressed textures in a pcc, so we'll need to stuff them into a tfc
                string tfcName = Path.GetFileNameWithoutExtension(FilePath);
                using (var tfc = File.OpenWrite(Path.ChangeExtension(FilePath, "tfc")))
                {
                    Guid tfcGuid = Guid.NewGuid();
                    tfc.WriteGuid(tfcGuid);

                    foreach (ExportEntry texport in exports.Where(exp => exp.IsTexture()))
                    {
                        var mips = Texture2D.GetTexture2DMipInfos(texport, null);
                        var offsets = new List<int>();
                        foreach (Texture2DMipInfo mipInfo in mips)
                        {
                            if (mipInfo.storageType == StorageTypes.pccLZO || mipInfo.storageType == StorageTypes.pccZlib)
                            {
                                offsets.Add((int)tfc.Position);
                                byte[] mip;
                                if (mipInfo.storageType == StorageTypes.pccLZO)
                                {
                                    mip = TextureCompression.CompressTexture(Texture2D.GetTextureData(mipInfo), StorageTypes.extZlib);
                                }
                                else
                                {
                                    mip = Texture2D.GetTextureData(mipInfo, false);
                                }
                                tfc.WriteFromBuffer(mip);
                            }
                        }
                        offsets.Add((int)tfc.Position);
                        texport.setBinaryData(ExportBinaryConverter.ConvertTexture2D(texport, Game, offsets, StorageTypes.extZlib));
                        texport.WriteProperty(new NameProperty(tfcName, "TextureFileCacheName"));
                        texport.WriteProperty(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                    }
                }
            }
        }
    }
}