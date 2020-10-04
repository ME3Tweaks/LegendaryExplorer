using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3Explorer.Soundplorer
{
    class AFCCompactor
    {
        internal class ReferencedAudio
        {
            public bool Equals(ReferencedAudio x, ReferencedAudio y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.afcName == y.afcName && x.audioOffset == y.audioOffset;
            }

            public int GetHashCode(ReferencedAudio obj)
            {
                unchecked
                {
                    return ((obj.afcName != null ? obj.afcName.GetHashCode() : 0) * 397) ^ obj.audioOffset.GetHashCode();
                }
            }

            public string afcName { get; set; }
            public long audioOffset { get; set; }
            public long audioSize { get; set; }
            public string uiSourceName { get; set; }
        }

        public static List<ReferencedAudio> GetReferencedAudio(MEGame game, string inputPath)
        {
            var pccFiles = Directory.GetFiles(inputPath, "*.pcc", SearchOption.AllDirectories);
            var afcFiles = Directory.GetFiles(inputPath, "*.afc", SearchOption.AllDirectories);
            var gameAFCFiles = MELoadedFiles.GetAllFiles(game, includeAFCs: true).Where(x => Path.GetExtension(x) == ".afc");
            var referencedAFCAudio = new List<ReferencedAudio>();

            int i = 1;
            foreach (string pccPath in pccFiles)
            {
                //NotifyStatusUpdate?.Invoke($"Finding all referenced audio ({i}/{pccFiles.Length})");
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    List<ExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (ExportEntry exp in wwiseStreamExports)
                    {
                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null)
                        {
                            var afcFile = afcFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
                            if (afcFile == null)
                            {
                                // Try to find basegame version
                                afcFile = gameAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
                            }
                            if (afcFile != null)
                            {
                                string afcName = afcNameProp.ToString().ToLower();
                                int readPos = exp.Data.Length - 8;
                                int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                                int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
                                referencedAFCAudio.Add(new ReferencedAudio()
                                {
                                    afcName = afcName,
                                    audioSize = audioSize,
                                    audioOffset = audioOffset,
                                    uiSourceName = exp.InstancedFullPath
                                });

                            }
                        }
                    }
                }
                i++;
            }
            referencedAFCAudio = referencedAFCAudio.Distinct().ToList();
            return referencedAFCAudio;
        }

        public void CompactAFC(string inputPath, string newAFCBaseName, Action<string> NotifyStatusUpdate = null)
        {


            ////extract referenced audio
            ////BusyText = "Extracting referenced audio";
            //var extractedAudioMap = new Dictionary<ReferencedAudio, byte[]>();
            //i = 1;
            //foreach (var reference in referencedAFCAudio)
            //{
            //    NotifyStatusUpdate?.Invoke($"Extracting referenced audio ({i} / {referencedAFCAudio.Count}");
            //    var afcPath = afcFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(reference.afcName, StringComparison.InvariantCultureIgnoreCase));
            //    FileStream stream = new FileStream(afcPath, FileMode.Open, FileAccess.Read);
            //    stream.Seek(reference.audioOffset, SeekOrigin.Begin);
            //    var extractedAudio = new byte[reference.audioSize];
            //    stream.Read(extractedAudio, 0, (int)reference.audioSize);
            //    stream.Close();
            //    extractedAudioMap[reference] = extractedAudio;
            //    i++;
            //}

            //var newAFCEntryPointMap = new Dictionary<ReferencedAudio, long>();
            //i = 1;
            //string newAfcPath = Path.Combine(inputPath, newAFCBaseName + ".afc");
            //if (File.Exists(newAfcPath))
            //{
            //    File.Delete(newAfcPath);
            //}
            //FileStream newAFCStream = new FileStream(newAfcPath, FileMode.CreateNew, FileAccess.Write);



            //foreach (var reference in referencedAFCAudio)
            //{
            //    NotifyStatusUpdate?.Invoke($"Building new AFC file ({i} / {referencedAFCAudio.Count})");
            //    newAFCEntryPointMap[reference] = newAFCStream.Position; //save entry point in map
            //    newAFCStream.Write(extractedAudioMap[reference], 0, extractedAudioMap[reference].Length);
            //    i++;
            //}
            //newAFCStream.Close();
            //extractedAudioMap = null; //clean out ram on next GC

            //i = 1;
            //foreach (string pccPath in pccFiles)
            //{
            //    NotifyStatusUpdate?.Invoke($"Updating audio references ({i}/{pccFiles.Length})");
            //    using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
            //    {
            //        bool shouldSave = false;
            //        List<ExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
            //        foreach (ExportEntry exp in wwiseStreamExports)
            //        {
            //            var afcNameProp = exp.GetProperty<NameProperty>("Filename");
            //            if (afcNameProp != null)
            //            {
            //                var afcPath = afcFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
            //                if (afcPath != null)
            //                {
            //                    //it's in list of AFCs
            //                    string afcName = afcNameProp.ToString().ToLower();
            //                    int readPos = exp.Data.Length - 8;
            //                    int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
            //                    int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
            //                    var key = new ReferencedAudio() { afcName = afcName, audioSize = audioSize, audioOffset = audioOffset };
            //                    if (newAFCEntryPointMap.TryGetValue(key, out long newOffset))
            //                    {
            //                        //its a match
            //                        afcNameProp.Value = newAFCBaseName;
            //                        Application.Current.Dispatcher.Invoke(() =>
            //                        {
            //                            exp.WriteProperty(afcNameProp);
            //                            byte[] newData = exp.Data;
            //                            Buffer.BlockCopy(BitConverter.GetBytes((int)newOffset), 0, newData, newData.Length - 4, 4); //update AFC audio offset
            //                            exp.Data = newData;
            //                            if (exp.DataChanged)
            //                            {
            //                                //don't mark for saving if the data didn't actually change (e.g. trying to compact a compacted AFC).
            //                                shouldSave = true;
            //                            }
            //                        });
            //                    }
            //                }
            //            }
            //        }

            //        if (shouldSave)
            //        {
            //            // Must run on the UI thread or the tool interop will throw an exception
            //            // because we are on a background thread.
            //            Application.Current.Dispatcher.Invoke(() => pack.Save(false));
            //        }
            //    }
            //    i++;
            //}
        }
    }
}
