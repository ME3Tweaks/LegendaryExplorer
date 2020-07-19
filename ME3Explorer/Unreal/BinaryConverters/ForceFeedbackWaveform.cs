using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.ME3Enums;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class ForceFeedbackWaveform : ObjectBinary
    {
        public bool IsLooping;
        public List<WaveformSample> Samples;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref IsLooping);
                sc.Serialize(ref Samples, SCExt.Serialize);
            }
        }
    }

    public class WaveformSample
    {
        public byte LeftAmplitude;
        public byte RightAmplitude;
        public EWaveformFunction LeftFunction;
        public EWaveformFunction RightFunction;
        public float Duration;
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;
    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref WaveformSample sample)
        {
            if (sc.IsLoading)
            {
                sample = new WaveformSample();
            }
            sc.Serialize(ref sample.LeftAmplitude);
            sc.Serialize(ref sample.RightAmplitude);
            byte leftFunc = (byte)sample.LeftFunction;
            byte rightFunc = (byte)sample.RightFunction;
            sc.Serialize(ref leftFunc);
            sc.Serialize(ref rightFunc);
            sample.LeftFunction = (EWaveformFunction)leftFunc;
            sample.RightFunction = (EWaveformFunction)rightFunc;
            sc.Serialize(ref sample.Duration);
        }
    }
}
