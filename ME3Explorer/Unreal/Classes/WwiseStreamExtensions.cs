using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Soundplorer;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal.BinaryConverters;
using NAudio.Wave;

namespace ME3Explorer.Unreal.Classes
{
    public static class WwiseStreamExtensions
    {

        /// <summary>
        /// This method is deprecated and will be removed eventually
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pathtoafc"></param>
        public static void ImportFromFile(this WwiseStream ws, string path, string pathtoafc = "")
        {
            if (ws.Filename == "")
                return;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (pathtoafc != "")
                {
                    if (File.Exists(pathtoafc))
                        ws.ImportWwiseOgg(pathtoafc, stream);
                    else if (File.Exists(pathtoafc + ws.Filename + ".afc")) //legacy code for old soundplorer
                        ws.ImportWwiseOgg(pathtoafc + ws.Filename + ".afc", stream);
                    else
                    {
                        OpenFileDialog d = new OpenFileDialog();
                        d.Filter = ws.Filename + ".afc|" + ws.Filename + ".afc";
                        if (d.ShowDialog() == DialogResult.OK)
                            ws.ImportWwiseOgg(d.FileName, stream);
                    }
                }
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = ws.Filename + ".afc|" + ws.Filename + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ws.ImportWwiseOgg(d.FileName, stream);
                }
            }
        }

        public static TimeSpan? GetSoundLength(this WwiseStream ws)
        {
            string path;
            if (ws.IsPCCStored)
            {
                path = ws.Export.FileRef.FilePath; //we must load it decompressed.
            }
            else
            {
                path = ws.GetPathToAFC();
            }

            Stream waveStream = ws.CreateWaveStream(path);
            if (waveStream != null)
            {
                //Check it is RIFF
                byte[] riffHeaderBytes = new byte[4];
                waveStream.SeekBegin();
                waveStream.Read(riffHeaderBytes, 0, 4);
                string wemHeader = "" + (char)riffHeaderBytes[0] + (char)riffHeaderBytes[1] + (char)riffHeaderBytes[2] + (char)riffHeaderBytes[3];
                if (wemHeader == "RIFF")
                {
                    waveStream.SeekBegin();
                    WaveFileReader wf = new WaveFileReader(waveStream);
                    return wf.TotalTime;
                }
            }
            return null;
        }



        /// <summary>
        /// Creates wav file in temp directory
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public static string CreateWave(this WwiseStream ws, string afcPath)
        {
            string basePath = WwiseStreamHelper.GetATempSoundPath();
            if (WwiseStreamHelper.ExtractRawFromSourceToFile(basePath + ".wem", ws.GetPathToAFC(), ws.DataSize, ws.DataOffset))
            {
                var dataStream = ISBankEntry.ConvertAudioToWave(basePath + ".wem");
                //MemoryStream dataStream = ConvertRiffToWav(basePath + ".dat", export.FileRef.Game == MEGame.ME2);
                File.WriteAllBytes(basePath + ".wav", dataStream.ToArray());
            }
            return basePath + ".wav";
        }

        /// <summary>
        /// Creates wav stream from this WwiseStream
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public static Stream CreateWaveStream(this WwiseStream ws, string afcPath)
        {
            string basePath = WwiseStreamHelper.GetATempSoundPath();
            if (WwiseStreamHelper.ExtractRawFromSourceToFile(basePath + ".wem", afcPath, ws.DataSize, ws.DataOffset))
            {
                return ISBankEntry.ConvertAudioToWave(basePath + ".wem");
                //return ConvertRiffToWav(basePath + ".wem", export.FileRef.Game == MEGame.ME2);
            }
            return null;
        }

        public static bool ExtractRawFromSourceToFile(this WwiseStream ws, string outputFile, string afcPath)
        {
            return WwiseStreamHelper.ExtractRawFromSourceToFile(outputFile, afcPath, ws.DataSize, ws.DataOffset);
        }

        private static void ImportWwiseOgg(this WwiseStream ws, string pathafc, Stream wwiseOggStream)
        {
            if (!File.Exists(pathafc) || wwiseOggStream == null)
                return;
            //Convert wwiseoggstream
            MemoryStream convertedStream = WwiseStreamHelper.ConvertWwiseOggToME3Ogg(wwiseOggStream);
            byte[] newWavfile = convertedStream.ToArray();
            //Open AFC
            FileStream fs = new FileStream(pathafc, FileMode.Open, FileAccess.Read);
            var Header = new byte[94];

            //Seek to data we are replacing and read header
            fs.Seek(ws.DataOffset, SeekOrigin.Begin);
            fs.Read(Header, 0, 94);
            fs.Close();


            //append new wav
            fs = new FileStream(pathafc, FileMode.Append, FileAccess.Write, FileShare.Write);
            int newWavDataOffset = (int)fs.Length;
            int newWavSize = newWavfile.Length;
            fs.Write(newWavfile, 0, newWavSize);
            fs.Close();

            ws.DataSize = newWavSize;
            ws.DataOffset = newWavDataOffset;
        }
    }
}