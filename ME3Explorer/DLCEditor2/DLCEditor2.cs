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
using KFreonLib.Helpers.LiquidEngine;

namespace ME3Explorer.DLCEditor2
{
    public partial class DLCEditor2 : Form
    {
        DLCPackage DLC;
        bool automated = false; //Mod Manager 3 automator



        public DLCEditor2()
        {
            InitializeComponent();

            //FemShep's Mod Manager 3 automator for DLCEditor2.
            DebugOutput.StartDebugger("DLC Editor 2");
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 2)
            {
                try
                {
                    string cmdCommand = arguments[1];
                    if (cmdCommand.Equals("-dlcinject", StringComparison.Ordinal))
                    {
                        if (arguments.Length % 2 != 1)
                        {
                            MessageBox.Show("Wrong number of arguments for automated DLC injection:\nSyntax is: <exe> -dlcinject SFARPATH filetoreplace newfile filetoreplace2 newfile...", "ME3 DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        System.Diagnostics.Debug.WriteLine("Received injection signal from commandline!");
                        string dlcFileName = arguments[2];
                        int numfiles = (arguments.Length - 3) / 2;
                        System.Diagnostics.Debug.WriteLine("Have " + arguments.Length + " arguments, so there is " + numfiles + "file replacements to do.");

                        string[] filesToReplace = new String[numfiles];
                        string[] newFiles = new String[numfiles];

                        int argnum = 3; //starts at 3
                        for (int i = 0; i < filesToReplace.Length; i++)
                        {
                            filesToReplace[i] = arguments[argnum];
                            argnum++;
                            newFiles[i] = arguments[argnum];
                            argnum++;
                        }
                        automated = true;
                        foreach (string s in filesToReplace)
                        {
                            System.Diagnostics.Debug.WriteLine(s);
                        }
                        if (File.Exists(dlcFileName))
                        {
                            openSFAR(dlcFileName);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("dlcFilename does not exist: " + dlcFileName);
                            MessageBox.Show("Failed to autoinject: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    else
                        throw new Exception("Invalid arguments to this program.");
                }
                catch (FileNotFoundException exc)
                {
                    MessageBox.Show("Failed to open DLCEditor2 Autoinjection with the specified parameters.\n\nReason: 2 parameters were passed, so we expected -dlcinject. But it was not found!", "ME3 DLC Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(1);
                    Application.Exit();
                }
                /*catch (Exception exc)
                {
                    MessageBox.Show("Failed to open DLC Injection tool."+Environment.NewLine+"Reason: "+exc.ToString(), "ME3 DLC Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                    Application.Exit();
                }*/
                System.Diagnostics.Debug.WriteLine("Finished DLCInject job, exiting");
                Environment.Exit(0);
                Application.Exit();
            }
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

        private void openSFAR(String filename)
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

        private DLCPackage openSFAR2(String filename)
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
            hb1.ByteProvider = new DynamicByteProvider(DLC.DecompressEntry(n).ToArray());
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string h = "";
            foreach (byte b in DLCPackage.TOCHash)
                h += b.ToString("X2");
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter string to search", "ME3 Explorer", h, 0, 0);
            selectSearchedElement(result);
        }

        private void selectSearchedElement(String query)
        {
            if (query == "")
                return;
            TreeNode SelectedNode = SearchNode(query, treeView1.Nodes[0]);
            if (SelectedNode != null)
            {
                this.treeView1.SelectedNode = SelectedNode;
                this.treeView1.SelectedNode.Expand();
                this.treeView1.Select();
            };
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
                };
                if (StartNode.Nodes.Count != 0)
                {
                    node = SearchNode(SearchText, StartNode.Nodes[0]);//Recursive Search
                    if (node != null)
                    {
                        break;
                    };
                };
                StartNode = StartNode.NextNode;
            };
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
                DLC.AddFileQuick(d.FileName, path);
                DLC = new DLCPackage(DLC.MyFileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                MessageBox.Show("Done.");
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
            MessageBox.Show("Done.");
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
                MemoryTributary m = DLC.DecompressEntry(n);
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                DLC = new DLCPackage(DLC.MyFileName);
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(DLC.ToTree());
                MessageBox.Show("Done.");
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

        private void replaceFile(String filename, int n)
        {
            DLC.ReplaceEntry(filename, n);
            DLC = new DLCPackage(DLC.MyFileName);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(DLC.ToTree());
            if (!automated)
            {
                MessageBox.Show("Done.");
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
                MessageBox.Show("Done.");
            }
        }

        private void unpackSFARToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoTOC.AutoTOC toc = new AutoTOC.AutoTOC();
            if (DLC == null || DLC.Files == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter pattern for unpacking, keep default to unpack all", "ME3 Explorer", "pcc; tfc; afc; cnd; tlk; bin; bik; dlc", 0, 0);
            if (result == "")
                return;
            DebugOutput.PrintLn("result : " + result);
            result = result.Trim();
            if (result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1);
            string[] patt = result.Split(';');
            string file = DLC.MyFileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            string gamebase = Path.GetDirectoryName(t4);    //Mass Effect3
            DebugOutput.PrintLn("Extracting DLC with gamebase : " + gamebase);
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
                            string outpath = gamebase + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                            using (FileStream fs = new FileStream(outpath, FileMode.CreateNew))
                                DLC.DecompressEntry(i).WriteTo(fs);
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                DLC.DeleteEntry(Indexes);
            }

            // AutoTOC
            DebugOutput.PrintLn("Updating DLC's PCConsoleTOC.bin");
            List<string> FileNames = toc.GetFiles(t2 + "\\");
            List<string> tet = new List<string>(t2.Split('\\'));
            string remov = String.Join("\\", tet.ToArray());
            for (int i = 0; i < FileNames.Count; i++)
                FileNames[i] = FileNames[i].Substring(remov.Length + 1);
            string[] ts = t2.Split('\\');
            tet.Clear();
            tet.AddRange(ts);
            string basepath = String.Join("\\", tet.ToArray()) + '\\';
            string tocfile = t2 + "\\PCConsoleTOC.bin";
            toc.CreateTOC(basepath, tocfile, FileNames.ToArray());
            MessageBox.Show("Done.");
        }

        private void unpackSFAR(DLCPackage DLC)
        {
            AutoTOC.AutoTOC toc = new AutoTOC.AutoTOC();
            if (DLC == null || DLC.Files == null)
                return;
            string result = "pcc; tfc; afc; cnd; tlk; bin; bik; dlc";
            if (result == "")
                return;
            result = result.Trim();
            if (result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1);
            string[] patt = result.Split(';');
            string file = DLC.MyFileName;                   //full path
            string t1 = Path.GetDirectoryName(file);        //cooked
            string t2 = Path.GetDirectoryName(t1);          //DLC_Name
            string t3 = Path.GetDirectoryName(t2);          //DLC
            string t4 = Path.GetDirectoryName(t3);          //BioGame
            string gamebase = Path.GetDirectoryName(t4);    //Mass Effect3
            DebugOutput.PrintLn("Extracting DLC with gamebase : " + gamebase);
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
                            string outpath = gamebase + relPath;
                            DebugOutput.PrintLn("Extracting file #" + i.ToString("d4") + ": " + outpath);
                            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                            if (!File.Exists(outpath))
                                using (FileStream fs = new FileStream(outpath, FileMode.Create))
                                    DLC.DecompressEntry(i).WriteTo(fs);
                            Indexes.Add(i);
                            Application.DoEvents();
                            break;
                        }
                }
                DLC.DeleteEntry(Indexes);
            }

            // AutoTOC
            DebugOutput.PrintLn("Updating DLC's PCConsoleTOC.bin");
            List<string> FileNames = toc.GetFiles(t2 + "\\");
            List<string> tet = new List<string>(t2.Split('\\'));
            string remov = String.Join("\\", tet.ToArray());
            for (int i = 0; i < FileNames.Count; i++)
                FileNames[i] = FileNames[i].Substring(remov.Length + 1);
            string[] ts = t2.Split('\\');
            tet.Clear();
            tet.AddRange(ts);
            string basepath = String.Join("\\", tet.ToArray()) + '\\';
            string tocfile = t2 + "\\PCConsoleTOC.bin";
            toc.CreateTOC(basepath, tocfile, FileNames.ToArray());
            DebugOutput.PrintLn("DLC Done.");
        }

        private void unpackAllDLCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExtractAllDLC();
        }

        public void ExtractAllDLC()
        {
            string DLCBasePath = ME3Directory.DLCPath;
            DebugOutput.PrintLn("DLC Path: " + DLCBasePath);
            List<string> files = new List<string>(Directory.EnumerateFiles(DLCBasePath, "Default.sfar", SearchOption.AllDirectories));
            foreach (string file in files)
            {
                if (file != "")
                {
                    DLCPackage DLC = openSFAR2(file);
                    unpackSFAR(DLC);

                }
            }
            DebugOutput.PrintLn("All DLCs Done.");
        }

        public string GetRelativePath(string DLCpath)
        {
            return DLCpath.Replace("/", "\\");
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void hb1_Click(object sender, EventArgs e)
        {

        }
    }
}
