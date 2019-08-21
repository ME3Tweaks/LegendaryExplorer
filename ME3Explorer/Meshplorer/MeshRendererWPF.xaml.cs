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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;
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
        private ModelPreview Preview;
        private int CurrentLOD = 0;
        private float PreviewRotation = 0.0f;

        private void SceneViewer_Render(object sender, EventArgs e)
        {
            if (Preview != null && Preview.LODs.Count > 0)
            {
                //if (solidToolStripMenuItem.Checked && CurrentLOD < preview.LODs.Count) // TODO: Implement Solid and LOD options
                {
                    SceneViewer.Context.Wireframe = false;
                    Preview.Render(SceneViewer.Context, CurrentLOD, SharpDX.Matrix.RotationY(PreviewRotation));
                }
                //if (wireframeToolStripMenuItem.Checked) // TODO: Implement Wireframe option
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(SharpDX.Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), SharpDX.Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), SharpDX.Matrix.Transpose(SharpDX.Matrix.RotationY(PreviewRotation)));
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, Preview.LODs[CurrentLOD].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }
            }
        }
        #endregion

        public MeshRendererWPF()
        {
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith(("Default__"));
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;

            Preview?.Dispose();
            if (CurrentLoadedExport.ClassName == "StaticMesh")
            {
                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, ObjectBinary.From<StaticMesh>(CurrentLoadedExport), CurrentLOD, SceneViewer.Context.TextureCache);
            }
            else if (CurrentLoadedExport.ClassName == "SkeletalMesh")
            {
                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, new Unreal.Classes.SkeletalMesh(CurrentLoadedExport), SceneViewer.Context.TextureCache);
            }
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
            //throw new NotImplementedException();
        }
    }
}
