using System;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1;
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

        public string GroupName { get; set; }

        public Color GroupColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

        public ObservableCollectionExtended<InterpTrack> Tracks { get; } = new();

        public InterpGroup(ExportEntry export)
        {
            Export = export;
            GroupName = Export.GetProperty<NameProperty>("GroupName")?.Value.Instanced ?? Export.ObjectName.Instanced;

            if (Export.GetProperty<StructProperty>("GroupColor") is StructProperty colorStruct)
            {

                var a = colorStruct.GetProp<ByteProperty>("A").Value;
                var r = colorStruct.GetProp<ByteProperty>("R").Value;
                var g = colorStruct.GetProp<ByteProperty>("G").Value;
                var b = colorStruct.GetProp<ByteProperty>("B").Value;
                GroupColor = Color.FromArgb(a, r, g, b);
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
                    if (trackExport.IsA("BioEvtSysTrackDOF"))
                    {
                        //Depth of Field
                        Tracks.Add(new BioEvtSysTrackDOF(trackExport));
                    }
                    else if (trackExport.IsA("BioEvtSysTrackSubtitles"))
                    {
                        Tracks.Add(new BioEvtSysTrackSubtitles(trackExport));
                    }
                    else if (trackExport.IsA("BioEvtSysTrackGesture"))
                    {
                        Tracks.Add(new BioEvtSysTrackGesture(trackExport));
                    }
                    else if (trackExport.IsA("BioInterpTrack"))
                    {
                        Tracks.Add(new BioInterpTrack(trackExport));
                    }
                    else if (trackExport.ClassName == "InterpTrackSound")
                    {
                        Tracks.Add(new InterpTrackSound(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackEvent"))
                    {
                        Tracks.Add(new InterpTrackEvent(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackFaceFX"))
                    {
                        Tracks.Add(new InterpTrackFaceFX(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackAnimControl"))
                    {
                        Tracks.Add(new InterpTrackAnimControl(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackMove"))
                    {
                        Tracks.Add(new InterpTrackMove(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackVisibility"))
                    {
                        Tracks.Add(new InterpTrackVisibility(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackToggle"))
                    {
                        Tracks.Add(new InterpTrackToggle(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackWwiseEvent"))
                    {
                        Tracks.Add(new InterpTrackWwiseEvent(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackDirector"))
                    {
                        Tracks.Add(new InterpTrackDirector(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackFloatBase"))
                    {
                        Tracks.Add(new InterpTrackFloatBase(trackExport));
                    }
                    else if (trackExport.IsA("InterpTrackVectorBase"))
                    {
                        Tracks.Add(new InterpTrackVectorBase(trackExport));
                    }
                    else if (trackExport.IsA("BioEvtSysTrackVOElements") && trackExport.Game == MEGame.ME1)
                    {
                        Tracks.Add(new BioInterpTrack(trackExport));
                    }
                    else
                    {
                        throw new FormatException($"Unknown Track Type: {trackExport.ClassName}");
                    }
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        public int? StrRefId => Tracks.Select(t => t.Export.GetProperty<IntProperty>("m_nStrRefID")?.Value).FirstOrDefault(i => i != null);
    }

    public abstract class InterpTrack : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        public string TrackTitle { get; set; }

        public ObservableCollectionExtended<Key> Keys { get; } = new();

        protected InterpTrack(ExportEntry export)
        {
            Export = export;
            TrackTitle = Export.GetProperty<StrProperty>("TrackTitle")?.Value ?? Export.ObjectName.Instanced;
            LoadTrack();
        }

        public abstract void LoadTrack();


        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }
    }

    public class BioInterpTrack : InterpTrack
    {
        public BioInterpTrack(ExportEntry exp) : base(exp)
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
                    Keys.Add(new Key(trackKey.GetProp<FloatProperty>("fTime"), ME1TalkFiles.findDataById(strRef, Export.FileRef)));
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
