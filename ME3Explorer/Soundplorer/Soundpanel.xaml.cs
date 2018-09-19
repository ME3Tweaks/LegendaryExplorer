using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using NAudio.Vorbis;
using NAudio.Wave;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Soundpanel.xaml
    /// </summary>
    public partial class Soundpanel : ExportLoaderControl
    {
        new MEGame[] SupportedGames = new MEGame[] { MEGame.ME3 };

        WwiseStream w;
        public string afcPath = "";
        WaveOutEvent waveOut = new NAudio.Wave.WaveOutEvent();
        VorbisWaveReader vorbisStream;
        public Soundpanel()
        {
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            //throw new NotImplementedException();
            WwiseStream w = new WwiseStream(exportEntry.FileRef as ME3Package, exportEntry.Index);
            string s = "#" + exportEntry.Index + " WwiseStream : " + exportEntry.ObjectName + "\n\n";
            s += "Filename : \"" + w.FileName + "\"\n";
            s += "Data size: " + w.DataSize + " bytes\n";
            s += "Data offset: 0x" + w.DataOffset.ToString("X8") + "\n";
            s += "ID: 0x" + w.Id.ToString("X8") + " = " + w.Id + "\n";
            CurrentLoadedExport = exportEntry;
            infoTextBox.Text = s;
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            CurrentLoadedExport = null;
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return (exportEntry.FileRef.Game == MEGame.ME3 && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
        }

        private void Pause_Clicked(object sender, RoutedEventArgs e)
        {
            WwiseStream w = new WwiseStream(CurrentLoadedExport.FileRef as ME3Package, CurrentLoadedExport.Index);

        }

        private void Play_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport != null)
            {

                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    //Stop();
                    w = new WwiseStream(CurrentLoadedExport);
                    string path;
                    if (w.IsPCCStored)
                    {
                        path = CurrentLoadedExport.FileRef.FileName;
                    }
                    else
                    {
                        path = getPathToAFC(w.FileName);
                    }
                    if (path != "")
                    {
                        //Status.Text = "Loading...";
                        //`w.Play(path);
                        //Status.Text = "Ready";
                        Stream vorbStream = w.GetVorbisStream(path);

                        vorbisStream = new NAudio.Vorbis.VorbisWaveReader(vorbStream);
                        waveOut.Stop();
                        waveOut.Init(vorbisStream);
                        waveOut.Play();
                        // wait here until playback stops or should stop
                    }
                }
            }
        }

        private string getPathToAFC(string afcName)
        {
            string path = ME3Directory.cookedPath;
            if (!File.Exists(path + afcName + ".afc"))
            {
                if (!File.Exists(afcPath + w.FileName + ".afc"))
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = w.FileName + ".afc|" + w.FileName + ".afc";
                    if (d.ShowDialog().Value)
                    {
                        afcPath = System.IO.Path.GetDirectoryName(d.FileName) + '\\';
                    }
                    else
                    {
                        return "";
                    }
                }
                return afcPath;
            }
            return path + afcName + ".afc";
        }
    }
}
