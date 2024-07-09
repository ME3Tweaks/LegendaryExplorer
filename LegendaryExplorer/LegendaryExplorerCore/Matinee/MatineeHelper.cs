using System.Collections.Generic;
using System.Drawing;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;

namespace LegendaryExplorerCore.Matinee
{
    /// <summary>
    /// Static methods to perform common Matinee editing operations
    /// </summary>
    public static class MatineeHelper
    {
        /// <summary>
        /// Adds a new InterpGroup to the given InterpData
        /// </summary>
        /// <param name="interpData">InterpData export to add group to</param>
        /// <param name="groupName">Name of new group</param>
        /// <returns>The created InterpGroup</returns>
        public static ExportEntry AddNewGroupToInterpData(ExportEntry interpData, string groupName) => InternalAddGroup("InterpGroup", interpData, groupName);

        /// <summary>
        /// Adds a new InterpGroupDirector to the given InterpData
        /// </summary>
        /// <param name="interpData">InterpData export to add director to</param>
        /// <returns>The created InterpGroupDirector</returns>
        public static ExportEntry AddNewGroupDirectorToInterpData(ExportEntry interpData) => InternalAddGroup("InterpGroupDirector", interpData, null);

        /// <summary>
        /// Adds a preset interp export to the given InterpData or InterpGroup
        /// </summary>
        /// <param name="preset">Type of preset. Options: Camera, Actor, Director, Gesture, Gesture2</param>
        /// <param name="export">Parent export of desired preset, either an InterpData or InterpGroup export</param>
        /// <param name="game">Game you are working on</param>
        /// <param name="param1">Typically actor tag name or gesture name</param>
        /// <returns>Created preset export</returns>
        public static ExportEntry AddPreset(string preset, ExportEntry export, MEGame game, string param1 = null) => InternalAddPreset(preset, export, game, param1);

        private static ExportEntry InternalAddGroup(string className, ExportEntry interpData, string groupName)
        {
            var properties = new PropertyCollection{new ArrayProperty<ObjectProperty>("InterpTracks")};
            if (!string.IsNullOrEmpty(groupName))
            {
                properties.Add(new NameProperty(groupName, "GroupName"));
            }
            properties.Add(CommonStructs.ColorProp(className == "InterpGroup" ? Color.Green : Color.Purple, "GroupColor"));
            ExportEntry group = CreateNewExport(className, interpData, properties);

            var props = interpData.GetProperties();
            var groupsProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpGroups") ?? new ArrayProperty<ObjectProperty>("InterpGroups");
            groupsProp.Add(new ObjectProperty(group));
            props.AddOrReplaceProp(groupsProp);
            interpData.WriteProperties(props);

            return group;
        }

        private static ExportEntry InternalAddPreset(string preset, ExportEntry export, MEGame game, string param1)
        {
            switch (export.ClassName)
            {
                case "InterpData":
                    var group = PresetCreateNewExport(preset, export, game, param1);
                    PresetAddTracks(preset, group, game, param1);
                    return group;
                case "InterpGroup":
                    PresetAddTracks(preset, export, game, param1);
                    return export;
                default:
                    return null;
            }
        }

