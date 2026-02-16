using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Color = System.Windows.Media.Color;

namespace LegendaryExplorer.Tools.InterpEditor
{
    public sealed partial class InterpData
    {
        public ExportEntry Export;

        public ObservableCollectionExtended<InterpGroup> Groups { get; } = [];

        public InterpData(ExportEntry export)
        {
            Export = export;
            var pcc = export.FileRef;
            var props = export.GetCondensedProperties();
            var groupsProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpGroups");
            if (groupsProp != null)
            {
                foreach (var groupExport in groupsProp.Where(prop => pcc.IsUExport(prop.Value)).Select(prop => pcc.GetUExport(prop.Value)))
                {
                    var group = new InterpGroup(groupExport);
                    Groups.Add(group);
                    group.Parent = this;
                }
            }
        }
    }

    public partial class InterpGroup : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        private string _groupName;
        public string GroupName {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public Color GroupColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

        public ObservableCollectionExtended<InterpTrack> Tracks { get; } = [];

        public InterpGroup(ExportEntry export)
        {
            Export = export;
            GroupName = Export.GetProperty<NameProperty>("GroupName")?.Value.Instanced ?? Export.ObjectName.Instanced;

            if (Export.GetProperty<StructProperty>("GroupColor") is { } colorStruct)
            {
                var color = CommonStructs.GetColor(colorStruct);
                GroupColor = Color.FromArgb(color.A, color.R, color.G, color.B);
            }

            RefreshTracks();
        }

        public void RefreshTracks()
        {
            Tracks.ClearEx();
            var tracksProp = Export.GetProperty<ArrayProperty<ObjectProperty>>("InterpTracks");
            if (tracksProp != null)
            {
                var trackExports = tracksProp.Where(prop => Export.FileRef.IsUExport(prop.Value)).Select(prop => Export.FileRef.GetUExport(prop.Value));
                foreach (ExportEntry trackExport in trackExports)
                {
                    InterpTrack track = InterpTrack.CreateInterpTrackForExport(trackExport);
                    track.Group = this;
                    Tracks.Add(track);
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// Attempts to find the str ref value of the first track with an m_nStrRefID property
        /// </summary>
        /// <remarks>Calls GetProperties on all tracks, so may be expensive</remarks>
        /// <returns>StrRef ID if found, null otherwise</returns>
        public int? TryGetStrRefId()
        {
            return Tracks.Select(t => t.Export.GetProperty<IntProperty>("m_nStrRefID")?.Value)
                .FirstOrDefault(i => i != null);
        }
    }

    public abstract partial class InterpTrack : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        public InterpGroup Group { get; set; }

        private string _trackTitle;
        public string TrackTitle
        {
            get => _trackTitle;
            set => SetProperty(ref _trackTitle, value);
        }

        public ObservableCollectionExtended<Key> Keys { get; } = [];

        /// <summary>
        /// Factory method to create the appropriate <see cref="InterpTrack"/> subclass for an export
        /// </summary>
        /// <param name="trackExport">Export to create InterpTrack for</param>
        /// <returns>New instance of InterpTrack subclass</returns>
        /// <exception cref="FormatException">Export has no supported InterpTrack subclass</exception>
        public static InterpTrack CreateInterpTrackForExport(ExportEntry trackExport)
        {
            if (trackExport.IsA("BioEvtSysTrackDOF"))
            {
                //Depth of Field
                return new BioEvtSysTrackDOF(trackExport);
            }
            else if (trackExport.IsA("BioEvtSysTrackSubtitles"))
            {
                return new BioEvtSysTrackSubtitles(trackExport);
            }
            else if (trackExport.IsA("BioEvtSysTrackGesture"))
            {
                return new BioEvtSysTrackGesture(trackExport);
            }
            else if (trackExport.IsA("BioInterpTrack"))
            {
                string secondaryArrayName = null;
                if (trackExport.IsA("BioEvtSysTrackLighting"))
                {
                    secondaryArrayName = "m_aLightingKeys";
                }
                else if (trackExport.IsA("BioEvtSysTrackLookAt"))
                {
                    secondaryArrayName = "m_aLookAtKeys";
                }
                else if (trackExport.IsA("BioEvtSysTrackProp"))
                {
                    secondaryArrayName = "m_aPropKeys";
                }
                else if (trackExport.IsA("BioEvtSysTrackSetFacing"))
                {
                    secondaryArrayName = "m_aFacingKeys";
                }
                else if (trackExport.IsA("SFXGameInterpTrackProcFoley"))
                {
                    secondaryArrayName = "m_aProcFoleyStartStopKeys";
                }
                else if (trackExport.IsA("SFXInterpTrackPlayFaceOnlyVO"))
                {
                    secondaryArrayName = "m_aFOVOKeys";
                }
                if (secondaryArrayName is not null)
                {
                    return new SFXGameActorInterpTrack(trackExport, secondaryArrayName);
                }

                if (trackExport.IsA("SFXInterpTrackToggleBase"))
                {
                    secondaryArrayName = "m_aToggleKeyData";
                }
                else if (trackExport.IsA("BioEvtSysTrackDOF"))
                {
                    secondaryArrayName = "m_aDOFData";
                }
                else if (trackExport.IsA("BioEvtSysTrackInterrupt"))
                {
                    secondaryArrayName = "m_aInterruptData";
                }
                else if (trackExport.IsA("BioEvtSysTrackSwitchCamera"))
                {
                    secondaryArrayName = "m_aCameras";
                }
                else if (trackExport.IsA("BioInterpTrackRotationMode"))
                {
                    secondaryArrayName = "EventTrack";
                }
                else if (trackExport.IsA("SFXGameInterpTrackWwiseMicLock"))
                {
                    secondaryArrayName = "m_aMicLockKeys";
                }
                else if (trackExport.IsA("SFXInterpTrackAttachCrustEffect"))
                {
                    secondaryArrayName = "m_aCrustEffectKeyData";
                }
                else if (trackExport.IsA("SFXInterpTrackBlackScreen"))
                {
                    secondaryArrayName = "m_aBlackScreenKeyData";
                }
                else if (trackExport.IsA("SFXInterpTrackLightEnvQuality"))
                {
                    secondaryArrayName = "m_aLightEnvKeyData";
                }
                else if (trackExport.IsA("SFXInterpTrackMovieBase"))
                {
                    secondaryArrayName = "m_aMovieKeyData";
                }
                else if (trackExport.IsA("SFXInterpTrackSetPlayerNearClipPlane"))
                {
                    secondaryArrayName = "m_aNearClipKeyData";
                }
                else if (trackExport.IsA("SFXInterpTrackSetWeaponInstant"))
                {
                    secondaryArrayName = "m_aWeaponClassKeyData";
                }
                else if (trackExport.IsA("BioConvNodeTrackDebug"))
                {
                    return new BioConvNodeTrackDebug(trackExport);
                }
                return new BioInterpTrack(trackExport, secondaryArrayName);
            }
            else if (trackExport.ClassName == "InterpTrackSound")
            {
                return new InterpTrackSound(trackExport);
            }
            else if (trackExport.IsA("InterpTrackEvent"))
            {
                return new InterpTrackEvent(trackExport);
            }
            else if (trackExport.IsA("InterpTrackFaceFX"))
            {
                return new InterpTrackFaceFX(trackExport);
            }
            else if (trackExport.IsA("InterpTrackAnimControl"))
            {
                return new InterpTrackAnimControl(trackExport);
            }
            else if (trackExport.IsA("InterpTrackMove"))
            {
                return new InterpTrackMove(trackExport);
            }
            else if (trackExport.IsA("InterpTrackVisibility"))
            {
                return new InterpTrackVisibility(trackExport);
            }
            else if (trackExport.IsA("InterpTrackToggle"))
            {
                return new InterpTrackToggle(trackExport);
            }
            else if (trackExport.IsA("InterpTrackWwiseEvent"))
            {
                return new InterpTrackWwiseEvent(trackExport);
            }
            else if (trackExport.IsA("InterpTrackDirector"))
            {
                return new InterpTrackDirector(trackExport);
            }
            else if (trackExport.IsA("InterpTrackFloatBase"))
            {
                return new InterpTrackFloatBase(trackExport);
            }
            else if (trackExport.IsA("InterpTrackVectorBase"))
            {
                return new InterpTrackVectorBase(trackExport);
            }
            //in ME1, these three are not subclasses of BioInterpTrack, but are otherwise identical
            else if (trackExport.IsA("BioEvtSysTrackVOElements") && trackExport.Game == MEGame.ME1)
            {
                return new BioInterpTrack(trackExport, null);
            }
            else if (trackExport.IsA("BioEvtSysTrackSwitchCamera") && trackExport.Game == MEGame.ME1)
            {
                return new BioInterpTrack(trackExport, "m_aCameras");
            }
            else if (trackExport.IsA("BioEvtSysTrackSetFacing") && trackExport.Game == MEGame.ME1)
            {
                return new BioInterpTrack(trackExport, "m_aFacingKeys");
            }
            else
            {
                throw new FormatException($"Unknown Track Type: {trackExport.ClassName}");
            }
        }

        protected InterpTrack(ExportEntry export)
        {
            Export = export;
            TrackTitle = Export.GetProperty<StrProperty>("TrackTitle")?.Value ?? Export.ObjectName.Instanced;
            LoadTrack();
        }

        public abstract void LoadTrack();

        /// <summary>
        /// Updates the time value of a key at the given index and writes it back to the export.
        /// Subclasses should override this to handle their specific property structures.
        /// </summary>
        public abstract void UpdateKeyTime(int keyIndex, float newTime);

        /// <summary>
        /// Deletes a key at the given index. Reloads the track afterward.
        /// Subclasses should override this to handle their specific property structures.
        /// </summary>
        public abstract void DeleteKey(int keyIndex);

        /// <summary>
        /// Inserts a new key with default values at the given time.
        /// The key is inserted at the correct sorted position.
        /// </summary>
        public abstract void InsertKey(float time);

        #region Key Manipulation Helpers
        /// <summary>
        /// Generates a StructProperty with default values for insertion into an array.
        /// Uses the Unreal object info database to look up the struct type and its defaults.
        /// </summary>
        protected static StructProperty GenerateDefaultStruct(MEGame game, string arrayPropName, string containingClassName)
        {
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(game, arrayPropName, containingClassName);
            string typeName = p.Reference;
            PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(game, typeName, true, null);
            return new StructProperty(typeName, props, isImmutable: GlobalUnrealObjectInfo.IsImmutable(typeName, game));
        }

        /// <summary>
        /// Helper to insert a key with default values into a flat array property.
        /// </summary>
        protected void InsertKeyInArray(string arrayPropName, float time, string timePropName, string containingClassName)
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array == null)
            {
                props.Add(array = new ArrayProperty<StructProperty>(arrayPropName));
            }
            var element = GenerateDefaultStruct(Export.Game, arrayPropName, containingClassName);
            element.GetProp<FloatProperty>(timePropName).Value = time;
            int index = FindSortedInsertionIndex(array, time, timePropName);
            array.Insert(index, element);
            Export.WriteProperties(props);
            LoadTrack();
        }

        /// <summary>
        /// Helper to insert a key with default values into a nested struct/array property.
        /// </summary>
        protected void InsertKeyInNestedArray(string outerPropName, string arrayPropName, float time, string timePropName, string containingClassName)
        {
            var props = Export.GetProperties();
            var outer = props.GetProp<StructProperty>(outerPropName);
            var array = outer?.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null)
            {
                var element = GenerateDefaultStruct(Export.Game, arrayPropName, containingClassName);
                element.GetProp<FloatProperty>(timePropName).Value = time;
                int index = FindSortedInsertionIndex(array, time, timePropName);
                array.Insert(index, element);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Helper to insert a key into a flat array and a synchronized secondary array.
        /// </summary>
        protected void InsertKeyInArrayWithSecondary(string arrayPropName, float time, string timePropName, string containingClassName, string secondaryArrayPropName)
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array == null)
            {
                props.Add(array = new ArrayProperty<StructProperty>(arrayPropName));
            }
            var element = GenerateDefaultStruct(Export.Game, arrayPropName, containingClassName);
            element.GetProp<FloatProperty>(timePropName).Value = time;
            int index = FindSortedInsertionIndex(array, time, timePropName);
            array.Insert(index, element);

            var secondary = props.GetProp<ArrayProperty<StructProperty>>(secondaryArrayPropName);
            if (secondary == null)
            {
                props.Add(secondary = new ArrayProperty<StructProperty>(secondaryArrayPropName));
            }
            var secondaryElement = GenerateDefaultStruct(Export.Game, secondaryArrayPropName, containingClassName);
            secondary.Insert(index, secondaryElement);

            Export.WriteProperties(props);
            LoadTrack();
        }

