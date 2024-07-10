using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
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

        public static ForceFeedbackWaveform Create(PropertyCollection props)
        {
            var waveform = new ForceFeedbackWaveform
            {
                Samples = new List<WaveformSample>()
            };
            if (props is not null)
            {
                waveform.IsLooping = props.GetProp<BoolProperty>("bIsLooping")?.Value ?? false;
                if (props.GetProp<ArrayProperty<StructProperty>>("Samples") is {} samplesProp)
                {
                    foreach (StructProperty sample in samplesProp)
                    {
                        Enum.TryParse(sample.GetProp<EnumProperty>("LeftFunction")?.Value.Instanced ?? "WF_Constant", true, out EWaveformFunction leftFunction);
                        Enum.TryParse(sample.GetProp<EnumProperty>("RightFunction")?.Value.Instanced ?? "WF_Constant", true, out EWaveformFunction rightFunction);
                        waveform.Samples.Add(new WaveformSample
                        {
                            LeftAmplitude = sample.GetProp<ByteProperty>("LeftAmplitude")?.Value ?? 0,
                            RightAmplitude = sample.GetProp<ByteProperty>("RightAmplitude")?.Value ?? 0,
                            LeftFunction = leftFunction,
                            RightFunction = rightFunction,
                            Duration = sample.GetProp<FloatProperty>("Duration")?.Value ?? 0f
                        });
                    }
                }
            }
            return waveform;
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