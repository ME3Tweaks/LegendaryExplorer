using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ME3Explorer.SharedUI;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.ME3Structs;
using Color = System.Windows.Media.Color;

namespace ME3Explorer.Matinee
{
    public class InterpGroup : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        public string GroupName { get; set; }

        public Color GroupColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

        public ObservableCollectionExtended<InterpTrack> Tracks { get; } = new ObservableCollectionExtended<InterpTrack>();

        public InterpGroup(ExportEntry export)
        {
            Export = export;
            GroupName = export.GetProperty<NameProperty>("GroupName")?.Value ?? "Group";

            if (export.GetProperty<StructProperty>("GroupColor") is StructProperty colorStruct)
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
                    if (trackExport.IsOrInheritsFrom("BioInterpTrack"))
                    {
                        Tracks.Add(new BioInterpTrack(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackFloatBase"))
                    {
                        Tracks.Add(new InterpTrackFloatBase(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackVectorBase"))
                    {
                        Tracks.Add(new InterpTrackVectorBase(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackEvent"))
                    {
                        Tracks.Add(new InterpTrackEvent(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackFaceFX"))
                    {
                        Tracks.Add(new InterpTrackFaceFX(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackAnimControl"))
                    {
                        Tracks.Add(new InterpTrackAnimControl(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackMove"))
                    {
                        Tracks.Add(new InterpTrackMove(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackVisibility"))
                    {
                        Tracks.Add(new InterpTrackVisibility(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackToggle"))
                    {
                        Tracks.Add(new InterpTrackToggle(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackWwiseEvent"))
                    {
                        Tracks.Add(new InterpTrackWwiseEvent(trackExport));
                    }
                    else if (trackExport.IsOrInheritsFrom("InterpTrackDirector"))
                    {
                        Tracks.Add(new InterpTrackDirector(trackExport));
                    }
                    else
                    {
                        throw new FormatException($"Unknown Track Type: {trackExport.ClassName}");
                    }
                }
            }
        }
    }

    public abstract class InterpTrack : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; }

        public string TrackTitle { get; set; }

        public ObservableCollectionExtended<Key> Keys { get; } = new ObservableCollectionExtended<Key>();

        protected InterpTrack(ExportEntry export)
        {
            Export = export;
            TrackTitle = export.GetProperty<StrProperty>("TrackTitle")?.Value ?? export.ObjectName.Instanced;
        }
    }

    public class BioInterpTrack : InterpTrack
    {
        public BioInterpTrack(ExportEntry exp) : base(exp)
        {
            var trackKeys = exp.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
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
            var floatTrackProp = export.GetProperty<StructProperty>("FloatTrack");
            if (floatTrackProp?.GetStruct<InterpCurveFloat>() is InterpCurveFloat curveFloat)
            {
                foreach (var curvePoint in curveFloat.Points)
                {
                    Keys.Add(new Key(curvePoint.InVal));
                }
            }
        }
    }
    public class InterpTrackVectorBase : InterpTrack
    {
        public InterpTrackVectorBase(ExportEntry export) : base(export)
        {
            var vectorTrackProp = export.GetProperty<StructProperty>("VectorTrack");
            if (vectorTrackProp?.GetStruct<InterpCurveVector>() is InterpCurveVector curveVector)
            {
                foreach (var curvePoint in curveVector.Points)
                {
                    Keys.Add(new Key(curvePoint.InVal));
                }
            }
        }
    }
    public class InterpTrackEvent : InterpTrack
    {
        public InterpTrackEvent(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("EventTrack");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<EventTrackKey>())
                {
                    Keys.Add(new Key(trackKey.Time));
                }
            }
        }
    }
    public class InterpTrackFaceFX : InterpTrack
    {
        public InterpTrackFaceFX(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("FaceFXSeqs");
            if (trackKeys != null)
            {
                foreach (StructProperty trackKey in trackKeys)
                {
                    var fTime = trackKey.GetProp<FloatProperty>("StartTime");
                    Keys.Add(new Key(fTime));
                }
            }
        }
    }
    public class InterpTrackAnimControl : InterpTrack
    {
        public InterpTrackAnimControl(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("AnimSeqs");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<AnimControlTrackKey>())
                {
                    Keys.Add(new Key(trackKey.StartTime));
                }
            }
        }
    }
    public class InterpTrackMove : InterpTrack
    {
        public InterpTrackMove(ExportEntry export) : base(export)
        {
        }
    }
    public class InterpTrackVisibility : InterpTrack
    {
        public InterpTrackVisibility(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("VisibilityTrack");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<VisibilityTrackKey>())
                {
                    Keys.Add(new Key(trackKey.Time));
                }
            }
        }
    }
    public class InterpTrackToggle : InterpTrack
    {
        public InterpTrackToggle(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("ToggleTrack");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<ToggleTrackKey>())
                {
                    Keys.Add(new Key(trackKey.Time));
                }
            }
        }
    }
    public class InterpTrackWwiseEvent : InterpTrack
    {
        public InterpTrackWwiseEvent(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("WwiseEvents");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<WwiseEventTrackKey>())
                {
                    Keys.Add(new Key(trackKey.Time));
                }
            }
        }
    }
    public class InterpTrackDirector : InterpTrack
    {
        public InterpTrackDirector(ExportEntry export) : base(export)
        {
            var trackKeys = export.GetProperty<ArrayProperty<StructProperty>>("CutTrack");
            if (trackKeys != null)
            {
                foreach (var trackKey in trackKeys.AsStructs<DirectorTrackCut>())
                {
                    Keys.Add(new Key(trackKey.Time));
                }
            }
        }
    }
}
