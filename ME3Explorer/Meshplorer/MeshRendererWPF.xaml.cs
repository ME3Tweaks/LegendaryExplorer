using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
using Microsoft.WindowsAPICodePack.Dialogs;
using SharpDX;
using SlavaGu.ConsoleAppLauncher;
using Matrix = SharpDX.Matrix;
using SkeletalMesh = ME3Explorer.Unreal.BinaryConverters.SkeletalMesh;
using StaticMesh = ME3Explorer.Unreal.BinaryConverters.StaticMesh;

namespace ME3Explorer.Meshplorer
{
    /// <summary>
    /// Interaction logic for MeshRendererWPF.xaml
    /// </summary>
    public partial class MeshRendererWPF : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "SkeletalMesh", "StaticMesh", "FracturedStaticMesh" };

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
            set
            {
                if (SetProperty(ref _firstperson, value))
                {
                    SceneViewer.Context.Camera.FirstPerson = value;
                }
            }
        }

        private int _currentLOD = 0;
        public int CurrentLOD
        {
            get => _currentLOD;
            set
            {
                if (SetProperty(ref _currentLOD, value))
                {
                    SceneViewer.Context.RenderScene();
                }
            }
        }
        public ObservableCollectionExtended<string> LODPicker { get; } = new ObservableCollectionExtended<string>();

        private ModelPreview Preview;

        private float PreviewRotation;
        private bool HasLoaded;
        private WorldMesh STMCollisionMesh;

        private void SceneViewer_Render(object sender, EventArgs e)
        {
            if (Preview != null && Preview.LODs.Count > 0)
            {

                if (CurrentLOD < 0) { CurrentLOD = 0; }
                if (Solid && CurrentLOD < Preview.LODs.Count)
                {
                    SceneViewer.Context.Wireframe = false;
                    Preview.Render(SceneViewer.Context, CurrentLOD, Matrix.RotationY(PreviewRotation));
                }
                if (Wireframe)
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), Matrix.Transpose(SharpDX.Matrix.RotationY(PreviewRotation)));
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, Preview.LODs[CurrentLOD].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }
                if (IsStaticMesh && ShowCollisionMesh && STMCollisionMesh != null)
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), Matrix.Transpose(SharpDX.Matrix.RotationY(PreviewRotation)));
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, STMCollisionMesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
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
                SceneViewer.Context.Camera.Position = Vector3.Zero;
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

        private bool _busyProgressIndeterminate = true;

        public bool BusyProgressIndeterminate
        {
            get => _busyProgressIndeterminate;
            set => SetProperty(ref _busyProgressIndeterminate, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private int _busyProgressBarMax = 100;

        public int BusyProgressBarMax
        {
            get => _busyProgressBarMax;
            set => SetProperty(ref _busyProgressBarMax, value);
        }

        private int _busyProgressBarValue;
        public int BusyProgressBarValue
        {
            get => _busyProgressBarValue;
            set => SetProperty(ref _busyProgressBarValue, value);
        }
        #endregion

        #region Bindings
        private bool _isStaticMesh;
        public bool IsStaticMesh
        {
            get => _isStaticMesh;
            set => SetProperty(ref _isStaticMesh, value);
        }

        private bool _isSkeletalMesh;
        public bool IsSkeletalMesh
        {
            get => _isSkeletalMesh;
            set => SetProperty(ref _isSkeletalMesh, value);
        }

        private bool _isBrush;
        public bool IsBrush
        {
            get => _isBrush;
            set => SetProperty(ref _isBrush, value);
        }

        private bool _showCollisionMesh;
        public bool ShowCollisionMesh
        {
            get => _showCollisionMesh;
            set => SetProperty(ref _showCollisionMesh, value);
        }
        #endregion

        private bool startingUp = true;
        public MeshRendererWPF()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            var color = (System.Windows.Media.Color?)ColorConverter.ConvertFromString(Properties.Settings.Default.MeshplorerBackgroundColor);
            Background_ColorPicker.SelectedColor = color;
            SceneViewer.Context.BackgroundColor = new SharpDX.Color(color.Value.R, color.Value.G, color.Value.B);
            startingUp = false;
        }

        public ICommand UModelExportCommand { get; set; }
        private void LoadCommands()
        {
            UModelExportCommand = new GenericCommand(EnsureUModel, ExportLoaded);
        }

        private bool ExportLoaded() => CurrentLoadedExport != null;
        private void ExportViaUModel()
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select output directory"
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == CommonFileDialogResult.Ok)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (a, b) =>
                {
                    string umodel = Path.Combine(App.StaticExecutablesDirectory, "umodel", "umodel.exe");
                    List<string> args = new List<string>();
                    args.Add("-export");
                    args.Add($"-out=\"{dlg.FileName}\"");
                    args.Add($"\"{CurrentLoadedExport.FileRef.FilePath}\"");
                    args.Add(CurrentLoadedExport.ObjectName);
                    args.Add(CurrentLoadedExport.ClassName);

                    var arguments = string.Join(" ", args);
                    Debug.WriteLine("Running process: " + umodel + " " + arguments);
                    //Log.Information("Running process: " + exe + " " + args);


                    var umodelProcess = new ConsoleApp(umodel, arguments);
                    //BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
                    //BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();
                    IsBusy = true;
                    BusyText = "Exporting via UModel\nThis may take a few minutes";
                    BusyProgressIndeterminate = true;
                    umodelProcess.ConsoleOutput += (o, args2) => { Debug.WriteLine(args2.Line); };
                    umodelProcess.Run();
                    while (umodelProcess.State == AppState.Running)
                    {
                        Thread.Sleep(100); //this is kind of hacky but it works
                    }

                    Process.Start("explorer", dlg.FileName);
                };
                bw.RunWorkerCompleted += (a, b) => { IsBusy = false; };
                bw.RunWorkerAsync();
            }
        }

        public static bool CanParseStatic(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject && (parsableClasses.Contains(exportEntry.ClassName)
                   || (exportEntry.ClassName == "BrushComponent" && exportEntry.GetProperty<StructProperty>("BrushAggGeom") != null));
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return CanParseStatic(exportEntry);
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            UnloadExport();
            SceneViewer.InitializeD3D();
            //SceneViewer.Context.BackgroundColor = new SharpDX.Color(128, 128, 128);

            CurrentLoadedExport = exportEntry;
            CurrentLOD = 0;

            Func<ModelPreview.PreloadedModelData> loadMesh = null;
            if (CurrentLoadedExport.ClassName == "StaticMesh" || CurrentLoadedExport.ClassName == "FracturedStaticMesh")
            {
                IsStaticMesh = true;
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    BusyProgressIndeterminate = true;
                    IsBusy = true;
                    var meshObject = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = meshObject,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>()
                    };
                    IMEPackage meshFile = meshObject.Export.FileRef;
                    if (meshFile.Game != MEGame.UDK)
                    {
                        foreach (var section in meshObject.LODModels[CurrentLOD].Elements)
                        {
                            int matIndex = section.Material.value;
                            if (meshFile.IsUExport(matIndex))
                            {
                                ExportEntry entry = meshFile.GetUExport(matIndex);
                                Debug.WriteLine("Getting material assets " + entry.InstancedFullPath);

                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry);

                            }
                            else if (meshFile.IsImport(matIndex))
                            {
                                var extMaterialExport = ModelPreview.FindExternalAsset(meshFile.GetImport(matIndex), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
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
                    }
                    return pmd;
                };
            }
            else if (CurrentLoadedExport.ClassName == "SkeletalMesh")
            {
                IsSkeletalMesh = true;
                //var sm = new Unreal.Classes.SkeletalMesh(CurrentLoadedExport);
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    IsBusy = true;
                    var meshObject = ObjectBinary.From<SkeletalMesh>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = meshObject,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>()
                    };
                    IMEPackage package = meshObject.Export.FileRef;
                    if (package.Game != MEGame.UDK)
                    {
                        foreach (var material in meshObject.Materials)
                        {
                            if (package.IsUExport(material.value))
                            {
                                ExportEntry entry = package.GetUExport(material.value);
                                Debug.WriteLine("Getting material assets " + entry.InstancedFullPath);

                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry);

                            }
                            else if (package.IsImport(material.value))
                            {
                                var extMaterialExport = ModelPreview.FindExternalAsset(package.GetImport(material.value), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport);
                                }
                                else
                                {

                                    Debug.WriteLine("Could not find import material from materials list.");
                                    Debug.WriteLine("Import material: " + package.GetEntryString(material.value));
                                }
                            }
                        }
                    }
                    return pmd;
                };
            }
            else if (CurrentLoadedExport.ClassName == "BrushComponent")
            {
                IsBrush = true;
                loadMesh = () =>
                {
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = CurrentLoadedExport.GetProperty<StructProperty>("BrushAggGeom"),
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>(),
                    };
                    return pmd;
                };
            }

            if (loadMesh != null)
            {
                Task.Run(loadMesh).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    if (CurrentLoadedExport == null)
                    {
                        //in the time since the previous task was started, the export has been unloaded
                        return;
                    }
                    if (prevTask.Result is ModelPreview.PreloadedModelData pmd)
                    {
                        switch (pmd.meshObject)
                        {
                            case StaticMesh statM:
                                STMCollisionMesh = GetMeshFromAggGeom(statM.GetCollisionMeshProperty(Pcc));
                                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, statM, CurrentLOD, SceneViewer.Context.TextureCache, pmd);
                                SceneViewer.Context.Camera.FocusDepth = statM.Bounds.SphereRadius * 1.2f;
                                break;
                            case SkeletalMesh skm:
                                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, skm, SceneViewer.Context.TextureCache, pmd);
                                SceneViewer.Context.Camera.FocusDepth = skm.Bounds.SphereRadius * 1.2f;
                                break;
                            case StructProperty structProp: //BrushComponent
                                Preview = new ModelPreview(SceneViewer.Context.Device, GetMeshFromAggGeom(structProp));
                                SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                        }

                        CenterView();
                        LODPicker.ClearEx();
                        for (int l = 0; l < Preview.LODs.Count; l++)
                        {
                            LODPicker.Add($"LOD{l.ToString()}");
                        }
                    }
                });
            }
        }

        private void EnsureUModel()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += EnsureUModel_BackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is string message)
                {
                    BusyText = "Error downloading umodel";
                    MessageBox.Show($"An error occured fetching umodel. Please comes to the ME3Tweaks Discord for assistance.\n\n{message}", "Error fetching umodel");
                }
                else if (b.Result == null)
                {
                    ExportViaUModel();
                }

                IsBusy = false;
            };
            bw.RunWorkerAsync();
        }
        public void EnsureUModel_BackgroundThread(object sender, DoWorkEventArgs args)
        {
            void progressCallback(long bytesDownloaded, long bytesToDownload)
            {
                BusyProgressBarMax = (int)bytesToDownload;
                BusyProgressBarValue = (int)bytesDownloaded;
            }
            //try{
            BusyText = "Downloading umodel";
            BusyProgressIndeterminate = false;
            BusyProgressBarValue = 0;
            IsBusy = true;
            args.Result = OnlineContent.EnsureStaticZippedExecutable("umodel_win32.zip", "umodel", "umodel.exe", progressCallback);
            //}
            //catch (Exception e)
            //{
            //    args.Result = "Error downloading required files:\n" + ExceptionHandlerDialogWPF.FlattenException(e);
            //}
        }


        private WorldMesh GetMeshFromAggGeom(StructProperty aggGeom)
        {
            if (aggGeom?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") is ArrayProperty<StructProperty> convexElems)
            {
                var vertices = new List<WorldVertex>();
                var triangles = new List<Triangle>();
                int vertTotal = 0;
                foreach (StructProperty convexElem in convexElems)
                {
                    var faceTriData = convexElem.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                    for (int i = 0; i < faceTriData.Count; i += 3)
                    {
                        triangles.Add(new Triangle((uint)(faceTriData[i].Value + vertTotal), (uint)(faceTriData[i + 1].Value + vertTotal), (uint)(faceTriData[i + 2].Value + vertTotal)));
                    }

                    var vertexData = convexElem.GetProp<ArrayProperty<StructProperty>>("VertexData");
                    foreach (StructProperty vertex in vertexData)
                    {
                        float x = vertex.GetProp<FloatProperty>("X").Value;
                        float y = vertex.GetProp<FloatProperty>("Y").Value;
                        float z = vertex.GetProp<FloatProperty>("Z").Value;
                        vertices.Add(new WorldVertex(new Vector3(-x, z, y), Vector3.Zero, Vector2.Zero));
                        ++vertTotal;
                    }
                }

                return new WorldMesh(SceneViewer.Context.Device, triangles, vertices);
            }

            return null;
        }

        private static void AddMaterialBackgroundThreadTextures(List<ModelPreview.PreloadedTextureData> texturePreviewMaterials, ExportEntry entry)
        {
            var matinst = new Unreal.Classes.MaterialInstanceConstant(entry);
            foreach (var tex in matinst.Textures)
            {

                Debug.WriteLine("Preloading " + tex.InstancedFullPath);
                if (tex.ClassName == "TextureCube" || tex.ClassName.StartsWith("TextureRender"))
                {
                    //can't deal with cubemaps/renderers yet
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
            if (!startingUp && e.NewValue.HasValue)
            {
                var s = e.NewValue.Value.ToString();
                SceneViewer.Context.BackgroundColor = new SharpDX.Color(e.NewValue.Value.R, e.NewValue.Value.G, e.NewValue.Value.B);
            }
        }

        public override void UnloadExport()
        {
            IsBrush = false;
            IsSkeletalMesh = false;
            IsStaticMesh = false;
            Preview?.Dispose();
            CurrentLoadedExport = null;
            STMCollisionMesh = null;
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
