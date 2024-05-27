using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Model : ObjectBinary
    {
        public BoxSphereBounds Bounds;
        public Vector3[] Vectors;//BulkSerialized 12
        public Vector3[] Points;//BulkSerialized 12
        public BspNode[] Nodes; //BulkSerialized 64
        public UIndex Self;
        public BspSurf[] Surfs;
        public Vert[] Verts;//BulkSerialized 24, ME3: 16
        public int NumSharedSides;
        public ZoneProperties[] Zones;
        public UIndex Polys;
        public int[] LeafHulls; //BulkSerialized 4
        public int[] Leaves; //BulkSerialized 4
        public bool RootOutside;
        public bool Linked;
        public int[] PortalNodes; //BulkSerialized 4
        public MeshEdge[] ShadowVolume; //BulkSerialized 16
        public uint NumVertices;
        public ModelVertex[] VertexBuffer;//BulkSerialized 36
        public Guid LightingGuid;//ME3
        public LightmassPrimitiveSettings[] LightmassSettings;//ME3

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Bounds);
            sc.BulkSerialize(ref Vectors, SCExt.Serialize, 12);
            sc.BulkSerialize(ref Points, SCExt.Serialize, 12);
            sc.BulkSerialize(ref Nodes, SCExt.Serialize, 64);
            sc.Serialize(ref Self);
            sc.Serialize(ref Surfs, SCExt.Serialize);
            sc.BulkSerialize(ref Verts, SCExt.Serialize, sc.Game.IsGame3() ? 16 : 24);
            sc.Serialize(ref NumSharedSides);
            sc.Serialize(ref Zones, SCExt.Serialize);
            sc.Serialize(ref Polys);
            sc.BulkSerialize(ref LeafHulls, SCExt.Serialize, 4);
            sc.BulkSerialize(ref Leaves, SCExt.Serialize, 4);
            sc.Serialize(ref RootOutside);
            sc.Serialize(ref Linked);
            sc.BulkSerialize(ref PortalNodes, SCExt.Serialize, 4);
            if (sc.Game != MEGame.UDK)
            {
                sc.BulkSerialize(ref ShadowVolume, SCExt.Serialize, 16);
            }
            else if (sc.IsLoading)
            {
                ShadowVolume = Array.Empty<MeshEdge>();
            }
            sc.Serialize(ref NumVertices);
            sc.BulkSerialize(ref VertexBuffer, SCExt.Serialize, 36);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref LightingGuid);
                sc.Serialize(ref LightmassSettings, SCExt.Serialize);
            }
            else if (sc.IsLoading)
            {
                LightmassSettings = new[]
                {
                    new LightmassPrimitiveSettings
                    {
                        FullyOccludedSamplesFraction = 1,
                        EmissiveLightFalloffExponent = 2,
                        EmissiveBoost = 1,
                        DiffuseBoost = 1,
                        SpecularBoost = 1
                    }
                };
            }
        }

        public static Model Create()
        {
            return new()
            {
                Bounds = new BoxSphereBounds(),
                Vectors = Array.Empty<Vector3>(),
                Points = Array.Empty<Vector3>(),
                Nodes = Array.Empty<BspNode>(),
                Self = 0,
                Surfs = Array.Empty<BspSurf>(),
                Verts = Array.Empty<Vert>(),
                Zones = Array.Empty<ZoneProperties>(),
                Polys = 0,
                LeafHulls = Array.Empty<int>(),
                Leaves = Array.Empty<int>(),
                PortalNodes = Array.Empty<int>(),
                ShadowVolume = Array.Empty<MeshEdge>(),
                VertexBuffer = Array.Empty<ModelVertex>(),
                LightmassSettings = Array.Empty<LightmassPrimitiveSettings>()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ref TAction a = ref Unsafe.AsRef(in action);

            a.Invoke(ref Self, nameof(Self));
            for (int i = 0; i < Surfs.Length; i++)
            {
                BspSurf surf = Surfs[i];
                a.Invoke(ref surf.Material, $"Surfs[{i}].Material");
                a.Invoke(ref surf.Actor, $"Surfs[{i}].Actor");
            }
            for (int i = 0; i < Zones.Length; i++)
            {
                a.Invoke(ref Zones[i].ZoneActor, $"Zones[{i}].ZoneActor");
            }
            a.Invoke(ref Polys, nameof(Polys));
        }
    }

    public class BspNode
    {
        public Plane Plane;
        public int iVertPool;
        public int iSurf;
        public int iVertexIndex;
        public ushort ComponentIndex;
        public ushort ComponentNodeIndex;
        public int ComponentElementIndex;
        public int iBack;
        public int iFront;
        public int iPlane;
        public int iCollisionBound;
        public byte iZone0;
        public byte iZone1;
        public byte NumVertices;
        public byte NodeFlags;
        public int iLeaf0;
        public int iLeaf1;
    }

    public class BspSurf
    {
        public UIndex Material;
        public int PolyFlags;
        public int pBase;
        public int vNormal;
        public int vTextureU;
        public int vTextureV;
        public int iBrushPoly;
        public UIndex Actor;
        public Plane Plane;
        public float ShadowMapScale;
        public int LightingChannels; //Bitfield
        public int iLightmassIndex; //ME3
    }

    public class Vert
    {
        public int pVertex;
        public int iSide;
        public Vector2 ShadowTexCoord;
        public Vector2 BackfaceShadowTexCoord; //not ME3, not LE3
    }

    public class ZoneProperties
    {
        public UIndex ZoneActor;
        public float LastRenderTime;
        public ulong ConnectivityMask;
        public ulong VisibilityMask;
    }

    public class ModelVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX;
        public PackedNormal TangentZ;
        public Vector2 TexCoord;
        public Vector2 ShadowTexCoord;
    }

    public partial class SCExt
    {
        public static void Serialize(SerializingContainer2 sc, ref BspNode node)
        {
            if (sc.IsLoading)
            {
                node = new BspNode();
            }
            sc.Serialize(ref node.Plane);
            sc.Serialize(ref node.iVertPool);
            sc.Serialize(ref node.iSurf);
            sc.Serialize(ref node.iVertexIndex);
            sc.Serialize(ref node.ComponentIndex);
            sc.Serialize(ref node.ComponentNodeIndex);
            sc.Serialize(ref node.ComponentElementIndex);
            sc.Serialize(ref node.iBack);
            sc.Serialize(ref node.iFront);
            sc.Serialize(ref node.iPlane);
            sc.Serialize(ref node.iCollisionBound);
            sc.Serialize(ref node.iZone0);
            sc.Serialize(ref node.iZone1);
            sc.Serialize(ref node.NumVertices);
            sc.Serialize(ref node.NodeFlags);
            sc.Serialize(ref node.iLeaf0);
            sc.Serialize(ref node.iLeaf1);
        }
        public static void Serialize(SerializingContainer2 sc, ref BspSurf node)
        {
            if (sc.IsLoading)
            {
                node = new BspSurf();
            }
            sc.Serialize(ref node.Material);
            sc.Serialize(ref node.PolyFlags);
            sc.Serialize(ref node.pBase);
            sc.Serialize(ref node.vNormal);
            sc.Serialize(ref node.vTextureU);
            sc.Serialize(ref node.vTextureV);
            sc.Serialize(ref node.iBrushPoly);
            sc.Serialize(ref node.Actor);
            sc.Serialize(ref node.Plane);
            sc.Serialize(ref node.ShadowMapScale);
            sc.Serialize(ref node.LightingChannels);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref node.iLightmassIndex);
            }
            else
            {
                node.iLightmassIndex = 1;
            }
        }
        public static void Serialize(SerializingContainer2 sc, ref Vert vert)
        {
            if (sc.IsLoading)
            {
                vert = new Vert();
            }
            sc.Serialize(ref vert.pVertex);
            sc.Serialize(ref vert.iSide);
            sc.Serialize(ref vert.ShadowTexCoord);
            if (!sc.Game.IsGame3())
            {
                sc.Serialize(ref vert.BackfaceShadowTexCoord);
            }
            else if (sc.IsLoading)
            {
                //probably wrong
                vert.BackfaceShadowTexCoord = new Vector2(vert.ShadowTexCoord.Y, vert.BackfaceShadowTexCoord.X);
            }
        }
        public static void Serialize(SerializingContainer2 sc, ref ZoneProperties zone)
        {
            if (sc.IsLoading)
            {
                zone = new ZoneProperties();
            }
            sc.Serialize(ref zone.ZoneActor);
            sc.Serialize(ref zone.LastRenderTime);
            sc.Serialize(ref zone.ConnectivityMask);
            sc.Serialize(ref zone.VisibilityMask);
        }
        public static void Serialize(SerializingContainer2 sc, ref ModelVertex vert)
        {
            if (sc.IsLoading)
            {
                vert = new ModelVertex();
            }
            sc.Serialize(ref vert.Position);
            sc.Serialize(ref vert.TangentX);
            sc.Serialize(ref vert.TangentZ);
            sc.Serialize(ref vert.TexCoord);
            sc.Serialize(ref vert.ShadowTexCoord);
        }
    }
}