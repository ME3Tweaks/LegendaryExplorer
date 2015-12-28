using KFreonLib.Debugging;
using KFreonLib.GUI;
using KFreonLib.PCCObjects;
using KFreonLib.Scripting;
using KFreonLib.Textures;
using SaltTPF;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using UsefulThings;
using CSharpImageLibrary.General;

namespace KFreonLib.Textures
{
    /// <summary>
    /// TexInfo baseclass variables/properties/methods. Cannot be initialised.
    /// </summary>
    public abstract partial class abstractTexInfo
    {
        public string TexName = null;
        public List<string> Files = null;
        public List<string> OriginalFiles = null;
        public List<int> ExpIDs = null;
        public List<int> OriginalExpIDs = null;
        public uint Hash = 0;
        public uint OriginalHash = 0;
        public int NumMips = 0;
        public string Format = null;
        public int TreeInd = -1;
        public int GameVersion = -1;
    }

    /// <summary>
    /// Formerly texStruct. Provides an object to store tree texture information.
    /// </summary>
    public class TreeTexInfo : abstractTexInfo
    {
        // KFreon: Class Specific Variables
        public bool TriedThumbUpdate = false;
        public string FullPackage = null;
        public myTreeNode ParentNode = null;
        public int tfcOffset = -1;
        public List<ITexture2D> Textures = null;
        public string ThumbnailPath = null;
        public string ThumbName { get { return TexName + "_" + Hash + ".jpg"; } }
        public int ListViewIndex = -1;

        public string Package
        {
            get
            {
                if (String.IsNullOrEmpty(FullPackage))
                    return "";
                string temppack = FullPackage.Remove(FullPackage.Length - 1);
                if (temppack.Split('.').Length > 1)
                    return temppack.Split('.')[temppack.Split('.').Length - 1];
                else
                    return temppack.Split('.')[0];
            }
        }


        public void Update(List<string> GivenFiles, List<int> GivenExpIDs, uint GivenHash, int GivenMips, List<ITexture2D> GivenTextures, string pathBIOGame)
        {
            if (Files == null)
                Files = new List<string>();

            /*if (GameVersion != 1)
                KFreonLib.PCCObjects.Misc.ReorderFiles(ref GivenFiles, ref GivenExpIDs, pathBIOGame, GameVersion);

            Files.Insert(0, GivenFiles[0]);
            Files.AddRange(GivenFiles.GetRange(1, GivenFiles.Count - 1));
            OriginalFiles = new List<string>(Files);

            if (ExpIDs == null)
                ExpIDs = new List<int>();

            ExpIDs.Insert(0, GivenExpIDs[0]);
            ExpIDs.AddRange(GivenExpIDs.GetRange(1, GivenExpIDs.Count - 1));
            OriginalExpIDs = new List<int>(ExpIDs);*/

            if (Files == null)
                Files = new List<string>(GivenFiles);
            else
                Files.AddRange(GivenFiles);
            OriginalFiles = new List<string>(Files);

            if (ExpIDs == null)
                ExpIDs = new List<int>(GivenExpIDs);
            else
                ExpIDs.AddRange(GivenExpIDs);
            OriginalExpIDs = new List<int>(ExpIDs);


            if (Textures == null)
                Textures = new List<ITexture2D>();
            Textures.AddRange(GivenTextures);

            NumMips = GivenMips;
            if (Hash == 0 && GivenHash != 0)
                Hash = GivenHash;
        }

        public void Update(TreeTexInfo tex, string pathBIOGame)
        {
            Update(tex.Files, tex.ExpIDs, tex.Hash, tex.NumMips, tex.Textures, pathBIOGame);
        }


        /// <summary>
        /// Common initialiser used by both TexInfo derivations.
        /// </summary>
        /// <param name="GivenFiles">PCC's containing the texture.</param>
        /// <param name="GivenExpIDs">ExpID's of texture within PCC's.</param>
        /// <param name="GivenHash">Hash of texture.</param>
        /// <param name="GivenMips">Number of mips of texture.</param>
        /// <param name="GivenTextures">Textures stored to make accessing easier.</param>
        /// <param name="WhichGame">Game target.</param>
        private void InfoInitialise(List<string> GivenFiles, List<int> GivenExpIDs, uint GivenHash, int GivenMips, List<ITexture2D> GivenTextures, int WhichGame, string pathBIOGame)
        {
            GameVersion = WhichGame;
            Update(GivenFiles, GivenExpIDs, GivenHash, GivenMips, GivenTextures, pathBIOGame);
        }


