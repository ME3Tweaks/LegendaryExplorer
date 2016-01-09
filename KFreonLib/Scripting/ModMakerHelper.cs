using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BitConverter = KFreonLib.Misc.BitConverter;
using Gibbed.IO;
using KFreonLib.Debugging;
using KFreonLib.Textures;
using KFreonLib.MEDirectories;
using KFreonLib.PCCObjects;
using KFreonLib.GUI;
using CSharpImageLibrary.General;

namespace KFreonLib.Scripting
{
    /// <summary>
    /// Provides ModMaker and general .mod functions.
    /// </summary>
    public static class ModMaker
    {
        public static string exec;
        public static List<ModJob> JobList;

        // KFreon: Current mod data variable. So now data doesn't have to get written to disk several times.
        public static byte[] ModData { get; set; }


        /// <summary>
        /// Returns a texture .mod script built from template in exec folder, given pcc's, expID's, texture name, GameVersion, and some extra pathing stuff.
        /// </summary>
        /// <param name="ExecPath">Path to ME3Explorer exec folder.</param>
        /// <param name="pccs">PCC's to be affected by .mod.</param>
        /// <param name="ExpIDs">ExpID's of PCC's of texName to be affected.</param>
        /// <param name="texName">Name of texture to have the .mod edit.</param>
        /// <param name="WhichGame">Number of game texName belongs to.</param>
        /// <param name="pathBIOGame">BIOGame path of game in question.</param>
        /// <returns>Job script based on all the things.</returns>
        public static string GenerateTextureScript(string ExecPath, List<string> pccs, List<int> ExpIDs, string texName, int WhichGame, string pathBIOGame)
        {
            // KFreon: Get game independent path to remove from all pcc names, in order to make script computer independent.  (i.e. relative instead of absolute paths)
            //string MainPath = (WhichGame == 1) ? Path.GetDirectoryName(pathBIOGame) : pathBIOGame;
            // Heff: why were we removing the last directory for ME1 all over the place?
            string MainPath = pathBIOGame;

            // KFreon: Read template in from file
            string script;
            using (StreamReader scriptFile = new StreamReader(ExecPath + "TexScript.txt"))
            {
                script = scriptFile.ReadToEnd();
            }

            // KFreon: Set functions to run
            script = script.Replace("**m1**", "AddImage();");
            script = script.Replace("**m2**", "//No images to remove");
            script = script.Replace("**which**", WhichGame.ToString());

            // KFreon: Add pcc's to script
            string allpccs = "";
            foreach (string filename in pccs)
            {
                //string tempfile = Path.Combine(Path.GetFileName(Path.GetDirectoryName(filename)), Path.GetFileName(filename));
                string tempfile = (filename.ToLowerInvariant().Contains(MainPath.ToLowerInvariant())) ? filename.Remove(0, MainPath.Length + 1) : filename;

                //tempfile = ModGenerator.UpdatePathing(filename, tempfile);
                tempfile = tempfile.Replace("\\", "\\\\");
                allpccs += "pccs.Add(\"" + tempfile + "\");" + Environment.NewLine + "\t\t\t";
            }
            script = script.Replace("**m3**", allpccs);

            // KFreon: Add ExpID's to script
            string allIDs = "";
            foreach (int id in ExpIDs)
            {
                allIDs += "IDs.Add(" + id + ");" + Environment.NewLine + "\t\t\t";
            }
            script = script.Replace("**m4**", allIDs);

            // KFreon: Add texture name
            script = script.Replace("**m5**", texName);

            return script;
        }


        /// <summary>
        /// Sets up ModMaker static things, like the JobList.
        /// </summary>
        public static void Initialise()
        {
            JobList = new List<ModJob>();
            exec = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "exec") + "\\";
            BitConverter.IsLittleEndian = true;

            // KFreon: Clear the data cache and check that it exists
            while (true)
            {
                try
                {
                    if (File.Exists(exec + "ModData.cache"))
                        File.Delete(exec + "ModData.cache");
                    using (FileStream fs = new FileStream(exec + "ModData.cache", FileMode.CreateNew, FileAccess.Write))
                    {
                        fs.WriteByte(0x00);
                    }
                    break;
                }
                catch (IOException)
                { System.Threading.Thread.Sleep(50); }
            }
        }


