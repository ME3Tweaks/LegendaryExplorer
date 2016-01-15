using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gibbed.IO;
using System.Threading.Tasks;
using System.Threading;
using KFreonLib;
using KFreonLib.GUI;
using KFreonLib.Debugging;
using ModJob = KFreonLib.Scripting.ModMaker.ModJob;
using KFreonLib.Scripting;
using KFreonLib.Helpers;
using System.Diagnostics;
using KFreonLib.MEDirectories;
using System.Reflection;
using UsefulThings;
using KFreonLib.PCCObjects;

namespace ME3Explorer
{
    public partial class ModMaker : Form
    {
        List<int> SearchResults = null;
        CancellationTokenSource cts = new CancellationTokenSource();
        TextUpdater StatusUpdater;
        ProgressBarChanger MainProgBar;
        Gooey gooey;
        BackBone backbone;
        static ModMaker currentInstance;
        bool SuppressCheckEvent = true;
        object SuppressLocker = new object();
        MEDirectories MEExDirecs = new MEDirectories();
        string EmptyText = "Search in jobs, scripts, and pccs...";

        string ExecFolder
        {
            get
            {
                return MEExDirecs.ExecFolder;
            }
        }

        List<string> BIOGames
        {
            get
            {
                return MEExDirecs.BIOGames;
            }
        }

