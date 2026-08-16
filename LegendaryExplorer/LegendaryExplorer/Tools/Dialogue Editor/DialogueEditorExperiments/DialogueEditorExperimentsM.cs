using LegendaryExplorer.Dialogs;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryExplorer.Tools.Dialogue_Editor.DialogueEditorExperiments
{
    /// <summary>
    /// Mgamerz Experiments in Dialogue Editor.
    /// </summary>
    static class DialogueEditorExperimentsM
    {
        public static void AddSpeakerWithSharedFXAToAllConvos(WPFBase window)
        {
            if (window.Pcc == null)
            {
                return;
            }

            var conversations = window.Pcc.Exports.Where(e => e.ClassName == "BioConversation").ToList();

            if (conversations.Count == 0)
            {
                MessageBox.Show("This file doesn't contain any converations.");
                return;
            }

            var newTag = PromptDialog.Prompt(window, "Enter the tag of the speaker you want to add.", "Enter speaker tag");
            if (newTag == null)
            {
                return; // Nothing
            }

            var fxaM = EntrySelector.GetEntry<ExportEntry>(window, window.Pcc, "Select the male FXA to assign to the new speaker.", e => e is ExportEntry && e.ClassName == "FaceFXAnimSet"); ;
            if (fxaM == null)
            {
                return;
            }
            var fxaF = EntrySelector.GetEntry<ExportEntry>(window, window.Pcc, "Select the female FXA to assign to the new speaker.", e => e is ExportEntry && e.ClassName == "FaceFXAnimSet"); ;
            if (fxaF == null)
            {
                return;
            }

            foreach (var convo in conversations)
            {
                var bioconvo = convo.GetProperties();
                var speakerList = bioconvo.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                var fxaMs = bioconvo.GetProp<ArrayProperty<ObjectProperty>>("m_aMaleFaceSets");
                var fxaFs = bioconvo.GetProp<ArrayProperty<ObjectProperty>>("m_aFemaleFaceSets");

                speakerList.Add(new NameProperty(newTag));
                fxaMs.Add(new ObjectProperty(fxaM));
                fxaFs.Add(new ObjectProperty(fxaF));
                convo.WriteProperties(bioconvo);
            }

            MessageBox.Show("Done.");
        }

        public static void ExtractAllAudioFromSpeakerByTag(WPFBase window)
        {
            if (window.Pcc == null)
            {
                return;
            }

            if (window.Pcc.Game is not MEGame.LE2 and not MEGame.LE3)
            {
                MessageBox.Show("This feature only works on LE2 and LE3.");
                return;
            }

            // Prompt for speaker tag
            var speakerTag = PromptDialog.Prompt(window, "Enter the speaker tag to extract audio for.\nEnter 'player' to extract Shepard audio.\nEnter 'owner' to extract audio by conversation owner.", "Extract Speaker Audio");
            if (string.IsNullOrWhiteSpace(speakerTag))
            {
                return;
            }

            // Determine which genders to extract
            bool extractMale = true;
            bool extractFemale = true;

            if (speakerTag.Equals("player", StringComparison.OrdinalIgnoreCase))
            {
                var genderResult = MessageBox.Show(window as System.Windows.Window,
                    "Which audio files would you like to extract?\n\n" +
                    "Yes - Extract both male and female\n" +
                    "No - Extract only male\n" +
                    "Cancel - Extract only female",
                    "Select Genders",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                extractMale = genderResult != MessageBoxResult.Cancel;
                extractFemale = genderResult != MessageBoxResult.No;
            }

            // Prompt for output folder
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select Output Folder for Extracted Audio"
            };
            if (dlg.ShowDialog(window as System.Windows.Window) != CommonFileDialogResult.Ok)
            {
                return;
            }
            var outputFolder = dlg.FileName;

            // Prompt for dialogue text inclusion
            var includeText = MessageBox.Show(window as System.Windows.Window,
                "Include dialogue text in filenames?",
                "Filename Options",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

            var game = window.Pcc.Game;

            // Run extraction in background task with progress updates
            window.IsBusy = true;
            window.BusyText = "Preparing to extract audio...";
            Task.Run(() =>
            {
                var allFiles = MELoadedFiles.GetFilesLoadedInGame(game);
                int totalFiles = allFiles.Count;
                int totalExtracted = 0;
                int conversationsProcessed = 0;
                int filesProcessed = -1;

                var loc = window.Pcc.Localization is MELocalization.None ? MELocalization.INT : window.Pcc.Localization;

                foreach (var file in allFiles)
                {
                    filesProcessed++;

                    if (file.Key.GetUnrealLocalization() != loc)
                    {
                        continue;
                    }

                    try
                    {
                        // Update progress on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            window.BusyText = $"Scanning files [{filesProcessed + 1}/{totalFiles}]: {Path.GetFileName(file.Key)}";
                        });

                        using var package = MEPackageHandler.OpenMEPackage(file.Value);

                        var conversations = package.Exports.Where(e => e.ClassName == "BioConversation").ToList();

                        foreach (var convo in conversations)
                        {
                            try
                            {
                                // Parse conversation to get dialogue nodes
                                var convoData = new ConversationExtended(convo);
                                convoData.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

                                // Filter nodes by speaker tag
                                var isPlayerTag = speakerTag.Equals("player", StringComparison.OrdinalIgnoreCase);
                                var sourceList = isPlayerTag ? convoData.ReplyList : convoData.EntryList;
                                var speakerNodes = sourceList
                                    .Where(n => n.SpeakerTag?.SpeakerName?.Equals(speakerTag, StringComparison.OrdinalIgnoreCase) == true)
                                    .ToList();

                                if (speakerNodes.Any())
                                {
                                    // Update progress for this conversation
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        window.BusyText = $"Extracting audio from {convo.ObjectName.Name}...\n" +
                                                         $"Files: [{filesProcessed + 1}/{totalFiles}] | " +
                                                         $"Conversations: {conversationsProcessed + 1} | " +
                                                         $"Audio files: {totalExtracted}";
                                    });

                                    // Create subfolder for this conversation
                                    var convoFolder = Path.Combine(outputFolder, convo.ObjectName.Name);
                                    Directory.CreateDirectory(convoFolder);

                                    // Extract audio
                                    int extracted = DialogueEditorWindow.ExtractAudioFilesForSpeaker(
                                        speakerNodes, speakerTag, includeText, extractMale, extractFemale, convoFolder);

                                    totalExtracted += extracted;
                                    conversationsProcessed++;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log or skip problematic conversations
                                System.Diagnostics.Debug.WriteLine($"Error processing conversation {convo.ObjectName} in {file.Key}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log or skip problematic files
                        System.Diagnostics.Debug.WriteLine($"Error processing file {file.Key}: {ex.Message}");
                    }
                }

                return new { totalExtracted, conversationsProcessed, filesProcessed };
            }).ContinueWith(task =>
            {
                // Update UI on main thread when complete
                Application.Current.Dispatcher.Invoke(() =>
                {
                    window.IsBusy = false;

                    if (task.IsFaulted)
                    {
                        MessageBox.Show(window as Window,
                            $"Error during extraction:\n{task.Exception?.InnerException?.Message ?? task.Exception?.Message}",
                            "Extraction Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else
                    {
                        var result = task.Result;
                        MessageBox.Show(window as Window,
                            $"Extraction complete!\n" +
                            $"Files scanned: {result.filesProcessed}\n" +
                            $"Conversations processed: {result.conversationsProcessed}\n" +
                            $"Audio files extracted: {result.totalExtracted}",
                            "Extraction Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                });
            });
        }
    }
}
