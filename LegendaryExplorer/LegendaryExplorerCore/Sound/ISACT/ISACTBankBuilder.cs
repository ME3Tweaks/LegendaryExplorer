using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Sound.ISACT;

// Builds LE1 ISACT content and sample banks from PCM WAV files.
public static partial class ISACTBankBuilder
{
    public enum AuthoringMode
    {
        Conversation,
        Codex,
        Soundset,
        Music
    }

    private const int InvalidResourceIndex = int.MinValue;
    private const int DefaultTimeCode = 0x00190028;
    private const int DefaultTempo = 500000;
    private const int DefaultTimeSignature = 0x00040004;
    private const int DefaultSection = unchecked((int)0xE0000000);
    private const int ContentIndexEntriesPerPage = 50;

    // Describes an event-to-sample link created from the WAV filenames.
    public sealed record EventMapping(string EventName, string SampleFileName, int SampleIndex);

    // Source banks and their event mappings.
    public sealed record SourceBankResult(ISACTBankPair Banks, IReadOnlyList<EventMapping> EventMappings);

    // Paths written by WriteSourceBanksFromWavFolder.
    public sealed record SourceBankFiles(string ICBPath, string ISBPath, IReadOnlyList<EventMapping> EventMappings);

    // Final Ogg Vorbis banks produced by BankBuilder.
    public sealed record FinalBankFiles(
        string ICBPath,
        string ISBPath,
        string BuilderLog,
        IReadOnlyList<EventMapping> EventMappings);

    public sealed record SampleReplacementResult(
        string ISBPath,
        string BuilderLog,
        int SampleIndex,
        string SampleName);

    // Appends compiled samples and events without recompressing existing samples.
    public static ISACTBankPair AppendCompiledBanks(ISACTBankPair existing, ISACTBankPair additions)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(additions);
        ValidateBankPair(existing, nameof(existing));
        ValidateBankPair(additions, nameof(additions));

        List<ISACTListBankChunk> existingSamples = GetObjects(existing.ISBBank, "samp");
        List<ISACTListBankChunk> addedSamples = GetObjects(additions.ISBBank, "samp");
        List<ISACTListBankChunk> existingEvents = GetObjects(existing.ICBBank, "snde");
        List<ISACTListBankChunk> addedEvents = GetObjects(additions.ICBBank, "snde");

        EnsureUniqueTitles(existingSamples, addedSamples, "sample");
        EnsureUniqueTitles(existingEvents, addedEvents, "sound event");
        int sampleBase = GetNextResourceIndex(existingSamples);
        int eventBase = GetNextResourceIndex(existingEvents);
        if (sampleBase + addedSamples.Count > ushort.MaxValue)
            throw new InvalidDataException("The merged ISACT sample bank would exceed 65,535 addressable samples.");

        foreach (ISACTListBankChunk sample in addedSamples)
        {
            IntBankChunk index = GetRequiredIndex(sample);
            index.Value = checked(index.Value + sampleBase);
            existing.ISBBank.BankChunks.Add(sample);
        }

        foreach (ISACTListBankChunk soundEvent in addedEvents)
        {
            IntBankChunk index = GetRequiredIndex(soundEvent);
            index.Value = checked(index.Value + eventBase);
            RebaseSoundEventSamples(soundEvent, sampleBase, addedSamples.Count);
            existing.ICBBank.BankChunks.Add(soundEvent);
        }