        private void SaveProperties()
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("UNABLE to save properties: " + e.Message);
            }
        }

        public ModMaker()
        {
            InitializeComponent();
            KFreonTPFTools3.UpgradeSettings();

            // KFreon: Set number of threads if necessary
            if (Properties.Settings.Default.NumThreads == 0)
            {
                Properties.Settings.Default.NumThreads = KFreonLib.Misc.Methods.SetNumThreads(false);
                SaveProperties();
            }

            StatusUpdater = new TextUpdater(MainStatusLabel, BottomStrip);
            MainProgBar = new ProgressBarChanger(BottomStrip, MainProgressBar);
            backbone = new BackBone(() => { gooey.ChangeState(false); return true; }, () => { gooey.ChangeState(true); return true; });

            currentInstance = this;

            MainSplitter.SplitterDistance = MainSplitter.Width;
            MainContextStrip.Height = 0;
            PCCSplitter.SplitterDistance = PCCSplitter.Height;

            Initialise(false);
            KFreonLib.Scripting.ModMaker.Initialise();

            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
            SearchTextBox.MouseDown += SearchTextBox_MouseDown;
            SearchTextBox.LostFocus += SearchTextBox_LostFocus;

            SearchTextBox.ForeColor = Color.Gray;
            SearchTextBox.Text = EmptyText;

            // KFreon: Display version
            VersionLabel.Text = "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        void SearchTextBox_LostFocus(object sender, EventArgs e)
        {
            MouseHandler();
        }

        void SearchTextBox_MouseDown(object sender, MouseEventArgs e)
        {
            MouseHandler();
        }

        void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && KFreonLib.Scripting.ModMaker.JobList.Count > 0)
            {
                SearchResults = SearchEngine(SearchTextBox.Text);
                SelectThings(SearchResults[0]);
                e.Handled = true;
            }
        }

        void MouseHandler()
        {
            // Empty unselected textbox
            if (SearchTextBox.Text.Contains("..."))
            {
                SearchTextBox.ForeColor = Color.Black;
                SearchTextBox.Text = "";
            }
            else if (SearchTextBox.Text.Length == 0)  // Empty selected textbox
            {
                SearchTextBox.ForeColor = Color.LightGray;
                SearchTextBox.Text = EmptyText;
            }
        }

        private void ShowDetails(bool state)
        {
            UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(600);
            UsefulThings.WinForms.Transitions.Transition.run(MainSplitter, "SplitterDistance", (state ? 620 : MainSplitter.Width), trans);
        }


        private void Initialise(bool changeTree)
        {
            // KFreon: Setup GUI
            gooey = new Gooey(MainListView);
            gooey.AddControl(LoadButton, "Load", true);
            gooey.AddControl(RunAllButton, "RunAll", true);
            gooey.AddControl(ClearAllButton, "ClearAll", true);
            gooey.AddControl(SaveAllButton, "SaveAll", true);
            gooey.AddControl(RunSelectedButton, "RunSelected", true);
            gooey.AddControl(SaveSelectedButton, "SaveSelected", true);
            gooey.AddControl(MoveUpButton, "MoveUp", true);
            gooey.AddControl(MoveDownButton, "MoveDown", true);
            gooey.AddControl(SelectAllButton, "SelectAll", true);
            gooey.AddControl(UpdateJobButton, "UpdateJob", true);
            gooey.AddControl(ResetScriptButton, "ResetScript", true);
            gooey.AddControl(CancelationButton, "Cancel", false, true, false);
            gooey.AddControl(CancelationButton, "cts", new Action(() =>
            {
                if (CancelationButton.Visible)
                    cts = new CancellationTokenSource();
            }));
            gooey.AddControl(ChangePathsButton, "ChangePaths", true);

            DebugOutput.PrintLn();
            DebugOutput.PrintLn("Changing Trees...");

            // KFreon: Start debugger in separate thread
            if (!changeTree)
                DebugOutput.StartDebugger("Mod Maker 2.0");

            // KFreon: Get paths for each game
            CheckGameState();
        }

        private void CheckGameState()
        {
            List<string> messages = null;
            List<bool> states = KFreonLib.Misc.Methods.CheckGameState(MEExDirecs, false, out messages);

            for (int i = 0; i < 3; i++)
                ChangeIndicatorColors(i + 1, states[i]);
        }

        private void ChangeIndicatorColors(int game, bool state)
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(() => ChangeIndicatorColors(game, state)));
            else
            {
                Color color = state ? Color.LightGreen : Color.Red;
                switch (game)
                {
                    case 1:
                        OneLabel.ForeColor = color;
                        break;
                    case 2:
                        TwoLabel.ForeColor = color;
                        break;
                    case 3:
                        ThreeLabel.ForeColor = color;
                        break;
                }
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select .mod to load";
                ofd.Filter = "ME3 ModMaker Files|*.mod";
                ofd.Multiselect = true;
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    backbone.AddToBackBone(b =>
                    {
                        gooey.ModifyControl("ChangePaths", false);
                        LoadFromFiles(ofd.FileNames);
                        return true;
                    });
            }
        }


        private void LoadFromFiles(string[] filenames)
        {
            int nummods = 0;
            bool? autoupdate = LoadMods(filenames, out nummods);

            // KFreon: Setup display names
            MainProgBar.ChangeProgressBar(0, nummods);

            // KFreon: Load Cancelled
            if (autoupdate == null)
            {
                StatusUpdater.UpdateText("Ready.");
                MainProgBar.ChangeProgressBar(1, 1);
                return;
            }

            StatusUpdater.UpdateText((bool)autoupdate ? "Updating, Formatting names, and generating thumbnails..." : "Formatting names and generating thumbnails...");
            bool conflict;
            List<string> names = FormatJobs(autoupdate, false, out conflict);
            // Heff: prompt user to chose version and re-run with version chosen
            if (conflict)
            {
                this.Invoke(new Action(() =>
                {
                    var gameVers = VersionPickDialog.AskForGameVersion(this, message: "Could not detect game version, the files in this .mod were present in more than one game. \n"
                        + "Please choose the correct version:");
                    names = FormatJobs(autoupdate, false, out conflict, gameVers);
                }));
            }

            this.Invoke(new Action(() =>
            {
                MainListView.Items.Clear();
                if (cts.IsCancellationRequested)
                {
                    MainImageList.Images.Clear();
                    KFreonLib.Scripting.ModMaker.JobList.Clear();
                    

                    MainProgressBar.Value = MainProgressBar.Maximum;
                    MainStatusLabel.Text = "Load Cancelled!";
                }
                else
                {
                    for (int i = 0; i < names.Count; i++)
                    {
                        MainListView.Items.Add(names[i]);
                        MainListView.Items[i].ImageIndex = i;
                    }
                    StatusUpdater.UpdateText("Mods Loaded! Loaded " + nummods + " from " + filenames.Length + " files.");
                }

                MainListView.Refresh();
            }));
        }

        public void ExternalRefresh()
        {
            backbone.AddToBackBone(b =>
            {
                bool conflictIgnore;
                List<string> names = FormatJobs(false, false, out conflictIgnore);
                this.Invoke(new Action(() =>
                {
                    MainListView.Items.Clear();
                    for (int i = 0; i < names.Count; i++)
                    {
                        MainListView.Items.Add(names[i]);
                        MainListView.Items[i].ImageIndex = i;
                    }
                    MainListView.Refresh();
                    MainProgBar.ChangeProgressBar(1, 1);
                }));
                return true;
            });
        }

        public List<string> FormatJobs(bool? autoupdate, bool ExternalCall, out bool versionConflict, int version = -1)
        {
            DebugOutput.PrintLn("Formatting jobs...");
            versionConflict = false;

            // KFreon: Create list of display names and populate it for multi-threading
            List<string> names = new List<string>();
            MainImageList.Images.Clear();
            for (int i = 0; i < KFreonLib.Scripting.ModMaker.JobList.Count; i++)
            {
                names.Add("");
                MainImageList.Images.Add(new Bitmap(1, 1));
            }


            for (int i = 0; i < KFreonLib.Scripting.ModMaker.JobList.Count; i++)
            /*ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = Properties.Settings.Default.NumThreads;
            Parallel.For(0, KFreonLib.Scripting.ModMaker.JobList.Count, po, i =>*/
            {
                if (cts.IsCancellationRequested)
                    return null;
                ModJob job = KFreonLib.Scripting.ModMaker.JobList[i];

                // KFreon: Format size
                double len = job.Length;
                string size = len.ToString() + " bytes.";
                if (len > 1024)   // KFreon: Kilobyte
                {
                    double siz = 1024;
                    string ending = " Kilobytes.";
                    if (len > 1024 * 1024)  // KFreon: Megabytes
                    {
                        siz = 1024 * 1024;
                        ending = " Megabytes.";
                    }

                    string newsize = (len / siz).ToString();
                    size = newsize.Substring(0, newsize.IndexOf('.') + 3) + ending;
                }
                names[i] = (job.Name + "  --> Size: " + size);

                DebugOutput.PrintLn(String.Format("Job: {0}  size:  {1}", job.Name, job.Length));
                DebugOutput.PrintLn("Getting further job info...");

                var result = job.GetJobDetails(autoupdate == true, out versionConflict, version);
                // Heff: quit early if game version conflict is found, prompt user, then go again.
                if (versionConflict)
                    return null;

                if (result == false)
                { 
                    // Heff: If this happens then the user has already been informed via a popup.
                    // Heff: Hopefully this is a value that can be handled by all callers.

                    // KFreon: Still want to see the job instead of being given nothing
                    //cts.Cancel();
                    //return new List<string>();
                    return names;
                }

                DebugOutput.PrintLn("Got job details");
                if (!ExternalCall)
                {
                    // KFreon: Generate Thumbnail
                    if (job.JobType == "TEXTURE")
                        MainImageList.Images[i] = (job.GenerateJobThumbnail());
                    else
                        MainImageList.Images[i] = (Image.FromFile(Path.Combine(ExecFolder, "Placeholder.ico"))).GetThumbnailImage(64, 64, null, IntPtr.Zero);
                }


                // KFreon: Update if desired
                if (autoupdate == true)
                {
                    DebugOutput.PrintLn("Updating job: " + job.Name);
                    if (ExternalCall)
                        StatusUpdater.UpdateText("Updating job: " + job.Name);
                    bool res = job.UpdateJob(BIOGames, ExecFolder);
                    DebugOutput.PrintLn(!res ? "Failed to update job: " + job.Name : "Updated Job: " + job.Name);
                }

                KFreonLib.Scripting.ModMaker.JobList[i] = job;

                if (!ExternalCall)
                    MainProgBar.IncrementBar();
                //});
            }
            return names;
        }

        public bool? LoadMods(string[] FileNames, out int numMods, bool ExternalCall = false)
        {
            if (!ExternalCall)
                StatusUpdater.UpdateText("Loading From File/s...");

            if (KFreonLib.Scripting.ModMaker.JobList.Count == 0)
                KFreonLib.Scripting.ModMaker.Initialise();
            else
                numMods = KFreonLib.Scripting.ModMaker.JobList.Count;

            numMods = 0;

            bool? AutoUpdate = false;
            foreach (string file in FileNames)
            {
                DebugOutput.PrintLn("Loading file " + file, true);
                AutoUpdate = KFreonLib.Scripting.ModMaker.LoadDotMod(file, ref numMods, ((ExternalCall) ? null : MainProgressBar), ExternalCall);
                if (AutoUpdate == null)
                    break;
            }
            DebugOutput.PrintLn(AutoUpdate == null ? "User cancelled loading." : "Loaded " + numMods + ".", true);
            return AutoUpdate;
        }

        private void PCCListRewrite(ModJob job, bool CheckAll = false)
        {
            lock (SuppressLocker)
                SuppressCheckEvent = true;
            if (MainListView.InvokeRequired)
                this.Invoke(new Action(() => PCCListRewrite(job, CheckAll)));
            else
            {
                PCCList.Items.Clear();

                if (job.PCCs == null || (job.PCCs.Count == 0 && job.OrigPCCs.Count == 0))
                {
                    DebugOutput.PrintLn("Failed to get PCC's from script.");
                    PCCList.Items.Add("FAILED TO GET PCC'S");
                }
                else if (job.ExpIDs == null || (job.ExpIDs.Count == 0 && job.OrigExpIDs.Count == 0))
                {
                    DebugOutput.PrintLn("Failed to get ExpID's from script.");
                    PCCList.Items.Add("FAILED TO GET EXPID'S");
                }
                else if (job.PCCs.Count != job.ExpIDs.Count)
                {
                    DebugOutput.PrintLn("Number of PCC's and ExpID's extracted from script do not match.");
                    PCCList.Items.Add("PCC/EXPID COUNT MISMATCH");
                }
                else
                    for (int i = 0; i < job.OrigPCCs.Count; i++)
                        PCCList.Items.Add(job.OrigPCCs[i] + "  @ " + job.OrigExpIDs[i], CheckAll ? true : job.PCCs.Contains(job.OrigPCCs[i]));
                lock (SuppressLocker)
                    SuppressCheckEvent = false;
            }
        }

        private void MainListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            // KFreon: Index check
            int selectedInd = (MainListView.SelectedIndices.Count == 0) ? -1 : MainListView.SelectedIndices[0];
            if (selectedInd < 0)
                return;


            ShowMainContext(true);

            // KFreon: Save script and update PCC list
            ModJob job = KFreonLib.Scripting.ModMaker.JobList[selectedInd];
            ScriptPane.Text = job.Script;
            PCCListRewrite(job);
            KFreonLib.Scripting.ModMaker.JobList[selectedInd] = job;

            GameVersionBox.SelectedIndex = job.WhichGame;
            DisplayDetails(job);
        }


        private void DisplayDetails(ModJob job)
        {
            // KFreon: Fill details 
            string message = "Details:" + Environment.NewLine;
            message += "Texname: " + job.Texname + Environment.NewLine;
            message += "Number of ExpID's detected: " + job.ExpIDs.Count + Environment.NewLine;
            message += "Number of PCC's detected: " + job.PCCs.Count + Environment.NewLine;

            // KFreon: Check flimsy validity
            string fail = "";
            if (job.ExpIDs.Count == 0)
                fail += "EXPIDS ";

            if (job.PCCs.Count == 0)
                fail += "PCCS ";

            if (job.Texname == "")
                fail += "TEXNAME ";

            if (job.WhichGame == -1)
                fail += "WHICHGAME ";

            message += "Valid? " + job.Valid + "  " + fail;
            DetailsBox.Text = message;
        }

        private void ExpandScriptButton_Click(object sender, EventArgs e)
        {
            bool state = (ExpandScriptButton.Text == "<<<") ? true : false;
            ExpandScript(state);
            ExpandScriptButton.Text = (state) ? ">>>" : "<<<";
            HideShowButton.Text = ">>";
        }

        private void ExpandScript(bool state)
        {
            UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(600);
            UsefulThings.WinForms.Transitions.Transition.run(DetailsSplitter, "SplitterDistance", (state ? 0 : DetailsSplitter.Width / 2), trans);
        }

        private void ShowHideScript(bool state)
        {
            UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(600);
            UsefulThings.WinForms.Transitions.Transition.run(DetailsSplitter, "SplitterDistance", (state ? DetailsSplitter.Width / 2 : DetailsSplitter.Width - 76), trans);
        }

        private void HideShowButton_Click(object sender, EventArgs e)
        {
            bool state = (HideShowButton.Text == "<<") ? true : false;
            ChangeHideButton(state);
        }

        private void ChangeHideButton(bool state)
        {
            ShowHideScript(state);

            if (HideShowButton.InvokeRequired)
                this.Invoke(new Action(() =>
                {
                    HideShowButton.Text = (state) ? ">>" : "<<";
                    ExpandScriptButton.Text = "<<<";
                }));
            else
            {
                HideShowButton.Text = (state) ? ">>" : "<<";
                ExpandScriptButton.Text = "<<<";
            }
        }

        private void ShowMainContext(bool state)
        {
            //UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400);
            //UsefulThings.WinForms.Transitions.Transition.run(MainContextStrip, "Height", (state ? 25 : 0), trans);
            MainContextStrip.Height = state ? 25 : 0;
        }

        private void MainListView_MouseDown(object sender, MouseEventArgs e)
        {
            ListViewItem item = MainListView.GetItemAt(e.X, e.Y);
            if (item != null)
                DoDetailsShowing();
            else
            {
                // KFreon: Hide things
                ShowDetails(false);
                ShowMainContext(false);
            }
        }

        private void DoDetailsShowing()
        {
            // KFreon: Show details if necessary, and reset script splitter position
            Task.Run(() =>
            {
                if (DetailsSplitter.Width == 0)
                {
                    ShowDetails(true);
                    System.Threading.Thread.Sleep(600);
                }
                ChangeHideButton(true);
            });
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            if (CancelationButton.Visible)
            {
                if (MessageBox.Show("Background tasks are running. Are you sure you want to close?", "Really sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                {
                    cts.Cancel();

                    // KFreon: Wait for background tasks to be cancelled
                    Task.Run(() =>
                    {
                        while (!MainStatusLabel.Text.Contains("Cancelled"))
                            System.Threading.Thread.Sleep(100);

                        File.Delete(ExecFolder + "ModData.cache");
                        DebugOutput.PrintLn("-----Execution of ModMaker closing...-----");
                        KFreonLib.Scripting.ModMaker.JobList.Clear();
                        currentInstance = null;
                        this.Close();
                    });
                }
                e.Cancel = true;
            }
            KFreonLib.Scripting.ModMaker.JobList.Clear();
            DebugOutput.PrintLn("-----Execution of ModMaker closing...-----");
            currentInstance = null;
        }

        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            File.Delete(ExecFolder + "ModData.cache");
            MainListView.Items.Clear();
            KFreonLib.Scripting.ModMaker.JobList.Clear();
            PCCList.Items.Clear();
            ScriptPane.Clear();
            ShowDetails(false);
            ShowMainContext(false);


            // KFreon: Fix GUI
            gooey.ModifyControl("Load", false);
            gooey.ModifyControl("ChangePaths", true);
            gooey.ChangeState(false);
            ChangePathsButton.Enabled = true;



            StatusUpdater.UpdateText("Ready.");
            MainProgBar.ChangeProgressBar(0, 1);
            CancelationButton.Visible = false;
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index == 0 || MainListView.SelectedIndices.Count > 1)
                return;

            MoveJob(index, -1);
        }

        private void MoveJob(int index, int amount)
        {
            // KFreon: Joblist
            ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];
            KFreonLib.Scripting.ModMaker.JobList.RemoveAt(index);
            KFreonLib.Scripting.ModMaker.JobList.Insert(index + amount, job);

            // KFreon: ListView
            ListViewItem item = MainListView.Items[index];
            MainListView.Items.RemoveAt(index);
            MainListView.Items.Insert(index + amount, item);

            // KFreon: ImageList
            Image img = MainImageList.Images[index];
            List<Image> temp = new List<Image>();
            for (int i = 0; i < MainImageList.Images.Count; i++)
            {
                if (i == index + amount)
                    temp.Add(img);
                else if (i == index)
                    temp.Add(MainImageList.Images[i + amount]);
                else
                    temp.Add(MainImageList.Images[i]);
            }
            MainImageList.Images.Clear();
            foreach (Image im in temp)
                MainImageList.Images.Add(im);

            ResetImageIndicies();
            MainListView.Refresh();
        }

        private void ResetImageIndicies()
        {
            for (int i = 0; i < MainListView.Items.Count; i++)
                MainListView.Items[i].ImageIndex = i;
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index == MainListView.Items.Count - 1 || MainListView.SelectedIndices.Count > 1)
                return;

            MoveJob(index, 1);
        }

        private void MainListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                int index = MainListView.SelectedIndices[0];
                MainListView.Items.RemoveAt(index);
                KFreonLib.Scripting.ModMaker.JobList.RemoveAt(index);

                // KFreon: Damn imagelist can't RemoveAt
                List<Image> temp = new List<Image>();
                for (int i = 0; i < MainImageList.Images.Count; i++)
                {
                    if (i == index)
                        continue;

                    temp.Add(MainImageList.Images[i]);
                }
                MainImageList.Images.Clear();
                for (int i = 0; i < temp.Count; i++)
                {
                    MainImageList.Images.Add(temp[i]);
                    MainListView.Items[i].ImageIndex = i;
                }

                MainListView.Refresh();
                if (index > MainListView.Items.Count - 1 && index != 0)
                    index--;

                if (MainListView.Items.Count != 0)
                    MainListView.Items[index].Selected = true;
                else
                {
                    ShowDetails(false);
                    ShowMainContext(false);
                }
            }
            else if (e.KeyCode == Keys.Up)
            {
                int index = MainListView.SelectedIndices[0];
                if (index != 0)
                {
                    MainListView.Items[index - 1].Selected = true;
                    MainListView.Items[index].Selected = false;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                int index = MainListView.SelectedIndices[0];
                if (index != MainListView.Items.Count - 1)
                {
                    MainListView.Items[index + 1].Selected = true;
                    MainListView.Items[index].Selected = false;
                }
            }
        }

        private void MainListView_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList().Where(f => f.ToUpperInvariant().EndsWith(".MOD")).ToList();
            backbone.AddToBackBone(b => { LoadFromFiles(DroppedFiles.ToArray()); return true; });
        }

        private void MainListView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void RunAllButton_Click(object sender, EventArgs e)
        {
            MainProgBar.ChangeProgressBar(0, KFreonLib.Scripting.ModMaker.JobList.Count);
            StatusUpdater.UpdateText("Installing Mods:  1 /" + MainListView.Items.Count + " Mods Completed.");

            // KFreon: Create temp job list so no more can be added accidently
            List<ModJob> templist = new List<ModJob>();
            templist.AddRange(KFreonLib.Scripting.ModMaker.JobList);

            // KFreon: Install mods and update TOC's
            backbone.AddToBackBone(b =>
            {
                List<int> whichgames = new List<int>();
                List<string> DLCPCCs = RunJobs(templist, ref whichgames);
                if (!cts.IsCancellationRequested)
                    UpdateTOCS(DLCPCCs, whichgames);

                MainProgBar.ChangeProgressBar(1, 1);
                StatusUpdater.UpdateText(cts.IsCancellationRequested ? "Installation cancelled!" : "All Mods Installed!");
                return true;
            });
        }

        private List<string> RunJobs(List<ModJob> joblist, ref List<int> whichgames)
        {
            bool alreadyExtracted = true;
            foreach (var item in Directory.GetDirectories(ME3Directory.DLCPath))
            {
                if (!Directory.EnumerateFiles(item).ToList().Any(f => f.EndsWith(".pcc")))
                {
                    alreadyExtracted = false;
                    break;
                }
            }

            DialogResult result = DialogResult.Yes;
            this.Invoke(new Action(() =>
            {
                if (!alreadyExtracted)
                    result = MessageBox.Show("NOTE: ALL DLC's will be extracted to facilitate updating. This will take ~1 hour give or take 2 hours. It'll also take up many gigabytes on your HDD." + Environment.NewLine + "Continue? If you don't, the game may not start.", "You should say yes", MessageBoxButtons.YesNo);
            }));
            


            int count = 1;
            List<string> DLCPCCs = new List<string>();
            foreach (ModJob job in joblist)
            {
                if (cts.IsCancellationRequested)
                    break;
                StatusUpdater.UpdateText("Installing Job: " + count + " (" + job.Name + ") of " + joblist.Count);
                DebugOutput.PrintLn("Installing Job: " + count++ + " (" + job.Name + ") of " + joblist.Count);
                DLCPCCs.AddRange(InstallJob(job));
                MainProgBar.IncrementBar();
                if (!whichgames.Contains(job.WhichGame))
                    whichgames.Add(job.WhichGame);
            }
            return DLCPCCs;
        }

        private List<string> InstallJob(ModJob job)
        {
            List<string> DLCPCCs = new List<string>();

            ScriptCompiler sc = new ScriptCompiler();
            KFreonLib.Scripting.ModMaker.ModData = job.data;
            sc.rtb1.Text = job.Script;

            try
            {
                sc.Compile();
                foreach (string pcc in job.PCCs)
                {
                    string dlcname = KFreonLib.Misc.Methods.GetDLCNameFromPath(pcc);
                    if (dlcname != null && dlcname != "" && !DLCPCCs.Contains(dlcname))
                        DLCPCCs.Add(dlcname);
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Error occured: " + e.Message);
            }
            return DLCPCCs;
        }

        private void PCCList_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index < 0)
                return;

            DisappearPCCEditStuff(false);

            ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];

            // KFreon: Setup edit boxes
            int ind = PCCList.SelectedIndex;
            if (ind >= 0)
            {
                PathBox.Text = job.OrigPCCs[ind];
                ExpIDBox.Text = job.OrigExpIDs[ind].ToString();
            }
        }

        private void UpdatePCCList(int index, ModJob job, bool currentItemChecked, int pccInd)
        {
            // KFreon: Update script from PCC Checkbox
            job.PCCs.Clear();
            job.ExpIDs.Clear();
            for (int i = 0; i < PCCList.Items.Count; i++)
            {
                if (i == pccInd && currentItemChecked || i != pccInd && PCCList.GetItemChecked(i))
                {
                    job.PCCs.Add(job.OrigPCCs[i]);
                    job.ExpIDs.Add(job.OrigExpIDs[i]);
                }
            }



            job.Script = KFreonLib.Scripting.ModMaker.GenerateTextureScript(ExecFolder, job.PCCs, job.ExpIDs, job.Texname, job.WhichGame, BIOGames[job.WhichGame - 1]);
            ScriptPane.Text = job.Script;
            KFreonLib.Scripting.ModMaker.JobList[index] = job;
        }

        private void UpdateModButton_Click(object sender, EventArgs e)
        {
            if (MainListView.Items.Count == 0)
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Select .mod to update";
                    ofd.Filter = "ME3Explorer mods|*.mod";
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        backbone.AddToBackBone(b =>
                        {
                            LoadFromFiles(new string[] { ofd.FileName });
                            UpdateMods();
                            return true;
                        });
                    }
                }
            else
                backbone.AddToBackBone(b => { UpdateMods(); return true; });
        }

        private void UpdateMods()
        {
            bool failed = false;
            MainProgBar.ChangeProgressBar(0, KFreonLib.Scripting.ModMaker.JobList.Count);
            for (int i = 0; i < KFreonLib.Scripting.ModMaker.JobList.Count; i++)
            {
                ModJob job = KFreonLib.Scripting.ModMaker.JobList[i];
                StatusUpdater.UpdateText("Updating Mod: " + job.Name + ",  " + i + " of " + KFreonLib.Scripting.ModMaker.JobList.Count);
                MainProgBar.IncrementBar();
                bool res = job.UpdateJob(BIOGames, ExecFolder);
                if (res)
                    KFreonLib.Scripting.ModMaker.JobList[i] = job;
                else
                    failed = false;
            }
            StatusUpdater.UpdateText(failed ? "Some jobs failed." : "All Mods Updated!");
            MainProgBar.ChangeProgressBar(1, 1);
        }

        private void RunSelectedButton_Click(object sender, EventArgs e)
        {
            backbone.AddToBackBone(b =>
            {
                List<string> modified = new List<string>();
                int count = 0;
                this.Invoke(new Action(() => count = MainListView.SelectedIndices.Count));
                MainProgBar.ChangeProgressBar(0, count);
                List<int> whichgames = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    int index = 0;
                    this.Invoke(new Action(() => index = MainListView.SelectedIndices[i]));
                    if (index < 0)
                        continue;
                    else
                    {
                        ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];
                        StatusUpdater.UpdateText("Installing Mod: " + (i + 1) + " (" + job.Name + ") of " + count);
                        modified.AddRange(InstallJob(job));

                        if (!whichgames.Contains(job.WhichGame))
                            whichgames.Add(job.WhichGame);
                    }
                    MainProgBar.IncrementBar();
                }
                UpdateTOCS(modified, whichgames);
                MainProgBar.ChangeProgressBar(1, 1);
                StatusUpdater.UpdateText("All Mods Installed!");
                return true;
            });
        }

        private void UpdateTOCS(List<string> DLCPCCs, List<int> whichgames)
        {
            StatusUpdater.UpdateText("Updating TOC's...");

            List<string> temp = new List<string>();
            foreach (string dlc in DLCPCCs)
                if (!temp.Contains(dlc))
                    temp.Add(dlc);

            for (int i = 0; i < whichgames.Count; i++)
                Texplorer2.UpdateTOCs(BIOGames[whichgames[i] - 1], whichgames[i], MEExDirecs.GetDifferentDLCPath(whichgames[i]), temp);
        }

        private void SaveAllButton_Click(object sender, EventArgs e)
        {
            SaveJobs(KFreonLib.Scripting.ModMaker.JobList);
        }

        private void SaveJobs(List<ModJob> jobs)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Select location for new .mod";
                sfd.Filter = "ME3Explorer mods|*.mod";
                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    backbone.AddToBackBone(b =>
                    {
                        StatusUpdater.UpdateText("Saving .mod...");
                        MainProgBar.ChangeProgressBar(0, jobs.Count);
                        using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
                        {
                            KFreonLib.Scripting.ModMaker.WriteModHeader(fs, jobs.Count);
                            foreach (ModJob job in jobs)
                            {
                                if (cts.IsCancellationRequested)
                                    break;
                                job.WriteJobToFile(fs);
                                MainProgBar.IncrementBar();
                            }
                        }
                        StatusUpdater.UpdateText((cts.IsCancellationRequested) ? "Saving cancelled!" : "Mod saved!");
                        MainProgBar.ChangeProgressBar(1, 1);
                        if (cts.IsCancellationRequested)
                            File.Delete(sfd.FileName);

                        return true;
                    });
                }
            }
        }

        private void SaveSelectedButton_Click(object sender, EventArgs e)
        {
            if (MainListView.SelectedIndices == null || MainListView.SelectedIndices.Count == 0 || MainListView.SelectedIndices[0] < 0)
                return;


            List<ModJob> tempjobs = new List<KFreonLib.Scripting.ModMaker.ModJob>();
            foreach (int index in MainListView.SelectedIndices)
                tempjobs.Add(KFreonLib.Scripting.ModMaker.JobList[index]);

            SaveJobs(tempjobs);
        }

        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index < 0)
                return;

            CheckSelectAllPCCsList(true, true);
        }

        private void CheckSelectAllPCCsList(bool SelectAll, bool FromButton)
        {
            bool state = true;

            // KFreon: Check all unless all already checked, then uncheck all
            if (PCCList.CheckedIndices.Count == PCCList.Items.Count)
                state = false;

            this.Invoke(new Action(() => SelectAllButton.Text = (!state ? "Deselect All" : "Select All")));

            if (SelectAll)
                for (int i = 0; i < PCCList.Items.Count; i++)
                    PCCList.SetItemChecked(i, state);

            if (FromButton)
                PCCList_SelectedIndexChanged(null, null);
        }

        private void UpdateJobButton_Click(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index < 0)
                return;

            ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];

            MainProgBar.ChangeProgressBar(0, 1);
            Task.Run(() =>
            {
                bool res = job.UpdateJob(BIOGames, ExecFolder);
                if (res)
                {
                    KFreonLib.Scripting.ModMaker.JobList[index] = job;

                    // KFreon: Update GUI
                    this.BeginInvoke(new Action(() => ScriptPane.Text = job.Script));
                    PCCListRewrite(job, true);
                }

                StatusUpdater.UpdateText(res ? "Job Updated!" : "Update Failed!");
                MainProgBar.ChangeProgressBar(1, 1);
            });
        }

        private void ScriptPane_FocusLost(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices.Count == 0 ? -1 : MainListView.SelectedIndices[0];
            if (index < 0)
                return;

            // KFreon: Update job script
            ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];
            job.Script = ScriptPane.Text;
            KFreonLib.Scripting.ModMaker.JobList[index] = job;
        }

        private void PathBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (PathBox.Text.Length == 0)
                return;



            if (e.KeyCode == Keys.Enter)
            {
                string text = PathBox.Text.Replace("\\", "\\\\");
                PathBox.Text = text;
                int mainInd = MainListView.SelectedIndices[0];
                int secondInd = PCCList.SelectedIndex;
                if (mainInd < 0 || secondInd < 0)
                    return;

                ModJob job = KFreonLib.Scripting.ModMaker.JobList[mainInd];
                job.OrigPCCs[secondInd] = text;

                int ind = -1;
                if ((ind = job.PCCs.IndexOf(job.OrigPCCs[secondInd])) != -1)
                    job.PCCs[ind] = text;


                // KFreon: Reset PCCList
                PCCListRewrite(job);

                // KFreon: Update job script and displayed script
                string script = KFreonLib.Scripting.ModMaker.GenerateTextureScript(ExecFolder, job.PCCs, job.ExpIDs, job.Texname, job.WhichGame, BIOGames[job.WhichGame - 1]);
                job.Script = script;
                ScriptPane.Text = script;

                // KFreon: Save job
                KFreonLib.Scripting.ModMaker.JobList[mainInd] = job;

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control & (e.KeyCode == Keys.Back))
            {
                string text = PathBox.Text.Replace("\\", "\\\\");
                int length = text.LastIndexOfAny(new char[] { '_', ' ', '-', '\\' });
                PathBox.Text = text.Substring(0, length + 1);
                PathBox.SelectionStart = text.Length;
                PathBox.SelectionLength = 0;

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void DisappearPCCEditStuff(bool state)
        {
            if (MainListView.InvokeRequired)
                this.Invoke(new Action(() => DisappearPCCEditStuff(state)));
            else
            {
                UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping trans = new UsefulThings.WinForms.Transitions.TransitionType_CriticalDamping(400);
                UsefulThings.WinForms.Transitions.Transition.run(PCCSplitter, "SplitterDistance", (state) ? PCCSplitter.Height : PCCSplitter.Height - 62, trans);
            }
        }

        private void ExpIDBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (ExpIDBox.Text.Length == 0)
                return;

            if (e.KeyCode == Keys.Enter)
            {
                int mainInd = MainListView.SelectedIndices[0];
                int secondInd = PCCList.SelectedIndex;
                if (mainInd < 0 || secondInd < 0)
                    return;

                ModJob job = KFreonLib.Scripting.ModMaker.JobList[mainInd];

                // KFreon: Get input and check validity
                int res = -1;
                if (int.TryParse(ExpIDBox.Text, out res))
                    job.OrigExpIDs[secondInd] = res;
                else
                {
                    DebugOutput.PrintLn("ExpID input is invalid: " + ExpIDBox.Text);
                    StatusUpdater.UpdateText("ExpID input is invalid: " + ExpIDBox.Text);
                    return;
                }

                int ind = -1;
                if ((ind = job.PCCs.IndexOf(job.OrigPCCs[secondInd])) != -1)
                    job.PCCs[ind] = ExpIDBox.Text;


                // KFreon: Reset PCCList
                PCCListRewrite(job);

                // KFreon: Update job script and displayed script
                string script = KFreonLib.Scripting.ModMaker.GenerateTextureScript(ExecFolder, job.PCCs, job.ExpIDs, job.Texname, job.WhichGame, BIOGames[job.WhichGame - 1]);
                job.Script = script;
                ScriptPane.Text = script;

                // KFreon: Save job
                KFreonLib.Scripting.ModMaker.JobList[mainInd] = job;

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control & (e.KeyCode == Keys.Back))
            {
                string text = ExpIDBox.Text;
                int length = text.LastIndexOfAny(new char[] { '_', ' ', '-', '\\' });
                ExpIDBox.Text = text.Substring(0, length + 1);
                ExpIDBox.SelectionStart = ExpIDBox.Text.Length;
                ExpIDBox.SelectionLength = 0;

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void PCCSplitter_FocusLost(object sender, EventArgs e)
        {
            DisappearPCCEditStuff(true);
        }

        private void ResetScriptButton_Click(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            if (index < 0)
                return;

            if (MessageBox.Show("This will reset the selected jobs' script to the originally loaded version." + Environment.NewLine + "Continue?", "Could be good, could be bad.", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
            {
                ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];
                job.Script = job.OriginalScript;
                KFreonLib.Scripting.ModMaker.JobList[index] = job;

                ScriptPane.Text = job.Script;
            }
        }

        private void CancelationButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to cancel?", "HIGHLY not recommended.", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                cts.Cancel();
        }

        private void ChangePathsButton_Click(object sender, EventArgs e)
        {
            using (PathChanger changer = new PathChanger(BIOGames[0], BIOGames[1], BIOGames[2]))
            {
                if (changer.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
                    return;

                // KFreon: Change paths
                MEExDirecs.SetPaths(changer.PathME1, changer.PathME2, changer.PathME3);

                // KFreon: Colour stuff
                CheckGameState();
            }
        }

        public static async Task<ModMaker> GetCurrentInstance()
        {
            if (currentInstance == null)
            {
                currentInstance = new ModMaker();
                currentInstance.Show();


                await Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(400);
                    while (!currentInstance.LoadButton.Enabled)
                        System.Threading.Thread.Sleep(50);
                });
            }
            return currentInstance;
        }

        private void ScriptPane_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control)
            {
                // KFreon: Index check
                int selectedInd = (MainListView.SelectedIndices.Count == 0) ? -1 : MainListView.SelectedIndices[0];
                if (selectedInd < 0)
                    return;

                // KFreon: Save job script
                ModJob job = KFreonLib.Scripting.ModMaker.JobList[selectedInd];
                ScriptPane.Text = job.Script;
                PCCListRewrite(job);
                KFreonLib.Scripting.ModMaker.JobList[selectedInd] = job;
            }
        }

        private void HelpButton_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.freeforums.org/tutorial-modmaker-2-0-ish-t1427.html");
        }

        private void GameVersionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = MainListView.SelectedIndices[0];
            ModJob job = KFreonLib.Scripting.ModMaker.JobList[index];
            job.WhichGame = GameVersionBox.SelectedIndex;
            KFreonLib.Scripting.ModMaker.JobList[index] = job;
        }

        private void PCCList_CheckChanged(object sender, ItemCheckEventArgs e)
        {
            lock (SuppressLocker)
            {
                if (!SuppressCheckEvent)
                {
                    int pccInd = e.Index;
                    int listInd = MainListView.SelectedIndices[0];
                    if (listInd < 0 || pccInd < 0)
                        return;

                    UpdatePCCList(listInd, KFreonLib.Scripting.ModMaker.JobList[listInd], e.NewValue == CheckState.Checked, pccInd);
                    DisplayDetails(KFreonLib.Scripting.ModMaker.JobList[listInd]);
                }
            }
        }

        private void ExtractDataButton_Click(object sender, EventArgs e)
        {
            int ListInd = MainListView.SelectedIndices[0];
            if (ListInd < 0)
                return;

            if (MainListView.SelectedIndices.Count > 1)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        MainProgBar.ChangeProgressBar(0, MainListView.SelectedIndices.Count);
                        StatusUpdater.UpdateText("Extracting data...");
                        for (int i = 0; i < MainListView.SelectedIndices.Count; i++)
                        {
                            ModJob job = KFreonLib.Scripting.ModMaker.JobList[MainListView.SelectedIndices[i]];
                            string destpath = null;
                            if (job.JobType == "Texture")
                                destpath = Path.Combine(fbd.SelectedPath, job.ObjectName) + ".dds";
                            else
                            {
                                int exp = (job.ExpIDs != null && job.ExpIDs.Count > 0) ? job.ExpIDs[0] : 0;
                                destpath = Path.Combine(fbd.SelectedPath, job.ObjectName) + "_EXP- " + exp + ".bin";
                            }

                            // KFreon: If it exists already, DO NOT overwrite. Just rename.
                            int count = 0;
                            string newpath = destpath;
                            while (true)  // KFreon: Not using File.Exists as condition cos it can be a bit weird that way
                            {
                                if (!File.Exists(newpath))
                                    break;
                                
                                newpath = Path.Combine(Path.GetDirectoryName(destpath), Path.GetFileNameWithoutExtension(destpath) + count + Path.GetExtension(destpath));
                            }

                            File.WriteAllBytes(newpath, job.data);
                            MainProgBar.IncrementBar();
                        }
                        StatusUpdater.UpdateText("Data extracted!");
                    }
                }
            }
            else
            {
                ModJob job = KFreonLib.Scripting.ModMaker.JobList[ListInd];


                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "Select destination";
                    sfd.Filter = job.JobType == "TEXTURE" ? "DirectX images|*.dds" : "Meshes/etc|*.bin";

                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        StatusUpdater.UpdateText("Saving data...");
                        MainProgBar.ChangeProgressBar(0, 1);
                        File.WriteAllBytes(sfd.FileName, job.data);
                        MainProgBar.ChangeProgressBar(1, 1);
                        StatusUpdater.UpdateText("Data extracted!");
                    }
                }
            }
        }


        // KFreon: Searches jobs given search string
        public List<int> SearchEngine(string searchString)
        {
            List<int> retval = new List<int>();
            string searchElement = null;

            // KFreon: Only look if search is provided
            if (!String.IsNullOrEmpty(searchString))
            {
                searchElement = searchString.Substring(2).ToUpperInvariant();
                Predicate<ModJob> Searcher = j =>
                {
                    if (j.Name.ToUpperInvariant().Contains(searchElement))
                        return true;

                    if (j.Script.ToUpperInvariant().Contains(searchElement))
                        return true;

                    if (j.PCCs.Where(h => h.ToUpperInvariant().Contains(searchElement)).Count() > 0)
                        return true;

                    return false;
                };


                // KFreon: If search modifier
                if (searchString[0] == '-')
                {
                    switch (searchString[1])
                    {
                        case 's':   // KFreon: Scripts
                            Searcher = j => j.Script.Contains(searchElement);
                            break;
                        case 'j':   // KFreon: Jobs
                            Searcher = j => j.Name.Contains(searchElement);
                            break;
                        case 'f':   // KFreon: Files/pccs
                            Searcher = j => j.PCCs.Where(h => h.Contains(searchElement)).Count() > 0;
                            break;
                        default:
                            break;
                    }
                }


                // KFreon: Look at all jobs
                for (int i = 0; i < KFreonLib.Scripting.ModMaker.JobList.Count; i++)
                {
                    ModJob job = KFreonLib.Scripting.ModMaker.JobList[i];

                    if (Searcher(job))
                        retval.Add(i);
                }
            }

            SearchResults = retval;

            return retval;
        }


        // KFreon: Select item during search
        public void SelectThings(int thing)
        {
            // KFreon: Deselect everything else
            for (int i = 0; i < MainListView.SelectedIndices.Count; i++)
                MainListView.Items[MainListView.SelectedIndices[i]].Selected = false;

            // KFreon: Focus on MainListView
            MainListView.Focus();

            // KFreon: Select specified element
            MainListView.Items[thing].Selected = true;

            // KFreon: Show details
            DoDetailsShowing();
        }

        private void CreateFromPCCDiffButton_Click(object sender, EventArgs e)
        {
            // KFreon: This is all just renamed stuff from WV's work. No credit to me.


            // KFreon: Get pcc's
            IPCCObject basePCC = null;
            IPCCObject modifiedPCC = null;
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PCC Files|*.pcc";
                ofd.Title = "Select base (unmodified) pcc";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                basePCC = new ME3PCCObject(ofd.FileName);

                ofd.Title = "Select modified pcc";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                modifiedPCC = new ME3PCCObject(ofd.FileName);
            }


            // KFreon: Compare PCC's and build script
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            string script = File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");

            // KFreon: Set pcc name
            var bits = basePCC.pccFileName.Split('\\');
            script.Replace("**m1**", bits.Last());

            if (basePCC.NameCount != modifiedPCC.NameCount)
            {
                StringBuilder names = new StringBuilder();
                foreach (var name in modifiedPCC.Names)
                    if (!basePCC.Names.Contains(name))
                        names.AppendLine("AddName(\"" + name + "\");");
                script = script.Replace("**m2**", names.ToString());
            }
            else
                script = script.Replace("**m2**", "\\ No names to add");

            StringBuilder exports = new StringBuilder();
            using(MemoryStream data = new MemoryStream())
            {
                for (int i = 0; i < basePCC.ExportCount; i++)
                {
                    if (!basePCC.Exports[i].Data.SequenceEqual(modifiedPCC.Exports[i].Data))
                    {
                        int offset = (int)data.Position;
                        data.WriteBytes(modifiedPCC.Exports[i].Data);
                        exports.AppendLine("ReplaceData(" + i + ", " + offset + ", " + modifiedPCC.Exports[i].Data.Length + ");");
                    }
                }
                script = script.Replace("**m3**", exports.ToString());

                ModJob job = new ModJob();
                job.data = data.ToArray();
                job.Name = "PCC Replacement Job for " + bits.Last();
                job.Script = script;
                KFreonLib.Scripting.ModMaker.JobList.Add(job);
            }

            Refresh();
        }

        private void FindOther_DLC_Click(object sender, EventArgs e)
        {
            if (KFreonLib.Scripting.ModMaker.JobList == null || KFreonLib.Scripting.ModMaker.JobList.Count == 0)
                return;

            // KFreon: For each NON TEXTURE job, search for additional pcc's with the same name in basegame and DLC's
            foreach (ModJob job in KFreonLib.Scripting.ModMaker.JobList)
            {
                if (job.JobType == "Texture" || job.ExpIDs == null || job.WhichGame == -1 || 
                    job.ExpIDs.Count == 0 || job.PCCs == null || job.PCCs.Count == 0 || job.PCCs.Count > 1)  // KFreon: Skip jobs that already have more than 1 pcc i.e. Someone clicked this button twice.
                    continue;

                string basepath = ME3Directory.cookedPath;
                if (job.WhichGame == 2)
                    basepath = ME2Directory.cookedPath;
                else if (job.WhichGame == 1)
                    basepath = ME1Directory.cookedPath;

                string fullPCCPath = Path.Combine(basepath, job.PCCs[0]);
                fullPCCPath = fullPCCPath.ToLower().Replace("cookedpcconsole\\cookedpcconsole", "cookedpcconsole");

                string pccName = Path.GetFileName(fullPCCPath);

                if(!File.Exists(fullPCCPath))
                    DebugOutput.PrintLn("Looking for additional PCC's: Can't find original PCC: " + job.PCCs[0]);
                else
                {
                    int basePathLength = -1;
                    
                    List<string> OtherFiles = new List<string>();
                    List<int> OtherExpIDs = new List<int>();

                    // KFreon: Get name of export to look for
                    IPCCObject pcc = KFreonLib.PCCObjects.Creation.CreatePCCObject(fullPCCPath, job.WhichGame);
                    IExportEntry exp = pcc.Exports[job.ExpIDs[0]];
                    string exportName = exp.ObjectName;

                    // KFreon: Get list of files to look through based on detected game version
                    IEnumerable<string> files = null;
                    switch(job.WhichGame)
                    {
                        case 1:
                            files = ME1Directory.Files.Where(f => f != fullPCCPath && f.Contains(pccName, StringComparison.OrdinalIgnoreCase));
                            basePathLength = ME1Directory.cookedPath.Length;
                            break;
                        case 2:
                            files = ME2Directory.Files.Where(f => f != fullPCCPath && f.Contains(pccName, StringComparison.OrdinalIgnoreCase));
                            basePathLength = ME2Directory.cookedPath.Length;
                            break;
                        case 3:
                            files = ME3Directory.Files.Where(f => f != fullPCCPath && f.Contains(pccName, StringComparison.OrdinalIgnoreCase));
                            basePathLength = ME3Directory.cookedPath.Length;
                            break;
                    }

                    // KFreon: Probably only 1, but just in case
                    foreach (var file in files)
                    {
                        IPCCObject temppcc = KFreonLib.PCCObjects.Creation.CreatePCCObject(file, job.WhichGame);
                        for (int i = 0; i < temppcc.Exports.Count; i++) 
                        {
                            if (temppcc.Exports[i].ObjectName == exportName)
                            {
                                OtherFiles.Add(file.Remove(0, basePathLength)); // KFreon: Remove cooked part so it's relative to it. It's not always cooked...
                                OtherExpIDs.Add(i);
                            }
                        }
                    }


                    if (OtherFiles.Count != 0)
                    {
                        job.PCCs.AddRange(OtherFiles);
                        job.ExpIDs.AddRange(OtherExpIDs);
                    }
                }
            }
        }
    }
}
