using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;

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

        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, ClassInfo info)
        {
            MEGame game = pcc.Game;
            if (info.ClassName == "Sequence")
            {
                return new PropertyCollection { new ArrayProperty<ObjectProperty>("SequenceObjects") };
            }
            if (info.IsOrInheritsFrom(SequenceVariableName, game))
            {
                return new PropertyCollection();
            }

            PropertyCollection defaults = new PropertyCollection();

            ArrayProperty<StructProperty> varLinksProp = null;
            ArrayProperty<StructProperty> outLinksProp = null;
            Dictionary<string, ClassInfo> classes = UnrealObjectInfo.GetClasses(game);
            try
            {
                while (info != null && (varLinksProp == null || outLinksProp == null))
                {
                    string filepath = Path.Combine(MEDirectories.BioGamePath(game), info.pccPath);
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    else if (info.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                    {
                        filepath = App.CustomResourceFilePath(game);
                    }
                    else if (game == MEGame.ME1)
                    {
                        filepath = Path.Combine(ME1Directory.gamePath, info.pccPath); //for files from ME1 DLC
                    }
                    if (File.Exists(filepath))
                    {
                        using IMEPackage importPCC = MEPackageHandler.OpenMEPackage(filepath);
                        ExportEntry classExport = importPCC.GetUExport(info.exportIndex);
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
                                        EntryImporter.EnsureClassIsInFile(pcc, expectedVar.ObjectName) is IEntry portedExpectedVar)
                                    {
                                        expectedTypeProp.Value = portedExpectedVar.UIndex;
                                    }
                                }
                            }
                            if (outLinksProp == null && prop.Name == "OutputLinks" && prop is ArrayProperty<StructProperty> olp)
                            {
                                outLinksProp = olp;
                            }
                        }
                    }

                    classes.TryGetValue(info.baseClass, out info);
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

            //remove links if empty
            if (defaults.GetProp<ArrayProperty<StructProperty>>("OutputLinks") is ArrayProperty<StructProperty> outLinks && outLinks.Count == 0)
            {
                defaults.Remove(outLinks);
            }
            if (defaults.GetProp<ArrayProperty<StructProperty>>("VariableLinks") is ArrayProperty<StructProperty> varLinks && varLinks.IsEmpty())
            {
                defaults.Remove(varLinks);
            }



            return defaults;
        }
    }
}