        /// <summary>
        /// This is the object for storing .mod job data. 
        /// </summary>
        public class ModJob
        {
            public string Script = null;
            public string OriginalScript = null;
            public string Name = null;
            public string ObjectName
            {
                get
                {
                    string retval = Name;
                    if (Name.Contains("Binary"))
                        retval = Name.Split('"')[1].Trim();
                    else
                    {
                        if (Name.Contains(':'))
                            retval = Name.Split(':')[1].Trim();
                        else if (Name.Contains(" in "))
                            retval = Name.Substring(Name.IndexOf(" in ") + 4);
                    }
                    return retval;
                }
            }
            private uint Offset = 0;
            public uint Length = 0;
            public List<string> OrigPCCs = null;
            public List<string> PCCs = null;
            public List<int> ExpIDs = null;
            public List<int> OrigExpIDs = null;
            public string Texname = null;
            public string JobType = null;
            public int WhichGame = -1;
            public bool Valid
            {
                get
                {
                    return (ExpIDs.Count != 0 || PCCs.Count != 0 || !String.IsNullOrEmpty(Texname) || WhichGame != -1) && PCCs.Count == 0 ? false : !PCCs[0].Contains("sfar");
                }
            }


            // KFreon: Gets and sets data by storing it in a data cache file. Unfortunately, not everyone has 64 bit Windows.
            public byte[] data
            {
                get
                {
                    using (FileStream fs = new FileStream(exec + "ModData.cache", FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.Seek((long)Offset, SeekOrigin.Begin);
                        return fs.ReadBytes(Length);
                    }
                }
                set
                {
                    while (true)
                    {
                        try
                        {
                            if (value.Length > Length)
                            {
                                using (FileStream fs = new FileStream(exec + "ModData.cache", FileMode.Append, FileAccess.Write))
                                {
                                    Offset = (uint)fs.Position;
                                    Length = (uint)value.Length;
                                    fs.WriteBytes(value);
                                }
                            }
                            else
                            {
                                using (FileStream fs = new FileStream(exec + "ModData.cache", FileMode.Open, FileAccess.Write))
                                {
                                    fs.Seek(Offset, SeekOrigin.Begin);
                                    Length = (uint)value.Length;
                                    fs.WriteBytes(value);
                                }
                            }
                            break;
                        }
                        catch (IOException) { System.Threading.Thread.Sleep(50); }
                    }
                }
            }


            /// <summary>
            /// Constructor. Empty cos things get added dynamically.
            /// </summary>
            public ModJob()
            {
                // KFreon: Empty method
            }


            /// <summary>
            /// Write current job to fileStream.
            /// </summary>
            /// <param name="fs">FileStream to write to.</param>
            public void WriteJobToFile(FileStream fs)
            {
                // KFreon: Write job name
                //fs.WriteBytes(BitConverter.GetBytes(Name.Length));
                fs.WriteValueS32(Name.Length);
                foreach (char c in Name)
                    fs.WriteByte((byte)c);

                // KFreon: Write script
                //fs.WriteBytes(BitConverter.GetBytes(Script.Length));
                fs.WriteValueS32(Script.Length);
                foreach (char c in Script)
                    fs.WriteByte((byte)c);

                // KFreon: Write job data
                //fs.WriteBytes(BitConverter.GetBytes(data.Length));
                fs.WriteValueS32(data.Length);
                fs.WriteBytes(data);
            }


            /// <summary>
            /// Decides what current job is. If a texture job, returns TEXTURE, else OTHER. For now anyway, likely add mesh detection later.
            /// </summary>
            /// <returns></returns>
            public string DetectJobType()
            {
                string retval;
                if (Script.Contains("Texplorer"))
                    retval = "TEXTURE";
                else
                    retval = "OTHER";
                return retval;
            }

            /// <summary>
            /// Corrects incorrect DLC PCC paths from before ME3 DLC's were extracted.
            /// </summary>
            public void FixLegacyDLCPaths()
            {
                var folders = new List<String> ()
                {
                    "DLC_CON_APP01",
                    "DLC_CON_END",
                    "DLC_CON_GUN01",
                    "DLC_CON_GUN02",
                    "DLC_CON_MP1",
                    "DLC_CON_MP2",
                    "DLC_CON_MP3",
                    "DLC_CON_MP4",
                    "DLC_CON_MP5",
                    "DLC_EXP_Pack001",
                    "DLC_EXP_Pack002",
                    "DLC_EXP_Pack003",
                    "DLC_EXP_Pack003_Base",
                    "DLC_HEN_PR",
                    "DLC_OnlinePassHidCE"
                };

                List<string> lines = new List<string>(Script.Split('\n'));
                string processed = "";
                foreach (string line in lines) // Heff: This could probably be done with some nixe regex, but I'm tired and just want things to work.
                {
                    if (line.Contains("pccs.Add"))
                    {
                        var lineParts = line.Split('"');
                        var pathParts = lineParts[1].Replace("\\\\", "\\").Split(Path.DirectorySeparatorChar);
                        if (pathParts.Length > 1 && folders.Contains(pathParts[pathParts.Length - 2]))
                        {
                            //Heff: if the folder is one of the ME3 DLC's, fix it up:
                            var path = Path.GetDirectoryName(lineParts[1]);
                            var file = Path.GetFileName(lineParts[1]);
                            lineParts[1] = Path.Combine(path, "CookedPCConsole", file).Replace("\\", "\\\\");
                        }
                        processed += String.Join("\"", lineParts) + "\n";
                    }
                    else
                        processed += line + "\n";
                }
                Script = processed;
            }

            /// <summary>
            /// Gets details, like pcc's and expID's, from current script and sets local properties.
            /// Properties:
            ///     ExpID's, PCC's, Texname, WhichGame, JobType.
            /// </summary>
            public bool GetJobDetails(bool update, out bool versionConflict, int version)
            {
                JobType = DetectJobType();
                versionConflict = false;

                DebugOutput.PrintLn(String.Format("Job: {0}  type: {1}", Name, JobType));

                bool isTexture = JobType == "TEXTURE" ? true : false;
                ExpIDs = ModMaker.GetExpIDsFromScript(Script, isTexture);

                //Heff: adjust script paths for legacy ME3 DLC mods that references un-extracted DLC's
                FixLegacyDLCPaths();

                PCCs = ModMaker.GetPCCsFromScript(Script, isTexture);
                Texname = ModMaker.GetObjectNameFromScript(Script, isTexture);
                WhichGame = version == -1 ? ModMaker.GetGameVersionFromScript(Script, isTexture) : version;

                DebugOutput.PrintLn(String.Format("Job: {0} Detected game version: {1}  Detected texname: {2}", Name, WhichGame, Texname));

                // KFreon: Extra stuff to guess various things - NEW rev696 - from the WPF builds. Maybe unstable.
                #region Extra stuff from WPF
                // KFreon: Guess game version if required
                if (WhichGame == -1 && update)
                {
                    DebugOutput.PrintLn("Attempting to guess game version...");
                    /*int index = PCCs[0].IndexOf("Mass Effect");
                    char c = PCCs[0][index + 1];*/

                    DebugOutput.PrintLn("Found num PCCS: " + PCCs.Count);
                    WhichGame = GuessGame(PCCs);

                    if (WhichGame == -2)
                    {
                        versionConflict = true;
                        return false;
                    }
                }

                if (WhichGame == -1)
                {
                    DebugOutput.PrintLn("ERROR: No game found matching the mod files!\n" +
                        "Make sure that you have the proper game installed, and that the toolset has the correct path!\n" +
                        "If the mod targets DLC files, make sure that you have extracted all relevant DLC's.");

                    MessageBox.Show("No game found matching the mod files!\n" +
                        "Make sure that you have the proper game installed, and that the toolset has the correct path!\n" +
                        "If the mod targets DLC files, make sure that you have extracted all relevant DLC's.", "Error!");
                    return false;
                }
                else
                    DebugOutput.PrintLn("Guessed gameversion: " + WhichGame);

                // KFreon: Get ExpID's if required
                if (ExpIDs.Count == 0 && update)
                {
                    DebugOutput.PrintLn("Unable to find ExpID's in script. Attempting to find them manually. Game: " + WhichGame);

                    string biogame = MEDirectories.MEDirectories.GetDefaultBIOGame(WhichGame);
                    List<string> gameFiles = MEDirectories.MEDirectories.EnumerateGameFiles(WhichGame, biogame);
                    foreach (string pcc in PCCs)
                    {
                        //DebugOutput.PrintLn("Searching: " + pcc);
                        int index = -1;
                        if ((index = gameFiles.FindIndex(t => t.ToLower().Contains(pcc.ToLower()))) >= 0)
                        {
                            IPCCObject pccObject = PCCObjects.Creation.CreatePCCObject(gameFiles[index], WhichGame);
                            int count = 0;
                            foreach (IExportEntry export in pccObject.Exports)
                            {
                                //DebugOutput.PrintLn("Searching export: " + export.ObjectName);
                                if (export.ObjectName.Contains(Texname))
                                    ExpIDs.Add(count);
                                count++;
                            }
                        }
                    }
                    DebugOutput.PrintLn("Finished searching. Found: " + ExpIDs.Count + " matches.");
                }
                #endregion Extra Stuff from WPF

                OrigExpIDs = new List<int>(ExpIDs);
                OrigPCCs = new List<string>(PCCs);
                OriginalScript = Script;

                return true;
            }

            private int GuessGame(List<string> pccs)
            {
                int[] NumFounds = new int[3] { 0, 0, 0 };

                DebugOutput.PrintLn("Starting to guess game...");

                IEnumerable<string>[] GameFiles = new IEnumerable<string>[3];

                GameFiles[0] = ME1Directory.Files;
                if (GameFiles[0] == null)
                    DebugOutput.PrintLn("Could not find ME1 files!");
                else
                    DebugOutput.PrintLn("Got ME1 files...");

                GameFiles[1] = ME2Directory.Files;
                if (GameFiles[1] == null)
                    DebugOutput.PrintLn("Could not find ME2 files!");
                else
                    DebugOutput.PrintLn("Got ME2 files...");

                GameFiles[2] = ME3Directory.Files;
                if (GameFiles[2] == null)
                    DebugOutput.PrintLn("Could not find ME3 files!");
                else
                    DebugOutput.PrintLn("Got ME3 files...");
                //DebugOutput.PrintLn("List of gamefiles acquired with counts " + ME1Directory.Files.Count + "  " + ME2Directory.Files.Count + "  " + ME3Directory.Files.Count + ". Beginning search...");

                try
                {
                    int test = 0;
                    foreach (IEnumerable<string> gamefiles in GameFiles)
                    {
                        if (gamefiles == null)
                        {
                            DebugOutput.PrintLn("Gamefiles was null in GuessGame for ME" + test++ + 1);
                            continue;
                        }
                        int found = 0;
                        Parallel.ForEach(pccs, pcc =>
                        {
                            if (pcc == null)
                            {
                                DebugOutput.PrintLn("PCC was null in GuessGame related to ME" + test + 1);
                                return;
                            }
                            //DebugOutput.PrintLn("Searching for game in pcc: " + pcc);
                            string temp = pcc.Replace("\\\\", "\\");
                            if (gamefiles.FirstOrDefault(t => t.Contains(temp)) != null)
                                found++;
                        });

                        NumFounds[test++] = found;
                    }
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("Error guessing game: " + e.ToString());
                }


                DebugOutput.PrintLn("Finished guessing game.");
                if (NumFounds.Sum() == 0)
                    return -1;
                else
                {
                    int maxVal = NumFounds.Max();
                    var indices = Enumerable.Range(0, NumFounds.Count())
                        .Where(i => NumFounds[i] == maxVal)
                        .ToList();

                    if (indices.Count > 1)
                    {
                        DebugOutput.PrintLn("Could not guess game, files were present in more than one!");
                        return -2;
                    }
                    else
                        return indices[0] + 1;
                }
            }

            /// <summary>
            /// Returns a thumbnail sized bitmap of current job data (if texture).
            /// </summary>
            /// <returns>Thumbnail bitmap of current job data.</returns>
            public Image GenerateJobThumbnail()
            {
                DebugOutput.PrintLn("Generating thumbnail for: " + this.Name);

                Bitmap bmp = null;
                using (ImageEngineImage img = new ImageEngineImage(data, 64, false))
                    bmp = img.GetGDIBitmap(true, 64);

                return bmp;
            }


            /// <summary>
            /// Updates current job script to new format. Returns true if all bits to udpdate are found. NOTE that true does not mean updated script works.
            /// </summary>
            /// <param name="BIOGames">List of BIOGame paths for the games. MUST have only 3 elements. Each can be null if game files not found.</param>
            /// <param name="ExecFolder">Path to the ME3Explorer \exec\ folder.</param>
            /// <returns>True if all bits to update are found in current script.</returns>
            public bool UpdateJob(List<string> BIOGames, string ExecFolder)
            {
                bool retval = true;

                // KFreon: Ensure game target known
                /*if (WhichGame == -1)
                {
                    // KFreon: See if given pcc's exist on disk, and if so which game to they belong to. All basegame pcc's must be part of the same game.
                    int game = PCCObjects.Misc.SearchForPCC(PCCs, BIOGames, ExpIDs, ObjectName, JobType =="TEXTURE");

                    DebugOutput.PrintLn("Got game: " + game);

                    if (game == -1)
                    {
                        DebugOutput.PrintLn("Unable to find pcc's for job: " + Name);
                        retval = false;
                        WhichGame = 0;
                    }
                    else
                        WhichGame = game;
                }*/

                // KFreon: Return if already failed
                if (!retval)
                    return retval;


                // KFreon: If texture job, fix pcc pathing.
                
                //string pathBIOGame = WhichGame == 1 ? Path.GetDirectoryName(BIOGames[WhichGame - 1]) : BIOGames[WhichGame - 1];
                // Heff: Seems like we change the paths in so many places that it's bound to fuck up somewhere. Also VERY unfriendly to DLC mods, so chanigs this.
                string pathBIOGame = BIOGames[WhichGame - 1];

                /*if (WhichGame == 3)
                    pathBIOGame = Path.Combine(pathBIOGame, "CookedPCConsole");
                else
                    pathBIOGame = Path.Combine(pathBIOGame, "CookedPC");*/

                // KFreon: Deal with multiple files found during search
                List<string> multiples;
                List<int> MultiInds;

                // KFreon: Must be the same number of pcc's and expID's
                if (PCCs.Count != ExpIDs.Count)
                    DebugOutput.PrintLn("Job: " + Name + " has " + PCCs.Count + " PCC's and " + ExpIDs.Count + " ExpID's. Incorrect, so skipping...");
                else
                {
                    string script = "";
                    DebugOutput.PrintLn("Validating pccs");
                    OrigPCCs = ValidateGivenModPCCs(ref PCCs, ExpIDs, ObjectName, WhichGame, pathBIOGame, out multiples, out MultiInds, ref retval, JobType == "TEXTURE");

                    // KFreon: Texture job
                    if (JobType == "TEXTURE")
                    {
                        DebugOutput.PrintLn(Name + " is a texture mod.");

                        // KFreon: Get script for job
                        script = ModMaker.GenerateTextureScript(ExecFolder, OrigPCCs, ExpIDs, Texname, WhichGame, pathBIOGame);
                    }
                    else
                    {
                        // KFreon: HOPEFULLY a mesh mod...
                        DebugOutput.PrintLn(Name + " is a mesh mod. Hopefully...");

                        script = ModMaker.GenerateMeshScript(ExpIDs[0].ToString(), PCCs[0]);
                    }
                    Script = script;
                }

                return retval;
            }
        }

