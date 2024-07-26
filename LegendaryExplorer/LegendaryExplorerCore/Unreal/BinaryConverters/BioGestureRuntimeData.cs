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

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref m_mapAnimSetOwners, sc.Serialize, sc.Serialize);
            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref m_mapCharTypeOverrides, sc.Serialize, sc.Serialize);
            }
            else
            {
                sc.Serialize(ref m_mapMeshProps, sc.Serialize, sc.Serialize);
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

    public partial class SerializingContainer
    {
        public void Serialize(ref BioGestureRuntimeData.BioPropClientEffectParams p)
        {
            if (IsLoading)
            {
                p = new BioGestureRuntimeData.BioPropClientEffectParams();
            }
            Serialize(ref p.vHitLocation);
            Serialize(ref p.vHitNormal);
            Serialize(ref p.nmHitBone);
            Serialize(ref p.vRayDir);
            Serialize(ref p.vSpawnValue);
        }
        public void Serialize(ref BioGestureRuntimeData.BioMeshPropActionData d)
        {
            if (IsLoading)
            {
                d = new BioGestureRuntimeData.BioMeshPropActionData();
            }
            Serialize(ref d.nmActionName);
            if (Game.IsGame2())
            {
                Serialize(ref d.sClientEffect);
            }
            Serialize(ref d.bActivate);
            Serialize(ref d.nmAttachTo);
            Serialize(ref d.vOffsetLocation);
            Serialize(ref d.rOffsetRotation);
            Serialize(ref d.vOffsetScale);
            if (Game.IsGame3())
            {
                Serialize(ref d.sParticleSys);
                Serialize(ref d.sClientEffect);
                Serialize(ref d.bCooldown);
                Serialize(ref d.TSpawnParams);
            }
        }
        public void Serialize(ref BioGestureRuntimeData.BioMeshPropData d)
        {
            if (IsLoading)
            {
                d = new BioGestureRuntimeData.BioMeshPropData();
            }
            Serialize(ref d.nmPropName);
            Serialize(ref d.sMesh);
            Serialize(ref d.nmAttachTo);
            Serialize(ref d.vOffsetLocation);
            Serialize(ref d.rOffsetRotation);
            Serialize(ref d.vOffsetScale);
            Serialize(ref d.mapActions, Serialize, Serialize);
        }
        public void Serialize(ref BioGestureRuntimeData.BioGestCharOverride o)
        {
            if (IsLoading)
            {
                o = new BioGestureRuntimeData.BioGestCharOverride();
            }
            Serialize(ref o.nm_Female);
            Serialize(ref o.nm_Asari);
            Serialize(ref o.nm_Turian);
            Serialize(ref o.nm_Salarian);
            Serialize(ref o.nm_Quarian);
            Serialize(ref o.nm_Other);
            Serialize(ref o.nm_Krogan);
            Serialize(ref o.nm_Geth);
            Serialize(ref o.nm_Other_Artificial);
        }
    }
}