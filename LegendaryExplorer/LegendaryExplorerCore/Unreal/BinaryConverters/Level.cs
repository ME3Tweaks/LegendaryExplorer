using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Level : ObjectBinary
    {
        public UIndex Self;
        public List<UIndex> Actors;
        public URL URL;
        public UIndex Model;
        public UIndex[] ModelComponents;
        public UIndex[] GameSequences;
        public OrderedMultiValueDictionary<UIndex, StreamableTextureInstanceList> TextureToInstancesMap;
        public OrderedMultiValueDictionary<UIndex, uint> MeshComponentsWithDynamiclighting;//UDK
        public byte[] ApexMesh;//ME3 only
        public byte[] CachedPhysBSPData; //BulkSerialized
        public OrderedMultiValueDictionary<UIndex, CachedPhysSMData> CachedPhysSMDataMap;
        public List<KCachedConvexData> CachedPhysSMDataStore;
        public OrderedMultiValueDictionary<UIndex, CachedPhysSMData> CachedPhysPerTriSMDataMap;
        public List<KCachedPerTriData> CachedPhysPerTriSMDataStore;
        public int CachedPhysBSPDataVersion;
        public int CachedPhysSMDataVersion;
        public OrderedMultiValueDictionary<UIndex, bool> ForceStreamTextures;
        public UIndex NavListStart;
        public UIndex NavListEnd;
        public UIndex CoverListStart;
        public UIndex CoverListEnd;
        //if ME3
        public UIndex PylonListStart;
        public UIndex PylonListEnd;
        public OrderedMultiValueDictionary<Guid, int> guidToIntMap;
        public List<UIndex> CoverLinks;
        public OrderedMultiValueDictionary<int, byte> intToByteMap;
        public OrderedMultiValueDictionary<Guid, int> guidToIntMap2;
        public List<UIndex> NavPoints;
        public List<int> numbers;
        //endif
        public List<UIndex> CrossLevelActors;
        public UIndex ArtPlaceable1;//ME1
        public UIndex ArtPlaceable2;//ME1

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Self);
            sc.Serialize(ref Actors, SCExt.Serialize);
            sc.Serialize(ref URL);
            sc.Serialize(ref Model);
            sc.Serialize(ref ModelComponents, SCExt.Serialize);
            sc.Serialize(ref GameSequences, SCExt.Serialize);
            sc.Serialize(ref TextureToInstancesMap, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref MeshComponentsWithDynamiclighting, SCExt.Serialize, SCExt.Serialize);
            }
            else
            {
                MeshComponentsWithDynamiclighting = new OrderedMultiValueDictionary<UIndex, uint>();
            }
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref ApexMesh);
            }
            else if (sc.IsLoading)
            {
                ApexMesh = Array.Empty<byte>();
            }

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref CachedPhysBSPData);

            sc.Serialize(ref CachedPhysSMDataMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref CachedPhysSMDataStore, SCExt.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataStore, SCExt.Serialize);
            sc.Serialize(ref CachedPhysBSPDataVersion);
            sc.Serialize(ref CachedPhysSMDataVersion);
            sc.Serialize(ref ForceStreamTextures, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                var dummy = new KCachedConvexData { CachedConvexElements = Array.Empty<KCachedConvexDataElement>() };
                sc.Serialize(ref dummy);
                int dummyInt = 0;
                sc.Serialize(ref dummyInt);
            }
            sc.Serialize(ref NavListStart);
            sc.Serialize(ref NavListEnd);
            sc.Serialize(ref CoverListStart);
            sc.Serialize(ref CoverListEnd);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref PylonListStart);
                sc.Serialize(ref PylonListEnd);
            }
            if (sc.Game.IsGame3())
            {
                sc.Serialize(ref guidToIntMap, SCExt.Serialize, SCExt.Serialize);
                sc.Serialize(ref CoverLinks, SCExt.Serialize);
                sc.Serialize(ref intToByteMap, SCExt.Serialize, SCExt.Serialize);
                sc.Serialize(ref guidToIntMap2, SCExt.Serialize, SCExt.Serialize);
                sc.Serialize(ref NavPoints, SCExt.Serialize);
                sc.Serialize(ref numbers, SCExt.Serialize);
            }
            else if (sc.IsLoading)
            {
                PylonListStart = new UIndex(0);
                PylonListEnd = new UIndex(0);
                guidToIntMap = new OrderedMultiValueDictionary<Guid, int>();
                CoverLinks = new List<UIndex>();
                intToByteMap = new OrderedMultiValueDictionary<int, byte>();
                guidToIntMap2 = new OrderedMultiValueDictionary<Guid, int>();
                NavPoints = new List<UIndex>();
                numbers = new List<int>();
            }
            sc.Serialize(ref CrossLevelActors, SCExt.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
            }

            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref ArtPlaceable1);
                sc.Serialize(ref ArtPlaceable2);
            }
            else if (sc.IsLoading)
            {
                ArtPlaceable1 = new UIndex(0);
                ArtPlaceable2 = new UIndex(0);
            }

            if (sc.Game == MEGame.UDK && sc.IsSaving)
            {
                sc.ms.Writer.WriteBoolInt(false); //PrecomputedLightVolume bIsInitialized
                sc.ms.BaseStream.WriteZeros(28); //Zero-ed PrecomputedVisibilityHandler
                sc.ms.BaseStream.WriteZeros(45); //unk data
            }

            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                //PrecomputedLightVolume
                bool bIsInitialized = false;
                sc.Serialize(ref bIsInitialized);
                //should always be false, but just in case;
                if (bIsInitialized)
                {
                    throw new Exception($"PersistentLevel has a PreComputedLightVolume! Level in: {sc.Pcc.FilePath}");
                }
            }
        }

        public static Level Create(MEGame game)
        {
            return new()
            {
                Self = 0,
                Actors = new List<UIndex>(),
                URL = new URL
                {
                    Protocol = "unreal",
                    Host = "",
                    Map = game.IsGame1() ? "Entry.SFM" : "EntryMenu",
                    Portal = "",
                    Op = Array.Empty<string>(),
                    Port = game.IsGame3() ? 3659 : 7777,
                    Valid = 1
                },
                Model = 0,
                ModelComponents = Array.Empty<UIndex>(),
                GameSequences = Array.Empty<UIndex>(),
                TextureToInstancesMap = new OrderedMultiValueDictionary<UIndex, StreamableTextureInstanceList>(),
                MeshComponentsWithDynamiclighting = new OrderedMultiValueDictionary<UIndex, uint>(),
                ApexMesh = Array.Empty<byte>(),
                CachedPhysBSPData = Array.Empty<byte>(),
                CachedPhysSMDataMap = new OrderedMultiValueDictionary<UIndex, CachedPhysSMData>(),
                CachedPhysSMDataStore = new List<KCachedConvexData>(),
                CachedPhysPerTriSMDataMap = new OrderedMultiValueDictionary<UIndex, CachedPhysSMData>(),
                CachedPhysPerTriSMDataStore = new List<KCachedPerTriData>(),
                ForceStreamTextures = new OrderedMultiValueDictionary<UIndex, bool>(),
                NavListStart = 0,
                NavListEnd = 0,
                CoverListStart = 0,
                CoverListEnd = 0,
                PylonListStart = 0,
                PylonListEnd = 0,
                guidToIntMap = new OrderedMultiValueDictionary<Guid, int>(),
                CoverLinks = new List<UIndex>(),
                intToByteMap = new OrderedMultiValueDictionary<int, byte>(),
                guidToIntMap2 = new OrderedMultiValueDictionary<Guid, int>(),
                NavPoints = new List<UIndex>(),
                numbers = new List<int>(),
                CrossLevelActors = new List<UIndex>(),
                ArtPlaceable1 = 0,
                ArtPlaceable2 = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();
            
            uIndexes.AddRange(Actors.Select((u, i) => (u, $"{nameof(Actors)}[{i}]")));
            uIndexes.AddRange(GetUIndexesWithoutActorList(game));

            return uIndexes;
        }

        public List<(UIndex, string)> GetUIndexesWithoutActorList(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();

            uIndexes.Add((Self, nameof(Self)));
            uIndexes.Add((Model, nameof(Model)));
            uIndexes.AddRange(ModelComponents.Select((u, i) => (u, $"{nameof(ModelComponents)}[{i}]")));
            uIndexes.AddRange(GameSequences.Select((u, i) => (u, $"{nameof(GameSequences)}[{i}]")));
            uIndexes.AddRange(TextureToInstancesMap.Select((kvp, i) => (kvp.Key, $"{nameof(TextureToInstancesMap)}[{i}]")));
            uIndexes.AddRange(CachedPhysSMDataMap.Select((kvp, i) => (kvp.Key, $"{nameof(CachedPhysSMDataMap)}[{i}]")));
            uIndexes.AddRange(CachedPhysPerTriSMDataMap.Select((kvp, i) => (kvp.Key, $"{nameof(CachedPhysPerTriSMDataMap)}[{i}]")));
            uIndexes.AddRange(ForceStreamTextures.Select((kvp, i) => (kvp.Key, $"{nameof(ForceStreamTextures)}[{i}]")));
            uIndexes.Add((NavListStart, nameof(NavListStart)));
            uIndexes.Add((NavListEnd, nameof(NavListEnd)));
            uIndexes.Add((CoverListStart, nameof(CoverListStart)));
            uIndexes.Add((CoverListEnd, nameof(CoverListEnd)));
            if (game >= MEGame.ME3)
            {
                uIndexes.Add((PylonListStart, nameof(PylonListStart)));
                uIndexes.Add((PylonListEnd, nameof(PylonListEnd)));
            }
            if (game.IsGame3())
            {
                uIndexes.AddRange(CoverLinks.Select((u, i) => (u, $"{nameof(CoverLinks)}[{i}]")));
                uIndexes.AddRange(NavPoints.Select((u, i) => (u, $"{nameof(NavPoints)}[{i}]")));
            }
            uIndexes.AddRange(CrossLevelActors.Select((u, i) => (u, $"{nameof(CrossLevelActors)}[{i}]")));
            if (game.IsGame1())
            {
                uIndexes.Add((ArtPlaceable1, nameof(ArtPlaceable1)));
                uIndexes.Add((ArtPlaceable2, nameof(ArtPlaceable2)));
            }

            return uIndexes;
        }
    }

    public class URL
    {
        public string Protocol;
        public string Host;
        public string Map;
        public string Portal;
        public string[] Op;
        public int Port;
        public int Valid;
    }

    public class StreamableTextureInstanceList
    {
        public StreamableTextureInstance[] Instances;
    }

    public class StreamableTextureInstance
    {
        public Sphere BoundingSphere;
        public float TexelFactor;
    }

    public class CachedPhysSMData
    {
        public Vector3 Scale3D;
        public int CachedDataIndex;
    }

    public class KCachedConvexData
    {
        public KCachedConvexDataElement[] CachedConvexElements;
    }

    public class KCachedConvexDataElement
    {
        public byte[] ConvexElementData; //BulkSerialized
    }

    public class KCachedPerTriData
    {
        public byte[] CachedPerTriData; //BulkSerialized
    }

    public partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref URL url)
        {
            if (sc.IsLoading)
            {
                url = new URL();
            }

            sc.Serialize(ref url.Protocol);
            sc.Serialize(ref url.Host);
            sc.Serialize(ref url.Map);
            sc.Serialize(ref url.Portal);
            sc.Serialize(ref url.Op, SCExt.Serialize);
            sc.Serialize(ref url.Port);
            sc.Serialize(ref url.Valid);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StreamableTextureInstanceList texInstList)
        {
            if (sc.IsLoading)
            {
                texInstList = new StreamableTextureInstanceList();
            }

            sc.Serialize(ref texInstList.Instances, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StreamableTextureInstance texInst)
        {
            if (sc.IsLoading)
            {
                texInst = new StreamableTextureInstance();
            }

            sc.Serialize(ref texInst.BoundingSphere);
            sc.Serialize(ref texInst.TexelFactor);
        }
        public static void Serialize(this SerializingContainer2 sc, ref CachedPhysSMData smData)
        {
            if (sc.IsLoading)
            {
                smData = new CachedPhysSMData();
            }

            sc.Serialize(ref smData.Scale3D);
            sc.Serialize(ref smData.CachedDataIndex);
        }
        public static void Serialize(this SerializingContainer2 sc, ref KCachedConvexData convData)
        {
            if (sc.IsLoading)
            {
                convData = new KCachedConvexData();
            }

            sc.Serialize(ref convData.CachedConvexElements, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref KCachedConvexDataElement convDataElem)
        {
            if (sc.IsLoading)
            {
                convDataElem = new KCachedConvexDataElement();
            }
            sc.BulkSerialize(ref convDataElem.ConvexElementData, SCExt.Serialize, 1);
        }
        public static void Serialize(this SerializingContainer2 sc, ref KCachedPerTriData triData)
        {
            if (sc.IsLoading)
            {
                triData = new KCachedPerTriData();
            }

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref triData.CachedPerTriData);
        }
    }
}