        /// <summary>
        /// Helper to delete a key from a flat array property and reload.
        /// </summary>
        protected void DeleteKeyFromArray(string arrayPropName, int keyIndex)
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                array.RemoveAt(keyIndex);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Helper to delete a key from a flat array and a synchronized secondary array and reload.
        /// </summary>
        protected void DeleteKeyFromArrayWithSecondary<TSecondaryElement>(string arrayPropName, int keyIndex, string secondaryArrayName) where TSecondaryElement : Property
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                array.RemoveAt(keyIndex);
                var secondary = props.GetProp<ArrayProperty<TSecondaryElement>>(secondaryArrayName);
                if (secondary != null && keyIndex < secondary.Count)
                {
                    secondary.RemoveAt(keyIndex);
                }
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Helper to delete a key from a nested struct/array property and reload.
        /// </summary>
        protected void DeleteKeyFromNestedArray(string outerPropName, string arrayPropName, int keyIndex)
        {
            var props = Export.GetProperties();
            var outer = props.GetProp<StructProperty>(outerPropName);
            var array = outer?.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                array.RemoveAt(keyIndex);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Finds the correct sorted insertion index for a key with the given time, within an array
        /// that has already had the element removed.
        /// </summary>
        protected static int FindSortedInsertionIndex(ArrayProperty<StructProperty> array, float time, string timePropName)
        {
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i].GetProp<FloatProperty>(timePropName).Value > time)
                    return i;
            }
            return array.Count;
        }

        /// <summary>
        /// Helper to update time in a flat array property (e.g., m_aTrackKeys[i].fTime).
        /// Maintains chronological sort order and reloads the track.
        /// </summary>
        protected void UpdateTimeInArray(string arrayPropName, int keyIndex, float newTime, string timePropName)
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                var element = array[keyIndex];
                element.GetProp<FloatProperty>(timePropName).Value = newTime;
                array.RemoveAt(keyIndex);
                int newIndex = FindSortedInsertionIndex(array, newTime, timePropName);
                array.Insert(newIndex, element);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Helper to update time in a flat array while also reordering a synchronized secondary array.
        /// </summary>
        protected void UpdateTimeInArrayWithSecondary<TSecondaryElement>(string arrayPropName, int keyIndex, float newTime, string timePropName, string secondaryArrayPropName) where TSecondaryElement : Property
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                var element = array[keyIndex];
                element.GetProp<FloatProperty>(timePropName).Value = newTime;
                array.RemoveAt(keyIndex);
                int newIndex = FindSortedInsertionIndex(array, newTime, timePropName);
                array.Insert(newIndex, element);

                var secondary = props.GetProp<ArrayProperty<TSecondaryElement>>(secondaryArrayPropName);
                secondary.MoveElement(keyIndex, newIndex);

                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        /// <summary>
        /// Helper to update time in a nested struct/array property (e.g., FloatTrack.Points[i].InVal).
        /// Maintains chronological sort order and reloads the track.
        /// </summary>
        protected void UpdateTimeInNestedArray(string outerPropName, string arrayPropName, int keyIndex, float newTime, string timePropName)
        {
            var props = Export.GetProperties();
            var outer = props.GetProp<StructProperty>(outerPropName);
            var array = outer?.GetProp<ArrayProperty<StructProperty>>(arrayPropName);
            if (array != null && keyIndex < array.Count)
            {
                var element = array[keyIndex];
                element.GetProp<FloatProperty>(timePropName).Value = newTime;
                array.RemoveAt(keyIndex);
                int newIndex = FindSortedInsertionIndex(array, newTime, timePropName);
                array.Insert(newIndex, element);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }
        #endregion

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }

    public class BioInterpTrack : InterpTrack
    {
        public string SecondaryArrayName { get; set; }

        public BioInterpTrack(ExportEntry export, string secondaryArrayName) : base(export)
        {
            SecondaryArrayName = secondaryArrayName;
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (StructProperty bioTrackKey in trackKeys)
                {
                    var fTime = bioTrackKey.GetProp<FloatProperty>("fTime");
                    Keys.Add(new Key(fTime, index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArrayWithSecondary<StrProperty>("m_aTrackKeys", keyIndex, newTime, "fTime", SecondaryArrayName);

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArrayWithSecondary<StrProperty>("m_aTrackKeys", keyIndex, SecondaryArrayName);

        public override void InsertKey(float time) =>
            InsertKeyInArrayWithSecondary("m_aTrackKeys", time, "fTime", Export.ClassName, SecondaryArrayName);
    }

    public partial class SFXGameActorInterpTrack : BioInterpTrack
    {
        public SFXGameActorInterpTrack(ExportEntry export,string secondaryArrayName) : base(export, secondaryArrayName)
        {
        }
    }

    public class BioConvNodeTrackDebug : BioInterpTrack
    {
        public BioConvNodeTrackDebug(ExportEntry export) : base(export, "m_aDbgStrings")
        {
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArrayWithSecondary<StructProperty>("m_aTrackKeys", keyIndex, newTime, "fTime", SecondaryArrayName);

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArrayWithSecondary<StructProperty>("m_aTrackKeys", keyIndex, SecondaryArrayName);

        public override void InsertKey(float time)
        {
            var props = Export.GetProperties();
            var array = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys");
            if (array != null)
            {
                var element = GenerateDefaultStruct(Export.Game, "m_aTrackKeys", Export.ClassName);
                element.GetProp<FloatProperty>("fTime").Value = time;
                int index = FindSortedInsertionIndex(array, time, "fTime");
                array.Insert(index, element);
                var secondary = props.GetProp<ArrayProperty<StrProperty>>(SecondaryArrayName);
                if (secondary != null)
                {
                    secondary.Insert(index, new StrProperty(""));
                }
                Export.WriteProperties(props);
                LoadTrack();
            }
        }
    }

    public class InterpTrackFloatBase : InterpTrack
    {
        public InterpTrackFloatBase(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var floatTrackProp = Export.GetProperty<StructProperty>("FloatTrack");
            if (floatTrackProp?.GetProp<ArrayProperty<StructProperty>>("Points") is { } pointsArray)
            {
                int i = 0;
                foreach (var curvePoint in pointsArray)
                {
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), curvePoint.GetProp<FloatProperty>("OutVal").Value.ToString(), index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInNestedArray("FloatTrack", "Points", keyIndex, newTime, "InVal");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromNestedArray("FloatTrack", "Points", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInNestedArray("FloatTrack", "Points", time, "InVal", "InterpCurveFloat");
    }
    public class InterpTrackVectorBase : InterpTrack
    {
        public InterpTrackVectorBase(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var vectorTrackProp = Export.GetProperty<StructProperty>("VectorTrack");
            if (vectorTrackProp?.GetProp<ArrayProperty<StructProperty>>("Points") is { } pointsArray)
            {
                int i = 0;
                foreach (var curvePoint in pointsArray)
                {
                    (float x, float y, float z) = CommonStructs.GetVector3(curvePoint.GetProp<StructProperty>("OutVal"));
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), $"X={x},Y={y},Z={z}", index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInNestedArray("VectorTrack", "Points", keyIndex, newTime, "InVal");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromNestedArray("VectorTrack", "Points", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInNestedArray("VectorTrack", "Points", time, "InVal", "InterpCurveVector");
    }
    public class InterpTrackSound : InterpTrack
    {
        public InterpTrackSound(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var props = Export.GetProperties();
            var vectorTrackProp = props.GetProp<StructProperty>("VectorTrack");
            var sounds = props.GetProp<ArrayProperty<StructProperty>>("Sounds");
            if (vectorTrackProp?.GetProp<ArrayProperty<StructProperty>>("Points") is { } points)
            {
                int keyindex = 0;
                foreach (var curvePoint in points)
                {
                    int? soundUIndex = sounds?.Count > keyindex ? sounds?[keyindex].GetProp<ObjectProperty>("Sound")?.Value : null;
                    string tooltip = null;
                    if (soundUIndex.HasValue && Export.FileRef.TryGetEntry(soundUIndex.Value, out var entry))
                    {
                        tooltip = $"Sound: {entry.FullPath}\n" +
                                  $"Volume: {sounds?[keyindex].GetProp<FloatProperty>("Volume")?.Value}\n" +
                                  $"Pitch: {sounds?[keyindex].GetProp<FloatProperty>("Pitch")?.Value}\n" +
                                  $"Length: {sounds?[keyindex].GetProp<FloatProperty>("Time")?.Value}";
                    }
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), tooltip, index: keyindex, parentTrack: this));
                    keyindex++;
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime)
        {
            var props = Export.GetProperties();
            var vectorTrack = props.GetProp<StructProperty>("VectorTrack");
            var points = vectorTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            if (points != null && keyIndex < points.Count)
            {
                var element = points[keyIndex];
                element.GetProp<FloatProperty>("InVal").Value = newTime;
                points.RemoveAt(keyIndex);
                int newIndex = FindSortedInsertionIndex(points, newTime, "InVal");
                points.Insert(newIndex, element);

                var sounds = props.GetProp<ArrayProperty<StructProperty>>("Sounds");
                sounds.MoveElement(keyIndex, newIndex);

                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        public override void DeleteKey(int keyIndex)
        {
            var props = Export.GetProperties();
            var vectorTrack = props.GetProp<StructProperty>("VectorTrack");
            var points = vectorTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            var sounds = props.GetProp<ArrayProperty<StructProperty>>("Sounds");
            if (points != null && keyIndex < points.Count)
            {
                points.RemoveAt(keyIndex);
                if (sounds != null && keyIndex < sounds.Count)
                    sounds.RemoveAt(keyIndex);
                Export.WriteProperties(props);
                LoadTrack();
            }
        }

        public override void InsertKey(float time)
        {
            var props = Export.GetProperties();
            var vectorTrack = props.GetProp<StructProperty>("VectorTrack");
            var points = vectorTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            if (points != null)
            {
                var element = GenerateDefaultStruct(Export.Game, "Points", "InterpCurveVector");
                element.GetProp<FloatProperty>("InVal").Value = time;
                int index = FindSortedInsertionIndex(points, time, "InVal");
                points.Insert(index, element);

                var sounds = props.GetProp<ArrayProperty<StructProperty>>("Sounds");
                if (sounds != null)
                {
                    var soundElement = GenerateDefaultStruct(Export.Game, "Sounds", Export.ClassName);
                    sounds.Insert(index, soundElement);
                }

                Export.WriteProperties(props);
                LoadTrack();
            }
        }
    }
    public class InterpTrackEvent : InterpTrack
    {
        public InterpTrackEvent(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("EventTrack");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), trackKey.GetProp<NameProperty>("EventName").Value.Name, index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("EventTrack", keyIndex, newTime, "Time");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("EventTrack", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("EventTrack", time, "Time", Export.ClassName);
    }
    public class InterpTrackFaceFX : InterpTrack
    {
        public InterpTrackFaceFX(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("FaceFXSeqs");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (StructProperty trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("StartTime"), trackKey.GetProp<StrProperty>("FaceFXSeqName")?.Value, index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("FaceFXSeqs", keyIndex, newTime, "StartTime");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("FaceFXSeqs", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("FaceFXSeqs", time, "StartTime", Export.ClassName);
    }
    public partial class InterpTrackAnimControl : InterpTrack
    {
        public InterpTrackAnimControl(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("AnimSeqs");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("StartTime"), index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("AnimSeqs", keyIndex, newTime, "StartTime");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("AnimSeqs", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("AnimSeqs", time, "StartTime", Export.ClassName);
    }
    public partial class InterpTrackMove : InterpTrack
    {
        public InterpTrackMove(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var lookupstruct = Export.GetProperty<StructProperty>("LookupTrack");
            if (lookupstruct != null)
            {
                var trackKeys = lookupstruct.GetProp<ArrayProperty<StructProperty>>("Points"); //on lookuptrack

                var posTrack = Export.GetProperty<StructProperty>("PosTrack");
                var points = posTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
                if (trackKeys != null)
                {
                    int keyindex = 0;
                    foreach (var trackKey in trackKeys)
                    {
                        string tooltip = null;
                        if (points != null && points.Count > keyindex)
                        {
                            StructProperty vector = points[keyindex].GetProp<StructProperty>("OutVal");
                            (float x, float y, float z) = CommonStructs.GetVector3(vector);
                            tooltip = $"X={x},Y={y},Z={z}";
                        }

                        var time = trackKey.GetProp<FloatProperty>("Time");
                        Keys.Add(new Key(time, tooltip, index: keyindex, parentTrack: this));
                        keyindex++;
                    }
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime)
        {
            var props = Export.GetProperties();
            var lookupTrack = props.GetProp<StructProperty>("LookupTrack");
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var eulerTrack = props.GetProp<StructProperty>("EulerTrack");

            var lookupPoints = lookupTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            if (lookupPoints != null && keyIndex < lookupPoints.Count)
            {
                var element = lookupPoints[keyIndex];
                element.GetProp<FloatProperty>("Time").Value = newTime;
                lookupPoints.RemoveAt(keyIndex);
                int newIndex = FindSortedInsertionIndex(lookupPoints, newTime, "Time");
                lookupPoints.Insert(newIndex, element);

                // Move corresponding elements in synchronized tracks
                var posPoints = posTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
                posPoints.MoveElement(keyIndex, newIndex);
                if (posPoints != null && newIndex < posPoints.Count)
                {
                    posPoints[newIndex].GetProp<FloatProperty>("InVal").Value = newTime;
                }

                var eulerPoints = eulerTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
                eulerPoints.MoveElement(keyIndex, newIndex);
                if (eulerPoints != null && newIndex < eulerPoints.Count)
                {
                    eulerPoints[newIndex].GetProp<FloatProperty>("InVal").Value = newTime;
                }
            }

            Export.WriteProperties(props);
            LoadTrack();
        }

        public override void DeleteKey(int keyIndex)
        {
            var props = Export.GetProperties();
            var lookupTrack = props.GetProp<StructProperty>("LookupTrack");
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var eulerTrack = props.GetProp<StructProperty>("EulerTrack");

            var lookupPoints = lookupTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            var posPoints = posTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            var eulerPoints = eulerTrack?.GetProp<ArrayProperty<StructProperty>>("Points");

            if (lookupPoints != null && keyIndex < lookupPoints.Count) lookupPoints.RemoveAt(keyIndex);
            if (posPoints != null && keyIndex < posPoints.Count) posPoints.RemoveAt(keyIndex);
            if (eulerPoints != null && keyIndex < eulerPoints.Count) eulerPoints.RemoveAt(keyIndex);

            Export.WriteProperties(props);
            LoadTrack();
        }

        public override void InsertKey(float time)
        {
            var props = Export.GetProperties();
            var lookupTrack = props.GetProp<StructProperty>("LookupTrack");
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var eulerTrack = props.GetProp<StructProperty>("EulerTrack");

            var lookupPoints = lookupTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            if (lookupPoints != null)
            {
                var lookupElement = GenerateDefaultStruct(Export.Game, "Points", "InterpLookupTrack");
                lookupElement.GetProp<FloatProperty>("Time").Value = time;
                int index = FindSortedInsertionIndex(lookupPoints, time, "Time");
                lookupPoints.Insert(index, lookupElement);

                var posPoints = posTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
                if (posPoints != null)
                {
                    var posElement = GenerateDefaultStruct(Export.Game, "Points", "InterpCurveVector");
                    posElement.GetProp<FloatProperty>("InVal").Value = time;
                    posPoints.Insert(index, posElement);
                }

                var eulerPoints = eulerTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
                if (eulerPoints != null)
                {
                    var eulerElement = GenerateDefaultStruct(Export.Game, "Points", "InterpCurveVector");
                    eulerElement.GetProp<FloatProperty>("InVal").Value = time;
                    eulerPoints.Insert(index, eulerElement);
                }
            }

            Export.WriteProperties(props);
            LoadTrack();
        }
    }
    public class InterpTrackVisibility : InterpTrack
    {
        public InterpTrackVisibility(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("VisibilityTrack");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("VisibilityTrack", keyIndex, newTime, "Time");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("VisibilityTrack", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("VisibilityTrack", time, "Time", Export.ClassName);
    }
    public class InterpTrackToggle : InterpTrack
    {
        public InterpTrackToggle(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("ToggleTrack");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("ToggleTrack", keyIndex, newTime, "Time");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("ToggleTrack", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("ToggleTrack", time, "Time", Export.ClassName);
    }
    public class InterpTrackWwiseEvent : InterpTrack
    {
        public InterpTrackWwiseEvent(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("WwiseEvents");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("WwiseEvents", keyIndex, newTime, "Time");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("WwiseEvents", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("WwiseEvents", time, "Time", Export.ClassName);
    }
    public class InterpTrackDirector : InterpTrack
    {
        public InterpTrackDirector(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("CutTrack");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), trackKey.GetProp<NameProperty>("TargetCamGroup").Value.Name, index: i++, parentTrack: this));
                }
            }
        }

        public override void UpdateKeyTime(int keyIndex, float newTime) =>
            UpdateTimeInArray("CutTrack", keyIndex, newTime, "Time");

        public override void DeleteKey(int keyIndex) =>
            DeleteKeyFromArray("CutTrack", keyIndex);

        public override void InsertKey(float time) =>
            InsertKeyInArray("CutTrack", time, "Time", Export.ClassName);
    }

    public class BioEvtSysTrackDOF : BioInterpTrack
    {
        public BioEvtSysTrackDOF(ExportEntry export) : base(export, "m_aDOFData")
        {
        }
    }

    public class BioEvtSysTrackSubtitles : BioInterpTrack
    {
        public BioEvtSysTrackSubtitles(ExportEntry export) : base(export, "m_aSubtitleData")
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            var subtitleData = Export.GetProperty<ArrayProperty<StructProperty>>("m_aSubtitleData");
            if (trackKeys != null)
            {
                int keyindex = 0;
                foreach (var trackKey in trackKeys)
                {
                    int strRef = subtitleData?[keyindex]?.GetProp<IntProperty>("nStrRefID");
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime"), TLKManagerWPF.GlobalFindStrRefbyID(strRef, Export.FileRef), index: keyindex, parentTrack: this));
                    keyindex++;
                }
            }
        }
    }

    public partial class BioEvtSysTrackGesture : SFXGameActorInterpTrack
    {
        public BioEvtSysTrackGesture(ExportEntry export) : base(export, "m_aGestures")
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            var gestureData = Export.GetProperty<ArrayProperty<StructProperty>>("m_aGestures");
            if (trackKeys != null)
            {
                int i = 0;
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime"), index: i++, parentTrack: this));
                }
            }
        }
    }
}
