using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.Packages;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using StructProperty = LegendaryExplorerCore.Unreal.StructProperty;

namespace LegendaryExplorer.Tools.Sequence_Editor
{
    public static class SequenceObjectCreator
    {
        private const string SequenceEventName = "SequenceEvent";
        private const string SequenceConditionName = "SequenceCondition";
        private const string SequenceActionName = "SequenceAction";
        private const string SequenceVariableName = "SequenceVariable";

        public static List<ClassInfo> GetCommonObjects(MEGame game)
        {
            return new List<string>
            {
                "Sequence",
                "SeqAct_Interp",
                "InterpData",
                "BioSeqAct_EndCurrentConvNode",
                "BioSeqEvt_ConvNode",
                "BioSeqVar_ObjectFindByTag",
                "SeqVar_Object",
                "SeqAct_ActivateRemoteEvent",
                "SeqEvent_SequenceActivated",
                "SeqAct_Delay",
                "SeqAct_Gate",
                "BioSeqAct_PMCheckState",
                "BioSeqAct_PMExecuteTransition",
                "SeqAct_FinishSequence"
            }.Select(className => GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className)).NonNull().ToList();
        }

        public static List<ClassInfo> GetSequenceVariables(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceVariableName, game);
            return classes;
        }

        public static List<ClassInfo> GetSequenceActions(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceActionName, game);
            return classes;
        }

        public static List<ClassInfo> GetSequenceEvents(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceEventName, game);
            return classes;
        }

        public static List<ClassInfo> GetSequenceConditions(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceConditionName, game);
            return classes;
        }

        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, string className, MEGame game) => GetSequenceObjectDefaults(pcc, GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className));

        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, ClassInfo info)
        {
            MEGame game = pcc.Game;
            PropertyCollection defaults = new();
            if (info.ClassName == "Sequence")
            {
                defaults.Add(new ArrayProperty<ObjectProperty>("SequenceObjects"));
            }
            else if (!info.IsA(SequenceVariableName, game))
            {
                ArrayProperty<StructProperty> varLinksProp = null;
                ArrayProperty<StructProperty> outLinksProp = null;
                ArrayProperty<StructProperty> eventLinksProp = null;
                ArrayProperty<StructProperty> inLinksProp = null;
                Dictionary<string, ClassInfo> classes = GlobalUnrealObjectInfo.GetClasses(game);
                try
                {
                    ClassInfo classInfo = info;
                    while (classInfo != null && (varLinksProp is null || outLinksProp is null || eventLinksProp is null || game == MEGame.ME1 && inLinksProp is null))
                    {
                        string filepath = Path.Combine(MEDirectories.GetBioGamePath(game), classInfo.pccPath);
                        Stream loadStream = null;
                        if (File.Exists(classInfo.pccPath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(classInfo.pccPath);
                        }
                        else if (classInfo.pccPath == GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                        {
                            loadStream = LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(game);
                        }
                        else if (File.Exists(filepath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(filepath);
                        }
                        else if (game == MEGame.ME1)
                        {
                            filepath = Path.Combine(ME1Directory.DefaultGamePath, classInfo.pccPath); //for files from ME1 DLC
                            if (File.Exists(filepath))
                            {
                                loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(filepath);
                            }
                        }
                        if (loadStream != null)
                        {
                            using IMEPackage importPCC = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                            ExportEntry classExport = importPCC.GetUExport(classInfo.exportIndex);
                            UClass classBin = ObjectBinary.From<UClass>(classExport);
                            ExportEntry classDefaults = importPCC.GetUExport(classBin.Defaults);

                            foreach (var prop in classDefaults.GetProperties())
                            {
                                if (varLinksProp == null && prop.Name == "VariableLinks" && prop is ArrayProperty<StructProperty> vlp)
                                {
                                    varLinksProp = vlp;
                                    //relink ExpectedType
                                    foreach (StructProperty varLink in varLinksProp)
                                    {
                                        if (varLink.GetProp<ObjectProperty>("ExpectedType") is ObjectProperty expectedTypeProp &&
                                            importPCC.TryGetEntry(expectedTypeProp.Value, out IEntry expectedVar) &&
                                            EntryImporter.EnsureClassIsInFile(pcc, expectedVar.ObjectName, RelinkResultsAvailable: EntryImporterExtended.ShowRelinkResults) is IEntry portedExpectedVar)
                                        {
                                            expectedTypeProp.Value = portedExpectedVar.UIndex;
                                        }
                                    }
                                }
                                if (outLinksProp == null && prop.Name == "OutputLinks" && prop is ArrayProperty<StructProperty> olp)
                                {
                                    outLinksProp = olp;
                                }

                                if (eventLinksProp == null && prop.Name == "EventLinks" && prop is ArrayProperty<StructProperty> elp)
                                {
                                    eventLinksProp = elp;
                                    //relink ExpectedType
                                    foreach (StructProperty eventLink in eventLinksProp)
                                    {
                                        if (eventLink.GetProp<ObjectProperty>("ExpectedType") is ObjectProperty expectedTypeProp &&
                                            importPCC.TryGetEntry(expectedTypeProp.Value, out IEntry expectedVar) &&
                                            EntryImporter.EnsureClassIsInFile(pcc, expectedVar.ObjectName, RelinkResultsAvailable: EntryImporterExtended.ShowRelinkResults) is IEntry portedExpectedVar)
                                        {
                                            expectedTypeProp.Value = portedExpectedVar.UIndex;
                                        }
                                    }
                                }

                                // Jan 31 2021 change by Mgamerz: Not sure why it only adds input links if it's ME1
                                // I removed it to let other games work too
                                //if (game == MEGame.ME1 && inLinksProp is null && prop.Name == "InputLinks" && prop is ArrayProperty<StructProperty> ilp)
                                if (inLinksProp is null && prop.Name == "InputLinks" && prop is ArrayProperty<StructProperty> ilp)
                                {
                                        inLinksProp = ilp;
                                }
                            }
                        }
                        classes.TryGetValue(classInfo.baseClass, out classInfo);
                        switch (classInfo.ClassName)
                        {
                            case "SequenceFrame":
                            case "SequenceCondition":
                            case "SequenceObject":
                            case "SequenceReference":
                            case "Sequence":
                            case "SequenceVariable":
                                goto loopend;

                        }
                    }
                    loopend: ;
                }
                catch
                {
                    // ignored
                }
                if (varLinksProp != null)
                {
                    defaults.Add(varLinksProp);
                }
                if (outLinksProp != null)
                {
                    defaults.Add(outLinksProp);
                }
                if (eventLinksProp != null)
                {
                    defaults.Add(eventLinksProp);
                }
                if (inLinksProp != null)
                {
                    defaults.Add(inLinksProp);
                }

                //remove links if empty
                if (defaults.GetProp<ArrayProperty<StructProperty>>("OutputLinks") is { } outLinks && outLinks.IsEmpty())
                {
                    defaults.Remove(outLinks);
                }
                if (defaults.GetProp<ArrayProperty<StructProperty>>("VariableLinks") is { } varLinks && varLinks.IsEmpty())
                {
                    defaults.Remove(varLinks);
                }
                if (defaults.GetProp<ArrayProperty<StructProperty>>("EventLinks") is { } eventLinks && eventLinks.IsEmpty())
                {
                    defaults.Remove(eventLinks);
                }
                if (defaults.GetProp<ArrayProperty<StructProperty>>("InputLinks") is { } inputLinks && inputLinks.IsEmpty())
                {
                    defaults.Remove(inputLinks);
                }
            }

            int objInstanceVersion = GlobalUnrealObjectInfo.getSequenceObjectInfo(game, info.ClassName)?.ObjInstanceVersion ?? 1;
            defaults.Add(new IntProperty(objInstanceVersion, "ObjInstanceVersion"));

            return defaults;
        }

        public static ExportEntry CreateSequenceObject(IMEPackage pcc, string className, MEGame game)
        {
            var seqObj = new ExportEntry(pcc, 0, pcc.GetNextIndexedName(className), properties: GetSequenceObjectDefaults(pcc, className, game))
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, RelinkResultsAvailable: EntryImporterExtended.ShowRelinkResults)
            };
            seqObj.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(seqObj);
            return seqObj;
        }
    }
}
