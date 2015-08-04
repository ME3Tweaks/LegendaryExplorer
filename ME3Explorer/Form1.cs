using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.UnrealHelper;
using KFreonLib.MEDirectories;
using KFreonLib.Scripting;
using KFreonLib.Debugging;
using System.Diagnostics;
using System.Reflection;

namespace ME3Explorer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        Languages lang;

        private void decompressorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form2 decomp = new Form2();
            lang.SetLang(decomp);
            decomp.MdiParent = this;
            decomp.WindowState = FormWindowState.Maximized;
            decomp.Show();
            taskbar.AddTool(decomp, imageList1.Images[5]);
        }

        private void conditionalsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Conditionals con = new Conditionals();
            con.MdiParent = this;
            lang.SetLang(con);
            con.WindowState = FormWindowState.Maximized;
            con.Show();
            taskbar.AddTool(con, imageList1.Images[6]);
        }

        private void dLCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLCExplorer expl = new DLCExplorer();
            lang.SetLang(expl);
            expl.MdiParent = this;
            expl.WindowState = FormWindowState.Maximized;
            expl.Show();
            taskbar.AddTool(expl, imageList1.Images[12]);
        }

        private void languageSelectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Language_Selector());
        }

        private void aFCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AFCExtract af = new AFCExtract();
            lang.SetLang(af);
            af.MdiParent = this;
            af.WindowState = FormWindowState.Maximized;
            af.Show();
            taskbar.AddTool(af, imageList1.Images[10]);
        }

        private void moviestfcBikToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BIKExtract bik = new BIKExtract();
            lang.SetLang(bik);
            bik.MdiParent = this;
            bik.WindowState = FormWindowState.Maximized;
            bik.Show();
            taskbar.AddTool(bik, imageList1.Images[14]);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            taskbar.strip = toolStrip1; //this be a toolstrip reference to class taskbar            
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            lang = new Languages(loc + "\\exec\\languages.xml", 0);
            lang.SetLang(this);
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                if (args[1].Equals("-version-switch-from") && args.Length == 3)
                {

                    string version = Assembly.GetExecutingAssembly().GetName().Version.Build.ToString();
                    if (version.Equals(args[2]))
                    {
                        MessageBox.Show("Version switched to is the same as the one you had before the switch.");
                    } else
                    {
                        MessageBox.Show("Version switched: "+args[2]+" => "+version);
                    }
                } else
                //automation
                if (args[1].Equals("-dlcinject") || args[1].Equals("-dlcextract"))
                {
                    //autostart DLC editor 2 (used by FemShep's Mod Manager 3/3.2)
                    //saves a little duplicate code
                    dLCEditor2ToolStripMenuItem.PerformClick();
                    return;
                } else
                if (args[1].Equals("-toceditorupdate"))
                {
                    //autostart the TOCEditor (used by FemShep's Mod Manager 3)
                    //saves a little duplicate code
                    tOCbinEditorToolStripMenuItem.PerformClick();
                    return;
                } else
                if (args[1].Equals("-decompresspcc"))
                {
                    //autostart the TOCEditor (used by FemShep's Mod Manager 3.2)
                    //saves a little duplicate code
                    pCCRepackerToolStripMenuItem.PerformClick();
                    return;
                } else
                if (args[1].Equals("--help") || args[1].Equals("-h")){
                    String commandLineHelp = "ME3Explorer Command Line Options\n";
                    commandLineHelp += " -dlcinject DLC.sfar SearchTerm PathToNewFile [SearchTerm2 PathToNewFile2]...\n";
                    commandLineHelp += "     Automates injecting pairs of files into a .sfar file using DLCEditor2. SearchTerm is a value you would type into the searchbox with the first result being the file that will be replaced.\n\n";
                    commandLineHelp += " -dlcextract DLC.sfar SearchTerm ExtractionPath\n";
                    commandLineHelp += "     Automates DLCEditor2 to extract the specified SearchTerm. SearchTerm is a value you would type into the searchbox with the first result being the file that will be extracted. The file is extracted to the specied ExtractionPath.\n\n";
                    commandLineHelp += " -toceditorupdate PCConsoleTOCFile.bin SearchTerm size\n";
                    commandLineHelp += "     Automates updating a single entry in a TOC file.  SearchTerm is a value you would type into the Searchbox with the first result being the file that will be updated. Size is the new size of the file, in bytes. Interface shows up if the file is not in the TOC.\n\n";
                    commandLineHelp += " -decompresspcc pccPath.pcc decompressedPath.pcc\n";
                    commandLineHelp += "     Automates PCCRepacker to decompress a file to the new location.\n\n";
                    System.Console.WriteLine(commandLineHelp);
                    Application.Exit();
                    return;
                }
               
                string ending = Path.GetExtension(args[1]).ToLower();
                switch (ending)
                {
                    case ".pcc":
                        PCCEditor2 editor = new PCCEditor2();
                        editor.MdiParent = this;
                        editor.Show();
                        editor.WindowState = FormWindowState.Maximized;
                        editor.LoadFile(args[1]);
                        break;
                    case ".txt":
                        ScriptCompiler sc = new ScriptCompiler();
                        sc.MdiParent = this;
                        sc.rtb1.LoadFile(args[1]);
                        sc.Compile();
                        sc.Show();
                        sc.WindowState = FormWindowState.Maximized;
                        break;
                    case ".mod":
                        ModMaker m = new ModMaker();
                        m.Show();
                        string[] s = new string[1];
                        s[0] = args[1];
                        //m.LoadMods(s);
                        m.WindowState = FormWindowState.Maximized;
                        break;
                }
            }
        }

        private void tLKEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainWindow mw = new MainWindow();
            mw.Show();
        }

        private void xBoxConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            XBoxConverter x = new XBoxConverter();
            lang.SetLang(x);
            x.MdiParent = this;
            x.WindowState = FormWindowState.Maximized;
            x.Show();
            taskbar.AddTool(x, imageList1.Images[16]);
        }

        private void selectToolLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Language_Editor le = new Language_Editor();
            lang.SetLang(le);
            le.lang = lang;
            le.MdiParent = this;
            le.WindowState = FormWindowState.Maximized;
            le.Show();
        }

        private void pCCRepackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCRepack pccRepack = new PCCRepack();
            pccRepack.MdiParent = this;
            pccRepack.WindowState = FormWindowState.Maximized;
            pccRepack.Show();
            taskbar.AddTool(pccRepack, imageList1.Images[2]);
        }

        private void scriptCompilerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ScriptCompiler sc = new ScriptCompiler();
            sc.MdiParent = this;
            sc.WindowState = FormWindowState.Maximized;
            sc.Show();
            taskbar.AddTool(sc, imageList1.Images[8]);
        }

        private void pCCEditor20ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCEditor2 pcc = new PCCEditor2();
            pcc.MdiParent = this;
            pcc.WindowState = FormWindowState.Maximized;
            pcc.Show();
            taskbar.AddTool(pcc, imageList1.Images[1]);
        }

        private void assetExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetExplorer ass = new AssetExplorer();
            ass.MdiParent = this;
            ass.Show(); //:D
            ass.WindowState = FormWindowState.Maximized;
            ass.LoadMe();
            taskbar.AddTool(ass, imageList1.Images[11]); //Add Tool ass. Ehh....
        }

        private void modMakerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ModMaker modmaker = new ModMaker();
            modmaker.Show();
        }

        private void textureExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new TextureExplorer());
        }

        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SequenceEditor seq = new SequenceEditor();
            seq.MdiParent = this;
            seq.Show();
            seq.WindowState = FormWindowState.Maximized;
            taskbar.AddTool(seq, imageList1.Images[9]);
        }
        private void taskbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStrip1.Visible = taskbarToolStripMenuItem.Checked;
        }

        private void coalescedEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Coalesced_Editor.CoalEditor());
        }

        private void meshplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Meshplorer.Meshplorer());
        }

        private void coalescedOperatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Coalesced_Operator.Operator newop = new Coalesced_Operator.Operator();
            newop.MdiParent = this;
            Size newvalue = new Size(900, 600);
            this.Size = newvalue;       //getting a bit cramped up at smaller resolution
            taskbar.AddTool(newop, imageList1.Images[17]);
            newop.Show();
            newop.WindowState = FormWindowState.Maximized;

        }

        private void patcherToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Patcher.Patcher());
        }

        private void tOCbinUpdaterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new TOCUpdater.TOCUpdater());
        }

        private void materialViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Material_Viewer.MaterialViewer());
        }

        private void versionCheckerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new VersionChecker.VersionChecker());
        }

        private void pSKViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new PSKViewer.PSKViewer());
        }

        private void soundplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Soundplorer());
        }

        private void pSAViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new PSAViewer());
        }

        private void switchToUDKExplorerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            UDKExplorer.UDKExplorer ex = new UDKExplorer.UDKExplorer();
            ex.Show();
        }

        private void mE2ExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\ME2Explorer.exe"))
                RunShell(loc + "\\ME2Explorer.exe", "");
        }

        private void RunShell(string cmd, string args)
        {
            //System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            //procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            //procStartInfo.UseShellExecute = true;
            //procStartInfo.CreateNoWindow = true;
            //System.Diagnostics.Process proc = new System.Diagnostics.Process();
            //proc.StartInfo = procStartInfo;
            //proc.Start();
            System.Diagnostics.Process.Start(cmd + " " + args);
        }

        private void mE1ExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\ME1Explorer.exe"))
                RunShell(loc + "\\ME1Explorer.exe", "");
        }

        private void texplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Texplorer2 texplorer = new Texplorer2(false);
            texplorer.Show();
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DebugOutput.StartDebugger("ME3Explorer Main Form");
        }

        private void levelExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LevelExplorer.ME3LevelExplorer l = new LevelExplorer.ME3LevelExplorer();
            l.Show();

        }

        private void propertyManagerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            PropertyManager m = new PropertyManager();
            lang.SetLang(m);
            m.MdiParent = this;
            m.WindowState = FormWindowState.Maximized;
            m.Show();
            taskbar.AddTool(m, imageList1.Images[7]);
        }

        private void propertyDumperToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new Property_Dumper.PropDumper());
        }

        private void propertyDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Propertydb.PropertyDB());
        }

        private void scriptDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new ScriptDB.ScriptDB());
        }

        private void textureToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Texture_Tool.TextureTool());
        }

        private void animationExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new AnimationExplorer.AnimationExplorer());
        }

        private void dLLInjectorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new DLLInjector.DLLInjector());
        }

        private void plotVarDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new PlotVarDB.PlotVarDB());
        }

        private void threadOptionsMenu_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter the number of threads you want the program to use in multi-threaded applications. Using more than 2 threads per CPU core is discouraged", "ME3Explorer", Properties.Settings.Default.NumThreads.ToString(), 0, 0);

            if (String.IsNullOrEmpty(result))
                return;

            uint NumResult;
            try
            {
                NumResult = Convert.ToUInt32(result);
            }
            catch
            {
                MessageBox.Show("Your input was not in the correct form");
                return;
            }

            if (NumResult == 0)
            {
                MessageBox.Show("You can't have 0 threads. Must be 1 or greater");
                return;
            }

            if (NumResult > (2 * Environment.ProcessorCount))
            {
                if (DialogResult.No == MessageBox.Show("You've selected " + NumResult + " threads, but your CPU has been detected as having " + Environment.ProcessorCount + " cores. Using more threads than twice your number of processors is not recommended. Continue anyway?", "That's a lot of threads!", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
                    return;
            }

            Properties.Settings.Default.NumThreads = (int)NumResult;
            MessageBox.Show("Number of threads changed to " + NumResult);
        }

        private void uDKConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new UDKConverter.UDKConverter());
        }

        private void setCustomPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String installPath = KFreonLib.Misc.Methods.SelectGameLoc(3);
            if (String.IsNullOrEmpty(installPath))
                return;

            ME3Directory.GamePath(installPath);
            String cookPath = Path.Combine(installPath, "BIOGame", "CookedPCConsole");
            if (!Directory.Exists(cookPath))
            {
                MessageBox.Show("Required CookedPCConsole folder not found at:\n" + cookPath, "Directory not found!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Properties.Settings.Default.TexplorerME3Path = cookPath;
            Properties.Settings.Default.Save();
            MessageBox.Show("New path setting saved", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void uECodeEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new UECodeEditor.UECodeEditor());
        }

        private void batchrenamerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new batchrenamer.BatchRenamer());
        }

        private void OpenMaximized(Form f, int ImageIndex = 19)
        {
            f.MdiParent = this;
            f.Show();
            f.WindowState = FormWindowState.Maximized;
            taskbar.AddTool(f, imageList1.Images[ImageIndex]);
        }

        private void meshplorer2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Meshplorer2.Meshplorer2());
        }

        private void meshplorerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new Meshplorer.Meshplorer());
        }

        private void materialViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new Material_Viewer.MaterialViewer()); 
        }

        private void pSAViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new PSAViewer());
        }

        private void pSKViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new PSKViewer.PSKViewer());
        }

        private void codexEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new Codex_Editor.CodexEditor());
        }

        private void questMapEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new QuestMapEditor.QMapEditor());
        }

        private void classViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new ClassViewer.ClassViewer());
        }

        private void gUIDEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new GUIDCacheEditor.GUIDCacheEditor());
        }

        private void dLCEditor2ToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new DLCEditor2.DLCEditor2());
        }

        private void pAREditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new PAREditor.PAREditor());
        }

        private void dialogEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new DialogEditor.DialogEditor());
        }

        private void faceFXAnimSetEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new FaceFXAnimSetEditor.FaceFXAnimSetEditor());
        }

        private void wwiseBankViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new WwiseBankViewer.WwiseViewer());
        }

        private void dLCTOCbinUpdaterToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new DLCTOCbinUpdater.DLCTOCbinUpdater());
        }

        private void tOCbinEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TOCeditor tocedit = new TOCeditor();
            lang.SetLang(tocedit);
            tocedit.MdiParent = this;
            tocedit.WindowState = FormWindowState.Maximized;
            tocedit.Show();
            taskbar.AddTool(tocedit, imageList1.Images[3]);
        }

        private void TOCbinAKEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new TOCEditorAK.TOCEditorAK());
        }

        private void subtitleScannerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenMaximized(new SubtitleScanner.SubtitleScanner());
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            foreach (taskbar.task_list l in taskbar.tools)
            {
                if (l.tool.IsDisposed)
                {
                    taskbar.strip.Items.Remove(l.icon);
                    taskbar.tools.Remove(l);
                    break;
                }
            }
        }

        private void autoTOCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new AutoTOC.AutoTOC());
        }

		private void KFreonTPFToolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KFreonTPFTools3 tpftools = new KFreonTPFTools3();
            tpftools.Show();
		}

        private void showKnownPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string s = "GamePath :\n" + ME3Directory.gamePath + "\n";
            s += "DLCPath :\n" + ME3Directory.DLCPath + "\n";
            s += "CookedPath :\n" + ME3Directory.cookedPath + "\n";
            s += "BioWareDocPath :\n" + ME3Directory.BioWareDocPath;
            MessageBox.Show(s);
        }

        private void vanillaMakerBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\ME3VanillaMaker.exe"))
                RunShell(loc + "\\ME3VanillaMaker.exe", "");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void mE3CREATORToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\ME3Creator.exe"))
            {
                RunShell(loc + "\\ME3Creator.exe", "");
            }
            else
            {
                MessageBox.Show("Cant find ME3Creator.exe!");
            }
        }

        private void extraToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void interpEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new InterpEditor.InterpEditor());
        }

        private void texplorerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            Texplorer2 texplorer = new Texplorer2();
            texplorer.Show();
        }

        private void modMakerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ModMaker modmaker = new ModMaker();
            modmaker.Show();
        }

        private void tPFDDSToolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KFreonTPFTools3 tpftools = new KFreonTPFTools3();
            tpftools.Show();
        }

        private void cameraToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new CameraTool.CamTool());
        }

        private void dDSConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Disabled for now :(");
        }

        private void mE3WikiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.wikia.com/wiki/ME3Explorer_Wiki");
        }

        private void forumsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.freeforums.org/");
        }

        private void versionSwitcherToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\VersionSwitcher.exe"))
            {
                RunShell(loc + "\\VersionSwitcher.exe", "");
            }
            else
            {
                MessageBox.Show("Couldn't find VersionSwitcher.exe.");
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutME3Explorer().Show(this);
        }
    }
}
