using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Class that stores basic audio information such as sample rate, bits per sample, codec, etc
    /// </summary>
    public class AudioInfo : INotifyPropertyChanged
    {
        /// <summary>
        /// Size of the audio sample chunk (typically the value that follows the 'data' chunk header)
        /// </summary>
        public uint AudioDataSize { get; set; }
        public uint Channels { get; set; }
        public uint BitsPerSample { get; set; }
        public uint SampleRate { get; set; }
        public int CodecID { get; set; }
        public string CodecName { get; set; }
        /// <summary>
        /// Number of known samples
        /// </summary>
        public uint SampleCount { get; set; }

        /// <summary>
        /// Gets an ESTIMATED length of the audio
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetLength()
        {
            if (Channels == 0) return TimeSpan.Zero; //division by zero
            if (SampleCount > 0 && SampleRate > 0)
            {
                return TimeSpan.FromSeconds((double)SampleCount / SampleRate);
            } else if (AudioDataSize > 0 && BitsPerSample > 0 && SampleRate > 0 && Channels > 0)
            {
                return TimeSpan.FromSeconds(AudioDataSize / (SampleRate * ((double)BitsPerSample / 8) * Channels));
            }

            return TimeSpan.Zero;
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
