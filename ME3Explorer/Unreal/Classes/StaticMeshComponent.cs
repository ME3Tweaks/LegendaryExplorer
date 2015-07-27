using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using lib3ds.Net;

namespace ME3Explorer.Unreal.Classes
{
    public class StaticMeshComponent
    {
#region Unreal Props
	
	//Byte Properties

	public int LightMapEncoding;
	public int RBChannel;
	public int DepthPriorityGroup;
	public int TickGroup;
	//Bool Properties

	public bool bIgnoreInstanceForTextureStreaming = false;
	public bool CastShadow = false;
	public bool CollideActors = false;
	public bool BlockRigidBody = false;
	public bool bForceDirectLightMap = false;
	public bool bCastDynamicShadow = false;
	public bool bAcceptsDynamicDominantLightShadows = false;
	public bool bAcceptsLights = false;
	public bool bAcceptsDynamicLights = false;
	public bool bCullModulatedShadowOnBackfaces = false;
	public bool bCullModulatedShadowOnEmissive = false;
	public bool bAllowAmbientOcclusion = false;
	public bool bUsePrecomputedShadows = false;
	public bool CanBlockCamera = false;
	public bool bAllowShadowFade = false;
	public bool bBioIsReceivingDecals = false;
	public bool BlockNonZeroExtent = false;
	public bool bAcceptsStaticDecals = false;
	public bool bAcceptsDynamicDecals = false;
	public bool bAcceptsFoliage = false;
	public bool HiddenGame = false;
	public bool bBioForcePrecomputedShadows = false;
	public bool bCastHiddenShadow = false;
	public bool bUseAsOccluder = false;
	public bool BlockZeroExtent = false;
	public bool bAllowCullDistanceVolume = false;
	public bool bAllowApproximateOcclusion = false;
	public bool bSelfShadowOnly = false;
	public bool OverridePhysMat = false;
	public bool bUseOnePassLightingOnTranslucency = false;
	public bool bLockLightingCache = false;
	public bool bDisableAllRigidBody = false;
	public bool BlockActors = false;
	public bool bNotifyRigidBodyCollision = false;
	public bool bIgnoreRadialImpulse = false;
	public bool bIgnoreRadialForce = false;
	public bool HiddenEditor = false;
	//Object Properties

	public int StaticMesh_;
	public int ReplacementPrimitive;
	public int LightEnvironment;
	public int ShadowParent;
	public int PhysMaterialOverride;
	//Float Properties

	public float MaxDrawDistance;
	public float CachedMaxDrawDistance;
	public float MinDrawDistance;
	public float AudioObstruction;
	public float AudioOcclusion;
	public float OverriddenLODMaxRange;
	public float Scale = 1.0f;
	public float MassiveLODDistance;
	public float MotionBlurScale;
	//Integer Properties

	public int TranslucencySortPriority;
	public int LocalTranslucencySortPriority;
	public int ForcedLodModel;

    //Vector Properties
    public Vector3 Scale3D = new Vector3(1, 1, 1);
    public Vector3 Rotation = new Vector3(0, 0, 0);
    public Vector3 Translation = new Vector3(0, 0, 0);

#endregion

        public int MyIndex;
        public PCCObject pcc;
        public byte[] data;
        public List<PropertyReader.Property> Props;
        public StaticMesh STM;
        public Matrix MyMatrix;

