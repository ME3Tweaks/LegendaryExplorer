using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for ParticleSystemExportLoader.xaml
    /// </summary>
    public partial class AnimNodeExportLoader : ExportLoaderControl
    {
        public ObservableCollectionExtended<AnimNode> AnimNodes { get; } = new();

        public class AnimNode
        {
            public IEntry Entry { get; set; }
            public string Header { get; set; }
            public ObservableCollectionExtended<AnimNode> Children { get; } = new();
        }

        public ICommand OpenExportInPECommand { get; set; }

        public ICommand NavigateToEntryCommandInternal { get; set; }

        public RelayCommand NavigateToEntryCommand
        {
            get => (RelayCommand)GetValue(NavigateToEntryCallbackProperty);
            set => SetValue(NavigateToEntryCallbackProperty, value);
        }

        public static readonly DependencyProperty NavigateToEntryCallbackProperty = DependencyProperty.Register(
            "NavigateToEntryCommand", typeof(RelayCommand), typeof(AnimNodeExportLoader), new PropertyMetadata(null));

        public AnimNodeExportLoader() : base("AnimNode Tree Viewer")
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            NavigateToEntryCommandInternal = new RelayCommand(FireNavigateCallback, CanFireNavigateCallback);
            OpenExportInPECommand = new RelayCommand(OpenInPackageEditor, (o => o is int i && Pcc.IsEntry(i)));
        }

        private void FireNavigateCallback(object uIndex)
        {
            var entry = CurrentLoadedExport.FileRef.GetEntry((int)uIndex);
            NavigateToEntryCommand?.Execute(entry);
        }

        private bool CanFireNavigateCallback(object uIndex)
        {
            if (CurrentLoadedExport != null && NavigateToEntryCommand != null && uIndex is int index)
            {
                var entry = CurrentLoadedExport.FileRef.GetEntry(index);
                return NavigateToEntryCommand.CanExecute(entry);
            }

            return false;
        }

        private void OpenInPackageEditor(object parameter)
        {
            if (parameter is int index)
            {
                var p = new PackageEditorWindow();
                p.Show();
                p.LoadFile(CurrentLoadedExport.FileRef.FilePath, index);
                p.Activate(); //bring to front
            }
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            !exportEntry.IsDefaultObject && exportEntry.IsA("AnimNode");

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            AnimNodes.ClearEx();
            AnimNodes.Add(GenerateNode(exportEntry));
        }

        private AnimNode GenerateNode(ExportEntry exportEntry)
        {
            var node = new AnimNode()
            {
                Entry = exportEntry
            };

            var props = exportEntry.GetProperties();
            var className = exportEntry.ClassName;
            var nodeType = className.Contains("AnimNode") ? className.Substring(className.IndexOf("AnimNode") + 8) : className;
            var name = props.GetProp<NameProperty>("NodeName") ?? props.GetProp<NameProperty>("AnimSeqName");
            node.Header = $"{nodeType} {exportEntry.UIndex}: {name?.Value.Instanced ?? exportEntry.ObjectName.Instanced}";

            var blendChildren = props.GetProp<ArrayProperty<StructProperty>>("Children");
            if(blendChildren is not null)
            {
                foreach (var sp in blendChildren.Values)
                {
                    var child = sp.Properties.GetProp<ObjectProperty>("Anim");
                    if (child is not null && exportEntry.FileRef.IsUExport(child.Value))
                    {
                        node.Children.Add(GenerateNode((ExportEntry) child.ResolveToEntry(exportEntry.FileRef)));
                    }
                }
            }

            return node;
        }

        public override void UnloadExport()
        {
            AnimNodes.ClearEx();
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new ParticleSystemExportLoader(), CurrentLoadedExport)
                {
                    Title = $"AnimNode Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
        }
    }
}