        /// <summary>
        /// Common initialiser used by both TexInfo derivations.
        /// </summary>
        /// <param name="tex2D">Texture2D to get info from, e.g. Format, mips.</param>
        /// <param name="ExpID">ExpID of texture.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pcc">PCC to get info from, e.g. PackageName</param>
        /// <param name="tfcoffset">Offset of image data in TFC.</param>
        /// <param name="thumbpath">Path to thumbnail image.</param>
        private void InfoInitialise(ITexture2D tex2D, int ExpID, uint hash, int WhichGame, IPCCObject pcc, int tfcoffset, string thumbpath, string pathBIOGame)
        {
            List<string> files = new List<string>();
            files.Add(pcc.pccFileName);
            List<int> expids = new List<int>();
            expids.Add(ExpID);
            InfoInitialise(files, expids, hash, tex2D.imgList.Count, new List<ITexture2D>(), WhichGame, pathBIOGame);

            FullPackage = pcc.Exports[ExpID].PackageFullName;

            TexName = tex2D.texName;
            tfcOffset = tfcoffset;

            Format = tex2D.texFormat;
            ThumbnailPath = thumbpath;

            // KFreon: ME2 only?
            if (pcc.Exports[ExpID].PackageFullName == "Base Package")
                FullPackage = Path.GetFileNameWithoutExtension(pcc.pccFileName).ToUpperInvariant();
            else
                FullPackage = pcc.Exports[ExpID].PackageFullName.ToUpperInvariant();
        }

        /// <summary>
        /// Constructor for tree texture object.
        /// </summary>
        /// <param name="GivenFiles">List of PCC's containing texture.</param>
        /// <param name="GivenExpIDs">List of ExpID's of texture within PCC's.</param>
        /// <param name="GivenHash">Hash of texture.</param>
        /// <param name="GivenMips">Number of mips in texture.</param>
        /// <param name="GivenTextures">List of easy access Texture2D's.</param>
        /// <param name="WhichGame">Game target.</param>
        public TreeTexInfo(List<string> GivenFiles, List<int> GivenExpIDs, uint GivenHash, int GivenMips, List<ITexture2D> GivenTextures, int WhichGame, string pathBIOGame)
        {
            InfoInitialise(GivenFiles, GivenExpIDs, GivenHash, GivenMips, GivenTextures, WhichGame, pathBIOGame);
        }


        /// <summary>
        /// Constructor for tree texture object for use in initial lists to be populated later.
        /// </summary>
        public TreeTexInfo()
        {
            // KFreon: Initialise lists
            Textures = new List<ITexture2D>();
            Files = new List<string>();
            OriginalFiles = new List<string>();
            ExpIDs = new List<int>();
            OriginalFiles = new List<string>();
        }


        /// <summary>
        /// Constructor for tree texture objects.
        /// </summary>
        /// <param name="tex2D">Texture2D to get data from.</param>
        /// <param name="ExpID">ExpID of texture.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pcc">PCC to get info from.</param>
        /// <param name="tfcoffset">Offset of texture data in TFC.</param>
        /// <param name="thumbpath">Path to thumbnail.</param>
        public TreeTexInfo(ITexture2D tex2D, int ExpID, uint hash, int WhichGame, IPCCObject pcc, int tfcoffset, string thumbpath, string pathBIOGame)
        {
            InfoInitialise(tex2D, ExpID, hash, WhichGame, pcc, tfcoffset, thumbpath, pathBIOGame);
        }


