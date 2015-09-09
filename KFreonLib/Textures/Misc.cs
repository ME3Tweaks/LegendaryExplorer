using AmaroK86.ImageFormat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;
using System.Drawing;
using DDSPreview = KFreonLib.Textures.SaltDDSPreview.DDSPreview;
using System.Drawing.Drawing2D;
using SaltTPF;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KFreonLib.PCCObjects;
using KFreonLib.Debugging;
using CSharpImageLibrary;

namespace KFreonLib.Textures
{

    /// <summary>
    /// Provides methods related to textures.
    /// </summary>
    public static class Methods
    {
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static Bitmap GetJPGFromByteArray(byte[] imgData, int width, int height)
        {
            using (MemoryStream stream = new MemoryStream(imgData))
            {
                ImageEngineImage img = new ImageEngineImage(stream, ".dds");

                using (MemoryStream savestream = new MemoryStream(imgData.Length))
                {
                    img.Save(savestream, ImageEngineFormat.JPG, false);
                    return UsefulThings.WinForms.Misc.CreateBitmap(savestream.ToArray(), width, height);
                }
            }
        }





        #region HASHES
        /// <summary>
        /// Finds hash from texture name given list of PCC's and ExpID's.
        /// </summary>
        /// <param name="name">Name of texture.</param>
        /// <param name="Files">List of PCC's to search with.</param>
        /// <param name="IDs">List of ExpID's to search with.</param>
        /// <param name="TreeTexes">List of tree textures to search through.</param>
        /// <returns>Hash if found, else 0.</returns>
        public static uint FindHashByName(string name, List<string> Files, List<int> IDs, List<TreeTexInfo> TreeTexes)
        {
            foreach (TreeTexInfo tex in TreeTexes)
                if (name == tex.TexName)
                    for (int i = 0; i < Files.Count; i++)
                        for (int j = 0; j < tex.Files.Count; j++)
                            if (tex.Files[j].Contains(Files[i].Replace("\\\\", "\\")))
                                if (tex.ExpIDs[j] == IDs[i])
                                    return tex.Hash;
            return 0;
        }


