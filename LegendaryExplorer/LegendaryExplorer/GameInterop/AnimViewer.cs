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
        public static void SetUpAnimStreamFile(MEGame game, string animSourceFilePath, int animSequenceUIndex, string saveAsName)
        {
            string animViewerAnimStreamFilePath = Path.Combine(AppDirectories.ExecFolder, $"{game}AnimViewer_StreamAnim.pcc");

            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(animViewerAnimStreamFilePath);
            if (animSourceFilePath != null)
            {
                try
                {
                    Debug.WriteLine($"AnimViewer Loading: {animSourceFilePath} #{animSequenceUIndex}");

                    using IMEPackage animSourceFile = MEPackageHandler.OpenMEPackage(animSourceFilePath);

                    ExportEntry sourceAnimSeq = animSourceFile.GetUExport(animSequenceUIndex);

                    var animInterpData = pcc.FindExport("TheWorld.PersistentLevel.Main_Sequence.InterpData_0");
                    var animTrack = pcc.FindExport("TheWorld.PersistentLevel.Main_Sequence.InterpData_0.InterpGroup_0.InterpTrackAnimControl_0");
                    var dynamicAnimSet = pcc.FindExport("TheWorld.PersistentLevel.Main_Sequence.KIS_DYN_Animset");

                    IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceAnimSeq.ParentFullPath, animSourceFile, pcc, new RelinkerOptionsPackage());

                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAnimSeq, pcc, parent, true, new RelinkerOptionsPackage(), out IEntry ent);
                    ExportEntry importedAnimSeq = (ExportEntry)ent;

                    NameReference seqName = importedAnimSeq.GetProperty<NameProperty>("SequenceName").Value;
                    float seqLength = importedAnimSeq.GetProperty<FloatProperty>("SequenceLength");
                    IEntry bioAnimSet = pcc.GetEntry(importedAnimSeq.GetProperty<ObjectProperty>("m_pBioAnimSetData").Value);
                    string setName = importedAnimSeq.ObjectName.Name.RemoveRight(seqName.Name.Length + 1);

                    // This seems to come up on 'AnimSequence' (LE1)
                    if (string.IsNullOrWhiteSpace(setName))
                        setName = seqName.Name;

                    animInterpData.WriteProperty(new FloatProperty(seqLength, "InterpLength"));

                    var animSeqKeys = animTrack.GetProperty<ArrayProperty<StructProperty>>("AnimSeqs");
                    animSeqKeys[0].Properties.AddOrReplaceProp(new NameProperty(seqName, "AnimSeqName"));
                    animTrack.WriteProperty(animSeqKeys);

                    dynamicAnimSet.WriteProperty(new ObjectProperty(bioAnimSet.UIndex, "m_pBioAnimSetData"));
                    dynamicAnimSet.WriteProperty(new NameProperty(setName, "m_nmOrigSetName"));
                    dynamicAnimSet.WriteProperty(new ArrayProperty<ObjectProperty>("Sequences")
                        {
                            new ObjectProperty(importedAnimSeq.UIndex)
                        });
                }
                catch
                {
                    MessageBox.Show($"Error loading {animSourceFilePath} #{animSequenceUIndex}");
                }
            }

            string tempFilePath = Path.Combine(MEDirectories.GetCookedPath(pcc.Game), $"{saveAsName}.pcc");
            pcc.Save(tempFilePath);

            // Only ME3 needs this
            if (pcc.Game == MEGame.ME3)
            {
                InteropHelper.TryPadFile(tempFilePath, 10_485_760);
            }
        }

        /// <summary>
        /// Opens a map file in the game
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="canHotLoad"></param>
        /// <param name="shouldPad"></param>
        /// <param name="mapName">Name of the map file - without extension</param>
        public static void OpenMapInGame(MEGame game, bool canHotLoad = false, bool shouldPad = true, string mapName = null)
        {
            var interopTarget = GameController.GetInteropTargetForGame(game);
            var tempMapName = mapName ?? GameController.TempMapName;
            string tempDir = MEDirectories.GetCookedPath(game);
            string tempFilePath = Path.Combine(tempDir, $"{tempMapName}.pcc");

            //pcc.Save(tempFilePath); // This eventually needs reinstated!!

            // LE games don't need this
            if (game is MEGame.ME3 && shouldPad)
            {
                if (!InteropHelper.TryPadFile(tempFilePath))
                {
                    //if file was too big to pad, hotloading is impossible 
                    canHotLoad = false;
                }
            }

            if (interopTarget.TryGetProcess(out var gameProcess) && (canHotLoad || game.IsLEGame()))
            {
                if (game.IsLEGame())
                {
                    // LE
                    interopTarget.ModernExecuteConsoleCommand($"at {tempMapName}");
                }
                else
                {
                    // ME3 
                    interopTarget.ME3ExecuteConsoleCommands($"at {tempMapName}");
                }
                return;
            }

            gameProcess?.Kill();
            int resX = 1000;
            int resY = 800;
            int posX = (int)(SystemParameters.PrimaryScreenWidth - (resX + 100));
            int posY = (int)(SystemParameters.PrimaryScreenHeight - resX) / 2;
            TOCCreator.CreateTOCForGame(game);
            var args = $"{tempMapName} -nosplash -nostartupmovies -Windowed ResX={resX} ResY={resY} WindowPosX={posX} WindowPosY={posY}";
            if (game is MEGame.LE1 or MEGame.LE2)
            {
                args += " -NOHOMEDIR";
            }

            ProcessStartInfo psi = new ProcessStartInfo(MEDirectories.GetExecutablePath(game))
            {
                Arguments = args,
                WorkingDirectory = MEDirectories.GetExecutableFolderPath(game)
            };
            Process.Start(psi);
        }
    }
}
