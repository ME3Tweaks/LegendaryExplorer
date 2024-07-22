using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using StructProperty = LegendaryExplorerCore.Unreal.StructProperty;

namespace LegendaryExplorerCore.Kismet
{
    /// <summary>
    /// Static methods to handle sequence variable creation
    /// </summary>
    public static class SequenceObjectCreator
    {
        private const string SequenceEventName = "SequenceEvent";
        private const string SequenceConditionName = "SequenceCondition";
        private const string SequenceActionName = "SequenceAction";
        private const string SequenceVariableName = "SequenceVariable";

        /// <summary>
        /// Gets the class info for some common sequence object classes
        /// </summary>
        /// <param name="game">Game to get sequence objects for</param>
        /// <returns>ClassInfos for common objects</returns>
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
                "SeqAct_FinishSequence",
                "SeqEvent_RemoteEvent"
            }.Select(className => GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className)).NonNull().ToList();
        }

        /// <summary>
        /// Gets the class info for most sequence variable classes in the ObjectInfo
        /// </summary>
        /// <remarks>Some SeqVar classes are excluded</remarks>
        /// <param name="game">Game to get class info for</param>
        /// <returns>ClassInfos for SeqVar classes</returns>
        public static List<ClassInfo> GetSequenceVariables(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceVariableName, game);
            classes.RemoveAll(info => info.ClassName is "SeqVar_Byte" or "SeqVar_Group" or "SeqVar_Character" or "SeqVar_Union" or "SeqVar_UniqueNetId");
            return classes;
        }

        /// <summary>
        /// Gets the class info for all sequence action classes in the ObjectInfo
        /// </summary>
        /// <param name="game">Game to get class info for</param>
        /// <returns>ClassInfos for SeqAct classes</returns>
        public static List<ClassInfo> GetSequenceActions(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceActionName, game);
            return classes;
        }

        /// <summary>
        /// Gets the class info for all sequence event classes in the ObjectInfo
        /// </summary>
        /// <param name="game">Game to get class info for</param>
        /// <returns>ClassInfos for SeqEvt classes</returns>
        public static List<ClassInfo> GetSequenceEvents(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceEventName, game);
            return classes;
        }

        /// <summary>
        /// Gets the class info for all sequence condition classes in the ObjectInfo
        /// </summary>
        /// <param name="game">Game to get class info for</param>
        /// <returns>ClassInfos for SeqCond classes</returns>
        public static List<ClassInfo> GetSequenceConditions(MEGame game)
        {
            List<ClassInfo> classes = GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf(SequenceConditionName, game);
            return classes;
        }

        /// <summary>
        /// Creates the default sequence object properties that should be on a new sequence object
        /// </summary>
        /// <param name="pcc">Package file new sequence object is in</param>
        /// <param name="className">Sequence object class to get defaults for</param>
        /// <param name="game">Game that defaults should be for</param>
        /// <param name="pc">Optional: PackageCache for relinker</param>
        /// <returns>PropertyCollection of default props</returns>
        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, string className, MEGame game, PackageCache pc = null) => GetSequenceObjectDefaults(pcc, GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className), pc);

        /// <summary>
        /// Creates the default sequence object properties that should be on a new sequence object
        /// </summary>
        /// <remarks>
        /// This method will handle porting in imports needed to set up links properly.
        /// Will add default value properties for most SeqVar types.
        /// </remarks>
        /// <param name="pcc">Package file sequence object is in</param>
        /// <param name="info">ClassInfo of sequence object class to get defaults for</param>
        /// <param name="pc">Optional: PackageCache for relinker</param>
        /// <returns>PropertyCollection of default props</returns>
        public static PropertyCollection GetSequenceObjectDefaults(IMEPackage pcc, ClassInfo info, PackageCache pc = null)
        {
            pc ??= new PackageCache();
            MEGame game = pcc.Game;
            PropertyCollection defaults = new();
            if (info.ClassName == "Sequence")
            {
                defaults.Add(new ArrayProperty<ObjectProperty>("SequenceObjects"));
            }
            else if (info.IsA(SequenceVariableName, game))
            {
                switch (info.ClassName)
                {
                    case "SeqVar_Bool":
                        defaults.Add(new IntProperty(0, "bValue"));
                        break;
                    case "SeqVar_External":
                        defaults.Add(new StrProperty("", "VariableLabel"));
                        defaults.Add(new ObjectProperty(0, "ExpectedType"));
                        break;
                    case "SeqVar_Float":
                        defaults.Add(new FloatProperty(0, "FloatValue"));
                        break;
                    case "SeqVar_Int":
                        defaults.Add(new IntProperty(0, "IntValue"));
                        break;
                    case "SeqVar_Name":
                        defaults.Add(new NameProperty("None", "NameValue"));
                        break;
                    case "SeqVar_Named":
                    case "SeqVar_ScopedNamed":
                        defaults.Add(new NameProperty("None", "FindVarName"));
                        defaults.Add(new ObjectProperty(0, "ExpectedType"));
                        break;
                    case "SeqVar_Object":
                    case "SeqVar_ObjectVolume":
                        defaults.Add(new ObjectProperty(0, "ObjValue"));
                        break;
                    case "SeqVar_RandomFloat":
                        defaults.Add(new FloatProperty(0, "Min"));
                        defaults.Add(new FloatProperty(1, "Max"));
                        break;
                    case "SeqVar_RandomInt":
                        defaults.Add(new IntProperty(0, "Min"));
                        defaults.Add(new IntProperty(100, "Max"));
                        break;
                    case "SeqVar_String":
                        defaults.Add(new StrProperty("", "StrValue"));
                        break;
                    case "SeqVar_Vector":
                        defaults.Add(CommonStructs.Vector3Prop(0, 0, 0, "VectValue"));
                        break;
                    case "SFXSeqVar_Rotator":
                        defaults.Add(CommonStructs.RotatorProp(0, 0, 0, "m_Rotator"));
                        break;
                    case "SFXSeqVar_ToolTip":
                        defaults.Add(new EnumProperty("TargetTipText_Use", "ETargetTipText", pcc.Game, "TipText"));
                        break;
                    case "BioSeqVar_ObjectFindByTag" when pcc.Game.IsGame3():
                        defaults.Add(new NameProperty("None", "m_sObjectTagToFind"));
                        break;
                    case "BioSeqVar_ObjectFindByTag":
                    case "BioSeqVar_ObjectListFindByTag":
                        defaults.Add(new StrProperty("", "m_sObjectTagToFind"));
                        break;
                    case "BioSeqVar_StoryManagerBool":
                    case "BioSeqVar_StoryManagerFloat":
                    case "BioSeqVar_StoryManagerInt":
                    case "BioSeqVar_StoryManagerStateId":
                        defaults.Add(new IntProperty(-1, "m_nIndex"));
                        break;
                    case "BioSeqVar_StrRef":
                        defaults.Add(new StringRefProperty(0, "m_srValue"));
                        break;
                    case "BioSeqVar_StrRefLiteral":
                        defaults.Add(new IntProperty(0, "m_srStringID"));
                        break;
                    default:
                    case "SeqVar_ObjectList":
                    case "SeqVar_Player":
                    case "SFXSeqVar_Hench":
                    case "SFXSeqVar_SavedBool":
                    case "BioSeqVar_ChoiceGUIData":
                    case "BioSeqVar_SFXArray":
                        break;
                }
            }
            else
            {
                ArrayProperty<StructProperty> varLinksProp = null;
                ArrayProperty<StructProperty> outLinksProp = null;
                ArrayProperty<StructProperty> eventLinksProp = null;
                ArrayProperty<StructProperty> inLinksProp = null;
                Dictionary<string, ClassInfo> classes = GlobalUnrealObjectInfo.GetClasses(game);
                try
                {
                    ClassInfo classInfo = info;
                    while (classInfo != null && (varLinksProp is null || outLinksProp is null || eventLinksProp is null || inLinksProp is null))
                    {
                        string filepath = Path.Combine(MEDirectories.GetBioGamePath(game), classInfo.pccPath);
                        string loadPath = null;
                        Stream loadStream = null;
                        if (File.Exists(classInfo.pccPath))
                        {
                            loadPath = classInfo.pccPath;
                        }
                        else if (classInfo.pccPath == GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                        {
                            loadStream = LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(game);
                        }
                        else if (File.Exists(filepath))
                        {
                            loadPath = filepath;
                        }
                        else if (game == MEGame.ME1)
                        {
                            filepath = Path.Combine(ME1Directory.DefaultGamePath, classInfo.pccPath); //for files from ME1 DLC
                            if (File.Exists(filepath))
                            {
                                loadPath = filepath;
                            }
                        }
                        if (loadStream != null || loadPath != null)
                        {
                            IMEPackage importPCC;
                            if (loadStream is null)
                            {
                                pc.TryGetCachedPackage(loadPath, true, out importPCC);
                            }
                            else
                            {
                                importPCC = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                            }
                            ExportEntry classExport = importPCC.GetUExport(classInfo.exportIndex);
                            var classBin = ObjectBinary.From<UClass>(classExport);
                            ExportEntry classDefaults = importPCC.GetUExport(classBin.Defaults);

                            var rop = new RelinkerOptionsPackage { Cache = pc ?? new PackageCache() };

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
                                            EntryImporter.EnsureClassIsInFile(pcc, expectedVar.ObjectName, rop) is IEntry portedExpectedVar)
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
                                            EntryImporter.EnsureClassIsInFile(pcc, expectedVar.ObjectName, rop) is IEntry portedExpectedVar)
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
                            if (!pc.TryGetCachedPackage(loadPath, false, out _)) importPCC.Dispose(); // Can't do a using statement because of the pc out var - not good enough at c# to fix
                        }
                        classes.TryGetValue(classInfo.baseClass, out classInfo);
                        switch (classInfo.ClassName)
                        {
                            case SequenceConditionName:
                                classes.TryGetValue(classInfo.baseClass, out classInfo);
                                break;
                            case "SequenceFrame":
                            case "SequenceObject":
                            case "SequenceReference":
                            case "Sequence":
                            case SequenceVariableName:
                                goto loopend;

                        }
                    }
                loopend:;
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

                // 08/30/2022 Add useful defaults for editor - Mgamerz
                // edited default to None as that is in every package and should be default if there is no named event to reference. - KK
                switch (info.ClassName)
                {
                    case "SeqEvent_Console":
                        defaults.Add(new NameProperty("None", "ConsoleEventName"));
                        break;
                    case "SeqEvent_RemoteEvent":
                    case "SeqAct_ActivateRemoteEvent":
                        defaults.Add(new NameProperty("None", "EventName"));
                        break;
                    case "BioSeqAct_PMExecuteTransition":
                    case "BioSeqAct_PMCheckState":
                    case "BioSeqAct_PMCheckConditional":
                        defaults.Add(new IntProperty(0, "m_nIndex"));
                        break;
                }
            }

            int objInstanceVersion = GlobalUnrealObjectInfo.GetSequenceObjectInfo(game, info.ClassName)?.ObjInstanceVersion ?? 1;
            defaults.Add(new IntProperty(objInstanceVersion, "ObjInstanceVersion"));

            return defaults;
        }

        /// <summary>
        /// Creates a new sequence object in a package file
        /// </summary>
        /// <param name="pcc">Package to add new sequence object to</param>
        /// <param name="className">Class of new sequence object</param>
        /// <param name="cache">PackageCache for relinker</param>
        /// <param name="handleRelinkResults">Invoked when relinking is complete and the export has been added. You can inspect the object for failed relink operations, for example.</param>
        /// <returns></returns>
        public static ExportEntry CreateSequenceObject(IMEPackage pcc, string className, PackageCache cache = null, Action<RelinkerOptionsPackage> handleRelinkResults = null)
        {
            var rop = new RelinkerOptionsPackage() { Cache = cache ?? new PackageCache() };
            var seqObj = new ExportEntry(pcc, 0, pcc.GetNextIndexedName(className), properties: GetSequenceObjectDefaults(pcc, className, pcc.Game, cache))
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, rop)
            };
            seqObj.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(seqObj);
            handleRelinkResults?.Invoke(rop);
            return seqObj;
        }
    }
}
