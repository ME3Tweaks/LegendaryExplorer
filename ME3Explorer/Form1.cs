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
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;

namespace ME3Explorer
{
    public partial class Form1 : Form
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;
        bool IsDLCDone = false;

        public Form1()
        {
            InitializeComponent();
            disableDLCCheckOnStartupToolStripMenuItem.Checked = Properties.Settings.Default.DisableDLCCheckOnStart;
            TalkFiles.LoadSavedTlkList();
            Unreal.UnrealObjectInfo.loadfromJSON();
        }

        private async void DoDLCCheck()
        {
            await Task.Delay(1000);
            InitialDLCExtractor dlcChecker = new InitialDLCExtractor();
            dlcChecker.Closing += (sender, args) => IsDLCDone = true;
            if (dlcChecker.vm.Required == true)
                dlcChecker.ShowDialog();
            else
                IsDLCDone = true;
        }

        private void decompressorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Decompressor decomp = new Decompressor();
            decomp.MdiParent = this;
            decomp.WindowState = FormWindowState.Maximized;
            decomp.Show();
            
            taskbar.AddTool(decomp, Properties.Resources.decompressor_64x64);
        }

        private void conditionalsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Conditionals con = new Conditionals();
            con.MdiParent = this;
            con.WindowState = FormWindowState.Maximized;
            con.Show();
            taskbar.AddTool(con, Properties.Resources.conditionals_editor_64x64);
        }

        private void dLCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DLCExplorer expl = new DLCExplorer();
            expl.MdiParent = this;
            expl.WindowState = FormWindowState.Maximized;
            expl.Show();
            taskbar.AddTool(expl, Properties.Resources.sfar_basiceditor_icon_64x64);
        }

        private void languageSelectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ls = new Language_Selector();
            OpenMaximized(ls);
            taskbar.AddTool(ls, Properties.Resources.lang_select_64x64);
        }

        private void aFCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AFCExtract af = new AFCExtract();
            OpenMaximized(af);
            taskbar.AddTool(af, Properties.Resources.audio_extract_64x64);
        }

        private void moviestfcBikToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BIKExtract bik = new BIKExtract();
            OpenMaximized(bik);
            taskbar.AddTool(bik, Properties.Resources.BIK_movie_64x64);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            taskbar.strip = toolStrip1; //this be a toolstrip reference to class taskbar            
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                if (args[1].Equals("-version-switch-from") && args.Length == 3)
                {

                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    if (version.Equals(args[2]))
                    {
                        MessageBox.Show("Version switched to is the same as the one you had before the switch.");
                    } else
                    {
                        MessageBox.Show("Version switched: "+args[2]+" => "+version);
                    }
                } else
                //automation
                if (args[1].Equals("-dlcinject") || args[1].Equals("-dlcextract") || args[1].Equals("-dlcaddfiles") || args[1].Equals("-dlcremovefiles") || args[1].Equals("-dlcunpack") || args[1].Equals("-dlcunpack-nodebug"))
                {
                    //autostart DLC editor 2 (used by FemShep's Mod Manager 3/3.1+)
                    //saves a little duplicate code
                    dLCEditor2ToolStripMenuItem.PerformClick();
                    return;
                }
                if (args[1].Equals("-autotoc"))
                {
                    //autostart the AutoTOC Tool (uses a path) (used by FemShep's Mod Manager 3)
                    //saves a little duplicate code
                    autoTOCToolStripMenuItem.PerformClick();
                    return;
                }
                else
                if (args[1].Equals("-decompresspcc"))
                {
                    //autostart the TOCEditor (used by FemShep's Mod Manager 3.2)
                    //saves a little duplicate code
                    pCCRepackerToolStripMenuItem.PerformClick();
                    return;
                } else
                if (args[1].Equals("--help") || args[1].Equals("-h") || args[1].Equals("/?"))
                {
                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    string commandLineHelp = "\nME3Explorer v"+version+" Command Line Options\n";
                    commandLineHelp += " -decompresspcc pccPath.pcc decompressedPath.pcc\n";
                    commandLineHelp += "     Automates PCCRepacker to decompress a pcc to the new location.\n\n";
                    commandLineHelp += " -dlcinject DLC.sfar SearchTerm PathToNewFile [SearchTerm2 PathToNewFile2]...\n";
                    commandLineHelp += "     Automates injecting pairs of files into a .sfar file using DLCEditor2. SearchTerm is a value you would type into the searchbox with the first result being the file that will be replaced.\n\n";
                    commandLineHelp += " -dlcextract DLC.sfar SearchTerm ExtractionPath\n";
                    commandLineHelp += "     Automates DLCEditor2 to extract the specified SearchTerm. SearchTerm is a value you would type into the searchbox with the first result being the file that will be extracted. The file is extracted to the specied ExtractionPath.\n\n";
                    commandLineHelp += " -dlcaddfiles DLC.sfar InternalPath NewFile [InternalPath2 NewFile2]...\n";
                    commandLineHelp += "     Automates DLCEditor2 to add the specified new files. InternalPath is the internal path in the SFAR the file NewFile will be placed at.\n\n";
                    commandLineHelp += " -dlcremovefiles DLC.sfar SearchTerm [SearchTerm2]...\n";
                    commandLineHelp += "     Automates removing a file or list of files from a DLC. SearchTerm is a value you would type into the Searchbox with the first result being the file that will be removed.\n\n";
                    commandLineHelp += " -dlcunpack DLC.sfar Unpackpath\n";
                    commandLineHelp += "     Automates unpacking an SFAR file to the specified directory. Shows the debug interface to show progress. To unpack a game DLC for use by the game, unpack to the Mass Effect 3 directory. Unpacking Patch_001.sfar will cause the game to crash at startup.\n\n";
                    commandLineHelp += " -dlcunpack-nodebug DLC.sfar Unpackpath\n";
                    commandLineHelp += "     Same as -dlcunpack but does not show the debugging interface.\n\n";
                    System.Console.WriteLine(commandLineHelp);
                    Environment.Exit(0);
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
                    /*case ".mod":    // KFreon: Disabled for now due to the fact that it doesn't do the DLC check first
                        ModMaker m = new ModMaker();
                        m.Show();
                        string[] s = new string[1];
                        s[0] = args[1];
                        //m.LoadMods(s);
                        m.WindowState = FormWindowState.Maximized;
                        break;*/
                }
            }

            if (!String.IsNullOrEmpty(Properties.Settings.Default.ME3InstallDir))
                ME3Directory.GamePath(Properties.Settings.Default.ME3InstallDir);
            if (!String.IsNullOrEmpty(Properties.Settings.Default.ME2InstallDir))
                ME2Directory.GamePath(Properties.Settings.Default.ME2InstallDir);
            if (!String.IsNullOrEmpty(Properties.Settings.Default.ME1InstallDir))
                ME1Directory.GamePath(Properties.Settings.Default.ME1InstallDir);

            var vers = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            versionToolStripMenuItem.Text += vers;
            versionToolStripMenuItem.Tag = "versionItem";
            menuStrip1.Renderer = new NoHighlightRenderer();

            if (!disableDLCCheckOnStartupToolStripMenuItem.Checked)
                DoDLCCheck();
        }

        internal class NoHighlightRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if ((string)e.Item.Tag != "versionItem")
                {
                    base.OnRenderMenuItemBackground(e);
                }
            }
        }

        private void pCCRepackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCRepack pccRepack = new PCCRepack();
            pccRepack.MdiParent = this;
            pccRepack.WindowState = FormWindowState.Maximized;
            pccRepack.Show();
            taskbar.AddTool(pccRepack, Properties.Resources.pcc_repacker_64x64);
        }

        private void scriptCompilerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ScriptCompiler sc = new ScriptCompiler();
            sc.MdiParent = this;
            sc.WindowState = FormWindowState.Maximized;
            sc.Show();
            taskbar.AddTool(sc, Properties.Resources.script_compiler_64x64);
        }

        private void pCCEditor20ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PCCEditor2 pcc = new PCCEditor2();
            pcc.MdiParent = this;
            pcc.WindowState = FormWindowState.Maximized;
            pcc.Show();
            taskbar.AddTool(pcc, Properties.Resources.pcceditor2_64x64);
            pcc.LoadMostRecent();
        }

        private void assetExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetExplorer ass = new AssetExplorer();
            OpenMaximized(ass);
            ass.LoadMe();
            taskbar.AddTool(ass, Properties.Resources.asset_explorer_64x64); //Add Tool ass. Ehh....
        }

        private void modMakerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ModMaker modmaker = new ModMaker();
            modmaker.Show();
            taskbar.AddTool(modmaker, Properties.Resources.modmaker_64x64, true);
        }

        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SequenceEditor seq = new SequenceEditor();
            seq.MdiParent = this;
            seq.Show();
            seq.WindowState = FormWindowState.Maximized;
            taskbar.AddTool(seq, Properties.Resources.sequence_editor_64x64);
        }
        private void taskbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStrip1.Visible = taskbarToolStripMenuItem.Checked;
        }

        private void coalescedEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This tool is outdated and does not work properly for editing DLC's. " +
                "\nTankmaster has developed a new tool which handles this correctly, do you wish to visit his thread instead?",
                "Outdated/Buggy tool!",
                MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);

            if (result == DialogResult.Yes)
                System.Diagnostics.Process.Start("http://me3explorer.freeforums.org/additional-tools-t1524.html");
            else
            {
                var coalesced = new Coalesced_Editor.CoalEditor();
                OpenMaximized(coalesced);
                taskbar.AddTool(coalesced, Properties.Resources.coalesced_editor_64x64);
            }
        }

        private void coalescedOperatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Coalesced_Operator.Operator newop = new Coalesced_Operator.Operator();
            newop.MdiParent = this;
            Size newvalue = new Size(900, 600);
            this.Size = newvalue;       //getting a bit cramped up at smaller resolution
            taskbar.AddTool(newop, Properties.Resources.coalesced_operator_64x64);
            newop.Show();
            newop.WindowState = FormWindowState.Maximized;

        }

        private void soundplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new Soundplorer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.soundplorer_64x64);
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
            m.MdiParent = this;
            m.WindowState = FormWindowState.Maximized;
            m.Show();
            taskbar.AddTool(m, Properties.Resources.property_manager_64x64);
        }

        private void propertyDumperToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new Property_Dumper.PropDumper();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.property_dumper_64x64);
        }

        private void propertyDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new Propertydb.PropertyDB();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.property_database_64x64);
        }

        private void scriptDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new ScriptDB.ScriptDB();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.script_database_64x64);
        }

        private void animationExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new AnimationExplorer.AnimationExplorer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.animation_explorer_64x64);
        }

        private void dLLInjectorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenMaximized(new DLLInjector.DLLInjector());
        }

        private void plotVarDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new PlotVarDB.PlotVarDB();
            OpenMaximized(form);
            taskbar.AddTool(form, Properties.Resources.plot_DB_64x64);
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
            var tool = new UDKConverter.UDKConverter();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.udk_converter_64x64);
        }

        private void uECodeEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new UECodeEditor.UECodeEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.ue_codeeditor_64x64);
        }

        private void batchrenamerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new batchrenamer.BatchRenamer();
            OpenMaximized(form);
            taskbar.AddTool(form, Properties.Resources.batch_rename_64x64);
        }

        private void OpenMaximized(Form f)
        {
            f.MdiParent = this;
            f.Show();
            f.WindowState = FormWindowState.Maximized;
        }

        private void meshplorer2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new Meshplorer2.Meshplorer2();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.meshplorer2_64x64);
        }

        private void meshplorerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new Meshplorer.Meshplorer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.meshplorer_64x64);
        }

        private void pSAViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new PSAViewer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.psa_viewer_64x64);
        }

        private void pSKViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new PSKViewer.PSKViewer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.psk_viewer_64x64);
        }

        private void codexEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new Codex_Editor.CodexEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.codex_editor_64x64);
        }

        private void questMapEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new QuestMapEditor.QMapEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.questmap_editor_64x64);
        }

        private void classViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new ClassViewer.ClassViewer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.class_viewer_64x64);
        }

        private void gUIDEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new GUIDCacheEditor.GUIDCacheEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.GUIDcache_editor_64x64);
        }

        private void dLCEditor2ToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new DLCEditor2.DLCEditor2();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.sfar_editor2_icon_64x64);
        }

        private void pAREditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new PAREditor.PAREditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.PAR_editor_64x64);
        }

        private void dialogEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new DialogEditor.DialogEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.dialogue_editor_64x64);
        }

        private void faceFXAnimSetEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new FaceFXAnimSetEditor.FaceFXAnimSetEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.fxa_editor_64x64);
        }

        private void wwiseBankViewerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new WwiseBankViewer.WwiseViewer();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.wwisebank_editor_64x64);
        }

        private void dLCTOCbinUpdaterToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var form = new DLCTOCbinUpdater.DLCTOCbinUpdater();
            OpenMaximized(form);
            taskbar.AddTool(form, Properties.Resources.SFARTOC_64x64);
        }

        private void subtitleScannerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new SubtitleScanner.SubtitleScanner();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.subtitle_scanner_64x64);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            foreach (taskbar.task_list l in taskbar.tools)
            {
                if (l.tool != null && l.tool.IsDisposed)
                {
                    taskbar.strip.Items.Remove(l.icon);
                    taskbar.tools.Remove(l);
                    break;
                }
                else if (l.wpfWindow != null && System.Windows.PresentationSource.FromVisual(l.wpfWindow) == null)
                {
                    taskbar.strip.Items.Remove(l.icon);
                    taskbar.tools.Remove(l);
                    break;
                }
            }
        }

        private void autoTOCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new AutoTOC.AutoTOC();
            OpenMaximized(form);
            taskbar.AddTool(form, Properties.Resources.autotoc_64x64);
        }

        private void showKnownPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string s = "ME3:\n";
            s += "  GamePath :\n\t" + ME3Directory.gamePath + "\n";
            s += "  DLCPath :\n\t" + ME3Directory.DLCPath + "\n";
            s += "  CookedPath :\n\t" + ME3Directory.cookedPath + "\n";
            s += "  BioWareDocPath :\n\t" + ME3Directory.BioWareDocPath + "\n";
            s += "\nME2:\n";
            s += "  GamePath :\n\t" + ME2Directory.gamePath + "\n";
            s += "  DLCPath :\n\t" + ME2Directory.DLCPath + "\n";
            s += "  CookedPath :\n\t" + ME2Directory.cookedPath + "\n";
            s += "  BioWareDocPath :\n\t" + ME2Directory.BioWareDocPath + "\n";
            s += "\nME1:\n";
            s += "  GamePath :\n\t" + ME1Directory.gamePath + "\n";
            s += "  DLCPath :\n\t" + ME1Directory.DLCPath + "\n";
            s += "  CookedPath :\n\t" + ME1Directory.cookedPath + "\n";
            s += "  BioWareDocPath :\n\t" + ME1Directory.BioWareDocPath + "\n";
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
            Properties.Settings.Default.ME3InstallDir = ME3Directory.gamePath;
            Properties.Settings.Default.ME2InstallDir = ME2Directory.gamePath;
            Properties.Settings.Default.ME1InstallDir = ME1Directory.gamePath;
            Properties.Settings.Default.Save();
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

        private void texplorerToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            Texplorer2 texplorer = new Texplorer2();
            texplorer.Show();
            taskbar.AddTool(texplorer, Properties.Resources.texplorer_64x64, true);
        }

        private void tPFDDSToolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KFreonTPFTools3 tpftools = new KFreonTPFTools3();
            tpftools.Show();
            taskbar.AddTool(tpftools, Properties.Resources.TPFTools_64x64, true);
        }

        private void cameraToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var cam = new CameraTool.CamTool();
            OpenMaximized(cam);
            taskbar.AddTool(cam, Properties.Resources.camera_tool_64x64);
        }

        private void imageEngineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var conv = new CSharpImageLibrary.MainWindow();
            ElementHost.EnableModelessKeyboardInterop(conv);
            conv.Show();
            taskbar.AddTool(null, Properties.Resources.ImageEngine_icon_64x64, true, conv);
        }

        private void mE3WikiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.wikia.com/wiki/ME3Explorer_Wiki");
        }

        private void forumsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://me3explorer.freeforums.org/");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutME3Explorer().Show(this);
        }

        private void tLKEditorToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var tool = new TLKEditor();
            tool.Show();
            taskbar.AddTool(null, Properties.Resources.TLK_editor_64x64, true, tool);
        }

        private void interpEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tool = new InterpEditor.InterpEditor();
            OpenMaximized(tool);
            taskbar.AddTool(tool, Properties.Resources.interp_viewer_icon_64x64);
        }

        private void massEffect3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String installPath = KFreonLib.Misc.Methods.SelectGameLoc(3);
            if (String.IsNullOrEmpty(installPath))
                return;

            ME3Directory.GamePath(installPath);

            Properties.Settings.Default.ME3InstallDir = installPath;
            Properties.Settings.Default.Save();
            MessageBox.Show("New path setting saved", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void massEffect2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String installPath = KFreonLib.Misc.Methods.SelectGameLoc(2);
            if (String.IsNullOrEmpty(installPath))
                return;

            ME2Directory.GamePath(installPath);
            Properties.Settings.Default.ME2InstallDir = installPath;
            Properties.Settings.Default.Save();
            MessageBox.Show("New path setting saved", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void massEffect1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String installPath = KFreonLib.Misc.Methods.SelectGameLoc(1);
            if (String.IsNullOrEmpty(installPath))
                return;

            ME1Directory.GamePath(installPath);
            Properties.Settings.Default.ME1InstallDir = installPath;
            Properties.Settings.Default.Save();
            MessageBox.Show("New path setting saved", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void gitHubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://github.com/ME3Explorer/ME3Explorer");
        }

        private void nexusModsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.nexusmods.com/masseffect3/mods/409/");
        }

        private void disableDLCCheckOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (disableDLCCheckOnStartupToolStripMenuItem.Checked)
            {
                disableDLCCheckOnStartupToolStripMenuItem.Checked = false;
                Properties.Settings.Default.DisableDLCCheckOnStart = false;
                Properties.Settings.Default.Save();
            }
            else
            {
                var res = MessageBox.Show(String.Join(Environment.NewLine, new[] { "Disabling DLC check when all DLC is not extracted can lead to unusual activity in Texplorer, TPFTools, and Modmaker.", "Are you sure you want to disable this check?" }), "Uhh...Commander?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (res == DialogResult.Yes)
                {
                    Properties.Settings.Default.DisableDLCCheckOnStart = true;
                    Properties.Settings.Default.Save();
                    disableDLCCheckOnStartupToolStripMenuItem.Checked = true;
                }
            }
        }

        private void allPathsAtOnceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] biogames = new string[3];
            biogames[0] = ME1Directory.GamePath();
            biogames[1] = ME2Directory.GamePath();
            biogames[2] = ME3Directory.GamePath();


            // KFreon: Display PathChanger
            using (KFreonLib.Helpers.PathChanger changer = new KFreonLib.Helpers.PathChanger(biogames[0], biogames[1], biogames[2]))
            {
                if (changer.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // KFreon: Change paths
                    Properties.Settings.Default.ME1InstallDir = changer.PathME1;
                    Properties.Settings.Default.ME2InstallDir = changer.PathME2;
                    Properties.Settings.Default.ME3InstallDir = changer.PathME3;

                    KFreonLib.MEDirectories.MEDirectories.SaveSettings(new List<string>() { changer.PathME1, changer.PathME2, changer.PathME2 });
                    ME1Directory.GamePath(changer.PathME1);
                    ME2Directory.GamePath(changer.PathME2);
                    ME3Directory.GamePath(changer.PathME3);
                }
            }
        }
    }
}
