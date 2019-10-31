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
using ME3Explorer.AutoTOC;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using SharpDX;
using StreamHelpers;
using MessageBox = System.Windows.MessageBox;

namespace ME3Explorer.GameInterop
{
    public static class AnimViewer
    {
        public static void SetUpAnimStreamFile(string animSourceFilePath, int animSequenceUIndex, string saveAsName)
        {
            string animViewerAnimStreamFilePath = Path.Combine(App.ExecFolder, "ME3AnimViewer_StreamAnim.pcc");

            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(animViewerAnimStreamFilePath);
            if (animSourceFilePath != null)
            {
                const int InterpDataUIndex = 8;
                const int InterpTrackAnimControlUIndex = 10;
                const int KIS_DYN_AnimsetUIndex = 6;
                using IMEPackage animSourceFile = MEPackageHandler.OpenMEPackage(animSourceFilePath);

                ExportEntry sourceAnimSeq = animSourceFile.GetUExport(animSequenceUIndex);

                IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceAnimSeq.ParentFullPath, animSourceFile, pcc);

                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAnimSeq, pcc, parent, true, out IEntry ent);
                ExportEntry importedAnimSeq = (ExportEntry)ent;

                NameReference seqName = importedAnimSeq.GetProperty<NameProperty>("SequenceName").Value;
                float seqLength = importedAnimSeq.GetProperty<FloatProperty>("SequenceLength");
                IEntry bioAnimSet = pcc.GetUExport(importedAnimSeq.GetProperty<ObjectProperty>("m_pBioAnimSetData").Value);
                string setName = importedAnimSeq.ObjectName.Name.RemoveRight(seqName.Name.Length + 1);

                ExportEntry animInterpData = pcc.GetUExport(InterpDataUIndex);
                animInterpData.WriteProperty(new FloatProperty(seqLength, "InterpLength"));

                ExportEntry animTrack = pcc.GetUExport(InterpTrackAnimControlUIndex);
                var animSeqKeys = animTrack.GetProperty<ArrayProperty<StructProperty>>("AnimSeqs");
                animSeqKeys[0].Properties.AddOrReplaceProp(new NameProperty(seqName, "AnimSeqName"));
                animTrack.WriteProperty(animSeqKeys);

                ExportEntry dynamicAnimSet = pcc.GetUExport(KIS_DYN_AnimsetUIndex);
                dynamicAnimSet.WriteProperty(new ObjectProperty(bioAnimSet.UIndex, "m_pBioAnimSetData"));
                dynamicAnimSet.WriteProperty(new NameProperty(setName, "m_nmOrigSetName"));
                dynamicAnimSet.WriteProperty(new ArrayProperty<ObjectProperty>("Sequences")
                {
                    new ObjectProperty(importedAnimSeq.UIndex)
                });

            }

            string tempFilePath = Path.Combine(ME3Directory.cookedPath, $"{saveAsName}.pcc");

            pcc.Save(tempFilePath);
            TryPadFile(tempFilePath, 10_485_760);
        }

        public static void OpenFileInME3(IMEPackage pcc, bool canHotLoad = false, bool shouldPad = true)
        {
            var tempMapName = GameController.TempMapName;
            string tempDir = ME3Directory.cookedPath;
            string tempFilePath = Path.Combine(tempDir, $"{tempMapName}.pcc");

            pcc.Save(tempFilePath);
            if (shouldPad)
            {
                if (!TryPadFile(tempFilePath))
                {
                    //if file was too big to pad, hotloading is impossible 
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
            AutoTOCWPF.GenerateAllTOCs();
            Process.Start(ME3Directory.ExecutablePath, $"{tempMapName} -nostartupmovies -Windowed ResX={resX} ResY={resY} WindowPosX={posX} WindowPosY={posY}");
        }

        public static bool TryPadFile(string tempFilePath, int paddedSize = 52_428_800 /* 50 MB */)
        {
            using (FileStream fs = File.OpenWrite(tempFilePath))
            {
                fs.Seek(0, SeekOrigin.End);
                long size = fs.Position;
                if (size <= paddedSize)
                {
                    var paddingSize = paddedSize - size;
                    fs.WriteZeros((uint)paddingSize);
                    return true;
                }
            }

            return false;
        }
    }
}