        /// <summary>
        /// Returns a uint of a hash in string format. 
        /// </summary>
        /// <param name="line">String containing hash in texmod log format of name|0xhash.</param>
        /// <returns>Hash as a uint.</returns>
        public static uint FormatTexmodHashAsUint(string line)
        {
            return uint.Parse(line.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
        }


        /// <summary>
        /// Returns hash as a string in the 0xhash format.
        /// </summary>
        /// <param name="hash">Hash as a uint.</param>
        /// <returns>Hash as a string.</returns>
        public static string FormatTexmodHashAsString(uint hash)
        {
            return "0x" + System.Convert.ToString(hash, 16).PadLeft(8, '0').ToUpper();
        }
        #endregion

        #region Images

        /// <summary>
        /// Gets external image data as byte[] with some buffering i.e. retries if fails up to 20 times.
        /// </summary>
        /// <param name="file">File to get data from.</param>
        /// <returns>byte[] of image.</returns>
        public static byte[] GetExternalData(string file)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    // KFreon: Try readng file to byte[]
                    return File.ReadAllBytes(file);
                }
                catch
                {
                    // KFreon: Sleep for a bit and try again
                    System.Threading.Thread.Sleep(300);
                }
            }
            return null;
        }


        
        /// <summary>
        /// GONNA BE REPLACED!
        /// </summary>
        /// <param name="tmpTex"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static Bitmap DDSCheck(abstractTexInfo tmpTex, byte[] data)
        {
            DDSPreview dds = new DDSPreview(data);
            tmpTex.NumMips = (int)dds.NumMips;
            tmpTex.Format = dds.FormatString;
            byte[] datat = dds.GetMipData();
            Bitmap retval = DDSImage.ToBitmap(datat, (dds.FormatString == "G8") ? DDSFormat.G8 : dds.Format, (int)dds.Width, (int)dds.Height);
            dds = null;
            return retval;
        }

        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static Bitmap BetterDDSCheck(abstractTexInfo tmpTex, byte[] imgData)
        {
            Bitmap retval = null;
            using (MemoryStream ms = new MemoryStream(imgData))
            {
                ImageEngineImage img = new ImageEngineImage(ms, ".dds");
                tmpTex.NumMips = img.NumMipMaps;
                tmpTex.Format = img.Format.InternalFormat.ToString();

                if (tmpTex.Format == "None")
                {
                    var preview = new DDSPreview(imgData);
                    if (preview.FormatString == "A8R8G8B8")
                        tmpTex.Format = "ARGB";
                    else if (preview.FormatString == "R8G8B8")
                        tmpTex.Format = "RGB";
                    else if (preview.FormatString == "A8B8G8R8")
                        tmpTex.Format = "ABGR";
                    else if (preview.FormatString == "B8G8R8")
                        tmpTex.Format = "BGR";

                    // Heff: unreliable, seems to always be ARGB.
                    //if (((ResILImage)img).MemoryFormat == ResIL.Unmanaged.DataFormat.RGBA)
                    //tmpTex.Format = "ARGB";
                }


                try
                {
                    using (MemoryStream savestream = new MemoryStream())
                    {
                        img.Save(savestream, ImageEngineFormat.JPG, false);
                        retval = UsefulThings.WinForms.Misc.CreateBitmap(savestream.ToArray(), img.Width, img.Height);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }            
            return retval;
        }


        /// <summary>
        /// Check that texture format is what we want.  PROBABLY GONNA BE REPLACED!
        /// </summary>
        /// <param name="newtexture">Path to new texture file.</param>
        /// <param name="desiredformat">Format retrieved from tree, to which newtexture must conform.</param>
        /// <returns>Format of new texture as string, empty string if not correct, BORKED if something broke.</returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static bool CheckTextureFormat(byte[] data, string desiredformat, out string format)
        {
            format = null;
            try
            {
                // KFreon: Load image to DDS to test its format
                DDSPreview dds = new DDSPreview(data);
                format = dds.FormatString;
                return CheckTextureFormat(format, desiredformat);
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckTextureFormat(string currentFormat, string desiredFormat)
        {
            string curr = currentFormat.ToLowerInvariant().Replace("pf_", "").Replace("DDS_", "");
            string des = desiredFormat.ToLowerInvariant().Replace("pf_", "").Replace("DDS_", "");
            bool correct = curr == des;
            return correct || (!correct && curr.Contains("ati") && des.Contains("normalmap")) || (!correct && curr.Contains("normalmap") && des.Contains("ati"));
        }


        /// <summary>
        /// Check that new texture contains enough mips.
        /// </summary>
        /// <param name="newtexture">Path to texture to load.</param>
        /// <param name="ExpectedMips">Number of expected mips.</param>
        /// <returns>True if number of mips is valid.</returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static bool CheckTextureMips(string newtexture, int ExpectedMips, out int numMips)
        {
            numMips = 0;
            try
            {
                DDSPreview dds = new DDSPreview(File.ReadAllBytes(newtexture));
                numMips = (int)dds.NumMips;
                if (ExpectedMips > 1)
                {
                    if (dds.NumMips < ExpectedMips)
                        return false;
                    else
                        return true;
                }
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Resizes thumbnail to correct proportions and returns it.
        /// </summary>
        /// <param name="image">Original thumbnail.</param>
        /// <param name="size">Size to set.</param>
        /// <returns>Bitmap of new thumbnail.</returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static Bitmap FixThumb(Image image, int size)
        {
            int tw, th, tx, ty;
            int w = image.Width;
            int h = image.Height;
            double whRatio = (double)w / h;

            if (image.Width >= image.Height)
            {
                tw = size;
                th = (int)(tw / whRatio);
            }
            else
            {
                th = size;
                tw = (int)(th * whRatio);
            }
            tx = (size - tw) / 2;
            ty = (size - th) / 2;

            Bitmap thumb = new Bitmap(size, size);
            Graphics g = Graphics.FromImage(thumb);
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(image, new Rectangle(tx, ty, tw, th), new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);

            return thumb;
        }


        /// <summary>
        /// Salts resize image function. Returns resized image.
        /// </summary>
        /// <param name="imgToResize">Image to resize</param>
        /// <param name="size">Size to shape to</param>
        /// <returns>Resized image as an Image.</returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static Image resizeImage(Image imgToResize, Size size)
        {
            // KFreon: And so begins the black magic
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            using (Graphics g = Graphics.FromImage((Image)b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            }
            return (Image)b;
        }


        [Obsolete("just no...", true)]
        public static Bitmap GetImage(string ddsformat, byte[] imgData)
        {
            using (ImageEngineImage img = new ImageEngineImage(imgData))
                return img.GetGDIBitmap();
        }


        /// <summary>
        /// Saves given image to file.
        /// </summary>
        /// <param name="image">Image to save.</param>
        /// <param name="savepath">Path to save image to.</param>
        /// <returns>True if saved successfully. False if failed or already exists.</returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static bool SaveImage(Image image, string savepath)
        {
            // Heff: fix to ensure that the temp directories are created.
            Directory.CreateDirectory(Path.GetDirectoryName(savepath));
            if (!File.Exists(savepath))
                try
                {
                    image.Save(savepath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("GDI Error in: " + savepath);
                    DebugOutput.PrintLn("ERROR: " + e.Message);
                    return false;
                }

            return true;
        }


        /// <summary>
        /// GOING TO CHANGE
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [Obsolete("Use CSharpImageLibrary instead.", true)]
        public static DDSPreview GetAllButDXT3DDSData(byte[] data)
        {
            if (data == null)
            {
                DebugOutput.PrintLn("Unable to access file or invalid file.");
                return null;
            }

            DDSPreview ddsimg = new DDSPreview(data);
            return ddsimg;
        }
    }


    /// <summary>
    /// Provides functions to create texture objects.
    /// </summary>
    public static class Creation
    {
        #region Object Creators
        /// <summary>
        /// Generates a thumbnail image from a given image.
        /// </summary>
        /// <param name="img">Image to get a thumbnail from.</param>
        /// <param name="size">OPTIONAL: Size to resize to (Maximum in any direction). Defaults to 128.</param>
        /// <returns></returns>
        [Obsolete("not good", true)]
        public static Bitmap GenerateThumbImage(Image img, int size = 128)
        {
            // KFreon: Get resize details for resize to max size 128 (same aspect ratio)
            double DeterminingDimension = (img.Width > img.Height) ? img.Width : img.Height;
            double divisor = DeterminingDimension / size;

            // KFreon: Check for smaller dimensions and don't resize if so (SCALE UP?)
            if (divisor < 1)
                divisor = 1;

            // KFreon: If image is weird (i.e. 1px high), nullify
            if ((int)img.Width / divisor < 1 || (int)img.Height / divisor < 1)
                return null;
            else
            {
                // KFreon: Resize image
                Image image = (divisor == 1) ? new Bitmap(img) : new Bitmap(img, new Size((int)(img.Width / divisor), (int)(img.Height / divisor)));//resizeImage((Image)img, new Size((int)(img.Width / divisor), (int)(img.Height / divisor)));
                image = Methods.FixThumb(image, size);
                return image as Bitmap;
            }
        }


        /// <summary>
        /// Generates a thumbnail from an image and saves to file.
        /// </summary>
        /// <param name="img">Image to generate thumbnail from.</param>
        /// <param name="savepath">Path to save thumbnail to.</param>
        /// <param name="execpath">Path to ME3Explorer \exec\ folder.</param>
        /// <returns>Path to saved thumbnail.</returns>
        [Obsolete("not good", true)]
        public static string GenerateThumbnail(Bitmap img, string savepath, string execpath)
        {
            // KFreon: Get thumbnail and set savepath
            using (Bitmap newimg = GenerateThumbImage(img))
            {
                if (newimg == null)
                    savepath = execpath + "placeholder.ico";
                else
                {
                    // KFreon: Save image to file
                    for (int i = 0; i < 3; i++)
                        if (Methods.SaveImage(newimg, savepath))
                            break;
                        else
                            System.Threading.Thread.Sleep(1000);
                }
            }
            string retval = execpath + "placeholder.ico";
            if (savepath != "")
                retval = savepath;

            return retval;
        }

        public static string GenerateThumbnail(string filename, int WhichGame, int expID, string pathBIOGame, string savepath, string execpath)
        {
            ITexture2D tex2D = CreateTexture2D(filename, expID, WhichGame, pathBIOGame);
            using (MemoryStream ms = UsefulThings.RecyclableMemoryManager.GetStream(tex2D.GetImageData()))
                ImageEngine.GenerateThumbnailToFile(ms, savepath, 128, 128);
            return savepath;
        }


        /// <summary>
        /// Load an image into one of AK86's classes.
        /// </summary>
        /// <param name="im">AK86 image already, just return it unless null. Then load from fileToLoad.</param>
        /// <param name="fileToLoad">Path to file to be loaded. Irrelevent if im is provided.</param>
        /// <returns>AK86 Image file.</returns>
        public static ImageFile LoadAKImageFile(ImageFile im, string fileToLoad)
        {
            ImageFile imgFile = null;
            if (im != null)
                imgFile = im;
            else
            {
                if (!File.Exists(fileToLoad))
                    throw new FileNotFoundException("invalid file to replace: " + fileToLoad);

                // check if replacing image is supported
                string fileFormat = Path.GetExtension(fileToLoad);
                switch (fileFormat)
                {
                    case ".dds": imgFile = new DDS(fileToLoad, null); break;
                    case ".tga": imgFile = new TGA(fileToLoad, null); break;
                    default: throw new FileNotFoundException(fileFormat + " image extension not supported");
                }
            }
            return imgFile;
        }


        /// <summary>
        /// Create a Texture2D from things.
        /// </summary>
        /// <param name="filename">Filename to load Texture2D from.</param>
        /// <param name="expID">ExpID of texture in question.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <returns>Texture2D object</returns>
        public static ITexture2D CreateTexture2D(string filename, int expID, int WhichGame, string pathBIOGame, uint hash = 0)
        {
            IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(filename, WhichGame);
            return pcc.CreateTexture2D(expID, pathBIOGame, hash);
        }


        /// <summary>
        /// Populates a Texture2D given a base Texture2D.
        /// </summary>
        /// <param name="tex2D">Base Texture2D. Most things missing.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <returns>Populated Texture2D.</returns>
        public static ITexture2D CreateTexture2D(ITexture2D tex2D, int WhichGame, string pathBIOGame)
        {
            ITexture2D temp = CreateTexture2D(tex2D.allPccs[0], tex2D.expIDs[0], WhichGame, pathBIOGame);
            temp.allPccs = new List<string>(tex2D.allPccs);
            temp.Hash = tex2D.Hash;
            temp.hasChanged = tex2D.hasChanged;
            temp.expIDs = tex2D.expIDs;
            return temp;
        }


        /// <summary>
        /// Creates a Texture2D from a bunch of stuff.
        /// </summary>
        /// <param name="texName">Name of texture to create.</param>
        /// <param name="pccs">List of PCC's containing texture.</param>
        /// <param name="ExpIDs">List of ExpID's of texture in PCC's. MUST have same number of elements as in PCC's.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <returns>Texture2D object.</returns>
        public static ITexture2D CreateTexture2D(string texName, List<string> pccs, List<int> ExpIDs, int WhichGame, string pathBIOGame, uint hash = 0)
        {
            ITexture2D temptex2D = null;
            switch (WhichGame)
            {
                case 1:
                    temptex2D = new ME1Texture2D(texName, pccs, ExpIDs, pathBIOGame, WhichGame, hash);
                    break;
                case 2:
                    temptex2D = new ME2Texture2D(texName, pccs, ExpIDs, pathBIOGame,WhichGame, hash);
                    break;
                case 3:
                    temptex2D = new ME3SaltTexture2D(texName, pccs, ExpIDs, hash, pathBIOGame, WhichGame);
                    break;
            }
            if (hash != 0)
                temptex2D.Hash = hash;
            return temptex2D;
        }
        #endregion
    }
        #endregion
}
