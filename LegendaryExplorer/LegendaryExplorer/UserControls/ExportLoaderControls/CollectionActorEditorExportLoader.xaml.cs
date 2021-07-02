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
        private ExportEntry StaticCollectionActorExport;

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
            StaticCollectionActorExport =  exportEntry.Parent as ExportEntry;
            if (IsValidSCA(out StaticCollectionActor sca, out int index))
            {
                ((PosX, PosY, PosZ), (ScaleX, ScaleY, ScaleZ), (UUPitch, UUYaw, UURoll)) = sca.LocalToWorldTransforms[index].UnrealDecompose();
                titleLabel.Content = $"Edit this {CurrentLoadedExport.ClassName}'s transformation matrix\n(contained in #{StaticCollectionActorExport.UIndex} {StaticCollectionActorExport.ObjectName.Instanced})";
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

        private bool IsValidSCA(out StaticCollectionActor staticCollectionActor, out int i)
        {
            if (StaticCollectionActorExport != null && ObjectBinary.From(StaticCollectionActorExport) is StaticCollectionActor sca)
            {
                staticCollectionActor = sca;
                if (StaticCollectionActorExport.GetProperty<ArrayProperty<ObjectProperty>>(staticCollectionActor.ComponentPropName) is { } components)
                {
                    i = components.FindIndex(prop => prop.Value == CurrentLoadedExport.UIndex);
                    return i >= 0 && staticCollectionActor.LocalToWorldTransforms.Count > i;
                }
            }
            i = 0;
            staticCollectionActor = null;
            return false;

        }

        public override void UnloadExport()
        {
            StaticCollectionActorExport = null;
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
            if (IsValidSCA(out StaticCollectionActor sca, out int index))
            {
                Matrix4x4 m = ActorUtils.ComposeLocalToWorld(new Vector3(PosX, PosY, PosZ), 
                                                          new Rotator(UUPitch, UUYaw, UURoll), 
                                                          new Vector3(ScaleX, ScaleY, ScaleZ));
                sca.LocalToWorldTransforms[index] = m;
                StaticCollectionActorExport.WriteBinary(sca);
            }
            else
            {
                MessageBox.Show("Unable to save changes! Parent StaticCollectionActor is malformed!");
            }
        }
    }
}
