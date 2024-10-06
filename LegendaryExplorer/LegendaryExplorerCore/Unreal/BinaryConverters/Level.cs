using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.Collections;
using Microsoft.Toolkit.HighPerformance;
using UIndex = System.Int32;

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
        public UMultiMap<UIndex, StreamableTextureInstanceList> TextureToInstancesMap; //TODO: Make this a UMap
        public UMultiMap<UIndex, uint> MeshComponentsWithDynamiclighting;//UDK  //TODO: Make this a UMap
        public byte[] ApexMesh;//ME3 only
        public byte[] CachedPhysBSPData; //BulkSerialized
        public UMultiMap<UIndex, CachedPhysSMData> CachedPhysSMDataMap;
        public List<KCachedConvexData> CachedPhysSMDataStore;
        public UMultiMap<UIndex, CachedPhysSMData> CachedPhysPerTriSMDataMap;
        public List<KCachedPerTriData> CachedPhysPerTriSMDataStore;
        public int CachedPhysBSPDataVersion;
        public int CachedPhysSMDataVersion;
        public UMultiMap<UIndex, bool> ForceStreamTextures;  //TODO: Make this a UMap
        public UIndex NavListStart;
        public UIndex NavListEnd;
        public UIndex CoverListStart;
        public UIndex CoverListEnd;
        //if ME3
        public UIndex PylonListStart;
        public UIndex PylonListEnd;
        public List<GuidIndexPair> CrossLevelCoverGuidRefs;
        public List<UIndex> CoverLinkRefs;
        public List<CoverIndexPair> CoverIndexPairs;
        public List<GuidIndexPair> CrossLevelNavGuidRefs;
        public List<UIndex> NavRefs;
        public List<int> NavRefIndicies;
        //endif
        public List<UIndex> CrossLevelActors;
        public UIndex ArtPlaceable1;//ME1
        public UIndex ArtPlaceable2;//ME1

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Self);
            sc.Serialize(ref Actors, sc.Serialize);
            sc.Serialize(ref URL);
            sc.Serialize(ref Model);
            sc.Serialize(ref ModelComponents, sc.Serialize);
            sc.Serialize(ref GameSequences, sc.Serialize);
            sc.Serialize(ref TextureToInstancesMap, sc.Serialize, sc.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref MeshComponentsWithDynamiclighting, sc.Serialize, sc.Serialize);
            }
            else
            {
                MeshComponentsWithDynamiclighting = [];
            }
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref ApexMesh);
            }
            else if (sc.IsLoading)
            {
                ApexMesh = [];
            }

            int byteSize = 1;
            sc.Serialize(ref byteSize);
            sc.Serialize(ref CachedPhysBSPData);

            sc.Serialize(ref CachedPhysSMDataMap, sc.Serialize, sc.Serialize);
            sc.Serialize(ref CachedPhysSMDataStore, sc.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataMap, sc.Serialize, sc.Serialize);
            sc.Serialize(ref CachedPhysPerTriSMDataStore, sc.Serialize);
            sc.Serialize(ref CachedPhysBSPDataVersion);
            sc.Serialize(ref CachedPhysSMDataVersion);
            sc.Serialize(ref ForceStreamTextures, sc.Serialize, sc.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                var dummy = new KCachedConvexData { CachedConvexElements = [] };
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

            if (sc.Game.IsGame3() || sc.Game == MEGame.UDK) // Gated by licensee version
            {
                sc.Serialize(ref CrossLevelCoverGuidRefs, sc.Serialize);
                sc.Serialize(ref CoverLinkRefs, sc.Serialize);
                sc.Serialize(ref CoverIndexPairs, sc.Serialize);
                if (sc.Game != MEGame.UDK)
                {
                    // BioWare specific
                    sc.Serialize(ref CrossLevelNavGuidRefs, sc.Serialize);
                    sc.Serialize(ref NavRefs, sc.Serialize);
                    sc.Serialize(ref NavRefIndicies, sc.Serialize);
                }
            }
            else if (sc.IsLoading)
            {
                PylonListStart = 0;
                PylonListEnd = 0;
                CrossLevelCoverGuidRefs = [];
                CoverLinkRefs = [];
                CoverIndexPairs = [];
                CrossLevelNavGuidRefs = [];
                NavRefs = [];
                NavRefIndicies = [];
            }
            sc.Serialize(ref CrossLevelActors, sc.Serialize);

            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref ArtPlaceable1);
                sc.Serialize(ref ArtPlaceable2);
            }
            else if (sc.IsLoading)
            {
                ArtPlaceable1 = 0;
                ArtPlaceable2 = 0;
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
                Actors = [],
                URL = new URL
                {
                    Protocol = "unreal",
                    Host = "",
                    Map = game.IsGame1() ? "Entry.SFM" : "EntryMenu",
                    Portal = "",
                    Op = [],
                    Port = game.IsGame3() ? 3659 : 7777,
                    Valid = 1
                },
                Model = 0,
                ModelComponents = [],
                GameSequences = [],
                TextureToInstancesMap = [],
                MeshComponentsWithDynamiclighting = [],
                ApexMesh = [],
                CachedPhysBSPData = [],
                CachedPhysSMDataMap = [],
                CachedPhysSMDataStore = [],
                CachedPhysPerTriSMDataMap = [],
                CachedPhysPerTriSMDataStore = [],
                ForceStreamTextures = [],
                NavListStart = 0,
                NavListEnd = 0,
                CoverListStart = 0,
                CoverListEnd = 0,
                PylonListStart = 0,
                PylonListEnd = 0,
                CrossLevelCoverGuidRefs = [],
                CoverLinkRefs = [],
                CoverIndexPairs = [],
                CrossLevelNavGuidRefs = [],
                NavRefs = [],
                NavRefIndicies = [],
                CrossLevelActors = [],
                ArtPlaceable1 = 0,
                ArtPlaceable2 = 0
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            var actorSpan = Actors.AsSpan();
            for (int i = 0; i < actorSpan.Length; i++)
            {
                Unsafe.AsRef(in action).Invoke(ref actorSpan[i], $"Actors[{i}]");
            }
            ForEachUIndexExceptActorList(game, action);
        }

        public void ForEachUIndexExceptActorList<TAction>(MEGame game, in TAction action) where TAction : struct, IUIndexAction
        {
            ref TAction a = ref Unsafe.AsRef(in action);

            a.Invoke(ref Self, nameof(Self));
            a.Invoke(ref Model, nameof(Model));
            ForEachUIndexInSpan(action, ModelComponents.AsSpan(), nameof(ModelComponents));
            ForEachUIndexInSpan(action, GameSequences.AsSpan(), nameof(GameSequences));
            ForEachUIndexKeyInMultiMap(action, TextureToInstancesMap, nameof(TextureToInstancesMap));
            if (game is MEGame.UDK)
            {
                ForEachUIndexKeyInMultiMap(action, MeshComponentsWithDynamiclighting, nameof(MeshComponentsWithDynamiclighting));
            }
            ForEachUIndexKeyInMultiMap(action, CachedPhysSMDataMap, nameof(CachedPhysSMDataMap));
            ForEachUIndexKeyInMultiMap(action, CachedPhysPerTriSMDataMap, nameof(CachedPhysPerTriSMDataMap));
            ForEachUIndexKeyInMultiMap(action, ForceStreamTextures, nameof(ForceStreamTextures));
            a.Invoke(ref NavListStart, nameof(NavListStart));
            a.Invoke(ref NavListEnd, nameof(NavListEnd));
            a.Invoke(ref CoverListStart, nameof(CoverListStart));
            a.Invoke(ref CoverListEnd, nameof(CoverListEnd));
            if (game >= MEGame.ME3)
            {
                a.Invoke(ref PylonListStart, nameof(PylonListStart));
                a.Invoke(ref PylonListEnd, nameof(PylonListEnd));
            }
            if (game.IsGame3())
            {
                ForEachUIndexInSpan(action, CoverLinkRefs.AsSpan(), nameof(CoverLinkRefs));
                ForEachUIndexInSpan(action, NavRefs.AsSpan(), nameof(NavRefs));
            }
            ForEachUIndexInSpan(action, CrossLevelActors.AsSpan(), nameof(CrossLevelActors));
            if (game.IsGame1())
            {
                a.Invoke(ref ArtPlaceable1, nameof(ArtPlaceable1));
                a.Invoke(ref ArtPlaceable2, nameof(ArtPlaceable2));
            }
        }
    }

    [DebuggerDisplay("CoverIndexPair | Index {CoverIndexIdx}, Slot {SlotIdx}")]
    public struct CoverIndexPair
    {
        /// <summary>
        /// The index into the CoverLinkRefs array on Level
        /// </summary>
        public uint CoverIndexIdx;

        /// <summary>
        /// The slot index of the cover
        /// </summary>
        public byte SlotIdx;
    }

    public struct GuidIndexPair
    {
        public Guid Guid;
        public int CoverIndexIdx;
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

    public class FPrecomputedVolumeDistanceField
    {
        public float VolumeMaxDistance;
        public Box VolumeBox;
        public int VolumeSizeX;
        public int VolumeSizeY;
        public int VolumeSizeZ;
        public Color[] Data;
        public int UDKUnknown; // Might not be part of this; always seems to be 0
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref FPrecomputedVolumeDistanceField vdf)
        {
            if (IsLoading)
            {
                vdf = new FPrecomputedVolumeDistanceField();
            }
            Serialize(ref vdf.VolumeMaxDistance);
            Serialize(ref vdf.VolumeBox);
            Serialize(ref vdf.VolumeSizeX);
            Serialize(ref vdf.VolumeSizeY);
            Serialize(ref vdf.VolumeSizeZ);
            Serialize(ref vdf.Data, Serialize);
            Serialize(ref vdf.UDKUnknown);
        }

        public void Serialize(ref URL url)
        {
            if (IsLoading)
            {
                url = new URL();
            }

            Serialize(ref url.Protocol);
            Serialize(ref url.Host);
            Serialize(ref url.Map);
            Serialize(ref url.Portal);
            Serialize(ref url.Op, Serialize);
            Serialize(ref url.Port);
            Serialize(ref url.Valid);
        }

        public void Serialize(ref StreamableTextureInstanceList texInstList)
        {
            if (IsLoading)
            {
                texInstList = new StreamableTextureInstanceList();
            }

            Serialize(ref texInstList.Instances, Serialize);
        }

        public void Serialize(ref StreamableTextureInstance texInst)
        {
            if (IsLoading)
            {
                texInst = new StreamableTextureInstance();
            }

            Serialize(ref texInst.BoundingSphere);
            Serialize(ref texInst.TexelFactor);
        }

        public void Serialize(ref CachedPhysSMData smData)
        {
            if (IsLoading)
            {
                smData = new CachedPhysSMData();
            }

            Serialize(ref smData.Scale3D);
            Serialize(ref smData.CachedDataIndex);
        }

        public void Serialize(ref KCachedConvexData convData)
        {
            if (IsLoading)
            {
                convData = new KCachedConvexData();
            }

            Serialize(ref convData.CachedConvexElements, Serialize);
        }

        public void Serialize(ref KCachedConvexDataElement convDataElem)
        {
            if (IsLoading)
            {
                convDataElem = new KCachedConvexDataElement();
            }

            BulkSerialize(ref convDataElem.ConvexElementData, Serialize, 1);
        }

        public void Serialize(ref KCachedPerTriData triData)
        {
            if (IsLoading)
            {
                triData = new KCachedPerTriData();
            }

            int byteSize = 1;
            Serialize(ref byteSize);
            Serialize(ref triData.CachedPerTriData);
        }

        public void Serialize(ref CoverIndexPair val)
        {
            Serialize(ref val.CoverIndexIdx);
            Serialize(ref val.SlotIdx);
        }

        public void Serialize(ref GuidIndexPair val)
        {
            Serialize(ref val.Guid);
            Serialize(ref val.CoverIndexIdx);
        }
    }
}