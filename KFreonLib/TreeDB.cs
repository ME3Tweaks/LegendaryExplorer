using KFreonLib.GUI;
using KFreonLib.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UsefulThings;

namespace KFreonLib
{
    public class TreeDB
    {
        #region Properties
        public bool AdvancedFeatures { get; private set; }
        public int TexCount
        {
            get
            {
                lock (Sync)
                    if (Texes != null)
                        return Texes.Count;
                    else
                        return -1;
            }
        }

        public int numPCCs
        {
            get
            {
                lock (Sync)
                    return pccs.Count;
            }
        }

        public int NodeCount
        {
            get
            {
                lock (Sync)
                    return nodeList.Count;
            }
        }
        #endregion

        #region Globals
        public int numDLCFiles
        {
            get
            {
                return pccs.Where(t => t.Contains("DLC_")).Count();
            }
        }
        List<TreeTexInfo> Texes = new List<TreeTexInfo>();
        List<string> pccs = new List<string>();
        List<myTreeNode> nodeList = new List<myTreeNode>();
        TreeView TexplorerTreeView;
        private readonly object Sync = new object();
        public string TreePath = "";
        int GameVersion = 0;
        public Task TreeAddTask;
        List<List<object>> TreeTempTexes = new List<List<object>>();
        string pathBIOGame;
        #endregion


        public TreeDB(List<string> given, ref TreeView textree, int WhichGame, string pathbio)
        {
            setup(ref textree, WhichGame, pathbio);
            pccs.AddRange(given);
        }

        public TreeDB(ref TreeView textree, int WhichGame, string pathbio)
        {
            setup(ref textree, WhichGame, pathbio);
        }

        private void setup(ref TreeView textree, int WhichGame, string pathbio)
        {
            TexplorerTreeView = textree;
            GameVersion = WhichGame;
            pathBIOGame = pathbio;
            TreeAddTask = new Task(() => PerformTreeComparison());
        }


        public List<myTreeNode> GetNodesAsList()
        {
            lock (Sync)
                return nodeList;
        }

        public List<string> GetPCCsAsList()
        {
            lock (Sync)
                return pccs;
        }

        public bool AddPCCs(List<string> files)
        {
            lock (Sync)
                if (pccs != null)
                    pccs.AddRange(files);
                else
                    return false;
            return true;
        }

        public void Clear(bool complete = false)
        {
            lock (Sync)
            {
                Texes.Clear();
                if (TexplorerTreeView != null)
                    TexplorerTreeView.Nodes.Clear();
                nodeList.Clear();

                if (complete)
                    pccs.Clear();
            }
        }

        public TreeTexInfo GetTex(int index)
        {
            lock (Sync)
            {
                if (index < 0 || index >= TexCount)
                    return null;
                else
                    return Texes[index];
            }
        }

        public bool ReplaceTex(int index, TreeTexInfo tex)
        {
            lock (Sync)
            {
                if (index < 0 || index >= TexCount)
                    return false;
                else
                {
                    Texes[index] = tex;
                    return true;
                }
            }
        }

        private TreeTexInfo Contains(TreeTexInfo tex, string PackName, string filename)
        {
            for (int i = 0; i < TexCount; i++)
                if (Compare(tex, i, PackName, filename))
                    return Texes[i];

            return null;
        }

        private bool Compare(TreeTexInfo tex, int i, string PackName, string filename)
        {
            if (tex.TexName == Texes[i].TexName)
                if ((tex.Hash == 0 && tex.tfcOffset == Texes[i].tfcOffset) || (tex.Hash != 0 && tex.Hash == Texes[i].Hash))
                {
                    if (tex.GameVersion == 1 && (tex.Package.ToLowerInvariant() == PackName.ToLowerInvariant() || Path.GetFileNameWithoutExtension(filename).ToLowerInvariant().Contains(tex.Package.ToLowerInvariant())))
                        return true;
                    else if (tex.GameVersion != 1)
                        return true;
                    else
                        return false;
                }
            return false;
        }

        public List<TreeTexInfo> GetTreeAsList()
        {
            return new List<TreeTexInfo>(Texes);
        }


        public void BlindAddTex(TreeTexInfo tex)
        {
            lock (Sync)
            {
                tex.TreeInd = TexCount;
                Texes.Add(tex);
            }
        }

