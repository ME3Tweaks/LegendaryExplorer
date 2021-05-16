using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    /// Class for generating TOC files
    /// </summary>
    public class TOCCreator
    {
        /// <summary>
        /// Returns the files in a given directory that match the pattern of a TOCable file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetTocableFiles(string path)
        {
            string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc", "*.isb" };
            var res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        /// <summary>
        /// Recursively finds all TOCable files in a directory and it's subfolders
        /// </summary>
        /// <param name="basefolder"></param>
        /// <returns></returns>
        private static List<string> GetFiles(string basefolder)
        {
            var res = new List<string>();
            string directoryName = Path.GetFileName(Path.GetDirectoryName(basefolder));
            res.AddRange(GetTocableFiles(basefolder));
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            var folders = folder.GetDirectories();
            if (folders.Length != 0)
            {
                if (directoryName != "BIOGame")
                {
                    //treat as dlc and include all folders.
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(Path.Combine(basefolder, f.Name)));
                }
                else
                {
                    //biogame, only do cookedpcconsole and movies.
                    foreach (DirectoryInfo f in folders)
                    {
                        if (f.Name == "CookedPCConsole" || f.Name == "Movies")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name)));
                        else if (f.Name == "Content")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name, "Packages\\ISACT")));

                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Finds directories that need TOC files created. Includes BioGame, all DLC folders
        /// </summary>
        /// <param name="game">Game to search installation directory for</param>
        /// <returns></returns>
        private static List<string> GetTOCableFoldersForGame(MEGame game)
        {
            List<string> tocDirectories = new()
            {
                MEDirectories.GetBioGamePath(game)
            };


            if (Directory.Exists(MEDirectories.GetDLCPath(game)))
            {
                var dlcFolders = new DirectoryInfo(MEDirectories.GetDLCPath(game)).GetDirectories();
                tocDirectories.AddRange(dlcFolders.Where(f => f.Name.StartsWith("DLC_")).Select(f => f.ToString()));
            }

            return tocDirectories;
        }

        /// <summary>
        /// Returns whether or not a folder should be TOCable
        /// </summary>
        /// <param name="directory">Directory to checl</param>
        /// <returns></returns>
        /// TODO: Is there an easy way to make this not iterate over all files?
        public static bool IsTOCableFolder(string directory) => GetFiles(directory).Count > 0;


        public static void CreateTOCForGame(MEGame game, Action<int> percentDoneCallback = null)
        {
            if (game is MEGame.ME1 or MEGame.ME2)
            {
                throw new ArgumentOutOfRangeException("TOC files cannot be created for ME1 or ME2");
            }

            var tocFolders = GetTOCableFoldersForGame(game);

            foreach(var dir in tocFolders)
            {
                var tocFileLocation = Path.Combine(dir, "PCConsoleTOC.bin");
                File.WriteAllBytes(tocFileLocation, CreateTOCForDirectory(dir).GetBuffer());
                var percent = ((float)tocFolders.IndexOf(dir) / tocFolders.Count);
                percentDoneCallback?.Invoke((int)(percent * 100));
            }
        }


        /// <summary>
        /// Creates the binary for a TOC file for a specified directory root
        /// </summary>
        /// <param name="directory">DLC_ directory, like DLC_CON_JAM, or the BIOGame directory of the game.</param>
        /// <returns>Memorystream of TOC created, null if there are no entries or input was invalid</returns>
        public static MemoryStream CreateTOCForDirectory(string directory)
        {
            var files = GetFiles(directory);
            var originalFilesList = files;
            if (files.Count > 0)
            {
                //Strip the non-relative path information
                string file0fullpath = files[0];
                int dlcFolderStartSubStrPos = file0fullpath.IndexOf("DLC_", StringComparison.InvariantCultureIgnoreCase);
                if (dlcFolderStartSubStrPos > 0)
                {
                    files = files.Select(x => x.Substring(dlcFolderStartSubStrPos)).ToList();
                    files = files.Select(x => x.Substring(x.IndexOf('\\') + 1)).ToList(); //remove first slash
                }
                else
                {
                    int biogameStrPos = file0fullpath.IndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase);
                    if (biogameStrPos > 0)
                    {
                        files = files.Select(x => x.Substring(biogameStrPos)).ToList();
                    }
                }

                var entries = originalFilesList.Select((t, i) => (files[i], (int) new FileInfo(t).Length)).ToList();

                return CreateTOCForEntries(entries);
            }
            throw new Exception("There are no TOCable files in the specified directory.");
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified list of filenames and sizes. The filenames should already be in the format that will be used in the TOC itself.
        /// </summary>
        /// <param name="filesystemInfo">list of filenames and sizes for the TOC</param>
        /// <returns>memorystream of TOC, null if list is empty</returns>
        public static MemoryStream CreateTOCForEntries(List<(string filename, int size)> filesystemInfo)
        {
            if (filesystemInfo.Count != 0)
            {
                long selfSizePosition = -1;
                byte[] SHA1 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                MemoryStream fs = MemoryManager.GetMemoryStream();

                fs.WriteInt32(TOCBinFile.TOCMagicNumber);
                fs.WriteInt32(0x0);
                fs.WriteInt32(0x1);
                fs.WriteInt32(0x8);
                fs.WriteInt32(filesystemInfo.Count);
                for (int i = 0; i < filesystemInfo.Count; i++)
                {
                    (string file, int size) entry = filesystemInfo[i];
                    if (i == filesystemInfo.Count - 1) //Entry Size - is last item?
                        fs.WriteUInt16(0);
                    else
                        fs.WriteUInt16((ushort)(0x1D + entry.file.Length));
                    fs.WriteUInt16(0); //Flags
                    if (Path.GetFileName(entry.file).ToLower() != @"pcconsoletoc.bin")
                    {
                        fs.WriteInt32(entry.size); //Filesize
                    }
                    else
                    {
                        selfSizePosition = fs.Position;
                        fs.WriteInt32(0); //Filesize
                    }

                    fs.Write(SHA1, 0, 20);
                    fs.WriteStringASCII(entry.file);
                    //foreach (char c in file)
                    //    fs.WriteByte((byte)c);
                    fs.WriteByte(0);
                }

                if (selfSizePosition >= 0)
                {
                    // Write the size of our own TOC. This ensures TOC appears up to date when we try to update it later
                    // (important for DLC TOCs)
                    fs.Seek(selfSizePosition, SeekOrigin.Begin);
                    fs.WriteInt32((int)fs.Length);
                }

                return fs;
            }

            return null;
        }
    }
}