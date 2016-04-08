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
using CSharpImageLibrary.General;
using UsefulThings;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace KFreonLib.Textures
{

    /// <summary>
    /// Provides methods related to textures.
    /// </summary>
    public static class Methods
    {
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
            uint hash = 0;
            uint.TryParse(line.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash);
            return hash;
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


        /*public static bool CheckTextureFormat(string currentFormat, string desiredFormat)
        {
            string curr = currentFormat.ToLowerInvariant().Replace("pf_", "").Replace("DDS_", "");
            string des = desiredFormat.ToLowerInvariant().Replace("pf_", "").Replace("DDS_", "");
            bool correct = curr == des;
            return correct || (!correct && curr.Contains("ati") && des.Contains("normalmap")) || (!correct && curr.Contains("normalmap") && des.Contains("ati"));
        }*/

        public static ImageEngineFormat ParseFormat(string formatString)
        {
            if (String.IsNullOrEmpty(formatString))
                return ImageEngineFormat.Unknown;

            if (formatString.Contains("normal", StringComparison.OrdinalIgnoreCase))
                return ImageEngineFormat.DDS_ATI2_3Dc;
            else
                return ImageFormats.FindFormatInString(formatString).InternalFormat;
        }

        public static string StringifyFormat(ImageEngineFormat format)
        {
            return format.ToString().Replace("DDS_", "").Replace("_3Dc","");
        }
    }


    /// <summary>
    /// Provides functions to create texture objects.
    /// </summary>
    public static class Creation
    {
        #region Object Creators
        public static string GenerateThumbnail(string filename, int WhichGame, int expID, string pathBIOGame, string savepath, string execpath)
        {
            ITexture2D tex2D = CreateTexture2D(filename, expID, WhichGame, pathBIOGame);
            using (MemoryStream ms = new MemoryStream(tex2D.GetImageData()))
            {
                GenerateThumbnail(ms, savepath, 128);
            }
            return savepath;
        }

        public static string GenerateThumbnail(Stream sourceStream, string savePath, int maxDimension)
        {
            MemoryStream stream = ImageEngine.GenerateThumbnailToStream(sourceStream, 128, false, true);
            if (stream == null)
                return null;

            WriteableBitmap source = new WriteableBitmap(UsefulThings.WPF.Images.CreateWPFBitmap(stream));
            WriteableBitmap dest = new WriteableBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, System.Windows.Media.PixelFormats.Bgra32, source.Palette);

            // KFreon: Write onto black
            var overlayed = Overlay(dest, source);

            JpegBitmapEncoder enc = new JpegBitmapEncoder();
            enc.QualityLevel = 90;
            enc.Frames.Add(BitmapFrame.Create(overlayed));

            using (FileStream fs = new FileStream(savePath, FileMode.Create))
                enc.Save(fs);

            return savePath;
        }


        /// <summary>
        /// Overlays one image on top of another.
        /// Both images MUST be the same size.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="overlay"></param>
        /// <returns></returns>
        public static BitmapSource Overlay(BitmapSource source, BitmapSource overlay)
        {
            if (source.PixelWidth != overlay.PixelWidth || source.PixelHeight != overlay.PixelHeight)
                throw new InvalidDataException("Source and overlay must be the same dimensions.");

            var drawing = new DrawingVisual();
            var context = drawing.RenderOpen();
            context.DrawImage(source, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight));
            context.DrawImage(overlay, new System.Windows.Rect(0, 0, overlay.PixelWidth, overlay.PixelHeight));

            context.Close();
            var overlayed = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
            overlayed.Render(drawing);


            return overlayed;
        }

        public static Bitmap Overlay(Bitmap source, Bitmap overlay)
        {
            if (source.Width != overlay.Width || source.Height != overlay.Height)
                throw new InvalidDataException("Source and Overlay must be same dimensions.");

            Bitmap img = new Bitmap(source.Width, source.Height);
            using (Graphics gr = Graphics.FromImage(img))
            {
                gr.DrawImage(source, new Point(0, 0));
                gr.DrawImage(overlay, new Point(0, 0));
            }

            return img;
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
