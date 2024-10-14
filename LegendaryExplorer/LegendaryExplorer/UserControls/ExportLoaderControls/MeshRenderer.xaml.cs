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
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.UserControls.SharedToolControls.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.SharpDX;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorer.UserControls.ExportLoaderControls.TextureViewer;
using LegendaryExplorer.UserControls.Interfaces;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Shaders;
using SkeletalMesh = LegendaryExplorerCore.Unreal.BinaryConverters.SkeletalMesh;
using Color = LegendaryExplorerCore.SharpDX.Color;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for MeshRenderer.xaml
    /// </summary>
    public partial class MeshRenderer : ExportLoaderControl, ISceneRenderContextConfigurable
    {
        private static readonly string[] parsableClasses = ["SkeletalMesh", "StaticMesh", "FracturedStaticMesh", "BioSocketSupermodel", "ModelComponent", "Model"];

        #region 3D

        public MeshRenderContext MeshContext { get; }

        private bool _rotating = Settings.Meshplorer_ViewRotating;
        private bool _renderWireframe;
        private bool _renderSolid = true;
        private bool _renderGameShader;
        private bool _firstperson;

        public bool Rotating
        {
            get => _rotating;
            set
            {
                if (SetProperty(ref _rotating, value))
                {
                    Settings.Meshplorer_ViewRotating = value;
                    Settings.Save();
                }
            }
        }

        public bool RenderWireframe
        {
            get => _renderWireframe;
            set => SetProperty(ref _renderWireframe, value);
        }

        private bool _canUseGameShaders;
        public bool CanUseGameShaders
        {
            get => _canUseGameShaders;
            set => SetProperty(ref _canUseGameShaders, value);
        }

        public bool RenderGameShader
        {
            get => _renderGameShader;
            set
            {
                if (SetProperty(ref _renderGameShader, value) && _renderGameShader)
                {
                    //require reload so that the game shader feature is (relatively) costless when not used
                    if (GameShaderPreview is null)
                    {
                        LoadExport(CurrentLoadedExport);
                    }
                    RenderSolid = false;
                }
            }
        }

        public bool RenderSolid
        {
            get => _renderSolid;
            set
            {
                if (SetProperty(ref _renderSolid, value) && _renderSolid)
                {
                    RenderGameShader = false;
                }
            }
        }

        public bool FirstPerson
        {
            get => _firstperson;
            set
            {
                if (SetProperty(ref _firstperson, value))
                {
                    MeshContext.Camera.FirstPerson = value;
                }
            }
        }

        private int _currentLOD;
        public int CurrentLOD
        {
            get => _currentLOD;
            set
            {
                if (SetProperty(ref _currentLOD, value))
                {
                    //SceneViewer.Context.RenderScene();
                }
            }
        }
        public ObservableCollectionExtended<string> LODPicker { get; } = new();

        #region DISPLAY OPTIONS
        private bool _setAlphaToBlack = true;
        public bool SetAlphaToBlack
        {
            get => _setAlphaToBlack;
            set
            {
                SetProperty(ref _setAlphaToBlack, value);
                if (value)
                {
                    this.MeshContext.CurrentTextureViewFlags |= TextureRenderContext.TextureViewFlags.AlphaAsBlack;
                }
                else
                {
                    this.MeshContext.CurrentTextureViewFlags &= ~TextureRenderContext.TextureViewFlags.AlphaAsBlack;
                }
            }
        }

        private bool _showRedChannel = true;
        public bool ShowRedChannel
        {
            get => _showRedChannel;
            set
            {
                SetProperty(ref _showRedChannel, value);
                if (value)
                {
                    this.MeshContext.CurrentTextureViewFlags |= TextureRenderContext.TextureViewFlags.EnableRedChannel;
                }
                else
                {
                    this.MeshContext.CurrentTextureViewFlags &= ~TextureRenderContext.TextureViewFlags.EnableRedChannel;
                }
            }
        }

        private bool _showGreenChannel = true;
        public bool ShowGreenChannel
        {
            get => _showGreenChannel;
            set
            {
                SetProperty(ref _showGreenChannel, value);
                if (value)
                {
                    this.MeshContext.CurrentTextureViewFlags |= TextureRenderContext.TextureViewFlags.EnableGreenChannel;
                }
                else
                {
                    this.MeshContext.CurrentTextureViewFlags &= ~TextureRenderContext.TextureViewFlags.EnableGreenChannel;
                }
            }
        }

        private bool _showBlueChannel = true;
        public bool ShowBlueChannel
        {
            get => _showBlueChannel;
            set
            {
                SetProperty(ref _showBlueChannel, value);
                if (value)
                {
                    this.MeshContext.CurrentTextureViewFlags |= TextureRenderContext.TextureViewFlags.EnableBlueChannel;
                }
                else
                {
                    this.MeshContext.CurrentTextureViewFlags &= ~TextureRenderContext.TextureViewFlags.EnableBlueChannel;
                }
            }
        }

        private bool _showAlphaChannel = true;
        public bool ShowAlphaChannel
        {
            get => _showAlphaChannel;
            set
            {
                SetProperty(ref _showAlphaChannel, value);
                if (value)
                {
                    this.MeshContext.CurrentTextureViewFlags |= TextureRenderContext.TextureViewFlags.EnableAlphaChannel;
                }
                else
                {
                    this.MeshContext.CurrentTextureViewFlags &= ~TextureRenderContext.TextureViewFlags.EnableAlphaChannel;
                }
            }
        }

        private System.Windows.Media.Color _backgroundColor = Colors.White;
        public System.Windows.Media.Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                SetProperty(ref _backgroundColor, value);
                MeshContext.BackgroundColor = value;
            }
        }
        #endregion

        private ModelPreview<WorldVertex> LEXPreview;
        private ModelPreview<LEVertex> GameShaderPreview;

        /// <summary>
        /// Value is true after _Loaded is called. False after _Unloaded (which if in tab control, is called when different tab is selected)
        /// </summary>
        private bool ControlIsLoaded;
        private WorldMesh STMCollisionMesh;
        private Action ViewportLoadAction = null;

        private void SceneContext_RenderScene(object sender, EventArgs e)
        {
            if (CurrentLOD < 0) { CurrentLOD = 0; }
            foreach (RenderPass renderPass in Enum.GetValues<RenderPass>())
            {
                if (RenderSolid && LEXPreview is not null && CurrentLOD < LEXPreview.LODs.Count)
                {
                    MeshContext.Wireframe = false;
                    LEXPreview.Render(renderPass, MeshContext, CurrentLOD, Matrix4x4.Identity);
                }
                if (RenderGameShader && GameShaderPreview != null && CurrentLOD < GameShaderPreview.LODs.Count)
                {
                    MeshContext.Wireframe = false;
                    GameShaderPreview.Render(renderPass, MeshContext, CurrentLOD, Matrix4x4.Identity);
                }
            }
            if (RenderWireframe && LEXPreview is not null && CurrentLOD < LEXPreview.LODs.Count)
            {
                MeshContext.Wireframe = true;
                var viewConstants = new MeshRenderContext.WorldConstants(Matrix4x4.Transpose(MeshContext.Camera.ProjectionMatrix), Matrix4x4.Transpose(MeshContext.Camera.ViewMatrix), Matrix4x4.Identity, MeshContext.CurrentTextureViewFlags);
                MeshContext.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext, MeshContext.AlphaBlendState);
                MeshContext.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, viewConstants, LEXPreview.LODs[CurrentLOD].Mesh, [null]);
            }
            if (IsStaticMesh && ShowCollisionMesh && STMCollisionMesh != null)
            {
                MeshContext.Wireframe = true;
                var viewConstants = new MeshRenderContext.WorldConstants(Matrix4x4.Transpose(MeshContext.Camera.ProjectionMatrix), Matrix4x4.Transpose(MeshContext.Camera.ViewMatrix), Matrix4x4.Identity, MeshContext.CurrentTextureViewFlags);
                MeshContext.DefaultEffect.PrepDraw(SceneViewer.Context.ImmediateContext, MeshContext.AlphaBlendState);
                MeshContext.DefaultEffect.RenderObject(SceneViewer.Context.ImmediateContext, viewConstants, STMCollisionMesh, [null]);
            }
        }

        private void CenterView()
        {
            if (CurrentLOD >= 0)
            {
                if (GameShaderPreview != null && GameShaderPreview.LODs.Count > 0)
                {
                    var m = GameShaderPreview.LODs[CurrentLOD].Mesh;
                    MeshContext.Camera.Position = m.AABBCenter;
                    MeshContext.Camera.Pitch = -MathF.PI / 7.0f;
                    if (MeshContext.Camera.FirstPerson)
                    {
                        MeshContext.Camera.Position -= MeshContext.Camera.CameraForward * MeshContext.Camera.FocusDepth;
                    }
                }
                else if (LEXPreview != null && LEXPreview.LODs.Count > 0)
                {
                    var m = LEXPreview.LODs[CurrentLOD].Mesh;
                    MeshContext.Camera.Position = m.AABBCenter;
                    MeshContext.Camera.Pitch = -MathF.PI / 7.0f;
                    if (MeshContext.Camera.FirstPerson)
                    {
                        MeshContext.Camera.Position -= MeshContext.Camera.CameraForward * MeshContext.Camera.FocusDepth;
                    }
                }
                else
                {
                    MeshContext.Camera.Position = Vector3.Zero;
                    MeshContext.Camera.Pitch = -MathF.PI / 5.0f;
                    MeshContext.Camera.Yaw = MathF.PI / 4.0f;
                }
            }
        }
        #endregion

        #region Busy variables
        private bool _isBusy;

        private readonly Stopwatch sw = new();
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy && !value)
                {
                    sw.Stop();
                    Debug.WriteLine($"MeshRenderer busy time: {sw.Elapsed}");
                }
                else if (!_isBusy && value)
                {
                    sw.Reset();
                    sw.Start();
                }

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
                    MeshContext.Camera.FOV = LegendaryExplorerCore.SharpDX.MathUtil.DegreesToRadians(value);
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
                    MeshContext.Camera.ZNear = value;
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
                    MeshContext.Camera.ZFar = value;
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

        private readonly bool startingUp;
        public MeshRenderer() : base("Mesh Renderer")
        {
            startingUp = true;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            MeshContext = new MeshRenderContext();
            if (ColorConverter.ConvertFromString(Settings.Meshplorer_BackgroundColor) is System.Windows.Media.Color color)
            {
                BackgroundColor = color;
            }
            SceneViewer.Context = MeshContext;
            //MeshContext.BackgroundColor = color is not null ? new Color(color.Value.R, color.Value.G, color.Value.B) : Color.FromRgba(0x999999);
            SceneViewer.Loaded += (sender, args) =>
            {
                if (MeshContext.IsReady)
                {
                    this.ViewportLoadAction?.Invoke();
                }
                this.ViewportLoadAction = null;
            };

            startingUp = false;
        }

        public ICommand UModelExportCommand { get; set; }

        private void LoadCommands()
        {
            UModelExportCommand = new GenericCommand(EnsureUModelAndExport, CanExportViaUModel);
        }

        public event EventHandler IsBusyChanged;

        private bool CanExportViaUModel() => CurrentLoadedExport != null && (IsStaticMesh || IsSkeletalMesh);

        public static bool CanParseStatic(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject &&
                   (parsableClasses.Contains(exportEntry.ClassName, StringComparer.OrdinalIgnoreCase) ||
                    (exportEntry.ClassName.CaseInsensitiveEquals("BrushComponent") && exportEntry.GetProperty<StructProperty>("BrushAggGeom") != null) ||
                    (exportEntry.Game.IsMEGame() && exportEntry.ClassName.CaseInsensitiveEquals("StaticMeshComponent") && exportEntry.GetProperty<ObjectProperty>("StaticMesh")?.Value != 0));
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return CanParseStatic(exportEntry);
        }

        private readonly List<string> alreadyLoadedImportMaterials = new();

        /// <summary>
        /// Used for debugging by listing the used instances
        /// </summary>
        //public ObservableCollectionExtended<PreviewTextureCache.PreviewTextureEntry> SceneViewerProperty => SceneViewer?.Context?.TextureCache?.AssetCache;

        public override void LoadExport(ExportEntry exportEntry)
        {
            UnloadExport();
            if (exportEntry == null)
                return; // Can reload due to static mesh component looking for static mesh


            //SceneViewer.Context.BackgroundColor = new SharpDX.Color(128, 128, 128);
            alreadyLoadedImportMaterials.Clear();
            CurrentLoadedExport = exportEntry;
            CurrentLOD = 0;
            CanUseGameShaders = exportEntry.Game is MEGame.LE3;

            Func<PreloadedModelData> loadMesh;
            var assetCache = new PackageCache();

            if (exportEntry.ClassName is "StaticMeshComponent")
            {
                var cache = new PackageCache();
                var mesh = CurrentLoadedExport.GetProperty<ObjectProperty>("StaticMesh")?.ResolveToExport(exportEntry.FileRef, cache);
                if (mesh != null)
                {
                    var mats = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
                    if (mats != null)
                    {
                        OverlayMaterials = mats.Select(x => x.Value != 0 ? x.ResolveToExport(CurrentLoadedExport.FileRef, cache) : null).Cast<IEntry>().ToList();
                    }
                }

                // Reload on the mesh.
                LoadExport(mesh);
                return;
            }

            if (CurrentLoadedExport.ClassName is "StaticMesh" or "FracturedStaticMesh")
            {
                IsStaticMesh = true;
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    BusyProgressIndeterminate = true;
                    IsBusy = true;

                    var meshObject = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                    if (OverlayMaterials != null)
                    {
                        meshObject.SetMaterials(OverlayMaterials, true);
                        OverlayMaterials = null;
                    }
                    var pmd = new PreloadedModelData
                    {
                        meshObject = meshObject,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<PreloadedTextureData>()
                    };
                    IMEPackage meshFile = meshObject.Export.FileRef;
                    if (meshFile.Game != MEGame.UDK)
                    {
                        foreach (StaticMeshElement section in meshObject.LODModels[CurrentLOD].Elements)
                        {
                            int matIndex = section.Material;
                            if (meshFile.IsUExport(matIndex))
                            {
                                ExportEntry entry = meshFile.GetUExport(matIndex);
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry, assetCache);
                            }
                            else if (meshFile.IsImport(matIndex))
                            {
                                var extMaterialExport = EntryImporter.ResolveImport(meshFile.GetImport(matIndex), assetCache);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, assetCache);
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
                    var pmd = new PreloadedModelData
                    {
                        meshObject = meshObject,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<PreloadedTextureData>()
                    };
                    IMEPackage package = meshObject.Export.FileRef;
                    if (package.Game != MEGame.UDK)
                    {
                        foreach (int material in meshObject.Materials)
                        {
                            if (package.TryGetUExport(material, out ExportEntry matExp))
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, matExp, assetCache);
                            }
                            else if (package.TryGetImport(material, out ImportEntry matImp) && alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                            {
                                var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                                //var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, assetCache);
                                    alreadyLoadedImportMaterials.Add(extMaterialExport.InstancedFullPath);
                                }
                                else
                                {
                                    Debug.WriteLine("Could not find import material from materials list.");
                                    Debug.WriteLine("Import material: " + package.GetEntryString(material));
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
                    var pmd = new PreloadedModelData
                    {
                        meshObject = CurrentLoadedExport.GetProperty<StructProperty>("BrushAggGeom"),
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<PreloadedTextureData>(),
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
                    var pmd = new PreloadedModelData
                    {
                        meshObject = modelComp,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<PreloadedTextureData>(),
                    };

                    foreach (var element in modelComp.Elements)
                    {
                        if (CurrentLoadedExport != null)
                        {
                            if (CurrentLoadedExport.FileRef.TryGetUExport(element.Material, out var matExp))
                            {
                                AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, matExp, assetCache);
                                pmd.sections.Add(new ModelPreviewSection(matExp.ObjectName, 0, 3)); //???
                            }
                            else if (CurrentLoadedExport.FileRef.TryGetImport(element.Material, out var matImp))
                            {
                                var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                                //var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                                if (extMaterialExport != null)
                                {
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, extMaterialExport, assetCache);
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
                    var pmd = new PreloadedModelData
                    {
                        meshObject = modelComp,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<PreloadedTextureData>(),
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
                                    AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials, entry, assetCache);
                                }
                                else if (CurrentLoadedExport.FileRef.TryGetImport(element.Material, out var matImp) &&
                                         alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                                {
                                    var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                                    if (extMaterialExport != null)
                                    {
                                        AddMaterialBackgroundThreadTextures(pmd.texturePreviewMaterials,
                                            extMaterialExport, assetCache);
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
            else
            {
                return;
            }

            

            Task.Run(loadMesh).ContinueWith(prevTask =>
            {
                if (CanUseGameShaders && RenderGameShader)
                {
                    BusyText = "Reading Shader Cache (~15s)";
                    RefShaderCacheReader.PopulateOffsets(Pcc.Game);
                }
                return prevTask.Result;
            }).ContinueWithOnUIThread(prevTask =>
            {
                IsBusy = false;
                if (CurrentLoadedExport == null)
                {
                    //in the time since the previous task was started, the export has been unloaded
                    return;
                }
                if (prevTask.Result is PreloadedModelData pmd)
                {
                    Action loadPreviewAction = () =>
                    {
                        LEXPreview?.Dispose();
                        GameShaderPreview?.Dispose();
                        LEXPreview = null;
                        GameShaderPreview = null;
                        STMCollisionMesh?.Dispose();
                        STMCollisionMesh = null;
                        switch (pmd.meshObject)
                        {
                            case StaticMesh statM:
                                STMCollisionMesh = GetMeshFromAggGeom(statM.GetCollisionMeshProperty(Pcc));
                                if (CanUseGameShaders && RenderGameShader) GameShaderPreview = new ModelPreview<LEVertex>(MeshContext.Device, statM, CurrentLOD, MeshContext.TextureCache, assetCache, pmd);
                                LEXPreview = new ModelPreview<WorldVertex>(MeshContext.Device, statM, CurrentLOD, MeshContext.TextureCache, assetCache, pmd);
                                MeshContext.Camera.FocusDepth = statM.Bounds.SphereRadius * 1.2f;
                                break;
                            case SkeletalMesh skm:
                                if (CanUseGameShaders && RenderGameShader) GameShaderPreview = new ModelPreview<LEVertex>(MeshContext.Device, skm, MeshContext.TextureCache, assetCache, pmd);
                                LEXPreview = new ModelPreview<WorldVertex>(MeshContext.Device, skm, MeshContext.TextureCache, assetCache, pmd);
                                MeshContext.Camera.FocusDepth = skm.Bounds.SphereRadius * 1.2f;
                                break;
                            case StructProperty structProp: //BrushComponent
                                LEXPreview = new ModelPreview<WorldVertex>(MeshContext.Device, GetMeshFromAggGeom(structProp), MeshContext.TextureCache, assetCache, pmd);
                                MeshContext.Camera.FocusDepth = LEXPreview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                            case ModelComponent mc:
                                LEXPreview = new ModelPreview<WorldVertex>(MeshContext.Device, GetMeshFromModelComponent(mc), MeshContext.TextureCache, assetCache, pmd);
                                //SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                            case Model m:
                                var sections = new List<ModelPreviewSection>();
                                Mesh<WorldVertex> mesh = GetMeshFromModelSubcomponents(m, sections);
                                pmd.sections = sections;
                                if (mesh.Vertices.Any())
                                {
                                    MeshContext.Camera.Position = mesh.Vertices[0].Position;
                                }

                                LEXPreview = new ModelPreview<WorldVertex>(MeshContext.Device, mesh, MeshContext.TextureCache, assetCache, pmd);
                                //SceneViewer.Context.Camera.FocusDepth = Preview.LODs[0].Mesh.AABBHalfSize.Length() * 1.2f;
                                break;
                        }
                        assetCache.Dispose();
                        LODPicker.ClearEx();
                        if (LEXPreview is not null)
                        {
                            for (int i = 0; i < LEXPreview.LODs.Count; i++)
                            {
                                LODPicker.Add($"LOD{i}");
                            }
                        }
                        CenterView();
                    };

                    LODPicker.ClearEx();
                    //clearing the LODPicker will set CurrentLOD to -1
                    //if it is -1, meshes will not render.
                    CurrentLOD = 0;

                    // We can't call graphics methods until the render control has been loaded by WPF - only then will it have initialized D3D.
                    if (this.MeshContext.IsReady)
                    {
                        loadPreviewAction.Invoke();
                    }
                    else
                    {
                        this.ViewportLoadAction = loadPreviewAction;
                    }
                }
            });
        }

        /// <summary>
        /// Material overrides for a mesh
        /// </summary>
        public List<IEntry> OverlayMaterials { get; set; }

        /// <summary>
        /// Exports via UModel after ensuring
        /// </summary>
        public void EnsureUModelAndExport()
        {
            if (CurrentLoadedExport == null) return;
            var savewarning = CurrentLoadedExport.FileRef.IsModified ? MessageBoxResult.None : MessageBoxResult.OK;

            // show if we have not shown before
            if (savewarning == MessageBoxResult.None)
            {
                savewarning = Xceed.Wpf.Toolkit.MessageBox.Show(null,
                                                                "Exporting a model via UModel requires this package to be saved. Confirm it's OK to save this package before UModel processes exporting from this file.",
                                                                "Package save warning",
                                                                MessageBoxButton.OKCancel,
                                                                MessageBoxImage.Exclamation);
            }
            if (savewarning == MessageBoxResult.OK)
            {
                CurrentLoadedExport.FileRef.Save();

                var bw = new BackgroundWorker();
                bw.DoWork += EnsureUModel_BackgroundThread;
                bw.RunWorkerCompleted += (_, b) =>
                {
                    if (b.Result is string message)
                    {
                        BusyText = "Error downloading umodel";
                        MessageBox.Show($"An error occurred fetching umodel. Please comes to the ME3Tweaks Discord for assistance.\n\n{message}", "Error fetching umodel");
                    }
                    else if (b.Result == null)
                    {
                        UModelHelper.ExportViaUModel(Window.GetWindow(this), CurrentLoadedExport);
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
            // Pass error message back
            args.Result = UModelHelper.EnsureUModel(
                () => IsBusy = true,
                maxProgress => BusyProgressBarMax = maxProgress,
                currentProgress => BusyProgressBarValue = currentProgress,
                busyText => BusyText = busyText
                );
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
            var vertexList = new List<WorldVertex>();
            var triangles = new List<Triangle>();

            foreach (var vertex in model.VertexBuffer)
            {
                // We don't know the normal vectors yet
                vertexList.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(vertex.TexCoord.X, vertex.TexCoord.Y)));
            }
            Span<WorldVertex> vertsSpan = CollectionsMarshal.AsSpan(vertexList);

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
                                vertsSpan[matchingNode.iVertexIndex + i].Normal = new Vector3(-normal.X, normal.Z, normal.Y);
                            }
                        }
                    }
                }
            }

            return new WorldMesh(SceneViewer.Context.Device, triangles, vertexList);
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
                    //var nodeVertices = new List<LegendaryExplorerCore.SharpDX.Vector3>(matchingNode.NumVertices);

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

        private static void AddMaterialBackgroundThreadTextures(List<PreloadedTextureData> texturePreviewMaterials, ExportEntry entry, PackageCache assetCache)
        {
            if (texturePreviewMaterials.Any(x => x.MaterialExport.InstancedFullPath == entry.InstancedFullPath))
                return; //already cached
            Debug.WriteLine("Loading material assets for " + entry.InstancedFullPath);
            foreach (var tex in MaterialInstanceConstant.GetTextures(entry, assetCache))
            {
                Debug.WriteLine("Preloading " + tex.InstancedFullPath);
                if (tex.ClassName.StartsWith("TextureRender"))
                {
                    //can't deal with renderers yet
                    continue;
                }
                if (tex is ImportEntry import)
                {
                    var extAsset = EntryImporter.ResolveImport(import, assetCache);
                    if (extAsset != null) //Apparently some assets are cubemaps, we don't want these.
                    {
                        texturePreviewMaterials.Add(new PreloadedTextureData
                        {
                            TextureExport = extAsset,
                            MaterialExport = entry
                        });
                    }
                }
                else
                {
                    texturePreviewMaterials.Add(new PreloadedTextureData
                    {
                        TextureExport = (ExportEntry)tex,
                        MaterialExport = entry
                    });
                }
            }
        }

        private void MeshRenderer_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MESHRENDERER UNLOADED");
            if (Parent is TabItem { Parent: TabControl tc })
            {
                tc.SelectionChanged -= MeshRendererWPF_HostingTabSelectionChanged;
            }
            MeshContext.UpdateScene -= SceneContext_UpdateScene;
            MeshContext.RenderScene -= SceneContext_RenderScene;
            ControlIsLoaded = false;
        }

        private void MeshRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            if (!ControlIsLoaded)
            {
                Debug.WriteLine("MESHRENDERER ONLOADED");
                if (Parent is TabItem { Parent: TabControl tc })
                {
                    tc.SelectionChanged += MeshRendererWPF_HostingTabSelectionChanged;
                }
                ControlIsLoaded = true;
                MeshContext.UpdateScene += SceneContext_UpdateScene;
                MeshContext.RenderScene += SceneContext_RenderScene;
            }
        }

        private void SceneContext_UpdateScene(object sender, float timeStep)
        {
            if (ControlIsLoaded && Rotating)
            {
                MeshContext.Camera.Yaw += 0.3f * timeStep;
                if (MeshContext.Camera.Yaw > 6.28) //It's in radians 
                    MeshContext.Camera.Yaw -= 6.28f; // Subtract so we don't overflow if this is open too long
            }

            Matrix4x4.Invert(MeshContext.Camera.ViewMatrix, out Matrix4x4 viewMatrix);
            Vector3 eyePosition = viewMatrix.Translation;

            if (UseDegrees)
            {
                CameraPitch = MathUtil.RadiansToDegrees(MeshContext.Camera.Pitch);
                CameraYaw = MathUtil.RadiansToDegrees(MeshContext.Camera.Yaw);
            }
            else if (UseRadians)
            {
                CameraPitch = MeshContext.Camera.Pitch;
                CameraYaw = MeshContext.Camera.Yaw;
            }
            else if (UseUnreal)
            {
                CameraPitch = MeshContext.Camera.Pitch.RadiansToUnrealRotationUnits();
                CameraYaw = MeshContext.Camera.Yaw.RadiansToUnrealRotationUnits();
            }

            CameraX = eyePosition.X;
            CameraY = eyePosition.Z; // Z and Y are switched to put the UI coordinates into Unreal Z-up coords
            CameraZ = eyePosition.Y;

            CameraFOV = MathUtil.RadiansToDegrees(MeshContext.Camera.FOV);
            CameraZNear = MeshContext.Camera.ZNear;
            CameraZFar = MeshContext.Camera.ZFar;
        }

        private void BackgroundColorPicker_Changed(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (!startingUp && e.NewValue.HasValue)
            {
                var s = e.NewValue.Value.ToString();
                Settings.Meshplorer_BackgroundColor = s;
                Settings.Save();
                MeshContext.BackgroundColor = System.Windows.Media.Color.FromRgb(e.NewValue.Value.R, e.NewValue.Value.G, e.NewValue.Value.B);
            }
        }

        public override void UnloadExport()
        {
            IsBrush = false;
            IsSkeletalMesh = false;
            IsStaticMesh = false;
            IsModel = false;
            CurrentLoadedExport = null;
            STMCollisionMesh?.Dispose();
            STMCollisionMesh = null;
            LEXPreview?.Dispose();
            LEXPreview = null;
            GameShaderPreview?.Dispose();
            GameShaderPreview = null;
            SceneViewer?.Context?.EmptyCaches();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new MeshRenderer(), CurrentLoadedExport)
                {
                    Title = $"Mesh Renderer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            if (Parent is TabItem { Parent: TabControl tc })
            {
                tc.SelectionChanged -= MeshRendererWPF_HostingTabSelectionChanged;
            }
            STMCollisionMesh?.Dispose();
            STMCollisionMesh = null;
            LEXPreview?.Dispose();
            LEXPreview = null;
            GameShaderPreview?.Dispose();
            GameShaderPreview = null;
            if (SceneViewer is { Context: not null })
            {
                MeshContext.RenderScene -= SceneContext_RenderScene;
                MeshContext.UpdateScene -= SceneContext_UpdateScene;
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
