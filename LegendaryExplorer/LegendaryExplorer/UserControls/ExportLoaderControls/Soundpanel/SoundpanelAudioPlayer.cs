using NAudio.Wave;
using System;
using System.IO;
using LegendaryExplorerCore.Audio;
using NAudio.Vorbis;
using NVorbis;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public class SoundpanelAudioPlayer
    {
        public enum PlaybackStopTypes
        {
            PlaybackStoppedByUser, PlaybackStoppedReachingEndOfFile, PlaybackSwitchedToNewFile
        }

        public PlaybackStopTypes PlaybackStopType { get; set; }

        public WaveStream _audioFileReader { get; set; }
        private WaveOutEvent _output;

        public event Action PlaybackResumed;
        public event Action PlaybackStopped;
        public event Action PlaybackPaused;

        private WaveChannel32 waveChannel;

        public SoundpanelAudioPlayer(Stream audioBuffer, float volume)
        {
            _output = new WaveOutEvent();
            _output.PlaybackStopped += _output_PlaybackStopped;

            if (audioBuffer is RawSourceWaveStream rwss)
            {
                _output.Init(rwss);
            }
            else
            {
                if (audioBuffer is OggWaveStream)
                {
                    _audioFileReader = new VorbisWaveReader(audioBuffer);
                }
                else
                {
                    _audioFileReader = new WaveFileReader(audioBuffer);
                }
                //for debugging
                //audioBuffer.WriteToFile(@"C:\users\Mgamerz\desktop\out.wav");
                waveChannel = new WaveChannel32(_audioFileReader);
                waveChannel.PadWithZeroes = false;
                _output.Init(waveChannel);
            }

            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
        }

        public void Play(PlaybackState playbackState, double currentVolumeLevel)
        {
            if (playbackState == PlaybackState.Stopped || playbackState == PlaybackState.Paused)
            {
                _output.Play();
            }

            waveChannel.Volume = (float)currentVolumeLevel;

            if (PlaybackResumed != null)
            {
                PlaybackResumed();
            }
        }

        private void _output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispose();
            if (PlaybackStopped != null)
            {
                PlaybackStopped();
            }
        }

        public void Stop()
        {
            if (_output != null)
            {
                _output.Stop();
            }
        }

        public void Pause()
        {
            if (_output != null)
            {
                _output.Pause();

                if (PlaybackPaused != null)
                {
                    PlaybackPaused();
                }
            }
        }

        public void TogglePlayPause(double currentVolumeLevel)
        {
            if (_output != null)
            {
                if (_output.PlaybackState == PlaybackState.Playing)
                {
                    Pause();
                }
                else
                {
                    Play(_output.PlaybackState, currentVolumeLevel);
                }
            }
            else
            {
                Play(PlaybackState.Stopped, currentVolumeLevel);
            }
        }

        public void Dispose()
        {
            if (_output != null)
            {
                if (_output.PlaybackState == PlaybackState.Playing)
                {
                    _output.Stop();
                }
                _output.Dispose();
                _output = null;
            }
            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
        }

        public double GetLengthInSeconds()
        {
            if (_audioFileReader != null)
            {
                return _audioFileReader.TotalTime.TotalSeconds;
            }
            else
            {
                return 0;
            }
        }

        public double GetPositionInSeconds()
        {
            return _audioFileReader != null ? _audioFileReader.CurrentTime.TotalSeconds : 0;
        }

        public float GetVolume()
        {
            if (waveChannel != null)
            {
                return waveChannel.Volume;
            }
            return 1;
        }

        public void SetPosition(double value)
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(value);
            }
        }

        public void SetVolume(float value)
        {
            if (waveChannel != null)
            {
                waveChannel.Volume = value;
            }
        }
    }
}
