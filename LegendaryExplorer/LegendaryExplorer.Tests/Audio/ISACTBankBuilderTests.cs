using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorer.Tests.Audio;

/// <summary>
/// Verifies deterministic ISACT authoring, event naming, bank mutation, and LE1 package integration.
/// </summary>
[TestClass]
public class ISACTBankBuilderTests
{
    /// <summary>
    /// Initializes package and binary-converter services used by the package integration tests.
    /// </summary>
    [ClassInitialize]
    public static void Initialize(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default);

    /// <summary>
    /// Verifies conversation filenames create the expected samples, gendered events, and ISACT indices.
    /// </summary>
    [TestMethod]
    public void CreateSourceBanks_BuildsExpectedSamplesAndEvents()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "EN_dialogue_example_00000100_M.wav"), 100);
            WritePcmWave(Path.Combine(directory, "EN_dialogue_example_00000101_F.wav"), 120);
            WritePcmWave(Path.Combine(directory, "EN_dialogue_example_00000101_M.wav"), 140);

            var result = ISACTBankBuilder.CreateSourceBanksFromWavFolder(directory, "test_bank");

            CollectionAssert.AreEqual(
                new[] { "VO_100", "VO_100_M", "VO_101", "VO_101_M" },
                result.EventMappings.Select(mapping => mapping.EventName).ToArray());
            CollectionAssert.AreEqual(
                new[] { 0, 0, 1, 2 },
                result.EventMappings.Select(mapping => mapping.SampleIndex).ToArray());

            // Reparse both banks to verify their serialized representation rather than only the source objects.
            using var icbStream = new MemoryStream();
            result.Banks.ICBBank.Write(icbStream);
            icbStream.Position = 0;
            var icb = new ISACTBank(icbStream);

            using var isbStream = new MemoryStream();
            result.Banks.ISBBank.Write(isbStream);
            isbStream.Position = 0;
            var isb = new ISACTBank(isbStream);

            var events = icb.BankChunks.OfType<ISACTListBankChunk>().Where(list => list.ObjectType == "snde").ToList();
            var samples = isb.BankChunks.OfType<ISACTListBankChunk>().Where(list => list.ObjectType == "samp").ToList();
            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(3, samples.Count);

            var index = icb.BankChunks.OfType<ContentIndexBankChunk>().Single();
            CollectionAssert.AreEqual(
                new[] { "VO_100", "VO_100_M", "VO_101", "VO_101_M" },
                index.IndexPages.SelectMany(page => page.IndexEntries).Select(entry => entry.Title).ToArray());

            CollectionAssert.AreEqual(
                new uint[] { 0x10000, 0x10000, 0x10001, 0x10002 },
                events.Select(GetTrackBufferIndex).ToArray());
            Assert.AreEqual(2500, isb.BankChunks.OfType<IntBankChunk>().Single(chunk => chunk.ChunkName == "stri").Value);
            Assert.IsTrue(samples.All(sample => sample.SampleData?.Length > 0));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies deterministic serialization and the shared-audio fallback for unpaired dialogue lines.
    /// </summary>
    [TestMethod]
    public void CreateSourceBanks_IsDeterministicAndUsesSingleGenderAsSharedFallback()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "VO_200_M.wav"), 100);
            WritePcmWave(Path.Combine(directory, "VO_199_F.wav"), 100);

            var first = ISACTBankBuilder.CreateSourceBanksFromWavFolder(directory, "deterministic");
            var second = ISACTBankBuilder.CreateSourceBanksFromWavFolder(directory, "deterministic");

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, first.EventMappings.Select(mapping => mapping.SampleIndex).ToArray());
            CollectionAssert.AreEqual(Serialize(first.Banks.ICBBank), Serialize(second.Banks.ICBBank));
            CollectionAssert.AreEqual(Serialize(first.Banks.ISBBank), Serialize(second.Banks.ISBBank));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies conversation authoring rejects filenames without a trailing string reference.
    /// </summary>
    [TestMethod]
    public void CreateSourceBanks_RejectsMalformedConversationName()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "EN_line_without_a_string_ref.wav"), 100);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                ISACTBankBuilder.CreateSourceBanksFromWavFolder(directory, "invalid"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies content-index entries are divided into the 50-entry pages used by ISACT.
    /// </summary>
    [TestMethod]
    public void CreateSourceBanks_SplitsContentIndexAtLegacyFiftyEntryLimit()
    {
        string directory = CreateTempDirectory();
        try
        {
            for (int line = 0; line < 26; line++)
                WritePcmWave(Path.Combine(directory, $"VO_{1000 + line}_M.wav"), 10);

            var result = ISACTBankBuilder.CreateSourceBanksFromWavFolder(directory, "paged_bank");
            var index = result.Banks.ICBBank.BankChunks.OfType<ContentIndexBankChunk>().Single();

            CollectionAssert.AreEqual(
                new[] { 50, 2 },
                index.IndexPages.Select(page => page.IndexEntries.Length).ToArray());
            Assert.AreEqual(52, result.EventMappings.Count);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies appending preserves compiled sample data and rebases sample and event indices.
    /// </summary>
    [TestMethod]
    public void AppendCompiledBanks_PreservesExistingSamplesAndRebasesAddedObjects()
    {
        string existingDirectory = CreateTempDirectory();
        string additionDirectory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(existingDirectory, "VO_100_M.wav"), 100);
            WritePcmWave(Path.Combine(additionDirectory, "VO_200_F.wav"), 120);
            WritePcmWave(Path.Combine(additionDirectory, "VO_200_M.wav"), 140);

            var existing = ReparsePair(ISACTBankBuilder.CreateSourceBanksFromWavFolder(existingDirectory, "bank").Banks);
            var additions = ReparsePair(ISACTBankBuilder.CreateSourceBanksFromWavFolder(additionDirectory, "bank").Banks);
            byte[] originalSampleData = existing.ISBBank.BankChunks
                .OfType<ISACTListBankChunk>().Single(chunk => chunk.ObjectType == "samp").SampleData.ToArray();

            ISACTBankPair merged = ISACTBankBuilder.AppendCompiledBanks(existing, additions);
            // Serialize and reparse to catch incorrect chunk sizes or index data written by the merge.
            using var icbStream = new MemoryStream(Serialize(merged.ICBBank));
            using var isbStream = new MemoryStream(Serialize(merged.ISBBank));
            var reparsedIcb = new ISACTBank(icbStream);
            var reparsedIsb = new ISACTBank(isbStream);
            var events = reparsedIcb.BankChunks.OfType<ISACTListBankChunk>()
                .Where(chunk => chunk.ObjectType == "snde").ToList();
            var samples = reparsedIsb.BankChunks.OfType<ISACTListBankChunk>()
                .Where(chunk => chunk.ObjectType == "samp").ToList();

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(3, samples.Count);
            CollectionAssert.AreEqual(originalSampleData, samples[0].SampleData);
            CollectionAssert.AreEqual(
                new uint[] { 0x10000, 0x10000, 0x10001, 0x10002 },
                events.Select(GetTrackBufferIndex).ToArray());
            CollectionAssert.AreEqual(
                new[] { "VO_100", "VO_100_M", "VO_200", "VO_200_M" },
                reparsedIcb.BankChunks.OfType<ContentIndexBankChunk>().Single().IndexPages
                    .SelectMany(page => page.IndexEntries).Select(entry => entry.Title).ToArray());
        }
        finally
        {
            Directory.Delete(existingDirectory, true);
            Directory.Delete(additionDirectory, true);
        }
    }

    /// <summary>
    /// Verifies an append cannot introduce a Sound Event title already present in the content bank.
    /// </summary>
    [TestMethod]
    public void AppendCompiledBanks_RejectsDuplicateEvents()
    {
        string firstDirectory = CreateTempDirectory();
        string secondDirectory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(firstDirectory, "VO_100_M.wav"), 100);
            WritePcmWave(Path.Combine(secondDirectory, "VO_100_M.wav"), 120);
            var existing = ReparsePair(ISACTBankBuilder.CreateSourceBanksFromWavFolder(firstDirectory, "bank").Banks);
            var additions = ReparsePair(ISACTBankBuilder.CreateSourceBanksFromWavFolder(secondDirectory, "bank").Banks);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                ISACTBankBuilder.AppendCompiledBanks(existing, additions));
        }
        finally
        {
            Directory.Delete(firstDirectory, true);
            Directory.Delete(secondDirectory, true);
        }
    }

    /// <summary>
    /// Verifies sample replacement retains the original resource identity and all unrelated payloads.
    /// </summary>
    [TestMethod]
    public void ReplaceCompiledSample_PreservesIndexTitleAndOtherSamplePayloads()
    {
        string existingDirectory = CreateTempDirectory();
        string replacementDirectory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(existingDirectory, "VO_100_F.wav"), 100);
            WritePcmWave(Path.Combine(existingDirectory, "VO_100_M.wav"), 120);
            WritePcmWave(Path.Combine(replacementDirectory, "VO_200_M.wav"), 180);
            ISACTBankPair existing = ReparsePair(
                ISACTBankBuilder.CreateSourceBanksFromWavFolder(existingDirectory, "bank").Banks);
            ISACTBankPair replacement = ReparsePair(
                ISACTBankBuilder.CreateSourceBanksFromWavFolder(replacementDirectory, "replacement").Banks);
            var originalSamples = existing.ISBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Where(chunk => chunk.ObjectType == "samp").ToList();
            byte[] untouchedData = originalSamples[0].SampleData.ToArray();
            string replacedTitle = originalSamples[1].TitleInfo.Value;
            ISACTListBankChunk compiledReplacement = replacement.ISBBank.BankChunks
                .OfType<ISACTListBankChunk>().Single(chunk => chunk.ObjectType == "samp");

            ISACTBankBuilder.ReplaceCompiledSample(existing.ISBBank, 1, compiledReplacement);
            using var stream = new MemoryStream(Serialize(existing.ISBBank));
            var reparsed = new ISACTBank(stream);
            var samples = reparsed.BankChunks.OfType<ISACTListBankChunk>()
                .Where(chunk => chunk.ObjectType == "samp").ToList();

            CollectionAssert.AreEqual(untouchedData, samples[0].SampleData);
            Assert.AreEqual(replacedTitle, samples[1].TitleInfo.Value);
            Assert.AreEqual(1, ((IntBankChunk)samples[1].GetChunk("indx")).Value);
            CollectionAssert.AreEqual(compiledReplacement.SampleData, samples[1].SampleData);
        }
        finally
        {
            Directory.Delete(existingDirectory, true);
            Directory.Delete(replacementDirectory, true);
        }
    }

    /// <summary>
    /// Verifies a new BioSoundNodeWaveStreamingData export has the required hierarchy, flags, and stripped ISB.
    /// </summary>
    [TestMethod]
    public void CreateStreamingDataExport_CreatesExportInEmptyPackage()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "VO_100_M.wav"), 100);
            var source = ISACTBankBuilder.WriteSourceBanksFromWavFolder(directory, directory, "native_bank");
            using IMEPackage package = MEPackageHandler.CreateMemoryEmptyPackage("LOC_INT.pcc", MEGame.LE1);

            ExportEntry export = ISACTHelper.CreateSoundNodeWaveStreamingData(
                package, "native_bank", source.ICBPath, source.ISBPath);

            Assert.AreEqual("BioSoundNodeWaveStreamingData", export.ClassName);
            Assert.AreEqual("native_bank", export.ObjectName.Name);
            Assert.AreEqual("DVDStreamingAudioData.PC", export.ParentInstancedFullPath);
            Assert.IsTrue(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.Public));
            Assert.IsTrue(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.Standalone));
            Assert.IsTrue(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.LoadForClient));
            Assert.IsTrue(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.LoadForServer));
            Assert.IsTrue(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.LoadForEdit));
            Assert.IsFalse(export.ObjectFlags.HasFlag(UnrealFlags.EObjectFlags.LocalizedResource));
            Assert.IsTrue(export.ExportFlags.HasFlag(UnrealFlags.EExportFlags.ForcedExport));
            var binary = export.GetBinaryData<BioSoundNodeWaveStreamingData>();
            Assert.AreEqual(2, binary.BankPair.ICBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Count(chunk => chunk.ObjectType == "snde"));
            Assert.AreEqual(1, binary.BankPair.ISBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Count(chunk => chunk.ObjectType == "samp"));
            Assert.IsNull(binary.BankPair.ISBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Single(chunk => chunk.ObjectType == "samp").SampleData);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies localized sample-bank names do not alter the content-bank filename or title.
    /// </summary>
    [TestMethod]
    public void SourceBanks_CanUseLocalizedSampleBankName()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "VO_100_M.wav"), 100);
            var source = ISACTBankBuilder.WriteSourceBanksFromWavFolder(
                directory, directory, "native_bank", sampleBankName: "native_bank_DE");

            Assert.AreEqual("native_bank.icb", Path.GetFileName(source.ICBPath));
            Assert.AreEqual("native_bank_DE.isb", Path.GetFileName(source.ISBPath));
            using var icbStream = File.OpenRead(source.ICBPath);
            using var isbStream = File.OpenRead(source.ISBPath);
            Assert.AreEqual("native_bank.icb", new ISACTBank(icbStream).BankChunks.OfType<TitleBankChunk>().Single().Value);
            Assert.AreEqual("native_bank_DE.isb", new ISACTBank(isbStream).BankChunks.OfType<TitleBankChunk>().Single().Value);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies LE1 localization naming is applied to the streaming root and data export.
    /// </summary>
    [TestMethod]
    public void CreateStreamingDataExport_AppliesLocalizedObjectNames()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "VO_100_M.wav"), 100);
            var source = ISACTBankBuilder.WriteSourceBanksFromWavFolder(
                directory, directory, "native_bank", sampleBankName: "native_bank_DE");
            using IMEPackage package = MEPackageHandler.CreateMemoryEmptyPackage("LOC_DE.pcc", MEGame.LE1);

            ExportEntry export = ISACTHelper.CreateSoundNodeWaveStreamingData(
                package, "native_bank", source.ICBPath, source.ISBPath, MELocalization.DEU);

            Assert.AreEqual("native_bank_DE", export.ObjectName.Name);
            Assert.AreEqual("DVDStreamingAudioData_DE.PC", export.ParentInstancedFullPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies Codex and Soundset filenames produce their required Sound Event names.
    /// </summary>
    [TestMethod]
    public void NamedAuthoringModes_CreateExpectedEvents()
    {
        string codexDirectory = CreateTempDirectory();
        string soundsetDirectory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(codexDirectory, "vo_codex_example_entry.wav"), 100);
            WritePcmWave(Path.Combine(soundsetDirectory, "EN_example_atg00.wav"), 100);
            WritePcmWave(Path.Combine(soundsetDirectory, "EN_example_png03.wav"), 100);
            WritePcmWave(Path.Combine(soundsetDirectory, "EN_example_sb100.wav"), 100);
            WritePcmWave(Path.Combine(soundsetDirectory, "EN_example_sb200.wav"), 100);

            var codex = ISACTBankBuilder.CreateSourceBanksFromWavFolder(
                codexDirectory, "codex", authoringMode: ISACTBankBuilder.AuthoringMode.Codex);
            var soundset = ISACTBankBuilder.CreateSourceBanksFromWavFolder(
                soundsetDirectory, "soundset", authoringMode: ISACTBankBuilder.AuthoringMode.Soundset);

            CollectionAssert.AreEqual(
                new[] { "vo_codex_example_entry" },
                codex.EventMappings.Select(mapping => mapping.EventName).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "VO_AttackGrunt_00", "VO_PainGrunt_03",
                    "VO_SpecialAbilityRacial1_00", "VO_SpecialAbilityRacial2_00"
                },
                soundset.EventMappings.Select(mapping => mapping.EventName).ToArray());
        }
        finally
        {
            Directory.Delete(codexDirectory, true);
            Directory.Delete(soundsetDirectory, true);
        }
    }

    /// <summary>
    /// Verifies looping music authoring creates one queue containing every generated Sound Event.
    /// </summary>
    [TestMethod]
    public void MusicAuthoring_CreatesLoopingSoundQueue()
    {
        string directory = CreateTempDirectory();
        try
        {
            WritePcmWave(Path.Combine(directory, "music_example.wav"), 100);
            var eventsOnly = ISACTBankBuilder.CreateSourceBanksFromWavFolder(
                directory, "mus_example", 2000,
                authoringMode: ISACTBankBuilder.AuthoringMode.Music);
            var result = ISACTBankBuilder.CreateSourceBanksFromWavFolder(
                directory, "mus_example", 2000,
                authoringMode: ISACTBankBuilder.AuthoringMode.Music,
                createLoopingMusicQueue: true);

            Assert.IsFalse(eventsOnly.Banks.ICBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Any(chunk => chunk.ObjectType == "sdqu"));
            ISACTListBankChunk queue = result.Banks.ICBBank.BankChunks.OfType<ISACTListBankChunk>()
                .Single(chunk => chunk.ObjectType == "sdqu");
            CollectionAssert.AreEqual(
                new[] { "music_example", "mus_example" },
                result.Banks.ICBBank.BankChunks.OfType<ContentIndexBankChunk>().Single().IndexPages
                    .SelectMany(page => page.IndexEntries).Select(entry => entry.Title).ToArray());
            CollectionAssert.AreEqual(
                new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                queue.GetChunk("qinf").RawData);
            CollectionAssert.AreEqual(
                new byte[] { (byte)'s', (byte)'n', (byte)'d', (byte)'e', 0, 0, 0, 0 },
                queue.GetChunk("qcnt").RawData);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Reads the first sample-buffer reference from a serialized Sound Event.
    /// </summary>
    private static uint GetTrackBufferIndex(ISACTListBankChunk soundEvent) =>
        ((SoundEventSoundTracks)soundEvent.GetChunk(SoundEventSoundTracks.FixedChunkTitle)).SoundTracks.Single().BufferIndex;

    /// <summary>
    /// Serializes an ISACT bank for binary equality checks and reparsing.
    /// </summary>
    private static byte[] Serialize(ISACTBank bank)
    {
        using var stream = new MemoryStream();
        bank.Write(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Round-trips a bank pair through its binary representation.
    /// </summary>
    private static ISACTBankPair ReparsePair(ISACTBankPair pair)
    {
        using var icbStream = new MemoryStream(Serialize(pair.ICBBank));
        using var isbStream = new MemoryStream(Serialize(pair.ISBBank));
        return new ISACTBankPair { ICBBank = new ISACTBank(icbStream), ISBBank = new ISACTBank(isbStream) };
    }

    /// <summary>
    /// Creates an isolated directory for one test's bank and WAV files.
    /// </summary>
    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"LEX_ISACTBankBuilder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Writes a deterministic mono PCM16 WAV fixture without relying on external audio files.
    /// </summary>
    private static void WritePcmWave(string path, int frameCount)
    {
        const ushort channels = 1;
        const ushort bitsPerSample = 16;
        const uint sampleRate = 44100;
        const ushort blockAlign = channels * (bitsPerSample / 8);
        byte[] pcm = new byte[frameCount * blockAlign];

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }
}
