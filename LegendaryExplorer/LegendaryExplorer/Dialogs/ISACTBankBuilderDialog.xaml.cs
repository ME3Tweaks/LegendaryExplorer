using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;

namespace LegendaryExplorer.Dialogs;

/// <summary>
/// Selects whether the dialog creates a bank pair or appends to an existing pair.
/// </summary>
public enum ISACTBankBuildMode
{
    /// <summary>Create a new ICB and ISB from source WAV files.</summary>
    Build,
    /// <summary>Append newly compiled content to an existing ICB and ISB.</summary>
    Rebuild
}

/// <summary>
/// Coordinates WAV preparation, ISACT bank compilation, and optional LE1 package installation.
/// </summary>
public partial class ISACTBankBuilderDialog : NotifyPropertyChangedWindowBase
{
    /// <summary>Identifies a BioSoundNodeWaveStreamingData export displayed in the package selector.</summary>
    private sealed record StreamingDataChoice(int UIndex, string DisplayName);

    /// <summary>Pairs an LE localization with the suffix used by LE1 ISACT object and bank names.</summary>
    private sealed record LocalizationChoice(MELocalization Localization, string DisplayName)
    {
        public string Suffix => Localization is MELocalization.None or MELocalization.INT
            ? string.Empty
            : $"_{Localization.ToLocaleString(MEGame.LE1)}";
    }

    /// <summary>Defines filename interpretation and user guidance for an authoring mode.</summary>
    private sealed record AuthoringModeChoice(
        ISACTBankBuilder.AuthoringMode Mode, string DisplayName, string WavTooltip);

    // ES and JA use INT audio in LE1 and therefore have no separate ISACT authoring target.
    private static readonly LocalizationChoice[] Localizations =
    [
        new(MELocalization.INT, "INT"), new(MELocalization.DEU, "DE"),
        new(MELocalization.FRA, "FR"), new(MELocalization.ITA, "IT"),
        new(MELocalization.POL, "PLPC"), new(MELocalization.RUS, "RA")
    ];
    // Each mode applies a different WAV filename-to-Sound Event naming rule.
    private static readonly AuthoringModeChoice[] AuthoringModes =
    [
        new(ISACTBankBuilder.AuthoringMode.Conversation, "BioConversation",
            "Names must end in a numeric string reference, optionally followed by _M or _F."),
        new(ISACTBankBuilder.AuthoringMode.Codex, "Codex",
            "Names use vo_codex_, optionally preceded by an LE1 audio locale such as EN_."),
        new(ISACTBankBuilder.AuthoringMode.Soundset, "Soundset",
            "Names end in a three-letter cue plus two digits; racial abilities use sb + variant + two digits, such as sb100."),
        new(ISACTBankBuilder.AuthoringMode.Music, "Music",
            "The mus_ bank name is required. Each filename becomes a Sound Event; looping is optional.")
    ];

    private readonly ISACTBankBuildMode _mode;

    /// <summary>
    /// Creates a bank-authoring dialog configured for a new build or an append operation.
    /// </summary>
    /// <param name="mode">Operation exposed by the dialog.</param>
    /// <param name="owner">Window that owns this dialog.</param>
    public ISACTBankBuilderDialog(ISACTBankBuildMode mode, Window owner)
    {
        _mode = mode;
        Owner = owner;
        InitializeComponent();
        LocalizationComboBox.ItemsSource = Localizations;
        LocalizationComboBox.SelectedIndex = 0;
        AuthoringModeComboBox.ItemsSource = AuthoringModes;
        AuthoringModeComboBox.SelectedIndex = 0;

        string defaultBuilder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "ISACT", "ISACT SDK", "Win", "Bin", "BankBuilder.exe");
        if (File.Exists(defaultBuilder)) BankBuilderBox.Text = defaultBuilder;

