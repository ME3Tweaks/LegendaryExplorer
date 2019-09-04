using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using SharpDX;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class Level : ObjectBinary
    {
        public UIndex Self;
        public UIndex[] Actors;
        public URL URL;
        public UIndex Model;
        public UIndex[] ModelComponents;
        public UIndex[] GameSequences;
        public OrderedMultiValueDictionary<UIndex, StreamableTextureInstanceList> TextureToInstancesMap;
        public byte[] ApexMesh;//ME3 only
        public byte[] CachedPhysBSPData; //BulkSerialized
        public OrderedMultiValueDictionary<UIndex, CachedPhysSMData> CachedPhysSMDataMap;
        public KCachedConvexData[] CachedPhysSMDataStore;
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
        public UIndex[] CoverLinks;
        public OrderedMultiValueDictionary<int, byte> intToByteMap;
        public OrderedMultiValueDictionary<Guid, int> guidToIntMap2;
        public UIndex[] NavPoints;
        public int[] numbers;
        //endif
        public UIndex[] CrossLevelActors;
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
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref ApexMesh, SCExt.Serialize);
            }
            else if (sc.IsLoading)
            {
                ApexMesh = Array.Empty<byte>();
            }

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref CachedPhysBSPData, SCExt.Serialize);

            sc.Serialize(ref CachedPhysSMDataMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref CachedPhysSMDataStore, SCExt.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataStore, SCExt.Serialize);
            sc.Serialize(ref CachedPhysBSPDataVersion);
            sc.Serialize(ref CachedPhysSMDataVersion);
            sc.Serialize(ref ForceStreamTextures, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref NavListStart);
            sc.Serialize(ref NavListEnd);
            sc.Serialize(ref CoverListStart);
            sc.Serialize(ref CoverListEnd);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref PylonListStart);
                sc.Serialize(ref PylonListEnd);
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
                CoverLinks = new UIndex[0];
                intToByteMap = new OrderedMultiValueDictionary<int, byte>();
                guidToIntMap2 = new OrderedMultiValueDictionary<Guid, int>();
                NavPoints = new UIndex[0];
                numbers = new int[0];
            }
            sc.Serialize(ref CrossLevelActors, SCExt.Serialize);
            if (sc.Game == MEGame.ME3)
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

            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref ArtPlaceable1);
                sc.Serialize(ref ArtPlaceable2);
            }
            else if (sc.IsLoading)
            {
                ArtPlaceable1 = new UIndex(0);
                ArtPlaceable2 = new UIndex(0);
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();

            uIndexes.Add((Self, nameof(Model)));
            uIndexes.AddRange(Actors.Select((u, i) => (u, $"{nameof(Actors)}[{i}]")));
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
            if (game == MEGame.ME3)
            {
                uIndexes.Add((PylonListStart, nameof(PylonListStart)));
                uIndexes.Add((PylonListEnd, nameof(PylonListEnd)));
                uIndexes.AddRange(CoverLinks.Select((u, i) => (u, $"{nameof(CoverLinks)}[{i}]")));
                uIndexes.AddRange(NavPoints.Select((u, i) => (u, $"{nameof(NavPoints)}[{i}]")));
            }
            uIndexes.AddRange(CrossLevelActors.Select((u, i) => (u, $"{nameof(CrossLevelActors)}[{i}]")));
            if (game == MEGame.ME1)
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
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

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
            sc.Serialize(ref url.Op, Serialize);
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

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref convDataElem.ConvexElementData, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref KCachedPerTriData triData)
        {
            if (sc.IsLoading)
            {
                triData = new KCachedPerTriData();
            }

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref triData.CachedPerTriData, Serialize);
        }
    }
}
