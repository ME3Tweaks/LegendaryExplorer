using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorer.Tools.PackageEditor
{
    /// <summary>
    /// Interaction logic for LECLDataEditorWindow.xaml
    /// </summary>
    public partial class LECLDataEditorWindow : WPFBase
    {
        /// <summary>
        /// If this file is determined to be a post load file heuristically
        /// </summary>
        public bool IsNotHeuristicPostLoad { get; set; }

        public LECLDataEditorWindow(Window window, IMEPackage pcc) : base("LECL Metadata Editor")
        {
            Owner = window;
            RegisterPackage(pcc);

            IsNotHeuristicPostLoad = !EntryImporter.IsPostLoadFile(pcc.FilePath, pcc.Game);

            // Merged pattern is too hard to read, use null accessor instead.
            if (pcc.LECLTagData?.ImportHintFiles != null)
            {
                ImportableFileHints.ReplaceAll(pcc.LECLTagData.ImportHintFiles);
            }
            Title += $" {pcc.FilePath}";
            InitializeComponent();
        }

        /// <summary>
        /// This is here as this is an editable list as the LECL data for it is not a collection type that supports
        /// collection notification
        /// </summary>
        public ObservableCollectionExtended<string> ImportableFileHints { get; } = new();

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            // This tool doesn't work with imports, names, or exports. Technically I guess LECL data could be 
            // synchronized but PackageUpdate would need changed.
        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var result = PromptDialog.Prompt(this, "Enter a package filename, including the extension.", "Add importable file hint");
            if (!string.IsNullOrWhiteSpace(result) && result.RepresentsPackageFilePath() && !ImportableFileHints.Contains(result, StringComparer.InvariantCultureIgnoreCase))
            {
                // Should we trim this?
                ImportableFileHints.Add(result);
                Pcc.LECLTagData.ImportHintFiles.Add(result);
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is string str)
            {
                ImportableFileHints.Remove(str);
                Pcc.LECLTagData.ImportHintFiles.Remove(str);
            }
        }
    }
}