        private static ExportEntry CreateNewExport(string className, ExportEntry parent, PropertyCollection properties)
        {
            IMEPackage pcc = parent.FileRef;
            var group = new ExportEntry(pcc, parent, pcc.GetNextIndexedName(className), properties: properties)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, new RelinkerOptionsPackage() {ImportExportDependencies = true})
            };
            group.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(group);
            return group;
        }

        /// <summary>
        /// Gets all InterpTrack subclasses for a game
        /// </summary>
        /// <param name="game">Game to get track classes for</param>
        /// <returns>InterpTrack and InterpTrack subclasses</returns>
        public static List<ClassInfo> GetInterpTracks(MEGame game) => GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf("InterpTrack", game);

        /// <summary>
        /// Adds a new InterpTrack of the given class to an InterpGroup
        /// </summary>
        /// <example><c>AddNewTrackToGroup(myInterpGroup, "InterpTrackMove")</c></example>
        /// <param name="interpGroup">InterpGroup to add track to</param>
        /// <param name="trackClass">Class name of InterpTrack to add</param>
        /// <returns>The created track</returns>
        public static ExportEntry AddNewTrackToGroup(ExportEntry interpGroup, string trackClass)
        {
            //should add the property that contains track keys at least
            ExportEntry track = CreateNewExport(trackClass, interpGroup, null);

            var props = interpGroup.GetProperties();
            var tracksProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpTracks") ?? new ArrayProperty<ObjectProperty>("InterpTracks");
            tracksProp.Add(new ObjectProperty(track));
            props.AddOrReplaceProp(tracksProp);
            interpGroup.WriteProperties(props);

            return track;
        }

        /// <summary>
        /// Adds some pre-determined default properties to a given InterpTrack, based on class name
        /// </summary>
        /// <param name="trackExport">InterpTrack to add properties to</param>
        public static void AddDefaultPropertiesToTrack(ExportEntry trackExport)
        {
            if (trackExport.IsA("BioInterpTrack"))
            {
                if (trackExport.IsA("SFXInterpTrackToggleBase"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aToggleKeyData"));
                }
                else if (trackExport.IsA("BioConvNodeTrackDebug"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StrProperty>("m_aDbgStrings"));
                }
                else if (trackExport.IsA("BioEvtSysTrackDOF"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aDOFData"));
                }
                else if (trackExport.IsA("BioEvtSysTrackGesture"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aGestures"));
                }
                else if (trackExport.IsA("BioEvtSysTrackInterrupt"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aInterruptData"));
                }
                else if (trackExport.IsA("BioEvtSysTrackLighting"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aLightingKeys"));
                }
                else if (trackExport.IsA("BioEvtSysTrackLookAt"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aLookAtKeys"));
                }
                else if (trackExport.IsA("BioEvtSysTrackProp"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aPropKeys"));
                }
                else if (trackExport.IsA("BioEvtSysTrackSetFacing"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aFacingKeys"));
                }
                else if (trackExport.IsA("BioEvtSysTrackSubtitles"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aSubtitleData"));
                }
                else if (trackExport.IsA("BioEvtSysTrackSwitchCamera"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aCameras"));
                }
                else if (trackExport.IsA("BioInterpTrackRotationMode"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("EventTrack"));
                }
                else if (trackExport.IsA("SFXGameInterpTrackProcFoley"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aProcFoleyStartStopKeys"));
                    trackExport.WriteProperty(new ObjectProperty(0, "m_TrackFoleySound"));
                }
                else if (trackExport.IsA("SFXGameInterpTrackWwiseMicLock"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aMicLockKeys"));
                }
                else if (trackExport.IsA("SFXInterpTrackAttachCrustEffect"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aCrustEffectKeyData"));
                    trackExport.WriteProperty(new ObjectProperty(0, "oEffect"));
                }
                else if (trackExport.IsA("SFXInterpTrackBlackScreen"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aBlackScreenKeyData"));
                }
                else if (trackExport.IsA("SFXInterpTrackLightEnvQuality"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aLightEnvKeyData"));
                }
                else if (trackExport.IsA("SFXInterpTrackMovieBase"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aMovieKeyData"));
                }
                else if (trackExport.IsA("SFXInterpTrackSetPlayerNearClipPlane"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aNearClipKeyData"));
                }
                else if (trackExport.IsA("SFXInterpTrackSetWeaponInstant"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aWeaponClassKeyData"));
                }
                else if (trackExport.IsA("SFXInterpTrackPlayFaceOnlyVO"))
                {
                    trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aFOVOKeys"));
                }

                trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aTrackKeys"));
            }
            else if (trackExport.ClassName == "InterpTrackSound")
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("Sounds"));
                trackExport.WriteProperty(new InterpCurveVector().ToStructProperty(trackExport.Game, "VectorTrack"));
            }
            else if (trackExport.IsA("InterpTrackEvent"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("EventTrack"));
            }
            else if (trackExport.IsA("InterpTrackFaceFX"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("FaceFXSeqs"));
            }
            else if (trackExport.IsA("InterpTrackAnimControl"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("AnimSeqs"));
            }
            else if (trackExport.IsA("InterpTrackMove"))
            {
                trackExport.WriteProperty(new InterpCurveVector().ToStructProperty(trackExport.Game, "PosTrack"));
                trackExport.WriteProperty(new InterpCurveVector().ToStructProperty(trackExport.Game, "EulerTrack"));
                trackExport.WriteProperty(new StructProperty("InterpLookupTrack", new PropertyCollection
                {
                    new ArrayProperty<StructProperty>("Points")
                }, "LookupTrack"));
            }
            else if (trackExport.IsA("InterpTrackVisibility"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("VisibilityTrack"));
            }
            else if (trackExport.IsA("InterpTrackToggle"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("ToggleTrack"));
            }
            else if (trackExport.IsA("InterpTrackWwiseEvent"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("WwiseEvents"));
            }
            else if (trackExport.IsA("InterpTrackDirector"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("CutTrack"));
            }
            else if (trackExport.IsA("BioEvtSysTrackSubtitles"))
            {
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aSubtitleData"));
                trackExport.WriteProperty(new ArrayProperty<StructProperty>("m_aTrackKeys"));
            }
            else if (trackExport.IsA("InterpTrackFloatBase"))
            {
                trackExport.WriteProperty(new InterpCurveFloat().ToStructProperty(trackExport.Game, "FloatTrack"));
            }
            else if (trackExport.IsA("InterpTrackVectorBase"))
            {
                trackExport.WriteProperty(new InterpCurveVector().ToStructProperty(trackExport.Game, "VectorTrack"));
            }
        }

        private static ExportEntry PresetCreateNewExport(string preset, ExportEntry interpData, MEGame game, string param1)
        {
            string className = "InterpGroup";
            var properties = new PropertyCollection { new ArrayProperty<ObjectProperty>("InterpTracks") };

            switch (preset)
            {
                case "Camera":
                case "Actor":
                    if (!string.IsNullOrEmpty(param1))
                    {
                        if (game.IsGame3())
                        {
                            properties.Add(new NameProperty(param1, "m_nmSFXFindActor"));
                        }
                        properties.Add(new NameProperty(param1, "GroupName"));
                    }
                    properties.Add(CommonStructs.ColorProp(Color.Green, "GroupColor"));
                    break;

                case "Director":
                    className = "InterpGroupDirector";
                    properties.Add(CommonStructs.ColorProp(Color.Purple, "GroupColor"));
                    break;

                default:
                    properties.Add(CommonStructs.ColorProp(Color.Green, "GroupColor"));
                    break;
            }

            ExportEntry group = CreateNewExport(className, interpData, properties);

            var props = interpData.GetProperties();
            var groupsProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpGroups") ?? new ArrayProperty<ObjectProperty>("InterpGroups");
            groupsProp.Add(new ObjectProperty(group));
            props.AddOrReplaceProp(groupsProp);
            interpData.WriteProperties(props);

            return group;
        }

        private static void PresetAddTracks(string preset, ExportEntry interpGroup, MEGame game, string param1 = null)
        {
            switch (preset)
            {
                case "Camera":
                    var move = AddNewTrackToGroup(interpGroup, "InterpTrackMove");
                    AddDefaultPropertiesToTrack(move);

                    var fov = AddNewTrackToGroup(interpGroup, "InterpTrackFloatProp");
                    fov.WriteProperty(new InterpCurveFloat().ToStructProperty(fov.Game, "FloatTrack"));
                    fov.WriteProperty(new StrProperty("FOVAngle", "TrackTitle"));
                    fov.WriteProperty(new NameProperty("FOVAngle", "PropertyName"));
                    break;

                case "Director":
                    var dir = AddNewTrackToGroup(interpGroup, "InterpTrackDirector");
                    AddDefaultPropertiesToTrack(dir);

                    var dof = AddNewTrackToGroup(interpGroup, "BioEvtSysTrackDOF");
                    AddDefaultPropertiesToTrack(dof);
                    break;

                case "Gesture":
                    var ges = AddNewTrackToGroup(interpGroup, "BioEvtSysTrackGesture");
                    ges.WriteProperty(new ArrayProperty<StructProperty>("m_aGestures"));
                    ges.WriteProperty(new ArrayProperty<StructProperty>("m_aTrackKeys"));
                    ges.WriteProperty(new NameProperty("None", "nmStartingPoseSet"));
                    ges.WriteProperty(new NameProperty("None", "nmStartingPoseAnim"));
                    ges.WriteProperty(new FloatProperty(0, "m_fStartPoseOffset"));
                    ges.WriteProperty(new EnumProperty("None", "EBioTrackAllPoseGroups", game, "ePoseFilter"));
                    ges.WriteProperty(new EnumProperty("None", "EBioGestureAllPoses", game, "eStartingPose"));
                    ges.WriteProperty(new BoolProperty(true, "m_bUseDynamicAnimSets"));
                    ges.WriteProperty(new NameProperty(param1, "m_nmFindActor"));
                    ges.WriteProperty(new StrProperty($"Gesture -- {param1}", "TrackTitle"));
                    break;

                case "Gesture2":
                    var ges2 = AddNewTrackToGroup(interpGroup, "BioEvtSysTrackGesture");
                    var m_aGestures = new ArrayProperty<StructProperty>("m_aGestures");
                    var gesProps = PresetCreateProperties("Gesture2-gesture", game);
                    if (gesProps != null)
                    {
                        m_aGestures.Add(new StructProperty("BioGestureData", gesProps, "BioGestureData"));
                        ges2.WriteProperty(m_aGestures);
                    }
                    ges2.WriteProperty(new NameProperty("None", "nmStartingPoseSet"));
                    ges2.WriteProperty(new NameProperty("None", "nmStartingPoseAnim"));
                    ges2.WriteProperty(new FloatProperty(0, "m_fStartPoseOffset"));
                    ges2.WriteProperty(new BoolProperty(true, "m_bUseDynamicAnimSets"));
                    ges2.WriteProperty(new EnumProperty("None", "EBioTrackAllPoseGroups", game, "ePoseFilter"));
                    ges2.WriteProperty(new NameProperty(param1, "m_nmFindActor"));
                    var m_aTrackKeys = new ArrayProperty<StructProperty>("m_aTrackKeys");
                    var keyProps = PresetCreateProperties("Gesture2-key", game);
                    if (keyProps != null)
                    {
                        m_aTrackKeys.Add(new StructProperty("BioTrackKey", keyProps, "BioTrackKey"));
                        ges2.WriteProperty(m_aTrackKeys);
                    }
                    ges2.WriteProperty(new StrProperty($"Gesture -- {param1}", "TrackTitle"));
                    break;

                case "Actor":
                    var actMove = AddNewTrackToGroup(interpGroup, "InterpTrackMove");
                    AddDefaultPropertiesToTrack(actMove);
                    PresetAddTracks("Gesture", interpGroup, game, param1);
                    break;

                default:
                    return;
            }
        }

        private static PropertyCollection PresetCreateProperties(string preset, MEGame game)
        {
            PropertyCollection props = null;
            switch (preset)
            {
                case "Gesture2-gesture":
                    props = new PropertyCollection();
                    props.AddOrReplaceProp(new ArrayProperty<IntProperty>("aChainedGestures"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmPoseSet"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmPoseAnim"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmGestureSet"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmGestureAnim"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmTransitionSet"));
                    props.AddOrReplaceProp(new NameProperty("None", "nmTransitionAnim"));
                    props.AddOrReplaceProp(new FloatProperty(1, "fPlayRate"));
                    props.AddOrReplaceProp(new FloatProperty(0, "fStartOffset"));
                    props.AddOrReplaceProp(new FloatProperty(0, "fEndOffset"));
                    props.AddOrReplaceProp(new FloatProperty(0.1F, "fStartBlendDuration"));
                    props.AddOrReplaceProp(new FloatProperty(0.1F, "fEndBlendDuration"));
                    props.AddOrReplaceProp(new FloatProperty(1, "fWeight"));
                    props.AddOrReplaceProp(new FloatProperty(0, "fTransBlendTime"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bInvalidData"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bOneShotAnim"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bChainToPrevious"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bPlayUntilNext"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bTerminateAllGestures"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bUseDynAnimSets"));
                    props.AddOrReplaceProp(new BoolProperty(false, "bSnapToPose"));
                    props.AddOrReplaceProp(new EnumProperty("None", "EBioValidPoseGroups", game, "ePoseFilter"));
                    props.AddOrReplaceProp(new EnumProperty("None", "EBioGestureValidPoses", game, "ePose"));
                    props.AddOrReplaceProp(new EnumProperty("None", "EBioGestureGroups", game, "eGestureFiler"));
                    props.AddOrReplaceProp(new EnumProperty("None", "EBioGestureValidGestures", game, "eGesture"));

                    break;
                case "Gesture2-key":
                    props = new PropertyCollection();
                    props.AddOrReplaceProp(new NameProperty("None", "KeyName"));
                    props.AddOrReplaceProp(new FloatProperty(0, "fTime"));

                    break;
            }
            return props;
        }

        /// <summary>
        /// Try get an InterpGroup in an InterpData by groupName.
        /// </summary>
        /// <param name="interpData">InterpData to search on.</param>
        /// <param name="groupName">Group name to find.</param>
        /// <param name="interpGroup">Target InterpGroup.</param>
        /// <returns>Whether the InterpGroup was found or not.</returns>
        public static bool TryGetInterpGroup(ExportEntry interpData, string groupName, out ExportEntry interpGroup)
        {
            interpGroup = null;
            IMEPackage pcc = interpData.FileRef;

            ArrayProperty<ObjectProperty> interpGroups = interpData.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
            if (interpGroups == null) { return false; }

            foreach (ObjectProperty groupRef in interpGroups)
            {
                if (!pcc.TryGetUExport(groupRef.Value, out ExportEntry group)) { continue; }

                // Skip non Conversation groups
                NameProperty GroupName = group.GetProperty<NameProperty>("GroupName");
                if (GroupName != null && GroupName.Value == groupName)
                {
                    interpGroup = group;
                    break;
                }
            }

            return interpGroup != null;
        }

        /// <summary>
        /// Try get an InterpTrack in an InterpGroup by trackClass.
        /// </summary>
        /// <param name="interpGroup">InterpGroup to search on.</param>
        /// <param name="trackClass">Class of the track to find.</param>
        /// <param name="interpTrack">Target InterpTrack.</param>
        /// <returns>Whether the InterpTrack was found or not.</returns>
        public static bool TryGetInterpTrack(ExportEntry interpGroup, string trackClass, out ExportEntry interpTrack)
        {
            interpTrack = null;
            IMEPackage pcc = interpGroup.FileRef;

            ArrayProperty<ObjectProperty> interpTracks = interpGroup.GetProperty<ArrayProperty<ObjectProperty>>("InterpTracks");
            if (interpTracks == null) { return false; }

            foreach (ObjectProperty trackRef in interpTracks)
            {
                if (!pcc.TryGetUExport(trackRef.Value, out ExportEntry track)) { continue; }

                // Find the VO track
                if (track.ClassName == trackClass)
                {
                    interpTrack = track;
                    break;
                }
            }

            return interpTrack != null;

        }
    }
}