        ContentIndexBankChunk contentIndex = existing.ICBBank.BankChunks
            .OfType<ContentIndexBankChunk>()
            .SingleOrDefault()
            ?? throw new InvalidDataException("The existing ICB does not contain a content index.");
        IReadOnlyList<IndexEntry> existingIndexEntries = contentIndex.IndexPages
            .SelectMany(page => page.IndexEntries)
            .ToList();
        var mergedIndexEntries = existingIndexEntries
            .Concat(addedEvents.Select((soundEvent, localIndex) => new IndexEntry
            {
                Title = soundEvent.TitleInfo.Value,
                ObjectType = "snde",
                ObjectIndex = checked((uint)(eventBase + localIndex))
            }))
            .ToArray();
        contentIndex.IndexPages = CreateIndexPages(mergedIndexEntries);
        return existing;
    }

    // Replaces one compiled sample while preserving its resource index and title.
    public static void ReplaceCompiledSample(
        ISACTBank existingIsb,
        int sampleIndex,
        ISACTListBankChunk compiledReplacement)
    {
        ArgumentNullException.ThrowIfNull(existingIsb);
        ArgumentNullException.ThrowIfNull(compiledReplacement);
        if (existingIsb.BankType != ISACTBankType.ISB)
            throw new ArgumentException("The existing bank must be an ISB.", nameof(existingIsb));
        if (compiledReplacement.ObjectType != "samp")
            throw new ArgumentException("The replacement object must be an ISACT sample.", nameof(compiledReplacement));

        ISACTListBankChunk target = GetObjects(existingIsb, "samp")
            .SingleOrDefault(sample => GetRequiredIndex(sample).Value == sampleIndex)
            ?? throw new InvalidDataException($"The ISB does not contain sample index {sampleIndex}.");
        string originalTitle = target.TitleInfo?.Value
            ?? throw new InvalidDataException($"ISACT sample {sampleIndex} has no title.");
        int bankChunkIndex = existingIsb.BankChunks.IndexOf(target);
        if (bankChunkIndex < 0)
            throw new InvalidDataException($"ISACT sample {sampleIndex} is not a top-level bank object.");

        GetRequiredIndex(compiledReplacement).Value = sampleIndex;
        if (compiledReplacement.TitleInfo is null)
            throw new InvalidDataException("The compiled replacement sample has no title.");
        compiledReplacement.TitleInfo.RawData = Encoding.Unicode.GetBytes(originalTitle + '\0');

        // Preserve the original authoring path.
        BankChunk originalPath = target.GetChunk("path");
        BankChunk replacementPath = compiledReplacement.GetChunk("path");
        if (originalPath is not null && replacementPath is not null)
            replacementPath.RawData = originalPath.RawData?.ToArray();

        existingIsb.BankChunks[bankChunkIndex] = compiledReplacement;
    }

    private enum DialogueGender
    {
        Shared,
        Female,
        Male
    }

    private sealed record WaveSample(
        string FilePath,
        string FileName,
        ulong LineId,
        DialogueGender Gender,
        int SampleRate,
        ushort BitsPerSample,
        ushort Channels,
        ushort BlockAlign,
        byte[] PCMData);

    private static readonly IReadOnlyDictionary<string, string> SoundsetEventNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["acl"] = "Allclear", ["alr"] = "Alert", ["atg"] = "AttackGrunt",
            ["bfa"] = "BuffActivated", ["dth"] = "DeathCry", ["gcf"] = "GiveCommandFollow",
            ["hpn"] = "HoldingPosition", ["hsq"] = "HealingSquad", ["idl"] = "Idle",
            ["igr"] = "IncomingGrenade", ["jmp"] = "Jump", ["lkf"] = "LOSunavailable",
            ["lnd"] = "Land", ["lse"] = "LOSestablished", ["lwh"] = "LowHealth",
            ["mov"] = "Move", ["mtf"] = "MoveToFailure", ["mts"] = "MoveToSuccess",
            ["pae"] = "PerceptionAttackedByEnemy", ["phe"] = "PerceptionHearEnemy",
            ["png"] = "PainGrunt", ["prc"] = "PlayerRecovery",
            ["shd"] = "ShieldsDown", ["smv"] = "StartMoveToConfirmation",
            ["tbd"] = "TechBeaconDeployed", ["tdr"] = "TacticalDeathRecovery",
            ["tgd"] = "TargetDown", ["tgr"] = "ThrowGrenade", ["vdm"] = "VehicleDamaged",
            ["vsd"] = "VehicleShieldsDown"
        };

    // Creates BankBuilder-ready PCM banks from a WAV folder.
    public static SourceBankResult CreateSourceBanksFromWavFolder(
        string wavFolderPath,
        string bankName,
        int streamPacketMilliseconds = 2500,
        string sampleBankName = null,
        AuthoringMode authoringMode = AuthoringMode.Conversation,
        bool createLoopingMusicQueue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavFolderPath);
        ValidateBankName(bankName);
        sampleBankName ??= bankName;
        ValidateBankName(sampleBankName);
        if (authoringMode == AuthoringMode.Music &&
            !bankName.StartsWith("mus_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Music bank names must begin with 'mus_'.");

        if (!Directory.Exists(wavFolderPath))
            throw new DirectoryNotFoundException($"WAV folder does not exist: {wavFolderPath}");
        if (streamPacketMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(streamPacketMilliseconds), "Packet duration cannot be negative.");

        var wavPaths = Directory.EnumerateFiles(wavFolderPath, "*.wav", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();
        if (wavPaths.Count == 0)
            throw new InvalidDataException($"No WAV files were found in: {wavFolderPath}");
        if (wavPaths.Count > ushort.MaxValue)
            throw new InvalidDataException("An ISACT sample bank cannot contain more than 65,535 addressable samples.");

        var samples = wavPaths.Select(path => ReadWaveSample(path, authoringMode)).ToList();
        if (authoringMode == AuthoringMode.Conversation)
            ValidateLineInputs(samples);

        var sampleIndexes = samples
            .Select((sample, index) => (sample.FilePath, index))
            .ToDictionary(item => item.FilePath, item => item.index, StringComparer.OrdinalIgnoreCase);
        var eventMappings = CreateEventMappings(samples, sampleIndexes, authoringMode);

        var isb = CreateSampleBank(sampleBankName, samples, streamPacketMilliseconds);
        if (createLoopingMusicQueue && authoringMode != AuthoringMode.Music)
            throw new ArgumentException("Looping Sound Queues are only supported for music authoring.", nameof(createLoopingMusicQueue));
        var icb = CreateContentBank(bankName, eventMappings, createLoopingMusicQueue);
        return new SourceBankResult(new ISACTBankPair { ICBBank = icb, ISBBank = isb }, eventMappings);
    }

    // Writes PCM ICB and ISB files for BankBuilder.
    public static SourceBankFiles WriteSourceBanksFromWavFolder(
        string wavFolderPath,
        string outputDirectory,
        string bankName,
        int streamPacketMilliseconds = 2500,
        string sampleBankName = null,
        AuthoringMode authoringMode = AuthoringMode.Conversation,
        bool createLoopingMusicQueue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        sampleBankName ??= bankName;
        var result = CreateSourceBanksFromWavFolder(
            wavFolderPath, bankName, streamPacketMilliseconds, sampleBankName, authoringMode,
            createLoopingMusicQueue);
        Directory.CreateDirectory(outputDirectory);

        string icbPath = Path.Combine(outputDirectory, $"{bankName}.icb");
        string isbPath = Path.Combine(outputDirectory, $"{sampleBankName}.isb");
        using (var stream = File.Create(icbPath))
            result.Banks.ICBBank.Write(stream);
        using (var stream = File.Create(isbPath))
            result.Banks.ISBBank.Write(stream);

        return new SourceBankFiles(icbPath, isbPath, result.EventMappings);
    }

    // Builds and validates final banks with BankBuilder.
    public static async Task<FinalBankFiles> BuildFinalBanksFromWavFolder(
        string wavFolderPath,
        string outputDirectory,
        string bankName,
        string bankBuilderPath,
        int streamPacketMilliseconds = 2500,
        float compressionQuality = 0.8f,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default,
        string sampleBankName = null,
        AuthoringMode authoringMode = AuthoringMode.Conversation,
        bool createLoopingMusicQueue = false)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The official ISACT BankBuilder is a Windows executable.");
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bankBuilderPath);
        sampleBankName ??= bankName;
        ValidateBankName(sampleBankName);
        if (compressionQuality is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(compressionQuality), "Compression quality must be between 0 and 1.");

        string bankBuilderExe = ResolveBankBuilderExecutable(bankBuilderPath);
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"LEX_ISACTBankBuilder_{Guid.NewGuid():N}");
        string toolDirectory = Path.Combine(stagingRoot, "tool");
        string sourceDirectory = Path.Combine(stagingRoot, "source");
        string builtDirectory = Path.Combine(stagingRoot, "built");
        Directory.CreateDirectory(toolDirectory);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(builtDirectory);

        string builderLog = string.Empty;
        try
        {
            string stagedBuilder = Path.Combine(toolDirectory, "BankBuilder.exe");
            File.Copy(bankBuilderExe, stagedBuilder);
            foreach (string codecName in new[] { "ogg.dll", "vorbis.dll", "vorbisfile.dll" })
            {
                string codecPath = ResolveBankBuilderDependency(bankBuilderExe, codecName);
                File.Copy(codecPath, Path.Combine(toolDirectory, codecName));
            }

            SourceBankFiles sourceFiles = WriteSourceBanksFromWavFolder(
                wavFolderPath, sourceDirectory, bankName, streamPacketMilliseconds, sampleBankName,
                authoringMode, createLoopingMusicQueue);
            string quality = compressionQuality.ToString("0.0###", CultureInfo.InvariantCulture);
            var startInfo = new ProcessStartInfo
            {
                FileName = stagedBuilder,
                // BankBuilder's parser expects the value to be concatenated to the switch.
                Arguments = $"-i\"{sourceDirectory}\" -o\"{builtDirectory}\" -cOGGVORBIS -pWindows -q{quality}",
                WorkingDirectory = toolDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("BankBuilder could not be started.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            string builderLogPath = Path.Combine(toolDirectory, "BuilderLog.txt");
            bool completedFromLog = false;
            var elapsed = Stopwatch.StartNew();
            try
            {
                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (elapsed.Elapsed > (timeout ?? TimeSpan.FromMinutes(10)))
                        throw new TimeoutException("ISACT BankBuilder did not finish within the allowed time.");

                    builderLog = TryReadBuilderLog(builderLogPath);
                    if (HasBuilderCompletionSummary(builderLog))
                    {
                        completedFromLog = true;
                        break;
                    }
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                throw;
            }

            // BankBuilder can remain idle after writing its completion summary.
            if (completedFromLog && !process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            string stdout = await standardOutput.ConfigureAwait(false);
            string stderr = await standardError.ConfigureAwait(false);
            builderLog = TryReadBuilderLog(builderLogPath);

            if (!completedFromLog && process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"BankBuilder exited with code {process.ExitCode}.\n{stdout}\n{stderr}\n{builderLog}".Trim());
            }
            if (ContainsBuilderFailure(builderLog))
                throw new InvalidOperationException($"BankBuilder reported a warning or error:\n{builderLog}");

            string builtICB = Path.Combine(builtDirectory, Path.GetFileName(sourceFiles.ICBPath));
            string builtISB = Path.Combine(builtDirectory, Path.GetFileName(sourceFiles.ISBPath));
            ValidateBuiltBanks(
                builtICB, builtISB, sourceFiles.EventMappings.Count, createLoopingMusicQueue);

            Directory.CreateDirectory(outputDirectory);
            string finalICB = Path.Combine(outputDirectory, $"{bankName}.icb");
            string finalISB = Path.Combine(outputDirectory, $"{sampleBankName}.isb");
            File.Copy(builtICB, finalICB, overwrite: true);
            File.Copy(builtISB, finalISB, overwrite: true);
            return new FinalBankFiles(finalICB, finalISB, builderLog, sourceFiles.EventMappings);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                try
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }
                catch (IOException)
                {
                    // Redirected handles may be released after process exit.
                }
                catch (UnauthorizedAccessException)
                {
                    // Preserve the build result.
                }
            }
        }
    }

    // Compiles and appends new WAVs without recompressing existing samples.
    public static async Task<FinalBankFiles> AppendFinalBanksFromWavFolder(
        string existingIcbPath,
        string existingIsbPath,
        string wavFolderPath,
        string outputDirectory,
        string bankBuilderPath,
        int streamPacketMilliseconds = 2500,
        float compressionQuality = 0.8f,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default,
        AuthoringMode authoringMode = AuthoringMode.Conversation)
    {
        if (!File.Exists(existingIcbPath))
            throw new FileNotFoundException("Existing ICB was not found.", existingIcbPath);
        if (!File.Exists(existingIsbPath))
            throw new FileNotFoundException("Existing ISB was not found.", existingIsbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string bankName = Path.GetFileNameWithoutExtension(existingIcbPath);
        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"LEX_ISACTAppend_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            FinalBankFiles compiledAdditions = await BuildFinalBanksFromWavFolder(
                wavFolderPath,
                stagingDirectory,
                bankName,
                bankBuilderPath,
                streamPacketMilliseconds,
                compressionQuality,
                timeout,
                cancellationToken,
                Path.GetFileNameWithoutExtension(existingIsbPath),
                authoringMode).ConfigureAwait(false);

            ISACTBankPair existing;
            using (var icbStream = File.OpenRead(existingIcbPath))
            using (var isbStream = File.OpenRead(existingIsbPath))
            {
                existing = new ISACTBankPair
                {
                    ICBBank = new ISACTBank(icbStream),
                    ISBBank = new ISACTBank(isbStream)
                };
            }

            ISACTBankPair additions;
            using (var icbStream = File.OpenRead(compiledAdditions.ICBPath))
            using (var isbStream = File.OpenRead(compiledAdditions.ISBPath))
            {
                additions = new ISACTBankPair
                {
                    ICBBank = new ISACTBank(icbStream),
                    ISBBank = new ISACTBank(isbStream)
                };
            }

            int existingSampleCount = GetNextResourceIndex(GetObjects(existing.ISBBank, "samp"));
            int existingEventCount = GetObjects(existing.ICBBank, "snde").Count;
            AppendCompiledBanks(existing, additions);

            Directory.CreateDirectory(outputDirectory);
            string outputIcbPath = Path.Combine(outputDirectory, Path.GetFileName(existingIcbPath));
            string outputIsbPath = Path.Combine(outputDirectory, Path.GetFileName(existingIsbPath));
            string temporaryIcbPath = Path.Combine(stagingDirectory, $"merged_{Guid.NewGuid():N}.icb");
            string temporaryIsbPath = Path.Combine(stagingDirectory, $"merged_{Guid.NewGuid():N}.isb");
            using (var stream = File.Create(temporaryIcbPath))
                existing.ICBBank.Write(stream);
            using (var stream = File.Create(temporaryIsbPath))
                existing.ISBBank.Write(stream);

            ValidateBuiltBanks(temporaryIcbPath, temporaryIsbPath,
                existingEventCount + compiledAdditions.EventMappings.Count, expectedSoundQueue: false);
            File.Move(temporaryIcbPath, outputIcbPath, overwrite: true);
            File.Move(temporaryIsbPath, outputIsbPath, overwrite: true);

            var rebasedMappings = compiledAdditions.EventMappings
                .Select(mapping => mapping with { SampleIndex = checked(mapping.SampleIndex + existingSampleCount) })
                .ToList();
            return new FinalBankFiles(
                outputIcbPath,
                outputIsbPath,
                compiledAdditions.BuilderLog,
                rebasedMappings);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                try
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    // Compiles and replaces one ISB sample.
    public static async Task<SampleReplacementResult> ReplaceFinalBankSampleFromWave(
        string existingIsbPath,
        int sampleIndex,
        string replacementWavPath,
        string outputIsbPath,
        string bankBuilderPath,
        int? streamPacketMilliseconds = null,
        float compressionQuality = 0.8f,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(existingIsbPath))
            throw new FileNotFoundException("Existing ISB was not found.", existingIsbPath);
        if (!File.Exists(replacementWavPath))
            throw new FileNotFoundException("Replacement WAV was not found.", replacementWavPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIsbPath);

        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"LEX_ISACTReplace_{Guid.NewGuid():N}");
        string wavDirectory = Path.Combine(stagingDirectory, "wav");
        string compiledDirectory = Path.Combine(stagingDirectory, "compiled");
        Directory.CreateDirectory(wavDirectory);
        try
        {
            ISACTBank existing;
            using (var stream = File.OpenRead(existingIsbPath))
                existing = new ISACTBank(stream);
            int packetMilliseconds = streamPacketMilliseconds
                ?? existing.BankChunks.OfType<IntBankChunk>().FirstOrDefault(chunk => chunk.ChunkName == "stri")?.Value
                ?? 2500;

            string stagedWave = Path.Combine(wavDirectory, "replacement_1.wav");
            File.Copy(replacementWavPath, stagedWave);
            string bankName = Path.GetFileNameWithoutExtension(existingIsbPath);
            FinalBankFiles compiled = await BuildFinalBanksFromWavFolder(
                wavDirectory, compiledDirectory, bankName, bankBuilderPath,
                packetMilliseconds, compressionQuality, timeout, cancellationToken).ConfigureAwait(false);
            ISACTBank replacementBank;
            using (var stream = File.OpenRead(compiled.ISBPath))
                replacementBank = new ISACTBank(stream);
            ISACTListBankChunk replacement = GetObjects(replacementBank, "samp").Single();
            string sampleName = GetObjects(existing, "samp")
                .Single(sample => GetRequiredIndex(sample).Value == sampleIndex).TitleInfo.Value;
            ReplaceCompiledSample(existing, sampleIndex, replacement);

            string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputIsbPath))!;
            Directory.CreateDirectory(outputDirectory);
            string temporaryOutput = Path.Combine(stagingDirectory, $"updated_{Guid.NewGuid():N}.isb");
            using (var stream = File.Create(temporaryOutput))
                existing.Write(stream);
            ValidateFinalIsb(temporaryOutput);
            File.Move(temporaryOutput, outputIsbPath, overwrite: true);
            return new SampleReplacementResult(outputIsbPath, compiled.BuilderLog, sampleIndex, sampleName);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                try { Directory.Delete(stagingDirectory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static ISACTBank CreateSampleBank(string bankName, IReadOnlyList<WaveSample> samples, int packetMilliseconds)
    {
        var bank = new ISACTBank(ISACTBankType.ISB);
        bank.BankChunks.AddRange(CreateCommonObjectChunks(
            title: $"{bankName}.isb",
            resourceIndex: 0,
            globalEffectIndex: InvalidResourceIndex,
            trackCount: 1,
            status: 32));
        bank.BankChunks.Add(IntChunk("stri", packetMilliseconds));
        bank.BankChunks.Add(IntChunk("msti", 0));

        for (int index = 0; index < samples.Count; index++)
            bank.BankChunks.Add(CreateSampleObject(samples[index], index));
        return bank;
    }

    private static ISACTListBankChunk CreateSampleObject(WaveSample sample, int sampleIndex)
    {
        long frameCount = sample.PCMData.LongLength / sample.BlockAlign;
        int durationMilliseconds = frameCount == 0
            ? 0
            : checked((int)(((frameCount - 1) * 1000L) / sample.SampleRate));

        var chunks = CreateCommonObjectChunks(
            title: sample.FileName,
            resourceIndex: sampleIndex,
            globalEffectIndex: InvalidResourceIndex,
            trackCount: 1,
            status: 0);
        chunks.Add(new SampleInfoBankChunk
        {
            ChunkName = SampleInfoBankChunk.FixedChunkTitle,
            BufferOffset = 0,
            TimeLength = durationMilliseconds,
            SamplesPerSecond = sample.SampleRate,
            ByteLength = sample.PCMData.Length,
            BitsPerSample = sample.BitsPerSample
        });
        chunks.Add(IntChunk("s3di", InvalidResourceIndex));
        chunks.Add(new ChannelBankChunk
        {
            ChunkName = ChannelBankChunk.FixedChunkTitle,
            ChannelCount = sample.Channels
        });
        chunks.Add(IntChunk("prel", 0));
        chunks.Add(new CompressionInfoBankChunk
        {
            ChunkName = CompressionInfoBankChunk.FixedChunkTitle,
            CurrentFormat = CompressionInfoBankChunk.ISACTCompressionFormat.PCM,
            TargetFormat = CompressionInfoBankChunk.ISACTCompressionFormat.PCM,
            TotalSize = 0,
            PacketSize = 0,
            CompressionRatio = 0,
            CompressionQuality = 0.4f
        });
        chunks.Add(IntChunk("chfl", GetChannelFlags(sample.Channels)));
        chunks.Add(UnicodeChunk("path", sample.FilePath));
        chunks.Add(RawChunk(DataBankChunk.FixedChunkTitle, sample.PCMData));
        return new ISACTListBankChunk("samp", chunks);
    }

    private static ISACTBank CreateContentBank(
        string bankName, IReadOnlyList<EventMapping> eventMappings, bool createLoopingQueue)
    {
        var bank = new ISACTBank(ISACTBankType.ICB);
        bank.BankChunks.AddRange(CreateCommonObjectChunks(
            title: $"{bankName}.icb",
            resourceIndex: InvalidResourceIndex,
            globalEffectIndex: InvalidResourceIndex,
            trackCount: 1,
            status: 32));
        bank.BankChunks.Add(CreateContentIndex(eventMappings, createLoopingQueue ? bankName : null));
        bank.BankChunks.Add(IntChunk("segv", 0));

        for (int eventIndex = 0; eventIndex < eventMappings.Count; eventIndex++)
            bank.BankChunks.Add(CreateSoundEvent(eventMappings[eventIndex], eventIndex));
        if (createLoopingQueue)
            bank.BankChunks.Add(CreateSoundQueue(bankName, eventMappings.Count));
        return bank;
    }

    private static ContentIndexBankChunk CreateContentIndex(
        IReadOnlyList<EventMapping> eventMappings, string queueName)
    {
        IEnumerable<IndexEntry> entries = eventMappings.Select((mapping, index) => new IndexEntry
        {
            Title = mapping.EventName,
            ObjectType = "snde",
            ObjectIndex = (uint)index
        });
        if (queueName is not null)
        {
            entries = entries.Append(new IndexEntry
            {
                Title = queueName,
                ObjectType = "sdqu",
                ObjectIndex = 0
            });
        }

        return new ContentIndexBankChunk
        {
            ChunkName = ContentIndexBankChunk.FixedChunkTitle,
            IndexPages = CreateIndexPages(entries.ToArray())
        };
    }

    private static ISACTListBankChunk CreateSoundQueue(string queueName, int eventCount)
    {
        var chunks = CreateCommonObjectChunks(
            title: queueName,
            resourceIndex: 0,
            globalEffectIndex: -1,
            trackCount: 1,
            status: 0);
        chunks.Add(RawInt32Chunk("qinf", eventCount, 0, eventCount - 1, 0));

        byte[] content = new byte[eventCount * 8];
        uint soundEventType = BinaryPrimitives.ReadUInt32LittleEndian("snde"u8);
        for (int index = 0; index < eventCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(index * 8), soundEventType);
            BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(index * 8 + 4), (uint)index);
        }
        chunks.Add(RawChunk("qcnt", content));
        return new ISACTListBankChunk("sdqu", chunks);
    }

    private static List<IndexPage> CreateIndexPages(IReadOnlyList<IndexEntry> entries)
    {
        var pages = new List<IndexPage>();
        for (int start = 0; start < entries.Count; start += ContentIndexEntriesPerPage)
        {
            IndexEntry[] pageEntries = entries.Skip(start).Take(ContentIndexEntriesPerPage).ToArray();
            pages.Add(new IndexPage
            {
                EntryCount = (uint)pageEntries.Length,
                IndexEntries = pageEntries
            });
        }
        return pages;
    }

    private static void ValidateBankPair(ISACTBankPair pair, string parameterName)
    {
        if (pair.ICBBank?.BankType != ISACTBankType.ICB || pair.ISBBank?.BankType != ISACTBankType.ISB)
            throw new ArgumentException("The bank pair must contain an ICB and an ISB.", parameterName);
    }

    private static List<ISACTListBankChunk> GetObjects(ISACTBank bank, string objectType) =>
        bank.BankChunks.OfType<ISACTListBankChunk>().Where(chunk => chunk.ObjectType == objectType).ToList();

    private static void EnsureUniqueTitles(
        IEnumerable<ISACTListBankChunk> existing,
        IEnumerable<ISACTListBankChunk> additions,
        string objectDescription)
    {
        var titles = new HashSet<string>(
            existing.Select(chunk => chunk.TitleInfo?.Value ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
        foreach (ISACTListBankChunk addition in additions)
        {
            string title = addition.TitleInfo?.Value
                ?? throw new InvalidDataException($"An appended {objectDescription} has no title.");
            if (!titles.Add(title))
                throw new InvalidDataException($"Cannot append duplicate {objectDescription} '{title}'.");
        }
    }

    private static IntBankChunk GetRequiredIndex(ISACTListBankChunk chunk) =>
        chunk.GetChunk("indx") as IntBankChunk
        ?? throw new InvalidDataException($"ISACT {chunk.ObjectType} '{chunk.TitleInfo?.Value}' has no resource index.");

    private static int GetNextResourceIndex(IReadOnlyCollection<ISACTListBankChunk> objects) =>
        objects.Count == 0 ? 0 : checked(objects.Max(chunk => GetRequiredIndex(chunk).Value) + 1);

    private static void RebaseSoundEventSamples(ISACTListBankChunk soundEvent, int sampleBase, int addedSampleCount)
    {
        IEnumerable<ISACTSoundTrack> tracks = soundEvent.GetChunk(SoundEventSoundTracks.FixedChunkTitle) switch
        {
            SoundEventSoundTracks standard => standard.SoundTracks,
            _ when soundEvent.GetChunk(SoundEventSoundTracksFour.FixedChunkTitle) is SoundEventSoundTracksFour compact => compact.SoundTracks,
            _ => throw new InvalidDataException($"Sound event '{soundEvent.TitleInfo?.Value}' has no supported sound tracks.")
        };

        foreach (ISACTSoundTrack track in tracks)
        {
            uint localSampleIndex = track.BufferIndex & 0xFFFF;
            if (localSampleIndex >= addedSampleCount)
                throw new InvalidDataException(
                    $"Sound event '{soundEvent.TitleInfo?.Value}' references missing appended sample {localSampleIndex}.");
            uint rebasedIndex = checked((uint)sampleBase + localSampleIndex);
            track.BufferIndex = (track.BufferIndex & 0xFFFF0000) | rebasedIndex;
        }
    }

    private static ISACTListBankChunk CreateSoundEvent(EventMapping mapping, int eventIndex)
    {
        var chunks = CreateCommonObjectChunks(
            title: mapping.EventName,
            resourceIndex: eventIndex,
            globalEffectIndex: -1,
            trackCount: 1,
            status: 0);
        chunks.Add(new SoundEventInfoBankChunk
        {
            ChunkName = SoundEventInfoBankChunk.FixedChunkTitle,
            EventSelection = SoundEventInfoBankChunk.ISACTSEEventSelection.USE_EVS_CHANCE,
            DefaultChance = 50,
            EqualChance = 1,
            Flags = 0,
            ResetParamsOnLoop = 1,
            ResetSampleOnLoop = 1
        });
        chunks.Add(new SoundEventSoundTracks
        {
            ChunkName = SoundEventSoundTracks.FixedChunkTitle,
            SoundTracks = new List<ISACTSoundTrack> { CreateSoundTrack(mapping.SampleIndex) }
        });
        chunks.Add(RawChunk("silt", Array.Empty<byte>()));
        return new ISACTListBankChunk("snde", chunks);
    }

    private static ISACTSoundTrack CreateSoundTrack(int sampleIndex) => new()
    {
        BufferIndex = 0x10000u | (uint)sampleIndex,
        PathIndex = uint.MaxValue,
        Order = 1,
        Chance = 100,
        Position = Vector3.Zero,
        Orientation = new ISACTOrientation(),
        Velocity = Vector3.Zero,
        MinGain = 1,
        MaxGain = 1,
        MinPitch = 1,
        MaxPitch = 1,
        MinDirect = 1,
        MaxDirect = 1,
        MinDirectHF = 1,
        MaxDirectHF = 1,
        SendEnable = new int[4],
        MinSend = new float[4],
        MaxSend = new float[4],
        MinSendHF = Enumerable.Repeat(1f, 4).ToArray(),
        MaxSendHF = Enumerable.Repeat(1f, 4).ToArray(),
        Flags = 0x3F
    };

    private static List<BankChunk> CreateCommonObjectChunks(
        string title,
        int resourceIndex,
        int globalEffectIndex,
        int trackCount,
        int status)
    {
        return new List<BankChunk>
        {
            IntChunk("stat", status),
            UnicodeChunk(TitleBankChunk.FixedChunkTitle, title),
            IntChunk("indx", resourceIndex),
            IntChunk("geix", globalEffectIndex),
            IntChunk("trks", trackCount),
            IntChunk("tmcd", DefaultTimeCode),
            IntChunk("dtmp", DefaultTempo),
            IntChunk("dtsg", DefaultTimeSignature),
            IntChunk("dsec", DefaultSection),
            RawInt32Chunk("sync", 0, 1),
            IntChunk("loop", 0),
            FloatChunk("gbst", 1),
            RawInt32Chunk("cgvi", -1, -1, -1, -1, 0)
        };
    }

    private static IReadOnlyList<EventMapping> CreateEventMappings(
        IReadOnlyList<WaveSample> samples,
        IReadOnlyDictionary<string, int> sampleIndexes,
        AuthoringMode authoringMode)
    {
        if (authoringMode != AuthoringMode.Conversation)
        {
            var namedMappings = samples.Select(sample => new EventMapping(
                GetNamedEventName(sample, authoringMode),
                sample.FileName,
                sampleIndexes[sample.FilePath])).ToList();
            string duplicate = namedMappings.GroupBy(mapping => mapping.EventName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate is not null)
                throw new InvalidDataException($"Multiple WAV files produce the sound event '{duplicate}'.");
            return namedMappings;
        }

        var mappings = new List<EventMapping>();
        foreach (var group in samples.GroupBy(sample => sample.LineId).OrderBy(group => group.Key))
        {
            WaveSample shared = group.SingleOrDefault(sample => sample.Gender == DialogueGender.Shared);
            WaveSample female = group.SingleOrDefault(sample => sample.Gender == DialogueGender.Female);
            WaveSample male = group.SingleOrDefault(sample => sample.Gender == DialogueGender.Male);
            WaveSample femaleSample = female ?? shared ?? male;
            WaveSample maleSample = male ?? shared ?? female;
            string eventBaseName = $"VO_{group.Key.ToString(CultureInfo.InvariantCulture)}";

            mappings.Add(new EventMapping(eventBaseName, femaleSample.FileName, sampleIndexes[femaleSample.FilePath]));
            mappings.Add(new EventMapping($"{eventBaseName}_M", maleSample.FileName, sampleIndexes[maleSample.FilePath]));
        }
        return mappings;
    }

    private static string GetNamedEventName(WaveSample sample, AuthoringMode authoringMode)
    {
        string fileBaseName = Path.GetFileNameWithoutExtension(sample.FileName);
        if (authoringMode is AuthoringMode.Codex or AuthoringMode.Music)
        {
            if (authoringMode == AuthoringMode.Codex &&
                !fileBaseName.StartsWith("vo_codex_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Codex WAV names must begin with 'vo_codex_': {sample.FileName}");
            return fileBaseName;
        }

        Match match = SoundsetFileNameRegex().Match(fileBaseName);
        if (!match.Success)
            throw new InvalidDataException(
                $"Soundset WAV names must end in a supported three-character cue code and two-digit index: {sample.FileName}");
        if (match.Groups["racialVariant"].Success)
            return $"VO_SpecialAbilityRacial{match.Groups["racialVariant"].Value}_{match.Groups["index"].Value}";

        string code = match.Groups["code"].Value;
        if (!SoundsetEventNames.TryGetValue(code, out string eventName))
            throw new InvalidDataException($"Unknown soundset cue code '{code}' in {sample.FileName}.");
        return $"VO_{eventName}_{match.Groups["index"].Value}";
    }

    private static void ValidateLineInputs(IEnumerable<WaveSample> samples)
    {
        foreach (var group in samples.GroupBy(sample => sample.LineId))
        {
            foreach (var genderGroup in group.GroupBy(sample => sample.Gender))
            {
                if (genderGroup.Count() > 1)
                {
                    string files = string.Join(", ", genderGroup.Select(sample => sample.FileName));
                    throw new InvalidDataException(
                        $"Line {group.Key} has multiple {genderGroup.Key} WAV files: {files}");
                }
            }
        }
    }

    private static WaveSample ReadWaveSample(string path, AuthoringMode authoringMode)
    {
        string fileName = Path.GetFileName(path);
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (nameWithoutExtension.Any(char.IsWhiteSpace))
            throw new InvalidDataException($"ISACT WAV names cannot contain spaces: {fileName}");
        ulong lineId = 0;
        DialogueGender gender = DialogueGender.Shared;
        if (authoringMode == AuthoringMode.Conversation)
        {
            Match match = DialogueFileNameRegex().Match(nameWithoutExtension);
            if (!match.Success || !ulong.TryParse(match.Groups["id"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out lineId))
            {
                throw new InvalidDataException(
                    $"Conversation WAV names must end in a numeric string reference, optionally followed by _F or _M: {fileName}");
            }

            gender = match.Groups["gender"].Value.ToUpperInvariant() switch
            {
                "F" => DialogueGender.Female,
                "M" => DialogueGender.Male,
                _ => DialogueGender.Shared
            };
        }

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (ReadFourCC(reader) != "RIFF")
            throw new InvalidDataException($"WAV does not begin with RIFF: {fileName}");
        _ = reader.ReadUInt32();
        if (ReadFourCC(reader) != "WAVE")
            throw new InvalidDataException($"RIFF file is not WAVE audio: {fileName}");

        ushort format = 0;
        ushort channels = 0;
        int sampleRate = 0;
        ushort blockAlign = 0;
        ushort bitsPerSample = 0;
        byte[] pcmData = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string chunkName = ReadFourCC(reader);
            uint chunkSize = reader.ReadUInt32();
            long chunkEnd = checked(stream.Position + chunkSize);
            if (chunkEnd > stream.Length)
                throw new InvalidDataException($"WAV contains a truncated {chunkName} chunk: {fileName}");

            if (chunkName == "fmt ")
            {
                if (chunkSize < 16)
                    throw new InvalidDataException($"WAV fmt chunk is too short: {fileName}");
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = checked((int)reader.ReadUInt32());
                _ = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkName == "data")
            {
                if (chunkSize > int.MaxValue)
                    throw new InvalidDataException($"WAV data is too large: {fileName}");
                pcmData = reader.ReadBytes((int)chunkSize);
                if (pcmData.Length != chunkSize)
                    throw new InvalidDataException($"WAV contains truncated PCM data: {fileName}");
            }

            long nextChunk = checked(chunkEnd + (chunkSize & 1));
            if (nextChunk > stream.Length)
                throw new InvalidDataException($"WAV contains truncated padding after {chunkName}: {fileName}");
            stream.Position = nextChunk;
        }

        if (format != 1)
            throw new InvalidDataException($"Only uncompressed PCM WAV files are supported: {fileName}");
        if (bitsPerSample != 16)
            throw new InvalidDataException($"ISACT authoring input must be signed 16-bit PCM: {fileName}");
        if (channels is not (1 or 2))
            throw new InvalidDataException($"ISACT dialogue input must be mono or stereo: {fileName}");
        if (channels == 0 || sampleRate <= 0 || bitsPerSample == 0 || blockAlign == 0)
            throw new InvalidDataException($"WAV has invalid format metadata: {fileName}");
        if (pcmData is null)
            throw new InvalidDataException($"WAV has no data chunk: {fileName}");
        if (pcmData.Length % blockAlign != 0)
            throw new InvalidDataException($"WAV data is not aligned to complete sample frames: {fileName}");
        if (fileName.Length > 127)
            throw new InvalidDataException($"ISACT sample names cannot exceed 127 characters: {fileName}");

        return new WaveSample(path, fileName, lineId, gender, sampleRate, bitsPerSample, channels, blockAlign, pcmData);
    }

    private static void ValidateBankName(string bankName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);
        if (bankName != Path.GetFileName(bankName) || bankName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Bank name must be a valid filename without a path.", nameof(bankName));
        if (Path.HasExtension(bankName))
            throw new ArgumentException("Bank name must not include a file extension.", nameof(bankName));
    }

    private static int GetChannelFlags(ushort channels) => channels switch
    {
        1 => 0x4, // Front centre
        2 => 0x3, // Front left + front right
        _ => 0
    };

    private static string ReadFourCC(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));

    private static string ResolveBankBuilderExecutable(string path)
    {
        string executable = Directory.Exists(path) ? Path.Combine(path, "BankBuilder.exe") : path;
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable))
            throw new FileNotFoundException("BankBuilder.exe was not found.", executable);
        return executable;
    }

    private static string ResolveBankBuilderDependency(string bankBuilderExe, string dependencyName)
    {
        var searchDirectory = new DirectoryInfo(Path.GetDirectoryName(bankBuilderExe)!);
        for (int level = 0; searchDirectory is not null && level < 8; level++, searchDirectory = searchDirectory.Parent)
        {
            string candidate = Path.Combine(searchDirectory.FullName, dependencyName);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException(
            $"{dependencyName} was not found beside BankBuilder.exe or in one of its parent directories.",
            dependencyName);
    }

    private static bool ContainsBuilderFailure(string log)
    {
        foreach (string line in log.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Regex.IsMatch(line, @"\b(warnings?|errors?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;
            if (Regex.IsMatch(line, @"\b(no|0)\s+(warnings?|errors?)\b|\b(warnings?|errors?)\s*:\s*0\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;
            return true;
        }
        return false;
    }

    private static bool HasBuilderCompletionSummary(string log) =>
        Regex.IsMatch(log, @"(?m)^\s*\d+\s+Errors?\s+and\s+\d+\s+Warnings?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string TryReadBuilderLog(string path)
    {
        if (!File.Exists(path))
            return string.Empty;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static void ValidateBuiltBanks(
        string icbPath, string isbPath, int expectedEventCount, bool expectedSoundQueue)
    {
        if (!File.Exists(icbPath))
            throw new InvalidDataException($"BankBuilder did not produce the expected ICB: {icbPath}");
        if (!File.Exists(isbPath))
            throw new InvalidDataException($"BankBuilder did not produce the expected ISB: {isbPath}");

        using var icbStream = File.OpenRead(icbPath);
        var icb = new ISACTBank(icbStream);
        if (icb.BankType != ISACTBankType.ICB)
            throw new InvalidDataException("BankBuilder output ICB has the wrong bank type.");
        int eventCount = icb.BankChunks.OfType<ISACTListBankChunk>().Count(list => list.ObjectType == "snde");
        if (eventCount != expectedEventCount)
            throw new InvalidDataException($"BankBuilder output contains {eventCount} events; expected {expectedEventCount}.");
        int queueCount = icb.BankChunks.OfType<ISACTListBankChunk>().Count(list => list.ObjectType == "sdqu");
        if (expectedSoundQueue && queueCount != 1)
            throw new InvalidDataException($"BankBuilder output contains {queueCount} Sound Queues; expected one.");

        ValidateFinalIsb(isbPath);
    }

    private static void ValidateFinalIsb(string isbPath)
    {
        using var isbStream = File.OpenRead(isbPath);
        var isb = new ISACTBank(isbStream);
        if (isb.BankType != ISACTBankType.ISB)
            throw new InvalidDataException("BankBuilder output ISB has the wrong bank type.");
        var sampleLists = isb.BankChunks.OfType<ISACTListBankChunk>().Where(list => list.ObjectType == "samp").ToList();
        if (sampleLists.Count == 0)
            throw new InvalidDataException("BankBuilder output ISB contains no samples.");
        foreach (var sample in sampleLists)
        {
            if (sample.CompressionInfo.CurrentFormat != CompressionInfoBankChunk.ISACTCompressionFormat.OGGVORBIS)
                throw new InvalidDataException($"BankBuilder did not encode sample {sample.TitleInfo?.Value} as Ogg Vorbis.");
            if (sample.SampleData is not { Length: >= 4 } data || !data.AsSpan(0, 4).SequenceEqual("OggS"u8))
                throw new InvalidDataException($"BankBuilder output sample {sample.TitleInfo?.Value} has invalid Ogg data.");
        }
    }

    private static BankChunk IntChunk(string name, int value) => RawInt32Chunk(name, value);

    private static BankChunk FloatChunk(string name, float value) =>
        RawInt32Chunk(name, BitConverter.SingleToInt32Bits(value));

    private static BankChunk RawInt32Chunk(string name, params int[] values)
    {
        byte[] data = new byte[values.Length * sizeof(int)];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(index * sizeof(int)), values[index]);
        return RawChunk(name, data);
    }

    private static BankChunk UnicodeChunk(string name, string value) =>
        RawChunk(name, Encoding.Unicode.GetBytes(value + '\0'));

    private static BankChunk RawChunk(string name, byte[] data) => new()
    {
        ChunkName = name,
        RawData = data
    };

    [GeneratedRegex(@"(?<id>\d+)(?:_(?<gender>[FM]))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DialogueFileNameRegex();

    [GeneratedRegex(@"_(?:sb(?<racialVariant>\d)|(?<code>[A-Za-z]{3}))(?<index>\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SoundsetFileNameRegex();
}