        private static List<string> ValidateGivenModPCCs(ref List<string> PCCs, List<int> ExpIDs, string ObjectName, int WhichGame, string pathBIOGame, out List<string> multiples, out List<int> MultiInds, ref bool retval, bool isTexture)
        {
            multiples = new List<string>();
            MultiInds = new List<int>();

            var gameFiles = new List<String>();
            switch (WhichGame)
            {
                case 1:
                    gameFiles = ME1Directory.Files;
                    break;
                case 2:
                    gameFiles = ME2Directory.Files;
                    break;
                case 3:
                    gameFiles = ME3Directory.Files;
                    break;
            }

            // KFreon: Fix pccs
            List<string> pccs = new List<string>();
            for (int i = 0; i < PCCs.Count; i++)
            {
                // KFreon: Test if pcc naming is correct. If not, fix.
                string pcc = PCCs[i];
                string test = pcc.Replace("\\\\", "\\");

                DebugOutput.PrintLn("About to begin validating");


                var result = gameFiles.Where(p => p.Contains(test)).DefaultIfEmpty("none").FirstOrDefault();
                if (result != "none")
                    test = result; // Heff: this can potentially be a problem for bad .mods that are for DLC's but only specify pcc name.
                else
                    DebugOutput.PrintLn("File not found in game files:" + test + ". Continuing...");

                if (test.Contains("#"))
                {
                    string[] parts = test.Split('#');
                    multiples.AddRange(parts);
                    for (int m = 0; m < parts.Length; m++)
                        MultiInds.Add(pccs.Count);
                }
                else if (test != "")
                {
                    string temp = test;
                    if (test.Contains(pathBIOGame))
                        temp = test.Remove(0, pathBIOGame.Length + 1);
                    if (!temp.Contains("\\\\"))
                        temp = temp.Replace("\\", "\\\\");
                    pccs.Add(temp);
                }
                else
                {
                    DebugOutput.PrintLn("Unable to find path for: " + pcc + ". This WILL cause errors later.");
                    pccs.Add(pcc);
                    retval = false;
                }
            }

            // KFreon: Deal with multiples
            if (multiples.Count > 0)
            {
                int found = 0;
                for (int i = 0; i < multiples.Count; i++)
                {
                    // TODO KFREON Need both multiples here
                    string pcc1 = multiples[i];
                    for (int j = i + 1; j < multiples.Count; j++)
                    {
                        string pcc2 = multiples[j];
                        if (pcc1 == pcc2)
                        {
                            found++;

                            if (!pcc1.Contains("\\\\"))
                                pcc1 = pcc1.Replace("\\", "\\\\");

                            pccs.Insert(MultiInds[i], pcc1);
                        }
                    }
                }

                // KFreon: Multiples still unresolved
                if (found != 0)
                {
                    // TODO KFreon add check to look at the given name fisrst. Might have something in it to clarify.
                    // TODO:  KFreon add selection ability
                    DebugOutput.PrintLn("MULTIPLES STILL UNRESOLVED!!!");
                }
            }
            PCCs = new List<string>(pccs);
            return new List<string>(pccs);
        }