        /// <summary>
        /// Constructor for tree texture object.
        /// </summary>
        /// <param name="temppcc">PCC to get info from.</param>
        /// <param name="ExpID">ExpID of texture.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">BIOGame path to game targeted.</param>
        /// <param name="ExecPath">Path to ME3Explorer \exec\ folder.</param>
        /// <param name="allfiles">List of all PCC's containing texture.</param>
        /// <param name="Success">OUT: True if sucessfully created.</param>
        public TreeTexInfo(IPCCObject temppcc, int ExpID, int WhichGame, string pathBIOGame, string ExecPath, out bool Success)
        {
            Success = false;
            CRC32 crcgen = new CRC32();
            string ArcPath = pathBIOGame;
            ITexture2D temptex2D = null;
            if (temppcc.Exports[ExpID].ValidTextureClass())
            {
                try { temptex2D = temppcc.CreateTexture2D(ExpID, pathBIOGame); }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return; 
                }

                // KFreon:  If no images, ignore
                if (temptex2D.imgList.Count == 0)
                    return;


                string texname = temptex2D.texName;

                IImageInfo tempImg = temptex2D.GenerateImageInfo();
                uint hash = 0;



                /*if (WhichGame != 1 && temptex2D.arcName != "None")
                    ValidFirstPCC = true;*/
                




                // KFreon: Add pcc name to list in tex2D if necessary
                /*if (temptex2D.allFiles == null || temptex2D.allFiles.Count == 0)
                {
                    temptex2D.allFiles = new List<string>();
                    temptex2D.allFiles.Add(temppcc.pccFileName);
                }
                else if (!temptex2D.allFiles.Contains(temppcc.pccFileName))
                    temptex2D.allFiles.Add(temppcc.pccFileName);*/

                // KFreon: Get texture hash
                if (tempImg.CompareStorage("pccSto"))
                {
                    if (temptex2D.texFormat != "PF_NormalMap_HQ")
                        hash = ~crcgen.BlockChecksum(temptex2D.DumpImg(tempImg.imgSize, ArcPath));
                    else
                        hash = ~crcgen.BlockChecksum(temptex2D.DumpImg(tempImg.imgSize, pathBIOGame), 0, tempImg.uncSize / 2);
                }
                else
                {
                    byte[] buffer = temptex2D.DumpImg(tempImg.imgSize, ArcPath);
                    if (buffer == null)
                        hash = 0;
                    else
                    {
                        if (temptex2D.texFormat != "PF_NormalMap_HQ")
                            hash = ~crcgen.BlockChecksum(buffer);
                        else
                            hash = ~crcgen.BlockChecksum(buffer, 0, tempImg.uncSize / 2);
                    }
                }

                // KFreon: Get image thumbnail
                string thumbnailPath = ExecPath + "placeholder.ico";
                string tempthumbpath = ExecPath + "ThumbnailCaches\\" + "ME" + WhichGame + "ThumbnailCache\\" + texname + "_" + hash + ".jpg";
                bool exists = File.Exists(tempthumbpath);
                if (!exists)
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(temptex2D.GetImageData()))
                            if (ImageEngine.GenerateThumbnailToFile(ms, tempthumbpath, 128))
                                thumbnailPath = tempthumbpath;
                    }
                    catch { }  // KFreon: Don't really care about failures
                    

