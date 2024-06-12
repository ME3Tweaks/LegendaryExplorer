using System.Diagnostics;
using System.IO;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;

namespace LegendaryExplorer.UnrealExtensions.Classes
{
    public static class WwiseStreamExtensions
    {
        /// <summary>
        /// This method is deprecated and will be removed eventually
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pathtoafc"></param>
        public static void ImportFromFile(this WwiseStream ws, string path, string pathtoafc = "", string forcedAFCBaseName = null)
        {
            if (ws.Filename == "")
                return;
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);

            if (ws.IsPCCStored)
            {
                ws.ImportWwiseOgg(pathtoafc, stream);
            }
            else if (pathtoafc != "")
            {
                if (File.Exists(pathtoafc))
                    ws.ImportWwiseOgg(pathtoafc, stream);
                else if (File.Exists(pathtoafc + ws.Filename + ".afc")) //legacy code for old soundplorer
                    ws.ImportWwiseOgg(pathtoafc + ws.Filename + ".afc", stream);
                else
                {
                    OpenFileDialog d = new OpenFileDialog()
                    {
                        CustomPlaces = AppDirectories.GameCustomPlaces
                    };
                    d.Filter = ws.Filename + ".afc|" + ws.Filename + ".afc";
                    if (d.ShowDialog() == true)
                        ws.ImportWwiseOgg(d.FileName, stream);
                }

                // Update the AFC name - it might be changing
                if (forcedAFCBaseName != null)
                {
                    ws.Export.WriteProperty(new NameProperty(forcedAFCBaseName, "Filename")); // Update the filename
                    ws.Filename = forcedAFCBaseName;
                }
            }
            else if (forcedAFCBaseName != null)
            {
                // Force an AFC - put it next to the package file we are working on
                // IDK where we could reliably put it so user doesn't lose it.
                var savePath = Path.Combine(Directory.GetParent(ws.Export.FileRef.FilePath).FullName, forcedAFCBaseName+".afc");
                ws.ImportWwiseOgg(savePath, stream);
                ws.Export.WriteProperty(new NameProperty(forcedAFCBaseName, "Filename")); // Update the filename
                ws.Filename = forcedAFCBaseName;
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = ws.Filename + ".afc|" + ws.Filename + ".afc";
                if (d.ShowDialog() == true)
                    ws.ImportWwiseOgg(d.FileName, stream);
            }
        }

        /// <summary>
        /// Creates wav file in temp directory
        /// </summary>
        /// <param name="ws"></param>
        /// <returns></returns>
        public static string CreateWave(this WwiseStream ws)
        {
            string basePath = AudioStreamHelper.GetATempSoundPath();
            string wavPath = basePath + ".wav";
            if (ws.CreateWaveStream() is MemoryStream dataStream)
            {
                File.WriteAllBytes(wavPath, dataStream.ToArray());
            }
            return wavPath;
        }

        /// <summary>
        /// Creates wav stream from this WwiseStream
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public static MemoryStream CreateWaveStream(this WwiseStream ws)
        {
            string basePath = AudioStreamHelper.GetATempSoundPath();
            string wemPath = basePath + ".wem";
            if (ws.ExtractRawFromSourceToFile(wemPath))
            {
                return AudioStreamHelper.ConvertRIFFToWaveVGMStream(wemPath);
            }

            return null;
        }

        public static bool ExtractRawFromSourceToFile(this WwiseStream ws, string outputFile)
        {
            if (ws.IsPCCStored)
            {
                if (ws.EmbeddedData is null || ws.EmbeddedData.Length == 0)
                {
                    return false;
                }
                if (File.Exists(outputFile)) File.Delete(outputFile);
                File.WriteAllBytes(outputFile, ws.EmbeddedData);
                return true;
            }
            return AudioStreamHelper.ExtractRawFromSourceToFile(outputFile, ws.GetPathToAFC(), ws.DataSize, ws.DataOffset);
        }

        private static void ImportWwiseOgg(this WwiseStream ws, string pathafc, Stream wwiseOggStream)
        {
            // 07/03/2022 - Change logic to remove check if file exists as we can create AFCs.
            // - Mgamerz
            if (wwiseOggStream == null)
            {
                Debug.WriteLine("Improperly setup ImportWwiseOgg() call!");
                return;
            }

            MemoryStream convertedStream = new MemoryStream();
            if (ws.Export.FileRef.Game is MEGame.ME3)
            {
                //Convert wwiseoggstream
                convertedStream = AudioStreamHelper.ConvertWwiseOggToME3Ogg(wwiseOggStream);
            }
            else wwiseOggStream.CopyToEx(convertedStream, (int)wwiseOggStream.Length);

            byte[] newWavfile = convertedStream.ToArray();

            if (ws.IsPCCStored)
            {
                ws.EmbeddedData = newWavfile;
                //DataSize and DataOffset are automatically calculated during serialization
                //when EmbeddedData != null
                return;
            }

            //Open AFC
            // Disabled 07/03/2022 - Not really sure what this did
            // - Mgamerz
            //FileStream fs = new FileStream(pathafc, FileMode.Open, FileAccess.Read);
            //var Header = new byte[94];

            ////Seek to data we are replacing and read header
            //fs.Seek(ws.DataOffset, SeekOrigin.Begin);
            //fs.Read(Header, 0, 94);
            //fs.Close();

            //append new wav
            var fs = new FileStream(pathafc, FileMode.Append, FileAccess.Write, FileShare.Write);
            int newWavDataOffset = (int)fs.Length;
            int newWavSize = newWavfile.Length;
            fs.Write(newWavfile, 0, newWavSize);
            fs.Close();

            ws.DataSize = newWavSize;
            ws.DataOffset = newWavDataOffset;
            ws.EmbeddedData = null;
        }
    }
}