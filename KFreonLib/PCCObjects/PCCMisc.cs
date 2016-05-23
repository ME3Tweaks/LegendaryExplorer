using KFreonLib.Debugging;
using KFreonLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KFreonLib.Textures;
using UsefulThings;

namespace KFreonLib.PCCObjects
{
    /// <summary>
    /// Provides PCC creation methods.
    /// </summary>
    public static class Creation
    {
        /// <summary>
        /// Creates a PCCObject from a file.
        /// </summary>
        /// <param name="file">PCC file to create object from.</param>
        /// <param name="WhichGame">Game version.</param>
        /// <returns>IPCCObject from file.</returns>
        public static IPCCObject CreatePCCObject(string file, int WhichGame)
        {
            IPCCObject pcc;

            // KFreon: Use different methods for each game.
            if (WhichGame == 1)
                pcc = new ME1PCCObject(file);
            else if (WhichGame == 2)
                pcc = new ME2PCCObject(file);
            else if (WhichGame == 3)
                pcc = new ME3PCCObject(file);
            else
            {
                DebugOutput.PrintLn("WHAT HAVE YOU DONE!!   PCCObject creation failed!");
                return null;
            }
            return pcc;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="stream"></param>
        /// <param name="WhichGame"></param>
        /// <returns></returns>
        public static IPCCObject CreatePCCObject(string file, MemoryStream stream, int WhichGame)
        {
            IPCCObject pcc;
            if (WhichGame == 1)
                pcc = new ME1PCCObject(file, stream);
            else if (WhichGame == 2)
                pcc = new ME2PCCObject(file, stream);
            else if (WhichGame == 3)
                pcc = new ME3PCCObject(file, stream);
            else
            {
                DebugOutput.PrintLn("WHAT HAVE YOU DONE!!   PCCObject creation failed!");
                return null;
            }
            return pcc;
        }
    }


    /// <summary>
    /// Provides miscellaneous methods related to PCC's.
    /// </summary>
    public static class Misc
    {
        static Dictionary<string, List<string>> ValidFiles = new Dictionary<string, List<string>>();

