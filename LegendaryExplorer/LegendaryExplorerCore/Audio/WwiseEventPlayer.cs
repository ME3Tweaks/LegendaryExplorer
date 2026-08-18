using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Resolves a WwiseEvent export to the data the native <see cref="WwiseAudioPlayer"/> engine
    /// needs - the game's global Init.bnk, the WwiseBank containing the event, and any AFC-backed or
    /// pcc-embedded WwiseStream audio the event references - and plays it.
    /// </summary>
    /// <remarks>
    /// LE2 and LE3 are supported - both ship the same Wwise 7110 engine, with the same WwiseAudio.pcc
    /// / Init.bnk / AFC-backed WwiseStream setup. They differ only in how the event's bank/stream
    /// relationships are stored: LE2 nests them under References[0].Relationships, while LE3 stores
    /// Relationships.Bank directly as a property and WwiseStreams in the export's binary Links (see
    /// WwiseEvent.cs, WwiseBankImport.cs, ExportBinaryConverter.cs). LE1 has no Wwise audio (it uses
    /// ISACT); ME2/ME3 use older, incompatible Wwise versions and aren't supported here.
    /// </remarks>
    public static class WwiseEventPlayer
    {
        private static readonly Dictionary<MEGame, byte[]> InitBankCache = new();
        private static uint? _currentPlayingId;

        public static bool CanPlay(ExportEntry export) => export is { ClassName: "WwiseEvent" } && export.Game is MEGame.LE2 or MEGame.LE3;

        /// <summary>
        /// Resolves and plays the given WwiseEvent export, stopping anything previously playing first.
        /// </summary>
        /// <exception cref="ArgumentException">The export isn't a supported WwiseEvent.</exception>
        /// <exception cref="InvalidOperationException">The event's Id, bank, or the game's Init.bnk couldn't be resolved.</exception>
        public static void Play(ExportEntry wwiseEventExport)
        {
            if (!CanPlay(wwiseEventExport))
            {
                throw new ArgumentException($"Cannot play {wwiseEventExport?.InstancedFullPath}: not a supported WwiseEvent export.", nameof(wwiseEventExport));
            }

            int eventId = wwiseEventExport.GetProperty<IntProperty>("Id")
                ?? throw new InvalidOperationException("This WwiseEvent export has no 'Id' property.");

            using var cache = new PackageCache();
            var (bankExport, streamExports) = ResolveRelationships(wwiseEventExport, cache);
            if (bankExport == null)
            {
                throw new InvalidOperationException("Could not resolve the WwiseBank referenced by this event's Relationships property.");
            }

            byte[] initBank = GetInitBank(wwiseEventExport.Game);
            byte[] eventBank = ObjectBinary.From<WwiseBank>(bankExport).BnkFile;

            Stop();

            WwiseAudioPlayer.LoadInitBank(initBank);
            WwiseAudioPlayer.ClearStreams();
            foreach (ExportEntry streamExport in streamExports)
            {
                WwiseStream ws = ObjectBinary.From<WwiseStream>(streamExport);
                byte[] data = GetStreamData(ws);
                if (data != null)
                {
                    WwiseAudioPlayer.RegisterStream((uint)ws.Id, data);
                }
            }
            WwiseAudioPlayer.LoadEventBank(eventBank);

            _currentPlayingId = WwiseAudioPlayer.PostEvent((uint)eventId);
        }

        /// <summary>
        /// Stops whatever this player most recently started playing (or everything, if nothing is tracked).
        /// </summary>
        public static void Stop()
        {
            if (_currentPlayingId is { } id)
            {
                WwiseAudioPlayer.StopPlaying(id);
                _currentPlayingId = null;
            }
            else
            {
                WwiseAudioPlayer.StopAll();
            }
        }

        private static (ExportEntry bank, List<ExportEntry> streams) ResolveRelationships(ExportEntry wwiseEventExport, PackageCache cache)
        {
            IMEPackage pcc = wwiseEventExport.FileRef;
            var streams = new List<ExportEntry>();
            ExportEntry bankExport = null;

            StructProperty relationships;
            if (wwiseEventExport.Game == MEGame.LE3)
            {
                // LE3 layout: Relationships.Bank is a direct property; Streams live in the export's
                // binary Links[0].WwiseStreams instead (see WwiseEvent.cs, ExportBinaryConverter.cs).
                relationships = wwiseEventExport.GetProperty<StructProperty>("Relationships");

                WwiseEvent binary = ObjectBinary.From<WwiseEvent>(wwiseEventExport);
                if (binary.Links is { Count: > 0 })
                {
                    foreach (int uIndex in binary.Links[0].WwiseStreams)
                    {
                        if (ResolveUIndex(pcc, uIndex, cache) is { } streamExport)
                        {
                            streams.Add(streamExport);
                        }
                    }
                }
            }
            else
            {
                // LE2 layout: References[0].Relationships.{Bank, Streams} (see WwiseBankImport.cs)
                var references = wwiseEventExport.GetProperty<ArrayProperty<StructProperty>>("References");
                relationships = references is { Count: > 0 } ? references[0].GetProp<StructProperty>("Relationships") : null;

                if (relationships?.GetProp<ArrayProperty<ObjectProperty>>("Streams") is { } streamsProp)
                {
                    foreach (ObjectProperty sp in streamsProp)
                    {
                        if (sp.ResolveToExport(pcc, cache) is { } streamExport)
                        {
                            streams.Add(streamExport);
                        }
                    }
                }
            }

            if (relationships?.GetProp<ObjectProperty>("Bank") is { } bankProp)
            {
                bankExport = bankProp.ResolveToExport(pcc, cache);
            }

            return (bankExport, streams);
        }

        private static ExportEntry ResolveUIndex(IMEPackage pcc, int uIndex, PackageCache cache)
        {
            return pcc.GetEntry(uIndex) switch
            {
                ExportEntry exp => exp,
                ImportEntry imp => EntryImporter.ResolveImport(imp, cache),
                _ => null
            };
        }

        private static byte[] GetStreamData(WwiseStream ws)
        {
            if (ws.IsPCCStored)
            {
                return ws.EmbeddedData;
            }

            string afc = ws.GetPathToAFC();
            if (string.IsNullOrWhiteSpace(afc) || !File.Exists(afc))
            {
                return null;
            }

            using Stream stream = ExternalFileHelper.ReadExternalData(afc, ws.DataOffset, ws.DataSize);
            if (stream == null)
            {
                return null;
            }

            var buffer = new byte[ws.DataSize];
            stream.ReadExactly(buffer, 0, ws.DataSize);
            return buffer;
        }

        private static byte[] GetInitBank(MEGame game)
        {
            if (InitBankCache.TryGetValue(game, out byte[] cached))
            {
                return cached;
            }

            if (!MELoadedFiles.TryGetHighestMountedFile(game, "WwiseAudio.pcc", out string wwiseAudioPath))
            {
                throw new InvalidOperationException("Could not locate WwiseAudio.pcc among the game's loaded files.");
            }

            using IMEPackage pcc = MEPackageHandler.UnsafePartialLoad(wwiseAudioPath, exp => exp.InstancedFullPath == "SFXWwise_Init.Init");
            ExportEntry initExport = pcc.Exports.FirstOrDefault(exp => exp.InstancedFullPath == "SFXWwise_Init.Init");
            if (initExport == null)
            {
                throw new InvalidOperationException("Could not find SFXWwise_Init.Init in WwiseAudio.pcc.");
            }

            byte[] initBank = ObjectBinary.From<WwiseBank>(initExport).BnkFile;
            InitBankCache[game] = initBank;
            return initBank;
        }
    }
}