        /// <summary>
        /// Create a texture ModJob from a tex2D with some pathing stuff.
        /// </summary>
        /// <param name="tex2D">Texture2D to build job from.</param>
        /// <param name="imgPath">Path of texture image to create job with.</param>
        /// <param name="WhichGame">Game to target.</param>
        /// <param name="pathBIOGame">Path to BIOGame of targeted game.</param>
        /// <returns>New ModJob based on provided image and Texture2D.</returns>
        public static ModJob CreateTextureJob(ITexture2D tex2D, string imgPath, int WhichGame, string pathBIOGame)
        {
            // KFreon: Get script
            string script = GenerateTextureScript(exec, tex2D.allPccs, tex2D.expIDs, tex2D.texName, WhichGame, pathBIOGame);
            ModJob job = new ModJob();
            job.Script = script;

            // KFreon: Get image data
            using (FileStream stream = new FileStream(imgPath, FileMode.Open))
            {
                FileInfo fs = new FileInfo(imgPath);
                byte[] buff = new byte[fs.Length];
                stream.Read(buff, 0, buff.Length);
                job.data = buff;
            }
            job.Name = (tex2D.Mips > 1 ? "Upscale (with MIP's): " : "Upscale: ") + tex2D.texName; 
            return job;
        }


        /// <summary>
        /// Writes the first invariable parts of a .mod (version, number of jobs) to FileStream.
        /// </summary>
        /// <param name="fs">FileStream to write to.</param>
        /// <param name="jobcount">Number of jobs. Exists because it's not always JobList.Count.</param>
        public static void WriteModHeader(FileStream fs, int jobcount)
        {
            // KFreon: Write version
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            fs.Seek(0, SeekOrigin.Begin);
            //byte[] length = BitConverter.GetBytes(version.Length);
            //fs.WriteBytes(length);
            fs.WriteValueS32(version.Length);
            foreach (char c in version)
                fs.WriteByte((byte)c);

            // KFreon: Write number of jobs to be included in this .mod
            fs.WriteValueS32(jobcount);

            DebugOutput.PrintLn("Version: " + version);
            DebugOutput.PrintLn("Number of jobs: " + jobcount);
        }


