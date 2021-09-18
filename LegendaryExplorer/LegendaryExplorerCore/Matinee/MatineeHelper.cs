using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;

namespace LegendaryExplorerCore.Matinee
{
    public static class MatineeHelper
    {
        public static ExportEntry AddNewGroupToInterpData(ExportEntry interpData, string groupName) => InternalAddGroup("InterpGroup", interpData, groupName);

        public static ExportEntry AddNewGroupDirectorToInterpData(ExportEntry interpData) => InternalAddGroup("InterpGroupDirector", interpData, null);

        public static ExportEntry AddPresetDirectorGroup(ExportEntry interpData) => InternalAddPresetGroup("Director", interpData);

        public static ExportEntry AddPresetCameraGroup(ExportEntry interpData, string camName) => InternalAddPresetGroup("Camera", interpData, camName);

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

        private static ExportEntry InternalAddPresetGroup(string preset, ExportEntry interpData, string? camName = null)
        {
            var group = PresetCreateNewExport(preset, interpData, camName);
            PresetAddTracks(preset, group);

            return group;
        }

        private static ExportEntry PresetCreateNewExport(string preset, ExportEntry interpData, string? camName = null)
        {
            string className = "InterpGroup";
            var properties = new PropertyCollection { new ArrayProperty<ObjectProperty>("InterpTracks") };

            switch (preset)
            {
                case "Camera":
                    if (!string.IsNullOrEmpty(camName))
                    {
                        properties.Add(new NameProperty(camName, "m_nmSFXFindActor"));
                        properties.Add(new NameProperty(camName, "GroupName"));
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

        private static void PresetAddTracks(string preset, ExportEntry group)
        {
            switch (preset)
            {
                case "Camera":
                    var move = AddNewTrackToGroup(group, "InterpTrackMove");
                    AddDefaultPropertiesToTrack(move);

                    var fov = AddNewTrackToGroup(group, "InterpTrackFloatProp");
                    fov.WriteProperty(new InterpCurveFloat().ToStructProperty(fov.Game, "FloatTrack"));
                    fov.WriteProperty(new StrProperty("FOVAngle", "TrackTitle"));
                    fov.WriteProperty(new NameProperty("FOVAngle", "PropertyName"));
                    break;

                case "Director":
                    var dir = AddNewTrackToGroup(group, "InterpTrackDirector");
                    AddDefaultPropertiesToTrack(dir);

                    var dof = AddNewTrackToGroup(group, "BioEvtSysTrackDOF");
                    AddDefaultPropertiesToTrack(dof);
                    break;
            }
            return;
        }

        private static ExportEntry CreateNewExport(string className, ExportEntry parent, PropertyCollection properties)
        {
            IMEPackage pcc = parent.FileRef;
            var group = new ExportEntry(pcc, parent, pcc.GetNextIndexedName(className), properties: properties)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className)
            };
            group.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(group);
            return group;
        }

        public static List<ClassInfo> GetInterpTracks(MEGame game) => GlobalUnrealObjectInfo.GetNonAbstractDerivedClassesOf("InterpTrack", game);

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
    }
}
