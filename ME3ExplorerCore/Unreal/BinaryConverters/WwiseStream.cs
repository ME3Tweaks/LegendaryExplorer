using System;
using System.IO;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class WwiseStream : ObjectBinary
    {
        public uint Unk1;//ME2
        public uint Unk2;//ME2
        public Guid UnkGuid;//ME2
        public uint Unk3;//ME2
        public uint Unk4;//ME2
        public uint Unk5;
        public int DataSize;
        public int DataOffset;
        public byte[] EmbeddedData;


        public int Id;
        public string Filename;
        public bool IsPCCStored => Filename == null;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.IsLoading)
            {
                Id = Export.GetProperty<IntProperty>("Id");
                Filename = Export.GetProperty<NameProperty>("Filename")?.Value;
            }

            if (sc.Game != MEGame.ME3 && sc.Game != MEGame.ME2)
            {
                throw new Exception($"WwiseStream is not a valid class for {sc.Game}!");
            }
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (Unk1 == 0 && Unk2 == 0)
                {
                    return; //not sure what's going on here
                }
                sc.Serialize(ref UnkGuid);
                sc.Serialize(ref Unk3);
                sc.Serialize(ref Unk4);
            }
            sc.Serialize(ref Unk5);
            if (sc.IsSaving && EmbeddedData != null)
            {
                DataOffset = sc.FileOffset + 12;
                DataSize = EmbeddedData.Length;
            }
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataOffset);
            if (IsPCCStored)
            {
                sc.Serialize(ref EmbeddedData, DataSize);
            }
        }

        /// <summary>
        /// This method is deprecated and will be removed eventually
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pathtoafc"></param>
        public void ImportFromFile(string path, string pathtoafc = "")
        {
            if (Filename == "")
                return;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (pathtoafc != "")
                {
                    if (File.Exists(pathtoafc))
                        ImportWwiseOgg(pathtoafc, stream);
                    else if (File.Exists(pathtoafc + Filename + ".afc")) //legacy code for old soundplorer
                        ImportWwiseOgg(pathtoafc + Filename + ".afc", stream);
                    else
                    {
                        OpenFileDialog d = new OpenFileDialog();
                        d.Filter = Filename + ".afc|" + Filename + ".afc";
                        if (d.ShowDialog() == DialogResult.OK)
                            ImportWwiseOgg(d.FileName, stream);
                    }
                }
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = Filename + ".afc|" + Filename + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ImportWwiseOgg(d.FileName, stream);
                }
            }
        }

        public TimeSpan? GetSoundLength()
        {
            string path;
            if (IsPCCStored)
            {
                path = Export.FileRef.FilePath; //we must load it decompressed.
            }
            else
            {
                path = GetPathToAFC();
            }

            Stream waveStream = CreateWaveStream(path);
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

        public string GetPathToAFC()
        {
            //Check if pcc-stored
            if (Filename == null)
            {
                return null; //it's pcc stored. we will return null for this case since we already coded for "".
            }

            //Look in currect directory first


            string path = Path.Combine(Path.GetDirectoryName(Export.FileRef.FilePath), Filename + ".afc");
            if (File.Exists(path))
            {
                return path; //in current directory of this pcc file
            }

            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Export.FileRef.Game, includeAFCs: true);
            gameFiles.TryGetValue(Filename + ".afc", out string afcPath);
            return afcPath ?? "";
        }

        /// <summary>
        /// Creates wav file in temp directory
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public string CreateWave(string afcPath)
        {
            string basePath = WwiseHelper.GetATempSoundPath();
            if (WwiseHelper.ExtractRawFromSourceToFile(basePath + ".wem", GetPathToAFC(), DataSize, DataOffset))
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
        public Stream CreateWaveStream(string afcPath)
        {
            string basePath = WwiseHelper.GetATempSoundPath();
            if (WwiseHelper.ExtractRawFromSourceToFile(basePath + ".wem", afcPath, DataSize, DataOffset))
            {
                return ISBankEntry.ConvertAudioToWave(basePath + ".wem");
                //return ConvertRiffToWav(basePath + ".wem", export.FileRef.Game == MEGame.ME2);
            }
            return null;
        }

        public bool ExtractRawFromSourceToFile(string outputFile, string afcPath)
        {
            return WwiseHelper.ExtractRawFromSourceToFile(outputFile, afcPath, DataSize, DataOffset);
        }

        private void ImportWwiseOgg(string pathafc, Stream wwiseOggStream)
        {
            if (!File.Exists(pathafc) || wwiseOggStream == null)
                return;
            //Convert wwiseoggstream
            MemoryStream convertedStream = WwiseHelper.ConvertWwiseOggToME3Ogg(wwiseOggStream);
            byte[] newWavfile = convertedStream.ToArray();
            //Open AFC
            FileStream fs = new FileStream(pathafc, FileMode.Open, FileAccess.Read);
            var Header = new byte[94];

            //Seek to data we are replacing and read header
            fs.Seek(DataOffset, SeekOrigin.Begin);
            fs.Read(Header, 0, 94);
            fs.Close();


            //append new wav
            fs = new FileStream(pathafc, FileMode.Append, FileAccess.Write, FileShare.Write);
            int newWavDataOffset = (int)fs.Length;
            int newWavSize = newWavfile.Length;
            fs.Write(newWavfile, 0, newWavSize);
            fs.Close();

            DataSize = newWavSize;
            DataOffset = newWavDataOffset;
        }
    }
}