        public StaticMeshComponent (PCCObject Pcc, int Index)
        {
            pcc = Pcc;
            MyIndex = Index;
            if (pcc.isExport(Index))
                data = pcc.Exports[Index].Data;
            Props = PropertyReader.getPropList(pcc, data);
            BitConverter.IsLittleEndian = true;
            foreach (PropertyReader.Property p in Props)
                switch (pcc.getNameEntry(p.Name))
                {
                    #region
                    case "LightMapEncoding":
						LightMapEncoding = p.Value.IntValue;
						break;
					case "RBChannel":
						RBChannel = p.Value.IntValue;
						break;
					case "DepthPriorityGroup":
						DepthPriorityGroup = p.Value.IntValue;
						break;
					case "TickGroup":
						TickGroup = p.Value.IntValue;
						break;
					case "bIgnoreInstanceForTextureStreaming":
						if (p.raw[p.raw.Length - 1] == 1)
						bIgnoreInstanceForTextureStreaming = true;
						break;
					case "CastShadow":
						if (p.raw[p.raw.Length - 1] == 1)
						CastShadow = true;
						break;
					case "CollideActors":
						if (p.raw[p.raw.Length - 1] == 1)
						CollideActors = true;
						break;
					case "BlockRigidBody":
						if (p.raw[p.raw.Length - 1] == 1)
						BlockRigidBody = true;
						break;
					case "bForceDirectLightMap":
						if (p.raw[p.raw.Length - 1] == 1)
						bForceDirectLightMap = true;
						break;
					case "bCastDynamicShadow":
						if (p.raw[p.raw.Length - 1] == 1)
						bCastDynamicShadow = true;
						break;
					case "bAcceptsDynamicDominantLightShadows":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsDynamicDominantLightShadows = true;
						break;
					case "bAcceptsLights":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsLights = true;
						break;
					case "bAcceptsDynamicLights":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsDynamicLights = true;
						break;
					case "bCullModulatedShadowOnBackfaces":
						if (p.raw[p.raw.Length - 1] == 1)
						bCullModulatedShadowOnBackfaces = true;
						break;
					case "bCullModulatedShadowOnEmissive":
						if (p.raw[p.raw.Length - 1] == 1)
						bCullModulatedShadowOnEmissive = true;
						break;
					case "bAllowAmbientOcclusion":
						if (p.raw[p.raw.Length - 1] == 1)
						bAllowAmbientOcclusion = true;
						break;
					case "bUsePrecomputedShadows":
						if (p.raw[p.raw.Length - 1] == 1)
						bUsePrecomputedShadows = true;
						break;
					case "CanBlockCamera":
						if (p.raw[p.raw.Length - 1] == 1)
						CanBlockCamera = true;
						break;
					case "bAllowShadowFade":
						if (p.raw[p.raw.Length - 1] == 1)
						bAllowShadowFade = true;
						break;
					case "bBioIsReceivingDecals":
						if (p.raw[p.raw.Length - 1] == 1)
						bBioIsReceivingDecals = true;
						break;
					case "BlockNonZeroExtent":
						if (p.raw[p.raw.Length - 1] == 1)
						BlockNonZeroExtent = true;
						break;
					case "bAcceptsStaticDecals":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsStaticDecals = true;
						break;
					case "bAcceptsDynamicDecals":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsDynamicDecals = true;
						break;
					case "bAcceptsFoliage":
						if (p.raw[p.raw.Length - 1] == 1)
						bAcceptsFoliage = true;
						break;
					case "HiddenGame":
						if (p.raw[p.raw.Length - 1] == 1)
						HiddenGame = true;
						break;
					case "bBioForcePrecomputedShadows":
						if (p.raw[p.raw.Length - 1] == 1)
						bBioForcePrecomputedShadows = true;
						break;
					case "bCastHiddenShadow":
						if (p.raw[p.raw.Length - 1] == 1)
						bCastHiddenShadow = true;
						break;
					case "bUseAsOccluder":
						if (p.raw[p.raw.Length - 1] == 1)
						bUseAsOccluder = true;
						break;
					case "BlockZeroExtent":
						if (p.raw[p.raw.Length - 1] == 1)
						BlockZeroExtent = true;
						break;
					case "bAllowCullDistanceVolume":
						if (p.raw[p.raw.Length - 1] == 1)
						bAllowCullDistanceVolume = true;
						break;
					case "bAllowApproximateOcclusion":
						if (p.raw[p.raw.Length - 1] == 1)
						bAllowApproximateOcclusion = true;
						break;
					case "bSelfShadowOnly":
						if (p.raw[p.raw.Length - 1] == 1)
						bSelfShadowOnly = true;
						break;
					case "OverridePhysMat":
						if (p.raw[p.raw.Length - 1] == 1)
						OverridePhysMat = true;
						break;
					case "bUseOnePassLightingOnTranslucency":
						if (p.raw[p.raw.Length - 1] == 1)
						bUseOnePassLightingOnTranslucency = true;
						break;
					case "bLockLightingCache":
						if (p.raw[p.raw.Length - 1] == 1)
						bLockLightingCache = true;
						break;
					case "bDisableAllRigidBody":
						if (p.raw[p.raw.Length - 1] == 1)
						bDisableAllRigidBody = true;
						break;
					case "BlockActors":
						if (p.raw[p.raw.Length - 1] == 1)
						BlockActors = true;
						break;
					case "bNotifyRigidBodyCollision":
						if (p.raw[p.raw.Length - 1] == 1)
						bNotifyRigidBodyCollision = true;
						break;
					case "bIgnoreRadialImpulse":
						if (p.raw[p.raw.Length - 1] == 1)
						bIgnoreRadialImpulse = true;
						break;
					case "bIgnoreRadialForce":
						if (p.raw[p.raw.Length - 1] == 1)
						bIgnoreRadialForce = true;
						break;
					case "HiddenEditor":
						if (p.raw[p.raw.Length - 1] == 1)
						HiddenEditor = true;
						break;
					case "StaticMesh":
						StaticMesh_ = p.Value.IntValue;
						break;
					case "ReplacementPrimitive":
						ReplacementPrimitive = p.Value.IntValue;
						break;
					case "LightEnvironment":
						LightEnvironment = p.Value.IntValue;
						break;
					case "ShadowParent":
						ShadowParent = p.Value.IntValue;
						break;
					case "PhysMaterialOverride":
						PhysMaterialOverride = p.Value.IntValue;
						break;
					case "MaxDrawDistance":
						MaxDrawDistance = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "CachedMaxDrawDistance":
						CachedMaxDrawDistance = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "MinDrawDistance":
						MinDrawDistance = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "AudioObstruction":
						AudioObstruction = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "AudioOcclusion":
						AudioOcclusion = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "OverriddenLODMaxRange":
						OverriddenLODMaxRange = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "Scale":
						Scale = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
                    case "Scale3D":
                        Scale3D = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    case "Rotation":
                        Rotation = new Vector3(BitConverter.ToInt32(p.raw, p.raw.Length - 12),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 8),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 4));
                        break;
                    case "Translation":
                        Translation = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
					case "MassiveLODDistance":
						MassiveLODDistance = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "MotionBlurScale":
						MotionBlurScale = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
						break;
					case "TranslucencySortPriority":
						TranslucencySortPriority = p.Value.IntValue;
						break;
					case "LocalTranslucencySortPriority":
						LocalTranslucencySortPriority = p.Value.IntValue;
						break;
					case "ForcedLodModel":
						ForcedLodModel = p.Value.IntValue;
						break;
                    #endregion
                }
            if (StaticMesh_ - 1 >= 0 && StaticMesh_ - 1 < pcc.Exports.Count)
                if (pcc.Exports[StaticMesh_ - 1].ClassName == "StaticMesh")
                {
                    STM = new StaticMesh(pcc, StaticMesh_ - 1);
                    STM.Mesh.Bounds.t = null;//save memory
                    STM.Mesh.Edges.t = null;
                    STM.Mesh.Buffers.t = null;
                    STM.Mesh.IdxBuf.t = null;
                    STM.Mesh.kDOPTree.t = null;
                    STM.Mesh.Mat.t = null;
                    STM.Mesh.RawTris.t = null;
                    STM.Mesh.UnknownPart.t = null;
                    STM.Mesh.Vertices.t = null;
                }
            MyMatrix = Matrix.Identity;
            MyMatrix *=  Matrix.Scaling(Scale3D);
            MyMatrix *=  Matrix.Scaling(Scale, Scale, Scale);
            Vector3 rot = RotatorToDX(Rotation);
            MyMatrix *= Matrix.RotationYawPitchRoll(rot.X, rot.Y, rot.Z);
            MyMatrix *= Matrix.Translation(Translation);
        }

        public Vector3 RotatorToDX(Vector3 v)
        {
            Vector3 r = v;
            r.X = (int)r.X % 65536;
            r.Y = (int)r.Y % 65536;
            r.Z = (int)r.Z % 65536;
            float f = (3.1415f * 2f) / 65536f;
            r.X = v.Z * f;
            r.Y = v.X * f;
            r.Z = v.Y * f;
            return r;
        }

        public void Render(Device device, Matrix m)
        {
            if (STM != null)
            {
                Matrix t = MyMatrix * m;
                STM.Render(device, t);
            }
        }

        public void SetSelection(bool Selected)
        {
            if (STM != null)
                STM.SetSelection(Selected);
        }

        public bool GetSelection()
        {
            if (STM != null)
                return STM.GetSelection();
            return false;
        }

        public void Focus(Matrix m)
        {
            if (STM != null)
            {
                Matrix t = MyMatrix * m;
                STM.Focus(t);
            }
        }

        public float Process3DClick(Vector3 org, Vector3 dir, Matrix m)
        {
            if (STM != null)
            {
                Matrix t = MyMatrix * m;
                return STM.Process3DClick(org, dir, t);                
            }
            return -1f;
        }

        public void Export3DS(Lib3dsFile f, Matrix m)
        {
            if (STM != null)
            {
                Matrix t = MyMatrix * m;
                STM.Export3DS(f, t);
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("#" + MyIndex + " : " + pcc.Exports[MyIndex].ObjectName);
			res.Nodes.Add("LightMapEncoding : " + pcc.getNameEntry(LightMapEncoding));
			res.Nodes.Add("RBChannel : " + pcc.getNameEntry(RBChannel));
			res.Nodes.Add("DepthPriorityGroup : " + pcc.getNameEntry(DepthPriorityGroup));
			res.Nodes.Add("TickGroup : " + pcc.getNameEntry(TickGroup));
			res.Nodes.Add("bIgnoreInstanceForTextureStreaming : " + bIgnoreInstanceForTextureStreaming);
			res.Nodes.Add("CastShadow : " + CastShadow);
			res.Nodes.Add("CollideActors : " + CollideActors);
			res.Nodes.Add("BlockRigidBody : " + BlockRigidBody);
			res.Nodes.Add("bForceDirectLightMap : " + bForceDirectLightMap);
			res.Nodes.Add("bCastDynamicShadow : " + bCastDynamicShadow);
			res.Nodes.Add("bAcceptsDynamicDominantLightShadows : " + bAcceptsDynamicDominantLightShadows);
			res.Nodes.Add("bAcceptsLights : " + bAcceptsLights);
			res.Nodes.Add("bAcceptsDynamicLights : " + bAcceptsDynamicLights);
			res.Nodes.Add("bCullModulatedShadowOnBackfaces : " + bCullModulatedShadowOnBackfaces);
			res.Nodes.Add("bCullModulatedShadowOnEmissive : " + bCullModulatedShadowOnEmissive);
			res.Nodes.Add("bAllowAmbientOcclusion : " + bAllowAmbientOcclusion);
			res.Nodes.Add("bUsePrecomputedShadows : " + bUsePrecomputedShadows);
			res.Nodes.Add("CanBlockCamera : " + CanBlockCamera);
			res.Nodes.Add("bAllowShadowFade : " + bAllowShadowFade);
			res.Nodes.Add("bBioIsReceivingDecals : " + bBioIsReceivingDecals);
			res.Nodes.Add("BlockNonZeroExtent : " + BlockNonZeroExtent);
			res.Nodes.Add("bAcceptsStaticDecals : " + bAcceptsStaticDecals);
			res.Nodes.Add("bAcceptsDynamicDecals : " + bAcceptsDynamicDecals);
			res.Nodes.Add("bAcceptsFoliage : " + bAcceptsFoliage);
			res.Nodes.Add("HiddenGame : " + HiddenGame);
			res.Nodes.Add("bBioForcePrecomputedShadows : " + bBioForcePrecomputedShadows);
			res.Nodes.Add("bCastHiddenShadow : " + bCastHiddenShadow);
			res.Nodes.Add("bUseAsOccluder : " + bUseAsOccluder);
			res.Nodes.Add("BlockZeroExtent : " + BlockZeroExtent);
			res.Nodes.Add("bAllowCullDistanceVolume : " + bAllowCullDistanceVolume);
			res.Nodes.Add("bAllowApproximateOcclusion : " + bAllowApproximateOcclusion);
			res.Nodes.Add("bSelfShadowOnly : " + bSelfShadowOnly);
			res.Nodes.Add("OverridePhysMat : " + OverridePhysMat);
			res.Nodes.Add("bUseOnePassLightingOnTranslucency : " + bUseOnePassLightingOnTranslucency);
			res.Nodes.Add("bLockLightingCache : " + bLockLightingCache);
			res.Nodes.Add("bDisableAllRigidBody : " + bDisableAllRigidBody);
			res.Nodes.Add("BlockActors : " + BlockActors);
			res.Nodes.Add("bNotifyRigidBodyCollision : " + bNotifyRigidBodyCollision);
			res.Nodes.Add("bIgnoreRadialImpulse : " + bIgnoreRadialImpulse);
			res.Nodes.Add("bIgnoreRadialForce : " + bIgnoreRadialForce);
			res.Nodes.Add("HiddenEditor : " + HiddenEditor);
			res.Nodes.Add("StaticMesh : " + StaticMesh_);
			res.Nodes.Add("ReplacementPrimitive : " + ReplacementPrimitive);
			res.Nodes.Add("LightEnvironment : " + LightEnvironment);
			res.Nodes.Add("ShadowParent : " + ShadowParent);
			res.Nodes.Add("PhysMaterialOverride : " + PhysMaterialOverride);
			res.Nodes.Add("MaxDrawDistance : " + MaxDrawDistance);
			res.Nodes.Add("CachedMaxDrawDistance : " + CachedMaxDrawDistance);
			res.Nodes.Add("MinDrawDistance : " + MinDrawDistance);
			res.Nodes.Add("AudioObstruction : " + AudioObstruction);
			res.Nodes.Add("AudioOcclusion : " + AudioOcclusion);
			res.Nodes.Add("OverriddenLODMaxRange : " + OverriddenLODMaxRange);
			res.Nodes.Add("Scale : " + Scale);
			res.Nodes.Add("MassiveLODDistance : " + MassiveLODDistance);
			res.Nodes.Add("MotionBlurScale : " + MotionBlurScale);
			res.Nodes.Add("TranslucencySortPriority : " + TranslucencySortPriority);
			res.Nodes.Add("LocalTranslucencySortPriority : " + LocalTranslucencySortPriority);
			res.Nodes.Add("ForcedLodModel : " + ForcedLodModel);
            if (STM != null)
            {
                res.Nodes.Add("#" + STM.index + " : " + pcc.Exports[STM.index].ObjectName);
                //res.Nodes.Add(STM.ToTree());
            }
            return res;
        }

    }
}