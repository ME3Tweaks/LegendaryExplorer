using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using SharpDX;
using StreamHelpers;
using MessageBox = System.Windows.MessageBox;

namespace ME3Explorer.GameInterop
{
    public static class AnimViewer
    {
        public static Vector3 ViewAnimInGame(string animSourceFilePath, int animSequenceUIndex, bool shepard = false)
        {
            string animViewerBaseFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer.pcc");

            using IMEPackage animViewerBase = MEPackageHandler.OpenMEPackage(animViewerBaseFilePath);
            using IMEPackage animSourceFile = MEPackageHandler.OpenMEPackage(animSourceFilePath);

            ExportEntry sourceAnimSeq = animSourceFile.GetUExport(animSequenceUIndex);

            byte[] binData = sourceAnimSeq.getBinaryData();
            Vector3 offsetVector = Vector3.Zero;
            if (binData.Length >= 16)
            {
                offsetVector = new Vector3(BitConverter.ToSingle(binData, 4), BitConverter.ToSingle(binData, 8), BitConverter.ToSingle(binData, 12));
            }

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

            if (shepard)
            {
                ExportEntry bioWorldInfo = animViewerBase.GetUExport(4);
                //6 is the hoody
                bioWorldInfo.WriteProperty(new IntProperty(6, "ForcedCasualAppearanceID"));
            }

            OpenFileInME3(animViewerBase, true);
            return offsetVector;
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        public static void OpenFileInME3(IMEPackage pcc, bool canHotLoad = false)
        {
            var tempMapName = GameController.TempMapName;
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

            if (GameController.TryGetME3Process(out Process me3Process) && canHotLoad)
            {
                IntPtr handle = me3Process.MainWindowHandle;
                GameController.ExecuteConsoleCommands(handle, MEGame.ME3, $"at {tempMapName}");
                return;
            }

            me3Process?.Kill();
            int resX = 1000;
            int resY = 800;
            int posX = (int)(SystemParameters.PrimaryScreenWidth - (resX + 100));
            int posY = (int)(SystemParameters.PrimaryScreenHeight - resX) / 2;
            Process.Start(ME3Directory.ExecutablePath, $"{tempMapName} -nostartupmovies -Windowed ResX={resX} ResY={resY} WindowPosX={posX} WindowPosY={posY}");
        }
    }
}
