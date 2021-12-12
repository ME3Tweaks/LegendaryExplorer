using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.AutoTOC;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using MessageBox = System.Windows.MessageBox;

namespace LegendaryExplorer.GameInterop
{
    public static class AnimViewer
    {
        public static void SetUpAnimStreamFile(string animSourceFilePath, int animSequenceUIndex, string saveAsName)
        {
            string animViewerAnimStreamFilePath = Path.Combine(AppDirectories.ExecFolder, "ME3AnimViewer_StreamAnim.pcc");

            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(animViewerAnimStreamFilePath);
            if (animSourceFilePath != null)
            {
                try
                {
                    const int InterpDataUIndex = 8;
                    const int InterpTrackAnimControlUIndex = 10;
                    const int KIS_DYN_AnimsetUIndex = 6;
#if DEBUG
                    Debug.WriteLine($"AnimViewer Loading: {animSourceFilePath} #{animSequenceUIndex}");
#endif
                    using IMEPackage animSourceFile = MEPackageHandler.OpenMEPackage(animSourceFilePath);

                    ExportEntry sourceAnimSeq = animSourceFile.GetUExport(animSequenceUIndex);

                    IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceAnimSeq.ParentFullPath, animSourceFile, pcc, new RelinkerOptionsPackage());

                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAnimSeq, pcc, parent, true, new RelinkerOptionsPackage(),out IEntry ent);
                    ExportEntry importedAnimSeq = (ExportEntry)ent;

                    NameReference seqName = importedAnimSeq.GetProperty<NameProperty>("SequenceName").Value;
                    float seqLength = importedAnimSeq.GetProperty<FloatProperty>("SequenceLength");
                    IEntry bioAnimSet = pcc.GetEntry(importedAnimSeq.GetProperty<ObjectProperty>("m_pBioAnimSetData").Value);
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
                catch
                {
                    MessageBox.Show($"Error Loading {animSourceFilePath} #{animSequenceUIndex}");
                }

            }

            string tempFilePath = Path.Combine(ME3Directory.CookedPCPath, $"{saveAsName}.pcc");

            pcc.Save(tempFilePath);
            InteropHelper.TryPadFile(tempFilePath, 10_485_760);
        }

        public static void OpenFileInME3(IMEPackage pcc, bool canHotLoad = false, bool shouldPad = true)
        {
            var interopTarget = GameController.GetInteropTargetForGame(MEGame.ME3);
            var tempMapName = GameController.TempMapName;
            string tempDir = ME3Directory.CookedPCPath;
            string tempFilePath = Path.Combine(tempDir, $"{tempMapName}.pcc");

            pcc.Save(tempFilePath);
            if (shouldPad)
            {
                if (!InteropHelper.TryPadFile(tempFilePath))
                {
                    //if file was too big to pad, hotloading is impossible 
                    canHotLoad = false;
                }
            }

            if (interopTarget.TryGetProcess(out var me3Process) && canHotLoad)
            {
                interopTarget.ExecuteConsoleCommands($"at {tempMapName}");
                return;
            }
            me3Process?.Kill();
            int resX = 1000;
            int resY = 800;
            int posX = (int)(SystemParameters.PrimaryScreenWidth - (resX + 100));
            int posY = (int)(SystemParameters.PrimaryScreenHeight - resX) / 2;
            TOCCreator.CreateTOCForGame(pcc.Game);
            Process.Start(ME3Directory.ExecutablePath, $"{tempMapName} -nostartupmovies -Windowed ResX={resX} ResY={resY} WindowPosX={posX} WindowPosY={posY}");
        }
    }
}
