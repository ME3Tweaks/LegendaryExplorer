using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Animation;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders
using VertexType = LegendaryExplorer.Tools.LevelEditor.Scene3D.LEVertex;

namespace LegendaryExplorer.Tools.LevelEditor;

public class SkeletalMeshComponentProxy : MeshComponentProxy
{
    SkinnedMeshRenderer skinnedMeshRenderer;
    AnimSequencePlayer animPlayer;
    private PreviewMeshDependencies Dependencies;
    private AnimSequence CurrentAnimation;
    private float AnimationPosition;

    /// <summary>
    /// When set, the component is transformed relative to this component instead of its actor (ParentAnimComponent with bTransformFromAnimParent).
    /// Must come before this component in its actor's <see cref="ActorProxy.Components"/>, so that it's updated first
    /// </summary>
    private PrimitiveComponentProxy TransformParent;

    protected override Matrix4x4 ParentToWorld => TransformParent?.LocalToWorld ?? base.ParentToWorld;

    //a preview's: the component its ParentAnimComponent is drawn as, which its pending properties can't refer to
    private readonly PrimitiveComponentProxy previewAnimParent;

    /// <param name="previewProperties">For a preview (a clone's, until its commit makes the component): its values, which its export (a stand-in) doesn't
    /// have. Its mesh and materials are <paramref name="previewAssets"/>, and its ParentAnimComponent <paramref name="previewAnimParent"/></param>
    public SkeletalMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
        PreviewAssets previewAssets = null, PrimitiveComponentProxy previewAnimParent = null) : base(context, componentExport, parent, previewProperties, previewAssets)
    {
        this.previewAnimParent = previewAnimParent;
        LoadMesh();
    }

    private void LoadMesh()
    {
        var context = RenderContext;
        var parent = Actor;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        TransformParent = null;
        // LE1 declares it an int (Engine.SkeletalMeshComponent.bTransformFromAnimParent is an IntProperty)
        bool bTransformFromAnimParent = Properties.GetProp<Property>("bTransformFromAnimParent") switch
        {
            BoolProperty flag => flag.Value,
            IntProperty flag => flag.Value != 0,
            _ => true
        };
        var parentAnimComponent = PreviewProperties is not null ? previewAnimParent
            : Properties.GetProp<ObjectProperty>("ParentAnimComponent")?.ResolveToEntry(Export.FileRef) is ExportEntry parentAnimExport
                ? parent.Components.FirstOrDefault(cmp => cmp.Export == parentAnimExport) : null;
        if (bTransformFromAnimParent && parentAnimComponent is not null)
        {
            TransformParent = parentAnimComponent;
            base.UpdateLocalToWorld();
        }
        //like the static mesh, it may come from an archetype in another package
        var meshExport = ResolveMesh("SkeletalMesh", Dependencies);
        if (meshExport is not null)
        {
            SkeletalMesh skm = meshExport.GetBinaryData<SkeletalMesh>();
            bool usesLightEnvironment = false;
            DynamicLightEnvironment lightEnvironment = null;
            if (skm.LODModels.Length > LOD)
            {
                foreach (int material in skm.Materials)
                    Dependencies.Track(meshExport.FileRef.GetEntry(material));
                //Without a light environment (or with a disabled one), the mesh is lit by the level's lights directly
                MeshStaticLighting staticLighting = null;
                if (Export.Game.IsLEGame())
                {
                    usesLightEnvironment = context.ResolveLightEnvironment(Export, Properties, parent?.Export, out lightEnvironment, out string unsupportedReason);
                    UsesUnsupportedLightEnvironment = usesLightEnvironment && lightEnvironment is null;
                    UnsupportedLightEnvironmentReason = unsupportedReason;
                    if (!usesLightEnvironment)
                    {
                        staticLighting = MeshStaticLighting.CreateDynamic(context, Export, Properties);
                        if (staticLighting is not null) staticLighting.IsDeleted = IsDeleted;
                    }
                }
                Mesh = new ModelPreview<VertexType>(context, skm, staticLighting, LOD, MaterialOverrides);
                StaticLighting = staticLighting;
                MaterialOverrides.Clear();
                MeshIFP = meshExport.InstancedFullPath;
                skinnedMeshRenderer = new SkinnedMeshRenderer();
                skinnedMeshRenderer.BuildFromSkeletalMesh(meshExport.FileRef.Game, skm.LODModels[LOD]);
                animPlayer = new AnimSequencePlayer(skm);
            }
            UpdateSelfLocalToWorld();
            if (Mesh is not null && lightEnvironment is not null)
            {
                //MeshComponent's defaults: CastShadow, bCastDynamicShadow
                bool castsShadow = Properties.GetProp<BoolProperty>("CastShadow") is not { Value: false }
                                   && Properties.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
                var boneNames = new HashSet<string>(skm.RefSkeleton.Select(bone => bone.Name.Instanced), StringComparer.OrdinalIgnoreCase);
                JoinLightEnvironment(context, lightEnvironment, LightingChannels.FromProperty(Properties.GetProp<StructProperty>("LightingChannels"), default,
                    isInitialized: false, LightingChannels.DynamicPrimitiveDefault), castsShadow, hasBone: boneNames.Contains);
            }
        }
    }

    public override bool RefreshPreview(IMEPackage package, IReadOnlySet<int> updatedExports, bool force = false)
    {
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        ReleaseMesh();
        skinnedMeshRenderer = null;
        animPlayer = null;
        MeshIFP = null;
        ReadProperties();
        ReadMaterialOverrides();
        LoadMesh();
        // Its actor's class defaults and morph face. (Its transform is updated by the actor after the refresh)
        Actor?.OnComponentReloaded(this);
        SetAnimation(CurrentAnimation, AnimationPosition);
        return true;
    }

    public override void UpdateScene(MeshRenderContext context, float deltaTime)
    {
        if (Mesh is not null && skinnedMeshRenderer.NeedsUpdate)
        {
            skinnedMeshRenderer.UpdateSkinning(context.ImmediateContext, Mesh.LODs[LOD].Mesh, animPlayer);
        }
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (!context.IsComponentVisible(this)) return;
        Mesh?.Render(pass, context, LOD);
    }

    public void SetAnimation(AnimSequence animSequence, float pos)
    {
        CurrentAnimation = animSequence;
        AnimationPosition = pos;
        if (animPlayer is null) return;
        if (animSequence is null)
        {
            if (animPlayer.HasAnimation)
            {
                //cancel animation, reset to ref pose
                animPlayer.SetAnimation(null);
                skinnedMeshRenderer.NeedsUpdate = true;
            }
            return;
        }
        if (animSequence.Name != animPlayer.AnimName)
        {
            animPlayer.SetAnimation(animSequence);
        }
        animPlayer.SetCurrentTime(pos);
        skinnedMeshRenderer.NeedsUpdate = true;
    }

    public void ApplyMorph(LegendaryExplorerCore.Unreal.Classes.BonePosition[] bonePositions, Vector3[][] morphLods)
    {
        if (Mesh is null) return;
        if (morphLods?.Length > LOD)
        {
            skinnedMeshRenderer.UpdateVertexPositions(morphLods[LOD]);
            skinnedMeshRenderer.NeedsUpdate = true;
        }
        if (bonePositions is not null && animPlayer is not null)
        {
            animPlayer.ApplyBonePositions(bonePositions);
            skinnedMeshRenderer.NeedsUpdate = true;
        }
    }

    public override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        UpdateSelfLocalToWorld();
    }

    private void UpdateSelfLocalToWorld()
    {
        Mesh?.UpdateLocalToWorld(LocalToWorld);
    }
}
