using AmaroK86.ImageFormat;
using KFreonLib;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Textures = KFreonLib.Textures;
using PCCObjects = KFreonLib.PCCObjects;
using Helpers = KFreonLib.Helpers;
using KFreonLib.GUI;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using TreeTexInfo = KFreonLib.Textures.TreeTexInfo;
using System.Collections.Concurrent;
using System.Reflection;
using CSharpImageLibrary.General;
using System.Text;
using UsefulThings;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer
{
    public partial class Texplorer2 : Form
    {
        BackBone backbone;
        int WhichGame = 0;
        bool noWindow = false;
        myTreeNode t;
        static readonly object TreeLock = new object();
        MEDirectories MEExDirecs = new MEDirectories();

        List<ToolTip> tooltips = new List<ToolTip>();

        string pathBIOGame
        {
            get
            {
                return MEExDirecs.PathBIOGame;
            }
        }

        string pathCooked
        {
            get
            {
                return MEExDirecs.pathCooked;
            }
        }

        string ExecFolder
        {
            get
            {
                return MEExDirecs.ExecFolder;
            }
        }

        string DLCPath
        {
            get
            {
                return MEExDirecs.DLCPath;
            }
        }

        string ThumbnailPath
        {
            get
            {
                return MEExDirecs.ThumbnailPath;
            }
        }
        string WindowTitle;
        public TreeDB Tree;
        int NumThreads = 4;
        int PropertiesWidth = 0;
        KFreonSearchForm Search;
        CancellationTokenSource cts = new CancellationTokenSource();
        List<int> ChangedTextures = new List<int>();
        bool ModMakerMode = false;
        string tocpath = "";
        bool DisableContext = false;
        bool TPFMode = false;
        TextUpdater StatusUpdater;
        ProgressBarChanger ProgBarUpdater;
        Gooey gooey;
        bool cancelling = true;

        Dictionary<string, Bitmap> Previews = new Dictionary<string, Bitmap>();


        private void SaveProperties()
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to save settings in Texplorer: " + e);
            }
        }


        public Texplorer2(bool nowindow = false, int which = -1)
        {
            if (!nowindow)
                InitializeComponent();
            KFreonTPFTools3.UpgradeSettings();

            // KFreon: Set number of threads if necessary
            if (Properties.Settings.Default.NumThreads == 0)
            {
                Properties.Settings.Default.NumThreads = KFreonLib.Misc.Methods.SetNumThreads(false);
                SaveProperties();
            }

            WhichGame = (which == -1) ? Properties.Settings.Default.TexplorerGameVersion : which;
            MEExDirecs.WhichGame = WhichGame;

            StatusUpdater = new TextUpdater(StatusLabel, StatusStrip);
            ProgBarUpdater = new ProgressBarChanger(StatusStrip, MainProgressBar);
            gooey = new Gooey(MainTreeView);
            SetupGUI();

            DebugOutput.StartDebugger("Texplorer 2.0");
            if (!nowindow)
            {
                ChangeButton.Text = "Modding ME" + WhichGame;
                WindowTitle = "Texplorer 2.0:  " + "ME" + WhichGame;
                this.Text = WindowTitle;
                DisappearPictureBox();
                ResetSearchBox();
            }
            backbone = new BackBone(() =>
            {
                Console.WriteLine("changing gui to false");
                gooey.ChangeState(false);
                return true;
            }, () =>
            {
                Console.WriteLine("Changeing gui to true");
                gooey.ChangeState(true);
                return true;
            });

            noWindow = nowindow;


            // KFreon: Get number of threads to use
            NumThreads = KFreonLib.Misc.Methods.SetNumThreads(false);

            DebugOutput.PrintLn("Using: " + NumThreads + " threads.");

            // KFreon: Setup paths
            // KFreon: Check game states DONE HERE COS TREE NEEDS THESE THINGS
            CheckGameStates();


            // KFreon: Check path exists
            bool exists = true;

            // KFreon: Load tree only if necessary
            if (!nowindow && exists)
                backbone.AddToBackBone(b =>
                {
                    BeginLoadingTree();
                    return true;
                });

            // KFreon: Display version
            if (!nowindow)
                VersionLabel.Text = "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void CheckGameStates()
        {
            DebugOutput.PrintLn("Checking if games are present...");
            List<string> messages = null;
            List<bool> states = KFreonLib.Misc.Methods.CheckGameState(MEExDirecs, true, out messages);

            for (int i = 0; i < states.Count; i++)
            {
                // KFreon: Actually set visual cues
                if (!noWindow)
                    ChangeGameIndicators(i + 1, states[i]);

                OutputBoxPrintLn(messages[i]);
            }
        }

        private void SetupGUI()
        {
            gooey.AddControl(CancelButton, "CancelB", new Action<bool>(state =>
            {
                CancelButton.Visible = !state;
                if (state)
                {
                    CancelButton.Text = "Cancel";
                    cts = new CancellationTokenSource();
                }
            }));

            gooey.AddControl(saveChangesToolStripMenuItem, "saveChanges", true);
            gooey.AddControl(treeIOToolStripMenuItem, "treeIO", true);
            gooey.AddControl(rebuildDatabaseToolStripMenuItem, "rebuild", true);
            gooey.AddControl(updateTOCsToolStripMenuItem, "updateTOCs", true);
            gooey.AddControl(SearchBox, "SearchBox", true);
            gooey.AddControl(ChangeButton, "ChangeButton", true);
            gooey.AddControl(regenerateThumbnailsToolStripMenuItem, "regenerate", true);
            gooey.AddControl(startTPFModeToolStripMenuItem, "TPFMode", true);
            gooey.AddControl(addDLCToTreeToolStripMenuItem, "addDLC", true);
        }

        private bool SaveFile(List<string> Filenames, List<int> ExpIDs, Textures.ITexture2D tex2D, int j)
        {
            if (cts.IsCancellationRequested)
                return false;

            PCCObjects.IPCCObject PCC = null;
            string currentPCC = Filenames[j];

            // KFreon: Skip non existent pccs
            if (!File.Exists(currentPCC))
                return true;

            // KFreon: Fix pathing
            string temppath = WhichGame == 1 ? Path.GetDirectoryName(pathBIOGame) : pathBIOGame;
            if (!currentPCC.Contains(temppath))
                currentPCC = Path.Combine(temppath, currentPCC);

            DebugOutput.PrintLn("Now saving pcc: " + currentPCC + "...");
            PCC = PCCObjects.Creation.CreatePCCObject(currentPCC, WhichGame);

            if (String.Compare(tex2D.texName, PCC.Exports[ExpIDs[j]].ObjectName, true) != 0 || (PCC.Exports[ExpIDs[j]].ClassName != "Texture2D" && PCC.Exports[ExpIDs[j]].ClassName != "LightMapTexture2D" && PCC.Exports[ExpIDs[j]].ClassName != "TextureFlipBook"))
                throw new InvalidDataException("Export object has wrong class or name");

            Textures.ITexture2D temptex2D = PCC.CreateTexture2D(ExpIDs[j], pathBIOGame);
            temptex2D.CopyImgList(tex2D, PCC);
            PCC.Exports[ExpIDs[j]].SetData(temptex2D.ToArray(PCC.Exports[ExpIDs[j]].DataOffset, PCC));
            PCC.saveToFile(currentPCC);
            PCC.Dispose();
            return true;
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ChangedTextures.Count == 0)
            {
                MessageBox.Show("You haven't made any changes yet.", "Emergency Induction Port...", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DisableContext = true;
            ProgBarUpdater.ChangeProgressBar(0, ChangedTextures.Count);
            OutputBoxPrintLn("Saving " + ChangedTextures.Count + " Textures...");

            // KFreon: Run save function on backbone
            backbone.AddToBackBone(b =>
            {
                for (int i = 0; i < ChangedTextures.Count; i++)
                {
                    // KFreon: Check cancellation
                    if (cts.IsCancellationRequested)
                    {
                        StatusUpdater.UpdateText("Saving Cancelled!");
                        OutputBoxPrintLn("Saving Cancelled!");
                        return false;  // KFreon: return doesn't matter right now
                    }

                    // KFreon: Setup objects
                    TreeTexInfo tex = Tree.GetTex(ChangedTextures[i]);
                    StatusUpdater.UpdateText("Saving " + tex.TexName + " |  " + MainProgressBar.Value + 1 + " \\ " + MainProgressBar.Maximum);
                    PCCObjects.IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(tex.Files[0], WhichGame);
                    {
                        Textures.ITexture2D tex2D = tex.Textures[0];
                        {
                            // KFreon: Save first file
                            /*PCCObjects.IExportEntry expEntry = pcc.Exports[tex2D.pccExpIdx];
                            expEntry.SetData(tex2D.ToArray(expEntry.DataOffset, pcc));
                            pcc.saveToFile(pcc.pccFileName);*/

                            /*this.Invoke(new Action(() => MainProgressBar.Increment(1)));
                            OutputBoxPrintLn("Initial saving complete for " + tex.TexName + ". Now saving remaining PCC's for this texture...");*/


                            // KFreon: Save first file and refresh object for next file. Dunno why necessary but it is. git: #296
                            if (!SaveFile(new List<string>() { tex.Files[0] }, new List<int>() { tex.ExpIDs[0] }, tex2D, 0))
                                return false;

                            // KFreon: Refresh objects
                            if (tex.Files.Count > 1)
                            {
                                pcc = PCCObjects.Creation.CreatePCCObject(tex.Files[0], WhichGame);
                                tex2D = pcc.CreateTexture2D(tex.ExpIDs[0], pathBIOGame);
                            }

                            // KFreon: Save files
                            for (int j = 1; j < tex.Files.Count; j++)
                            {
                                if (!SaveFile(tex.Files, tex.ExpIDs, tex2D, j))
                                    return false;
                            }

                            tex2D.hasChanged = false;
                        } 
                    }
                    ProgBarUpdater.IncrementBar();
                }

                OutputBoxPrintLn("All textures saved!");
                ChangedTextures.Clear();
                DisableContext = false;
                StatusUpdater.UpdateText("Updating TOC's...");
                ProgBarUpdater.ChangeProgressBar(0, 1);
                UpdateTOCs(WhichGame);
                ProgBarUpdater.ChangeProgressBar(1, 1);
                StatusUpdater.UpdateText("All textures saved!");
                return true;
            });
        }

        private async void ImportTree()
        {
            if (Tree == null)
            {
                MessageBox.Show("Game files not found. Unfortunately we seem to need them to use trees...");
                return;
            }
            else if (Tree.TexCount != 0)
                if (MessageBox.Show("This will import the tree and thumbnails (if present) from the specified location. Be sure the game currently selected in Texplorer matches the tree you're importing. Do you want to continue?", "Sure you wanna do that Commander?", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
                    return;
            
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "ME Trees|*.bin";
                ofd.Title = "Select tree to import.";

                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StatusUpdater.UpdateText("Importing tree...");
                    ProgBarUpdater.ChangeProgressBar(0, 1);

                    TreeDB temptree = Tree.Clone();  // KFreon: Copy settings so can back track if tree load fails

                    string destTreePath = ExecFolder + "me" + WhichGame + "tree.bin";
                    File.Copy(ofd.FileName, destTreePath);

                    int status;
                    if (!LoadTreeFromFile(destTreePath, out status, false))  // KFreon: Load actual tree.bin
                    {
                        Tree = temptree;
                        MessageBox.Show("Error occured while loading tree. Likely a corrupted or invalid tree.");
                        return;
                    }

                    StatusUpdater.UpdateText("Importing thumbs...");

                    await Task.Run(() =>
                    {
                        // KFreon: Copy thumbnails
                        string basePath = Path.GetDirectoryName(ofd.FileName);
                        string mainCachePath = Path.Combine(basePath, $"ThumbnailCaches\\ME{WhichGame}ThumbnailCache");

                        bool inMainCachePath = Directory.Exists(mainCachePath);
                        string sourceThumbCachePath = mainCachePath;

                        if (!inMainCachePath)
                        {
                            string nakedPath = Path.Combine(basePath, $"ME{WhichGame}ThumbnailCache");
                            bool isNakedPath = Directory.Exists(nakedPath);  // KFreon: Huehuehue

                            if (isNakedPath)
                                sourceThumbCachePath = nakedPath;
                            else
                            {
                                DebugOutput.PrintLn("Thumbnails not found. Skipping...");
                                return;
                            }
                        }

                        string destThumbCachePath = ThumbnailPath;
                        Directory.CreateDirectory(destThumbCachePath);

                        var files = Directory.GetFiles(sourceThumbCachePath);
                        ProgBarUpdater.ChangeProgressBar(0, files.Length);

                        int count = 0;
                        foreach (var file in files)
                        {
                            string filename = Path.GetFileName(file);
                            string destPath = Path.Combine(destThumbCachePath, filename);  // KFreon: No overwriting. 
                            if (!File.Exists(destPath))
                                File.Copy(file, destPath);

                            if (count++ % 10 == 0)
                                ProgBarUpdater.IncrementBar();
                        }
                    });

                    ChangeTreeIndicators(MEExDirecs.WhichGame, true);
                    ProgBarUpdater.ChangeProgressBar(1, 1);
                    StatusUpdater.UpdateText("Tree imported!");
                }
            }
        }

        private async void ExportTree()
        {
            if (Tree == null || Tree.TexCount == 0)
            {
                MessageBox.Show("No tree loaded to export!");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Select export location for your tree.";
                sfd.Filter = "ME Trees|*.bin";
                sfd.FileName = "ME" + WhichGame + "tree.bin";
                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StatusUpdater.UpdateText("Exporting Tree...");
                    ProgBarUpdater.ChangeProgressBar(0, 1);
                    await Task.Run(() =>
                    {
                        Tree.WriteToFile(sfd.FileName, Path.GetDirectoryName(pathBIOGame));

                        // KFreon: Copy out Thumbnails
                        if (Directory.Exists(ThumbnailPath))
                        {
                            string baseDirec = Path.GetDirectoryName(sfd.FileName);
                            string destThumbDirec = Path.Combine(baseDirec, $"ThumbnailCaches\\ME{MEExDirecs.WhichGame}ThumbnailCache");

                            Directory.CreateDirectory(destThumbDirec);

                            var files = Directory.GetFiles(ThumbnailPath);

                            ProgBarUpdater.ChangeProgressBar(0, files.Length);

                            foreach (var file in files)
                            {
                                string destName = Path.Combine(destThumbDirec, Path.GetFileName(file));
                                File.Copy(file, destName, true);

                                ProgBarUpdater.IncrementBar();
                            }
                        }
                    });

                    DebugOutput.PrintLn("Tree exported successfully!");
                    StatusUpdater.UpdateText("Tree Exported!");
                    ProgBarUpdater.ChangeProgressBar(1, 1);
                }
            }
        }

        public ToolTip SetupToolTip(Control control)
        {
            ToolTip newtip = new ToolTip();
            newtip.Show(PrimaryToolTip.GetToolTip(control), control, 10, 10, 100000);
            return newtip;
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Search.Show();
        }

        private void rebuildDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will delete your current tree and re-scan your gamefiles. Are you sure?", "Reconstruct facial profile?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
            {
                // KFreon: Check game state first in case of update
                CheckGameStates();

                // KFreon: Remove tree
                if (File.Exists(ExecFolder + "me" + WhichGame + "tree.bin"))
                    File.Delete(ExecFolder + "me" + WhichGame + "tree.bin");
                OutputBoxPrintLn(Environment.NewLine + "Rebuilding ME" + WhichGame + " tree...");

                // KFreon: Removed due to extraction occuring during toolset start
                // MrFob: probably unnecessary but if the game is ME3, run extract DLCs just to be sure
                /*if (WhichGame == 3)
                {
                    DLCEditor2.DLCEditor2 dlcedit2 = new DLCEditor2.DLCEditor2();
                    dlcedit2.ExtractAllDLC();
                }*/

                // KFreon: Clear everything and rebuild tree
                ClearDisplays();
                Tree = null;
                BeginLoadingTree(true, true);
            }
        }

        private void updateTOCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (WhichGame == 3)
            {
                StatusUpdater.UpdateText("Updating TOCs...");
                Task.Run(() =>
                {
                    UpdateTOCs(WhichGame);
                    StatusUpdater.UpdateText("TOCs Updated!");
                });
            }
            else
            {
                DebugOutput.PrintLn($"ME{WhichGame} does not require TOCing.");
            }
        }

        private async void ChangeButton_Click(object sender, EventArgs e)
        {
            OutputBoxPrintLn(Environment.NewLine);
            switch (WhichGame)
            {
                case 1:
                    WhichGame = 2;
                    ChangeButton.Text = "Modding ME2";
                    break;
                case 2:
                    WhichGame = 3;
                    ChangeButton.Text = "Modding ME3";
                    break;
                case 3:
                    WhichGame = 1;
                    ChangeButton.Text = "Modding ME1";
                    break;
            }

            MEExDirecs.WhichGame = WhichGame;
            DebugOutput.PrintLn();
            DebugOutput.PrintLn("Changing Trees...");
            WindowTitle = "Texplorer 2.0:  " + "ME" + WhichGame;
            this.Text = WindowTitle;
            Properties.Settings.Default.TexplorerGameVersion = WhichGame;
            SaveProperties();
            ClearDisplays();
            Tree = null;

            if (!noWindow)
            {
                NoRenderButton.Visible = (WhichGame == 1);
                LowResButton.Visible = (WhichGame == 1);
            }

            // KFreon: Setup pathing
            await Task.Run(() => MEExDirecs.SetupPathing(true));
            BeginLoadingTree();
        }

        private void ClearDisplays()
        {
            if (MainListView.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    Tree.Clear();
                    MainListView.Items.Clear();
                    PCCsCheckedListBox.Items.Clear();
                    PropertiesRTB.Clear();
                }));
            }
            else
            {
                if (Tree != null)
                    Tree.Clear();
                MainListView.Items.Clear();
                PCCsCheckedListBox.Items.Clear();
                PropertiesRTB.Clear();
            }
        }

        private void ChangeTreeIndicators(int game, bool state)
        {
            if (MainTreeView.InvokeRequired)
                MainTreeView.Invoke(new Action(() => ChangeTreeIndicators(game, state)));
            else
            {
                DebugOutput.PrintLn("Changing tree indicator of Game: " + game + " to: " + state);
                Color color = state ? Color.LightGreen : Color.Red;
                switch (game)
                {
                    case 1:
                        Tree1Label.ForeColor = color;
                        break;
                    case 2:
                        Tree2Label.ForeColor = color;
                        break;
                    case 3:
                        Tree3Label.ForeColor = color;
                        break;
                }
            }
        }

        private void ChangeGameIndicators(int game, bool state)
        {
            if (MainTreeView.InvokeRequired)
                MainTreeView.Invoke(new Action(() => ChangeGameIndicators(game, state)));
            else
            {
                Color color = state ? Color.LightGreen : Color.Red;
                switch (game)
                {
                    case 1:
                        Game1Label.ForeColor = color;
                        break;
                    case 2:
                        Game2Label.ForeColor = color;
                        break;
                    case 3:
                        Game3Label.ForeColor = color;
                        break;
                }
            }
        }

        private void BeginLoadingTree(bool NoUser = false, bool ForceRebuild = false)
        {
            // KFreon: Move to backbone if necessary
            if (!MainListView.InvokeRequired)
            {
                backbone.AddToBackBone(b =>
                {
                    BeginLoadingTree(NoUser, ForceRebuild);
                    return true;
                });
                return;
            }

            DebugOutput.PrintLn("Beginning to load trees.");

            // KFreon: Wait for GUI controls to be created
            while (!ChangeButton.Parent.Created)
            {
                DebugOutput.PrintLn("Waiting for GUI to be created...");
                System.Threading.Thread.Sleep(100);
            }


            this.Invoke(new Action(() =>
            {
                // KFreon: Close search list
                TabSearchSplitter.SplitterDistance = 0;

                // KFreon: Close context panel
                ContextPanel.Height = 0;
            }));

            // KFreon: Loop over all trees
            for (int i = 1; i < 4; i++)
            {
                // KFreon: Partly check tree we want
                if (i == WhichGame)
                {
                    DebugOutput.PrintLn("This is the tree we want: " + i);
                    if (!Texplorer2.SetupTree(ref Tree, pathCooked, WhichGame, MainTreeView, pathBIOGame))
                        continue;
                }
                else
                {
                    // KFreon: Start tasks for trees we don't care about. Could keep these, but won't so trees can be loaded dynamically.
                    var y = i;
                    DebugOutput.PrintLn("This is a tree we don't care so much about: " + y);
                    Task.Run(() =>
                    {
                        TreeDB temptree = null;
                        MEExDirecs.SetupPathing(false);
                        string tempbio = MEExDirecs.GetDifferentPathBIOGame(y);
                        bool res = Texplorer2.SetupTree(ref temptree, MEExDirecs.GetDifferentPathCooked(y), y, null, tempbio);
                        bool temp = false;
                        if (res)
                        {
                            int status2;
                            temp = temptree.ReadFromFile(ExecFolder + "me" + y + "tree.bin", Path.GetDirectoryName(tempbio), ExecFolder + "ThumbnailCaches\\ME" + y + "ThumbnailCache\\", out status2);
                        }

                        DebugOutput.PrintLn(temp ? "Found ME" + y + " tree." : "ME" + y + " tree not found.");
                        ChangeTreeIndicators(y, temp);
                        return temp;
                    });
                }
            }

            bool dostuff = ForceRebuild;
            bool treefound = false;

            DebugOutput.PrintLn("Dealing with main tree now...");

            if (Tree == null)
                MessageBox.Show("ME" + WhichGame + " not found!", "Forgetting something?", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (!dostuff && Tree != null)
            {
                int status = 0;
                if (!LoadTreeFromFile(ExecFolder + "me" + WhichGame + "tree.bin", out status, true))
                {
                    DebugOutput.PrintLn("Tree loading failed for: " + WhichGame);

                    // KFreon: Load failed, but user said rebuild
                    if (status == 1)
                        dostuff = true;
                    else if (!NoUser)
                    {
                        this.Invoke(new Action(() =>
                        {
                            if (MessageBox.Show("No ME" + WhichGame + " tree found. Do you want to build one?", "Damn Kai Leng...", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                                dostuff = true;
                        }));
                    }
                    else if (status == 2)   // KFreon: User cancelled rebuild
                    {
                        DebugOutput.PrintLn("Not rebuilding tree.");
                        OutputBoxPrintLn("Tree corrupt. Load cancelled!");
                        ChangeButton.Invoke(new Action(() => ChangeButton.Enabled = true));
                        return;
                    }
                }
                else
                    treefound = true;
            }

            // KFreon: Don't want else if here cos do stuff can change inside the !dostuff statement. Also this performs the ManageDLC stuff if tree not found
            if (Tree == null)
            {
                DebugOutput.PrintLn("Tree was null. Probably cos gamefiles don't exist and tree wasn't loaded.");

                // KFreon: Placeholder
                OutputBoxPrintLn("ME" + WhichGame + " game files not found. Tree not loaded.");
                StatusUpdater.UpdateText("ME" + WhichGame + " not found.");
            }
            else if (!treefound)
            {
                StatusUpdater.UpdateText("Preparing First Time Setup...");
                DebugOutput.PrintLn("Preparing for FTCS...");
                if (dostuff)
                    treefound = FirstTimeSetup();
                else
                {
                    OutputBoxPrintLn("Tree not found, and tree scan cancelled!");
                    StatusUpdater.UpdateText("Ready.");
                }
            }
            else
                treefound = true;

            DebugOutput.PrintLn("Setting up GUI...");

            ChangeTreeIndicators(WhichGame, treefound && Tree != null);

            DebugOutput.PrintLn("Changing GUI before setting up search...");

            gooey.ModifyControl("updateTOCs", treefound);
            gooey.ModifyControl("SearchBox", treefound);
            gooey.ModifyControl("regenerate", treefound);
            gooey.ModifyControl("TPFMode", treefound);
            gooey.ModifyControl("saveChanges", treefound);

            if (treefound)
            {
                if (!cts.IsCancellationRequested)
                    SetupSearch();
                else
                {
                    StatusUpdater.UpdateText("Operation cancelled!");
                    OutputBoxPrintLn("Operation cancelled!");
                }
            }
            Console.WriteLine("Finished function");
        }


        private bool LoadTreeFromFile(string filename, out int status, bool AllowQuestions)
        {
            status = 0;
            OutputBoxPrintLn("Trying to load tree from:  " + filename);
            StatusUpdater.UpdateText("Trying to load tree...");

            if (!Tree.ReadFromFile(filename, Path.GetDirectoryName(pathBIOGame), ThumbnailPath, out status, AllowQuestions ? this : null))
                return false;

            
            OutputBoxPrintLn("Tree loaded.");
            ConstructTree();
            OutputBoxPrintLn("Tree constructed. Ready.");
            StatusUpdater.UpdateText("Ready.");
            return true;
        }


        private bool DateCheckFileList(List<string> files, bool Basegame, long year = 2013, int month = 6)
        {
            bool modified = false;
            string print = "";
            foreach (string file in files)
            {
                DateTime dt = File.GetLastWriteTime(file);
                if (Basegame)
                {
                    if (dt.Year < year)
                        continue;
                    else if (dt.Year == year && dt.Month < month)
                        continue;
                }
                else
                {
                    // KFreon: Year here is actually ticks
                    if (year > 3000 && dt.Ticks <= year)
                        continue;
                    else if (year < 3000 && dt.Year <= year)
                        continue;
                }

                string message = "Modified File:  " + file + "  - Modified at: " + dt.ToLongTimeString() + ' ' + dt.ToLongDateString();
                print += message + Environment.NewLine;
                modified = true;
            }
            DebugOutput.PrintLn(print);
            return modified;
        }


        private void BeginTreeScan()
        {
            // KFreon: Create/clear thumbnail folder
            if (!Directory.Exists(ThumbnailPath))
            {
                Directory.CreateDirectory(ThumbnailPath);
                DebugOutput.PrintLn("Thumbnail cache created at: " + ThumbnailPath);
            }
            else
            {
                DebugOutput.PrintLn("Attempting to delete old thumbnails...");
                StatusUpdater.UpdateText("Attempting to delete old thumbnails...");
                try
                {
                    foreach (string file in Directory.GetFiles(ThumbnailPath))
                        File.Delete(file);
                    DebugOutput.PrintLn("Thumbnails successfully cleared.");
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("ERROR removing thumbnails: " + e.Message);
                }
            }

            double StarTime = Environment.TickCount;
            ConcurrentBag<string> errors = ScanPCCList(true);

            // KFreon: If errors occured, display affected files
            if (errors.Count != 0)
            {
                // KFreon: Remove duplicates and display errors
                List<string> Errors = errors.Distinct().ToList();
                this.Invoke(new Action(() =>
                {
                    KFreonListErrorBox msg = new KFreonListErrorBox("Some files didn't scan.", "Must be Vorcha in the sewer pipes", Errors, SystemIcons.Error);
                    msg.ShowDialog();
                }));
            }


            if (cts.IsCancellationRequested)
            {
                OutputBoxPrintLn("Scan Cancelled!");
                StatusUpdater.UpdateText("Scan cancelled!");
            }
            else
            {
                //StatusUpdater.UpdateText("Ensuring file order is correct. This will take time...");
                Tree.WriteToFile(ExecFolder + "me" + WhichGame + "tree.bin", Path.GetDirectoryName(pathBIOGame));
                Console.WriteLine("TEXCOUNT: " + Tree.TexCount);
                this.Invoke(new Action(() =>
                {
                    ConstructTree();
                    MainProgressBar.Value = MainProgressBar.Maximum;
                }));
                StatusUpdater.UpdateText("Ready.  Treescan time: " + TimeSpan.FromMilliseconds(Environment.TickCount - StarTime).Duration());


            }
            ChangeTreeIndicators(WhichGame, !cts.IsCancellationRequested);
        }


        private ConcurrentBag<string> ScanPCCList(bool isTree, List<string> pccs = null)
        {
            ProgBarUpdater.ChangeProgressBar(0, isTree ? Tree.numPCCs : pccs.Count);
            ConcurrentBag<string> errors = new ConcurrentBag<string>();

            OutputBoxPrintLn("Scanning Files and generating thumbnails...");
            StatusUpdater.UpdateText("Scanned: 0/" + (isTree ? Tree.numPCCs : pccs.Count));

            // KFreon: Begin parallel file scan
            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = NumThreads;
            int count = 0;
            object countlock = new object();


            //DeepScanPCC(@"R:\Games\Origin Games\Mass Effect 3\BIOGame\CookedPCConsole\BIOG_HMM_HIR_PRO_R.pcc");

            Parallel.For(0, isTree ? Tree.numPCCs : pccs.Count, po, (b, loopstate) =>
            {
                if (cts.IsCancellationRequested)
                    loopstate.Stop();
                string file = isTree ? Tree.GetPCC(b) : pccs[b];
                DebugOutput.PrintLn("Scanning: " + file);
                if (!DeepScanPCC(file))
                    errors.Add(file);

                lock (countlock)
                    count++;
                if (count % 10 == 0)
                    this.Invoke(new Action(() =>
                    {
                        MainProgressBar.Increment(10);
                        StatusLabel.Text = "Scanning: " + count + " / " + (isTree ? Tree.numPCCs : pccs.Count);
                    }));
            });      

            return errors;
        }



        private bool DeepScanPCC(string filename)
        {
            try
            {
                using (PCCObjects.IPCCObject temppcc = PCCObjects.Creation.CreatePCCObject(filename, WhichGame))
                {
                    // KFreon:  Search the exports list for viable texture types
                    for (int i = 0; i < temppcc.Exports.Count; i++)
                    {
                        //TreeTexInfo tex = KFreonFormsLib.Miscextra.GenerateTexStruct(temppcc, i, WhichGame, pathBIOGame, ExecFolder, allpccs);
                        bool result;
                        TreeTexInfo tex = new TreeTexInfo(temppcc, i, WhichGame, pathBIOGame, ExecFolder, out result);

                        if (result)
                            Tree.AddTex(tex, (WhichGame == 1) ? temppcc.Exports[i].Package : "", filename);
                    }
                }
            }
            catch(Exception e)
            {
                DebugOutput.PrintLn("Scanning PCC: " + filename + " failed. Reason: " + e.ToString());
                return false;
            }
            return true;
        }


        public static bool SetupTree(ref TreeDB Tree, string pathCooked, int WhichGame, TreeView MainTreeView, string pathBIOGame)
        {
            DebugOutput.PrintLn("Setting up tree: " + WhichGame + " using PathBIOGame: " + pathBIOGame);
            if (Tree == null)
            {
                DebugOutput.PrintLn("Tree: " + WhichGame + " is not null.");
                List<string> allPccFiles = new List<string>();
                string path = pathCooked;

                if (!Directory.Exists(path))
                {
                    DebugOutput.PrintLn("Couldn't find main game files for ME" + WhichGame + ".");
                    return false;
                }
                else
                {
                    DebugOutput.PrintLn("Found main gamefiles for ME" + WhichGame + " at  " + pathBIOGame);
                }

                allPccFiles.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories).Where(s =>
                {
                    if (WhichGame == 1)
                    {
                        return s.EndsWith(".upk", true, null) || s.EndsWith(".u", true, null) || s.EndsWith(".sfm", true, null);
                    }
                    else
                        return s.EndsWith(".pcc");
                }));

                DebugOutput.PrintLn("Creating tree: " + WhichGame + ". Found: " + allPccFiles.Count + " scannable gamefiles.");

                Tree = new TreeDB(allPccFiles, ref MainTreeView, WhichGame, pathBIOGame);
            }
            return true;
        }


        private bool FirstTimeSetup()
        {
            DebugOutput.PrintLn("Beginning First Time Setup...");
            List<string> dlcfiles = new List<string>();


            // KFreon: Removed for rev 745 fix #261
            // KFreon: Added game gate here to stop it trying to do stuff for other games
            if (WhichGame == 3)
            {
                /*DebugOutput.PrintLn("Starting DLC Extraction...");
                StatusUpdater.UpdateText("Extracting all DLC. This will take time...");

                DLCEditor2.DLCEditor2 dlcedit2 = new DLCEditor2.DLCEditor2();
                dlcedit2.ExtractAllDLC();*/

                // KFreon: Enumerate DLC files here
                try
                {
                    dlcfiles = new List<string>(Directory.EnumerateFiles(DLCPath).Where(file => file.ToLower().EndsWith(".pcc") || file.ToLower().EndsWith(".tfc")));
                }
                catch
                {
                    // Kfreon: Ignore.
                }
            }

            DebugOutput.PrintLn(String.Format("Starting FTS Window with parameters: Game: {0}  DLCPath: {1}  Cooked: {2}", WhichGame, DLCPath, pathCooked));

            using (TexplorerFirstTimeSetup fts = new TexplorerFirstTimeSetup(WhichGame, DLCPath, pathCooked))
            {
                fts.ShowDialog();
                if (fts.FilesToAddToTree == null)
                {
                    StatusUpdater.UpdateText("First Time Setup Cancelled!");
                    DebugOutput.PrintLn("First time setup cancelled.");
                    return false;
                }
                else
                {
                    Tree.Clear(true);
                    Tree.AddPCCs(fts.FilesToAddToTree);
                    if (dlcfiles.Count != 0)
                    Tree.AddPCCs(dlcfiles);
                }
            }

            BeginTreeScan();


            OutputBoxPrintLn("Performing first time setup. Don't worry, this only has to be done once.");
            return true;
        }

        private void DisappearPictureBox()
        {
            MainListView.Dock = DockStyle.Fill;
            MainListView.Visible = true;
        }

        private void ConstructTree()
        {
            Dictionary<string, string> things = new Dictionary<string, string>();


            DebugOutput.PrintLn("Number of texes = " + Tree.TexCount);
            OutputBoxPrintLn("Constructing tree...");
            StatusUpdater.UpdateText("Constructing Tree...");
            if (!noWindow)
            {
                this.Invoke(new Action(() =>
                {
                    MainTreeView.Nodes.Clear();
                    MainTreeView.BeginUpdate();
                }));
            }
            t = new myTreeNode("All Texture Files", "All Texture Files");

            // KFreon:  Loop over all textures and add them to a node
            for (int i = 0; i < Tree.TexCount; i++)
            {
                TreeTexInfo currentTex = Tree.GetTex(i);
                string[] packs = currentTex.FullPackage.Split('.');
                myTreeNode node = null;
                myTreeNode prevNode = t;
                int NodeCount = Tree.NodeCount;

                for (int j = 0; j < packs.Length; j++)
                {
                    node = null;
                    string tempPack = String.Join(".", packs, 0, j + 1);
                    for (int k = 0; k < NodeCount; k++)
                    {
                        myTreeNode current = Tree.GetNode(k);
                        if (current.Name == tempPack)
                        {
                            node = current;
                            break;
                        }
                    }


                    if (node == null)
                    {
                        node = new myTreeNode(packs[j], tempPack);
                        prevNode.Nodes.Add(node);
                        Tree.AddNode(node);
                    }
                    prevNode = node;
                }
                string texname = currentTex.TexName;
                node.TexInds.Add(i);

                Textures.ITexture2D tex2D = Textures.Creation.CreateTexture2D(texname, currentTex.Files, currentTex.ExpIDs, WhichGame, pathBIOGame, currentTex.Hash);               

                tex2D.Mips = currentTex.NumMips;
                currentTex.Textures.Add(tex2D);
                currentTex.ParentNode = node;
                if (!Tree.ReplaceTex(i, currentTex))
                    MessageBox.Show("Replace in tree failed! Index: " + i + ", thisTex: " + currentTex.TexName);

            }

            if (!noWindow)
            {
                this.Invoke(new Action(() =>
                {
                    MainTreeView.Sort();
                    MainTreeView.Nodes.Add(t);
                    MainTreeView.EndUpdate();
                }));
            }
            DebugOutput.PrintLn("Tree constructed!");
        }


        private void DrawPCCList(int index)
        {
            TreeTexInfo tex = Tree.GetTex(index);
            PCCsCheckedListBox.Items.Clear();
            PCCsCheckedListBox.Items.Add("Select All", (tex.Files.Count == tex.OriginalFiles.Count ? true : false));
            for (int i = 0; i < tex.OriginalFiles.Count; i++)
            {
                bool isChecked = false;
                for (int j = 0; j < tex.Files.Count; j++)
                {
                    if (tex.OriginalFiles[i] == tex.Files[j])
                    {
                        isChecked = true;
                        break;
                    }
                }
                PCCsCheckedListBox.Items.Add(tex.OriginalFiles[i] + "      @" + tex.OriginalExpIDs[i], isChecked);
            }
        }


        private void UpdatePCCList(bool SelectAll, TreeTexInfo current)
        {
            if (SelectAll)
                for (int i = 0; i < PCCsCheckedListBox.Items.Count; i++)
                    PCCsCheckedListBox.SetItemChecked(i, PCCsCheckedListBox.GetItemChecked(0));
            else
                PCCsCheckedListBox.SetItemChecked(0, false);

            List<string> newlist = new List<string>();
            List<int> newlist2 = new List<int>();
            foreach (string item in PCCsCheckedListBox.CheckedItems)
            {
                var bits = item.Split('@');

                if (bits.Length == 1)
                    continue;

                newlist.Add(bits[0].Trim());
                newlist2.Add(Convert.ToInt32(bits[1]));
            }
            current.Files = new List<string>(newlist);
            current.ExpIDs = new List<int>(newlist2);


            Tree.ReplaceTex(GetSelectedTexInd(), current);
        }


        private void WriteDebug(string line)
        {
            using (StreamWriter sw = new StreamWriter("SEND TO KFREON.txt", true))
                sw.WriteLine(line);
        }

        public bool InstallTexture(string texname, List<string> pccs, List<int> IDs, byte[] imgdata)
        {
            if (pccs.Count == 0)
            {
                DebugOutput.PrintLn("No PCC's found for " + texname + ", skipping.");
                return false;
            }
            string fulpath = pccs[0];
            //string temppath = (WhichGame == 1) ? Path.GetDirectoryName(pathBIOGame) : pathBIOGame;
            // Heff: Again, is the removal of the last dir for ME1 intended, and if so for what purpose?
            string temppath = pathBIOGame;
            if (!fulpath.Contains(temppath))
                fulpath = Path.Combine(temppath, fulpath);


            // KFreon: Skip files that don't exist
            if (!File.Exists(fulpath))
                return false;

            PCCObjects.IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(fulpath, WhichGame);
            
            if ((pcc.Exports[IDs[0]].ClassName != "Texture2D" && pcc.Exports[IDs[0]].ClassName != "LightMapTexture2D" && pcc.Exports[IDs[0]].ClassName != "TextureFlipBook") || String.Compare(pcc.Exports[IDs[0]].ObjectName, texname, true) != 0)
                throw new InvalidDataException("Export is not correct class or name!");

            //Load the texture from the pcc
            Textures.ITexture2D tex2D = pcc.CreateTexture2D(IDs[0], pathBIOGame);
            {
                    
                tex2D.allPccs = pccs;
                tex2D.expIDs = IDs;
                int noImg = tex2D.imgList.Count;

                DebugOutput.PrintLn("Now replacing textures in texture: " + tex2D.texName, true);
                Debug.WriteLine("Now replacing textures in texture: " + tex2D.texName + "  ID: " + IDs[0]);
                WriteDebug("Now replacing textures in texture: " + tex2D.texName + "  ID: " + IDs[0]);

                ImageFile im = null;
                try
                {
                    im = new DDS("", imgdata);
                }
                catch
                {
                    Console.WriteLine("Error: Unable to detect input DDS format, skipping.");
                    return false;
                }



                // KFreon: TESTING
                Debug.WriteLine("First pcc: " + fulpath + "    ArcName: " + tex2D.arcName);
                WriteDebug("First pcc: " + fulpath + "    ArcName: " + tex2D.arcName);



                //The texture is a single image, therefore use replace function
                if (noImg == 1)
                {
                    string imgSize = tex2D.imgList[0].imgSize.width.ToString() + "x" + tex2D.imgList[0].imgSize.height.ToString();
                    try
                    {
                        tex2D.replaceImage(imgSize, im, pathBIOGame);
                    }
                    catch
                    {
                        // KFreon:  If replace fails, it's single image thus use the singleimageupscale function
                        tex2D.singleImageUpscale(im, pathBIOGame);
                    }
                }

                //If the texture has multiple images, then check the input texture for MIPMAPS
                else
                {
                    bool hasMips = true;
                    ImageFile imgFile = im;
                    /*try { ImageMipMapHandler imgMipMap = new ImageMipMapHandler("", imgdata); }
                    catch (Exception e)
                    {
                        hasMips = false;
                    }*/
                    using (ImageEngineImage img = new ImageEngineImage(imgdata))
                        hasMips = img.NumMipMaps > 1;


                    if (!hasMips)
                    {
                        string imgSize = imgFile.imgSize.width.ToString() + "x" + imgFile.imgSize.height.ToString();
                        try
                        {
                            //Try replacing the image. If it doesn't exist then it'll throw and error and you'll need to upscale the image
                            tex2D.replaceImage(imgSize, imgFile, pathBIOGame);
                        }
                        catch (Exception e)
                        {
                            tex2D.addBiggerImage(imgFile, pathBIOGame);
                        }
                    }
                    else
                    {
                        try
                        {
                            tex2D.OneImageToRuleThemAll(imgFile, pathBIOGame, imgdata);
                        }
                        catch (Exception e)
                        {
                            if (e.Message.Contains("Format"))
                            {
                                MessageBox.Show(texname + " is in the wrong format." + Environment.NewLine + Environment.NewLine + e.Message);
                                return false;
                            }
                        }
                    }
                }

                Debug.WriteLine("After replace: " + tex2D.arcName);
                WriteDebug("After replace: " + tex2D.arcName);


                DebugOutput.PrintLn("Replacement complete. Now saving pcc: " + pcc.pccFileName, true);

                PCCObjects.IExportEntry expEntry = pcc.Exports[IDs[0]];
                var tt = tex2D.ToArray(expEntry.DataOffset, pcc);
                expEntry.SetData(tt);
                expEntry.hasChanged = true;
                pcc.Exports[IDs[0]] = expEntry;

                pcc.saveToFile(pcc.pccFileName);

                int modCount = tex2D.allPccs.Count;

                // KFreon: Elapsed time stuff
                int start = Environment.TickCount;

                // KFreon: Refresh objects after saving
                pcc = KFreonLib.PCCObjects.Creation.CreatePCCObject(pcc.pccFileName, WhichGame);
                tex2D = pcc.CreateTexture2D(IDs[0], pathBIOGame);


                if (modCount > 1)
                    for (int item = 1; item < modCount; item++)
                    {
                        Debug.WriteLine(pccs[item] + "   " + IDs[item]);
                        WriteDebug(pccs[item] + "   " + IDs[item]);
                        if (!SaveFile(pccs, IDs, tex2D, item))
                            break;
                    }
                Debug.WriteLine("");
                WriteDebug("");

                // KFreon: More timer stuff
                TimeSpan ts = TimeSpan.FromMilliseconds(Environment.TickCount - start);
                Console.WriteLine(ts.Duration().ToString());
                DebugOutput.Print("All PCC updates finished. ");
                return true;
            }

            pcc = null;
        }

        public static void UpdateTOCs(int WhichGame = 3)
        {
            //SirCxyrtyx: ME1+2 do not have TOCs
            if (WhichGame == 3)
            {
                DebugOutput.PrintLn("Updating TOCs...");
                AutoTOC.AutoTOC toccer = new AutoTOC.AutoTOC();
                toccer.GenerateAllTOCs();
                DebugOutput.PrintLn("TOCs updated.");
            }
        }

        internal void AddToTree(string path, int exportID)
        {
            MessageBox.Show("THIS DOESN'T WORK YET! LET ME KNOW PLEASE.");
            return;
            BeginLoadingTree(true);
            using (PCCObjects.IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(path, WhichGame))
            {
                //TreeTexInfo tex = KFreonFormsLib.Miscextra.GenerateTexStruct(pcc, exportID, WhichGame, pathBIOGame, ExecFolder, Tree.GetPCCsAsList());
                bool Success;
                TreeTexInfo tex = new TreeTexInfo(pcc, exportID, WhichGame, pathBIOGame, ExecFolder, out Success);
                Tree.AddTex(tex, "", "");  // not right - should be blind add?
                if (File.Exists(Tree.TreePath))
                    File.Delete(Tree.TreePath);
                Tree.WriteToFile(Tree.TreePath, Path.GetDirectoryName(pathBIOGame));
            }
        }

        private void OutputBoxPrintLn(string message)
        {
            if (noWindow)
            {
                DebugOutput.PrintLn(message);
                return;
            }

            if (!OutputBox.Parent.Created)
            {
                Task.Run(() =>
                {
                    try
                    {
                        while (!OutputBox.Parent.Created)
                            System.Threading.Thread.Sleep(100);
                        OutputBoxPrintLn(message);
                    }
                    catch
                    {
                        return;
                    }
                });
                return;
            }

            DebugOutput.PrintLn(message);
            if (OutputBox.InvokeRequired)
                OutputBox.Invoke(new Action(() =>
                {
                    OutputBox.AppendText(message + Environment.NewLine);
                    OutputBox.ScrollToCaret();
                }));
            else
            {
                OutputBox.AppendText(message + Environment.NewLine);
                OutputBox.ScrollToCaret();
            }
        }


        private void UpdateTexDetails(int index, out TreeTexInfo tex, out Textures.ITexture2D tex2D)
        {
            tex = Tree.GetTex(index);
            tex2D = tex.Textures[0];

            //if (tex2D.imgList.Count == 0)
            if (!tex2D.hasChanged)
            {
                //Textures.ITexture2D newtex2D = KFreonFormsLib.Textures.Creation.CreateTexture2D(KFreonFormsLib.PCCObjects.Creation.CreatePCCObject(tex2D.allPccs[0], WhichGame), tex2D.expIDs[0], WhichGame, pathBIOGame, tex2D.Hash);
                tex2D = Textures.Creation.CreateTexture2D(tex2D, WhichGame, pathBIOGame);

                tex.Textures = new List<Textures.ITexture2D>();
                tex.Textures.Add(tex2D);
                Tree.ReplaceTex(index, tex);
            }
        }


        private void MainTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // KFreon: Populate MainListView and change icons etc in MainTreeView
            MainListView.BeginUpdate();
            MainListView.Items.Clear();
            myTreeNode locTree = e.Node as myTreeNode;
            int i = 0;
            //foreach (Textures.ITexture2D tex in locTree.textures)
            for (int j = 0; j < locTree.TexInds.Count; j++)
            {
                TreeTexInfo tex = Tree.GetTex(locTree.TexInds[j]);
                ListViewItem it = new ListViewItem("", 0);
                it.Text = tex.TexName;
                it.Name = i.ToString();
                tex.ListViewIndex = MainListView.Items.Count;
                //Tree.ReplaceTex(locTree.TexInds[j], tex);
                MainListView.Items.Add(it);
                i++;
            }
            MainListView.EndUpdate();

            // KFreon: Populate properties tab
            PropertiesRTB.Text = "Tree Node:  " + e.Node.Name + Environment.NewLine;
            int line1length = PropertiesRTB.TextLength;

            // KFreon: Count number of textures in node
            int count = locTree.NodeTextureCount();
            if (locTree.TexCount == 0)
                locTree.TexCount = count;
            PropertiesRTB.Text += "Contains: " + count;

            PCCsCheckedListBox.Items.Clear();

            // KFreon: Load thumbnails
            ListViewImageList.Images.Add(Image.FromFile(ExecFolder + "Placeholder.ico"));  // This is the old image seen in the list
            UpdateThumbnailDisplays(e.Node as myTreeNode);
        }


        /// <summary>
        /// Loads thumbnails from disk, generated during treescan.
        /// </summary>
        /// <param name="nod">Node to load textures into.</param>
        private void UpdateThumbnailDisplays(myTreeNode nod)
        {
            if (nod == null)
                return;

            if (!MainListView.InvokeRequired)
            {
                Task.Run(new Action(() => UpdateThumbnailDisplays(nod)));
                return;
            }
            myTreeNode SelectedNode = nod;
            List<Image> thumbs = new List<Image>();
            List<int> thuminds = new List<int>();

            for (int i = 0; i < SelectedNode.TexInds.Count; i++)
            {
                TreeTexInfo tex = Tree.GetTex(SelectedNode.TexInds[i]);
                int ind = 0;

                bool FoundImage = false;

                // KFreon: If no thumbnail, try again
                if (tex?.ThumbnailPath == null)
                    continue;
                else if (File.Exists(tex.ThumbnailPath))
                {
                    FoundImage = true;
                    ind = i + 1;
                }
                else if (tex.ThumbnailPath.Contains("placeholder.ico") && !tex.TriedThumbUpdate)
                {
                    try
                    {
                        using (Bitmap img = tex.GetImage(pathBIOGame))
                        {

                            using (PCCObjects.IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(tex.Files[0], WhichGame))
                            {
                                using (Textures.ITexture2D tex2D = pcc.CreateTexture2D(tex.ExpIDs[0], pathBIOGame))
                                {
                                    if (img != null)
                                    {
                                        string savepath = Path.Combine(ThumbnailPath, tex.ThumbName);
                                        if (File.Exists(savepath))
                                        {
                                            ind = i + 1;
                                            tex.ThumbnailPath = savepath;
                                            FoundImage = true;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                byte[] data = UsefulThings.WinForms.Imaging.GetPixelDataFromBitmap(img);
                                                ImageEngine.GenerateThumbnailToFile(new MemoryStream(data), savepath, 128);
                                                tex.ThumbnailPath = savepath;
                                                FoundImage = true;
                                                ind = i + 1;
                                            }
                                            catch
                                            {
                                                // IGNORE
                                            }
                                        }
                                        tex.TriedThumbUpdate = true;
                                        Tree.ReplaceTex(SelectedNode.TexInds[i], tex);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        thumbs.Add(new Bitmap(1, 1));
                    }
                }

                Image thumb;
                if (FoundImage)
                    try
                    {
                        thumb = UsefulThings.WinForms.Imaging.PadImageToSquare(tex.ThumbnailPath, 128);
                        thumbs.Add(thumb);
                    }
                    catch
                    {
                        FoundImage = false;  // Kfreon: Set flag for later denoting that the image failed to work properly
                    }

                // KFreon: Double I know, but this way, I can set the image to the placefinder image for a variety of situations
                if (!FoundImage)
                {
                    DebugOutput.PrintLn("Thumbnail: " + tex.ThumbnailPath + "  not found.");
                    ind = 0;
                }
                thuminds.Add(ind);
            }

            // KFreon: Set images to show in ListView
            if (!MainListView.IsDisposed)
            {
                this.Invoke(new Action(() =>
                {
                    ListViewImageList.Images.Clear();
                    var placeholder = new Bitmap(Path.Combine(ExecFolder, "placeholder.ico"));
                    ListViewImageList.Images.Add(placeholder);
                    ListViewImageList.Images.AddRange(thumbs.ToArray());

                    placeholder.Dispose();
                    thumbs.ForEach(t => t.Dispose());
                    thumbs = null;


                    for (int i = 0; i < MainListView.Items.Count; i++)
                    {
                        try
                        {
                            MainListView.Items[i].ImageIndex = thuminds[i];
                        }
                        catch (Exception e)
                        {
                            DebugOutput.PrintLn("Error setting thumbnails in display: " + e.Message);
                        }
                    }

                    MainListView.Refresh();
                }));
            }
        }

        private void MainTreeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            e.Node.ImageIndex = 1;
        }

        private void MainTreeView_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            e.Node.ImageIndex = 0;
        }

        private void ShowContextPanel(bool state)
        {
            UsefulThings.WinForms.Transitions.ITransitionType trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400);
            UsefulThings.WinForms.Transitions.Transition.run(ContextPanel, "Height", (!state) ? 0 : 50, trans);
        }

        private void MainListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            // KFreon: Indexing check
            if (MainListView.SelectedIndices.Count <= 0 || MainListView.SelectedIndices[0] < 0)
            {
                // KFreon: Disappear context menu
                //ShowContextPanel(false);
                return;
            }

            // KFreon: Show context menu
            //ShowContextPanel(true);

            // KFreon: Populate properties pane
            int index = GetSelectedTexInd();   // Note: SelectedIndicies only has 1 element (multiselect = false)
            TreeTexInfo tex = Tree.GetTex(index);
            Textures.ITexture2D tex2D = tex.Textures[0];

            // KFreon: Update Texture information if required
            UpdateTexDetails(index, out tex, out tex2D);

            Textures.IImageInfo info = tex2D.GenerateImageInfo();

            // KFreon: Display texture properties
            DisplayTextureProperties(tex2D, info);
            DrawPCCList(index);
        }

        private void DisplayTextureProperties(Textures.ITexture2D tex2D, Textures.IImageInfo info)
        {
            List<string> message = new List<string>();
            message.Add("Texture Name:  " + tex2D.texName);
            message.Add("Format:  " + tex2D.texFormat.ToString().Replace("DDS_", ""));
            message.Add("Width:  " + info.imgSize.width + ",  Height:  " + info.imgSize.height);
            int mipcount = tex2D.imgList.Where(t => t.offset != -1).Count();
            message.Add("Mipmaps:  " + (mipcount > 1 ? "Yes (" + mipcount + ")" : "No (1)"));
            //message.Add("LODGroup:  " + (tex2D.hasChanged ? "TEXTUREGROUP_Shadowmap" : ((String.IsNullOrEmpty(tex2D.LODGroup) ? "None (Uses World)" : tex2D.LODGroup))));
            // Heff: Were ALL modified textures assigned the shadowmap texture group?
            message.Add("LODGroup:  " + (String.IsNullOrEmpty(tex2D.LODGroup) ? "None (Uses World)" : tex2D.LODGroup));
            message.Add("Texmod Hash:  " + Textures.Methods.FormatTexmodHashAsString(tex2D.Hash));

            if (WhichGame != 1)
                message.Add("Texture Cache File:  " + (info.storageType == 0  ? "PCC Stored" : tex2D.arcName + ".tfc"));

            PropertiesRTB.Text = String.Join(Environment.NewLine, message);
        }

        private void PCCsCheckedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = PCCsCheckedListBox.SelectedIndex;
            bool SelectAll = false;
            if (index < 0)
                return;

            int texInd = GetSelectedTexInd();
            TreeTexInfo temp = Tree.GetTex(texInd);
            if (index == 0)
                SelectAll = true;
            else if ((PCCsCheckedListBox.CheckedIndices.Count == temp.Files.Count) && !PCCsCheckedListBox.CheckedIndices.Contains(0))    // KFreon: If all items checked, check "Select All" box
            {
                PCCsCheckedListBox.SetItemChecked(0, true);
                SelectAll = true;
            }

            Console.WriteLine(PCCsCheckedListBox.CheckedIndices.Count);
            UpdatePCCList(SelectAll, temp);
        }


        private async void Form_Closing(object sender, FormClosingEventArgs e)
        {
            bool cleanup = false;
            bool unsaved = false;

            // KFreon: Cancelling is a multithread fix for closing the form when I want to
            if (cancelling)
            {
                // KFreon: Background processes
                if (CancelButton.Visible)  // Only visible when backbone is doing stuff
                {
                    CancelButton_Click(null, null);
                    cleanup = cts.IsCancellationRequested;

                    // KFreon: Don't cancel
                    if (!cleanup)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // KFreon: Unsaved changes
                if (ChangedTextures.Count > 0)
                {
                    if (MessageBox.Show("There are unsaved changes. Are you sure you want to exit?", "We fight or we die...", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                    else
                    {
                        cleanup = true;
                        unsaved = true;
                    }
                }
            }

            e.Cancel = cancelling;
            this.ControlBox = !cancelling;

            if (cancelling)
                await Task.Run(() => CloseForm(cleanup, unsaved));
            else
            {
                DebugOutput.PrintLn("-----Execution of Texplorer closing...-----");
                SaveProperties();
            }
        }

        private void CloseForm(bool cleanup, bool unsaved)
        {
            cancelling = false;
            // KFreon: Wait for stuff, if necessary
            if (cleanup)
            {
                if (!unsaved)
                    while (!StatusLabel.Text.ToLowerInvariant().Contains("cancelled"))
                        System.Threading.Thread.Sleep(100);

                // KFreon: Regenerate modified thumbnails if necessary
                for (int i = 0; i < ChangedTextures.Count; i++)
                {
                    TreeTexInfo tex = Tree.GetTex(ChangedTextures[i]);
                    RegenerateThumbnail(tex, ChangedTextures[i], true);
                }


                // KFreon: Reset Search
                if (Search != null)
                {
                    Search.Close();
                    Search.Dispose();
                }
            }
            this.Invoke(new Action(() => this.Close()));
        }

        public void SetupSearch()
        {
            Search = new KFreonSearchForm(this);
            Search.LoadSearch();
        }

        public void ResetSearchAfterClose()
        {
            Task.Run(() =>
            {
                while (Search.IsHandleCreated)
                    System.Threading.Thread.Sleep(50);

                this.Invoke(new Action(() => SetupSearch()));
            });
        }

        // KFreon:  This method extends the original by dealing correctly with dupicate named textures.
        public void GoToTex(string name, string nodeName, int selectedIndex)
        {
            // Clear previous search
            MainListView.SelectedItems.Clear();
            foreach (myTreeNode node in Tree.GetNodesAsList())
                node.BackColor = Color.White;

            // KFreon: Search in tree for specified texture
            List<TreeTexInfo> found = new List<TreeTexInfo>(Tree.GetTreeAsList().Where(tex => (name.ToLowerInvariant() == tex.TexName.ToLowerInvariant() && nodeName.ToLowerInvariant() == tex.ParentNode.Text.ToLowerInvariant())));

            // KFreon: Pick one of the textures found based on which is selected in the search box
            TreeTexInfo treetex = found[selectedIndex];

            // KFreon: Set selected node and set its colour
            MainTreeView.SelectedNode = treetex.ParentNode;
            treetex.ParentNode.BackColor = Color.Green;

            // KFreon: Select item in ListView and ensure it's visible and focused
            MainListView.Items[treetex.ListViewIndex].Selected = true;
            MainListView.EnsureVisible(treetex.ListViewIndex);
            MainListView.Focus();
        }

        private void DetailsHideButton_Click(object sender, EventArgs e)
        {
            UsefulThings.WinForms.Transitions.ITransitionType transition = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(700);

            if (DetailsHideButton.Text == ">>")
            {
                PropertiesWidth = splitContainer3.Width - splitContainer3.Panel2.Width;
                TabSearchSplitter.Visible = false;
                SearchBox.Visible = false;
                SearchCountLabel.Visible = false;
                UsefulThings.WinForms.Transitions.Transition.run(splitContainer3, "SplitterDistance", splitContainer3.Width - 40, transition);
                DetailsHideButton.Text = "<<";
            }
            else
            {
                UsefulThings.WinForms.Transitions.Transition.run(splitContainer3, "SplitterDistance", PropertiesWidth, transition);
                DetailsHideButton.Text = ">>";
                TabSearchSplitter.Visible = true;
                SearchBox.Visible = true;
                SearchCountLabel.Visible = true;
            }
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (Tree.TexCount != 0)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Task.Run(() =>
                    {
                        if (SearchBox.Text != "")
                        {
                            UsefulThings.WinForms.Transitions.Transition.run(TabSearchSplitter, "SplitterDistance", TabSearchSplitter.Height / 2, new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400));
                            int start = Environment.TickCount;
                            Search.SearchAllv5(SearchBox.Text, SearchListBox, "new");
                            TimeSpan ts = TimeSpan.FromTicks(Environment.TickCount - start);
                            Console.WriteLine();
                        }
                    });
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.Back)
                {
                    if (SearchBox.Text != "")
                    {
                        string text = SearchBox.Text;
                        int length = text.LastIndexOfAny(new char[] { '_', ' ', '-', '\\' });
                        SearchBox.Text = text.Substring(0, length + 1);
                        SearchBox.SelectionStart = SearchBox.Text.Length;
                        SearchBox.SelectionLength = 0;
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (SearchBox.Text == "")
            {
                UsefulThings.WinForms.Transitions.Transition.run(TabSearchSplitter, "SplitterDistance", 0, new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400));
                SearchCountLabel.Text = "";
                int dest = splitContainer4.Width - (SearchBox.Location.X - TabSearchSplitter.Location.X) - 4;
                UsefulThings.WinForms.Transitions.Transition.run(SearchBox, "Width", dest, new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(200));
            }
            else
            {
                if (SearchBox.ForeColor != Color.Gray && Tree != null && Tree.TexCount != 0)
                {
                    if (SearchBox.Text[0] != '\\')
                    {
                        UsefulThings.WinForms.Transitions.Transition.run(TabSearchSplitter, "SplitterDistance", TabSearchSplitter.Height / 2, new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400));

                        int start = Environment.TickCount;
                        Search.SearchAllv5(SearchBox.Text, SearchListBox, "new");
                        Console.WriteLine(SearchBox.Text + "   " + TimeSpan.FromTicks(Environment.TickCount - start));



                        SearchCountLabel.Text = SearchListBox.Items.Count.ToString();
                        int dest = splitContainer4.Width - 4 - (SearchCountLabel.Width) - (SearchBox.Location.X - TabSearchSplitter.Location.X);
                        UsefulThings.WinForms.Transitions.Transition.run(SearchBox, "Width", dest, new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(200));
                    }
                }
            }
        }

        private void SearchListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SearchList_Click(SearchListBox);
        }

        public void SearchList_Click(ListBox box)
        {
            if (box.SelectedIndex == -1)
                return;

            string fullName = box.SelectedItem.ToString();
            string texName = fullName.Remove(fullName.IndexOf(" ("));
            int lastIndex = fullName.LastIndexOf("(");
            string nodeName = fullName.Remove(0, lastIndex >= 0 ? lastIndex : 0).Replace("(", "").Replace(")", "");

            // KFreon: Get all entries in box
            int index = -1;
            int count = 0;
            for (int i = 0; i < box.Items.Count; i++)
            {
                string name = box.Items[i].ToString();
                if (name.ToLowerInvariant() == fullName.ToLowerInvariant())
                {
                    index = i == box.SelectedIndex ? count : index;
                    count++;
                }
            }

            GoToTex(texName, nodeName, index);
        }

        public List<TreeTexInfo> SearchLoad()
        {
            return Tree.GetTreeAsList();
        }

        private void SearchBox_Enter(object sender, EventArgs e)
        {
            if (SearchBox.ForeColor == Color.Gray)
            {
                SearchBox.Clear();
                SearchBox.ForeColor = Color.Black;
            }
        }

        private void ResetSearchBox()
        {
            SearchBox.ForeColor = Color.Gray;
            SearchBox.Text = "Type to search...";
        }

        private void SearchBox_Leave(object sender, EventArgs e)
        {
            if (SearchBox.Text == "")
                ResetSearchBox();
        }

        private void Change_Changed(object sender, EventArgs e)
        {
            // KFreon: Recalculate position
            SearchCountLabel.Left = TabSearchSplitter.Width - (SearchCountLabel.Width + 4);
            Search.Reset();
            Search.LoadSearch();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Background tasks are running. Are you sure you want to cancel them?", "I don't know about this Shepard...", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
            {
                CancelButton.Text = "Cancelling...";
                CancelButton.Enabled = false;
                cts.Cancel();
            }
        }

        private void MainListView_DoubleClick(object sender, EventArgs e)
        {
            MainPictureBox.Image = null; // KFreon: Reset image

            if (MainListView.SelectedItems.Count <= 0)
                return;


            if (Previews.Count > 10)
            {
                var prev = Previews.First();
                prev.Value.Dispose();
                Previews.Remove(prev.Key);
            }


            TreeTexInfo tex = Tree.GetTex(GetSelectedTexInd());
            string key = tex.TexName + tex.Hash;

            // Get from cache if available
            if (Previews.ContainsKey(key))
            {
                MainPictureBox.Image = Previews[key];
                MainPictureBox.Refresh();
                MainListView.Visible = false;
                return;
            }

            Bitmap img = tex.GetImage(pathBIOGame, 512);
            
            // KFreon: Show image
            if (img != null)
            {
                MainPictureBox.Image = img;
                MainPictureBox.Refresh();
                MainListView.Visible = false;
                Previews.Add(key, img);
            }
            else
                MessageBox.Show("Unknown DDS image. Contact KFreon.");
        }

        private int GetSelectedTexInd()
        {
            return ((myTreeNode)MainTreeView.SelectedNode).TexInds[MainListView.SelectedIndices[0]];
        }

        private void MainPictureBox_Click(object sender, EventArgs e)
        {
            MainListView.Visible = true;
        }

        private void MainListView_FocusLeave(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                // KFreon: Disappear context menu
                System.Threading.Thread.Sleep(300);
                ShowContextPanel(false);
            });

        }

        private void PicturePanel_Click(object sender, MouseEventArgs e)
        {
            MainPictureBox_Click(null, null);
        }

        private string ExternalImageSelector(Textures.ITexture2D tex2D)
        {
            string path = "";
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select the image to add";
                ofd.Filter = "Image file|*.dds|All files|*.*";
                //                ofd.Filter = "Image file|*" + tex2D.getFileFormat() + "|All files|*.*";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return "";
                else
                    path = ofd.FileName;
            }

            StringBuilder sb = new StringBuilder();

            // KFreon: Check replacing texture
            using (ImageEngineImage img = new ImageEngineImage(path))
            {
                if (img.Format.InternalFormat != tex2D.texFormat)
                    sb.Append("Invalid format. Selected image is: " + img.Format.InternalFormat.ToString() + "  Required: " + tex2D.texFormat.ToString().ToUpperInvariant());

                if (img.NumMipMaps < tex2D.Mips)
                    sb.AppendLine("Mipmap error. Requires: " + tex2D.Mips + ".  Currently: " + img.NumMipMaps);
            }


            if (sb.Length != 0)
            {
                MessageBox.Show(sb.ToString(), "Mission Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }

            return path;
        }

        private void UpdateModifiedTex(Textures.ITexture2D tex2D, TreeTexInfo tex, int ind)
        {
            if (!tex2D.hasChanged)
                tex2D.hasChanged = true;
            tex.Textures[0] = tex2D;

            // KFreon: Change texture in tree
            if (!ChangedTextures.Contains(ind))
                ChangedTextures.Add(ind);
            Tree.ReplaceTex(ind, tex);
        }

        private void AddBiggerButton_Click(object sender, EventArgs e)
        {
            try
            {
                string path = "";
                int ind = GetSelectedTexInd();
                TreeTexInfo tex = Tree.GetTex(ind);
                Textures.ITexture2D tex2D = tex.Textures[0];
                tex2D.Mips = tex.NumMips;

                path = ExternalImageSelector(tex2D);
                if (path == "")
                    return;

                ProgBarUpdater.ChangeProgressBar(0);

                // KFreon: TPFMode shouldn't actually change any textures, just add to TPFTools. git #301
                if (TPFMode)
                {
                    AddTPFToolsJob(path, tex.Hash);
                    StatusUpdater.UpdateText("Job added to TPFTools!");
                    ProgBarUpdater.ChangeProgressBar(1, 1);
                    return;
                }

                ImageFile im = Textures.Creation.LoadAKImageFile(null, path);
                byte[] imgData = File.ReadAllBytes(path);

                // add function
                // KFreon:  If single image (no mips), use a different function.
                if (tex2D.imgList.Count <= 1)
                    tex2D.singleImageUpscale(im, pathBIOGame);
                else
                    tex2D.OneImageToRuleThemAll(im, pathBIOGame, imgData);

                //--------- update the pcc with the new replaced infos ----------
                //First check that the tex2D hasn't already been modded (i.e. it hasn't been replaced and then
                //upscaled
                //if (!t.textures[Convert.ToInt32(listItem.Name)].hasChanged)

                UpdateModifiedTex(tex2D, tex, ind);

                //UpdateTOCs(WhichGame);
                StatusUpdater.UpdateText("Replacement Complete!");
                OutputBoxPrintLn("Texture: " + tex.TexName + " Replaced.");
                this.Invoke(new Action(() => MainProgressBar.Value = MainProgressBar.Maximum));


                // KFreon: Regenerate thumbnail
                RegenerateThumbnail(tex, ind, false);
                UpdateThumbnailDisplays(MainTreeView.SelectedNode as myTreeNode);

                DisplayTextureProperties(tex2D, tex2D.GenerateImageInfo());

                // KFreon: If modmaking load textures
                if (ModMakerMode)
                {
                    AddModJob(tex2D, path);
                    StatusUpdater.UpdateText("Replacement Complete and job added to Modmaker!");
                }

                Previews.Remove(tex.TexName + tex.Hash);  // KFreon: It's changed now, so needs to be regenerated.
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while replacing texture:\n" + exc, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (ModMakerMode)
                    MessageBox.Show("The textures have also not been added to the ModMaker Joblist");
            }
        }

        private async void AddTPFToolsJob(string replacingPath, uint hash)
        {
            KFreonTPFTools3 tpftools = await KFreonTPFTools3.GetCurrentInstance();
            int texind = tpftools.LoadExternal(replacingPath, false);
            tpftools.UpdateHashAndReplace(texind, hash, true);
            tpftools.RedrawTreeView();
        }

        private void RegenerateThumbnail(TreeTexInfo tex, int index, bool FromFile)
        {
            // KFreon: Try to delete old thumbnail
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(tex.ThumbnailPath))
                    {
                        File.Delete(tex.ThumbnailPath);
                        DebugOutput.PrintLn("Deleted old thumbnail successfully at: " + tex.ThumbnailPath);
                    }
                    break;
                }
                catch
                {
                    this.Invoke(new Action(() => ListViewImageList.Images.Clear()));
                    System.Threading.Thread.Sleep(200);
                }
            }

            Directory.CreateDirectory(ThumbnailPath);

            // KFreon: Update tex details if necessary
            Textures.ITexture2D tex2D = null;
            UpdateTexDetails(index, out tex, out tex2D);

            string thumbpath = tex.ThumbnailPath ?? Path.Combine(ThumbnailPath, tex.ThumbName);

            try
            {
                if (FromFile)
                    Textures.Creation.GenerateThumbnail(tex.Files[0], WhichGame, tex.ExpIDs[0], pathBIOGame, thumbpath, ExecFolder);
                else
                    using (MemoryStream ms = new MemoryStream(tex2D.GetImageData()))
                        ImageEngine.GenerateThumbnailToFile(ms, thumbpath, 128);

                tex.ThumbnailPath = thumbpath;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error generating thumbnail: " + e.Message);
            }

            //Tree.ReplaceTex(index, tex);
        }

        private void RegenerateButton_Click(object sender, EventArgs e)
        {
            if (MainListView.SelectedIndices.Count != 0 && MainListView.SelectedIndices[0] >= 0)
            {
                MainProgressBar.Value = 0;
                int ind = GetSelectedTexInd();
                RegenerateThumbnail(Tree.GetTex(ind), ind, true);
                UpdateThumbnailDisplays(MainTreeView.SelectedNode as myTreeNode);
                StatusUpdater.UpdateText("Thumbnail Regenerated!");
                MainProgressBar.Value = MainProgressBar.Maximum;
            }
        }

        private async void AddModJob(Textures.ITexture2D tex2D, string replacingfile)
        {
            ModMaker modmaker = await ModMaker.GetCurrentInstance();
            KFreonLib.Scripting.ModMaker.AddJob(tex2D, replacingfile, WhichGame, pathBIOGame);
            modmaker.ExternalRefresh();
        }

        private void ExtractButton_Click(object sender, EventArgs e)
        {
            if (MainListView.SelectedIndices.Count != 0 && MainListView.SelectedIndices[0] >= 0)
            {
                ProgBarUpdater.ChangeProgressBar(0, 1);

                // KFreon: Select mip to replace
                List<string> names = new List<string>();
                int index = GetSelectedTexInd();
                TreeTexInfo tex = Tree.GetTex(index);
                Textures.ITexture2D tex2D = tex.Textures[0];


                string savepath = "";
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "Select destination for file. Hash will be appended to filename.";
                    sfd.FileName = tex.TexName + "_" + Textures.Methods.FormatTexmodHashAsString(tex.Hash) + ".dds";
                    sfd.Filter = "DDS Files|*.dds";
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        savepath = sfd.FileName;
                    else
                        return;
                }

                // KFreon: Removed as per #313
                // KFreon: Make sure hash is there
                /*if (!savepath.Contains(Textures.Methods.FormatTexmodHashAsString(tex.Hash)))
                    savepath = Path.Combine(Path.GetDirectoryName(savepath), Path.GetFileNameWithoutExtension(savepath) + "_" + Textures.Methods.FormatTexmodHashAsString(tex.Hash) + Path.GetExtension(savepath));*/

                // KFreon: Save file
                File.WriteAllBytes(savepath, tex2D.extractMaxImage(true));

                StatusUpdater.UpdateText("Image extracted and saved!");
                ProgBarUpdater.ChangeProgressBar(1, 1);
            }
        }

        private void UpscaleButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("If anyone uses this, let KFreon know and I'll look at adding it.");
        }

        private void NoRenderButton_Click(object sender, EventArgs e)
        {
            if (WhichGame == 1 && MainListView.SelectedIndices.Count != 0 && MainListView.SelectedIndices[0] >= 0)
            {
                MainProgressBar.Value = 0;
                int index = GetSelectedTexInd();
                TreeTexInfo tex = Tree.GetTex(index);
                Textures.ITexture2D tex2D = tex.Textures[0];

                tex2D.NoRenderFix = true;
                UpdateModifiedTex(tex2D, tex, index);
                StatusUpdater.UpdateText("No Render fix applied!");
                OutputBoxPrintLn("No render fix applied to " + tex.TexName);
                MainProgressBar.Value = MainProgressBar.Maximum;
            }
        }

        private void LowResButton_Click(object sender, EventArgs e)
        {
            if (WhichGame == 1 && MainListView.SelectedIndices.Count != 0 && MainListView.SelectedIndices[0] >= 0)
            {
                MainProgressBar.Value = 0;
                int index = GetSelectedTexInd();
                TreeTexInfo tex = Tree.GetTex(index);
                Textures.ITexture2D tex2D = tex.Textures[0];

                // KFreon: Single Image check
                if (tex2D.imgList.Count <= 1)
                {
                    StatusUpdater.UpdateText("Don't need to fix a single level texture. Don't even worry about it");
                    OutputBoxPrintLn("Texture: " + tex.TexName + " is single level. No fix required or applied.");
                    return;
                }

                tex2D.LowResFix();
                UpdateModifiedTex(tex2D, tex, index);

                StatusUpdater.UpdateText("Low res patch applied!");
                OutputBoxPrintLn("Low res patch applied to " + tex.TexName);
                MainProgressBar.Value = MainProgressBar.Maximum;
            }
        }

        private void startTPFModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!TPFMode && MessageBox.Show("This mode will capture all modifications and send them to an instance of TPFTools." + Environment.NewLine + "NOTE: No modifications will be made to your files in this mode. Proceed?", "How bout we go that way?", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
            {
                startTPFModeToolStripMenuItem.Text = "Stop TPF Mode";
                TPFMode = true;
                saveChangesToolStripMenuItem.Enabled = false;
            }
            else if (TPFMode)
            {
                startTPFModeToolStripMenuItem.Text = "Start TPF Mode";
                TPFMode = false;
                saveChangesToolStripMenuItem.Enabled = true;
            }
        }

        private void PCCBoxContext_Click(object sender, EventArgs e)
        {
            string savepath = "";
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Select destination for PCC list";
                sfd.Filter = "Text Files|*.txt";

                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    savepath = sfd.FileName;
                else
                    return;
            }

            TreeTexInfo tex = Tree.GetTex(GetSelectedTexInd());
            using (StreamWriter sw = new StreamWriter(savepath))
                foreach (string pccname in tex.Files)
                    sw.WriteLine(pccname);

            MessageBox.Show("PCC list exported!");
        }

        public void removeTopTexture(string texname, List<string> pccs, List<int> IDs)
        {
            // KFreon: WHY does this function exist...
        }

        private async void MainListView_MouseDown(object sender, MouseEventArgs e)
        {
            await Task.Delay(50);
            if (!DisableContext && MainListView.SelectedIndices.Count != 0 && MainListView.SelectedIndices[0] != -1)
                ShowContextPanel(true);
            else
                ShowContextPanel(false);
        }

        private void treeIOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void changePathsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // KFreon: Populate BIOGame entries
            List<string> biogames = new List<string>();
            for (int i = 1; i < 4; i++)
            {
                if (i == WhichGame)
                    biogames.Add(pathBIOGame);
                else
                    biogames.Add("");
            }

            // KFreon: Display PathChanger
            using (Helpers.PathChanger changer = new Helpers.PathChanger(biogames[0], biogames[1], biogames[2]))
            {
                if (changer.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
                    return;

                #region Change Paths and Save
                // KFreon: Change paths
                MEExDirecs.SetPaths(changer.PathME1, changer.PathME2, changer.PathME3);

                // KFreon: Get paths again
                CheckGameStates();
                #endregion
            }
        }

        private void AddDLCToTree(List<string> pccs)
        {
            Tree.AddPCCs(pccs);
            ConcurrentBag<string> errors = ScanPCCList(false, pccs);
            Tree.WriteToFile(Tree.TreePath, Path.GetDirectoryName(pathBIOGame));

            if (errors != null && errors.Count != 0)
            {
                MessageBox.Show("Errors occured!" + Environment.NewLine + String.Join(Environment.NewLine, errors), "Your technology is based on that of the Mass Relays...", MessageBoxButtons.OK);
                StatusUpdater.UpdateText("Errors occured. See DebugWindow");
            }
            else
            {
                StatusUpdater.UpdateText("Refreshing Tree...");

                ClearDisplays();
                Tree = null;
                BeginLoadingTree();
                StatusUpdater.UpdateText("DLC added to tree!");
            }

            ProgBarUpdater.ChangeProgressBar(1, 1);
        }

        private void addDLCToTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> pccs = new List<string>();

            string outputPath = null;
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.EnsurePathExists = true;
            dialog.Title = "Select DLC Folder to add. (e.g. DLC_CON_END)";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                outputPath = dialog.FileName;
            else
                return;

            pccs = Directory.EnumerateFiles(outputPath, "*.pcc", SearchOption.AllDirectories).ToList();

            if (pccs.Count == 0)
            {
                StatusUpdater.UpdateText("DLC is not extracted (no PCC's found).");
                return;
            }

            backbone.AddToBackBone(b =>
            {
                AddDLCToTree(pccs);
                return true;
            });
        }

        private void changeCustomTFCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select new TFC to be used for new textures";
                ofd.Filter = "Bioware Texture Caches|*.tfc";

                if (ofd.ShowDialog() == DialogResult.OK)
                    MEDirectories.CachePath = ofd.FileName;
            }
        }

        private void Game2Label_Click(object sender, EventArgs e)
        {

        }

        private void showFilesExpIDsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveTreeToCSV(true);
        }

        private void dontShowFilesExpIDsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveTreeToCSV(false);
        }

        private void SaveTreeToCSV(bool ShowFilesExpIDs)
        {
            if (Tree != null)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.FileName = "ME" + MEExDirecs.WhichGame + " Texture Details.csv";
                    sfd.Filter = "Comma Separated|*.csv";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        ProgBarUpdater.ChangeProgressBar(0, 1);
                        StatusUpdater.UpdateText("Exporting Tree...");

                        Tree.ExportToCSV(sfd.FileName, ShowFilesExpIDs);

                        StatusUpdater.UpdateText("Tree exported!");
                        ProgBarUpdater.ChangeProgressBar(1, 1);
                    }
                }
            }

        }

        private void regenerateAllThumbnailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will delete and recreate all thumbnails for ME" + WhichGame + ". This will take some time. Are you sure?", "Consensus?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
            {
                int texCount = Tree.TexCount;
                ProgBarUpdater.ChangeProgressBar(0, texCount);
                OutputBoxPrintLn("Regenerating ME" + WhichGame + " thumbnails...");
                StatusUpdater.UpdateText("Regenerating thumbnails...  0 of " + texCount);

                backbone.AddToBackBone(b =>
                {
                    return RegenerateThumbs(Tree.GetTreeAsList()); 
                });
            }
        }

        private void GetTexesRecursive(myTreeNode node, List<TreeTexInfo> temptexes)
        {
            for (int i = 0; i < node.TexInds.Count; i++)
                temptexes.Add(Tree.GetTex(node.TexInds[i]));

            var nodecount = node.Nodes?.Count ?? 0;
            if (nodecount != 0)
                foreach (myTreeNode n in node.Nodes)
                    GetTexesRecursive(n, temptexes);
        }

        private void selectedFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // KFreon: Get selected folder and contained texes
            myTreeNode selectedNode = MainTreeView.SelectedNode as myTreeNode;
            var selectedNodeTexCount = selectedNode?.TexInds?.Count ?? 0;
            if (selectedNodeTexCount <= 0)
                return;

            List<TreeTexInfo> temptexes = new List<TreeTexInfo>();
            GetTexesRecursive(selectedNode, temptexes);
            


            if (MessageBox.Show("This will delete and recreate all thumbnails for the selected folder. This will take some time. Are you sure?", "Consensus?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
            {
                int texCount = temptexes.Count;
                ProgBarUpdater.ChangeProgressBar(0, texCount);
                OutputBoxPrintLn("Regenerating ME" + WhichGame + " thumbnails...");
                StatusUpdater.UpdateText("Regenerating thumbnails...  0 of " + texCount);

                backbone.AddToBackBone(b =>
                {
                    return RegenerateThumbs(temptexes);
                });
            }
        }

        private bool RegenerateThumbs(List<TreeTexInfo> temptexes)
        {
            this.Invoke(new Action(() =>
            {
                foreach (Bitmap img in ListViewImageList.Images)
                    img.Dispose();

                ListViewImageList.Images.Clear();
            }));
            System.Threading.Thread.Sleep(200);

            if (temptexes.Count > 1000)  // KFreon: Entire thing - no folder has > 1000 texes
            {
                // KFreon: Delete old thumbnails
                if (Directory.Exists(ThumbnailPath))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            Directory.Delete(ThumbnailPath, true);
                            System.Threading.Thread.Sleep(100);
                            DebugOutput.PrintLn("Successfully deleted old thumbnails.");
                            break;
                        }
                        catch
                        {
                            DebugOutput.PrintLn("Failed to delete old thumbnails. Sleeping for a bit before trying again.");
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
            }
            else
            {
                foreach (var tex in temptexes)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            File.Delete(tex.ThumbnailPath);
                        }
                        catch (FileNotFoundException e)
                        {
                            DebugOutput.PrintLn($"Thumbnail: {tex.ThumbnailPath} not found, thus not deleted.");
                            System.Threading.Thread.Sleep(100);
                        }
                        catch (Exception e)
                        {
                            DebugOutput.PrintLn($"Error occured deleting thumbnail: {tex.ThumbnailPath}. {e.Message}");
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    
                }
            }
            

            // KFreon: Create directory
            Directory.CreateDirectory(ThumbnailPath);

            // KFreon: Generate thumbnails for all textures in file
            int count = 0;
            foreach (var tex in temptexes)
            {
                // KFreon: Generate thumbnails for each texture
                for (int j = 0; j < 3; j++)
                {
                    try
                    {
                        using (Textures.ITexture2D tex2D = KFreonLib.Textures.Creation.CreateTexture2D(tex.Textures[0], WhichGame, pathBIOGame))
                        {
                            string destination = tex.ThumbnailPath ?? Path.Combine(ThumbnailPath, tex.ThumbName);
                            using (MemoryStream ms = new MemoryStream(tex2D.GetImageData()))
                            {
                                var treetex = tex2D.imgList.Where(t => t.offset != -1).First();
                                int max = (int)(treetex.imgSize.height > treetex.imgSize.width ? treetex.imgSize.height : treetex.imgSize.width);
                                double divisor = max > 128 ? max / 128.0 : 1;

                                int newWidth = (int)(treetex.imgSize.width / divisor);
                                int newHeight = (int)(treetex.imgSize.height / divisor);

                                string result = Textures.Creation.GenerateThumbnail(ms, destination, newWidth, newHeight);
                                if (result != null)
                                    tex.ThumbnailPath = destination;
                                else
                                    tex.ThumbnailPath = Path.Combine(ExecFolder, "placeholder.ico");
                            }


                            DebugOutput.PrintLn("Generated thumbnail at: " + tex.ThumbnailPath);
                            ProgBarUpdater.IncrementBar();

                            // KFreon: Update status
                                count++;
                                if (count % 10 == 0)
                                    StatusUpdater.UpdateText("Regenerating thumbnails...  " + count + " of " + temptexes.Count);
                            break;
                        }
                    }
                    catch
                    {
                        DebugOutput.PrintLn("Failed to generate thumbnail from: " + tex.TexName + ". Sleeping before trying again.");
                        System.Threading.Thread.Sleep(100);
                    }
                }
                

            }
            ProgBarUpdater.ChangeProgressBar(1, 1);
            StatusUpdater.UpdateText("Thumbnails Regenerated.");
            OutputBoxPrintLn("Thumbnails Regenerated");
            if (!Tree.AdvancedFeatures && MessageBox.Show("Your current tree doesn't have advanced features enabled. Do you want to save these features to your current tree?", "You probably want to do this", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
            {
                for (int i = 0; i < 3; i++)
                    try
                    {
                        Tree.WriteToFile(Tree.TreePath, Path.GetDirectoryName(pathBIOGame));
                        DebugOutput.PrintLn("Tree saved with advanced features");
                        break;
                    }
                    catch
                    {
                        DebugOutput.PrintLn("Tree in use. Sleeping for a bit.");
                        System.Threading.Thread.Sleep(200);
                    }
            }

            UpdateThumbnailDisplays(MainTreeView.SelectedNode as myTreeNode);
            return true;
        }

        private void importTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportTree();
        }

        private void asFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportTree();
        }

        private void toggleTooltipsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelButton.Visible)
                return;

            if (tooltips.Count == 0)
            {
                // KFreon: Show tooltips
                ShowContextPanel(true);
                System.Threading.Thread.Sleep(300);
                tooltips.Add(SetupToolTip(MainTreeView));
                tooltips.Add(SetupToolTip(MainListView));
                tooltips.Add(SetupToolTip(DetailsHideButton));
                tooltips.Add(SetupToolTip(ContextPanel));
                tooltips.Add(SetupToolTip(tabControl1));
                tooltips.Add(SetupToolTip(ChangeButton));
                tooltips.Add(SetupToolTip(OutputBox));
            }
            else
            {
                tooltips.ForEach(tool => tool.Dispose());
                tooltips.Clear();
            }
        }

        private void wikiArticleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.wikia.com/wiki/Texplorer");
        }
    }
}