                // KFreon: Initialise things
                ValidFirstPCC = WhichGame == 2 && (!String.IsNullOrEmpty(temptex2D.arcName) && temptex2D.arcName != "None");
                InfoInitialise(temptex2D, ExpID, hash, WhichGame, temppcc, tempImg.offset, thumbnailPath, pathBIOGame);
                Success = true;
            }
        }


        /// <summary>
        /// Returns current texture as a Bitmap. Option to specify size.
        /// </summary>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <param name="size">OPTIONAL: Maximum size on any dimension. Defaults to max.</param>
        /// <returns>Bitmap image of texture.</returns>
        public Bitmap GetImage(string pathBIOGame, int size = -1)
        {
            ITexture2D tex2D = Textures[0];
            return tex2D.GetImage(size);
        }

        internal void ReorderCheck(string pathBIOGame)
        {
            if (!ValidFirstPCC && GameVersion == 2)
            {
                ValidFirstPCC = KFreonLib.PCCObjects.Misc.ReorderFiles(ref Files, ref ExpIDs, pathBIOGame, GameVersion);

                
                OriginalFiles = new List<string>(Files);

                if (ExpIDs == null)
                    ExpIDs = new List<int>();

                
                OriginalExpIDs = new List<int>(ExpIDs);
            }
        }

        public bool ValidFirstPCC { get; set; }
    }

    /// <summary>
    /// Formerly TexNodeHash. Provides an object to store TPFTools texture objects.
    /// </summary>
    public class TPFTexInfo : abstractTexInfo, IDisposable
    {
        // KFreon: Class Specific Variables
        public String FileName = "";
        public String ExpectedFormat = "";
        public int ExpectedMips = 0;
        public bool found = false;
        public int TPFInd = -1;
        public string FilePath = null;
        public bool AutofixSuccess = true;
        public MemoryStream Thumbnail = null;
        public int ThumbInd = -1;
        public List<TPFTexInfo> FileDuplicates = null;
        public List<int> TreeDuplicates = null;
        public int Height = -1;
        public int Width = -1;
        public SaltTPF.ZipReader zippy = null;
        public bool wasAnalysed = false;
        

        #region Properties
        public bool isDef
        {
            get
            {
                string filename = FileName.ToLowerInvariant();
                return (Path.GetExtension(filename) == ".def" || Path.GetExtension(filename) == ".txt" || Path.GetExtension(filename) == ".log") ? true : false;
            }
        }

        public bool isExternal
        {
            get
            {
                return FilePath != null;
            }
        }

        public bool CorrectMips
        {
            get
            {
                // KFreon: The ImageEngine ignores mips for the most part. It'll just make its own as required.
                // KFreon: By request of CreeperLava, I've re-enabled mip notifications
                //return true;


                bool standard = (ExpectedMips > 1 && NumMips > 1) || (ExpectedMips <= 1 && NumMips <= 1);
                bool calc = ExpectedMips > 1 && NumMips < (CalculateMipCount(this.Width, this.Height) - 3); // Heff: only check down to 4x4 / 4x8
                return standard && !calc;
            }
        }

        public bool ValidFormat
        {
            get
            {
                string expected = ExpectedFormat.ToLowerInvariant();
                string given = Format.ToLowerInvariant();
                return (expected.Contains("pf_norm") && given.Contains("ati")) || (expected.Contains("ati") && given.Contains("pf_norm")) || expected.Contains(given) || given.Contains(expected);
            }
        }

        public bool Valid
        {
            get
            {
                ValidDimensions = ValidateDimensions();
                return isDef ? false : (CorrectMips && ValidFormat && ValidDimensions);
            }
        }

        public static int CalculateMipCount(int Width, int Height)
        {
            return (int)Math.Log(Math.Max(Width, Height), 2)+ 1;
        }

        public bool ValidDimensions { get; set; }
        #endregion

        /// <summary>
        /// Constructor for TPF texture object. Placeholder for lists.
        /// </summary>
        public TPFTexInfo()
        {
            // KFreon: Placeholder for lists
        }


        /// <summary>
        /// Constructor for TPF texture objects. 
        /// </summary>
        /// <param name="filename">Filename of texture.</param>
        /// <param name="tpfind">Index of texture inside TPF, if applicable.</param>
        /// <param name="path">Path of texture, if applicable.</param>
        /// <param name="zip">Zippy of TPF, if applicable.</param>
        public TPFTexInfo(string filename, int tpfind, string path, ZipReader zip, int WhichGame)
        {
            FileName = filename;
            TPFInd = tpfind;
            FilePath = path;

            Files = new List<string>();
            ExpIDs = new List<int>();
            Thumbnail = new MemoryStream();
            OriginalExpIDs = new List<int>();
            OriginalFiles = new List<string>();
            FileDuplicates = new List<TPFTexInfo>();
            TreeDuplicates = new List<int>();
            zippy = zip;
            GameVersion = WhichGame;
        }


        /// <summary>
        /// Clones current TPF texture object.
        /// </summary>
        /// <returns>New TPF texture object.</returns>
        public TPFTexInfo Clone()
        {
            TPFTexInfo retval = new TPFTexInfo(FileName, TPFInd, FilePath, zippy, GameVersion);
            retval.Files = new List<string>(Files);
            retval.OriginalFiles = new List<string>(OriginalFiles);
            retval.ExpIDs = new List<int>(ExpIDs);
            retval.OriginalExpIDs = new List<int>(OriginalExpIDs);
            retval.Hash = Hash;
            retval.OriginalHash = OriginalHash;
            retval.found = found;
            retval.Format = Format;
            retval.ExpectedFormat = ExpectedFormat;
            retval.NumMips = NumMips;
            retval.AutofixSuccess = AutofixSuccess;
            retval.ThumbInd = ThumbInd;
            retval.Thumbnail = new MemoryStream(Thumbnail.ToArray());
            retval.FileDuplicates = new List<TPFTexInfo>(FileDuplicates);
            retval.TreeDuplicates = new List<int>(TreeDuplicates);
            retval.Height = Height;
            retval.Width = Width;
            retval.TexName = TexName;
            retval.GameVersion = GameVersion;
            retval.ValidDimensions = ValidDimensions;

            return retval;
        }

        public string DisplayString(int index)
        {
            if (OriginalExpIDs.Count < index && OriginalFiles.Count < index)
                return OriginalFiles[index] + " @ " + OriginalExpIDs[index];
            else
                return null;
        }

        public string DisplayString(string file)
        {
            int index = OriginalFiles.IndexOf(file);
            if (OriginalExpIDs.Count > index && OriginalFiles.Count > index)
                return OriginalFiles[index] + " @ " + OriginalExpIDs[index];
            else
                return null;
        }

        public void UndoAnalysis(int newGameVersion)
        {
            wasAnalysed = false;
            Files.Clear();
            OriginalFiles.Clear();
            ExpIDs.Clear();
            OriginalExpIDs.Clear();
            found = false;
            ExpectedFormat = "";
            ExpectedMips = 0;
            AutofixSuccess = true;
            FileDuplicates.Clear();
            TreeDuplicates.Clear();
            TexName = null;
            GameVersion = newGameVersion;
        }


        /// <summary>
        /// Create a .mod job from current TPF texture object.
        /// </summary>
        /// <param name="ExecFolder">Path to ME3Explorer \exec\ folder.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <returns>New ModJob created from current TPF texture object.</returns>
        public ModMaker.ModJob CreateModJob(string ExecFolder, string pathBIOGame)
        {
            ModMaker.ModJob job = new ModMaker.ModJob();
            job.Name = (NumMips > 1 ? "Upscale (with MIP's): " : "Upscale: ") + TexName;
            job.Script = ModMaker.GenerateTextureScript(ExecFolder, Files, ExpIDs, TexName, GameVersion, pathBIOGame);
            job.data = Extract(null, true);
            return job;
        }


        /// <summary>
        /// Format texture details for TPFTools.
        /// </summary>
        /// <param name="Analysed">Add extra stuff if analysis has been performed.</param>
        /// <returns>New name to display.</returns>
        public String FormatTexDetails(bool Analysed)
        {
            string text = FileName;
            // KFreon: Add visual cues if problem
            if (found)
            {
                text = TexName;
                string ending = " <----";

                if (Path.GetExtension(this.FileName) != ".dds")
                {
                    ending += "NOT DDS FORMAT";
                    text = "----> " + text + ending;
                }
                else if (!isDef && !Valid)
                {
                    if (!ValidDimensions)
                        ending += "  DIMENSIONS";
                    if (!ValidFormat)
                        ending += "  FORMAT";
                    if (!CorrectMips)
                        ending += "  MIPS";
                    if (!AutofixSuccess)
                        ending = "  -> *AUTOFIX FAILED*" + ending;
                    text = "----> " + text + ending;
                }
            }
            else if (Analysed && wasAnalysed && !isDef)
                text = "----> " + text + "  <----   NOT FOUND IN TREE";
            else if (Analysed && !isDef)
                text += " <--- Not Analyzed!";

            return text;
        }

        /// <summary>
        /// Extracts image from TPF or copies image data from external file. Also able to return a byte[] of image data.
        /// </summary>
        /// <param name="ExtractPath">Path to extract image to.</param>
        /// <param name="ToMemory">OPTIONAL: If true, byte[] of image data returned.</param>
        /// <returns>If ToMemory is true, returns byte[] of image data, otherwise null.</returns>
        public byte[] Extract(string ExtractPath, bool ToMemory = false)
        {
            byte[] retval = null;
            bool ExtractType = false;

            bool? temp = ExtractPath.isDirectory();
            if (temp == null)
                return null;
            else
                ExtractType = (bool)temp;


            // KFreon: Get byte[] of image data.
            if (ToMemory)
            {
                if (isExternal)
                    retval = Textures.Methods.GetExternalData(Path.Combine(FilePath, FileName));
                else
                    retval = zippy.Entries[TPFInd].Extract(true);
            }
            else    // KFreon: Extract image to disk
            {
                try
                {
                    if (isExternal)
                        File.Copy(Path.Combine(FilePath, FileName), ExtractType ? Path.Combine(ExtractPath, FileName) : ExtractPath);
                    else
                        zippy.Entries[TPFInd].Extract(false, ExtractType ? Path.Combine(ExtractPath, FileName) : ExtractPath);
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("File already exists.   " + e.ToString());
                }
            }
            return retval;
        }


        /// <summary>
        /// Converts texture.
        /// </summary>
        public void Convert()
        {

        }


        /// <summary>
        /// Updates current texture object from tree.
        /// </summary>
        /// <param name="treeInd">Index of texture in tree.</param>
        /// <param name="treetex">Tree texture object to get info from.</param>
        public void UpdateTex(int treeInd, TreeTexInfo treetex)
        {
            found = true;
            TreeInd = treeInd;
            TexName = treetex.TexName;

            // KFreon: Reorder files so DLC isn't first
            /*List<string> files = new List<string>(treetex.Files);
            List<int> ids = new List<int>(treetex.ExpIDs);

            int count = files.Count;
            int index = -1;

            if (files[0].Contains("DLC"))
                for (int i = 0; i < count; i++)
                    if (!files[i].Contains("DLC"))
                    {
                        index = i;
                        break;
                    }

            if (index != -1)
            {
                string thing = files[index];
                files.RemoveAt(index);
                files.Insert(0, thing);

                int thing2 = ids[index];
                ids.RemoveAt(index);
                ids.Insert(0, thing2);
            }

            Files.AddRange(files);
            ExpIDs.AddRange(ids);*/
            Files.AddRange(treetex.Files);
            ExpIDs.AddRange(treetex.ExpIDs);


            
            List<PCCExpID> things = new List<PCCExpID>();

            for (int i = 0; i < Files.Count; i++)
                things.Add(new PCCExpID(Files[i], ExpIDs[i]));


            // KFreon: Reorder ME1 files
            if (GameVersion == 1)
            {
                things = things.OrderByDescending(t => t.file.Length).ToList();

                Files.Clear();
                ExpIDs.Clear();

                foreach (var item in things)
                {
                    Files.Add(item.file);
                    ExpIDs.Add(item.expid);
                }
            }

            //ExpIDs.AddRange(treetex.ExpIDs);

            OriginalFiles = new List<string>(Files);
            OriginalExpIDs = new List<int>(ExpIDs);

            ExpectedMips = treetex.NumMips;
            ExpectedFormat = treetex.Format.Replace("PF_", "");
            if (ExpectedFormat.ToUpperInvariant().Contains("NORMALMAP"))
                ExpectedFormat = "ATI2_3Dc";

            if (ExpectedFormat.ToUpperInvariant().Contains("A8R8G8B8"))
                ExpectedFormat = "ARGB";

            // KFreon: File Dups
            List<TPFTexInfo> dups = new List<TPFTexInfo>(FileDuplicates);
            FileDuplicates.Clear();
            foreach (TPFTexInfo tex in dups)
            {
                TPFTexInfo texn = tex;
                texn.found = true;
                texn.TreeInd = TreeInd;
                texn.TexName = treetex.TexName;

                texn.Files.AddRange(treetex.Files);
                texn.ExpIDs.AddRange(treetex.ExpIDs);

                texn.ExpectedFormat = treetex.Format;
                texn.ExpectedMips = treetex.NumMips;

                texn.OriginalExpIDs = new List<int>(ExpIDs);
                texn.OriginalFiles = new List<string>(Files);

                FileDuplicates.Add(texn);
            }

            ValidDimensions = ValidateDimensions();
        }

        public struct PCCExpID
        {
            public string file;
            public int expid;

            public PCCExpID(string name, int exp)
            {
                file = name;
                expid = exp;
            }
        }


        /// <summary>
        /// Collects details of current texture, like thumbnail, number of mips, and format.
        /// </summary>
        public void EnumerateDetails()
        {
            byte[] data = Extract("", true);
            if (data == null)
                DebugOutput.PrintLn("Unable to get image data for: " + FileName);
            else
            {
                // KFreon: Check formatting etc
                try
                { 
                    using (ImageEngineImage image = new ImageEngineImage(data))
                    {
                        NumMips = image.NumMipMaps;
                        Height = image.Height;
                        Width = image.Width;
                        Format = image.Format.InternalFormat.ToString().Replace("DDS_", "");

                        image.Save(Thumbnail, ImageEngineFormat.JPG, false, 64);
                    }
                }
                catch(Exception e)
                {
                    DebugOutput.PrintLn("Error checking texture: " + e.Message);
                    NumMips = 0;
                    Format = Format ?? Path.GetExtension(FileName);
                    Thumbnail = null;
                }
                data = null;
            }
        }

        public string GetFileFromDisplay(string file)
        {
            return file.Split('@')[0];
        }

        public void Dispose()
        {
            Thumbnail.Dispose();
        }

        public string Autofixedpath(string TemporaryPath)
        {
            Format format = ImageFormats.FindFormatInString(ExpectedFormat);
            string newfilename = String.IsNullOrEmpty(ExpectedFormat) ? FileName : Path.ChangeExtension(FileName, ImageFormats.GetExtensionOfFormat(format.InternalFormat));
            return Path.Combine(Path.Combine(TemporaryPath, "Autofixed"), newfilename);   
        }

        public bool ValidateDimensions()
        {
            return UsefulThings.General.IsPowerOfTwo(Height) && UsefulThings.General.IsPowerOfTwo(Width);
        }
    }
}
