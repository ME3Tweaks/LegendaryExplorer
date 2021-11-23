using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Packages;
using LegendaryExplorer;
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
        public static string LiveFaceFxEditorFileName = "AAAME3EXPFACEFX1.pcc";
        public string LiveFaceFxEditorFilePath => Path.Combine(ME3Directory.CookedPCPath, LiveFaceFxEditorFileName);

        private IMEPackage SourcePcc;
        private IMEPackage LiveFile;

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
                Filter = GameFileFilters.ME3ME2SaveFileFilter,
                CheckFileExists = true,
                Multiselect = false,
                Title = "Select ME3 file containing the FaceFXAnimSet you want to edit"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    SourcePcc = MEPackageHandler.OpenME3Package(dlg.FileName);
                }
                catch (FormatException)
                {
                    MessageBox.Show("Must be an ME3 file!");
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
            File.Copy(Path.Combine(AppDirectories.ExecFolder, "ME3EmptyLevel.pcc"), filePath);
            LiveFile = MEPackageHandler.OpenMEPackage(filePath);
            for (int i = 0; i < LiveFile.Names.Count; i++)
            {
                if (LiveFile.Names[i].Equals("ME3EmptyLevel"))
                {
                    LiveFile.replaceName(i, Path.GetFileNameWithoutExtension(filePath));
                }
            }

            var packguid = Guid.NewGuid();
            var package = LiveFile.GetUExport(1);
            package.PackageGUID = packguid;
            LiveFile.PackageGuid = packguid;

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, SourceAnimSet.Export.Parent, LiveFile, null, true, new RelinkerOptionsPackage(), out _);

            var gender = SourceAnimSet.Export.ObjectNameString.Last() switch
            {
                'F' => FaceFXGender.Female,
                'M' => FaceFXGender.Male,
                _ => FaceFXGender.NonSpkr
            };

            ActorTag = null;
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
                        ActorTag = $"Player_{(isFemale ? 'F' : 'M')}";
                        IEntry ent;
                        using (IMEPackage soldierFile = MEPackageHandler.OpenME3Package(Path.Combine(ME3Directory.CookedPCPath, "SFXCharacterClass_Soldier.pcc")))
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, soldierFile.GetUExport(isFemale ? 10327 : 10330),
                                                             LiveFile, LiveFile.GetUExport(3), true, new RelinkerOptionsPackage(), out ent);

                        }
                        ExportEntry actor = (ExportEntry)ent;
                        LiveFile.AddToLevelActorsIfNotThere(actor);
                        actor.WriteProperty(new NameProperty(ActorTag, "Tag"));
                        
                        using (IMEPackage clothingFile = MEPackageHandler.OpenME3Package(Path.Combine(ME3Directory.CookedPCPath, $"BIOG_HM{(isFemale ? "F" : "M")}_ARM_CTH_R.pcc")))
                        {
                            var clothingPackage = EntryImporter.GetOrAddCrossImportOrPackage("CTHa", clothingFile, LiveFile, new RelinkerOptionsPackage());
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, clothingFile.GetUExport(isFemale ? 1625 : 1966),
                                                                 LiveFile, clothingPackage, true, new RelinkerOptionsPackage(), out ent);
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
                                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, parentFile.GetUExport(seqVar.GetProperty<ObjectProperty>("ObjValue").Value), LiveFile, LiveFile.GetUExport(3), true, new RelinkerOptionsPackage(), out var ent);
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

            var mainSeq = LiveFile.GetUExport(9);
            var interp = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "SeqAct_Interp");
            KismetHelper.AddObjectToSequence(interp, mainSeq);
            var interpData = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "InterpData");
            KismetHelper.AddObjectToSequence(interpData, mainSeq);
            KismetHelper.CreateVariableLink(interp, "Data", interpData);
            var playEvent = LEXSequenceObjectCreator.CreateSequenceObject(LiveFile, "SeqEvent_Console");
            playEvent.WriteProperty(new NameProperty("playfacefx", "ConsoleEventName"));
            KismetHelper.AddObjectToSequence(playEvent, mainSeq);
            KismetHelper.CreateOutputLink(playEvent, "Out", interp);

            var interpGroup = MatineeHelper.AddNewGroupToInterpData(interpData, ActorTag);


            LiveFile.Save();

            IMEPackage getParentFile()
            {
                var dlgFileName = Path.GetFileNameWithoutExtension(SourcePcc.FilePath);
                if (dlgFileName.EndsWith("LOC_INT", StringComparison.OrdinalIgnoreCase))
                {
                    if (MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3).TryGetValue(dlgFileName.Substring(0, dlgFileName.Length - 7), out string parentFilePath))
                    {
                        return MEPackageHandler.OpenME3Package(parentFilePath);
                    }
                }

                return null;
            }

            void importTaggedActor(IMEPackage parentFile)
            {
                var stuntActor = parentFile.Exports.FirstOrDefault(exp => exp.ClassName == "SFXStuntActor" &&
                                                                          ActorTag.CaseInsensitiveEquals(exp.GetProperty<NameProperty>("Tag")?.Value.Name));
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, stuntActor, LiveFile, LiveFile.GetUExport(3), true, new RelinkerOptionsPackage(), out var ent);
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
