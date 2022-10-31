using System.Windows;
using System.Numerics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for CollectionActorEditorExportLoader.xaml
    /// </summary>
    public partial class CollectionActorEditorExportLoader : ExportLoaderControl
    {
        #region Properties

        private float _posX;
        public float PosX
        {
            get => _posX;
            set => SetProperty(ref _posX, value);
        }

        private float _posY;
        public float PosY
        {
            get => _posY;
            set => SetProperty(ref _posY, value);
        }

        private float _posZ;
        public float PosZ
        {
            get => _posZ;
            set => SetProperty(ref _posZ, value);
        }

        private float _scaleX;
        public float ScaleX
        {
            get => _scaleX;
            set => SetProperty(ref _scaleX, value);
        }

        private float _scaleY;
        public float ScaleY
        {
            get => _scaleY;
            set => SetProperty(ref _scaleY, value);
        }

        private float _scaleZ;
        public float ScaleZ
        {
            get => _scaleZ;
            set => SetProperty(ref _scaleZ, value);
        }

        private int _uUPitch;
        public int UUPitch
        {
            get => _uUPitch;
            set => SetProperty(ref _uUPitch, value);
        }

        private int _uUYaw;
        public int UUYaw
        {
            get => _uUYaw;
            set => SetProperty(ref _uUYaw, value);
        }

        private int _uURoll;
        public int UURoll
        {
            get => _uURoll;
            set => SetProperty(ref _uURoll, value);
        }

        #endregion

        public CollectionActorEditorExportLoader() : base("CollectionActorEditor")
        {
            DataContext = this;
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return exportEntry.HasParent && 
                   (exportEntry.IsA("StaticMeshComponent") && exportEntry.Parent.IsA("StaticMeshCollectionActor") || 
                    exportEntry.IsA("LightComponent") && exportEntry.Parent.IsA("StaticLightCollectionActor") ) ;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(CurrentLoadedExport, out StaticCollectionActor sca, out int index))
            {
                ((PosX, PosY, PosZ), (ScaleX, ScaleY, ScaleZ), (UUPitch, UUYaw, UURoll)) = sca.GetDecomposedTransformationForIndex(index);
                titleLabel.Content = $"Edit this {CurrentLoadedExport.ClassName}'s transformation matrix\n(contained in #{sca.Export.UIndex} {sca.Export.ObjectName.Instanced})";
                editControlsPanel.Visibility = Visibility.Visible;
                errorLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                errorLabel.Content = "Parent StaticCollectionActor is malformed!";
                editControlsPanel.Visibility = Visibility.Collapsed;
                errorLabel.Visibility = Visibility.Visible;
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new CollectionActorEditorExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Collection Actor Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            UnloadExport();
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(CurrentLoadedExport, out StaticCollectionActor sca, out int index))
            {
                var location = new Vector3(PosX, PosY, PosZ);
                var rotation = new Rotator(UUPitch, UUYaw, UURoll);
                var scale = new Vector3(ScaleX, ScaleY, ScaleZ);
                sca.UpdateTransformationForIndex(index, location, scale, rotation);
                sca.Export.WriteBinary(sca);
            }
            else
            {
                MessageBox.Show("Unable to save changes! Parent StaticCollectionActor is malformed!");
            }
        }
    }
}