        /// <summary>
        /// Gets .mod build version, current toolset build version, and a boolean showing if they match.
        /// Returns: Current toolset version.
        /// Outs: .mod version, bool showing if matching.
        /// </summary>
        /// <param name="version">Version area extracted from .mod. May NOT be version if .mod is outdated.</param>
        /// <param name="newversion">OUT: .mod build version if applicable.</param>
        /// <param name="validVers">OUT: True if .mod supported by current toolset.</param>
        /// <returns>Current toolset build version.</returns>
        private static string GetVersion(string version, out string newversion, out bool validVers)
        {
            validVers = false;

            // KFreon: Get .mod version bits and check if valid version.
            List<string> modVersion = new List<string>(version.Split('.'));
            if (modVersion.Count == 4)
                validVers = true;


            List<string> ExecutingVersion = new List<string>(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.'));
            DebugOutput.PrintLn("Current Version: " + String.Join(".", ExecutingVersion) + "    Mod built with version: " + String.Join(".", modVersion));

            newversion = "";
            try
            {
                modVersion.RemoveAt(modVersion.Count - 1);
                modVersion.RemoveAt(modVersion.Count - 1);
                newversion = String.Join(".", modVersion);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Version parse failed: " + e.Message);
            }


            ExecutingVersion.RemoveAt(ExecutingVersion.Count - 1);
            ExecutingVersion.RemoveAt(ExecutingVersion.Count - 1);

            return String.Join(".", ExecutingVersion);
        }


        /// <summary>
        /// Loads a .mod file from given file and returns a nullable boolean (True, null, False).
        /// </summary>
        /// <param name="file">.mod file to load.</param>
        /// <param name="modCount">REF: Total number of jobs loaded.</param>
        /// <param name="progbar">ProgressBar to increment/change during method.</param>
        /// <param name="ExternalCall">If true, certain functions are disabled/automated.</param>
        /// <returns>True if update is to be done automatically, false if not, and null if user requests to stop loading .mod.</returns>
        public static bool? LoadDotMod(string file, ref int modCount, ToolStripProgressBar progbar, bool ExternalCall)
        {
            bool AutoUpdate = false;

            // KFreon: Load from file
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                // KFreon: Attempt to get version
                fs.Seek(0, SeekOrigin.Begin);
                int versionLength = fs.ReadValueS32();
                long countOffset = fs.Seek(0, SeekOrigin.Current);  // Just in case
                string version = "";
                int count = -1;
                string ExecutingVersion = null;
                bool validVersion = false;
                if (versionLength > 20)     // KFreon: Version is definitely wrong
                    ExecutingVersion = "";
                else
                {
                    // KFreon: Do version checking
                    for (int i = 0; i < versionLength; i++)
                        version += (char)fs.ReadByte();

                    // KFreon: Get Executing Version and check validity of read .mod version
                    string vers;
                    ExecutingVersion = GetVersion(version, out vers, out validVersion);
                    version = vers;

                    count = fs.ReadValueS32();

                    // KFreon: Check if update required
                    if (version != ExecutingVersion)
                    {
                        if (ExternalCall)
                            AutoUpdate = true;
                    }
                    else   // KFreon: Reset to null to signify success
                        ExecutingVersion = null;
                }

                
                // KFreon: Ask what to do about version
                if (ExecutingVersion != null) //&& !ExternalCall) // Heff: do we want to suppress this for external calls? should they always autoupdate?
                {                                                 // Seems better to keep it the current way, so that users get prompted if they load old .mods.
                    DialogResult dr = MessageBox.Show(Path.GetFileName(file) + " is old and unsupported by this version of ME3Explorer." + Environment.NewLine + "Click Yes to update .mod now, No to continue loading .mod, or Cancel to stop loading .mod", "Ancient .mod detected.", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (dr == System.Windows.Forms.DialogResult.Cancel)
                        return null;
                    else if (dr == System.Windows.Forms.DialogResult.Yes)
                        AutoUpdate = true;
                }
                /*else if (ExecutingVersion != null) // Heff: could use this for always updating if its an external call:
                    AutoUpdate = true;*/

                // KFreon: Reset stream position if necessary
                if (!validVersion)
                {
                    count = versionLength;
                    fs.Seek(countOffset, SeekOrigin.Begin);
                }

                // KFreon: Increment progress bar
                if (progbar != null)
                    progbar.GetCurrentParent().Invoke(new Action(() =>
                    {
                        progbar.Value = 0;
                        progbar.Maximum = count;
                    }));

                // KFreon: Read Data
                DebugOutput.PrintLn("Found " + count + " Jobs", true);
                modCount += count;
                for (int i = 0; i < count; i++)
                {
                    // KFreon: Read name
                    ModMaker.ModJob md = new ModMaker.ModJob();
                    int len = fs.ReadValueS32();
                    md.Name = "";
                    for (int j = 0; j < len; j++)
                        md.Name += (char)fs.ReadByte();

                    // KFreon: Read script
                    len = fs.ReadValueS32();
                    md.Script = "";
                    for (int j = 0; j < len; j++)
                        md.Script += (char)fs.ReadByte();

                    // KFreon: Read data
                    len = fs.ReadValueS32();
                    byte[] buff = fs.ReadBytes(len);
                    md.data = buff;
                    ModMaker.JobList.Add(md);
                    DebugOutput.PrintLn("Add Job \"" + md.Name + "\"", true);

                    if (progbar != null)
                        progbar.GetCurrentParent().Invoke(new Action(() => progbar.Increment(1)));
                }
            }
            return AutoUpdate;
        }


        /// <summary>
        /// Returns list of PCC's from job script.
        /// </summary>
        /// <param name="script">Script to search through.</param>
        /// <returns>List of PCC's found in script.</returns>
        public static List<string> GetPCCsFromScript(string script, bool isTexture)
        {
            List<string> lines = new List<string>(script.Split('\n'));
            List<string> pccs = new List<string>();

            // KFreon: Search through script lines
            if (isTexture)
            {
                foreach (string line in lines)
                    if (line.Contains("pccs."))   // KFreon: Only look at pcc lines.
                    {
                        string item = line.Split('"')[1];
                        pccs.Add(item);
                    }

                        
                    else if (line.Contains("public void RemoveTex()"))   // KFreon: Stop at the end of the first section.
                        break;
            }
            else
            {
                string pathing = "";
                foreach (string line in lines)
                {
                    if (line.Contains("string filename = "))
                    {
                        string[] parts = line.Split('=');
                        string pcc = parts[1].Split('"')[1];
                        if (pcc.Contains("sfar"))
                            pcc = "TOO OLD TO FIX";
                        pathing += pcc;
                    }
                    else if (line.Contains("string pathtarget = "))
                    {
                        string[] parts = line.Split('"');
                        string path = "";
                        if (parts.Count() > 1)
                            path = parts[1];
                        pathing = path + pathing;
                    }
                }
                if (pathing != "")
                {
                    pathing = pathing.TrimStart("\\".ToCharArray());
                    /*int ind = pathing.IndexOf("DLC\\");
                    if (ind != -1)
                        pathing = pathing.Substring(ind + 5);*/

                    pccs.Add(pathing); 
                }
                                       
            }
            
            return pccs;
        }


        /// <summary>
        /// Returns list of ExpID's from job script.
        /// </summary>
        /// <param name="script">Script to search through.</param>
        /// <returns>List of ExpID's found in script.</returns>
        public static List<int> GetExpIDsFromScript(string script, bool isTexture)
        {
            List<string> lines = new List<string>(script.Split('\n'));
            List<int> ids = new List<int>();

            // KFreon: Search through script lines.
            if (isTexture)
            {
                foreach (string line in lines)
                    if (line.Contains("IDs."))   // KFreon: Only look at ExpID lines.
                        ids.Add(Int32.Parse(line.Split('(')[1].Split(')')[0]));
                    else if (line.Contains("public void RemoveTex()"))   // KFreon: Stop at end of first section.
                        break;
            }
            else
            {
                foreach (string line in lines)
                {
                    if (line.Contains("int objidx = "))
                    {
                        string[] parts = line.Split('=');
                        string number = parts[1].Substring(1, parts[1].Length - 3);
                        ids.Add(Int32.Parse(number));
                        break;
                    }
                }
            }
            
            return ids;
        }


        /// <summary>
        /// Returns name of texture from job script.
        /// </summary>
        /// <param name="script">Script to search through.</param>
        /// <returns>Texture name found in script.</returns>
        public static string GetObjectNameFromScript(string script, bool isTexture)
        {
            List<string> lines = new List<string>(script.Split('\n'));
            string texname = "";

            // KFreon: Search through lines.
            if (isTexture)
            {
                foreach (string line in lines)
                    if (line.Contains("tex."))  // KFreon: Look for texture name
                        return line.Split('"')[1];
            }
            else
            {
                texname = "Binary/Mesh";
            }
            
            return texname;
        }


        /// <summary>
        /// Return which game script is targeting.
        /// </summary>
        /// <param name="script">Script to search through.</param>
        /// <returns>Game version.</returns>
        public static int GetGameVersionFromScript(string script, bool isTexture)
        {
            List<string> lines = new List<string>(script.Split('\n'));
            int retval = -1;

            // KFreon: Search through lines.
            if (isTexture)
            {
                foreach (string line in lines)
                {
                    // KFreon: Look for Texplorer line, which contains game target.
                    if (line.Contains("Texplorer2("))
                    {
                        string[] parts = line.Split(',');
                        int test = -1;
                        if (parts.Length > 1)
                            if (int.TryParse(parts[1].Split(')')[0], out test))
                                retval = test;
                    }
                }
            }
            else
                retval = 3;
            
            return retval;
        }

        public static void AddJob(ITexture2D tex2D, string ReplacingImage, int WhichGame, string pathBIOGame)
        {
            if (JobList.Count == 0)
                Initialise();
            ModJob job = ModMaker.CreateTextureJob(tex2D, ReplacingImage, WhichGame, pathBIOGame);
            JobList.Add(job);
        }

        public static string GenerateMeshScript(string expID, string pcc)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");
            template = template.Replace("**m1**", expID);
            template = template.Replace("**m2**", pcc);
            //template = template.Replace("**PATH**", pcc.ToUpperInvariant().Contains("DLC_") ? "ME3Directory.DLCPath" : "ME3Directory.cookedPath");
            return template;
        }

        public static ModJob GenerateMeshModJob(string newfile, int expID, string pccname, byte[] data)
        {
            KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();

            // KFreon: Get replacing data
            byte[] buff = data;
            if (data == null)
            {
                FileStream fs = new FileStream(newfile, FileMode.Open, FileAccess.Read);
                buff = new byte[fs.Length];
                int cnt;
                int sum = 0;
                while ((cnt = fs.Read(buff, sum, buff.Length - sum)) > 0) sum += cnt;
                fs.Close();
            }
            

            string currfile = Path.GetFileName(pccname);
            mj.data = buff;
            mj.Name = "Binary Replacement for file \"" + currfile + "\" in Object #" + expID + " with " + buff.Length + " bytes of data";
            mj.Script = GenerateMeshScript(expID.ToString(), currfile);
            return mj;
        }
    }
}
