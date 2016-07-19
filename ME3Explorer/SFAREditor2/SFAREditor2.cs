using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using UsefulThings;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer
{
    public partial class SFAREditor2 : Form
    {
        string previousTerm = "";
        DLCPackage DLC;
        bool automated = false; //Mod Manager 3 automator
        private string autoUnpackFolder;

        public SFAREditor2()
        {
            InitializeComponent();

            //FemShep's Mod Manager 3 automator for DLCEditor2.
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 2)
            {
                try
                {
                    string cmdCommand = arguments[1];
                    if (cmdCommand.Equals("-dlcinject", StringComparison.Ordinal))
                    {
                        if (arguments.Length % 2 != 1 || arguments.Length < 5)
                        {
                            MessageBox.Show("Wrong number of arguments for the -dlcinject switch.:\nSyntax is: <exe> -dlcinject SFARPATH SEARCHTERM NEWFILEPATH [SEARCHTERM2 NEWFILEPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        string dlcFileName = arguments[2];
                        int numfiles = (arguments.Length - 3) / 2;

                        string[] filesToReplace = new string[numfiles];
                        string[] newFiles = new string[numfiles];

                        int argnum = 3; //starts at 3
                        for (int i = 0; i < filesToReplace.Length; i++)
                        {
                            filesToReplace[i] = arguments[argnum];
                            argnum++;
                            newFiles[i] = arguments[argnum];
                            argnum++;
                        }
                        automated = true;
                        if (File.Exists(dlcFileName))
                        {
                            openSFAR(dlcFileName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("dlcFilename does not exist: " + dlcFileName);
                            MessageBox.Show("Failed to autoinject: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        //SFAR was opened.
                        //Now we search for the element to replace so it is selected...
                        for (int i = 0; i < numfiles; i++)
                        {
                            selectSearchedElement(filesToReplace[i]);
                            //the element is now selected, hopefully.
                            TreeNode t = treeView1.SelectedNode;
                            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                            {
                                MessageBox.Show("DLCEditor2 automator encountered an error: the file to replace does not exist or the tree has not been initialized.", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            replaceFile(newFiles[i], t.Index);
                        }
                    }
                    else if (cmdCommand.Equals("-dlcextract", StringComparison.Ordinal))
                    {
                        if (arguments.Length != 5)
                        {
                            //-2 for me3explorer & -dlcextract
                            MessageBox.Show("Wrong number of arguments for the -dlcinject switch.:\nSyntax is: <exe> -dlcextract SFARPATH SEARCHTERM EXTRACTIONPATH", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        automated = true;
                        string dlcFileName = arguments[2];
                        string searchTerm = arguments[3];
                        string extractionPath = arguments[4];
                        if (File.Exists(dlcFileName))
                        {
                            openSFAR(dlcFileName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("dlcFilename does not exist: " + dlcFileName);
                            MessageBox.Show("Failed to autoextract: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        //SFAR was opened.
                        //Now we search for the element to extract so it is selected...
                        selectSearchedElement(searchTerm);
                        //the element is now selected, hopefully.
                        TreeNode t = treeView1.SelectedNode;
                        if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                        {
                            MessageBox.Show("DLCEditor2 extraction automator encountered an error:\nThe file to replace does not exist or the tree has not been initialized.", "DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        extractFile(t.Index, extractionPath);
                    }
                    else if (cmdCommand.Equals("-dlcaddfiles", StringComparison.Ordinal))
                    {
                        if (arguments.Length % 2 != 1 || arguments.Length < 5)
                        {
                            MessageBox.Show("Wrong number of arguments for the -dlcaddfiles switch.:\nSyntax is: <exe> -dlcinject SFARPATH INTERNALPATH NEWFILEPATH [INTERNALPATH2 NEWFILEPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }

                        automated = true;
                        string dlcFileName = arguments[2];

                        int numfiles = (arguments.Length - 3) / 2;
                        string[] internalPaths = new string[numfiles];
                        string[] sourcePaths = new string[numfiles];

                        int argnum = 3; //starts at 3
                        for (int i = 0; i < internalPaths.Length; i++)
                        {
                            internalPaths[i] = arguments[argnum];
                            argnum++;
                            sourcePaths[i] = arguments[argnum];
                            argnum++;
                        }

                        if (File.Exists(dlcFileName))
                        {
                            openSFAR(dlcFileName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("DLC does not exist: " + dlcFileName);
                            MessageBox.Show("Failed to autoadd: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        //SFAR was opened.    
                        for (int i = 0; i < internalPaths.Length; i++)
                        {
                            System.Diagnostics.Debug.WriteLine("Adding file quick: " + sourcePaths[i] + " " + internalPaths[i]);
                            DLC.AddFileQuick(sourcePaths[i], internalPaths[i]);
                            DLC = new DLCPackage(DLC.MyFileName);
                            treeView1.Nodes.Clear();
                            treeView1.Nodes.Add(DLC.ToTree());
                        }
                    }
                    else if (cmdCommand.Equals("-dlcremovefiles", StringComparison.Ordinal))
                    {
                        if (arguments.Length < 4)
                        {
                            //-2 for me3explorer & -dlcextract
                            MessageBox.Show("Wrong number of arguments for the -dlcremovefiles switch.:\nSyntax is: <exe> -dlcinject SFARPATH INTERNALPATH [INTERNALPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        automated = true;
                        string dlcFileName = arguments[2];

                        int numfiles = (arguments.Length - 3);

                        string[] filesToRemove = new string[numfiles];

                        int argnum = 3; //starts at 3
                        for (int i = 0; i < filesToRemove.Length; i++)
                        {
                            filesToRemove[i] = arguments[argnum];
                            argnum++;
                        }

                        if (File.Exists(dlcFileName))
                        {
                            openSFAR(dlcFileName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("DLC does not exist: " + dlcFileName);
                            MessageBox.Show("Failed to autoremove: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        //SFAR was opened.         
                        for (int i = 0; i < filesToRemove.Length; i++)
                        {
                            selectSearchedElement(filesToRemove[i]);
                            //the element is now selected, hopefully.
                            TreeNode t = treeView1.SelectedNode;
                            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                            {
                                MessageBox.Show("DLCEditor2 file removal automator encountered an error:\nThe file to remove does not exist or the tree has not been initialized.", "DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Application.Exit();
                                return;
                            }
                            DLC.DeleteEntry(t.Index);
                            DLC = new DLCPackage(DLC.MyFileName);
                            treeView1.Nodes.Clear();
                            treeView1.Nodes.Add(DLC.ToTree());
                        }
                    }
                    else if (cmdCommand.Equals("-dlcunpack", StringComparison.Ordinal) || cmdCommand.Equals("-dlcunpack-nodebug", StringComparison.Ordinal))
                    {
                        if (arguments.Length != 4)
                        {
                            MessageBox.Show("Wrong number of arguments for automated DLC unpacking:\nSyntax is: <exe> -dlcinject SFARPATH EXTRACTIONPATH", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                       
                        string sfarPath = arguments[2];
                        autoUnpackFolder = arguments[3];

                        automated = true;
                        if (File.Exists(sfarPath))
                        {
                            openSFAR(sfarPath);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("DLC does not exist: " + sfarPath);
                            MessageBox.Show("Failed to autounpack: DLC file does not exist: " + sfarPath, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        }
                        //SFAR was opened.
                        if (cmdCommand.Equals("-dlcunpack"))
                        {
                            DebugOutput.StartDebugger("DLC Editor 2"); //open debugging window since this operation takes a long time. The main debugger won't start as this will exit before that code can be reached
                        }
                        //Simulate Unpack operation click.
                        unpackSFARToolStripMenuItem.PerformClick();
                    }
                }
                catch (FileNotFoundException exc)
                {
                    MessageBox.Show("Failed to run DLCEditor2 Automator with the specified parameters.\n\nA file not found error occured while trying to automate a task.\n" + exc.Message, "DLC Editor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(1);
                    Application.Exit();
                }
                Environment.Exit(0);
                Application.Exit();
            }
            DebugOutput.StartDebugger("DLC Editor 2"); //open debugging window AFTER automation. Otherwise it pops up all weirdlike.
        }

        private void openSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                openSFAR(d.FileName);
            }
        }

        private void openSFAR(string filename)
        {
            try
            {
                BitConverter.IsLittleEndian = true;
                DLC = new DLCPackage(filename);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private DLCPackage openSFAR2(string filename)
        {
            try
            {
                BitConverter.IsLittleEndian = true;
                DLC = new DLCPackage(filename);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return DLC;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter string to search", "ME3 Explorer", previousTerm, 0, 0);
            previousTerm = result;
            selectSearchedElement(result);
        }

        private void selectSearchedElement(string query)
        {
            if (query == "")
                return;
            TreeNode SelectedNode = SearchNode(query, treeView1.Nodes[0]);
            if (SelectedNode != null)
            {
                this.treeView1.SelectedNode = SelectedNode;
                this.treeView1.SelectedNode.Expand();
                this.treeView1.Select();
            }
        }

        private TreeNode SearchNode(string SearchText, TreeNode StartNode)
        {
            TreeNode node = null;
            while (StartNode != null)
            {
                if (StartNode.Text.ToLower().Contains(SearchText.ToLower()))
                {
                    node = StartNode;
                    break;
                }
                if (StartNode.Nodes.Count != 0)
                {
                    node = SearchNode(SearchText, StartNode.Nodes[0]);//Recursive Search
                    if (node != null)
                    {
                        break;
                    }
                }
                StartNode = StartNode.NextNode;
            }
            return node;
        }

        private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC == null)
                return;
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.*|*.*";
            string p = Path.GetDirectoryName(DLC.Files[0].FileName) + "\\";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = p + Path.GetFileName(d.FileName);
                path = path.Replace('\\', '/');
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter the path with name that the file will have inside the DLC", "ME3 Explorer", path, 0, 0);
                if (result == "")
                    return;
                path = result;
                System.Diagnostics.Debug.WriteLine("Adding file quick: " + d.FileName + " " + path);

                DLC.AddFileQuick(d.FileName, path);
                DLC = new DLCPackage(DLC.MyFileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                SearchNode(result, treeView1.Nodes[0]);
                MessageBox.Show("File added.");
            }
        }

        private void rebuildSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DLC == null)
                return;
            DebugOutput.StartDebugger("DLCEditor2");
            DLC.ReBuild();
            DLC = new DLCPackage(DLC.MyFileName);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(DLC.ToTree());
            MessageBox.Show("SFAR Rebuilt.");
        }

        private void extractSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
            SaveFileDialog d = new SaveFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                extractFile(n, d.FileName);
            }
        }

        private void extractFile(int n, string exportLocation)
        {
            MemoryStream m = DLC.DecompressEntry(n);
            FileStream fs = new FileStream(exportLocation, FileMode.Create, FileAccess.Write);
            fs.Write(m.ToArray(), 0, (int)m.Length);
            fs.Close();
            //DLC = new DLCPackage(DLC.MyFileName);
            //treeView1.Nodes.Clear();
            //treeView1.Nodes.Add(DLC.ToTree());
            if (!automated)
            {
                MessageBox.Show("File extracted.");
            }
        }

        private void replaceSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;

            OpenFileDialog d = new OpenFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                replaceFile(d.FileName, n);
            }
        }

        private void replaceFile(string filename, int n)
        {
            DLC.ReplaceEntry(filename, n);

            DLC = new DLCPackage(DLC.MyFileName);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(DLC.ToTree());
            SearchNode(filename, treeView1.Nodes[0]);
            if (!automated)
            {
                MessageBox.Show("File Replaced.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Injection complete.");
            }
        }

        private void createReplaceModJobToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            int n = t.Index;
            OpenFileDialog d = new OpenFileDialog();
            string filename = DLC.Files[n].FileName;
            d.Filter = Path.GetFileName(filename) + " | " + Path.GetFileName(filename);
            d.FileName = Path.GetFileName(filename);
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                string file = DLC.MyFileName;               //full path
                string t1 = Path.GetDirectoryName(file);    //cooked
                string t2 = Path.GetDirectoryName(t1);      //DLC_Name
                string t3 = Path.GetDirectoryName(t2);      //DLC
                t3 += "\\";
                file = file.Substring(t3.Length);
                file = file.Replace("\\", "\\\\");
                mj.data = File.ReadAllBytes(d.FileName);
                mj.Name = "DLC File Replacement in DLC \"" + file + "\" with File #" + n + " with " + mj.data.Length + " bytes of data";
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_DLCReplace.txt");
                template = template.Replace("**m1**", n.ToString());
                template = template.Replace("**m2**", file);
                mj.Script = template;
                KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                MessageBox.Show("Done.");
            }
        }

        private void deleteSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DLC == null || t == null || t.Parent == null || t.Parent.Text != "FileEntries")
                return;
            if (MessageBox.Show("Are you sure to delete this File?", "DLCEditor2", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int n = t.Index;
                DLC.DeleteEntry(n);
                DLC = new DLCPackage(DLC.MyFileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                MessageBox.Show("File Deleted.");
            }
        }

        private void unpackSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoTOC.AutoTOC toc = new AutoTOC.AutoTOC();
            if (DLC == null || DLC.Files == null)
                return;
            string result = "pcc; tfc; afc; cnd; tlk; bin; bik; dlc";
            string unpackFolder;
            if (!automated) //if automated, just do everything. otherwise prompt user
            {
                result = Microsoft.VisualBasic.Interaction.InputBox("Please enter pattern for unpacking, keep default to unpack everything.", "ME3 Explorer", "pcc; tfc; afc; cnd; tlk; bin; bik; dlc", 0, 0);

                if (result == "")
                    return;
                DebugOutput.PrintLn("result : " + result);
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                dialog.EnsurePathExists = true;
                dialog.Title = "Choose a folder to unpack to. Select ME3 directory to unpack to proper folder";
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    unpackFolder = dialog.FileName;
                else
                    return;
            } else
            {
                unpackFolder = autoUnpackFolder;
            }
            if (!unpackFolder.EndsWith("\\"))
                unpackFolder = unpackFolder + "\\";
            DebugOutput.PrintLn("Extracting DLC to : " + unpackFolder);

            result = result.Trim();
            if (result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1);
            string[] patt = result.Split(';');
            string file = DLC.MyFileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            DebugOutput.PrintLn("DLC name : " + t2);
            if (DLC.Files.Length > 1)
            {
                List<int> Indexes = new List<int>();
                for (int i = 0; i < DLC.Files.Length; i++)
                {
                    string DLCpath = DLC.Files[i].FileName;
                    for (int j = 0; j < patt.Length; j++)
                        if (DLCpath.ToLower().EndsWith(patt[j].Trim().ToLower()) && patt[j].Trim().ToLower() != "")
                        {
                            string relPath = GetRelativePath(DLCpath);
                            string outpath = unpackFolder + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                            using (FileStream fs = new FileStream(outpath, FileMode.Create))
                                DLC.DecompressEntry(i).WriteTo(fs);
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                DLC.DeleteEntries(Indexes);
            }

            // AutoTOC
            DebugOutput.PrintLn("Updating DLC's PCConsoleTOC.bin");
            List<string> FileNames = toc.GetFiles(t2 + "\\");
            List<string> tet = new List<string>(t2.Split('\\'));
            string remov = string.Join("\\", tet.ToArray());
            for (int i = 0; i < FileNames.Count; i++)
                FileNames[i] = FileNames[i].Substring(remov.Length + 1);
            string[] ts = t2.Split('\\');
            tet.Clear();
            tet.AddRange(ts);
            string basepath = string.Join("\\", tet.ToArray()) + '\\';
            string tocfile = t2 + "\\PCConsoleTOC.bin";
            toc.CreateTOC(basepath, tocfile, FileNames.ToArray());
            if (!automated)
            {
                MessageBox.Show("SFAR Unpacked.");
            }
        }

        private void unpackSFAR(DLCPackage dlc)
        {
            AutoTOC.AutoTOC toc = new AutoTOC.AutoTOC();
            if (dlc == null || dlc.Files == null)
                return;
            string result = "pcc; tfc; afc; cnd; tlk; bin; bik; dlc";
            if (result == "")
                return;
            result = result.Trim();
            if (result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1);
            string[] patt = result.Split(';');
            string file = dlc.MyFileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            string gamebase = Path.GetDirectoryName(t4);    //Mass Effect3
            DebugOutput.PrintLn("Extracting DLC with gamebase : " + gamebase);
            DebugOutput.PrintLn("DLC name : " + t2);
            if (dlc.Files.Length > 1)
            {
                List<int> Indexes = new List<int>();
                for (int i = 0; i < dlc.Files.Length; i++)
                {
                    string DLCpath = dlc.Files[i].FileName;
                    for (int j = 0; j < patt.Length; j++)
                        if (DLCpath.ToLower().EndsWith(patt[j].Trim().ToLower()) && patt[j].Trim().ToLower() != "")
                        {
                            string relPath = GetRelativePath(DLCpath);
                            string outpath = gamebase + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                            if (!File.Exists(outpath))
                                using (FileStream fs = new FileStream(outpath, FileMode.Create))
                                    dlc.DecompressEntry(i).WriteTo(fs);
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                dlc.DeleteEntries(Indexes);
            }

            // AutoTOC
            DebugOutput.PrintLn("Updating DLC's PCConsoleTOC.bin");
            List<string> FileNames = toc.GetFiles(t2 + "\\");
            List<string> tet = new List<string>(t2.Split('\\'));
            string remov = string.Join("\\", tet.ToArray());
            for (int i = 0; i < FileNames.Count; i++)
                FileNames[i] = FileNames[i].Substring(remov.Length + 1);
            string[] ts = t2.Split('\\');
            tet.Clear();
            tet.AddRange(ts);
            string basepath = string.Join("\\", tet.ToArray()) + '\\';
            string tocfile = t2 + "\\PCConsoleTOC.bin";
            toc.CreateTOC(basepath, tocfile, FileNames.ToArray());
            DebugOutput.PrintLn("DLC Done.");
        }

        private void unpackAllDLCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExtractAllDLC();
        }

        public bool ExtractAllDLC()
        {
            bool retval = true;
            string DLCBasePath = ME3Directory.DLCPath;
            DebugOutput.PrintLn("DLC Path: " + DLCBasePath);
            List<string> files = new List<string>(Directory.EnumerateFiles(DLCBasePath, "Default.sfar", SearchOption.AllDirectories));
            foreach (string file in files)
            {
                string[] parts = file.Split('\\');
                if (parts[parts.Length-2].ToLower() != "cookedpcconsole")
                {
                    DebugOutput.PrintLn(file + "  doesn't look correct. SFAR in the wrong place?");
                    retval = false;
                    continue;
                }


                if (file != "")
                {
                    unpackSFAR(openSFAR2(file));

                }
            }
            DebugOutput.PrintLn("All DLCs Done.");
            return retval;
        }

        public string GetRelativePath(string DLCpath)
        {
            return DLCpath.Replace("/", "\\");
        }

        private void updateTOCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLC.UpdateTOCbin();
            DebugOutput.PrintLn("Done.");
            MessageBox.Show("Done.");
        }

        private void updateTOCAndRebuildToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLC.UpdateTOCbin(true);
            DebugOutput.PrintLn("Done.");
            MessageBox.Show("Done.");
        }
    }
}
