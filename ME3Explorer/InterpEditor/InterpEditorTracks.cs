using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.SharedUI;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.Matinee
{
    class InterpGroup : NotifyPropertyChangedBase
    {
        public string GroupName { get; set; }

        public ObservableCollectionExtended<InterpTrack> Tracks { get; } = new ObservableCollectionExtended<InterpTrack>();
    }

    abstract class InterpTrack : NotifyPropertyChangedBase
    {
        public string TrackTitle { get; set; }

        public ObservableCollectionExtended<Key> Keys { get; } = new ObservableCollectionExtended<Key>();
    }

    class BioInterpTrack : InterpTrack
    {
        public BioInterpTrack(IExportEntry exp)
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
    class InterpTrackFloatBase : InterpTrack
    {

    }
    class InterpTrackVectorBase : InterpTrack
    {

    }
    class InterpTrackEvent : InterpTrack
    {

    }
    class InterpTrackFaceFX : InterpTrack
    {

    }
    class InterpTrackAnimControl : InterpTrack
    {

    }
    class InterpTrackMove : InterpTrack
    {

    }
    class InterpTrackVisibility : InterpTrack
    {

    }
    class InterpTrackToggle : InterpTrack
    {

    }
    class InterpTrackWwiseEvent : InterpTrack
    {

    }
    class InterpTrackDirector : InterpTrack
    {

    }
}