        /// <summary>
        /// Reorders files in a Tex2D object so the first file has a proper arcname variable. THANKS PRINCE.
        /// </summary>
        /// <param name="files">List of PCC's to check.</param>
        /// <param name="expIDs">List of ExpID's for given PCC's.</param>
        /// <param name="pathBIOGame">Path to BIOGame folder.</param>
        /// <param name="GameVersion">Game in question.</param>
        public static bool ReorderFiles(ref List<string> files, ref List<int> expIDs, string pathBIOGame, int GameVersion)
        {
            // KFreon: Loop over all files to find one that has the correct arcName property
            for (int i = 0; i < files.Count; i++)
            {
                ITexture2D tex = Creation.CreatePCCObject(files[i], GameVersion).CreateTexture2D(expIDs[i], pathBIOGame);
                string arc = tex.arcName;
                if (i == 0 && (arc != "None" && !String.IsNullOrEmpty(arc)))
                    return true;
                else if (arc != "None" && !String.IsNullOrEmpty(arc))
                {
                    string file = files[i];
                    int id = expIDs[i];
                    files.RemoveAt(i);
                    expIDs.RemoveAt(i);
                    files.Insert(0, file);
                    expIDs.Insert(0, id);
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Returns true if given ClassName is a valid texture class.
        /// </summary>
        /// <param name="ClassName">ClassName to validate.</param>
        /// <returns>True if valid texture class.</returns>
        public static bool ValidTexClass(string ClassName)
        {
            return ClassName == "Texture2D" || ClassName == "LightMapTexture2D" || ClassName == "TextureFlipBook";
        }


        /// <summary>
        /// Searches for PCC by name in a certain game specified by PathInclDLC. Can also use expID to narrow search if required. Return name of PCC, empty if not found.
        /// </summary>
        /// <param name="pccname">Name of PCC to search for.</param>
        /// <param name="PathInclDLC">Path encasing BIOGame and DLC. i.e. Parent folder containing folders BIOGame and DLC.</param>
        /// <param name="expID">ExpID of an object inside the desired PCC.</param>
        /// <returns>Name of PCC if found, else empty string.</returns>
        /*public static string SearchForPCC(string pccname, string PathInclDLC, int expID, string objectName, bool isTexture, int whichgame)
        {
            // KFreon: Lowercase PCC name
            string name = Path.GetFileName(pccname).ToLowerInvariant();
            List<string> searchResults = new List<string>();

            // KFreon: Create lists of valid files if necessary
            lock (ValidFiles)
                if (!ValidFiles.ContainsKey(PathInclDLC.ToLowerInvariant()))
                {
                    List<string> temp = Directory.EnumerateFiles(PathInclDLC, "*", SearchOption.AllDirectories).Where(pcc => pcc.EndsWith(".pcc", true, null) || pcc.EndsWith(".u", true, null) || pcc.EndsWith(".sfm", true, null) || pcc.EndsWith(".upk", true, null)).ToList();
                    ValidFiles.Add(PathInclDLC.ToLowerInvariant(), temp);
                }

            // KFreon: Attempt to find PCC
            searchResults.AddRange(ValidFiles[PathInclDLC.ToLowerInvariant()].Where(pcc => pcc.ToLowerInvariant().Contains(name)));

            string retval = "";

            // KFreon: Do special stuff if multiple files found
            if (searchResults.Count > 1)
            {
                // KFreon: If expID given, use it to try to discern correct pcc
                if (expID != -1)
                {
                    List<string> temp = new List<string>();
                    foreach (string file in searchResults)
                    {
                        // KFreon: Only work on stuff if file is correct given the provided information
                        if (CheckSearchedTexture(file, expID, objectName, whichgame))
                        {
                            temp.Add(file);

                            // KFreon: See if DLC is relevent
                            string dlcname = KFreonLib.Misc.Methods.GetDLCNameFromPath(pccname);
                            if (dlcname != "")
                            {
                                temp.Clear();
                                temp.Add(file);
                                break;
                            }
                        }
                    }
                    if (temp.Count == 1)
                        retval = temp[0];
                    else if (temp.Count > 1)
                    {
                        // KFreon: If still multiple files found, break things.
                        using (SelectionForm sf = new SelectionForm(temp, "LET ME KNOW ABOUT THIS PLEASE!!", "Oh dang. More work for me.", false))
                            sf.Show();

                        retval = String.Join("#", temp.ToArray());
                        DebugOutput.PrintLn("Multiple pccs found for: " + pccname);
                        foreach (string item in temp)
                            DebugOutput.PrintLn(item);
                        DebugOutput.PrintLn();
                    }
                }
            }
            else if (searchResults.Count == 1)
            {
                if (isTexture && CheckSearchedTexture(searchResults[0], expID, objectName, whichgame) || !isTexture)
                    retval = searchResults[0];
            }

            return retval;
        }*/

        private static bool CheckSearchedTexture(string file, int expID, string objectName, int whichgame)
        {
            DebugOutput.PrintLn("Checking texture");

            
            // KFreon: Test if this files' expID is the one we want
            IPCCObject pcc = Creation.CreatePCCObject(file, whichgame);

            // KFreon: First check if there's enough expID's in current file, then if we're looking at a texture in current file
            if (pcc.Exports.Count >= expID && pcc.Exports[expID].ValidTextureClass())
            {
                bool nametest = (objectName == null ? true : pcc.Exports[expID].ObjectName.ToLowerInvariant().Contains(objectName.ToLowerInvariant()));
                return nametest;
            }
            return false;
        }


        /// <summary>
        /// Search for PCC's in all games. Returns number of game PCC's belongs to.
        /// </summary>
        /// <param name="pccs">PCC's to search for.</param>
        /// <param name="pathBIOs">List of BIOGame paths, can contain nulls for non-existent games. MUST have 3 elements.</param>
        /// <param name="expIDs">List of ExpID's matching the provided PCC's. MUST have the same number of elements as PCC's.</param>
        /// <returns></returns>
        /*public static int SearchForPCC(List<string> pccs, List<string> pathBIOs, List<int> expIDs, string objectName, bool isTexture)
        {
            int game = -1;
            List<string> HerpGames = new List<string>();

            // KFreon: Search all 3 games
            for (int i = 0; i < 3; i++)
            {
                // KFreon: keep track of any multiple search results, index of this event, and the game targeted at the time
                List<string> multiples = new List<string>();
                List<int> MultiIndicies = new List<int>();   
                List<int> MultiGames = new List<int>();

                // KFreon: Skip if game not found
                if (pathBIOs[i] == "")
                    continue;

                // KFreon: Search current game for all given pccs
                List<int> games = new List<int>();
                for (int j = 0; j < pccs.Count; j++)
                {
                    string res = SearchForPCC(pccs[j], (i == 0) ? Path.GetDirectoryName(pathBIOs[i]) : pathBIOs[i], expIDs[j], objectName, isTexture, wh);

                    if (res.Contains("#"))
                    {
                        multiples.AddRange(res.Split('#'));
                        MultiIndicies.Add(games.Count);
                        MultiGames.Add(i + 1);
                    }

                    if (res != "")  // KFreon: If pcc found
                        games.Add(i + 1);
                    else
                        games.Add(-1);
                }

                // KFreon: Deal with multiples
                if (multiples.Count > 0)
                {
                    // KFreon: See if sets of multiples are needed. i.e. a pair of files are both being modified (PROBABLY most common)
                    bool found = false;
                    for (int k = 0; k < multiples.Count; k++)
                    {
                        if (found)
                            break;

                        string pcc1 = multiples[k];
                        for (int j = k + 1; j < multiples.Count; j++)
                        {
                            string pcc2 = multiples[j];
                            if (pcc1 == pcc2)
                            {
                                found = true;
                                games[MultiIndicies[k]] = MultiGames[k];            
                            }
                        }
                    }

                    // KFreon: If multiples still unresolved
                    if (!found)
                    {
                        Console.WriteLine();
                        using (SelectionForm sf = new SelectionForm(multiples, "LET ME KNOW ABOUT THIS PLEASE!!", "Oh dang. More work for me.", false))
                            sf.Show();
                        // show selection
                        // TODO: KFREON add selection ability
                    }
                }


                // KFreon: Look at results and decide what to do
                bool correct = !games.Contains(-1);
                game = correct ? games.First(gam => gam != -1) : -1;

                if (!correct && game != -1)
                    HerpGames.Add(game.ToString() + "|" + games.Where(gm => gm != -1));
                else if (correct && game != -1)
                {
                    HerpGames.Clear();
                    break;
                }
            }

            if (HerpGames.Count != 0)
            {
                int ind = -1;
                int max = -1;

                // KFreon: Find game with highest number of matches
                for (int i = 0; i < HerpGames.Count; i++)
                {
                    string[] parts = HerpGames[0].Split('|');
                    int num = int.Parse(parts.Last());

                    if (num > max)
                    {
                        max = num;
                        ind = i;
                    }
                }

                game = int.Parse(HerpGames[ind][0] + "");
            }
            return game;
        }*/
    }
}
