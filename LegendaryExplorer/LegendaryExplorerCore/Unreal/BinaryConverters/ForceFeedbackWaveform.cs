using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ForceFeedbackWaveform : ObjectBinary
    {
        public bool IsLooping;
        public List<WaveformSample> Samples;

        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref IsLooping);
                sc.Serialize(ref Samples, sc.Serialize);
            }
        }

        public static ForceFeedbackWaveform Create(PropertyCollection props)
        {
            var waveform = new ForceFeedbackWaveform
            {
                Samples = []
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

    public partial class SerializingContainer
    {
        public void Serialize(ref WaveformSample sample)
        {
            if (IsLoading)
            {
                sample = new WaveformSample();
            }
            Serialize(ref sample.LeftAmplitude);
            Serialize(ref sample.RightAmplitude);
            byte leftFunc = (byte)sample.LeftFunction;
            byte rightFunc = (byte)sample.RightFunction;
            Serialize(ref leftFunc);
            Serialize(ref rightFunc);
            sample.LeftFunction = (EWaveformFunction)leftFunc;
            sample.RightFunction = (EWaveformFunction)rightFunc;
            Serialize(ref sample.Duration);
        }
    }
}