        public myTreeNode GetNode(int index)
        {
            lock (Sync)
            {
                if (index < 0 || index >= nodeList.Count)
                    return null;
                else
                    return nodeList[index];
            }
        }

        public void AddNode(myTreeNode node)
        {
            nodeList.Add(node);
        }


        public string GetPCC(int index)
        {
            if (index < 0 || index >= numPCCs)
                return "";
            else
                return pccs[index];
        }


        /// <summary>
        /// Adds texture to tree with duplicate checks
        /// </summary>
        /// <param name="tex">Texture to add</param>
        public void AddTex(TreeTexInfo tex, string PackName, string filename)
        {
            lock (Sync)
            {
                List<object> tmp = new List<object>();
                tmp.Add(tex);
                tmp.Add(PackName);
                tmp.Add(filename);
                TreeTempTexes.Add(tmp);


                // KFreon: Start again if finished
                if (TreeAddTask.Status == TaskStatus.RanToCompletion)
                {
                    TreeAddTask = null;
                    TreeAddTask = new Task(() => PerformTreeComparison());
                    TreeAddTask.Start();
                }
                else if (TreeAddTask.Status == TaskStatus.Created)   // KFreon: Start if never started before
                    TreeAddTask.Start();
            }
        }

        public void PerformTreeComparison()
        {
            int count = -1;

            // KFreon: Get number of textures waiting to be processed
            lock (Sync)
                count = TreeTempTexes.Count;

            // KFreon: Add each element with duplicate checking
            while (count > 0)
            {
                // KFreon: Get elements from list as added previously
                List<object> item;
                lock (Sync)
                    item = TreeTempTexes[0];

                TreeTexInfo tex = (TreeTexInfo)item[0];
                string PackName = (string)item[1];
                string filename = (string)item[2];

                // KFreon: Add to list if not a duplicate
                TreeTexInfo temp = null;
                if ((temp = Contains(tex, PackName, filename)) != null)
                {
                    temp.Update(tex, pathBIOGame);

                    if (GameVersion == 2 && !temp.ValidFirstPCC && tex.ValidFirstPCC)
                    {
                        // KFreon: Get index of new first file in 'old' list
                        int index = temp.Files.IndexOf(tex.Files[0]);

                        // KFreon: Move pcc
                        var element = temp.Files.Pop(index);
                        temp.Files.Insert(0, element);

                        // KFreon: Move expid
                        var exp = temp.ExpIDs.Pop(index);
                        temp.ExpIDs.Insert(0, exp);

                        // KFreon: Update originals lists
                        temp.OriginalExpIDs = new List<int>(temp.ExpIDs);
                        temp.OriginalFiles = new List<string>(temp.Files);

						temp.ValidFirstPCC = true;
                    }
                }
                else
                    BlindAddTex(tex);

                // KFreon: Remove item from temp list
                lock (Sync)
                {
                    TreeTempTexes.RemoveAt(0);
                    count = TreeTempTexes.Count;
                }
            }
        }


