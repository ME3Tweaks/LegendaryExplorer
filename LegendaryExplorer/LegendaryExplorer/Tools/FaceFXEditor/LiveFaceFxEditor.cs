using System;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.FaceFXEditor
{
    public class LiveFaceFxEditor : NotifyPropertyChangedBase, IDisposable
    {
        public string LiveFaceFxEditorFileName => $"AAA{Game}EXPFACEFX1.pcc";
        public string LiveFaceFxEditorFilePath => Path.Combine(MEDirectories.GetCookedPath(Game), LiveFaceFxEditorFileName);

        private IMEPackage SourcePcc;
        private IMEPackage LiveFile;
        private MEGame Game = MEGame.LE3;

        public FaceFXAnimSet SourceAnimSet;

        public string ActorTag;

        public void OpenAnimSet()
        {
            SourceAnimSet = null;
            SourcePcc?.Dispose();
            SourcePcc = null;
            LiveFile?.Dispose();
            LiveFile = null;
            ActorTag = null;

            var dlg = new OpenFileDialog
            {
                Filter = GameFileFilters.LESaveFileFilter,
                CheckFileExists = true,
                Multiselect = false,
                Title = $"Select {Game} file containing the FaceFXAnimSet you want to edit",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (dlg.ShowDialog() == true)
            {
                SourcePcc = MEPackageHandler.OpenMEPackage(dlg.FileName);
                if (SourcePcc.Game != Game)
                {
                    MessageBox.Show($"Must be an {Game} file!");
                    return;
                }

                if (EntrySelector.GetEntry<ExportEntry>(null, SourcePcc, "Choose FaceFxAnimSet to edit", exp => exp.ClassName == "FaceFXAnimSet") is ExportEntry animSetEntry)
                {
                    SourceAnimSet = animSetEntry.GetBinaryData<FaceFXAnimSet>();
                }
            }
        }

        public void CreateLiveEditFile()
        {
            string filePath = LiveFaceFxEditorFilePath;
            MEPackageHandler.CreateEmptyLevel(filePath, Game);
            LiveFile = MEPackageHandler.OpenMEPackage(filePath);

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, SourceAnimSet.Export.Parent, LiveFile, null, true, new RelinkerOptionsPackage(), out _);

            var gender = SourceAnimSet.Export.ObjectNameString.Last() switch
            {
                'F' => FaceFXGender.Female,
                'M' => FaceFXGender.Male,
                _ => FaceFXGender.NonSpkr
            };

            ActorTag = null;
            ExportEntry levelExport = LiveFile.FindExport("TheWorld.PersistentLevel");
            ExportEntry mainSeqExport = LiveFile.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            try
            {
                if (gender is not FaceFXGender.NonSpkr)
                {
                    bool isFemale = gender is FaceFXGender.Female;
                    var bioConv = LiveFile.Exports.First(exp => exp.Class.ObjectName == "BioConversation");
                    var LiveFileAnimSet = LiveFile.FindExport(SourceAnimSet.Export.InstancedFullPath);
                    var propName = isFemale ? "m_aMaleFaceSets" : "m_aFemaleFaceSets" ;
                    int idx = bioConv.GetProperty<ArrayProperty<ObjectProperty>>(propName).FindIndex(objProp => objProp.Value == LiveFileAnimSet.UIndex);
                    if (idx is 0)
                    {
                        //player
                        char genderChar = isFemale ? 'F' : 'M';
                        ActorTag = $"Player_{genderChar}";
                        IEntry ent = null;
                        if (Game.IsGame3())
                        {
                            using (IMEPackage soldierFile = MEPackageHandler.OpenMEPackage(Path.Combine(MEDirectories.GetCookedPath(Game), "SFXCharacterClass_Soldier.pcc")))
                            {
                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, soldierFile.FindExport($"BioChar_Player.Archetypes.UIWorld.{(isFemale ? "Fem" : "M")}aleShepard_UIWorld"),
                                    LiveFile, levelExport, true, new RelinkerOptionsPackage(), out ent);

                            }
                        }
                        ExportEntry actor = (ExportEntry)ent;
                        LiveFile.AddToLevelActorsIfNotThere(actor);
                        actor.WriteProperty(new NameProperty(ActorTag, "Tag"));

                        if (Game.IsGame3())
                        {
                            using (IMEPackage clothingFile = MEPackageHandler.OpenMEPackage(Path.Combine(MEDirectories.GetCookedPath(Game), $"BIOG_HM{genderChar}_ARM_CTH_R.pcc")))
                            {
                                var clothingPackage = EntryImporter.GetOrAddCrossImportOrPackage("CTHa", clothingFile, LiveFile, new RelinkerOptionsPackage());
                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, clothingFile.FindExport($"CTHa.HM{genderChar}_ARM_CTHa_MDL"),
                                    LiveFile, clothingPackage, true, new RelinkerOptionsPackage(), out ent);
                            }
                        }

                        ExportEntry bodyComponent = LiveFile.GetUExport(actor.GetProperty<ObjectProperty>("BodyMesh").Value);
                        bodyComponent.WriteProperty(new ObjectProperty(ent.UIndex, "SkeletalMesh"));
                    }
                    else if (idx is 1)
                    {
                        //owner
                        using IMEPackage parentFile = getParentFile();
                        foreach (ExportEntry export in parentFile.Exports.Where(exp => exp.ClassName is "SFXSeqAct_StartConversation" or "SFXSeqAct_StartAmbientConv"))
                        {
                            if (export.GetProperty<ObjectProperty>("Conv") is ObjectProperty convProp && parentFile.TryGetImport(convProp.Value, out var convImport) &&
                                convImport.ObjectName == bioConv.ObjectName)
                            {
                                ExportEntry seqVar = parentFile.GetUExport(export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks")[0].GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")[0].Value);
                                if (seqVar.ClassName == "BioSeqVar_ObjectFindByTag")
                                {
                                    ActorTag = seqVar.GetProperty<NameProperty>("m_sObjectTagToFind").Value;
                                    if (!ActorTag.StartsWith("hench_", StringComparison.OrdinalIgnoreCase))
                                    {
                                        importTaggedActor(parentFile);
                                    }
                                }
                                else
                                {
                                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, parentFile.GetUExport(seqVar.GetProperty<ObjectProperty>("ObjValue").Value), LiveFile, levelExport, true, new RelinkerOptionsPackage(), out var ent);
                                    ExportEntry actor = (ExportEntry)ent;
                                    LiveFile.AddToLevelActorsIfNotThere(actor);
                                    ActorTag = actor.GetProperty<NameProperty>("Tag")?.Value.Name;
                                    if (ActorTag is null)
                                    {
                                        ActorTag = "ConvoOwner";
                                        actor.WriteProperty(new NameProperty(ActorTag, "Tag"));
                                    }
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        ActorTag = bioConv.GetProperty<ArrayProperty<NameProperty>>("m_aSpeakerList")[idx - 2].Value;
                        if (!ActorTag.StartsWith("hench_", StringComparison.OrdinalIgnoreCase))
                        {
                            using IMEPackage parentFile = getParentFile();
                            importTaggedActor(parentFile);
                        }
                    }
                }
                else
                {
                    //find nonspkr linkage somehow
                }
            }
            finally
            {
                //use generic if no better one found
                ActorTag ??= gender is FaceFXGender.Female ? "hench_ashley" : "hench_kaidan";
            }

            var interp = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "SeqAct_Interp");
            KismetHelper.AddObjectToSequence(interp, mainSeqExport);
            var interpData = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "InterpData");
            KismetHelper.AddObjectToSequence(interpData, mainSeqExport);
            KismetHelper.CreateVariableLink(interp, "Data", interpData);
            var playEvent = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "SeqEvent_Console");
            playEvent.WriteProperty(new NameProperty("playfacefx", "ConsoleEventName"));
            KismetHelper.AddObjectToSequence(playEvent, mainSeqExport);
            KismetHelper.CreateOutputLink(playEvent, "Out", interp);

            var interpGroup = MatineeHelper.AddNewGroupToInterpData(interpData, ActorTag);


            LiveFile.Save();

            IMEPackage getParentFile()
            {
                var dlgFileName = Path.GetFileNameWithoutExtension(SourcePcc.FilePath);
                if (dlgFileName.EndsWith("LOC_INT", StringComparison.OrdinalIgnoreCase))
                {
                    if (MELoadedFiles.GetFilesLoadedInGame(Game).TryGetValue(dlgFileName.Substring(0, dlgFileName.Length - 7), out string parentFilePath))
                    {
                        return MEPackageHandler.OpenMEPackage(parentFilePath);
                    }
                }

                return null;
            }

            void importTaggedActor(IMEPackage parentFile)
            {
                var stuntActor = parentFile.Exports.FirstOrDefault(exp => exp.ClassName == "SFXStuntActor" &&
                                                                          ActorTag.CaseInsensitiveEquals(exp.GetProperty<NameProperty>("Tag")?.Value.Name));
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, stuntActor, LiveFile, levelExport, true, new RelinkerOptionsPackage(), out var ent);
                LiveFile.AddToLevelActorsIfNotThere((ExportEntry)ent);
            }
        }

        enum FaceFXGender
        {
            NonSpkr,
            Female,
            Male
        }

        public void Dispose()
        {
            SourcePcc?.Dispose();
            LiveFile?.Dispose();
        }
    }
}
