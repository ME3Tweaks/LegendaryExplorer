using System;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Color = System.Windows.Media.Color;

namespace LegendaryExplorer.Tools.InterpEditor
{
    public class InterpGroup : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        private string _groupName;
        public string GroupName {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public Color GroupColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

        public ObservableCollectionExtended<InterpTrack> Tracks { get; } = new();

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
                    Tracks.Add(InterpTrack.CreateInterpTrackForExport(trackExport));
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

    public abstract class InterpTrack : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        private string _trackTitle;
        public string TrackTitle
        {
            get => _trackTitle;
            set => SetProperty(ref _trackTitle, value);
        }

        public ObservableCollectionExtended<Key> Keys { get; } = new();

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
                return new BioInterpTrack(trackExport);
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
            else if (trackExport.IsA("BioEvtSysTrackVOElements") && trackExport.Game == MEGame.ME1)
            {
                return new BioInterpTrack(trackExport);
            }
            else if (trackExport.IsA("BioEvtSysTrackSwitchCamera") && trackExport.Game == MEGame.ME1)
            {
                // Unsure if we should parse extra data out of this
                return new BioInterpTrack(trackExport);
            }
            else if (trackExport.IsA("BioEvtSysTrackSetFacing") && trackExport.Game == MEGame.ME1)
            {
                // Unsure if we should parse extra data out of this
                return new BioInterpTrack(trackExport);
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
        public BioInterpTrack(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            if (trackKeys != null)
            {
                foreach (StructProperty bioTrackKey in trackKeys)
                {
                    var fTime = bioTrackKey.GetProp<FloatProperty>("fTime");
                    Keys.Add(new Key(fTime));
                }
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
                foreach (var curvePoint in pointsArray)
                {
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), curvePoint.GetProp<FloatProperty>("OutVal").Value.ToString()));
                }
            }
        }
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
                foreach (var curvePoint in pointsArray)
                {
                    (float x, float y, float z) = CommonStructs.GetVector3(curvePoint.GetProp<StructProperty>("OutVal"));
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), $"X={x},Y={y},Z={z}"));
                }
            }
        }
    }
    public class InterpTrackSound : InterpTrack
    {
        public InterpTrackSound(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var vectorTrackProp = Export.GetProperty<StructProperty>("VectorTrack");
            var sounds = Export.GetProperty<ArrayProperty<StructProperty>>("Sounds");
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
                    Keys.Add(new Key(curvePoint.GetProp<FloatProperty>("InVal"), tooltip));
                    keyindex++;
                }
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>(Export.Game.IsGame1() ? "Time" : "StartTime"), trackKey.GetProp<NameProperty>("EventName").Value.Name));
                }
            }
        }
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
                foreach (StructProperty trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("StartTime"), trackKey.GetProp<StrProperty>("FaceFXSeqName")?.Value));
                }
            }
        }
    }
    public class InterpTrackAnimControl : InterpTrack
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("StartTime")));
                }
            }
        }
    }
    public class InterpTrackMove : InterpTrack
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
                        Keys.Add(new Key(time, tooltip));
                        keyindex++;
                    }
                }
            }
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time")));
                }
            }
        }
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time")));
                }
            }
        }
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time")));
                }
            }
        }
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
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("Time"), trackKey.GetProp<NameProperty>("TargetCamGroup").Value.Name));
                }
            }
        }
    }

    public class BioEvtSysTrackDOF : InterpTrack
    {
        public BioEvtSysTrackDOF(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime")));
                }
            }
        }
    }

    public class BioEvtSysTrackSubtitles : InterpTrack
    {
        public BioEvtSysTrackSubtitles(ExportEntry export) : base(export)
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
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime"), TLKManagerWPF.GlobalFindStrRefbyID(strRef, Export.FileRef)));
                }
            }
        }
    }

    public class BioEvtSysTrackGesture : InterpTrack
    {
        public BioEvtSysTrackGesture(ExportEntry export) : base(export)
        {
        }

        public override void LoadTrack()
        {
            Keys.ClearEx();
            var trackKeys = Export.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
            var gestureData = Export.GetProperty<ArrayProperty<StructProperty>>("m_aGestures");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys)
                {
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime")));
                }
            }
        }
    }
}
