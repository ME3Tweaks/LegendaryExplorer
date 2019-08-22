using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.DXGI.Device;

namespace ME3Explorer.Meshplorer
{
    /// <summary>
    /// Interaction logic for MeshRendererWPF.xaml
    /// </summary>
    public partial class MeshRendererWPF : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "SkeletalMesh", "StaticMesh" };

        #region 3D

        private bool _rotating, _wireframe, _solid = true, _firstperson;

        public bool Rotating
        {
            get => _rotating;
            set => SetProperty(ref _rotating, value);
        }

        public bool Wireframe
        {
            get => _wireframe;
            set => SetProperty(ref _wireframe, value);
        }

        public bool Solid
        {
            get => _solid;
            set => SetProperty(ref _solid, value);
        }

        public bool FirstPerson
        {
            get => _firstperson;
            set => SetProperty(ref _firstperson, value);
        }

        private ModelPreview Preview;
        private int CurrentLOD = 0;
        private float PreviewRotation = 0.0f;

        private void SceneViewer_Render(object sender, EventArgs e)
        {
            if (Preview != null && Preview.LODs.Count > 0)
            {
               
                if (Solid && CurrentLOD < Preview.LODs.Count)
                {
                    SceneViewer.Context.Wireframe = false;
                    Preview.Render(SceneViewer.Context, CurrentLOD, SharpDX.Matrix.RotationY(PreviewRotation));
                }
                if (Wireframe)
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(SharpDX.Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), SharpDX.Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), SharpDX.Matrix.Transpose(SharpDX.Matrix.RotationY(PreviewRotation)));
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, Preview.LODs[CurrentLOD].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }
            }
        }

        private void CenterView()
        {
            if (Preview != null && Preview.LODs.Count > 0)
            {
                WorldMesh m = Preview.LODs[CurrentLOD].Mesh;
                SceneViewer.Context.Camera.Position = m.AABBCenter;
                //SceneViewer.Context.Camera.FocusDepth = 1.0f;
                if (SceneViewer.Context.Camera.FirstPerson)
                {
                    SceneViewer.Context.Camera.Position -= SceneViewer.Context.Camera.CameraForward * SceneViewer.Context.Camera.FocusDepth;
                }

                Debug.WriteLine("Camera position: " + SceneViewer.Context.Camera.Position);
            }
            else
            {
                SceneViewer.Context.Camera.Position = SharpDX.Vector3.Zero;
                SceneViewer.Context.Camera.Pitch = -(float)Math.PI / 5.0f;
                SceneViewer.Context.Camera.Yaw = (float)Math.PI / 4.0f;
                Debug.WriteLine("Camera position: " + SceneViewer.Context.Camera.Position);
            }
        }
        #endregion

        public MeshRendererWPF()
        {
            DataContext = this;
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith(("Default__"));
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            SceneViewer.InitializeD3D();
            CurrentLoadedExport = exportEntry;

            Preview?.Dispose();
            if (CurrentLoadedExport.ClassName == "StaticMesh")
            {
                var sm = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                //globalscale = 1f;
                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, sm, CurrentLOD, SceneViewer.Context.TextureCache);
                //var bounding = sm.Bounds.SphereRadius;
                //SceneViewer.Context.Camera.Position = new Vector3(bounding / 2, bounding / 2, bounding / 3);
                SceneViewer.Context.Camera.FocusDepth = sm.Bounds.SphereRadius *2.5f;

            }
            else if (CurrentLoadedExport.ClassName == "SkeletalMesh")
            {
                var sm = new Unreal.Classes.SkeletalMesh(CurrentLoadedExport);
                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, sm, SceneViewer.Context.TextureCache);
                SceneViewer.Context.Camera.FocusDepth = sm.Bounding.r * 2.5f;

                //var bounding = sm.Bounding.r;
                //SceneViewer.Context.Camera.Position = new Vector3(bounding / 2, bounding / 2, bounding / 3);
            }

            //It seems like globalscale in this tool is not the same as main meshplorer and it's causing things to not be focused. 
            //need to find way to make item in view by default.
            CenterView();
        }

        private void MeshRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            SceneViewer.Context.Update += MeshRenderer_ViewUpdate;
        }

        private void MeshRenderer_ViewUpdate(object sender, float e)
        {
            if (Rotating)
            {
                PreviewRotation += .05f * e;
            }
        }

        private void BackgroundColorPicker_Changed(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            SceneViewer.Context.BackgroundColor = new SharpDX.Color(Background_ColorPicker.SelectedColor.Value.R, Background_ColorPicker.SelectedColor.Value.G, Background_ColorPicker.SelectedColor.Value.B);
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            Preview?.Dispose();
            CurrentLoadedExport = null;
            SceneViewer.Context.Update -= MeshRenderer_ViewUpdate;
        }
    }
}
