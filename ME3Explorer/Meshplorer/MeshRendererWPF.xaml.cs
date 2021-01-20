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
using ME3Explorer.Scene3D;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal.Classes;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using Microsoft.WindowsAPICodePack.Dialogs;
using SharpDX;
using SlavaGu.ConsoleAppLauncher;
using Matrix = SharpDX.Matrix;
using SkeletalMesh = ME3ExplorerCore.Unreal.BinaryConverters.SkeletalMesh;

namespace ME3Explorer.Meshplorer
{
    /// <summary>
    /// Interaction logic for MeshRendererWPF.xaml
    /// </summary>
    public partial class MeshRendererWPF : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "SkeletalMesh", "StaticMesh", "FracturedStaticMesh", "BioSocketSupermodel", "ModelComponent", "Model" };

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
                    Preview.Render(SceneViewer.Context, CurrentLOD, Matrix.Identity);
                }
                if (Wireframe)
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), Matrix.Identity);
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, Preview.LODs[CurrentLOD].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }
                if (IsStaticMesh && ShowCollisionMesh && STMCollisionMesh != null)
                {
                    SceneViewer.Context.Wireframe = true;
                    SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(Matrix.Transpose(SceneViewer.Context.Camera.ProjectionMatrix), Matrix.Transpose(SceneViewer.Context.Camera.ViewMatrix), Matrix.Identity);
                    SceneViewer.Context.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext);
                    SceneViewer.Context.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, ViewConstants, STMCollisionMesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }

            }
        }

        private void CenterView()
        {
            if (CurrentLOD >= 0)
            {
                if (Preview != null && Preview.LODs.Count > 0)
                {
                    WorldMesh m = Preview.LODs[CurrentLOD].Mesh;
                    SceneViewer.Context.Camera.Position = m.AABBCenter;
                    SceneViewer.Context.Camera.Pitch = -(float)Math.PI / 7.0f;
                    if (SceneViewer.Context.Camera.FirstPerson)
                    {
                        SceneViewer.Context.Camera.Position -= SceneViewer.Context.Camera.CameraForward *
                                                               SceneViewer.Context.Camera.FocusDepth;
                    }
                }
                else
                {
                    SceneViewer.Context.Camera.Position = Vector3.Zero;
                    SceneViewer.Context.Camera.Pitch = -(float)Math.PI / 5.0f;
                    SceneViewer.Context.Camera.Yaw = (float)Math.PI / 4.0f;
                }
            }
        }
        #endregion

        #region Busy variables
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    IsBusyChanged?.Invoke(this, EventArgs.Empty); //caller will just fetch and update this value
                }
            }
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

        private bool _isModel;
        public bool IsModel
        {
            get => _isModel;
            set => SetProperty(ref _isModel, value);
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

        private float _cameraPitch, _cameraYaw, _cameraX, _cameraY, _cameraZ, _cameraFOV, _cameraZNear, _cameraZFar;
        public float CameraPitch
        {
            get => _cameraPitch;
            set => SetProperty(ref _cameraPitch, value);
        }

        public float CameraYaw
        {
            get => _cameraYaw;
            set => SetProperty(ref _cameraYaw, value);
        }

        public float CameraX
        {
            get => _cameraX;
            set => SetProperty(ref _cameraX, value);
        }

        public float CameraY
        {
            get => _cameraY;
            set => SetProperty(ref _cameraY, value);
        }

        public float CameraZ
        {
            get => _cameraZ;
            set => SetProperty(ref _cameraZ, value);
        }

        public float CameraFOV
        {
            get => _cameraFOV;
            set
            {
                if (SetProperty(ref _cameraFOV, value))
                {
                    SceneViewer.Context.Camera.FOV = MathUtil.DegreesToRadians(value);
                }
            }
        }

        public float CameraZNear
        {
            get => _cameraZNear;
            set
            {
                if (SetProperty(ref _cameraZNear, value))
                {
                    SceneViewer.Context.Camera.ZNear = value;
                }
            }
        }

        public float CameraZFar
        {
            get => _cameraZFar;
            set
            {
                if (SetProperty(ref _cameraZFar, value))
                {
                    SceneViewer.Context.Camera.ZFar = value;
                }
            }
        }

        private bool _useDegrees = true, _useRadians, _useUnreal;

        public bool UseDegrees
        {
            get => _useDegrees;
            set => SetProperty(ref _useDegrees, value);
        }

        public bool UseRadians
        {
            get => _useRadians;
            set => SetProperty(ref _useRadians, value);
        }

        public bool UseUnreal
        {
            get => _useUnreal;
            set => SetProperty(ref _useUnreal, value);
        }

        #endregion

        private bool startingUp;
        public MeshRendererWPF() : base("Mesh Renderer")
        {
            startingUp = true;
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

        public event EventHandler IsBusyChanged;

        private bool ExportLoaded() => CurrentLoadedExport != null;
        private void ExportViaUModel()
        {
            BusyText = "Waiting for user input";
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

        private List<string> alreadyLoadedImportMaterials = new List<string>();

        public override void LoadExport(ExportEntry exportEntry)
        {
            UnloadExport();
            SceneViewer.InitializeD3D();
            //SceneViewer.Context.BackgroundColor = new SharpDX.Color(128, 128, 128);
            alreadyLoadedImportMaterials.Clear();
            CurrentLoadedExport = exportEntry;
            CurrentLOD = 0;

            Func<ModelPreview.PreloadedModelData> loadMesh = null;
            List<IMEPackage> cachedPackages = new List<IMEPackage>();

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
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry, cachedPackages);

                            }
                            else if (meshFile.IsImport(matIndex))
                            {
                                var extMaterialExport = ModelPreview.FindExternalAsset(meshFile.GetImport(matIndex), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, cachedPackages);
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
            else if (CurrentLoadedExport.IsA("SkeletalMesh"))
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
                            if (package.TryGetUExport(material.value, out var matExp))
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, matExp, cachedPackages);
                            }
                            else if (package.TryGetImport(material.value, out var matImp) && alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                            {
                                var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, cachedPackages);
                                    alreadyLoadedImportMaterials.Add(extMaterialExport.InstancedFullPath);
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
            else if (CurrentLoadedExport.ClassName == "ModelComponent")
            {
                IsModel = true;
                BusyText = "Fetching assets";
                BusyProgressIndeterminate = true;
                IsBusy = true;
                loadMesh = () =>
                {
                    var modelComp = ObjectBinary.From<ModelComponent>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = modelComp,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>(),
                    };

                    foreach (var element in modelComp.Elements)
                    {
                        if (CurrentLoadedExport != null)
                        {
                            if (CurrentLoadedExport.FileRef.TryGetUExport(element.Material, out var matExp))
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, matExp, cachedPackages);
                                pmd.sections.Add(new ModelPreviewSection(matExp.ObjectName, 0, 3)); //???

                            }
                            else if (CurrentLoadedExport.FileRef.TryGetImport(element.Material, out var matImp))
                            {
                                var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, cachedPackages);
                                }
                                else
                                {

                                    Debug.WriteLine("Could not find import material from section.");
                                    Debug.WriteLine("Import material: " + CurrentLoadedExport.FileRef.GetEntryString(element.Material));
                                }
                            }
                        }
                    }

                    return pmd;
                };
            }
            else if (CurrentLoadedExport.ClassName == "Model")
            {
                IsModel = true;
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    BusyProgressIndeterminate = true;
                    IsBusy = true;
                    var modelComp = ObjectBinary.From<Model>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = modelComp,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>(),
                    };
                    foreach (var mcExp in modelComp.Export.FileRef.Exports.Where(x =>
                        x.ClassName == "ModelComponent" && !x.IsDefaultObject))
                    {
                        var mc = ObjectBinary.From<ModelComponent>(mcExp);
                        if (mc.Model == modelComp.Self)
                        {
                            foreach (var element in mc.Elements)
                            {
                                if (CurrentLoadedExport == null) return pmd;
                                if (CurrentLoadedExport.FileRef.IsUExport(element.Material))
                                {
                                    ExportEntry entry = CurrentLoadedExport.FileRef.GetUExport(element.Material);
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry,
                                        cachedPackages);
                                }
                                else if (CurrentLoadedExport.FileRef.TryGetImport(element.Material, out var matImp) &&
                                         alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                                {
                                    var extMaterialExport = ModelPreview.FindExternalAsset(matImp,
                                        pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                    if (extMaterialExport != null)
                                    {
                                        AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials,
                                            extMaterialExport, cachedPackages);
                                        alreadyLoadedImportMaterials.Add(extMaterialExport.InstancedFullPath);
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Could not find import material from FModelElement.");
                                        Debug.WriteLine("Import material: " +
                                                        CurrentLoadedExport.FileRef.GetEntryString(element.Material));
                                    }
                                }
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
                    Debug.WriteLine("CACHED PACKAGE DISPOSAL");
                    foreach (var p in cachedPackages)
                    {
                        Debug.WriteLine($"Disposing package from asset lookup cache {p.FilePath}");
                        p?.Dispose();
                    }

                    cachedPackages = null;

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
                                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, statM, CurrentLOD, SceneViewer.Context.TextureCache, cachedPackages, pmd);
                                SceneViewer.Context.Camera.FocusDepth = statM.Bounds.SphereRadius * 1.2f;
                                break;
                            case SkeletalMesh skm:
                                Preview = new Scene3D.ModelPreview(SceneViewer.Context.Device, skm, SceneViewer.Context.TextureCache, cachedPackages, pmd);
                                SceneViewer.Context.Camera.FocusDepth = skm.Bounds.SphereRadius * 1.2f;
                                break;
                            case StructProperty structProp: //BrushComponent
                                Preview = new ModelPreview(SceneViewer.Context.Device, GetMeshFromAggGeom(structProp), SceneViewer.Context.TextureCache, cachedPackages, pmd);
                                SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                            case ModelComponent mc:
                                Preview = new ModelPreview(SceneViewer.Context.Device, GetMeshFromModelComponent(mc), SceneViewer.Context.TextureCache, cachedPackages, pmd);
                                //SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                            case Model m:
                                List<ModelPreviewSection> sections = new List<ModelPreviewSection>();
                                WorldMesh mesh = GetMeshFromModelSubcomponents(m, sections);
                                pmd.sections = sections;
                                if (mesh.Vertices.Any())
                                {
                                    SceneViewer.Context.Camera.Position = mesh.Vertices[0].Position;
                                }

                                Preview = new ModelPreview(SceneViewer.Context.Device, mesh, SceneViewer.Context.TextureCache, cachedPackages, pmd);
                                //SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
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

        /// <summary>
        /// Exports via UModel after ensuring
        /// </summary>
        public void EnsureUModel()
        {
            var savewarning = Xceed.Wpf.Toolkit.MessageBox.Show(null, "Exporting a model via UModel requires this package to be saved. Confirm it's OK to save this package before UModel processes exporting from this file.", "Package save warning", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

            if (savewarning == MessageBoxResult.OK)
            {
                CurrentLoadedExport.FileRef.Save();

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += EnsureUModel_BackgroundThread;
                bw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Result is string message)
                    {
                        BusyText = "Error downloading umodel";
                        MessageBox.Show($"An error occurred fetching umodel. Please comes to the ME3Tweaks Discord for assistance.\n\n{message}", "Error fetching umodel");
                    }
                    else if (b.Result == null)
                    {
                        ExportViaUModel();
                    }

                    IsBusy = false;
                };
                bw.RunWorkerAsync();
            }
        }

        private void CameraPropsMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock t)
            {
                var text = t.Text.Substring(t.Text.IndexOf(':') + 1).Trim();
                Clipboard.SetText(text);
            }
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

        private WorldMesh GetMeshFromModelSubcomponents(Model model, List<ModelPreviewSection> sections)
        {
            // LOL this will run terribly i'm sure
            var vertices = new List<WorldVertex>();
            var triangles = new List<Triangle>();

            foreach (var vertex in model.VertexBuffer)
            {
                // We don't know the normal vectors yet
                vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(vertex.TexCoord.X, vertex.TexCoord.Y)));
            }

            foreach (var mcExp in model.Export.FileRef.Exports.Where(x => x.ClassName == "ModelComponent" && !x.IsDefaultObject))
            {
                var mc = ObjectBinary.From<ModelComponent>(mcExp);
                if (mc.Model == model.Self)
                {
                    foreach (var modelElement in mc.Elements)
                    {
                        foreach (var node in modelElement.Nodes)
                        {
                            var matchingNode = model.Nodes[node];
                            var surface = model.Surfs[matchingNode.iSurf];
                            sections.Add(new ModelPreviewSection(model.Export.FileRef.getObjectName(surface.Material), (uint)triangles.Count * 3, ((uint)matchingNode.NumVertices - 2) * 3));

                            for (uint i = 2; i < matchingNode.NumVertices; i++)
                            {
                                triangles.Add(new Triangle((uint)matchingNode.iVertexIndex, (uint)matchingNode.iVertexIndex + i - 1, (uint)matchingNode.iVertexIndex + i));
                            }
                            // Overwrite the normal vectors of the included vertices now that we know them
                            Vector3 normal = model.Vectors[model.Surfs[matchingNode.iSurf].vNormal];
                            for (int i = 0; i < matchingNode.NumVertices; i++)
                            {
                                vertices[matchingNode.iVertexIndex + i].Normal = new Vector3(-normal.X, normal.Z, normal.Y);
                            }
                        }
                    }
                }
            }

            return new WorldMesh(SceneViewer.Context.Device, triangles, vertices);
        }

        private WorldMesh GetMeshFromModelComponent(ModelComponent mc)
        {

            var parentModel = ObjectBinary.From<Model>(mc.Export.FileRef.GetUExport(mc.Model));
            var vertices = new List<WorldVertex>();

            foreach (var point in parentModel.Points)
            {
                vertices.Add(new WorldVertex(new Vector3(-point.X, point.Z, point.Y), Vector3.Zero, Vector2.Zero));
            }

            var triangles = new List<Triangle>();

            foreach (var modelElement in mc.Elements)
            {
                foreach (var node in modelElement.Nodes)
                {
                    var matchingNode = parentModel.Nodes[node];
                    //var surface = parentModel.Surfs[matchingNode.iSurf];
                    //var nodeVertices = new List<ME3ExplorerCore.SharpDX.Vector3>(matchingNode.NumVertices);

                    var vert0 = parentModel.Verts[matchingNode.iVertPool];

                    for (uint i = 2; i < matchingNode.NumVertices; i++)
                    {
                        var tri = new Triangle((uint)vert0.pVertex, (uint)parentModel.Verts[matchingNode.iVertPool + i - 1].pVertex, (uint)parentModel.Verts[matchingNode.iVertPool + i].pVertex);
                        triangles.Add(tri); // 0 is the base point. The rest of the triangles share this point
                    }
                }
            }

            return new WorldMesh(SceneViewer.Context.Device, triangles, vertices);
        }

        private static void AddMaterialBackgroundThreadTextures(List<ModelPreview.PreloadedTextureData> texturePreviewMaterials, ExportEntry entry, List<IMEPackage> cachedPackages)
        {
            var matinst = new MaterialInstanceConstant(entry, cachedPackages);
            if (texturePreviewMaterials.Any(x => x.MaterialExport.InstancedFullPath == entry.InstancedFullPath))
                return; //already cached
            Debug.WriteLine("Loading material assets for " + entry.InstancedFullPath);
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
                    var extAsset = ModelPreview.FindExternalAsset(import, texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                    if (extAsset != null) //Apparently some assets are cubemaps, we don't want these.
                    {
                        var preloadedTextureData = new ModelPreview.PreloadedTextureData();
                        //Debug.WriteLine("Preloading ext texture " + extAsset.ObjectName + " for material " + entry.ObjectName);
                        Texture2D t2d = new Texture2D(extAsset);
                        preloadedTextureData.decompressedTextureData = t2d.GetImageBytesForMip(t2d.GetTopMip(), t2d.Export.Game, true);
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
                    preloadedTextureData.decompressedTextureData = t2d.GetImageBytesForMip(t2d.GetTopMip(), t2d.Export.Game, true);
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
                if (Parent is TabItem ti && ti.Parent is TabControl tc)
                {
                    tc.SelectionChanged += MeshRendererWPF_HostingTabSelectionChanged;
                }
                HasLoaded = true;
                SceneViewer.Context.Update += MeshRenderer_ViewUpdate;
            }
        }

        private void MeshRenderer_ViewUpdate(object sender, float timeStep)
        {
            if (Rotating)
            {
                SceneViewer.Context.Camera.Yaw += 0.05f * timeStep;
                if (SceneViewer.Context.Camera.Yaw > 6.28) //It's in radians 
                    SceneViewer.Context.Camera.Yaw -= 6.28f; // Subtract so we don't overflow if this is open too long
            }

            Matrix viewMatrix = SceneViewer.Context.Camera.ViewMatrix;
            viewMatrix.Invert();
            Vector3 eyePosition = viewMatrix.TranslationVector;

            if (UseDegrees)
            {
                CameraPitch = MathUtil.RadiansToDegrees(SceneViewer.Context.Camera.Pitch);
                CameraYaw = MathUtil.RadiansToDegrees(SceneViewer.Context.Camera.Yaw);
            }
            else if (UseRadians)
            {

                CameraPitch = SceneViewer.Context.Camera.Pitch;
                CameraYaw = SceneViewer.Context.Camera.Yaw;
            }
            else if (UseUnreal)
            {
                CameraPitch = SceneViewer.Context.Camera.Pitch.RadiansToUnrealRotationUnits();
                CameraYaw = SceneViewer.Context.Camera.Yaw.RadiansToUnrealRotationUnits();
            }

            CameraX = eyePosition.X;
            CameraY = eyePosition.X;
            CameraZ = eyePosition.Z;

            CameraFOV = MathUtil.RadiansToDegrees(SceneViewer.Context.Camera.FOV);
            CameraZNear = SceneViewer.Context.Camera.ZNear;
            CameraZFar = SceneViewer.Context.Camera.ZFar;
        }

        private void BackgroundColorPicker_Changed(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (!startingUp && e.NewValue.HasValue)
            {
                var s = e.NewValue.Value.ToString();
                Properties.Settings.Default.MeshplorerBackgroundColor = s;
                Properties.Settings.Default.Save();
                SceneViewer.Context.BackgroundColor = new SharpDX.Color(e.NewValue.Value.R, e.NewValue.Value.G, e.NewValue.Value.B);
            }
        }

        public override void UnloadExport()
        {
            IsBrush = false;
            IsSkeletalMesh = false;
            IsStaticMesh = false;
            IsModel = false;
            CurrentLoadedExport = null;
            STMCollisionMesh = null;
            Preview?.Materials.Clear();
            Preview?.Dispose();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new MeshRendererWPF(), CurrentLoadedExport)
                {
                    Title = $"Mesh Renderer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }
    

        public override void Dispose()
        {
            if (Parent is TabItem ti && ti.Parent is TabControl tc)
            {
                tc.SelectionChanged -= MeshRendererWPF_HostingTabSelectionChanged;
            }
            Preview?.Dispose();
            if (SceneViewer != null)
            {
                if (SceneViewer.Context != null)
                {
                    SceneViewer.Context.Update -= MeshRenderer_ViewUpdate;
                }
                SceneViewer.Dispose();
            }
            CurrentLoadedExport = null;
            SceneViewer = null;
        }

        private void MeshRendererWPF_HostingTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Parent is TabItem ti)
            {
                if (e.AddedItems.Contains(ti))
                {
                    SceneViewer?.SetShouldRender(true);
                }
                else if (e.RemovedItems.Contains(ti))
                {
                    SceneViewer?.SetShouldRender(false);
                }
            }
        }

        private void MeshRendererWPF_OnKeyUp(object sender, KeyEventArgs e)
        {
            SceneViewer?.OnKeyUp(sender, e);
        }

        private void MeshRendererWPF_OnKeyDown(object sender, KeyEventArgs e)
        {
            SceneViewer?.OnKeyDown(sender, e);
        }
    }
}
