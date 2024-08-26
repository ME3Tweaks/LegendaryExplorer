using System;
using System.Linq;
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

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Bounds);
            sc.BulkSerialize(ref Vectors, sc.Serialize, 12);
            sc.BulkSerialize(ref Points, sc.Serialize, 12);
            sc.BulkSerialize(ref Nodes, sc.Serialize, 64);
            sc.Serialize(ref Self);
            sc.Serialize(ref Surfs, sc.Serialize);
            sc.BulkSerialize(ref Verts, sc.Serialize, sc.Game.IsGame3() ? 16 : 24);
            sc.Serialize(ref NumSharedSides);
            sc.Serialize(ref Zones, sc.Serialize);
            sc.Serialize(ref Polys);
            sc.BulkSerialize(ref LeafHulls, sc.Serialize, 4);
            sc.BulkSerialize(ref Leaves, sc.Serialize, 4);
            sc.Serialize(ref RootOutside);
            sc.Serialize(ref Linked);
            sc.BulkSerialize(ref PortalNodes, sc.Serialize, 4);
            if (sc.Game != MEGame.UDK)
            {
                sc.BulkSerialize(ref ShadowVolume, sc.Serialize, 16);
            }
            else if (sc.IsLoading)
            {
                ShadowVolume = [];
            }
            sc.Serialize(ref NumVertices);
            sc.BulkSerialize(ref VertexBuffer, sc.Serialize, 36);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref LightingGuid);
                if (sc.IsSaving && sc.Game is MEGame.UDK) // We can't lightmass unless it's UDK so no point changing this unless it's UDK.
                {
                    // Ensure LightmassSettings struct is big enough 
                    var lightmassCount = Surfs.Length > 0 ? Surfs.Max(x => x.iLightmassIndex) : 0; //Will +1
                    LightmassSettings = new LightmassPrimitiveSettings[lightmassCount]; // Index 1 = 2 items in list
                    for (int i = 0; i < LightmassSettings.Length; i++)
                    {
                        LightmassSettings[i] = new LightmassPrimitiveSettings
                        {
                            FullyOccludedSamplesFraction = 1,
                            EmissiveLightFalloffExponent = 2,
                            EmissiveBoost = 1,
                            DiffuseBoost = 1,
                            SpecularBoost = 1
                        };
                    }
                }
                sc.Serialize(ref LightmassSettings, sc.Serialize);
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
                Vectors = [],
                Points = [],
                Nodes = [],
                Self = 0,
                Surfs = [],
                Verts = [],
                Zones = [],
                Polys = 0,
                LeafHulls = [],
                Leaves = [],
                PortalNodes = [],
                ShadowVolume = [],
                VertexBuffer = [],
                LightmassSettings = []
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

    public partial class SerializingContainer
    {
        public void Serialize(ref BspNode node)
        {
            if (IsLoading)
            {
                node = new BspNode();
            }
            Serialize(ref node.Plane);
            Serialize(ref node.iVertPool);
            Serialize(ref node.iSurf);
            Serialize(ref node.iVertexIndex);
            Serialize(ref node.ComponentIndex);
            Serialize(ref node.ComponentNodeIndex);
            Serialize(ref node.ComponentElementIndex);
            Serialize(ref node.iBack);
            Serialize(ref node.iFront);
            Serialize(ref node.iPlane);
            Serialize(ref node.iCollisionBound);
            Serialize(ref node.iZone0);
            Serialize(ref node.iZone1);
            Serialize(ref node.NumVertices);
            Serialize(ref node.NodeFlags);
            Serialize(ref node.iLeaf0);
            Serialize(ref node.iLeaf1);
        }
        public void Serialize(ref BspSurf node)
        {
            if (IsLoading)
            {
                node = new BspSurf();
            }
            Serialize(ref node.Material);
            Serialize(ref node.PolyFlags);
            Serialize(ref node.pBase);
            Serialize(ref node.vNormal);
            Serialize(ref node.vTextureU);
            Serialize(ref node.vTextureV);
            Serialize(ref node.iBrushPoly);
            Serialize(ref node.Actor);
            Serialize(ref node.Plane);
            Serialize(ref node.ShadowMapScale);
            Serialize(ref node.LightingChannels);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref node.iLightmassIndex);
            }
            else
            {
                node.iLightmassIndex = 1;
            }
        }
        public void Serialize(ref Vert vert)
        {
            if (IsLoading)
            {
                vert = new Vert();
            }
            Serialize(ref vert.pVertex);
            Serialize(ref vert.iSide);
            Serialize(ref vert.ShadowTexCoord);
            if (!Game.IsGame3())
            {
                Serialize(ref vert.BackfaceShadowTexCoord);
            }
            else if (IsLoading)
            {
                //probably wrong
                vert.BackfaceShadowTexCoord = new Vector2(vert.ShadowTexCoord.Y, vert.BackfaceShadowTexCoord.X);
            }
        }
        public void Serialize(ref ZoneProperties zone)
        {
            if (IsLoading)
            {
                zone = new ZoneProperties();
            }
            Serialize(ref zone.ZoneActor);
            Serialize(ref zone.LastRenderTime);
            Serialize(ref zone.ConnectivityMask);
            Serialize(ref zone.VisibilityMask);
        }
        public void Serialize(ref ModelVertex vert)
        {
            if (IsLoading)
            {
                vert = new ModelVertex();
            }
            Serialize(ref vert.Position);
            Serialize(ref vert.TangentX);
            Serialize(ref vert.TangentZ);
            Serialize(ref vert.TexCoord);
            Serialize(ref vert.ShadowTexCoord);
        }
    }
}