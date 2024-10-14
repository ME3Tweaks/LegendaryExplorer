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
                        if (loadStream != null || loadPath != null || pcc.FilePath == classInfo.pccPath)
                        {
                            IMEPackage importPCC;
                            if (loadStream != null)
                            {
                                importPCC = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                            }
                            else if (loadPath != null)
                            {
                                pc.TryGetCachedPackage(loadPath, true, out importPCC);
                            }
                            else
                            {
                                // Memory-package, it won't have a stream or disk, but it's the one we passed in.
                                importPCC = pcc;
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

        #region OBJECT CREATION

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


        /// <summary>
        /// Creates a new sequence object in a package file and adds it to a given sequence. This is used to generate objects that don't have a code version.
        /// </summary>
        /// <param name="sequence">The sequence to add the object to</param>
        /// <param name="className">Class of new sequence object</param>
        /// <param name="cache">PackageCache for relinker</param>
        /// <returns>The newly created object</returns>
        public static ExportEntry CreateSequenceObject(ExportEntry sequence, string className, PackageCache cache = null)
        {
            var seqObj = CreateSequenceObject(sequence.FileRef, className, cache);
            KismetHelper.AddObjectToSequence(seqObj, sequence);
            return seqObj;
        }


        // The following is mostly from Mass Effect / Mass Effect 2 Randomizer (LE versions)
        // MERSeqTools.cs

        /// <summary>
        /// Installs a random switch with the given number of links.
        /// </summary>
        /// <param name="sequence">The sequence to install the random switch into</param>
        /// <param name="numLinks">The number of links to put on the switch</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateRandSwitch(ExportEntry sequence, int numLinks, PackageCache cache = null)
        {
            var nSwitch = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_RandomSwitch", cache);
            KismetHelper.AddObjectToSequence(nSwitch, sequence);
            // var properties = nSwitch.GetProperties();
            //    var packageBin = MEREmbedded.GetEmbeddedPackage(target.Game, "PremadeSeqObjs.pcc");
            //    var premadeObjsP = MEPackageHandler.OpenMEPackageFromStream(packageBin);

            //    // 1. Add the switch object and link it to the sequence
            //    var nSwitch = PackageTools.PortExportIntoPackage(target, sequence.FileRef, premadeObjsP.FindExport("SeqAct_RandomSwitch_0"), sequence.UIndex, false, true);
            //    KismetHelper.AddObjectToSequence(nSwitch, sequence);

            // 2. Generate the output links array. We will refresh the properties
            // with new structs so we don't have to make a copy constructor
            var olinks = nSwitch.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            while (olinks.Count < numLinks)
            {
                olinks.Add(olinks[0]); // Just add a bunch of the first link
            }

            nSwitch.WriteProperty(olinks);

            // Reload the olinks with unique structs now
            olinks = nSwitch.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            for (int i = 0; i < numLinks; i++)
            {
                olinks[i].GetProp<StrProperty>("LinkDesc").Value = $"Link {i + 1}";
            }

            nSwitch.WriteProperty(olinks);
            nSwitch.WriteProperty(new IntProperty(numLinks, "LinkCount"));

            return nSwitch;
        }

        /// <summary>
        /// Adds a new delay object to a sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="delay">Amount of time to delay, in seconds</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateDelay(ExportEntry sequence, float delay, PackageCache cache = null)
        {
            var newDelay = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_Delay", cache);
            KismetHelper.AddObjectToSequence(newDelay, sequence);
            newDelay.WriteProperty(new FloatProperty(delay, "Duration"));
            return newDelay;
        }


        /// <summary>
        /// Creates a new SeqVar_RandomFloat with the given value range in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="minValue">Min value for the float</param>
        /// <param name="maxValue">Max value for the float</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateRandFloat(ExportEntry sequence, float minValue, float maxValue, PackageCache cache = null)
        {
            var fFloat = CreateSequenceObject(sequence.FileRef, "SeqVar_RandomFloat", cache);
            KismetHelper.AddObjectToSequence(fFloat, sequence);

            fFloat.WriteProperty(new FloatProperty(minValue, "Min"));
            fFloat.WriteProperty(new FloatProperty(maxValue, "Max"));

            return fFloat;
        }

        /// <summary>
        /// Creates a new delay with a SeqVar_RandomFloat in the specified range
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="min">Minimum delay time</param>
        /// <param name="max">Maximum delay time</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateRandomDelay(ExportEntry sequence, float min, float max, PackageCache cache = null)
        {
            var newDelay = CreateSequenceObject(sequence.FileRef, "SeqAct_Delay", cache);
            var newRandFloat = CreateRandFloat(sequence, min, max);
            KismetHelper.AddObjectsToSequence(sequence, false, newDelay, newRandFloat);
            KismetHelper.CreateVariableLink(newDelay, "Duration", newRandFloat);
            return newDelay;
        }

        /// <summary>
        /// Creates a new SeqVar_Object with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="value">The value to set on the object. If null, 0 will be written instead.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateObject(ExportEntry sequence, ExportEntry value, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "SeqVar_Object", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new ObjectProperty(value?.UIndex ?? 0, "ObjValue"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_Int with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="value">The value to set the integer to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>        
        public static ExportEntry CreateInt(ExportEntry sequence, int value, PackageCache cache = null)
        {
            var iObj = CreateSequenceObject(sequence.FileRef, "SeqVar_Int", cache);
            KismetHelper.AddObjectToSequence(iObj, sequence);

            iObj.WriteProperty(new IntProperty(value, "IntValue"));

            return iObj;
        }

        /// <summary>
        /// Creates a new SeqVar_Float with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="value">The value to set the float to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>    
        public static ExportEntry CreateFloat(ExportEntry sequence, float value, PackageCache cache = null)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Float", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new FloatProperty(value, "FloatValue"));

            return fObj;
        }

        /// <summary>
        /// Creates a player object in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="returnsPawns"></param>
        /// <returns></returns>
        public static ExportEntry CreatePlayerObject(ExportEntry sequence, bool returnsPawns, PackageCache cache = null)
        {
            var player = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Player", cache);
            if (returnsPawns)
            {
                player.WriteProperty(new BoolProperty(true, "bReturnPawns"));
            }
            KismetHelper.AddObjectToSequence(player, sequence);
            return player;
        }

        /// <summary>
        /// Creates a SeqAct_ConsoleCommand that executes the command on a player object
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="consoleCommand"></param>
        /// <returns></returns>
        public static ExportEntry CreateConsoleCommandObject(ExportEntry sequence, string consoleCommand, PackageCache cache = null)
        {
            var player = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Player", cache);
            var consoleCommandObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_ConsoleCommand", cache);
            var ap = new ArrayProperty<StrProperty>("Commands");
            ap.Add(consoleCommand);
            consoleCommandObj.WriteProperty(ap);
            KismetHelper.CreateVariableLink(consoleCommandObj, "Target", player);
            KismetHelper.AddObjectsToSequence(sequence, false, player, consoleCommandObj);
            return consoleCommandObj;
        }

        /// <summary>
        /// Creates a new SeqAct_ActivateRemoteEvent with the specified event name
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public static ExportEntry CreateActivateRemoteEvent(ExportEntry sequence, string eventName, PackageCache cache = null)
        {
            var rmEvt = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_ActivateRemoteEvent", cache);
            rmEvt.WriteProperty(new NameProperty(eventName, "EventName"));
            KismetHelper.AddObjectsToSequence(sequence, false, rmEvt);
            return rmEvt;
        }

        /// <summary>
        /// Creates a new SeqEvent_RemoteEvent with the given EventName
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="eventName"></param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSeqEventRemoteActivated(ExportEntry sequence, string eventName, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "SeqEvent_RemoteEvent", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new NameProperty(eventName, "EventName"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqEvent_Death with the given Originator
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="originator">The kismet object that will be linked as the Originator.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSeqEventDeath(ExportEntry sequence, ExportEntry originator, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "SeqEvent_Death", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new ObjectProperty(originator, "Originator"));

            return fObj;
        }

        /// <summary>
        /// Creates a new BioSeqVar_ObjectFindByTag with the given tag name and optionally searching unique tags
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="tagToFind"></param>
        /// <param name="searchUniqueTags"></param>
        /// <returns></returns>
        public static ExportEntry CreateFindObject(ExportEntry sequence, string tagToFind, bool searchUniqueTags = false, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "BioSeqVar_ObjectFindByTag", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new StrProperty(tagToFind, "m_sObjectTagToFind"));
            if (searchUniqueTags)
            {
                fObj.WriteProperty(new BoolProperty(true, "m_bSearchUniqueTag"));
            }
            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_AddFloat with the specified parameters (if any)
        /// </summary>
        /// <param name="sequence">Sequence the created object will be placed into</param>
        /// <param name="A">Kismet object for A. If null, A won't be linked.</param>
        /// <param name="B">Kismet object for B. If null, B won't be linked.</param>
        /// <param name="IntResult">Kismet object for the integer result. If null, it won't be linked.</param>
        /// <param name="FloatResult">Kismet object for the float result. If null, it won't be linked.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateAddFloat(ExportEntry sequence, ExportEntry A = null, ExportEntry B = null, ExportEntry FloatResult = null, ExportEntry IntResult = null, PackageCache cache = null)
        {
            var addInt = CreateSequenceObject(sequence.FileRef, "SeqAct_AddFloat", cache);
            KismetHelper.AddObjectToSequence(addInt, sequence);

            if (A != null)
            {
                KismetHelper.CreateVariableLink(addInt, "A", A);
            }
            if (B != null)
            {
                KismetHelper.CreateVariableLink(addInt, "B", B);
            }
            if (FloatResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "FloatResult", FloatResult);
            }
            if (IntResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "IntResult", IntResult);
            }
            return addInt;
        }

        /// <summary>
        /// Creates a new SeqVar_AddInt with the specified parameters (if any)
        /// </summary>
        /// <param name="sequence">Sequence the created object will be placed into</param>
        /// <param name="A">Kismet object for A. If null, A won't be linked.</param>
        /// <param name="B">Kismet object for B. If null, B won't be linked.</param>
        /// <param name="IntResult">Kismet object for the integer result. If null, it won't be linked.</param>
        /// <param name="FloatResult">Kismet object for the float result. If null, it won't be linked.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateAddInt(ExportEntry sequence, ExportEntry A = null, ExportEntry B = null, ExportEntry IntResult = null, ExportEntry FloatResult = null, PackageCache cache = null)
        {
            var addInt = CreateSequenceObject(sequence.FileRef, "SeqAct_AddInt", cache);
            KismetHelper.AddObjectToSequence(addInt, sequence);

            if (A != null)
            {
                KismetHelper.CreateVariableLink(addInt, "A", A);
            }
            if (B != null)
            {
                KismetHelper.CreateVariableLink(addInt, "B", B);
            }
            if (IntResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "IntResult", IntResult);
            }
            if (FloatResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "FloatResult", FloatResult);
            }

            return addInt;
        }

        /// <summary>
        /// Creates a SeqAct_SetInt with the specified parameters (if any)
        /// </summary>
        /// <param name="sequence">The sequence to place the object into</param>
        /// <param name="target">The SeqVar_Int or subclass that will have its value set. If null, it won't be linked.</param>
        /// <param name="value">The object that defines the value to set. If null, it won't be linked.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetInt(ExportEntry sequence, ExportEntry target = null, ExportEntry value = null, PackageCache cache = null)
        {
            var setInt = CreateSequenceObject(sequence.FileRef, "SeqAct_SetInt", cache);
            KismetHelper.AddObjectToSequence(setInt, sequence);

            if (value != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Value", value);
            }

            if (target != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Target", target);
            }

            return setInt;
        }

        /// <summary>
        /// Creates a SeqAct_SetFloat with the specified parameters (if any)
        /// </summary>
        /// <param name="sequence">The sequence to place the object into</param>
        /// <param name="target">The SeqVar_Float or subclass that will have its value set. If null, it won't be linked.</param>
        /// <param name="value">The object that defines the value to set. If null, it won't be linked.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetFloat(ExportEntry sequence, ExportEntry target = null, ExportEntry value = null, PackageCache cache = null)
        {
            var setInt = CreateSequenceObject(sequence.FileRef, "SeqAct_SetFloat", cache);
            KismetHelper.AddObjectToSequence(setInt, sequence);

            if (value != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Value", value);
            }

            if (target != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Target", target);
            }

            return setInt;
        }

        /// <summary>
        /// Creates a basic Gate object in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static ExportEntry CreateGate(ExportEntry sequence, PackageCache cache = null)
        {
            var gate = CreateSequenceObject(sequence.FileRef, "SeqAct_Gate", cache);
            KismetHelper.AddObjectToSequence(gate, sequence);
            return gate;
        }

        /// <summary>
        /// Creates an object of the specified class and adds it to the listed sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        public static ExportEntry CreateAndAddToSequence(ExportEntry sequence, string className, PackageCache cache = null)
        {
            var obj = CreateSequenceObject(sequence.FileRef, className, cache);
            KismetHelper.AddObjectToSequence(obj, sequence);
            return obj;
        }

        /// <summary>
        /// Creates a SeqAct_Log object
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        public static ExportEntry CreateLog(ExportEntry seq, string comment, PackageCache cache = null)
        {
            // This is often used for hackjobbing things
            var obj = CreateAndAddToSequence(seq, "SeqAct_Log", cache);
            KismetHelper.SetComment(obj, comment);
            return obj;
        }


        /// <summary>
        /// Creates a PMCheckState with the given index to check for
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static ExportEntry CreatePMCheckState(ExportEntry seq, int index, PackageCache cache = null)
        {
            var checkState = CreateSequenceObject(seq.FileRef, "BioSeqAct_PMCheckState", cache);
            KismetHelper.AddObjectToSequence(checkState, seq);

            checkState.WriteProperty(new IntProperty(index, "m_nIndex"));

            return checkState;
        }

        /// <summary>
        /// Creates a ModifyObjectList in the given sequence
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ExportEntry CreateModifyObjectList(ExportEntry seq, PackageCache cache = null)
        {
            var objListModifier = CreateSequenceObject(seq.FileRef, "SeqAct_ModifyObjectList", cache);
            KismetHelper.AddObjectToSequence(objListModifier, seq);
            return objListModifier;
        }

        /// <summary>
        /// Creates a new SeqVar_Named to find the name/class type combo in, in the given sequence
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="varName"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public static ExportEntry CreateSeqVarNamed(ExportEntry seq, string varName, string expectedType, PackageCache cache = null)
        {
            var varNamed = CreateSequenceObject(seq.FileRef, "SeqVar_Named", cache);
            KismetHelper.AddObjectToSequence(varNamed, seq);
            var expectedTypeClass = EntryImporter.EnsureClassIsInFile(seq.FileRef, expectedType, new RelinkerOptionsPackage(cache));
            varNamed.WriteProperty(new NameProperty(varName, "FindVarName"));
            varNamed.WriteProperty(new ObjectProperty(expectedTypeClass, "ExpectedType"));
            return varNamed;
        }

        /// <summary>
        /// Creates a WwisePostEvent action in the given sequence with the given event
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ExportEntry CreateWwisePostEvent(ExportEntry seq, IEntry wwiseEvent, PackageCache cache = null)
        {
            var postEvent = CreateSequenceObject(seq.FileRef, "SeqAct_WwisePostEvent", cache);
            KismetHelper.AddObjectToSequence(postEvent, seq);

            postEvent.WriteProperty(new ObjectProperty(wwiseEvent, "WwiseObject"));

            return postEvent;
        }

        /// <summary>
        /// Creates a SFXSeqCond_GetDifficulty object and returns it - automatically adds player variable link
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ExportEntry CreateCondGetDifficulty(ExportEntry seq, PackageCache cache = null)
        {
            var diff = CreateSequenceObject(seq.FileRef, "SFXSeqCond_GetDifficulty", cache);
            KismetHelper.AddObjectToSequence(diff, seq);
            var player = CreatePlayerObject(seq, true);
            KismetHelper.CreateVariableLink(diff, "Player", player);
            return diff;
        }

        public static ExportEntry CreatePlotInt(ExportEntry seq, int idx, PackageCache cache = null)
        {
            var plotInt = CreateSequenceObject(seq.FileRef, "BioSeqVar_StoryManagerInt", cache);
            KismetHelper.AddObjectToSequence(plotInt, seq);
            plotInt.WriteProperty(new IntProperty(idx, "m_nIndex"));
            // Technically there's other props but I don't think they are used.
            return plotInt;
        }

        /// <summary>
        /// Creates a SeqCond_CompareInt action in the given sequence with the given objects, if any
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="int1"></param>
        /// <param name="int2"></param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateCompareInt(ExportEntry seq, ExportEntry int1 = null, ExportEntry int2 = null, PackageCache cache = null)
        {
            var comp = CreateSequenceObject(seq.FileRef, "SeqCond_CompareInt", cache);
            KismetHelper.AddObjectToSequence(comp, seq);
            if (int1 != null)
            {
                KismetHelper.CreateVariableLink(comp, "A", int1);
            }
            if (int1 != null)
            {
                KismetHelper.CreateVariableLink(comp, "B", int2);
            }
            return comp;
        }

        /// <summary>
        /// Creates a SeqCond_CompareObject action in the given sequence with the given objects, if any
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="seqObjA"></param>
        /// <param name="seqObjB"></param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateCompareObject(ExportEntry seq, ExportEntry seqObjA = null, ExportEntry seqObjB = null, PackageCache cache = null)
        {
            var seqCond = CreateSequenceObject(seq.FileRef, "SeqCond_CompareObject", cache);
            KismetHelper.AddObjectToSequence(seqCond, seq);

            if (seqObjA != null)
            {
                KismetHelper.CreateVariableLink(seqCond, "A", seqObjA);
            }
            if (seqObjB != null)
            {
                KismetHelper.CreateVariableLink(seqCond, "B", seqObjB);
            }

            return seqCond;
        }


        /// <summary>
        /// Adds a SeqAct_Interp in the given sequence. This does not create the InterpData object nor does it create the variable links that the interp object uses.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateInterp(ExportEntry seq, PackageCache cache = null)
        {
            var interp = CreateSequenceObject(seq.FileRef, "SeqAct_Interp", cache);
            KismetHelper.AddObjectToSequence(interp, seq);
            return interp;
        }

        /// <summary>
        /// Adds an InterpData object in the given sequence.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="length">Optional: The length of the interp</param>
        /// <param name="interpGroups">Not really optional: List of interp groups this interp has</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateInterpData(ExportEntry seq, float length = float.NaN, List<ExportEntry> interpGroups = null, PackageCache cache = null)
        {
            var interpData = CreateSequenceObject(seq.FileRef, "InterpData", cache);
            KismetHelper.AddObjectToSequence(interpData, seq);

            if (!float.IsNaN(length))
            {
                interpData.WriteProperty(new FloatProperty(length, "InterpLength"));
            }

            if (interpGroups != null)
            {
                interpData.WriteProperty(new ArrayProperty<ObjectProperty>(interpGroups.Select(x => new ObjectProperty(x)), "InterpGroups"));
            }

            return interpData;
        }

        #endregion
        /// <summary>
        /// Adds a SeqAct_SetObject object in the given sequence, optionally linking the extra parameters if set.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="target">Optional: The object to link to the target terminal</param>
        /// <param name="objValue">Optional: The object to link to the Value terminal</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetObject(ExportEntry seq, ExportEntry target = null, ExportEntry objValue = null, PackageCache cache = null)
        {
            var setObj = CreateSequenceObject(seq.FileRef, "SeqAct_SetObject", cache);
            KismetHelper.AddObjectToSequence(setObj, seq);

            if (target != null)
            {
                KismetHelper.CreateVariableLink(setObj, "Target", target);
            }

            if (objValue != null)
            {
                KismetHelper.CreateVariableLink(setObj, "Value", objValue);
            }

            return setObj;
        }


        /// <summary>
        /// Creates a SeqEvent_LevelLoaded object in the given sequence
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateLevelLoaded(ExportEntry seq, PackageCache cache = null)
        {
            var player = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "SeqEvent_LevelLoaded", cache);
            KismetHelper.AddObjectToSequence(player, seq);
            return player;
        }

        /// <summary>
        /// Creates a new SeqVar_string with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="value">The value to set the string of the object to. If null, it will not write the StrValue property.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateString(ExportEntry sequence, string value, PackageCache cache = null)
        {
            var strObj = CreateSequenceObject(sequence.FileRef, "SeqVar_String", cache);
            KismetHelper.AddObjectToSequence(strObj, sequence);

            if (value != null) // We allow empty values
            {
                strObj.WriteProperty(new StrProperty(value, "StrValue"));
            }

            return strObj;
        }

        /// <summary>
        /// LEX ONLY: Creates a SeqAct_SendMessageToLEX, which when combined with the InteropASI, can signal LEX. Requires the class be compiled already or available in the local package.
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="message">Optional: Message to attach to the object.</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSendMessageToLEX(ExportEntry sequence, string message = null, PackageCache cache = null)
        {
            var sendMessage = CreateSequenceObject(sequence, "SeqAct_SendMessageToLEX", cache);
            if (message != null)
            {
                var sendLoadedString = CreateString(sequence, message, cache);
                KismetHelper.CreateVariableLink(sendMessage, "MessageName", sendLoadedString);
            }

            return sendMessage;
        }

        /// <summary>
        /// Creates a SeqEvent_Console object with the given console event name.
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="eventName">Name of console event that triggers this</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateConsoleEvent(ExportEntry sequence, string eventName, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "SeqEvent_Console", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new NameProperty(eventName, "ConsoleEventName"));

            return fObj;
        }

        /// <summary>
        /// Creates a SeqAct_ToggleHUD, with an optional object to link as the target.
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="target">Optional: Target to connect to the Target terminal</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateToggleHUD(ExportEntry sequence, ExportEntry target = null, PackageCache cache = null)
        {
            var fObj = CreateSequenceObject(sequence.FileRef, "SeqAct_ToggleHUD", cache);
            if (target != null)
            {
                KismetHelper.CreateVariableLink(fObj, "Target", target);
            }
            KismetHelper.AddObjectToSequence(fObj, sequence);
            return fObj;
        }

        /// <summary>
        /// Creates a BioSeqAct_ToggleSave, with an optional object to link as Enable/Disable variable.
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="enableSaveBool">Optional: Enable/Disable boolean variable</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateToggleSave(ExportEntry sequence, ExportEntry enableSaveBool = null, PackageCache cache = null)
        {
            // LE1
            var fObj = CreateSequenceObject(sequence.FileRef, "BioSeqAct_ToggleSave", cache);
            if (enableSaveBool != null)
            {
                KismetHelper.CreateVariableLink(fObj, "Enable", enableSaveBool);
            }
            KismetHelper.AddObjectToSequence(fObj, sequence);
            return fObj;

        }

        /// <summary>
        /// Creates a new SeqVar_Bool with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="value">The value to set the bool to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>    
        public static ExportEntry CreateBool(ExportEntry sequence, bool value, PackageCache cache = null)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Bool", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new IntProperty(value ? 1 : 0, "bValue"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_Vector with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="x">The x value to set</param>
        /// <param name="y">The y value to set</param>
        /// <param name="z">The z value to set</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>    
        public static ExportEntry CreateVector(ExportEntry sequence, float x, float y, float z, PackageCache cache = null)
        {
            var vObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Vector", cache);
            KismetHelper.AddObjectToSequence(vObj, sequence);
            vObj.WriteProperty(CommonStructs.Vector3Prop(x, y, z, "VectValue"));
            return vObj;
        }

        /// <summary>
        /// Creates a new SeqVar_ScopedNamed with the given value in the given sequence
        /// </summary>
        /// <param name="sequence">Sequence this object will be placed into</param>
        /// <param name="expectedTypeClassName">The expected variable type class name</param>
        /// <param name="varName">The name of the variable to find</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>   
        public static ExportEntry CreateScopeNamed(ExportEntry sequence, string expectedTypeClassName, NameReference varName, PackageCache cache = null)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_ScopedNamed", cache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            var expectedType = EntryImporter.EnsureClassIsInFile(sequence.FileRef, expectedTypeClassName, new RelinkerOptionsPackage(cache));

            fObj.WriteProperty(new ObjectProperty(expectedType, "ExpectedType"));
            fObj.WriteProperty(new NameProperty(varName, "FindVarName"));
            fObj.WriteProperty(new BoolProperty(true, "bStatusIsOK")); // Not entirely sure what this is for

            return fObj;
        }

        /// <summary>
        /// Creates a SeqCond_CompareFloat action in the given sequence with the given objects, if any
        /// </summary>
        /// <param name="seq">Sequence to place this object into</param>
        /// <param name="float1">Optional float object to assign to the A variable pin</param>
        /// <param name="float2">Optional float object to assign to the B variable pin</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateCompareFloat(ExportEntry seq, ExportEntry float1 = null, ExportEntry float2 = null, PackageCache cache = null)
        {
            var comp = CreateSequenceObject(seq.FileRef, "SeqCond_CompareFloat", cache);
            KismetHelper.AddObjectToSequence(comp, seq);
            if (float1 != null)
            {
                KismetHelper.CreateVariableLink(comp, "A", float1);
            }
            if (float1 != null)
            {
                KismetHelper.CreateVariableLink(comp, "B", float2);
            }
            return comp;
        }

        /// <summary>
        /// Creates a SeqAct_SetLocation action in the given sequence with the given objects, if any
        /// </summary>
        /// <param name="seq">Sequence to place this object into</param>
        /// <param name="target">Optional object to assign to the Target variable pin</param>
        /// <param name="location">Optional vector object to assign to the Location variable pin</param>
        /// <param name="rotation">Optional vector object to assign to the Rotation variable pin</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetLocation(ExportEntry seq, ExportEntry target = null, ExportEntry location = null, ExportEntry rotation = null, PackageCache cache = null)
        {
            var comp = CreateSequenceObject(seq.FileRef, "SeqAct_SetLocation", cache);
            KismetHelper.AddObjectToSequence(comp, seq);
            if (target != null)
            {
                KismetHelper.CreateVariableLink(comp, "Target", target);
            }
            if (location != null)
            {
                comp.WriteProperty(new BoolProperty(true, "bSetLocation"));
                KismetHelper.CreateVariableLink(comp, "Location", target);
            }
            if (rotation != null)
            {
                comp.WriteProperty(new BoolProperty(true, "bSetRotation"));
                KismetHelper.CreateVariableLink(comp, "Rotation", target);
            }
            return comp;
        }

        /// <summary>
        /// Creates a SeqVar_Name action in the given sequence with the given value
        /// </summary>
        /// <param name="sequence">Sequence to place this object into</param>
        /// <param name="name">Name to set this variable value to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateName(ExportEntry sequence, NameReference name, PackageCache cache = null)
        {
            var vObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Name", cache);
            KismetHelper.AddObjectToSequence(vObj, sequence);
            vObj.WriteProperty(new NameProperty(name, "NameValue"));
            return vObj;
        }


        /// <summary>
        /// Creates a new blank Sequence.
        /// </summary>
        /// <param name="parentSequence">Sequence to place this object into as a child</param>
        /// <param name="sequenceName">Name to give this sequence. Also sets the object name</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSequence(ExportEntry parentSequence, string sequenceName, PackageCache cache = null)
        {
            var sequence = SequenceObjectCreator.CreateSequenceObject(parentSequence, "Sequence", cache);
            sequence.ObjectName = parentSequence.FileRef.GetNextIndexedName(sequenceName);
            // We write this to make it a bit easier on the eyes in editor
            sequence.WriteProperty(new StrProperty(sequenceName, "ObjName"));
            return sequence;
        }

        /// <summary>
        /// Creates a BioSeqEvt_OnPlayerActivate action in the given sequence, with the given linked objects, if specified
        /// </summary>
        /// <param name="sequence">Sequence to place this object into as a child</param>
        /// <param name="attachee">Optional: Actor object to attach to the item on the event pin</param>
        /// <param name="seqEvent">Optional: The event to attach the attachee to</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateAttachToEvent(ExportEntry sequence, ExportEntry attachee = null, ExportEntry seqEvent = null, PackageCache cache = null)
        {
            var attachToEvent = CreateSequenceObject(sequence.FileRef, "SeqAct_AttachToEvent", cache);
            KismetHelper.AddObjectToSequence(attachToEvent, sequence);
            if (attachee != null)
            {
                KismetHelper.CreateVariableLink(attachToEvent, "Attachee", attachee);
            }
            if (seqEvent != null)
            {
                KismetHelper.CreateEventLink(attachToEvent, "Event", seqEvent);
            }
            return attachToEvent;
        }

        /// <summary>
        /// Creates a BioSeqAct_SetActive action in the given sequence, with the given linked objects, if specified
        /// </summary>
        /// <param name="sequence">Sequence to place this object into as a child</param>
        /// <param name="pawn">Optional: Actor object to attach to the item on the target pin</param>
        /// <param name="activeBool">Optional: The bool value to attach to the Active pin</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetActive(ExportEntry sequence, ExportEntry pawn = null, ExportEntry activeBool = null, PackageCache cache = null)
        {
            var setActive = CreateSequenceObject(sequence.FileRef, "BioSeqAct_SetActive", cache);
            KismetHelper.AddObjectToSequence(setActive, sequence);
            if (pawn != null)
            {
                KismetHelper.CreateVariableLink(setActive, "Target", pawn);
            }
            if (activeBool != null)
            {
                KismetHelper.CreateVariableLink(setActive, "Active", activeBool);
            }
            return setActive;
        }


        /// <summary>
        /// Adds a SeqAct_SetBool object in the given sequence, optionally linking the extra parameters if set.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="target">Optional: The bool object to link to the target terminal</param>
        /// <param name="objValue">Optional: The bool object to link to the Value terminal</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateSetBool(ExportEntry seq, ExportEntry target = null, ExportEntry objValue = null, PackageCache cache = null)
        {
            var setObj = CreateSequenceObject(seq.FileRef, "SeqAct_SetBool", cache);
            KismetHelper.AddObjectToSequence(setObj, seq);

            if (target != null)
            {
                KismetHelper.CreateVariableLink(setObj, "Target", target);
            }

            if (objValue != null)
            {
                KismetHelper.CreateVariableLink(setObj, "Value", objValue);
            }

            return setObj;
        }

        /// <summary>
        /// Adds a BioSeqAct_ChangeAI object in the given sequence, optionally linking the extra parameters if set. This class only works properly in LE1.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="controllerClass">Optional: The controller class property to set on the object</param>
        /// <param name="pawn">Optional: The pawn actor object to link to the Pawn terminal</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateChangeAI(ExportEntry seq, IEntry controllerClass = null, ExportEntry pawn = null, PackageCache cache = null)
        {
            // Validated for LE1
            var setObj = CreateSequenceObject(seq.FileRef, "BioSeqAct_ChangeAI", cache);
            KismetHelper.AddObjectToSequence(setObj, seq);

            if (controllerClass != null)
            {
                setObj.WriteProperty(new ObjectProperty(controllerClass, "ControllerClass"));
            }

            if (pawn != null)
            {
                KismetHelper.CreateVariableLink(setObj, "Pawn", pawn);
            }

            return setObj;
        }

        /// <summary>
        /// Adds a SeqCond_CompareBool object in the given sequence, optionally linking the extra parameters if set.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="boolObj">Optional: The bool object to link to the Bool terminal</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateCompareBool(ExportEntry seq, ExportEntry boolObj = null, PackageCache cache = null)
        {
            var comp = CreateSequenceObject(seq.FileRef, "SeqCond_CompareBool", cache);
            KismetHelper.AddObjectToSequence(comp, seq);
            if (boolObj != null)
            {
                KismetHelper.CreateVariableLink(comp, "Bool", boolObj);
            }
            return comp;
        }

        /// <summary>
        /// Adds a SeqAct_GetDistance object in the given sequence, optionally linking the extra parameters if set.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="objA">Optional: The object to connect to the A pin</param>
        /// <param name="objB">Optional: The object to connect to the B pin</param>
        /// <param name="fDistance">Optional: The output float object to connect to the Distance pin</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateGetDistance(ExportEntry seq, ExportEntry objA = null, ExportEntry objB = null, ExportEntry fDistance = null, PackageCache cache = null)
        {
            var getDistance = CreateSequenceObject(seq.FileRef, "SeqAct_GetDistance", cache);
            KismetHelper.AddObjectToSequence(getDistance, seq);
            if (objA != null)
            {
                KismetHelper.CreateVariableLink(getDistance, "A", objA);
            }
            if (objB != null)
            {
                KismetHelper.CreateVariableLink(getDistance, "B", objB);
            }
            if (fDistance != null)
            {
                KismetHelper.CreateVariableLink(getDistance, "Distance", fDistance);
            }
            return getDistance;
        }

        /// <summary>
        /// Adds a BioSeqAct_CauseDamage object in the given sequence, optionally linking the extra parameters if set.
        /// </summary>
        /// <param name="seq">Sequence to add the new object to</param>
        /// <param name="target">Optional: The object to connect to the Target pin</param>
        /// <param name="instigator">Optional: The object to connect to the Instigator pin</param>
        /// <param name="cache">Cache to use when creating the object. If you are doing many object creations, this will greatly improve performance.</param>
        /// <returns>The created kismet object</returns>
        public static ExportEntry CreateCauseDamage(ExportEntry seq, ExportEntry target = null, ExportEntry instigator = null, float? damagePercent = null, PackageCache cache = null)
        {
            // Likely only works for LE1
            var causeDamage = CreateSequenceObject(seq.FileRef, "BioSeqAct_CauseDamage", cache);
            KismetHelper.AddObjectToSequence(causeDamage, seq);
            if (target != null)
            {
                KismetHelper.CreateVariableLink(causeDamage, "Target", target);
            }
            if (instigator != null)
            {
                KismetHelper.CreateVariableLink(causeDamage, "Instigator", instigator);
            }
            if (damagePercent != null)
            {
                causeDamage.WriteProperty(new FloatProperty(damagePercent.Value, "m_fDamageAmountAsPercentOfMaxHealth"));
            }
            return causeDamage;
        }
    }
}
