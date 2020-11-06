using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3Explorer.Packages;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Explorer.Sequence_Editor
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
            }.Select(className => UnrealObjectInfo.GetClassOrStructInfo(game, className)).NonNull().ToList();
        }

        public static List<ClassInfo> GetSequenceVariables(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceVariableName, game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceActions(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceActionName, game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceEvents(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceEventName, game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceConditions(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceConditionName, game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, string className, MEGame game) => GetSequenceObjectDefaults(pcc, UnrealObjectInfo.GetClassOrStructInfo(game, className));

        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, ClassInfo info)
        {
            MEGame game = pcc.Game;
            PropertyCollection defaults = new PropertyCollection();
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
                Dictionary<string, ClassInfo> classes = UnrealObjectInfo.GetClasses(game);
                try
                {
                    ClassInfo classInfo = info;
                    while (classInfo != null && (varLinksProp is null || outLinksProp is null || eventLinksProp is null || game == MEGame.ME1 && inLinksProp is null))
                    {
                        string filepath = Path.Combine(MEDirectories.BioGamePath(game), classInfo.pccPath);
                        Stream loadStream = null;
                        if (File.Exists(classInfo.pccPath))
                        {
                            loadStream = new MemoryStream(File.ReadAllBytes(classInfo.pccPath));
                        }
                        else if (classInfo.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                        {
                            loadStream = Utilities.GetCustomAppResourceStream(game);
                        }
                        else if (File.Exists(filepath))
                        {
                            loadStream = new MemoryStream(File.ReadAllBytes(filepath));
                        }
                        else if (game == MEGame.ME1)
                        {
                            filepath = Path.Combine(ME1Directory.gamePath, classInfo.pccPath); //for files from ME1 DLC
                            if (File.Exists(filepath))
                            {
                                loadStream = new MemoryStream(File.ReadAllBytes(filepath));
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
                                            EntryImporterExtended.EnsureClassIsInFile(pcc, expectedVar.ObjectName) is IEntry portedExpectedVar)
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
                                            EntryImporterExtended.EnsureClassIsInFile(pcc, expectedVar.ObjectName) is IEntry portedExpectedVar)
                                        {
                                            expectedTypeProp.Value = portedExpectedVar.UIndex;
                                        }
                                    }
                                }

                                if (game == MEGame.ME1 && inLinksProp is null && prop.Name == "InputLinks" && prop is ArrayProperty<StructProperty> ilp)
                                {
                                    inLinksProp = ilp;
                                }
                            }
                        }
                        classes.TryGetValue(classInfo.baseClass, out classInfo);
                    }
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

            int objInstanceVersion = UnrealObjectInfo.getSequenceObjectInfo(game, info.ClassName)?.ObjInstanceVersion ?? 1;
            defaults.Add(new IntProperty(objInstanceVersion, "ObjInstanceVersion"));

            return defaults;
        }

        public static ExportEntry CreateSequenceObject(IMEPackage pcc, string className, MEGame game)
        {
            var seqObj = new ExportEntry(pcc, properties: GetSequenceObjectDefaults(pcc, className, game))
            {
                ObjectName = pcc.GetNextIndexedName(className),
                Class = EntryImporterExtended.EnsureClassIsInFile(pcc, className)
            };
            seqObj.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(seqObj);
            return seqObj;
        }
    }
}
