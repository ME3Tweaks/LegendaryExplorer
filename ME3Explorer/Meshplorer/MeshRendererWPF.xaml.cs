using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
using SharpDX;
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
            set
            {
                if (SetProperty(ref _firstperson, value))
                {
                    SceneViewer.Context.Camera.FirstPerson = value;
                }
            }
        }

        private ModelPreview Preview;
        private int CurrentLOD = 0;
        private float PreviewRotation;
        private bool HasLoaded;
        private WorldMesh STMCollisionMesh;

        private void SceneViewer_Render(object sender, EventArgs e)
        {
            if (Preview != null && Preview.LODs.Count > 0)
            {

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

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

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

        public MeshRendererWPF()
        {
            DataContext = this;
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__") 
                || (exportEntry.ClassName == "BrushComponent" && exportEntry.GetProperty<StructProperty>("BrushAggGeom") != null);
        }

        public override void PoppedOut(MenuItem recentsMenuItem)
        {
            //throw new NotImplementedException();
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            UnloadExport();
            SceneViewer.InitializeD3D();
            SceneViewer.Context.BackgroundColor = new SharpDX.Color(128, 128, 128);

            CurrentLoadedExport = exportEntry;


            Func<ModelPreview.PreloadedModelData> loadMesh = null;
            if (CurrentLoadedExport.ClassName == "StaticMesh")
            {
                IsStaticMesh = true;
                loadMesh = () =>
                {
                    BusyText = "Fetching assets";
                    IsBusy = true;
                    var meshObject = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                    ModelPreview.PreloadedModelData pmd = new ModelPreview.PreloadedModelData
                    {
                        meshObject = meshObject,
                        sections = new List<ModelPreviewSection>(),
                        texturePreviewMaterials = new List<ModelPreview.PreloadedTextureData>()
                    };
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
                        else if (meshFile.isImport(matIndex))
                        {
                            var extMaterialExport = ModelPreview.FindExternalAsset(meshFile.getImport(matIndex), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
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
                            var extMaterialExport = ModelPreview.FindExternalAsset(meshObject.Export.FileRef.getImport(material.value), pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList());
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
                    }
                });
            }
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

        #region CollisionMesh

        private bool _showCollisionMesh;
        public bool ShowCollisionMesh
        {
            get => _showCollisionMesh;
            set => SetProperty(ref _showCollisionMesh, value);
        }

        private int _maxVerts = 12;

        public int MaxVerts
        {
            get => _maxVerts;
            set => SetProperty(ref _maxVerts, value);
        }

        private uint _depth = 4;

        public uint Depth
        {
            get => _depth;
            set => SetProperty(ref _depth, value);
        }

        private double _conservationThreshold = 24.0;

        public double ConservationThreshold
        {
            get => _conservationThreshold;
            set => SetProperty(ref _conservationThreshold, value);
        }

        private StructProperty aggGeomProp;


        private void GenerateCollisionMesh(object sender, RoutedEventArgs e)
        {
            if (IsStaticMesh && (Preview?.LODs.Any() ?? false))
            {
                BusyText = "Generating Collision Mesh";
                IsBusy = true;
                Task.Run(() =>
                {
                    var mesh = Preview.LODs[0].Mesh;
                    return AggGeomBuilder.CreateAggGeom(mesh.Vertices.Select(vert => new Vector3(-vert.Position.X, vert.Position.Z, vert.Position.Y)).ToArray(),
                                                        mesh.Triangles.SelectMany(tri => new[] { tri.Vertex1, tri.Vertex2, tri.Vertex3 }).ToArray(),
                                                        Depth, ConservationThreshold, MaxVerts);
                }).ContinueWithOnUIThread(prevTask =>
                {
                    aggGeomProp = prevTask.Result;
                    STMCollisionMesh = GetMeshFromAggGeom(aggGeomProp);
                    IsBusy = false;
                });
            }
        }

        private void SaveGeneratedMesh(object sender, RoutedEventArgs e)
        {
            if (aggGeomProp == null)
            {
                MessageBox.Show("You must generate collision mesh before you can save it!");
                return;
            }
            ExportEntry rb_BodySetup;
            if (CurrentLoadedExport.GetProperty<ObjectProperty>("BodySetup")?.Value is int bodySetupUIndex && Pcc.isUExport(bodySetupUIndex))
            {
                rb_BodySetup = Pcc.getUExport(bodySetupUIndex);
            }
            else
            {
                rb_BodySetup = new ExportEntry(Pcc, properties:new PropertyCollection{ new IntProperty(34013709, "PreCachedPhysDataVersion") }, binary: new byte[4])
                {
                    Parent = CurrentLoadedExport,
                    ObjectName = "RB_BodySetup",
                    idxClass = Pcc.getEntryOrAddImport("Engine.RB_BodySetup").UIndex
                };
                Pcc.addExport(rb_BodySetup);
                var stm = ObjectBinary.From<StaticMesh>(CurrentLoadedExport);
                stm.BodySetup = rb_BodySetup.UIndex;
                CurrentLoadedExport.setBinaryData(stm.ToArray(Pcc));
                CurrentLoadedExport.WriteProperty(new ObjectProperty(rb_BodySetup, "BodySetup"));
            }

            rb_BodySetup.WriteProperty(aggGeomProp);
        }

        #endregion

        private static void AddMaterialBackgroundThreadTextures(List<ModelPreview.PreloadedTextureData> texturePreviewMaterials, ExportEntry entry)
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
            IsBrush = false;
            IsSkeletalMesh = false;
            IsStaticMesh = false;
            Preview?.Dispose();
            CurrentLoadedExport = null;
            STMCollisionMesh = null;
            aggGeomProp = null;
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
