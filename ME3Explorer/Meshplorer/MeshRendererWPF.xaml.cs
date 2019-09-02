using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
using SkeletalMesh = ME3Explorer.Unreal.BinaryConverters.SkeletalMesh;
using StaticMesh = ME3Explorer.Unreal.BinaryConverters.StaticMesh;

namespace ME3Explorer.Meshplorer
{
    /// <summary>
    /// Interaction logic for MeshRendererWPF.xaml
    /// </summary>
    public partial class MeshRendererWPF : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "SkeletalMesh", "StaticMesh" };

        #region 3D

        private bool _rotating = true, _wireframe, _solid = true, _firstperson;

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
        private bool HasLoaded;

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
                SceneViewer.Context.Camera.Pitch = -(float)Math.PI / 7.0f;
                if (SceneViewer.Context.Camera.FirstPerson)
                {
                    SceneViewer.Context.Camera.Position -= SceneViewer.Context.Camera.CameraForward * SceneViewer.Context.Camera.FocusDepth;
                }
            }
            else
            {
                SceneViewer.Context.Camera.Position = SharpDX.Vector3.Zero;
                SceneViewer.Context.Camera.Pitch = -(float)Math.PI / 5.0f;
                SceneViewer.Context.Camera.Yaw = (float)Math.PI / 4.0f;
            }
        }
        #endregion

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
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
            SceneViewer.Context.BackgroundColor = new SharpDX.Color(128, 128, 128);

            CurrentLoadedExport = exportEntry;

            Preview?.Dispose();

            Func<ModelPreview.PreloadedModelData> loadMesh = null;
            if (CurrentLoadedExport.ClassName == "StaticMesh")
            {
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    IsBusy = true;
                    var meshObject = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData();
                    pmd.meshObject = meshObject;
                    pmd.sections = new List<ModelPreviewSection>();
                    pmd.texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>();
                    IMEPackage meshFile = meshObject.Export.FileRef;
                    foreach (var section in meshObject.LODModels[CurrentLOD].Elements)
                    {
                        int matIndex = section.Material.value;
                        if (meshFile.isUExport(matIndex))
                        {
                            ExportEntry entry = meshFile.getUExport(matIndex);
                            Debug.WriteLine("Getting material assets " + entry.GetFullPath);

                            AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry);

                        }
                        else if (meshFile.isUImport(matIndex))
                        {
                            var extMaterialExport = ModelPreview.FindExternalAsset(meshFile.getUImport(matIndex), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
                            if (extMaterialExport != null)
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport);
                            }
                            else
                            {

                                Debug.WriteLine("Could not find import material from section.");
                                Debug.WriteLine("Import material: " + meshFile.GetEntryString(matIndex));
                            }
                        }

                        pmd.sections.Add(new ModelPreviewSection(meshFile.getObjectName(matIndex), section.FirstIndex, section.NumTriangles));
                    }
                    return pmd;
                };
            }
            else if (CurrentLoadedExport.ClassName == "SkeletalMesh")
            {
                //var sm = new Unreal.Classes.SkeletalMesh(CurrentLoadedExport);
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    IsBusy = true;
                    var meshObject = ObjectBinary.From<SkeletalMesh>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData();
                    pmd.meshObject = meshObject;
                    pmd.sections = new List<ModelPreviewSection>();
                    pmd.texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>();
                    foreach (var material in meshObject.Materials)
                    {
                        if (material.value > 0)
                        {
                            ExportEntry entry = meshObject.Export.FileRef.getUExport(material.value);
                            Debug.WriteLine("Getting material assets " + entry.GetFullPath);

                            AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry);

                        }
                        else if (material.value < 0)
                        {
                            var extMaterialExport = ModelPreview.FindExternalAsset(meshObject.Export.FileRef.getUImport(material.value), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
                            if (extMaterialExport != null)
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport);
                            }
                            else
                            {

                                Debug.WriteLine("Could not find import material from materials list.");
                                Debug.WriteLine("Import material: " + meshObject.Export.FileRef.GetEntryString(material.value));
                            }
                        }
                    }
                    return pmd;
                };
            }

            if (loadMesh != null)
            {
                Task.Run(loadMesh).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    if (prevTask.Result is ModelPreview.PreloadedModelData pmd)
                    {
                        if (pmd.meshObject is StaticMesh statM)
                        {
                            Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, statM, CurrentLOD, SceneViewer.Context.TextureCache, pmd);
                            SceneViewer.Context.Camera.FocusDepth = statM.Bounds.SphereRadius * 1.2f;
                        }
                        else if (pmd.meshObject is SkeletalMesh skm)
                        {
                            Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, skm, SceneViewer.Context.TextureCache, pmd);
                            SceneViewer.Context.Camera.FocusDepth = skm.Bounds.SphereRadius * 1.2f;
                        }

                        CenterView();
                    }
                });
            }
        }

        private void AddMaterialBackgroundThreadTextures(List<ModelPreview.PreloadedTextureData> texturePreviewMaterials, ExportEntry entry)
        {
            var matinst = new Unreal.Classes.MaterialInstanceConstant(entry);
            foreach (var tex in matinst.Textures)
            {

                Debug.WriteLine("Preloading " + tex.GetFullPath);
                if (tex.ClassName == "TextureCube")
                {
                    //can't deal with cubemaps yet
                    continue;
                }
                if (tex is ImportEntry import)
                {
                    var extAsset = ModelPreview.FindExternalAsset(import, texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
                    if (extAsset != null) //Apparently some assets are cubemaps, we don't want these.
                    {
                        var preloadedTextureData = new ModelPreview.PreloadedTextureData();
                        //Debug.WriteLine("Preloading ext texture " + extAsset.ObjectName + " for material " + entry.ObjectName);
                        Texture2D t2d = new Texture2D(extAsset);
                        preloadedTextureData.decompressedTextureData = t2d.GetImageBytesForMip(t2d.GetTopMip());
                        preloadedTextureData.MaterialExport = entry;
                        preloadedTextureData.Mip = t2d.GetTopMip(); //This may need to be adjusted for data returned by previous function if it's using a lower mip
                        texturePreviewMaterials.Add(preloadedTextureData);
                    }
                }
                else
                {
                    var preloadedTextureData = new ModelPreview.PreloadedTextureData();
                    Texture2D t2d = new Texture2D(tex as ExportEntry);
                    //Debug.WriteLine("Preloading local texture " + tex.ObjectName + " for material " + entry.ObjectName);
                    preloadedTextureData.decompressedTextureData = t2d.GetImageBytesForMip(t2d.GetTopMip());
                    preloadedTextureData.MaterialExport = entry;
                    preloadedTextureData.Mip = t2d.GetTopMip(); //This may need to be adjusted for data returned by previous function if it's using a lower mip
                    texturePreviewMaterials.Add(preloadedTextureData);
                }
            }
        }

        private void MeshRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            if (!HasLoaded)
            {
                HasLoaded = true;
                SceneViewer.Context.Update += MeshRenderer_ViewUpdate;
            }
        }

        private void MeshRenderer_ViewUpdate(object sender, float e)
        {
            //Todo: Find a way to disable SceneViewer.Context.Update from firing if this control is not visible
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
            Preview?.Dispose();
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            Preview?.Dispose();
            SceneViewer.Context.Update -= MeshRenderer_ViewUpdate;
            SceneViewer.Dispose();
            CurrentLoadedExport = null;
            SceneViewer = null;
        }
    }
}