        public bool ReadFromFile(string TreeName, string mainpath, string thumbpath, out int status, Form invokeObject = null)
        {
            status = 0;
            if (!File.Exists(TreeName))
                return false;

            TreePath = TreeName;

            try
            {
                using (FileStream fs = new FileStream(TreeName, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader bin = new BinaryReader(fs))
                    {
                        int numthings = bin.ReadInt32();
                        if (numthings == 1991)
                        {
                            AdvancedFeatures = true;
                            numthings = bin.ReadInt32();
                            Debugging.DebugOutput.PrintLn("Advanced ME" + GameVersion + " Tree features detected.");
                        }
                        else
                            Debugging.DebugOutput.PrintLn("Advanced ME" + GameVersion + " Tree features disabled.");
                            
                        for (int i = 0; i < numthings; i++)
                        {
                            TreeTexInfo tempStruct = new TreeTexInfo();
                            int temp = bin.ReadInt32();
                            char[] tempChar = bin.ReadChars(temp);
                            tempStruct.TexName = new string(tempChar);
                            tempStruct.Hash = bin.ReadUInt32();
                            tempChar = bin.ReadChars(bin.ReadInt32());
                            tempStruct.FullPackage = new string(tempChar);
                            tempChar = bin.ReadChars(bin.ReadInt32());

                            if (AdvancedFeatures)
                            {
                                string thum = new string(tempChar);
                                tempStruct.ThumbnailPath = thum != null ? Path.Combine(thumbpath, thum) : null;
                            }

                            tempStruct.NumMips = bin.ReadInt32();
                            tempStruct.Format = "";
                            int formatlen = bin.ReadInt32();
                            tempChar = bin.ReadChars(formatlen);
                            tempStruct.Format = new string(tempChar);

                            int numFiles = bin.ReadInt32();
                            tempStruct.Files = new List<string>();
                            for (int j = 0; j < numFiles; j++)
                            {
                                tempChar = bin.ReadChars(bin.ReadInt32());
                                string tempStr = new string(tempChar);
                                tempStruct.Files.Add(Path.Combine(mainpath, tempStr));
                            }

                            tempStruct.ExpIDs = new List<int>();
                            tempStruct.TriedThumbUpdate = false;
                            for (int j = 0; j < numFiles; j++)
                                tempStruct.ExpIDs.Add(bin.ReadInt32());

                            tempStruct.OriginalFiles = new List<string>(tempStruct.Files);
                            tempStruct.OriginalExpIDs = new List<int>(tempStruct.ExpIDs);
                            BlindAddTex(tempStruct);
                        }
                    }
                }
            }
            catch
            {
                if (invokeObject != null)
                {
                    int temp = status;
                    invokeObject.Invoke(new Action(() =>
                    {
                        if (MessageBox.Show("Tree is corrupted or wrong tree loaded  :(" + Environment.NewLine + "Do you want to build a new tree?", "Mission Failure.", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == System.Windows.Forms.DialogResult.Yes)
                        {
                            File.Delete(TreeName);
                            temp = 1;
                        }
                        else
                            temp = 2;
                    }));
                    status = temp;
                }
                else
                    status = 2;
                return false;
            }
            return true;
        }


        public void WriteToFile(string treeName, string mainpath)
        {
            TreePath = treeName;
            using (FileStream fs = new FileStream(treeName, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter bin = new BinaryWriter(fs))
                {
                    bin.Write(1991); // KFreon: Marker for advanced features
                    bin.Write(TexCount);
                    for (int i = 0; i < TexCount; i++)
                    {
                        TreeTexInfo tex = GetTex(i);
                        bin.Write(tex.TexName.Length);
                        bin.Write(tex.TexName.ToCharArray());

                        bin.Write(tex.Hash);

                        string fullpackage = tex.FullPackage;
                        if (String.IsNullOrEmpty(fullpackage))
                            fullpackage = "Base Package";
                        bin.Write(fullpackage.Length);
                        bin.Write(fullpackage.ToCharArray());

                        string thumbpath = tex.ThumbnailPath != null ? tex.ThumbnailPath.Split('\\').Last() : "placeholder.ico";
                        bin.Write(thumbpath.Length);
                        bin.Write(thumbpath.ToCharArray());

                        bin.Write(tex.NumMips);
                        bin.Write(tex.Format.Length);
                        bin.Write(tex.Format.ToCharArray());
                        bin.Write(tex.Files.Count);

                        /*if (GameVersion != 1)
                            KFreonLib.PCCObjects.Misc.ReorderFiles(ref tex.Files, ref tex.ExpIDs, Path.Combine(mainpath, "BIOGame"), GameVersion);*/


                        foreach (string file in tex.Files)
                        {
                            string tempfile = file;
                            tempfile = tempfile.Remove(0, mainpath.Length + 1);

                            bin.Write(tempfile.Length);
                            bin.Write(tempfile.ToCharArray());
                        }

                        foreach (int expid in tex.ExpIDs)
                            bin.Write(expid);
                    }
                }
            }
        }

        public TreeDB Clone()
        {
            TreeDB newtree = new TreeDB(pccs, ref TexplorerTreeView, GameVersion, pathBIOGame);
            newtree.AdvancedFeatures = AdvancedFeatures;
            newtree.Texes = new List<TreeTexInfo>(Texes);
            newtree.nodeList = new List<myTreeNode>(nodeList);
            newtree.TreePath = TreePath;
            return newtree;
        }
    }
}
