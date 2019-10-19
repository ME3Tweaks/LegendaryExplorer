using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using StreamHelpers;
using MessageBox = System.Windows.MessageBox;

namespace ME3Explorer.GameInterop
{
    public static class AnimViewer
    {
        public static void ViewAnimInGame(string animSourceFilePath, int animSequenceUIndex)
        {
            string animViewerBaseFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer.pcc");

            using IMEPackage animViewerBase = MEPackageHandler.OpenMEPackage(animViewerBaseFilePath);
            using IMEPackage animSourceFile = MEPackageHandler.OpenMEPackage(animSourceFilePath);

            ExportEntry sourceAnimSeq = animSourceFile.GetUExport(animSequenceUIndex);

            IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceAnimSeq.ParentFullPath, animSourceFile, animViewerBase);

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAnimSeq, animViewerBase, parent, true, out IEntry ent);
            ExportEntry importedAnimSeq = (ExportEntry)ent;

            NameReference seqName = importedAnimSeq.GetProperty<NameProperty>("SequenceName").Value;
            IEntry bioAnimSet = animViewerBase.GetUExport(importedAnimSeq.GetProperty<ObjectProperty>("m_pBioAnimSetData").Value);
            string setName = importedAnimSeq.ObjectName.Name.RemoveRight(seqName.Name.Length + 1);

            ExportEntry setAmbientPerformance = animViewerBase.GetUExport(19);
            ExportEntry dynamicAnimSet = animViewerBase.GetUExport(2666);

            setAmbientPerformance.WriteProperty(new NameProperty(seqName, "m_nmDefaultPoseAnim"));
            dynamicAnimSet.WriteProperty(new ObjectProperty(bioAnimSet.UIndex, "m_pBioAnimSetData"));
            dynamicAnimSet.WriteProperty(new NameProperty(setName, "m_nmOrigSetName"));
            dynamicAnimSet.WriteProperty(new ArrayProperty<ObjectProperty>("Sequences")
            {
                new ObjectProperty(importedAnimSeq.UIndex)
            });

            OpenFileInME3(animViewerBase, true);
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        public static void OpenFileInME3(IMEPackage pcc, bool canHotLoad = false)
        {
            const string tempMapName = "AAAME3EXPDEBUGLOAD";

            string tempDir = ME3Directory.cookedPath;
            string tempFilePath = Path.Combine(tempDir, $"{tempMapName}.pcc");

            pcc.Save(tempFilePath);
            using (FileStream fs = File.OpenWrite(tempFilePath))
            {
                const int paddedSize = 52_428_800; //50 MB
                fs.Seek(0, SeekOrigin.End);
                long size = fs.Position;
                if (size <= paddedSize)
                {
                    var paddingSize = paddedSize - size;
                    fs.WriteZeros((uint)paddingSize);
                }
                else
                {
                    canHotLoad = false;
                }
            }

            if (Process.GetProcessesByName("MassEffect3").FirstOrDefault() is Process me3Process)
            {
                //ME3 is already open!

                if (canHotLoad)
                {
                    string execFileName = "hotload";
                    string execFilePath = Path.Combine(ME3Directory.gamePath, "Binaries", execFileName);
                    File.WriteAllText(execFilePath, $"at {tempMapName}");
                    //MessageBox.Show($"Mass Effect 3 is already open! Open the in-game console (tab), and type:\nexec {execFileName}");
                    IntPtr handle = me3Process.MainWindowHandle;
                    SetForegroundWindow(handle);
                    const int WM_SYSKEYDOWN = 0x0104;
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.Tab, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.E, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.X, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.E, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.C, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.Space, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.H, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.O, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.T, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.L, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.O, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.A, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.D, 0);
                    PostMessage(handle, WM_SYSKEYDOWN, (int)Keys.Enter, 0);
                    //SendKeys.SendWait("{TAB}");
                    //SendKeys.SendWait(" exec hotload");
                    //SendKeys.SendWait("{ENTER}");
                    return;
                }

                me3Process.Kill();
            }

            Process.Start(ME3Directory.ExecutablePath, $"{tempMapName} -nostartupmovies -Windowed ResX=1000 ResY=800");
            MessageBox.Show("Mass Effect 3 starting up! This may take a moment.");
        }
    }
}