        // Build and rebuild share the form; only rebuild exposes existing-bank inputs.
        bool rebuild = mode == ISACTBankBuildMode.Rebuild;
        ExistingBankPanel.Visibility = rebuild ? Visibility.Visible : Visibility.Collapsed;
        ExistingIcbPanel.Visibility = rebuild ? Visibility.Visible : Visibility.Collapsed;
        BankNameBox.IsEnabled = !rebuild;
        RunButton.Content = rebuild ? "Rebuild" : "Build";
        Title = rebuild ? "Rebuild ISACT Banks" : "Build ISACT Banks";
        DescriptionText.Text = rebuild
            ? "Compile the new WAVs and append them to an existing final ICB/ISB pair. Existing compressed samples are not recompressed."
            : "Build new LE1 ICB/ISB banks from WAV files.";
        SetStreamingDataChoices([]);
    }

    /// <summary>
    /// Selects source WAVs and derives default output and bank names from their folder.
    /// </summary>
    private void BrowseWavFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = SelectFolder("Select the folder containing source WAV files");
        if (path is null) return;
        WavFolderBox.Text = path;
        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text)) OutputFolderBox.Text = Path.Combine(path, "output");
        if (_mode == ISACTBankBuildMode.Build && string.IsNullOrWhiteSpace(BankNameBox.Text))
            BankNameBox.Text = new DirectoryInfo(path).Name;
    }

    /// <summary>Selects the directory that receives the final ICB and ISB.</summary>
    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e) => SetFolder(OutputFolderBox, "Select the output folder");

    /// <summary>Selects the DLC Content directory that receives an optional ISB copy.</summary>
    private void BrowseDlcContent_Click(object sender, RoutedEventArgs e) => SetFolder(DlcContentBox, "Select DLC_MOD_Example/Content");

    /// <summary>Selects the optional content bank used by a rebuild.</summary>
    private void BrowseExistingIcb_Click(object sender, RoutedEventArgs e) =>
        SetFile(ExistingIcbBox, "ISACT Content Bank (*.icb)|*.icb", "Select the existing ICB");

    /// <summary>
    /// Selects the sample bank used by a rebuild and derives its bank name and localization.
    /// </summary>
    private void BrowseExistingIsb_Click(object sender, RoutedEventArgs e)
    {
        string path = SelectFile("ISACT Sample Bank (*.isb)|*.isb", "Select the existing ISB");
        if (path is null) return;
        ExistingIsbBox.Text = path;
        string isbName = Path.GetFileNameWithoutExtension(path);
        LocalizationChoice localization = Localizations.FirstOrDefault(choice =>
            choice.Suffix.Length > 0 && isbName.EndsWith(choice.Suffix, StringComparison.OrdinalIgnoreCase));
        if (localization is not null)
        {
            BankNameBox.Text = isbName[..^localization.Suffix.Length];
            LocalizationComboBox.SelectedItem = localization;
        }
        else
        {
            BankNameBox.Text = isbName;
            LocalizationComboBox.SelectedIndex = 0;
        }
        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text)) OutputFolderBox.Text = Path.GetDirectoryName(path);
    }

    /// <summary>Selects the external ISACT BankBuilder executable.</summary>
    private void BrowseBankBuilder_Click(object sender, RoutedEventArgs e) =>
        SetFile(BankBuilderBox, "BankBuilder.exe|BankBuilder.exe|Executable (*.exe)|*.exe", "Select BankBuilder.exe");

    /// <summary>
    /// Selects an LE1 package and loads its localization and streaming-data exports.
    /// </summary>
    private void BrowseDestinationPcc_Click(object sender, RoutedEventArgs e)
    {
        string path = SelectFile("Unreal package (*.pcc;*.upk)|*.pcc;*.upk", "Select the destination LE1 package");
        if (path is null) return;
        DestinationPccBox.Text = path;
        try
        {
            LoadStreamingDataChoices(path);
            SelectPackageLocalization(path);
        }
        catch (Exception exception)
        {
            SetStreamingDataChoices([]);
            System.Windows.MessageBox.Show(this, exception.Message, "Could not inspect package",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Validates the form, prepares WAV input, builds or appends banks, and installs optional outputs.
    /// </summary>
    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        string normalizedFolder = null;
        string extractedIcbDirectory = null;
        try
        {
            ValidateInputs();
            SetRunning(true, "Checking WAV input...");
            // Conversion uses a temporary folder so the author's source WAVs remain unchanged.
            bool allowNormalisation = NormalizeCheckBox.IsChecked == true;
            normalizedFolder = await Task.Run(() => PrepareWavInput(WavFolderBox.Text, allowNormalisation));

            int packetSize = int.Parse(PacketSizeBox.Text, CultureInfo.InvariantCulture);
            float quality = float.Parse(QualityBox.Text, CultureInfo.InvariantCulture);
            int selectedStreamingData = GetSelectedStreamingDataIndex();
            string inputIcbPath = ExistingIcbBox.Text;
            if (_mode == ISACTBankBuildMode.Rebuild && string.IsNullOrWhiteSpace(inputIcbPath))
            {
                // Rebuild can recover a discarded ICB from the selected package's stripped bank pair.
                extractedIcbDirectory = Path.Combine(Path.GetTempPath(), $"LEX_ISACTEmbeddedICB_{Guid.NewGuid():N}");
                Directory.CreateDirectory(extractedIcbDirectory);
                inputIcbPath = Path.Combine(extractedIcbDirectory, $"{BankNameBox.Text}.icb");
                ExtractEmbeddedIcb(DestinationPccBox.Text, selectedStreamingData, ExistingIsbBox.Text, inputIcbPath);
            }

            // Only newly supplied WAVs reach BankBuilder during an append operation.
            SetRunning(true, _mode == ISACTBankBuildMode.Build
                ? "Building ISACT banks..."
                : "Compiling and appending new ISACT content...");
            ISACTBankBuilder.FinalBankFiles result = _mode == ISACTBankBuildMode.Build
                ? await ISACTBankBuilder.BuildFinalBanksFromWavFolder(
                    normalizedFolder ?? WavFolderBox.Text, OutputFolderBox.Text, BankNameBox.Text,
                    BankBuilderBox.Text, packetSize, quality,
                    sampleBankName: BankNameBox.Text + GetLocalizationSuffix(),
                    authoringMode: GetAuthoringMode(),
                    createLoopingMusicQueue: LoopingMusicQueueCheckBox.IsChecked == true)
                : await ISACTBankBuilder.AppendFinalBanksFromWavFolder(
                    inputIcbPath, ExistingIsbBox.Text, normalizedFolder ?? WavFolderBox.Text,
                    OutputFolderBox.Text, BankBuilderBox.Text, packetSize, quality,
                    authoringMode: GetAuthoringMode());

            if (CopyIsbCheckBox.IsChecked == true)
            {
                // ISBs are external runtime files and may be installed directly into DLC Content.
                Directory.CreateDirectory(DlcContentBox.Text);
                File.Copy(result.ISBPath, Path.Combine(DlcContentBox.Text, Path.GetFileName(result.ISBPath)), true);
            }

            if (PackageIntegrationCheckBox.IsChecked == true)
            {
                // Existing exports retain their references; new exports are created in the LE1 streaming hierarchy.
                SetRunning(true, "Updating BioSoundNodeWaveStreamingData...");
                await Task.Run(() => InstallStreamingData(
                    DestinationPccBox.Text, selectedStreamingData, BankNameBox.Text,
                    GetLocalization(), result.ICBPath, result.ISBPath));
            }

            SetRunning(false, $"Created {result.EventMappings.Count} sound events.\n{result.ICBPath}\n{result.ISBPath}");
            System.Windows.MessageBox.Show(this, "ISACT bank operation completed successfully.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            SetRunning(false, exception.Message);
            System.Windows.MessageBox.Show(this, exception.Message, "ISACT bank operation failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DeleteTemporaryDirectory(normalizedFolder);
            DeleteTemporaryDirectory(extractedIcbDirectory);
        }
    }

    /// <summary>
    /// Validates fields required by the selected operation and optional installation steps.
    /// </summary>
    private void ValidateInputs()
    {
        if (!Directory.Exists(WavFolderBox.Text)) throw new DirectoryNotFoundException("Select a valid WAV source folder.");
        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text)) throw new InvalidDataException("Select an output folder.");
        if (string.IsNullOrWhiteSpace(BankNameBox.Text)) throw new InvalidDataException("Enter a bank name.");
        if (!File.Exists(BankBuilderBox.Text)) throw new FileNotFoundException("Select BankBuilder.exe.", BankBuilderBox.Text);
        if (!int.TryParse(PacketSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packet) || packet < 0)
            throw new InvalidDataException("Stream packet size must be a non-negative integer.");
        if (!float.TryParse(QualityBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float quality) || quality is < 0 or > 1)
            throw new InvalidDataException("Vorbis quality must be between 0 and 1.");
        if (_mode == ISACTBankBuildMode.Rebuild && !File.Exists(ExistingIsbBox.Text))
            throw new FileNotFoundException("Select an existing ISB.");
        if (_mode == ISACTBankBuildMode.Rebuild && LoopingMusicQueueCheckBox.IsChecked == true)
            throw new InvalidDataException("Appending to an existing music queue is not yet supported.");
        if (CopyIsbCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(DlcContentBox.Text))
            throw new InvalidDataException("Select the DLC Content folder.");

        // Rebuild without an external ICB must recover the embedded content bank from a package.
        bool packageNeeded = PackageIntegrationCheckBox.IsChecked == true ||
                             (_mode == ISACTBankBuildMode.Rebuild && string.IsNullOrWhiteSpace(ExistingIcbBox.Text));
        if (packageNeeded && !File.Exists(DestinationPccBox.Text))
            throw new FileNotFoundException("Select the package containing the target BioStreamingData.");
        if (packageNeeded && GetPackageLocalization(DestinationPccBox.Text) != GetLocalization())
            throw new InvalidDataException(
                "The selected localisation does not match the destination LOC package.");
        if (packageNeeded && _mode == ISACTBankBuildMode.Rebuild && GetSelectedStreamingDataIndex() <= 0)
            throw new InvalidDataException("Select the existing BioStreamingData export to rebuild.");
        if (_mode == ISACTBankBuildMode.Rebuild && !string.IsNullOrWhiteSpace(ExistingIcbBox.Text) && !File.Exists(ExistingIcbBox.Text))
            throw new FileNotFoundException("The optional existing ICB could not be found.", ExistingIcbBox.Text);
    }

    /// <summary>
    /// Verifies WAV channel and sample formats and optionally creates temporary PCM16 copies.
    /// </summary>
    /// <returns>A temporary converted directory, or <see langword="null"/> when conversion is unnecessary.</returns>
    private static string PrepareWavInput(string sourceFolder, bool allowNormalisation)
    {
        string[] files = Directory.GetFiles(sourceFolder, "*.wav", SearchOption.TopDirectoryOnly);
        if (files.Length == 0) throw new InvalidDataException("The selected folder contains no WAV files.");

        bool requiresConversion = false;
        foreach (string file in files)
        {
            using var reader = new WaveFileReader(file);
            if (reader.WaveFormat.Channels is not (1 or 2))
                throw new InvalidDataException($"ISACT audio must be mono or stereo: {Path.GetFileName(file)}");
            requiresConversion |= reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.BitsPerSample != 16;
        }
        if (!requiresConversion) return null;
        if (!allowNormalisation)
            throw new InvalidDataException("One or more WAV files are not signed 16-bit PCM. Enable automatic conversion or clean the sources first.");

        // Converted copies preserve source filenames because event naming is filename-driven.
        string convertedFolder = Path.Combine(Path.GetTempPath(), $"LEX_ISACT_PCM16_{Guid.NewGuid():N}");
        Directory.CreateDirectory(convertedFolder);
        foreach (string file in files)
        {
            string destination = Path.Combine(convertedFolder, Path.GetFileName(file));
            using var reader = new WaveFileReader(file);
            if (reader.WaveFormat.Encoding == WaveFormatEncoding.Pcm && reader.WaveFormat.BitsPerSample == 16)
                File.Copy(file, destination);
            else
                WaveFileWriter.CreateWaveFile16(destination, reader.ToSampleProvider());
        }
        return convertedFolder;
    }

    /// <summary>
    /// Loads available BioSoundNodeWaveStreamingData destinations from an LE1 package.
    /// </summary>
    private void LoadStreamingDataChoices(string packagePath)
    {
        using IMEPackage package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
        if (package.Game != MEGame.LE1) throw new InvalidDataException("The selected package is not an LE1 package.");
        var choices = new List<StreamingDataChoice>();
        if (_mode == ISACTBankBuildMode.Build) choices.Add(new StreamingDataChoice(0, "<Create new BioStreamingData>"));
        choices.AddRange(package.Exports.Where(export => export.ClassName == "BioSoundNodeWaveStreamingData")
            .Select(export => new StreamingDataChoice(export.UIndex, DescribeStreamingData(export))));
        SetStreamingDataChoices(choices);
    }

    /// <summary>Selects the authoring localization inferred from the destination package name.</summary>
    private void SelectPackageLocalization(string packagePath)
    {
        MELocalization localization = GetPackageLocalization(packagePath);
        LocalizationComboBox.SelectedItem = Localizations.Single(choice => choice.Localization == localization);
    }

    /// <summary>Returns the localization selected for generated object and sample-bank names.</summary>
    private MELocalization GetLocalization() =>
        (LocalizationComboBox.SelectedItem as LocalizationChoice)?.Localization ?? MELocalization.INT;

    /// <summary>Returns the LE1 suffix used by the selected audio localization.</summary>
    private string GetLocalizationSuffix() =>
        (LocalizationComboBox.SelectedItem as LocalizationChoice)?.Suffix ?? "";

    /// <summary>Returns the filename interpretation selected for source WAVs.</summary>
    private ISACTBankBuilder.AuthoringMode GetAuthoringMode() =>
        (AuthoringModeComboBox.SelectedItem as AuthoringModeChoice)?.Mode
        ?? ISACTBankBuilder.AuthoringMode.Conversation;

    /// <summary>
    /// Updates filename guidance and music-specific options when the authoring mode changes.
    /// </summary>
    private void AuthoringModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WavFolderBox is null || AuthoringModeComboBox.SelectedItem is not AuthoringModeChoice choice) return;
        WavFolderBox.ToolTip = choice.WavTooltip;
        LoopingMusicQueueCheckBox.Visibility = choice.Mode == ISACTBankBuilder.AuthoringMode.Music
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (choice.Mode != ISACTBankBuilder.AuthoringMode.Music)
            LoopingMusicQueueCheckBox.IsChecked = false;
        // Shipped LE1 music commonly uses 2000 ms packets instead of the dialogue-oriented default.
        if (choice.Mode == ISACTBankBuilder.AuthoringMode.Music && PacketSizeBox?.Text == "2500")
            PacketSizeBox.Text = "2000";
    }

    /// <summary>Infers an LE localization from a package filename and treats unlocalized files as INT.</summary>
    private static MELocalization GetPackageLocalization(string packagePath)
    {
        MELocalization localization = Path.GetFileNameWithoutExtension(packagePath).GetUnrealLocalization();
        return localization == MELocalization.None ? MELocalization.INT : localization;
    }

    /// <summary>Builds a selector label from an export path and its embedded content-bank title.</summary>
    private static string DescribeStreamingData(ExportEntry export)
    {
        string bankTitle = null;
        try
        {
            bankTitle = export.GetBinaryData<BioSoundNodeWaveStreamingData>().BankPair.ICBBank.BankChunks
                .OfType<TitleBankChunk>().FirstOrDefault()?.Value;
        }
        // Malformed exports remain selectable so validation can report the underlying bank error later.
        catch { }
        return bankTitle is null
            ? $"#{export.UIndex} {export.InstancedFullPath}"
            : $"#{export.UIndex} {export.InstancedFullPath} - {bankTitle}";
    }

    /// <summary>Replaces and initializes the available streaming-data destinations.</summary>
    private void SetStreamingDataChoices(IReadOnlyCollection<StreamingDataChoice> choices)
    {
        StreamingDataComboBox.ItemsSource = choices;
        StreamingDataComboBox.SelectedIndex = choices.Count > 0 ? 0 : -1;
    }

    /// <summary>Returns the selected streaming-data UIndex, or -1 when no destination is selected.</summary>
    private int GetSelectedStreamingDataIndex() =>
        StreamingDataComboBox.SelectedItem is StreamingDataChoice choice ? choice.UIndex : -1;

    /// <summary>
    /// Recovers an embedded ICB after confirming the selected external ISB belongs to the same bank pair.
    /// </summary>
    private static void ExtractEmbeddedIcb(
        string packagePath, int streamingDataUIndex, string existingIsbPath, string outputPath)
    {
        using IMEPackage package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
        ExportEntry streamingData = GetStreamingDataExport(package, streamingDataUIndex);
        string embeddedIsbTitle = streamingData.GetBinaryData<BioSoundNodeWaveStreamingData>().BankPair.ISBBank.BankChunks
            .OfType<TitleBankChunk>().FirstOrDefault()?.Value;
        string externalIsbTitle;
        using (var stream = File.OpenRead(existingIsbPath))
            externalIsbTitle = new ISACTBank(stream).BankChunks.OfType<TitleBankChunk>().FirstOrDefault()?.Value;
        if (!string.Equals(embeddedIsbTitle, externalIsbTitle, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"The selected BioStreamingData references '{embeddedIsbTitle}', but the existing ISB is '{externalIsbTitle}'.");
        ISACTHelper.ExportStreamingDataContentBank(streamingData, outputPath);
    }

    /// <summary>
    /// Updates a selected export or creates one under the localized DVDStreamingAudioData package.
    /// </summary>
    private static void InstallStreamingData(
        string destinationPath, int streamingDataUIndex, string bankName, MELocalization localization,
        string icbPath, string isbPath)
    {
        using IMEPackage destination = MEPackageHandler.OpenMEPackage(destinationPath, forceLoadFromDisk: true);
        if (destination.Game != MEGame.LE1)
            throw new InvalidDataException("BioSoundNodeWaveStreamingData generation is only supported for LE1 packages.");

        if (streamingDataUIndex > 0)
            ISACTHelper.GenerateSoundNodeWaveStreamingDataCS(
                GetStreamingDataExport(destination, streamingDataUIndex), icbPath, isbPath);
        else
            ISACTHelper.CreateSoundNodeWaveStreamingData(
                destination, bankName, icbPath, isbPath, localization);
        destination.Save();
    }

    /// <summary>Resolves and validates a BioSoundNodeWaveStreamingData export by UIndex.</summary>
    private static ExportEntry GetStreamingDataExport(IMEPackage package, int uIndex)
    {
        if (!package.TryGetUExport(uIndex, out ExportEntry export) || export.ClassName != "BioSoundNodeWaveStreamingData")
            throw new InvalidDataException($"Export #{uIndex} is not BioSoundNodeWaveStreamingData.");
        return export;
    }

    /// <summary>Updates command availability, progress visibility, and operation status.</summary>
    private void SetRunning(bool running, string status)
    {
        RunButton.IsEnabled = !running;
        Progress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = status;
    }

    /// <summary>Closes the dialog.</summary>
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Displays an owned folder picker and returns the selected path.</summary>
    private string SelectFolder(string title)
    {
        var dialog = new CommonOpenFileDialog(title) { IsFolderPicker = true };
        return dialog.ShowDialog(this) == CommonFileDialogResult.Ok ? dialog.FileName : null;
    }

    /// <summary>Displays a file picker and returns the selected path.</summary>
    private static string SelectFile(string filter, string title)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Copies a selected folder path into a form field.</summary>
    private void SetFolder(System.Windows.Controls.TextBox target, string title)
    {
        string path = SelectFolder(title);
        if (path is not null) target.Text = path;
    }

    /// <summary>Copies a selected file path into a form field.</summary>
    private static void SetFile(System.Windows.Controls.TextBox target, string filter, string title)
    {
        string path = SelectFile(filter, title);
        if (path is not null) target.Text = path;
    }

    /// <summary>Best-effort cleanup for temporary WAV and extracted-bank directories.</summary>
    private static void DeleteTemporaryDirectory(string path)
    {
        if (path is null || !Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
