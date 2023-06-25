using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioGestureRuntimeData : ObjectBinary
    {
        public UMultiMap<NameReference, NameReference> m_mapAnimSetOwners;//ME3/2  //TODO: Make this a UMap
        public UMultiMap<NameReference, BioMeshPropData> m_mapMeshProps;//ME3/2  //TODO: Make this a UMap
        public UMultiMap<NameReference, BioGestCharOverride> m_mapCharTypeOverrides;//ME1  //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref m_mapAnimSetOwners, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref m_mapCharTypeOverrides, SCExt.Serialize, SCExt.Serialize);
            }
            else
            {
                sc.Serialize(ref m_mapMeshProps, SCExt.Serialize, SCExt.Serialize);
            }
        }

        public static BioGestureRuntimeData Create()
        {
            return new()
            {
                m_mapAnimSetOwners = new(),
                m_mapMeshProps = new(),
                m_mapCharTypeOverrides = new()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            List<(NameReference, string)> names = base.GetNames(game);

            int i = 0;
            foreach ((NameReference key, NameReference value) in m_mapAnimSetOwners)
            {
                names.Add((key, $"{nameof(m_mapAnimSetOwners)}[{i}].Key"));
                names.Add((value, $"{nameof(m_mapAnimSetOwners)}[{i}].Value"));
                i++;
            }

            if (game.IsGame1())
            {
                i = 0;
                foreach ((NameReference key, BioGestCharOverride value) in m_mapCharTypeOverrides)
                {
                    names.Add((key, $"{nameof(m_mapCharTypeOverrides)}[{i}].Key"));
                    names.Add((value.nm_Female, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Female)}"));
                    names.Add((value.nm_Asari, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Asari)}"));
                    names.Add((value.nm_Turian, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Turian)}"));
                    names.Add((value.nm_Salarian, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Salarian)}"));
                    names.Add((value.nm_Quarian, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Quarian)}"));
                    names.Add((value.nm_Other, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Other)}"));
                    names.Add((value.nm_Krogan, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Krogan)}"));
                    names.Add((value.nm_Geth, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Geth)}"));
                    names.Add((value.nm_Other_Artificial, $"{nameof(m_mapCharTypeOverrides)}[{i}].Value.{nameof(BioGestCharOverride.nm_Other_Artificial)}"));
                    i++;
                }
            }
            else
            {
                i = 0;
                foreach ((NameReference key, BioMeshPropData value) in m_mapMeshProps)
                {
                    names.Add((key, $"{nameof(m_mapMeshProps)}[{i}].Key"));
                    names.Add((value.nmPropName, $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.nmPropName)}"));
                    names.Add((value.nmAttachTo, $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.nmAttachTo)}"));
                    int j = 0;
                    foreach ((NameReference innerKey, BioMeshPropActionData innerValue) in value.mapActions)
                    {
                        names.Add((innerKey, $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.mapActions)}.Key"));
                        names.Add((innerValue.nmActionName, $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.mapActions)}[{j}].Value.{nameof(BioMeshPropActionData.nmActionName)}"));
                        names.Add((innerValue.nmAttachTo, $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.mapActions)}[{j}].Value.{nameof(BioMeshPropActionData.nmAttachTo)}"));
                        if (game.IsGame3())
                        {
                            names.Add((innerValue.TSpawnParams.nmHitBone,
                                $"{nameof(m_mapMeshProps)}[{i}].Value.{nameof(BioMeshPropData.mapActions)}[{j}].Value.{nameof(BioMeshPropActionData.TSpawnParams)}.{nameof(BioPropClientEffectParams.nmHitBone)}"));
                        }
                        j++;
                    }
                    i++;
                }
            }

            return names;
        }

        public class BioMeshPropData
        {
            public NameReference nmPropName;
            public string sMesh;
            public NameReference nmAttachTo;
            public Vector3 vOffsetLocation;
            public Rotator rOffsetRotation;
            public Vector3 vOffsetScale;
            public UMultiMap<NameReference, BioMeshPropActionData> mapActions; //TODO: Make this a UMap
        }

        public class BioMeshPropActionData
        {
            public NameReference nmActionName;
            public bool bActivate;
            public NameReference nmAttachTo;
            public Vector3 vOffsetLocation;
            public Rotator rOffsetRotation;
            public Vector3 vOffsetScale;
            public string sParticleSys;//ME3
            public string sClientEffect;
            public bool bCooldown;//ME3
            public BioPropClientEffectParams TSpawnParams;//ME3
        }

        public class BioPropClientEffectParams
        {
            public Vector3 vHitLocation;
            public Vector3 vHitNormal;
            public NameReference nmHitBone;
            public Vector3 vRayDir;
            public Vector3 vSpawnValue;
        }

        public class BioGestCharOverride
        {
            public NameReference nm_Female;
            public NameReference nm_Asari;
            public NameReference nm_Turian;
            public NameReference nm_Salarian;
            public NameReference nm_Quarian;
            public NameReference nm_Other;
            public NameReference nm_Krogan;
            public NameReference nm_Geth;
            public NameReference nm_Other_Artificial;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref BioGestureRuntimeData.BioPropClientEffectParams p)
        {
            if (sc.IsLoading)
            {
                p = new BioGestureRuntimeData.BioPropClientEffectParams();
            }
            sc.Serialize(ref p.vHitLocation);
            sc.Serialize(ref p.vHitNormal);
            sc.Serialize(ref p.nmHitBone);
            sc.Serialize(ref p.vRayDir);
            sc.Serialize(ref p.vSpawnValue);
        }
        public static void Serialize(this SerializingContainer2 sc, ref BioGestureRuntimeData.BioMeshPropActionData d)
        {
            if (sc.IsLoading)
            {
                d = new BioGestureRuntimeData.BioMeshPropActionData();
            }
            sc.Serialize(ref d.nmActionName);
            if (sc.Game.IsGame2())
            {
                sc.Serialize(ref d.sClientEffect);
            }
            sc.Serialize(ref d.bActivate);
            sc.Serialize(ref d.nmAttachTo);
            sc.Serialize(ref d.vOffsetLocation);
            sc.Serialize(ref d.rOffsetRotation);
            sc.Serialize(ref d.vOffsetScale);
            if (sc.Game.IsGame3())
            {
                sc.Serialize(ref d.sParticleSys);
                sc.Serialize(ref d.sClientEffect);
                sc.Serialize(ref d.bCooldown);
                sc.Serialize(ref d.TSpawnParams);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref BioGestureRuntimeData.BioMeshPropData d)
        {
            if (sc.IsLoading)
            {
                d = new BioGestureRuntimeData.BioMeshPropData();
            }
            sc.Serialize(ref d.nmPropName);
            sc.Serialize(ref d.sMesh);
            sc.Serialize(ref d.nmAttachTo);
            sc.Serialize(ref d.vOffsetLocation);
            sc.Serialize(ref d.rOffsetRotation);
            sc.Serialize(ref d.vOffsetScale);
            sc.Serialize(ref d.mapActions, Serialize, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref BioGestureRuntimeData.BioGestCharOverride o)
        {
            if (sc.IsLoading)
            {
                o = new BioGestureRuntimeData.BioGestCharOverride();
            }
            sc.Serialize(ref o.nm_Female);
            sc.Serialize(ref o.nm_Asari);
            sc.Serialize(ref o.nm_Turian);
            sc.Serialize(ref o.nm_Salarian);
            sc.Serialize(ref o.nm_Quarian);
            sc.Serialize(ref o.nm_Other);
            sc.Serialize(ref o.nm_Krogan);
            sc.Serialize(ref o.nm_Geth);
            sc.Serialize(ref o.nm_Other_Artificial);
        }
    }
}