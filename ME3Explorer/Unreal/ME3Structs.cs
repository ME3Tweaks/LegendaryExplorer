using ME3Explorer.Unreal.ME3Enums;
using ME3Explorer.Unreal.ME3Classes;
using NameReference = ME3Explorer.Unreal.NameReference;

namespace ME3Explorer.Unreal.ME3Structs
{
    public class ReplicatedPickupObject
    {
        public int PickedUpBy;
        public EPickupObjectEvent EPOEvent;
        public byte Trigger;
    }
    public class Charge_PendingVolume
    {
        public int Volume;
        public bool bTouching;
    }
    public class SeqAct_SetVectorParameters
    {
        public LinearColor ColorValue;
        public float VectorStartDelta;
    }
    public class SeqAct_SetScalarParameters
    {
        public float ScalarValue;
        public float ScalarStartDelta;
    }
    public class SeqAct_SetTextureParameters
    {
        public int TextureValue;
    }
    public class SwarmerSpawnData
    {
        public Rotator SpawnRot;
        public NameReference SocketName;
        public NameReference BoneName;
    }
    public class AcidEffectData
    {
        public int Target;
        public float Lifetime;
    }
    public class ShieldData
    {
        public Guid ShieldClientEffectGuid;
        public int ShieldedPawn;
        public bool bDrainCrust;
        public bool bActive;
    }
    public class MailData
    {
        public string SmallImage;
        public int MailTitle;
        public int MailBody;
        public int MailStatePlot;
        public int SendConditional;
        public int ActionButtonText;
        public int ActionPlotID;
        public int ActionText;
    }
    public class ReplicatedInit_GethShotgun
    {
        public Vector Direction;
        public Vector location;
        public float Speed;
        public int Instigator;
        public float ChargeAmount;
        public int AcquiredTarget;
        public byte Trigger;
    }
    public class ReplicatedDroneCreator
    {
        public int Creator;
        public int CreatorPowerCustomActionIndex;
    }
    public class PyroFlameLeaks
    {
        public NameReference SocketName;
        public int PSC_Leak;
        public float HealthPctThreshold;
        public bool bActive;
    }
    public class TrainingItemData
    {
        public int ItemTitle;
        public int ItemDesc;
    }
    public class StreamData
    {
        public int H;
        public int VolID;
        public int CondID;
    }
    public class BonusWeaponOption
    {
        public string WeaponClass;
        public int OutputIdx;
        public ELoadoutWeapons Category;
    }
    public class ReplicatedTurretCreator
    {
        public int Creator;
        public int CreatorPowerCustomActionIndex;
    }
    public class BurningActor
    {
        public int AffectedActor;
        public int TimesAffected;
    }
    public class ReplicatedDecoyCreator
    {
        public int Creator;
        public int CreatorPowerCustomActionIndex;
    }
    public class LeaveMissionData
    {
        public NameReference CurrentMap;
        public NameReference NextMap;
        public NameReference NextStart;
        public int EndGmAct;
        public float EndGmX;
        public float EndGmY;
    }
    public class MEPlotInt
    {
        public int PlotInt;
        public int Value_On;
        public int Value_Off;
    }
    public class MEPlotState
    {
        public int[] MutuallyExclusiveStateNames;
        public int[] PlotStates_True;
        public int[] PlotStates_False;
        public MEPlotInt[] PlotInts;
        public int StateName;
        public int StateDescription;
    }
    public class CardPoolEntry
    {
        public string PoolName;
        public float Weight;
    }
    public class CardPackEntry
    {
        public string PackName;
        public CardPoolEntry[] Pools;
        public string BackupPool;
        public int Quantity;
    }
    public class ConsumableToPackEntry
    {
        public string PackName;
        public int Id;
    }
    public class StoreGUIData
    {
        public StoreImageData ImageData;
        public string DisplayTitle;
        public string DisplaySubtitle;
        public string DescriptionTitle;
        public string Description;
        public string PromoText;
        public int nID;
        public bool bExpires;
        public EPurchaseType EPurchaseType;
    }
    public class VisibleCondition
    {
        public int[] and;
    }
    public class StoreImageData
    {
        public string ImageReference;
        public EStoreImageLocation ImageLocation;
    }
    public class StoreInfoEntry
    {
        public StoreImageData ImageData;
        public string PackName;
        public VisibleCondition[] visible;
        public int[] RequiredDLCModuleIDs;
        public string RevealIntroTextureRef;
        public string RevealIntroHoloTextureRef;
        public NameReference RevealIntroSound;
        public int nID;
        public int Title;
        public int SubTitle;
        public int Description;
        public int CreditCost;
        public float ExpirationTime;
        public float RevealTime;
        public int offerId;
        public int srPromoString;
        public int PerPlayerMax;
        public bool bDisabled;
    }
    public class CardInfoData
    {
        public string UniqueName;
        public string GUIType;
        public string GUITextureRef;
        public int MaxCount;
        public int GUIName;
        public int GUIDescription;
        public int GUIIconIndex;
        public int PVIncrementBonus;
        public int VersionIdx;
        public int Category;
        public int LevelAwarded;
        public int CardOwner;
        public int Entitlement;
        public bool bUseVersionIdx;
        public ECardRarity Rarity;
        public EReinforcementGUICategory GUICategory;
    }
    public class SFXOperation_ObjectiveSpawnPointData
    {
        public int ObjectiveActor;
        public int CombatZone;
        public int AnnexZoneLocation;
    }
    public class SFXOperation_ObjectiveRequirement
    {
        public string ObjectiveType;
        public int MinimumObjectivesRequired;
        public int MaximumObjectivesAllowed;
    }
    public class SFXOperation_ObjectiveToSpawn
    {
        public int ObjectiveData;
        public bool IsObjectiveAllocatedForSpawn;
    }
    public class SFXOperation_ObjectiveGroupToSpawn
    {
        public SFXOperation_ObjectiveToSpawn[] ObjectivesToSpawn;
        public int MinimumObjectivesToSpawn;
        public int MaximumObjectivesToSpawn;
    }
    public class MatchSettingsDisplayInfo
    {
        public string MapName;
        public string EnemyName;
        public string ChallengeName;
        public string Wave;
        public int MapId;
        public int Enemy;
        public int Challenge;
        public int Time;
        public bool isPrivate;
        public bool bRandomMap;
        public bool bRandomEnemy;
        public bool bMissionSuccessful;
    }
    public class PatternData
    {
        public int PatternID;
        public int PatternName;
    }
    public class SFXMPHUD_TickerMovie
    {
        public int Id;
        public int TickerMovie;
        public int CurrentSlot;
    }
    public class SFXMPHUD_TickerEntry
    {
        public string Text;
        public float TimeToLive;
        public int MovieIndex;
        public bool Fading;
    }
    public class MPUIObjectiveCircle
    {
        public string Text;
        public int MovieClip;
        public float PercentComplete;
        public bool Complete;
    }
    public class MatchConsumableDisplayInfo
    {
        public string MatchConsumableIconRef;
        public string MatchConsumableName;
        public int Category;
    }
    public class PlayerDisplayInfo
    {
        public string Gamertag;
        public string PlayerName;
        public string ClassData;
        public string CurrentXPString;
        public string NextLevelXPString;
        public string CombinedXPString;
        public string Credits;
        public string KitTextureRef;
        public string SmallKitTextureRef;
        public string ClassIconTextureRef;
        public string KitPrettyName;
        public PowerDisplayInfo[] PowerData;
        public WeaponDisplayInfo[] WeaponData;
        public MatchConsumableDisplayInfo[] MatchConsumableData;
        public int XPPercentage;
        public int Rating;
        public int NumKickVotesReceived;
        public bool Ready;
        public bool IsLocalPlayer;
        public bool IsLeader;
    }
    public class InGameConsumableInfo
    {
        public string ConsumableName;
        public string ConsumableIconResource;
        public int ConsumableIconIndex;
        public int ConsumableCount;
        public int ConsumableCap;
        public int UniqueId;
    }
    public class PowerDisplayInfo
    {
        public string PowerIconResource;
        public string PowerName;
        public int PowerIconIndex;
    }
    public class WeaponDisplayInfo
    {
        public string WeaponIconResource;
        public string WeaponImage;
        public string WeaponName;
        public string WeaponMod1Reference;
        public string WeaponMod2Reference;
        public int WeaponIconIndex;
    }
    public class ConsumableDisplayInfo
    {
        public string Title;
        public string Description;
        public string Image;
        public int CardOwner;
        public int ConsumableID;
        public int Version;
        public int Category;
        public int Count;
        public bool Disabled;
        public bool Active;
        public bool New;
    }
    public class PlayerMatchResultData
    {
        public string PlayerName;
        public string ClassData;
        public string ClassIconRef;
        public RewardMedalData[] Medals;
        public int PlayerDataIdx;
        public int TotalMatchXP;
        public int MatchXPPercentage;
        public bool bExtracted;
        public bool bIsLocal;
    }
    public class RewardMedalData
    {
        public string MedalIconRef;
        public string MedalDescription;
    }
    public class PauseMenuScoreData
    {
        public string DisplayName;
        public int PRIIndex;
        public int Score;
    }
    public class CardTypeText
    {
        public string CardType;
        public int GUIDescription;
        public int GUIDupeDescription;
        public int GUIType;
        public int GUIDupeType;
    }
    public class CardDisplayData
    {
        public string DisplayName;
        public string DisplayDescription;
        public string TextureReference;
        public string CardTypeText;
        public int CardType;
        public bool IsNew;
    }
    public class ClassDisplayData
    {
        public string Name;
        public string DisplayName;
        public string CurrentLevelText;
        public string CurrentXPString;
        public string NextLevelXPString;
        public int CurrentLevel;
        public int XPPercentage;
        public bool bCanPromote;
        public bool bCanLevelKit;
        public bool bHasNewKit;
    }
    public class KitDisplayData
    {
        public string KitName;
        public string KitDisplayName;
        public string CharacterName;
        public string TextureReference;
        public string LockedTextureReference;
        public string PowerIconResource;
        public string PowerDescription1;
        public string PowerDescription2;
        public string PowerDescription3;
        public int PowerIcon1;
        public int PowerIcon2;
        public int PowerIcon3;
        public bool bLocked;
        public bool bDeployed;
        public bool bNeedsLevelUp;
    }
    public class StoreImage
    {
        public string ImagePath;
        public int ImageTexture;
        public bool bValid;
    }
    public class HMPlayer
    {
        public UniqueNetId NetId;
        public string PlayerName;
        public int[] PlayerMedals;
        public ActiveMatchConsumable[] ActiveMatchConsumables;
        public ScoreInfo Score;
    }
    public class HMGame
    {
        public int[] SquadMedals;
        public int MapSetting;
        public int EnemySetting;
        public int DifficultySetting;
        public bool PrivacySetting;
        public bool bRandomMap;
        public bool bRandomEnemy;
    }
    public class HMMatch
    {
        public WaveEventInfo Wave;
        public HMGame Game;
        public HMPlayer[] Players;
        public bool bValidGame;
        public bool bValidPlayers;
        public bool bValidWave;
        public bool bIsMissionComplete;
    }
    public class ReplicatedMeshInfoStruct
    {
        public int MeshUniqueID;
        public int ObjectiveTypeID;
    }
    public class MPPowerData
    {
        public int EvolvedChoices;
        public float CurrentRank;
        public int PowerClassPathID;
        public bool IsValid;
    }
    public class MPWeaponModData
    {
        public int WeaponModClassPathID;
        public int WeaponModLevel;
    }
    public class MPWeaponData
    {
        public MPWeaponModData WeaponMods;
        public int WeaponClassPathID;
        public int WeaponLevel;
        public bool IsValid;
    }
    public class MPCharacterData
    {
        public string CharacterName;
        public int CharacterKitID;
        public int ClassPrettyName;
        public int Level;
        public float XP;
        public int n7Rating;
        public int Tint1ID;
        public int Tint2ID;
        public int PatternID;
        public int PatternColorID;
        public int PhongID;
        public int EmissiveID;
        public int SkinToneID;
    }
    public class MapConfigData
    {
        public string MapName;
        public float XPMultiplier;
        public float CreditsMultiplier;
        public int ZoneIncrease;
        public EGAWZone ZoneID;
    }
    public class EnemySpawnInfo
    {
        public string EnemyArchetypeName;
        public NameReference EnemyType;
        public int EnemyArchetype;
        public int WaveCost;
    }
    public class EnemySquadInfo
    {
        public NameReference[] EnemyTypes;
        public int WaveCost;
    }
    public class SpawnedEnemy
    {
        public int Enemy;
        public int IndexInEnemyList;
    }
    public class PetData
    {
        public NameReference className;
        public int WaveCost;
    }
    public class BerserkStartCount
    {
        public int EnemiesLeft;
        public EDifficultyOptions Difficulty;
    }
    public class DifficultyLevelEnemies
    {
        public EnemyWaveInfo[] Enemies;
        public EDifficultyOptions Difficulty;
    }
    public class OperationWave
    {
        public int WaveNumber;
        public float CreditScale;
    }
    public class WaveType
    {
        public string WaveClassName;
        public int SelectionWeight;
    }
    public class WavePointRange
    {
        public Vector2D PointRange;
        public EDifficultyOptions Difficulty;
    }
    public class EmailData
    {
        public SFXChoiceEntry ChoiceEntry;
        public string EmailImage;
        public int AudioID;
        public int PlotIntID;
        public int SendCondID;
    }
    public class MapIDData
    {
        public NameReference MapName;
        public int MapId;
    }
    public class InterpCurvePointFloat
    {
        public float InVal;
        public float OutVal;
        public float ArriveTangent;
        public float LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveFloat
    {
        public InterpCurvePointFloat[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class InterpCurvePointVector2D
    {
        public float InVal;
        public Vector2D OutVal;
        public Vector2D ArriveTangent;
        public Vector2D LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveVector2D
    {
        public InterpCurvePointVector2D[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class InterpCurvePointVector
    {
        public float InVal;
        public Vector OutVal;
        public Vector ArriveTangent;
        public Vector LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveVector
    {
        public InterpCurvePointVector[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class InterpCurvePointTwoVectors
    {
        public float InVal;
        public TwoVectors OutVal;
        public TwoVectors ArriveTangent;
        public TwoVectors LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveTwoVectors
    {
        public InterpCurvePointTwoVectors[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class InterpCurvePointQuat
    {
        public float InVal;
        public Quat OutVal;
        public Quat ArriveTangent;
        public Quat LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveQuat
    {
        public InterpCurvePointQuat[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class InterpCurvePointLinearColor
    {
        public float InVal;
        public LinearColor OutVal;
        public LinearColor ArriveTangent;
        public LinearColor LeaveTangent;
        public EInterpCurveMode InterpMode;
    }
    public class InterpCurveLinearColor
    {
        public InterpCurvePointLinearColor[] Points;
        public EInterpMethodType InterpMethod;
    }
    public class RawDistribution
    {
        public byte Type;
        public byte Op;
        public byte LookupTableNumElements;
        public byte LookupTableChunkSize;
        public float[] LookupTable;
        public float LookupTableTimeScale;
        public float LookupTableStartTime;
    }
    public class BioRawDistributionRwVector3Base
    {
        public byte Type;
        public byte Op;
        public byte LookupTableNumElements;
        public byte LookupTableChunkSize;
        public float LookupTableMinOut;
        public float LookupTableMaxOut;
        public RwVector3[] LookupTable;
        public float LookupTableTimeScale;
        public float LookupTableStartTime;
    }
    public class RenderCommandFence
    {
        public int NumPendingFences;
    }
    public class OctreeElementId
    {
        public Pointer Node;
        public int ElementIndex;
    }
    public class BoneAtom
    {
        public Quat Rotation;
        public Vector Translation;
        public float Scale;
    }
    public class Pointer
    {
        public int Dummy;
    }
    public class RawDistributionFloat : RawDistribution
    {
        public int Distribution;
    }
    public class RawDistributionVector : RawDistribution
    {
        public int Distribution;
    }
    public class BioRawDistributionRwVector3 : BioRawDistributionRwVector3Base
    {
        public int Distribution;
    }
    public class QWord
    {
        public int A;
        public int B;
    }
    public class SFXTokenMapping
    {
        public int TokenId;
        public string Data;
    }
    public class Double
    {
        public int A;
        public int B;
    }
    public class ThreadSafeCounter
    {
        public int Value;
    }
    public class BitArray_Mirror
    {
        public Pointer IndirectData;
        public int InlineData;
        public int NumBits;
        public int MaxBits;
    }
    public class SparseArray_Mirror
    {
        public int[] Elements;
        public BitArray_Mirror AllocationFlags;
        public int FirstFreeIndex;
        public int NumFreeIndices;
    }
    public class Set_Mirror
    {
        public SparseArray_Mirror Elements;
        public Pointer Hash;
        public int InlineHash;
        public int HashSize;
    }
    public class Map_Mirror
    {
        public Set_Mirror Pairs;
    }
    public class MultiMap_Mirror
    {
        public Set_Mirror Pairs;
    }
    public class RwVector2
    {
        public float X;
        public float Y;
    }
    public class RwVector3
    {
        public float X;
        public float Y;
        public float Z;
    }
    public class RwVector4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }
    public class RwPlane
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }
    public class RwMatrix44
    {
        public RwPlane XPlane;
        public RwPlane YPlane;
        public RwPlane ZPlane;
        public RwPlane WPlane;
    }
    public class RwQuat
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }
    public class BioRwBox
    {
        public RwVector3 Min;
        public RwVector3 Max;
        public byte IsValid;
    }
    public class UntypedBulkData_Mirror
    {
        public Pointer VfTable;
        public int BulkDataFlags;
        public int ElementCount;
        public int BulkDataOffsetInFile;
        public int BulkDataSizeOnDisk;
        public int SavedBulkDataFlags;
        public int SavedElementCount;
        public int SavedBulkDataOffsetInFile;
        public int SavedBulkDataSizeOnDisk;
        public Pointer BulkData;
        public int LockStatus;
        public Pointer AttachedAr;
        public int bShouldFreeOnEmpty;
    }
    public class RenderCommandFence_Mirror
    {
    }
    public class FColorVertexBuffer_Mirror
    {
        public Pointer VfTable;
        public Pointer VertexData;
        public int Data;
        public int Stride;
        public int NumVertices;
    }
    public class IndirectArray_Mirror
    {
        public Pointer Data;
        public int ArrayNum;
        public int ArrayMax;
    }
    public class Array_Mirror
    {
        public Pointer Data;
        public int ArrayNum;
        public int ArrayMax;
    }
    public class Guid
    {
        public int A;
        public int B;
        public int C;
        public int D;
    }
    public class Vector
    {
        public float X;
        public float Y;
        public float Z;
    }
    public class Color
    {
        public byte B;
        public byte G;
        public byte R;
        public byte A;
    }
    public class LinearColor
    {
        public float R;
        public float G;
        public float B;
        public float A;
    }
    public class Box
    {
        public byte IsValid;
        public Vector Min;
        public Vector Max;
    }
    public class Vector4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }
    public class Vector2D
    {
        public float X;
        public float Y;
    }
    public class TwoVectors
    {
        public Vector v1;
        public Vector v2;
    }
    public class Plane : Vector
    {
        public float W;
    }
    public class Rotator
    {
        public int Pitch;
        public int Yaw;
        public int Roll;
    }
    public class Quat
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }
    public class IntPoint
    {
        public int X;
        public int Y;
    }
    public class SHVector
    {
        public float V;
        public float Padding;
    }
    public class SHVectorRGB
    {
        public SHVector R;
        public SHVector G;
        public SHVector B;
    }
    public class TPOV
    {
        public Vector location;
        public Rotator Rotation;
        public float FOV;
    }
    public class TAlphaBlend
    {
        public AlphaBlendType BlendType;
        public float AlphaIn;
        public float AlphaOut;
        public float AlphaTarget;
        public float BlendTime;
        public float BlendTimeToGo;
    }
    public class BoxSphereBounds
    {
        public float SphereRadius;
        public Vector Origin;
        public Vector BoxExtent;
    }
    public class Matrix
    {
        public Plane XPlane;
        public Plane YPlane;
        public Plane ZPlane;
        public Plane WPlane;
    }
    public class Cylinder
    {
        public float Height;
        public float Radius;
    }
    public class SFXTextureRefCount
    {
        public int Texture;
        public int RefCount;
    }
    public class TimerData
    {
        public NameReference FuncName;
        public float Rate;
        public float Count;
        public float TimerTimeDilation;
        public int TimerObj;
        public bool bLoop;
        public bool bPaused;
    }
    public class TraceHitInfo
    {
        public NameReference BoneName;
        public int Material;
        public int PhysMaterial;
        public int Item;
        public int LevelIndex;
        public int HitComponent;
    }
    public class ImpactInfo
    {
        public TraceHitInfo HitInfo;
        public Vector HitLocation;
        public Vector HitNormal;
        public Vector RayDir;
        public Vector StartTrace;
        public int HitActor;
        public float PenetrationDepth;
    }
    public class AnimSlotInfo
    {
        public float[] ChannelWeights;
        public NameReference SlotName;
    }
    public class AnimSlotDesc
    {
        public NameReference SlotName;
        public int NumChannels;
    }
    public class KeyValuePair
    {
        public string Key;
        public string Value;
    }
    public class PlayerResponseLine
    {
        public string PlayerName;
        public KeyValuePair[] PlayerInfo;
        public int PlayerNum;
        public int PlayerID;
        public int Ping;
        public int Score;
        public int StatsID;
    }
    public class ServerResponseLine
    {
        public string IP;
        public string ServerName;
        public string MapName;
        public string GameType;
        public KeyValuePair[] ServerInfo;
        public PlayerResponseLine[] PlayerInfo;
        public int ServerID;
        public int Port;
        public int QueryPort;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int Ping;
    }
    public class UniqueNetId
    {
        public QWord Uid;
    }
    public class LightingChannelContainer
    {
        public bool bInitialized;
        public bool BSP;
        public bool Static;
        public bool Dynamic;
        public bool CompositeDynamic;
        public bool Skybox;
        public bool Unnamed;
        public bool Cinematic;
        public bool Gameplay;
        public bool Crowd;
    }
    public class RBCollisionChannelContainer
    {
        public bool Default;
        public bool Nothing;
        public bool Pawn;
        public bool Vehicle;
        public bool Water;
        public bool GameplayPhysics;
        public bool EffectPhysics;
        public bool Untitled1;
        public bool Untitled2;
        public bool Untitled3;
        public bool Untitled4;
        public bool Cloth;
        public bool FluidDrain;
        public bool SoftBody;
        public bool FracturedMeshPart;
        public bool BlockingVolume;
        public bool DeadPawn;
    }
    public class MaterialViewRelevance
    {
        public bool bOpaque;
        public bool bTranslucent;
        public bool bDistortion;
        public bool bOneLayerDistortionRelevance;
        public bool bLit;
        public bool bUsesSceneColor;
    }
    public class RigidBodyState
    {
        public Vector Position;
        public Quat Quaternion;
        public Vector LinVel;
        public Vector AngVel;
        public byte bNewData;
    }
    public class RigidBodyContactInfo
    {
        public Vector ContactPosition;
        public Vector ContactNormal;
        public float ContactPenetration;
        public Vector ContactVelocity;
        public int PhysMaterial;
    }
    public class CollisionImpactData
    {
        public RigidBodyContactInfo[] ContactInfos;
        public Vector TotalNormalForceVector;
        public Vector TotalFrictionForceVector;
    }
    public class ReplicatedHitImpulse
    {
        public Vector AppliedImpulse;
        public Vector HitLocation;
        public NameReference BoneName;
        public bool bRadialImpulse;
        public byte ImpulseCount;
    }
    public class PhysEffectInfo
    {
        public float Threshold;
        public float ReFireDelay;
        public int Effect;
        public int Sound;
    }
    public class ActorReference
    {
        public Guid Guid;
        public int Actor;
    }
    public class NavReference
    {
        public Guid Guid;
        public int Nav;
    }
    public class BasedPosition
    {
        public Vector Position;
        public Vector CachedBaseLocation;
        public Rotator CachedBaseRotation;
        public Vector CachedTransPosition;
        public int Base;
    }
    public class VisiblePortalInfo
    {
        public int Source;
        public int Destination;
    }
    public class LUTBlender
    {
        public int[] LUTTextures;
        public float[] LUTWeights;
    }
    public class PostProcessSettings
    {
        public LUTBlender ColorGradingLUT;
        public LinearColor RimShader_Color;
        public Vector DOF_FocusPosition;
        public Vector Scene_HighLights;
        public Vector Scene_MidTones;
        public Vector Scene_Shadows;
        public float Bloom_Scale;
        public float Bloom_Threshold;
        public Color Bloom_Tint;
        public float Bloom_ScreenBlendThreshold;
        public float Bloom_InterpolationDuration;
        public float DOF_FalloffExponent;
        public float DOF_BlurKernelSize;
        public float DOF_BlurBloomKernelSize;
        public float DOF_MaxNearBlurAmount;
        public float DOF_MaxFarBlurAmount;
        public Color DOF_ModulateBlurColor;
        public float DOF_FocusInnerRadius;
        public float DOF_FocusDistance;
        public float DOF_InterpolationDuration;
        public float MotionBlur_MaxVelocity;
        public float MotionBlur_Amount;
        public float MotionBlur_CameraRotationThreshold;
        public float MotionBlur_CameraTranslationThreshold;
        public float MotionBlur_InterpolationDuration;
        public float Scene_Desaturation;
        public float Scene_InterpolationDuration;
        public float RimShader_InterpolationDuration;
        public int ColorGrading_LookupTable;
        public bool bOverride_EnableBloom;
        public bool bOverride_EnableDOF;
        public bool bOverride_EnableMotionBlur;
        public bool bOverride_EnableSceneEffect;
        public bool bOverride_AllowAmbientOcclusion;
        public bool bOverride_OverrideRimShaderColor;
        public bool bOverride_Bloom_Scale;
        public bool bOverride_Bloom_Threshold;
        public bool bOverride_Bloom_Tint;
        public bool bOverride_Bloom_ScreenBlendThreshold;
        public bool bOverride_Bloom_InterpolationDuration;
        public bool bOverride_DOF_FalloffExponent;
        public bool bOverride_DOF_BlurKernelSize;
        public bool bOverride_DOF_BlurBloomKernelSize;
        public bool bOverride_DOF_MaxNearBlurAmount;
        public bool bOverride_DOF_MaxFarBlurAmount;
        public bool bOverride_DOF_ModulateBlurColor;
        public bool bOverride_DOF_FocusType;
        public bool bOverride_DOF_FocusInnerRadius;
        public bool bOverride_DOF_FocusDistance;
        public bool bOverride_DOF_FocusPosition;
        public bool bOverride_DOF_InterpolationDuration;
        public bool bOverride_MotionBlur_MaxVelocity;
        public bool bOverride_MotionBlur_Amount;
        public bool bOverride_MotionBlur_FullMotionBlur;
        public bool bOverride_MotionBlur_CameraRotationThreshold;
        public bool bOverride_MotionBlur_CameraTranslationThreshold;
        public bool bOverride_MotionBlur_InterpolationDuration;
        public bool bOverride_Scene_Desaturation;
        public bool bOverride_Scene_HighLights;
        public bool bOverride_Scene_MidTones;
        public bool bOverride_Scene_Shadows;
        public bool bOverride_Scene_InterpolationDuration;
        public bool bOverride_RimShader_Color;
        public bool bOverride_RimShader_InterpolationDuration;
        public bool bEnableBloom;
        public bool bEnableDOF;
        public bool bEnableMotionBlur;
        public bool bEnableSceneEffect;
        public bool bAllowAmbientOcclusion;
        public bool bOverrideRimShaderColor;
        public bool bOverride_EnableFilmic;
        public bool bEnableFilmic;
        public bool bOverride_EnableVignette;
        public bool bEnableVignette;
        public bool bOverride_EnableFilmGrain;
        public bool bEnableFilmGrain;
        public bool MotionBlur_FullMotionBlur;
        public EFocusType DOF_FocusType;
    }
    public class ViewTargetTransitionParams
    {
        public float BlendTime;
        public float BlendExp;
        public bool bSkipCameraReset;
        public EViewTargetBlendFunction BlendFunction;
    }
    public class TCameraCache
    {
        public TPOV POV;
        public float TimeStamp;
    }
    public class TViewTarget
    {
        public TPOV POV;
        public int Target;
        public int Controller;
        public float AspectRatio;
        public int PRI;
    }
    public class RemoteEventParameter
    {
        public NameReference ParameterName;
        public KismetVarTypes VariableType;
    }
    public class SeqOpInputLink
    {
        public string LinkDesc;
        public NameReference LinkAction;
        public int QueuedActivations;
        public int LinkedOp;
        public bool bHasImpulse;
        public bool bDisabled;
    }
    public class SeqOpOutputInputLink
    {
        public int LinkedOp;
        public int InputLinkIdx;
    }
    public class SeqOpOutputLink
    {
        public SeqOpOutputInputLink[] Links;
        public string LinkDesc;
        public NameReference LinkAction;
        public int LinkedOp;
        public bool bHasImpulse;
        public bool bDisabled;
    }
    public class SeqVarLink
    {
        public int[] LinkedVariables;
        public string LinkDesc;
        public int ExpectedType;
        public NameReference LinkVar;
        public NameReference PropertyName;
        public int MinVars;
        public int MaxVars;
        public bool bWriteable;
        public bool bModifiesLinkedObject;
        public bool bAllowAnyType;
    }
    public class SeqEventLink
    {
        public int[] LinkedEvents;
        public string LinkDesc;
        public int ExpectedType;
    }
    public class FriendsQuery
    {
        public UniqueNetId UniqueId;
        public bool bIsFriend;
    }
    public class OnlineFriend
    {
        public UniqueNetId UniqueId;
        public QWord SessionId;
        public string NickName;
        public string PresenceInfo;
        public bool bIsOnline;
        public bool bIsPlaying;
        public bool bIsPlayingThisGame;
        public bool bIsJoinable;
        public bool bHasVoiceSupport;
        public bool bHaveInvited;
        public bool bHasInvitedYou;
        public EOnlineFriendState FriendState;
    }
    public class OnlineContent
    {
        public string FriendlyName;
        public string ContentPath;
        public string[] ContentPackages;
        public string[] ContentFiles;
        public int UserIndex;
    }
    public class OnlineRegistrant
    {
        public UniqueNetId PlayerNetId;
    }
    public class BoneDrivenMaterialParameter
    {
        public int MaterialSlot;
        public NameReference ParameterName;
        public NameReference BoneName;
        public bool LocalSpace;
        public int Axis;
    }
    public class BoneOverrideInfo
    {
        public int BoneIndex;
        public Vector Position;
        public Quat Orientation;
        public Matrix BasesInvMatrix;
    }
    public class AnimTickEntry
    {
        public int Node;
        public float TotalWeight;
        public float TotalWeightAccumulator;
        public bool bRelevant;
        public bool bBioNeedTickingCompleteCall;
        public bool bSkipTickWhenZeroWeight;
        public bool bTickDuringPausedAnims;
    }
    public class ActiveMorph
    {
        public int Target;
        public float Weight;
    }
    public class Attachment
    {
        public int Component;
        public NameReference BoneName;
        public Vector RelativeLocation;
        public Rotator RelativeRotation;
        public Vector RelativeScale;
    }
    public class BioActorReBase
    {
        public int ActorToReBase;
        public int NewBase;
        public int SkelComponent;
        public Vector NewFloor;
        public NameReference AttachName;
        public bool bNotifyActor;
    }
    public class BioActorAttach
    {
        public int Attachment;
        public Vector RelativeOffset;
        public Rotator RelativeRotation;
        public NameReference BoneName;
        public bool bDetach;
        public bool bHardAttach;
        public bool bUseRelativeOffset;
        public bool bUseRelativeRotation;
    }
    public class BonePair
    {
        public NameReference Bones;
    }
    public class InputEntry
    {
        public float Value;
        public float TimeDelta;
        public EInputTypes Type;
        public EInputMatchAction Action;
    }
    public class InputMatchRequest
    {
        public InputEntry[] Inputs;
        public int MatchDelegate;
        public NameReference MatchFuncName;
        public NameReference FailedFuncName;
        public NameReference RequestName;
        public int MatchActor;
    }
    public class DebugTextInfo
    {
        public string DebugText;
        public Vector SrcActorOffset;
        public Vector SrcActorDesiredOffset;
        public int SrcActor;
        public float Duration;
        public Color TextColor;
        public bool bAbsoluteLocation;
    }
    public class ClientAdjustment
    {
        public Vector NewLoc;
        public Vector NewVel;
        public Vector NewFloor;
        public float TimeStamp;
        public int NewBase;
        public EPhysics newPhysics;
        public byte bAckGoodMove;
        public byte bWarning;
    }
    public class ConsoleMessage
    {
        public string Text;
        public Color TextColor;
        public float MessageLife;
        public int PRI;
    }
    public class HudLocalizedMessage
    {
        public string StringMessage;
        public int Message;
        public int Switch;
        public float EndOfLife;
        public float Lifetime;
        public float PosY;
        public Color DrawColor;
        public int FontSize;
        public int StringFont;
        public float dx;
        public float DY;
        public int Count;
        public int OptionalObject;
        public bool Drawn;
    }
    public class KismetDrawTextInfo
    {
        public string MessageText;
        public Vector2D MessageFontScale;
        public Vector2D MessageOffset;
        public int MessageFont;
        public Color MessageColor;
        public float MessageEndTime;
    }
    public class OnlineArbitrationRegistrant : OnlineRegistrant
    {
        public QWord MachineId;
        public int Trustworthiness;
    }
    public class SpeechRecognizedWord
    {
        public int WordId;
        public string WordText;
        public float Confidence;
    }
    public class OnlinePlayerScore
    {
        public UniqueNetId PlayerID;
        public int TeamID;
        public int Score;
    }
    public class OnlineGameSearchResult
    {
        public Pointer PlatformData;
        public int GameSettings;
    }
    public class NavigationOctreeObject
    {
        public Box BoundingBox;
        public Vector BoxCenter;
        public int Owner;
        public byte OwnerType;
    }
    public class DebugNavCost
    {
        public string Desc;
        public int Cost;
    }
    public class CheckpointRecord
    {
        public bool bDisabled;
        public bool bBlocked;
    }
    public class PolyReference
    {
        public ActorReference OwningPylon;
        public int PolyId;
    }
    public class CachedValue
    {
        public float Value;
    }
    public class AmbientSoundSlot
    {
        public int Wave;
        public float PitchScale;
        public float VolumeScale;
        public float Weight;
    }
    public class CompressedTrack
    {
        public byte[] ByteStream;
        public float[] Times;
        public float Mins;
        public float Ranges;
    }
    public class TimeModifier
    {
        public float Time;
        public float TargetStrength;
    }
    public class SkelControlModifier
    {
        public TimeModifier[] Modifiers;
        public NameReference SkelControlName;
    }
    public class TranslationTrack
    {
        public Vector[] PosKeys;
        public float[] Times;
    }
    public class RotationTrack
    {
        public Quat[] RotKeys;
        public float[] Times;
    }
    public class CurveTrack
    {
        public float[] CurveWeights;
        public NameReference CurveName;
    }
    public class AnimNotifyEvent
    {
        public NameReference Comment;
        public float Time;
        public float Duration;
        public int Notify;
        public bool bIgnoreWeightThreshold;
    }
    public class RawAnimSequenceTrack
    {
        public Vector[] PosKeys;
        public Quat[] RotKeys;
    }
    public class AnimTag
    {
        public string Tag;
        public string[] Contains;
    }
    public class BoneTransform
    {
    }
    public class CurveKey
    {
        public NameReference CurveName;
        public float Weight;
    }
    public class AnimBlendChild
    {
        public NameReference Name;
        public float Weight;
        public int Anim;
        public bool bMirrorSkeleton;
        public bool bIsAdditive;
    }
    public class WeightNodeRule
    {
        public NameReference NodeName;
        public int CachedNode;
        public int CachedSlotNode;
        public int ChildIndex;
        public EWeightCheck WeightCheck;
    }
    public class WeightRule
    {
        public WeightNodeRule FirstNode;
        public WeightNodeRule SecondNode;
    }
    public class BranchInfo
    {
        public NameReference BoneName;
        public float PerBoneWeightIncrease;
    }
    public class PerBoneMaskInfo
    {
        public BranchInfo[] BranchList;
        public WeightRule[] WeightRuleList;
        public float DesiredWeight;
        public float BlendTimeToGo;
        public bool bWeightBasedOnNodeRules;
        public bool bDisableForNonLocalHumanPlayers;
    }
    public class AimTransform
    {
        public Quat Quaternion;
        public Vector Translation;
    }
    public class AimComponent
    {
        public AimTransform LU;
        public AimTransform LC;
        public AimTransform LD;
        public AimTransform CU;
        public AimTransform CC;
        public AimTransform CD;
        public AimTransform RU;
        public AimTransform RC;
        public AimTransform RD;
        public NameReference BoneName;
    }
    public class AimOffsetProfile
    {
        public AimComponent[] AimComponents;
        public NameReference ProfileName;
        public Vector2D HorizontalRange;
        public Vector2D VerticalRange;
        public NameReference AnimName_LU;
        public NameReference AnimName_LC;
        public NameReference AnimName_LD;
        public NameReference AnimName_CU;
        public NameReference AnimName_CC;
        public NameReference AnimName_CD;
        public NameReference AnimName_RU;
        public NameReference AnimName_RC;
        public NameReference AnimName_RD;
    }
    public class ChildBoneBlendInfo
    {
        public float[] TargetPerBoneWeight;
        public NameReference InitTargetStartBone;
        public NameReference OldStartBone;
        public float InitPerBoneIncrease;
        public float OldBoneIncrease;
    }
    public class RandomAnimInfo
    {
        public Vector2D PlayRateRange;
        public float Chance;
        public float BlendInTime;
        public bool bStillFrame;
        public byte LoopCountMin;
        public byte LoopCountMax;
    }
    public class AnimInfo
    {
        public NameReference AnimSeqName;
    }
    public class AnimBlendInfo
    {
        public AnimInfo AnimInfo;
        public NameReference AnimName;
    }
    public class SynchGroup
    {
        public int[] SeqNodes;
        public NameReference GroupName;
        public float RateScale;
        public bool bFireSlaveNotifies;
    }
    public class TrailSocketSamplePoint
    {
        public Vector Position;
        public Vector Velocity;
    }
    public class TrailSamplePoint
    {
        public TrailSocketSamplePoint FirstEdgeSample;
        public TrailSocketSamplePoint SecondEdgeSample;
        public TrailSocketSamplePoint ControlPointSample;
        public float RelativeTime;
    }
    public class TrailSample
    {
        public Vector FirstEdgeSample;
        public Vector SecondEdgeSample;
        public Vector ControlPointSample;
        public float RelativeTime;
    }
    public class AnimSetMeshLinkup
    {
        public QWord SkelMeshLinkupRUID;
        public int[] BoneToTrackTable;
        public byte[] BoneUseAnimTranslation;
        public byte[] ForceUseMeshTranslation;
    }
    public class AnimGroup
    {
        public NameReference GroupName;
        public float RateScale;
        public float SynchPctPosition;
    }
    public class SkelControlListHead
    {
        public NameReference BoneName;
        public int ControlHead;
    }
    public class PreviewSkelMeshStruct
    {
        public int[] PreviewMorphSets;
        public NameReference DisplayName;
        public int PreviewSkelMesh;
    }
    public class PreviewSocketStruct
    {
        public NameReference DisplayName;
        public NameReference SocketName;
        public int PreviewSkelMesh;
        public int PreviewStaticMesh;
    }
    public class PreviewAnimSetsStruct
    {
        public int[] PreviewAnimSets;
        public NameReference DisplayName;
    }
    public class StaticLightingMaskContainer
    {
        public bool bInitialized;
        public bool Unnamed;
    }
    public class NxDestructibleDepthParameters
    {
        public bool TAKE_IMPACT_DAMAGE;
        public bool IGNORE_POSE_UPDATES;
        public bool IGNORE_RAYCAST_CALLBACKS;
        public bool IGNORE_CONTACT_CALLBACKS;
        public bool USER_FLAG;
    }
    public class NxDestructibleParametersFlag
    {
        public bool ACCUMULATE_DAMAGE;
        public bool ASSET_DEFINED_SUPPORT;
        public bool WORLD_SUPPORT;
        public bool DEBRIS_TIMEOUT;
        public bool DEBRIS_MAX_SEPARATION;
        public bool CRUMBLE_SMALLEST_CHUNKS;
        public bool ACCURATE_RAYCASTS;
        public bool USE_VALID_BOUNDS;
    }
    public class NxDestructibleParameters
    {
        public NxDestructibleDepthParameters[] DepthParameters;
        public Box ValidBounds;
        public float DamageThreshold;
        public float DamageToRadius;
        public float DamageCap;
        public float ForceToDamage;
        public float ImpactVelocityThreshold;
        public float DamageToPercentDeformation;
        public float DeformationPercentLimit;
        public int SupportDepth;
        public int DebrisDepth;
        public int EssentialDepth;
        public float DebrisLifetimeMin;
        public float DebrisLifetimeMax;
        public float DebrisMaxSeparationMin;
        public float DebrisMaxSeparationMax;
        public float MaxChunkSpeed;
        public float MassScaleExponent;
        public NxDestructibleParametersFlag Flags;
        public float GrbVolumeLimit;
        public float GrbParticleSpacing;
        public float FractureImpulseScale;
        public bool bFormExtendedStructures;
    }
    public class AudioComponentParam
    {
        public NameReference ParamName;
        public float FloatParam;
        public int WaveParam;
    }
    public class SubtitleCue
    {
        public string Text;
        public float Time;
    }
    public class KSphereElem
    {
        public Matrix TM;
        public float Radius;
        public bool bNoRBCollision;
        public bool bPerPolyShape;
    }
    public class KBoxElem
    {
        public Matrix TM;
        public float X;
        public float Y;
        public float Z;
        public bool bNoRBCollision;
        public bool bPerPolyShape;
    }
    public class KSphylElem
    {
        public Matrix TM;
        public float Radius;
        public float Length;
        public bool bNoRBCollision;
        public bool bPerPolyShape;
    }
    public class KConvexElem
    {
        public Vector[] VertexData;
        public Plane[] PermutedVertexData;
        public int[] FaceTriData;
        public Vector[] EdgeDirections;
        public Vector[] FaceNormalDirections;
        public Plane[] FacePlaneData;
        public Box ElemBox;
    }
    public class KAggregateGeom
    {
        public KSphereElem[] SphereElems;
        public KBoxElem[] BoxElems;
        public KSphylElem[] SphylElems;
        public KConvexElem[] ConvexElems;
        public Pointer RenderInfo;
        public bool bSkipCloseAndParallelChecks;
    }
    public class KCachedConvexData_Mirror
    {
        public int[] CachedConvexElements;
    }
    public class GeomSelection
    {
        public int Type;
        public int Index;
        public int SelectionIndex;
    }
    public class ReverbSettings
    {
        public float Volume;
        public float FadeTime;
        public bool bApplyReverb;
        public ReverbPreset ReverbType;
    }
    public class InteriorSettings
    {
        public float ExteriorVolume;
        public float ExteriorTime;
        public float ExteriorLPF;
        public float ExteriorLPFTime;
        public float InteriorVolume;
        public float InteriorTime;
        public float InteriorLPF;
        public float InteriorLPFTime;
        public bool bIsWorldInfo;
    }
    public class Listener
    {
        public Vector location;
        public Vector Up;
        public Vector Right;
        public Vector Front;
        public int PortalVolume;
    }
    public class AudioClassInfo
    {
        public int NumResident;
        public int SizeResident;
        public int NumRealTime;
        public int SizeRealTime;
    }
    public class BioStageDOFData
    {
        public float fFocusInnerRadius;
        public float fFocusDistance;
        public bool bEnable;
    }
    public class BioTrackKey
    {
        public NameReference KeyName;
        public float fTime;
    }
    public class BioFlareColour
    {
        public Vector Tint;
        public float IntensityThreshold;
    }
    public class BioFlareParameters
    {
        public BioFlareColour FlareBase;
        public BioFlareColour FlareHot;
        public float FlareTintMultiplier;
        public float LineLength;
        public float FalloffParameter;
    }
    public class ExpressionInput
    {
        public int Expression;
        public int Mask;
        public int MaskR;
        public int MaskG;
        public int MaskB;
        public int MaskA;
        public int GCC64_Padding;
    }
    public class LightmassMaterialInterfaceSettings
    {
        public float EmissiveBoost;
        public float DiffuseBoost;
        public float SpecularBoost;
        public float ExportResolutionScale;
        public float DistanceFieldPenumbraScale;
        public bool bOverrideEmissiveBoost;
        public bool bOverrideDiffuseBoost;
        public bool bOverrideSpecularBoost;
        public bool bOverrideExportResolutionScale;
        public bool bOverrideDistanceFieldPenumbraScale;
    }
    public class FontParameterValue
    {
        public Guid ExpressionGUID;
        public NameReference ParameterName;
        public int FontValue;
        public int FontPage;
    }
    public class ScalarParameterValue
    {
        public Guid ExpressionGUID;
        public NameReference ParameterName;
        public float ParameterValue;
    }
    public class TextureParameterValue
    {
        public Guid ExpressionGUID;
        public NameReference ParameterName;
        public int ParameterValue;
    }
    public class VectorParameterValue
    {
        public LinearColor ParameterValue;
        public Guid ExpressionGUID;
        public NameReference ParameterName;
    }
    public class CameraShakeInstance
    {
        public Matrix UserPlaySpaceMatrix;
        public Vector LocSinOffset;
        public Vector RotSinOffset;
        public int SourceShake;
        public float OscillatorTimeRemaining;
        public float CurrentBlendInTime;
        public float CurrentBlendOutTime;
        public float FOVSinOffset;
        public float Scale;
        public int AnimInst;
        public bool bBlendingIn;
        public bool bBlendingOut;
        public ECameraAnimPlaySpace PlaySpace;
    }
    public class FOscillator
    {
        public float Amplitude;
        public float Frequency;
        public EInitialOscillatorOffset InitialOffset;
    }
    public class FontImportOptionsData
    {
        public string FontName;
        public string Chars;
        public string UnicodeRange;
        public string CharsFilePath;
        public string CharsFileWildcard;
        public LinearColor ForegroundColor;
        public float Height;
        public int TexturePageWidth;
        public int TexturePageMaxHeight;
        public int XPadding;
        public int YPadding;
        public int ExtendBoxTop;
        public int ExtendBoxBottom;
        public int ExtendBoxRight;
        public int ExtendBoxLeft;
        public int Kerning;
        public int DistanceFieldScaleFactor;
        public bool bEnableAntialiasing;
        public bool bEnableBold;
        public bool bEnableItalic;
        public bool bEnableUnderline;
        public bool bAlphaOnly;
        public bool bCreatePrintableOnly;
        public bool bIncludeASCIIRange;
        public bool bEnableDropShadow;
        public bool bEnableLegacyMode;
        public bool bUseDistanceFieldAlpha;
        public EFontImportCharacterSet CharacterSet;
    }
    public class FontCharacter
    {
        public int StartU;
        public int StartV;
        public int USize;
        public int VSize;
        public byte TextureIndex;
        public int VerticalOffset;
    }
    public class TextureGroupContainer
    {
        public bool TEXTUREGROUP_World;
        public bool TEXTUREGROUP_WorldNormalMap;
        public bool TEXTUREGROUP_WorldSpecular;
        public bool TEXTUREGROUP_Character;
        public bool TEXTUREGROUP_CharacterNormalMap;
        public bool TEXTUREGROUP_CharacterSpecular;
        public bool TEXTUREGROUP_Weapon;
        public bool TEXTUREGROUP_WeaponNormalMap;
        public bool TEXTUREGROUP_WeaponSpecular;
        public bool TEXTUREGROUP_Vehicle;
        public bool TEXTUREGROUP_VehicleNormalMap;
        public bool TEXTUREGROUP_VehicleSpecular;
        public bool TEXTUREGROUP_Cinematic;
        public bool TEXTUREGROUP_Effects;
        public bool TEXTUREGROUP_EffectsNotFiltered;
        public bool TEXTUREGROUP_Skybox;
        public bool TEXTUREGROUP_UI;
        public bool TEXTUREGROUP_Lightmap;
        public bool TEXTUREGROUP_RenderTarget;
        public bool TEXTUREGROUP_MobileFlattened;
        public bool TEXTUREGROUP_ProcBuilding_Face;
        public bool TEXTUREGROUP_ProcBuilding_LightMap;
        public bool TEXTUREGROUP_Shadowmap;
    }
    public class FadeMipMapChannelsContainer
    {
        public bool FadeRedChannel;
        public bool FadeGreenChannel;
        public bool FadeBlueChannel;
        public bool FadeAlphaChannel;
    }
    public class Texture2DMipMap
    {
        public UntypedBulkData_Mirror Data;
        public int SizeX;
        public int SizeY;
    }
    public class CanvasIcon
    {
        public int Texture;
        public float U;
        public float V;
        public float UL;
        public float VL;
    }
    public class DepthFieldGlowInfo
    {
        public LinearColor GlowColor;
        public Vector2D GlowOuterRadius;
        public Vector2D GlowInnerRadius;
        public bool bEnableGlow;
    }
    public class FontRenderInfo
    {
        public DepthFieldGlowInfo GlowInfo;
        public bool bClipText;
        public bool bEnableShadow;
    }
    public class PhysXEmitterVerticalProperties
    {
        public int ParticlesLodMin;
        public int ParticlesLodMax;
        public int PacketsPerPhysXParticleSystemMax;
        public float SpawnLodVsFifoBias;
        public bool bDisableLod;
        public bool bApplyCylindricalPacketCulling;
    }
    public class PhysXVerticalProperties
    {
        public PhysXEmitterVerticalProperties Emitters;
    }
    public class NetViewer
    {
        public Vector ViewLocation;
        public Vector ViewDir;
        public int InViewer;
        public int Viewer;
    }
    public class CompartmentRunList
    {
        public bool RigidBody;
        public bool Fluid;
        public bool Cloth;
        public bool SoftBody;
    }
    public class PhysXSimulationProperties
    {
        public float TimeStep;
        public int MaxSubSteps;
        public bool bUseHardware;
        public bool bFixedTimeStep;
    }
    public class PhysXSceneProperties
    {
        public PhysXSimulationProperties PrimaryScene;
        public PhysXSimulationProperties CompartmentRigidBody;
        public PhysXSimulationProperties CompartmentFluid;
        public PhysXSimulationProperties CompartmentCloth;
        public PhysXSimulationProperties CompartmentSoftBody;
    }
    public class ApexModuleDestructibleSettings
    {
        public int MaxChunkIslandCount;
        public int MaxRrbActorCount;
        public float MaxChunkSeparationLOD;
    }
    public class WorldFractureSettings
    {
        public float ChanceOfPhysicsChunkOverride;
        public float MaxExplosionChunkSize;
        public float MaxDamageChunkSize;
        public int MaxNumFacturedChunksToSpawnInAFrame;
        public float FractureExplosionVelScale;
        public bool bEnableChanceOfPhysicsChunkOverride;
        public bool bLimitExplosionChunkSize;
        public bool bLimitDamageChunkSize;
    }
    public class ScreenMessageString
    {
    }
    public class LightmassWorldInfoSettings
    {
        public float OcclusionCurveLookupTable;
        public float StaticLightingLevelScale;
        public int NumIndirectLightingBounces;
        public Color EnvironmentColor;
        public float EnvironmentIntensity;
        public float EmissiveBoost;
        public float DiffuseBoost;
        public float SpecularBoost;
        public float IndirectNormalInfluenceBoost;
        public float LightEnvironmentIndirectContrastFactor;
        public float DirectIlluminationOcclusionFraction;
        public float IndirectIlluminationOcclusionFraction;
        public float OcclusionExponent;
        public float FullyOccludedSamplesFraction;
        public float MaxOcclusionDistance;
        public bool bUseAmbientOcclusion;
        public bool bVisualizeMaterialDiffuse;
        public bool bVisualizeAmbientOcclusion;
    }
    public class NavMeshPathConstraintCacheDatum
    {
        public int List;
        public int ListIdx;
    }
    public class NavMeshPathGoalEvaluatorCacheDatum
    {
        public int List;
        public int ListIdx;
    }
    public class MusicTrackStruct
    {
        public int TheSoundCue;
        public float FadeInTime;
        public float FadeInVolumeLevel;
        public float FadeOutTime;
        public float FadeOutVolumeLevel;
        public bool bAutoPlay;
        public bool bPersistentAcrossLevels;
    }
    public class WIDGET_ID : Guid
    {
    }
    public class STYLE_ID : Guid
    {
    }
    public class UIRangeData
    {
        public float CurrentValue;
        public float MinValue;
        public float MaxValue;
        public float NudgeValue;
        public bool bIntRange;
    }
    public class TextureCoordinates
    {
        public float U;
        public float V;
        public float UL;
        public float VL;
    }
    public class UIProviderScriptFieldValue
    {
        public UniqueNetId NetIdValue;
        public string StringValue;
        public int[] ArrayValue;
        public UIRangeData RangeValue;
        public TextureCoordinates AtlasCoordinates;
        public NameReference PropertyTag;
        public int ImageValue;
        public EUIDataProviderFieldType PropertyType;
    }
    public class UIProviderFieldValue : UIProviderScriptFieldValue
    {
    }
    public class UIStyleReference
    {
        public int RequiredStyleClass;
        public STYLE_ID AssignedStyleID;
        public NameReference DefaultStyleTag;
    }
    public class UIScreenValue
    {
        public float Value;
        public EPositionEvalType ScaleType;
        public EUIOrientation Orientation;
    }
    public class UIScreenValue_Extent
    {
        public float Value;
        public EUIExtentEvalType ScaleType;
        public EUIOrientation Orientation;
    }
    public class UIScreenValue_Position
    {
        public float Value;
        public EPositionEvalType ScaleType;
    }
    public class UIScreenValue_Bounds
    {
        public float Value;
        public EPositionEvalType ScaleType;
        public EUIAspectRatioConstraint AspectRatioMode;
    }
    public class UIAnchorPosition : UIScreenValue_Position
    {
        public float ZDepth;
    }
    public class ScreenPositionRange : UIScreenValue_Position
    {
    }
    public class UIScreenValue_DockPadding
    {
        public float PaddingValue;
        public EUIDockPaddingEvalType PaddingScaleType;
    }
    public class UIScreenValue_AutoSizeRegion
    {
        public float Value;
        public EUIExtentEvalType EvalType;
    }
    public class AutoSizePadding : UIScreenValue_AutoSizeRegion
    {
    }
    public class AutoSizeData
    {
        public UIScreenValue_AutoSizeRegion Extent;
        public AutoSizePadding Padding;
        public bool bAutoSizeEnabled;
    }
    public class UIRenderingSubregion
    {
        public UIScreenValue_Extent ClampRegionSize;
        public UIScreenValue_Extent ClampRegionOffset;
        public bool bSubregionEnabled;
        public EUIAlignment ClampRegionAlignment;
    }
    public class InputEventSubscription
    {
        public int[] Subscribers;
        public NameReference KeyName;
    }
    public class DefaultEventSpecification
    {
        public int EventState;
        public int EventTemplate;
    }
    public class InputKeyAction
    {
        public SeqOpOutputInputLink[] TriggeredOps;
        public NameReference InputKeyName;
        public EInputEvent InputKeyState;
    }
    public class StateInputKeyAction : InputKeyAction
    {
        public int Scope;
    }
    public class PlayerInteractionData
    {
    }
    public class UIFocusPropagationData
    {
    }
    public class UINavigationData
    {
        public int ForcedNavigationTarget;
        public byte bNullOverride;
    }
    public class UIDockingSet
    {
        public UIScreenValue_DockPadding DockPadding;
        public int TargetWidget;
        public int OwnerWidget;
        public bool bLockWidthWhenDocked;
        public bool bLockHeightWhenDocked;
        public EUIWidgetFace TargetFace;
    }
    public class UIDockingNode
    {
        public int Widget;
        public EUIWidgetFace Face;
    }
    public class UIRotation
    {
        public UIAnchorPosition AnchorPosition;
        public Rotator Rotation;
        public ERotationAnchor AnchorType;
    }
    public class UIDataStoreBinding
    {
        public string MarkupString;
        public EUIDataProviderFieldType RequiredFieldType;
    }
    public class UIStyleSubscriberReference
    {
        public NameReference SubscriberId;
    }
    public class StyleReferenceId
    {
        public NameReference StyleReferenceTag;
        public int StyleProperty;
    }
    public class UITextAttributes
    {
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public bool Shadow;
        public bool Strikethrough;
    }
    public class UIImageAdjustmentData
    {
        public UIScreenValue_Extent ProtectedRegion;
        public EMaterialAdjustmentType AdjustmentType;
        public EUIAlignment Alignment;
    }
    public class UIStringCaretParameters
    {
        public NameReference CaretStyle;
        public float CaretWidth;
        public bool bDisplayCaret;
        public EUIDefaultPenColor CaretType;
    }
    public class RenderParameters
    {
        public TextureCoordinates DrawCoords;
        public LinearColor OverideDrawColor;
        public Vector2D Scaling;
        public Vector2D ImageExtent;
        public Vector2D SpacingAdjust;
        public float DrawX;
        public float DrawY;
        public float DrawXL;
        public float DrawYL;
        public int DrawFont;
        public float ViewportHeight;
        public bool bUseOverrideColor;
        public EUIAlignment TextAlignment;
    }
    public class TextAutoScaleValue
    {
        public float MinScale;
        public ETextAutoScaleMode AutoScaleMode;
    }
    public class UIStyleOverride
    {
        public LinearColor DrawColor;
        public float Padding;
        public float Opacity;
        public bool bOverrideDrawColor;
        public bool bOverrideOpacity;
        public bool bOverridePadding;
    }
    public class UITextStyleOverride : UIStyleOverride
    {
        public TextAutoScaleValue AutoScaling;
        public float DrawScale;
        public float SpacingAdjust;
        public int DrawFont;
        public UITextAttributes TextAttributes;
        public bool bOverrideDrawFont;
        public bool bOverrideAttributes;
        public bool bOverrideAlignment;
        public bool bOverrideClipMode;
        public bool bOverrideClipAlignment;
        public bool bOverrideAutoScale;
        public bool bOverrideScale;
        public bool bOverrideSpacingAdjust;
        public EUIAlignment TextAlignment;
        public ETextClipMode ClipMode;
        public EUIAlignment ClipAlignment;
    }
    public class UIImageStyleOverride : UIStyleOverride
    {
        public UIImageAdjustmentData Formatting;
        public TextureCoordinates Coordinates;
        public bool bOverrideCoordinates;
        public bool bOverrideFormatting;
    }
    public class UICombinedStyleData
    {
        public UIImageAdjustmentData AdjustmentType;
        public LinearColor TextColor;
        public LinearColor ImageColor;
        public TextureCoordinates AtlasCoords;
        public float TextPadding;
        public float ImagePadding;
        public TextAutoScaleValue TextAutoScaling;
        public Vector2D TextScale;
        public Vector2D TextSpacingAdjust;
        public int DrawFont;
        public int FallbackImage;
        public UITextAttributes TextAttributes;
        public bool bInitialized;
        public EUIAlignment TextAlignment;
        public ETextClipMode TextClipMode;
        public EUIAlignment TextClipAlignment;
    }
    public class ModifierData
    {
    }
    public class UIStringNodeModifier
    {
    }
    public class UIStringNode
    {
        public string SourceText;
        public Vector2D Extent;
        public Vector2D Scaling;
        public bool bForceWrap;
    }
    public class UIStringNode_Text : UIStringNode
    {
        public string RenderedText;
        public UICombinedStyleData NodeStyleParameters;
    }
    public class UIStringNode_Image : UIStringNode
    {
        public TextureCoordinates TexCoords;
        public Vector2D ForcedExtent;
        public int RenderedImage;
    }
    public class UIStringNode_NestedMarkupParent : UIStringNode
    {
    }
    public class UIStringNode_FormattedNodeParent : UIStringNode_Text
    {
    }
    public class WrappedStringElement
    {
        public string Value;
        public Vector2D LineExtent;
    }
    public class UIMouseCursor
    {
        public NameReference CursorStyle;
        public int Cursor;
    }
    public class InputEventParameters
    {
    }
    public class SubscribedInputEventParameters : InputEventParameters
    {
    }
    public class UIAxisEmulationDefinition
    {
        public NameReference InputKeyToEmulate;
        public NameReference AxisInputKey;
        public NameReference AdjacentAxisInputKey;
        public bool bEmulateButtonPress;
    }
    public class RawInputKeyEventData
    {
        public NameReference InputKeyName;
        public byte ModifierKeyFlags;
    }
    public class UIInputActionAlias
    {
        public RawInputKeyEventData[] LinkedInputKeys;
        public NameReference InputAliasName;
    }
    public class UIInputAliasValue
    {
        public NameReference InputAliasName;
        public byte ModifierFlagMask;
    }
    public class UIInputAliasMap
    {
    }
    public class UIInputAliasStateMap
    {
        public string StateClassName;
        public UIInputActionAlias[] StateInputAliases;
        public int State;
    }
    public class UIInputAliasClassMap
    {
        public string WidgetClassName;
        public UIInputAliasStateMap[] WidgetStates;
        public int WidgetClass;
    }
    public class AutoCompleteCommand
    {
        public string Command;
        public string Desc;
    }
    public class AutoCompleteNode
    {
        public int[] AutoCompleteListIndices;
        public Pointer[] ChildNodes;
        public int IndexChar;
    }
    public class CoverReference : ActorReference
    {
        public int SlotIdx;
        public int Direction;
    }
    public class LinkSlotHelper
    {
        public int[] Slots;
        public int Link;
    }
    public class CoverInfo
    {
        public int Link;
        public int SlotIdx;
    }
    public class TargetInfo
    {
        public int Target;
        public int SlotIdx;
        public int Direction;
    }
    public class CovPosInfo
    {
        public Vector location;
        public Vector Normal;
        public Vector Tangent;
        public int Link;
        public int LtSlotIdx;
        public int RtSlotIdx;
        public float LtToRtPct;
    }
    public class FireLinkItem
    {
        public ECoverType SrcType;
        public ECoverAction SrcAction;
        public ECoverType DestType;
        public ECoverAction DestAction;
    }
    public class FireLink
    {
        public byte[] Interactions;
        public int PackedProperties_CoverPairRefAndDynamicInfo;
        public bool bFallbackLink;
        public bool bDynamicIndexInited;
    }
    public class DynamicLinkInfo
    {
        public Vector LastTargetLocation;
        public Vector LastSrcLocation;
    }
    public class ExposedLink
    {
        public CoverReference TargetActor;
        public byte ExposedScale;
    }
    public class DangerLink
    {
        public ActorReference DangerNav;
        public int DangerCost;
    }
    public class SlotMoveRef
    {
        public BasedPosition Dest;
        public PolyReference Poly;
        public int Direction;
    }
    public class CoverSlot
    {
        public ECoverAction[] Actions;
        public FireLink[] FireLinks;
        public int[] ExposedCoverPackedProperties;
        public int[] DangerCoverPackedProperties;
        public CoverReference[] SlipTarget;
        public SlotMoveRef[] SlipRefs;
        public CoverReference[] OverlapClaims;
        public CoverReference MantleTarget;
        public Vector LocationOffset;
        public Rotator RotationOffset;
        public int SlotOwner;
        public int TurnTargetPackedProperties;
        public int CoverTurnTargetPackedProperties;
        public int ExtraCost;
        public float LeanTraceDist;
        public int SlotMarker;
        public bool bLeanLeft;
        public bool bLeanRight;
        public bool bForceCanPopUp;
        public bool bCanPopUp;
        public bool bCanMantle;
        public bool bCanClimbUp;
        public bool bForceCanCoverSlip_Left;
        public bool bForceCanCoverSlip_Right;
        public bool bCanCoverSlip_Left;
        public bool bCanCoverSlip_Right;
        public bool bCanSwatTurn_Left;
        public bool bCanSwatTurn_Right;
        public bool bCanCoverTurn_Left;
        public bool bCanCoverTurn_Right;
        public bool bEnabled;
        public bool bAllowPopup;
        public bool bAllowMantle;
        public bool bAllowCoverSlip;
        public bool bAllowClimbUp;
        public bool bAllowSwatTurn;
        public bool bAllowCoverTurn;
        public bool bForceNoGroundAdjust;
        public bool bPlayerOnly;
        public bool bUnsafeCover;
        public bool bFailedToFindSurface;
        public ECoverType ForceCoverType;
        public ECoverType CoverType;
        public ECoverLocationDescription LocationDescription;
    }
    public class StaticMeshComponentLODInfo
    {
        public int[] ShadowMaps;
        public int[] ShadowVertexBuffers;
        public Pointer LightMap;
        public Pointer OverrideVertexColors;
    }
    public class CoverMeshes
    {
        public int Base;
        public int LeanLeft;
        public int LeanRight;
        public int Climb;
        public int Mantle;
        public int SlipLeft;
        public int SlipRight;
        public int SwatLeft;
        public int SwatRight;
        public int PopUp;
        public int PlayerOnly;
    }
    public class ManualCoverTypeInfo
    {
        public byte SlotIndex;
        public ECoverType ManualCoverType;
    }
    public class CoverReplicationInfo
    {
        public byte[] SlotsEnabled;
        public byte[] SlotsDisabled;
        public byte[] SlotsAdjusted;
        public ManualCoverTypeInfo[] SlotsCoverTypeChanged;
        public int Link;
    }
    public class CullDistanceSizePair
    {
        public float Size;
        public float CullDistance;
    }
    public class UIDataProviderField
    {
        public int[] FieldProviders;
        public NameReference FieldTag;
        public EUIDataProviderFieldType FieldType;
    }
    public class GameDataProviderTypes
    {
        public int GameDataProviderClass;
        public int PlayerDataProviderClass;
        public int TeamDataProviderClass;
    }
    public class PresetGeneratedPoint
    {
        public float KeyIn;
        public float KeyOut;
        public float TangentIn;
        public float TangentOut;
        public bool TangentsValid;
        public EInterpCurveMode IntepMode;
    }
    public class PlayerDataStoreGroup
    {
    }
    public class KeyBind
    {
        public string Command;
        public NameReference Name;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool bIgnoreCtrl;
        public bool bIgnoreShift;
        public bool bIgnoreAlt;
    }
    public class DecalReceiver
    {
        public Pointer RenderData;
        public int Component;
    }
    public class ActiveDecalInfo
    {
        public int Decal;
        public float LifetimeRemaining;
    }
    public class MaterialInput
    {
        public int Expression;
        public int Mask;
        public int MaskR;
        public int MaskG;
        public int MaskB;
        public int MaskA;
        public int GCC64_Padding;
    }
    public class ColorMaterialInput : MaterialInput
    {
        public bool UseConstant;
        public Color Constant;
    }
    public class ScalarMaterialInput : MaterialInput
    {
        public bool UseConstant;
        public float Constant;
    }
    public class VectorMaterialInput : MaterialInput
    {
        public bool UseConstant;
        public Vector Constant;
    }
    public class Vector2MaterialInput : MaterialInput
    {
        public bool UseConstant;
        public float ConstantX;
        public float ConstantY;
    }
    public class LocalizedSubtitle
    {
        public SubtitleCue[] Subtitles;
        public bool bMature;
        public bool bManualWordWrap;
    }
    public class DominantShadowInfo
    {
        public Matrix WorldToLight;
        public Matrix LightToWorld;
        public Box LightSpaceImportanceBounds;
        public int ShadowMapSizeX;
        public int ShadowMapSizeY;
    }
    public class LightmassLightSettings
    {
        public float IndirectLightingScale;
        public float IndirectLightingSaturation;
        public float ShadowExponent;
    }
    public class LightmassPointLightSettings : LightmassLightSettings
    {
        public float LightSourceRadius;
    }
    public class LightmassDirectionalLightSettings : LightmassLightSettings
    {
        public float LightSourceAngle;
    }
    public class ParticleSystemLOD
    {
        public bool bLit;
    }
    public class DebugParticleParameterFloat
    {
        public NameReference nmValue;
        public float fValue;
    }
    public class DebugParticleParameterVector
    {
        public Vector vValue;
        public NameReference nmValue;
    }
    public class LODSoloTrack
    {
    }
    public class ParticleSysParam
    {
        public RwVector3 Vector;
        public NameReference Name;
        public float Scalar;
        public Color Color;
        public int Actor;
        public int Material;
        public EParticleSysParamType ParamType;
    }
    public class ParticleEventData
    {
        public RwVector3 location;
        public RwVector3 Direction;
        public RwVector3 Velocity;
        public NameReference EventName;
        public int Type;
        public float EmitterTime;
    }
    public class ParticleEmitterInstance
    {
    }
    public class ParticleEmitterInstanceMotionBlurInfo
    {
    }
    public class ViewParticleEmitterInstanceMotionBlurInfo
    {
    }
    public class ParticleEventSpawnData : ParticleEventData
    {
    }
    public class ParticleEventDeathData : ParticleEventData
    {
        public float ParticleTime;
    }
    public class ParticleEventCollideData : ParticleEventData
    {
        public RwVector3 Normal;
        public NameReference BoneName;
        public float ParticleTime;
        public float Time;
        public int Item;
    }
    public class ParticleEventKismetData : ParticleEventData
    {
        public RwVector3 Normal;
        public bool UsePSysCompLocation;
    }
    public class EmitterBaseInfo
    {
        public Vector RelativeLocation;
        public Rotator RelativeRotation;
        public int PSC;
        public int Base;
        public bool bInheritBaseScale;
    }
    public class StatColorMapEntry
    {
        public float In;
        public Color Out;
    }
    public class StatColorMapping
    {
        public string StatName;
        public StatColorMapEntry[] ColorMap;
        public bool DisableBlend;
    }
    public class BioLayerDetails
    {
        public string Prefix;
        public string Description;
        public Color Color;
    }
    public class DropNoteInfo
    {
        public string Comment;
        public Vector location;
        public Rotator Rotation;
    }
    public class LightmassPrimitiveSettings
    {
        public bool bUseTwoSidedLighting;
        public bool bShadowIndirectOnly;
        public bool bUseEmissiveForStaticLighting;
        public float EmissiveLightFalloffExponent;
        public float EmissiveLightExplicitInfluenceRadius;
        public float EmissiveBoost;
        public float DiffuseBoost;
        public float SpecularBoost;
        public float FullyOccludedSamplesFraction;
    }
    public class LightmassDebugOptions
    {
        public float CoplanarTolerance;
        public float ExecutionTimeDivisor;
        public bool bDebugMode;
        public bool bStatsEnabled;
        public bool bGatherBSPSurfacesAcrossComponents;
        public bool bUseDeterministicLighting;
        public bool bUseImmediateImport;
        public bool bImmediateProcessMappings;
        public bool bSortMappings;
        public bool bDumpBinaryFiles;
        public bool bDebugMaterials;
        public bool bPadMappings;
        public bool bDebugPaddings;
        public bool bOnlyCalcDebugTexelMappings;
        public bool bUseRandomColors;
        public bool bColorBordersGreen;
        public bool bColorByExecutionTime;
        public bool bInitialized;
    }
    public class SwarmDebugOptions
    {
        public bool bDistributionEnabled;
        public bool bForceContentExport;
        public bool bInitialized;
    }
    public class RootMotionCurve
    {
        public InterpCurveVector Curve;
        public NameReference AnimName;
        public float MaxCurveTime;
    }
    public class MeshMaterialRef
    {
        public int MeshComp;
        public int MaterialIndex;
    }
    public class LightMapRef
    {
        public Pointer Reference;
    }
    public class FoliageInstanceBase
    {
        public Vector location;
        public Vector XAxis;
        public Vector YAxis;
        public Vector ZAxis;
        public float DistanceFactorSquared;
    }
    public class StoredFoliageInstance : FoliageInstanceBase
    {
        public Color StaticLighting;
    }
    public class FoliageMesh
    {
        public LightmassPrimitiveSettings LightmassSettings;
        public Vector MinScale;
        public Vector MaxScale;
        public int InstanceStaticMesh;
        public int Material;
        public float MaxDrawRadius;
        public float MinTransitionRadius;
        public float MinThinningRadius;
        public float MinUniformScale;
        public float MaxUniformScale;
        public float SwayScale;
        public int Seed;
        public float SurfaceAreaPerInstance;
        public int Component;
        public bool bCreateInstancesOnBSP;
        public bool bCreateInstancesOnStaticMeshes;
        public bool bCreateInstancesOnTerrain;
    }
    public class WaveformSample
    {
        public float Duration;
        public byte LeftAmplitude;
        public byte RightAmplitude;
        public EWaveformFunction LeftFunction;
        public EWaveformFunction RightFunction;
    }
    public class FragmentGroup
    {
        public int[] FragmentIndices;
        public bool bGroupIsRooted;
    }
    public class DeferredPartToSpawn
    {
        public Vector InitialVel;
        public Vector InitialAngVel;
        public int ChunkIndex;
        public float RelativeScale;
        public bool bExplosion;
    }
    public class URL
    {
        public string Protocol;
        public string Host;
        public string Map;
        public string[] Op;
        public string Portal;
        public int Port;
        public int Valid;
    }
    public class LevelStreamingStatus
    {
        public NameReference PackageName;
        public bool bShouldBeLoaded;
        public bool bShouldBeVisible;
    }
    public class FullyLoadedPackagesInfo
    {
        public string Tag;
        public NameReference[] PackagesToLoad;
        public int[] LoadedObjects;
        public EFullyLoadPackageType FullyLoadType;
    }
    public class NamedNetDriver
    {
        public Pointer NetDriver;
        public NameReference NetDriverName;
    }
    public class GameClassShortName
    {
        public string ShortName;
        public string GameClassName;
    }
    public class GameTypePrefix
    {
        public string Prefix;
        public string GameType;
        public string[] AdditionalGameTypes;
        public string[] ForcedObjects;
        public string OverrideCommonPackage;
        public bool bUsesCommonPackage;
        public bool bIsMultiplayer;
    }
    public class LocalizedStringSetting
    {
        public int Id;
        public int ValueIndex;
        public EOnlineDataAdvertisementType AdvertisementType;
    }
    public class SettingsData
    {
        public int Value1;
        public ESettingsDataType Type;
    }
    public class SettingsProperty
    {
        public SettingsData Data;
        public int PropertyId;
        public EOnlineDataAdvertisementType AdvertisementType;
    }
    public class StringIdToStringMapping
    {
        public NameReference Name;
        public int Id;
        public bool bIsWildcard;
    }
    public class LocalizedStringSettingMetaData
    {
        public string ColumnHeaderText;
        public StringIdToStringMapping[] ValueMappings;
        public NameReference Name;
        public int Id;
    }
    public class SettingsPropertyPropertyMetaData
    {
        public string ColumnHeaderText;
        public IdToStringMapping[] ValueMappings;
        public SettingsData[] PredefinedValues;
        public NameReference Name;
        public int Id;
        public float MinVal;
        public float MaxVal;
        public float RangeIncrement;
        public EPropertyValueMappingType MappingType;
    }
    public class IdToStringMapping
    {
        public NameReference Name;
        public int Id;
    }
    public class GameplayEventsHeader
    {
        public int EngineVersion;
        public int StatsWriterVersion;
        public int StreamOffset;
        public int FooterOffset;
        public int TotalStreamSize;
        public int FileSize;
    }
    public class GameSessionInformation
    {
        public string Language;
        public string GameplaySessionTimestamp;
        public string GameplaySessionID;
        public string GameClassName;
        public string MapName;
        public string MapURL;
        public int AppTitleID;
        public int PlatformType;
        public float GameplaySessionStartTime;
        public float GameplaySessionEndTime;
        public bool bGameplaySessionInProgress;
    }
    public class TeamInformation
    {
        public string TeamName;
        public int TeamIndex;
        public Color TeamColor;
        public int MaxSize;
    }
    public class PlayerInformationNew
    {
        public string ControllerName;
        public string PlayerName;
        public bool bIsBot;
    }
    public class GameplayEventMetaData
    {
        public NameReference EventName;
        public int EventId;
        public int MaxValue;
        public EPropertyValueMappingType MappingType;
    }
    public class WeaponClassEventData
    {
        public string WeaponClassName;
    }
    public class DamageClassEventData
    {
        public string DamageClassName;
    }
    public class ProjectileClassEventData
    {
        public string ProjectileClassName;
    }
    public class PawnClassEventData
    {
        public string PawnClassName;
    }
    public class TitleSafeZoneArea
    {
        public float MaxPercentX;
        public float MaxPercentY;
        public float RecommendedPercentX;
        public float RecommendedPercentY;
    }
    public class PerPlayerSplitscreenData
    {
        public float SizeX;
        public float SizeY;
        public float OriginX;
        public float OriginY;
    }
    public class SplitscreenData
    {
        public PerPlayerSplitscreenData[] PlayerData;
    }
    public class DebugDisplayProperty
    {
        public NameReference PropertyName;
        public int Obj;
        public bool bSpecialProperty;
    }
    public class InstancedStaticMeshInstanceData
    {
        public Matrix Transform;
        public Vector2D LightmapUVBias;
        public Vector2D ShadowmapUVBias;
    }
    public class InstancedStaticMeshMappingInfo
    {
        public Pointer Mapping;
        public Pointer LightMap;
        public int LightmapTexture;
        public int ShadowmapTexture;
    }
    public class CurveEdEntry
    {
        public string CurveName;
        public int CurveObject;
        public Color CurveColor;
        public int bHideCurve;
        public int bColorCurve;
        public int bFloatingPointColorCurve;
        public int bClamp;
        public float ClampLow;
        public float ClampHigh;
    }
    public class CurveEdTab
    {
        public string TabName;
        public CurveEdEntry[] Curves;
        public float ViewStartInput;
        public float ViewEndInput;
        public float ViewStartOutput;
        public float ViewEndOutput;
    }
    public class BioBinkAsyncPreloader
    {
        public Pointer Callback;
        public Pointer Context;
    }
    public class BioResourcePreloadItem
    {
        public int pObject;
        public int nKeyIndex;
        public float fTime;
    }
    public class InterpEdSelKey
    {
        public int Group;
        public int TrackIndex;
        public int KeyIndex;
        public float UnsnappedPosition;
    }
    public class AnimControlTrackKey
    {
        public NameReference AnimSeqName;
        public float StartTime;
        public float AnimStartOffset;
        public float AnimEndOffset;
        public float AnimPlayRate;
        public bool bLooping;
        public bool bReverse;
    }
    public class DirectorTrackCut
    {
        public NameReference TargetCamGroup;
        public float Time;
        public float TransitionTime;
        public bool bSkipCameraReset;
    }
    public class EventTrackKey
    {
        public NameReference EventName;
        public float Time;
    }
    public class FaceFXTrackKey
    {
        public string FaceFXGroupName;
        public string FaceFXSeqName;
        public float StartTime;
    }
    public class FaceFXSoundCueKey
    {
        public int FaceFXSoundCue;
    }
    public class Override_Asset
    {
        public int fxAsset;
        public EBioAutoSetFXAnimGroupTrack eAnimGroup;
        public EBioAutoSetFXAnimSeqTrack eAnimSeq;
    }
    public class Override_AnimSet
    {
        public int[] aBioMaleSets;
        public int[] aBioFemaleSets;
        public int fxaAnimSet;
        public EBioAutoSetFXAnimTrack eAnimSequence;
    }
    public class ToggleTrackKey
    {
        public float Time;
        public ETrackToggleAction ToggleAction;
    }
    public class VisibilityTrackKey
    {
        public float Time;
        public EVisibilityTrackAction Action;
        public EVisibilityTrackCondition ActiveCondition;
    }
    public class InterpLookupPoint
    {
        public NameReference GroupName;
        public float Time;
    }
    public class InterpLookupTrack
    {
        public InterpLookupPoint[] Points;
    }
    public class ParticleReplayTrackKey
    {
        public float Time;
        public float Duration;
        public int ClipIDNumber;
    }
    public class SoundTrackKey
    {
        public float Time;
        public float Volume;
        public float Pitch;
        public int Sound;
    }
    public class LensFlareElementCurvePair
    {
        public string CurveName;
        public int CurveObject;
    }
    public class LensFlareElement
    {
        public RawDistributionFloat LFMaterialIndex;
        public RawDistributionFloat Scaling;
        public RawDistributionVector AxisScaling;
        public RawDistributionFloat Rotation;
        public RawDistributionVector Color;
        public RawDistributionFloat Alpha;
        public RawDistributionVector Offset;
        public RawDistributionVector DistMap_Scale;
        public RawDistributionVector DistMap_Color;
        public RawDistributionFloat DistMap_Alpha;
        public int[] LFMaterials;
        public Vector Size;
        public NameReference ElementName;
        public float RayDistance;
        public bool bIsEnabled;
        public bool bUseSourceDistance;
        public bool bNormalizeRadialDistance;
        public bool bModulateColorBySource;
    }
    public class LensFlareElementInstance
    {
    }
    public class RequestedPostProcessEffect
    {
        public Pointer pOwner;
        public int pEffect;
        public EAddPostProcessEffectCombineType CombineType;
    }
    public class SynchronizedActorVisibilityHistory
    {
        public Pointer State;
        public Pointer CriticalSection;
    }
    public class CurrentPostProcessVolumeInfo
    {
        public PostProcessSettings LastSettings;
        public int LastVolumeUsed;
        public float BlendStartTime;
        public float LastBlendTime;
    }
    public class CustomInput
    {
        public string InputName;
        public ExpressionInput Input;
    }
    public class ParameterValueOverTime
    {
        public Guid ExpressionGUID;
        public NameReference ParameterName;
        public float CycleTime;
        public float OffsetTime;
        public bool bLoop;
        public bool bAutoActivate;
        public bool bNormalizeTime;
        public bool bOffsetFromEnd;
    }
    public class FontParameterValueOverTime : ParameterValueOverTime
    {
        public int FontValue;
        public int FontPage;
    }
    public class ScalarParameterValueOverTime : ParameterValueOverTime
    {
        public InterpCurveFloat ParameterValueCurve;
        public float ParameterValue;
    }
    public class TextureParameterValueOverTime : ParameterValueOverTime
    {
        public int ParameterValue;
    }
    public class VectorParameterValueOverTime : ParameterValueOverTime
    {
        public InterpCurveVector ParameterValueCurve;
        public LinearColor ParameterValue;
    }
    public class MorphNodeConn
    {
        public int[] ChildNodes;
        public NameReference ConnName;
        public int DrawY;
    }
    public class BoneAngleMorph
    {
        public float Angle;
        public float TargetWeight;
    }
    public class PolySegmentSpan
    {
        public Pointer Poly;
        public Vector P1;
        public Vector P2;
    }
    public class EdgePointer
    {
        public Pointer Dummy;
    }
    public class PathStore
    {
        public EdgePointer[] EdgeList;
    }
    public class NavMeshPathParams
    {
        public Pointer Interface;
        public Vector SearchExtent;
        public Vector SearchStart;
        public float MaxDropHeight;
        public float MinWalkableZ;
        public float MaxHoverDistance;
        public bool bCanMantle;
        public bool bNeedsMantleValidityTest;
        public bool bAbleToSearch;
    }
    public class BiasedGoalActor
    {
        public int Goal;
        public int ExtraCost;
    }
    public class CommunityContentMetadata
    {
        public SettingsProperty[] MetadataItems;
        public int ContentType;
    }
    public class LocalTalker
    {
        public bool bHasVoice;
        public bool bHasNetworkedVoice;
        public bool bIsRecognizingSpeech;
        public bool bWasTalking;
        public bool bIsTalking;
        public bool bIsRegistered;
    }
    public class RemoteTalker
    {
        public UniqueNetId TalkerId;
        public bool bWasTalking;
        public bool bIsTalking;
        public bool bIsRegistered;
    }
    public class OnlineFriendMessage
    {
        public UniqueNetId SendingPlayerId;
        public string SendingPlayerNick;
        public string Message;
        public bool bIsFriendInvite;
        public bool bIsGameInvite;
        public bool bWasAccepted;
        public bool bWasDenied;
    }
    public class NamedInterface
    {
        public NameReference InterfaceName;
        public int InterfaceObject;
    }
    public class NamedInterfaceDef
    {
        public string InterfaceClassName;
        public NameReference InterfaceName;
    }
    public class TitleFile
    {
        public string Filename;
        public byte[] Data;
        public EOnlineEnumerationReadState AsyncState;
    }
    public class CommunityContentFile
    {
        public UniqueNetId Owner;
        public string LocalFilePath;
        public int ContentId;
        public int FileId;
        public int ContentType;
        public int FileSize;
        public int DownloadCount;
        public float AverageRating;
        public int RatingCount;
        public int LastRatingGiven;
    }
    public class PlayerInformation
    {
        public UniqueNetId UniqueId;
        public string ControllerName;
        public string PlayerName;
        public int LastPlayerEventIdx;
        public bool bIsBot;
    }
    public class GameplayEvent
    {
        public int PlayerEventAndTarget;
        public int EventNameAndDesc;
    }
    public class PlayerEvent
    {
        public Vector EventLocation;
        public float EventTime;
        public int PlayerIndexAndYaw;
        public int PlayerPitchAndRoll;
    }
    public class OverrideSkill
    {
        public UniqueNetId[] Players;
        public Double[] Mus;
        public Double[] Sigmas;
        public int LeaderboardId;
    }
    public class NamedObjectProperty
    {
        public string ObjectPropertyValue;
        public NameReference ObjectPropertyName;
    }
    public class OnlineGameSearchSortClause
    {
        public NameReference ObjectPropertyName;
        public int EntryId;
        public EOnlineGameSearchEntryType EntryType;
        public EOnlineGameSearchSortType SortType;
    }
    public class OnlineGameSearchORClause
    {
        public OnlineGameSearchParameter[] OrParams;
    }
    public class OnlineGameSearchQuery
    {
        public OnlineGameSearchORClause[] OrClauses;
        public OnlineGameSearchSortClause[] SortClauses;
    }
    public class NamedSession
    {
        public OnlineRegistrant[] Registrants;
        public OnlineArbitrationRegistrant[] ArbitrationRegistrants;
        public NameReference SessionName;
        public int GameSettings;
    }
    public class AchievementDetails
    {
        public string AchievementName;
        public string Description;
        public string HowTo;
        public int Id;
        public int Image;
        public int GamerPoints;
        public bool bIsSecret;
        public bool bWasAchievedOnline;
        public bool bWasAchievedOffline;
    }
    public class OnlinePartyMember
    {
        public UniqueNetId UniqueId;
        public QWord Data1;
        public QWord Data2;
        public string NickName;
        public int TitleId;
        public bool bIsLocal;
        public bool bIsInPartyVoice;
        public bool bIsTalking;
        public bool bIsInGameSession;
        public byte LocalUserNum;
        public ENATType NatType;
    }
    public class OnlineProfileSetting
    {
        public SettingsProperty ProfileSetting;
        public EOnlineProfilePropertyOwner Owner;
    }
    public class ConfiguredGameSetting
    {
        public string GameSettingsClassName;
        public string URL;
        public int GameSettingId;
    }
    public class Playlist
    {
        public ConfiguredGameSetting[] ConfiguredGames;
        public string LocalizationString;
        public int[] ContentIds;
        public string Name;
        public int PlaylistId;
        public int TeamSize;
        public int TeamCount;
        public bool bIsArbitrated;
        public bool bDisableDedicatedServerSearches;
    }
    public class RecentParty
    {
        public UniqueNetId PartyLeader;
        public UniqueNetId[] PartyMembers;
    }
    public class CurrentPlayerMet
    {
        public UniqueNetId NetId;
        public int TeamNum;
        public int Skill;
    }
    public class OnlineStatsColumn
    {
        public SettingsData StatValue;
        public int ColumnNo;
    }
    public class OnlineStatsRow
    {
        public UniqueNetId PlayerID;
        public SettingsData Rank;
        public string NickName;
        public OnlineStatsColumn[] Columns;
    }
    public class ColumnMetaData
    {
        public string ColumnName;
        public NameReference Name;
        public int Id;
    }
    public class ParticleBurst
    {
        public int Count;
        public int CountLow;
        public float Time;
    }
    public class AutoGenLODParam
    {
        public int nLabel;
        public float fPercentage;
        public float fDistance;
    }
    public class ParticleCurvePair
    {
        public string CurveName;
        public int CurveObject;
    }
    public class BeamModifierOptions
    {
        public bool bModify;
        public bool bScale;
        public bool bLock;
    }
    public class ParticleEvent_GenerateInfo
    {
        public int[] ParticleModuleEventsToSendToGame;
        public NameReference CustomName;
        public int Frequency;
        public int LowFreq;
        public int ParticleFrequency;
        public bool FirstTimeOnly;
        public bool LastTimeOnly;
        public bool UseReflectedImpactVector;
        public EParticleEventType Type;
    }
    public class OrbitOptions
    {
        public bool bProcessDuringSpawn;
        public bool bProcessDuringUpdate;
        public bool bUseEmitterTime;
    }
    public class EmitterDynamicParameter
    {
        public RawDistributionFloat ParamValue;
        public NameReference ParamName;
        public bool bUseEmitterTime;
        public bool bSpawnTimeOnly;
        public bool bScaleVelocityByParamValue;
        public EEmitterDynamicParameterValue ValueMethod;
    }
    public class BeamTargetData
    {
        public NameReference TargetName;
        public float TargetPercentage;
    }
    public class PhysXEmitterVerticalLodProperties
    {
        public float WeightForFifo;
        public float WeightForSpawnLod;
        public float SpawnLodRateVsLifeBias;
        public float RelativeFadeoutTime;
    }
    public class ParticleEmitterReplayFrame
    {
        public Pointer FrameState;
        public int EmitterType;
        public int OriginalEmitterIndex;
    }
    public class ParticleSystemReplayFrame
    {
        public ParticleEmitterReplayFrame[] Emitters;
    }
    public class PBRuleLink
    {
        public NameReference LinkName;
        public int NextRule;
    }
    public class PBVariationInfo
    {
        public NameReference VariationName;
        public bool bMeshOnTopOfFacePoly;
    }
    public class PBMeshCompInfo
    {
        public int MeshComp;
        public int TopLevelScopeIndex;
    }
    public class PBFracMeshCompInfo
    {
        public int FracMeshComp;
        public int TopLevelScopeIndex;
    }
    public class PBMaterialParam
    {
        public LinearColor Color;
        public NameReference ParamName;
    }
    public class PBMemUsageInfo
    {
        public int Building;
        public int Ruleset;
        public int NumStaticMeshComponent;
        public int NumInstancedStaticMeshComponents;
        public int NumInstancedTris;
        public int LightmapMemBytes;
        public int ShadowmapMemBytes;
        public int LODDiffuseMemBytes;
        public int LODLightingMemBytes;
    }
    public class PBEdgeInfo
    {
        public Vector EdgeEnd;
        public Vector EdgeStart;
        public int ScopeAIndex;
        public int ScopeBIndex;
        public float EdgeAngle;
        public EScopeEdge ScopeAEdge;
        public EScopeEdge ScopeBEdge;
    }
    public class RBCornerAngleInfo
    {
        public float Angle;
        public float CornerSize;
    }
    public class RBEdgeAngleInfo
    {
        public float Angle;
    }
    public class BuildingMatOverrides
    {
        public int[] MaterialOptions;
    }
    public class BuildingMeshInfo
    {
        public int[] MaterialOverrides;
        public BuildingMatOverrides[] SectionOverrides;
        public int Mesh;
        public float DimX;
        public float DimZ;
        public float Chance;
        public int Translation;
        public int Rotation;
        public int OverriddenMeshLightMapRes;
        public bool bMeshScaleTranslation;
        public bool bOverrideMeshLightMapRes;
    }
    public class RBSplitInfo
    {
        public NameReference SplitName;
        public float FixedSize;
        public float ExpandRatio;
        public bool bFixSize;
    }
    public class PhysXDestructibleDepthParameters
    {
        public bool bTakeImpactDamage;
        public bool bPlaySoundEffect;
        public bool bPlayParticleEffect;
        public bool bDoNotTimeOut;
        public bool bNoKillDummy;
    }
    public class PhysXDestructibleParameters
    {
        public PhysXDestructibleDepthParameters[] DepthParameters;
        public float DamageThreshold;
        public float DamageToRadius;
        public float DamageCap;
        public float ForceToDamage;
        public int FractureSound;
        public int CrumbleParticleSystem;
        public float CrumbleParticleSize;
        public float ScaledDamageToRadius;
        public bool bAccumulateDamage;
    }
    public class SpawnBasis
    {
        public Vector location;
        public Rotator Rotation;
        public float Scale;
    }
    public class PhysXDestructibleAssetChunk
    {
        public NameReference BoneName;
        public int Index;
        public int FragmentIndex;
        public float Volume;
        public float Size;
        public int Depth;
        public int ParentIndex;
        public int FirstChildIndex;
        public int NumChildren;
        public int MeshIndex;
        public int BoneIndex;
        public int BodyIndex;
    }
    public class PhysXDestructibleChunk
    {
        public Matrix RelativeMatrix;
        public Matrix WorldMatrix;
        public Pointer Structure;
        public Vector RelativeCentroid;
        public Vector WorldCentroid;
        public NameReference BoneName;
        public int ActorIndex;
        public int FragmentIndex;
        public int Index;
        public int MeshIndex;
        public int BoneIndex;
        public int BodyIndex;
        public float Radius;
        public int ParentIndex;
        public int FirstChildIndex;
        public int NumChildren;
        public int Depth;
        public float Age;
        public float Damage;
        public float Size;
        public int FIFOIndex;
        public int FirstOverlapIndex;
        public int NumOverlaps;
        public int ShortestRoute;
        public int NumSupporters;
        public int NumChildrenDup;
        public bool WorldCentroidValid;
        public bool WorldMatrixValid;
        public bool bCrumble;
        public bool IsEnvironmentSupported;
        public bool IsRouting;
        public bool IsRouteValid;
        public bool IsRouteBlocker;
        public EPhysXDestructibleChunkState CurrentState;
    }
    public class PhysXDestructibleOverlap
    {
        public int ChunkIndex0;
        public int ChunkIndex1;
        public int Adjacent;
    }
    public class IndexedRBState
    {
        public Vector CenterOfMass;
        public Vector LinearVelocity;
        public Vector AngularVelocity;
        public int Index;
    }
    public class RBVolumeFill
    {
        public IndexedRBState[] RBStates;
        public Vector[] Positions;
    }
    public class PlayerDataProviderTypes
    {
        public int PlayerOwnerDataProviderClass;
        public int CurrentWeaponDataProviderClass;
        public int WeaponDataProviderClass;
        public int PowerupDataProviderClass;
    }
    public class AutomatedTestingDatum
    {
        public int NumberOfMatchesPlayed;
        public int NumMapListCyclesDone;
    }
    public class RemovedArchetype
    {
        public int Archetype;
        public int RemovedVersion;
    }
    public class ActivateOp
    {
        public int ActivatorOp;
        public int Op;
        public int InputIdx;
        public float RemainingDelay;
    }
    public class QueuedActivationInfo
    {
        public int[] ActivateIndices;
        public int ActivatedEvent;
        public int inOriginator;
        public int inInstigator;
        public bool bPushTop;
    }
    public class KCachedConvexDataElement
    {
        public byte[] ConvexElementData;
    }
    public class KCachedConvexData
    {
        public KCachedConvexDataElement[] CachedConvexElements;
    }
    public class LinearDOFSetup
    {
        public float LimitSize;
        public byte bLimited;
    }
    public class EMG_Entry
    {
        public NameReference m_nmEffect;
        public int m_pMaterial;
    }
    public class RvrMultiplexorEntry
    {
        public NameReference m_nmTag;
        public int m_pMaterial;
    }
    public class PathSizeInfo
    {
        public NameReference Desc;
        public float Radius;
        public float Height;
        public float CrouchHeight;
        public byte PathColor;
    }
    public class SavedTransform
    {
        public Vector location;
        public Rotator Rotation;
    }
    public class CameraCutInfo
    {
        public Vector location;
        public float TimeStamp;
        public int SFXOwningScene;
    }
    public class LevelStreamingNameCombo
    {
        public NameReference LevelName;
        public int Level;
    }
    public class SwitchRange
    {
        public int Min;
        public int Max;
    }
    public class SwitchClassInfo
    {
        public NameReference className;
        public byte bFallThru;
    }
    public class SwitchObjectCase
    {
        public int ObjectValue;
        public bool bFallThru;
        public bool bDefaultValue;
    }
    public class BioScrubbingCamData
    {
        public Vector vCamPos;
        public Rotator rCamRot;
        public NameReference nmStageCam;
        public float fFov;
        public float fNearPlane;
        public float fAspectRatio;
    }
    public class SFXScenePlayData
    {
        public float fScenePosition;
        public bool bSceneNeedsStartup;
        public bool bSceneNeedsSmallTick;
        public bool bSceneFinished;
        public bool bSceneActive;
    }
    public class SFXSSEventHelper
    {
        public NameReference nmEventName;
        public int pNode;
        public float fEventTime;
    }
    public class SFXSSNodePinLink
    {
        public int pLinkedNode;
        public int nLinkedIndex;
    }
    public class SFXSSNodePin
    {
        public string sLinkName;
        public SFXSSNodePinLink[] aLinks;
    }
    public class SoftBodySpecialBoneInfo
    {
        public NameReference BoneName;
        public SoftBodyBoneType BoneType;
        public int[] AttachedVertexIndices;
    }
    public class SkelMeshActorControlTarget
    {
        public NameReference ControlName;
        public int TargetActor;
    }
    public class SkelMaterialSetterDatum
    {
        public int MaterialIndex;
        public int TheMaterial;
    }
    public class SoundClassEditorData
    {
        public int NodePosX;
        public int NodePosY;
    }
    public class SoundClassProperties
    {
        public float Volume;
        public float Pitch;
        public float StereoBleed;
        public float LFEBleed;
        public float VoiceCenterChannelVolume;
        public float VoiceRadioVolume;
        public bool bApplyEffects;
        public bool bAlwaysPlay;
        public bool bIsUISound;
        public bool bIsMusic;
        public bool bReverb;
    }
    public class SoundNodeEditorData
    {
        public int NodePosX;
        public int NodePosY;
    }
    public class AudioEQEffect
    {
        public float HFFrequency;
        public float HFGain;
        public float MFCutoffFrequency;
        public float MFBandwidth;
        public float MFGain;
        public float LFFrequency;
        public float LFGain;
    }
    public class SoundClassAdjuster
    {
        public NameReference SoundClass;
        public float VolumeAdjuster;
        public float PitchAdjuster;
        public bool bApplyToChildren;
    }
    public class DistanceDatum
    {
        public float FadeInDistanceStart;
        public float FadeInDistanceEnd;
        public float FadeOutDistanceStart;
        public float FadeOutDistanceEnd;
        public float Volume;
    }
    public class RecognisableWord
    {
        public string ReferenceWord;
        public string PhoneticWord;
        public int Id;
    }
    public class RecogVocabulary
    {
        public RecognisableWord[] WhoDictionary;
        public RecognisableWord[] WhatDictionary;
        public RecognisableWord[] WhereDictionary;
        public string VocabName;
        public byte[] VocabData;
        public byte[] WorkingVocabData;
    }
    public class RecogUserData
    {
        public byte[] UserData;
        public int ActiveVocabularies;
    }
    public class SpeedTreeStaticLight
    {
        public Guid Guid;
        public int BranchShadowMap;
        public int FrondShadowMap;
        public int LeafMeshShadowMap;
        public int LeafCardShadowMap;
        public int BillboardShadowMap;
    }
    public class SplineConnection
    {
        public int SplineComponent;
        public int ConnectTo;
    }
    public class SplineMeshParams
    {
        public Vector StartPos;
        public Vector StartTangent;
        public Vector EndPos;
        public Vector EndTangent;
        public Vector2D StartScale;
        public Vector2D StartOffset;
        public Vector2D EndScale;
        public Vector2D EndOffset;
        public float StartRoll;
        public float EndRoll;
    }
    public class SMMaterialSetterDatum
    {
        public int MaterialIndex;
        public int TheMaterial;
    }
    public class VehicleState
    {
        public RigidBodyState RBState;
        public int ServerView;
        public bool bServerHandbrake;
        public byte ServerBrake;
        public byte ServerGas;
        public byte ServerSteering;
        public byte ServerRise;
    }
    public class TerrainHeight
    {
    }
    public class TerrainInfoData
    {
    }
    public class TerrainWeightedMaterial
    {
    }
    public class TerrainLayer
    {
        public string Name;
        public int Setup;
        public int AlphaMapIndex;
        public bool Highlighted;
        public bool WireframeHighlighted;
        public bool Hidden;
        public Color HighlightColor;
        public Color WireframeColor;
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
    }
    public class AlphaMap
    {
    }
    public class TerrainDecorationInstance
    {
        public int Component;
        public float X;
        public float Y;
        public float Scale;
        public int Yaw;
    }
    public class TerrainDecoration
    {
        public int Factory;
        public float MinScale;
        public float MaxScale;
        public float Density;
        public float SlopeRotationBlend;
        public int RandSeed;
        public bool bRandomlyRotateYaw;
        public TerrainDecorationInstance[] Instances;
    }
    public class TerrainDecoLayer
    {
        public string Name;
        public TerrainDecoration[] Decorations;
        public int AlphaMapIndex;
    }
    public class TerrainMaterialResource
    {
    }
    public class CachedTerrainMaterialArray
    {
        public Pointer[] CachedMaterials;
    }
    public class SelectedTerrainVertex
    {
        public int X;
        public int Y;
        public int Weight;
    }
    public class TerrainkDOPTree
    {
        public int[] Nodes;
        public int[] Triangles;
    }
    public class TerrainBVTree
    {
        public int[] Nodes;
    }
    public class FilterLimit
    {
        public bool Enabled;
        public float Base;
        public float NoiseScale;
        public float NoiseAmount;
    }
    public class TerrainFilteredMaterial
    {
        public bool UseNoise;
        public float NoiseScale;
        public float NoisePercent;
        public FilterLimit MinHeight;
        public FilterLimit MaxHeight;
        public FilterLimit MinSlope;
        public FilterLimit MaxSlope;
        public float Alpha;
        public int Material;
    }
    public class TerrainFoliageMesh
    {
        public int StaticMesh;
        public int Material;
        public int Density;
        public float MaxDrawRadius;
        public float MinTransitionRadius;
        public float MinScale;
        public float MaxScale;
        public float MinUniformScale;
        public float MaxUniformScale;
        public float MinThinningRadius;
        public int Seed;
        public float SwayScale;
        public float AlphaMapThreshold;
        public float SlopeRotationBlend;
    }
    public class SourceTexture2DRegion
    {
        public int OffsetX;
        public int OffsetY;
        public int SizeX;
        public int SizeY;
        public int Texture2D;
    }
    public class LevelStreamingData
    {
        public int Level;
        public bool bShouldBeLoaded;
        public bool bShouldBeVisible;
        public bool bShouldBlockOnLoad;
    }
    public class UIAnimationNotify
    {
        public NameReference NotifyName;
        public EUIAnimNotifyType NotifyType;
    }
    public class UIAnimationRawData
    {
        public LinearColor DestAsColor;
        public Rotator DestAsRotator;
        public Vector DestAsVector;
        public UIAnimationNotify DestAsNotify;
        public float DestAsFloat;
    }
    public class UIAnimationKeyFrame
    {
        public UIAnimationRawData Data;
        public float RemainingTime;
        public float InterpExponent;
        public EUIAnimationInterpMode InterpMode;
    }
    public class UIAnimTrack
    {
        public UIAnimationKeyFrame[] KeyFrames;
        public EUIAnimType TrackType;
    }
    public class UIAnimSequence
    {
        public UIAnimTrack[] AnimationTracks;
        public int SequenceRef;
        public float PlaybackRate;
        public EUIAnimationLoopMode LoopMode;
    }
    public class UIListSortingParameters
    {
        public int PrimaryIndex;
        public int SecondaryIndex;
        public bool bReversePrimarySorting;
        public bool bReverseSecondarySorting;
        public bool bCaseSensitive;
        public bool bIntSortPrimary;
        public bool bIntSortSecondary;
        public bool bFloatSortPrimary;
        public bool bFloatSortSecondary;
    }
    public class UIListItemDataBinding
    {
        public NameReference DataSourceTag;
        public int DataSourceIndex;
    }
    public class UIListElementCell
    {
        public UIStyleReference CellStyle;
    }
    public class UIListElementCellTemplate : UIListElementCell
    {
        public string ColumnHeaderText;
        public NameReference CellDataField;
        public UIScreenValue_Extent CellSize;
        public float CellPosition;
    }
    public class UIListItem
    {
        public UIListElementCell[] Cells;
        public UIListItemDataBinding DataSource;
        public int ElementWidget;
    }
    public class UIElementCellSchema
    {
        public UIListElementCellTemplate[] Cells;
    }
    public class CellHitDetectionInfo
    {
        public int HitColumn;
        public int HitRow;
        public int ResizeColumn;
        public int ResizeRow;
    }
    public class ContextMenuItem
    {
        public string ItemText;
        public int ItemId;
        public EContextMenuItemType ItemType;
    }
    public class UISoundCue
    {
        public NameReference SoundName;
        public int SoundToPlay;
    }
    public class PlayerStorageArrayProvider
    {
        public NameReference PlayerStorageName;
        public int PlayerStorageId;
        public int Provider;
    }
    public class SettingsArrayProvider
    {
        public NameReference SettingsName;
        public int SettingsId;
        public int Provider;
    }
    public class DynamicResourceProviderDefinition
    {
        public string ProviderClassName;
        public NameReference ProviderTag;
    }
    public class GameResourceDataProvider
    {
        public string ProviderClassName;
        public NameReference ProviderTag;
        public bool bExpandProviders;
    }
    public class UIInputKeyData
    {
        public string ButtonFontMarkupString;
        public RawInputKeyEventData InputKeyData;
    }
    public class UIDataStoreInputAlias
    {
        public UIInputKeyData PlatformInputKeys;
        public NameReference AliasName;
    }
    public class GameSearchCfg
    {
        public int[] SearchResults;
        public int GameSearchClass;
        public int DefaultGameSettingsClass;
        public int SearchResultsProviderClass;
        public NameReference SearchName;
        public int DesiredSettingsProvider;
        public int Search;
    }
    public class GameSettingsCfg
    {
        public int GameSettingsClass;
        public NameReference SettingsName;
        public int Provider;
        public int GameSettings;
    }
    public class PlayerNickMetaData
    {
        public string PlayerNickColumnName;
        public NameReference PlayerNickName;
    }
    public class RankMetaData
    {
        public string RankColumnName;
        public NameReference RankName;
    }
    public class UIMenuInputMap
    {
        public string MappedText;
        public NameReference FieldName;
        public NameReference Set;
    }
    public class UIKeyRepeatData
    {
        public Double NextRepeatTime;
        public NameReference CurrentRepeatKey;
    }
    public class UIAxisEmulationData : UIKeyRepeatData
    {
        public bool bEnabled;
    }
    public class ArchetypeInstancePair
    {
    }
    public class StyleDataReference
    {
        public STYLE_ID SourceStyleID;
        public int OwnerStyle;
        public int SourceState;
        public int CustomStyleData;
    }
    public class ClothSpecialBoneInfo
    {
        public int[] AttachedVertexIndices;
        public NameReference BoneName;
        public ClothBoneType BoneType;
    }
    public class BoneMirrorInfo
    {
        public int SourceIndex;
        public EAxis BoneFlipAxis;
    }
    public class BoneMirrorExport
    {
        public NameReference BoneName;
        public NameReference SourceBoneName;
        public EAxis BoneFlipAxis;
    }
    public class SkeletalMeshLODInfo
    {
        public TriangleSortOption[] TriangleSorting;
        public bool[] bEnableShadowCasting;
        public float DisplayFactor;
        public float LODHysteresis;
        public int[] LODMaterialMap;
    }
    public class PBScope2D
    {
        public Matrix ScopeFrame;
        public float DimX;
        public float DimZ;
    }
    public class PBScopeProcessInfo
    {
        public bool bPartOfNonRect;
        public bool bGenerateLODPoly;
        public NameReference RulesetVariation;
        public int OwningBuilding;
        public int Ruleset;
    }
    public class OnlineGameSearchParameter
    {
        public EOnlineGameSearchComparisonType ComparisonType;
        public NameReference ObjectPropertyName;
        public int EntryId;
        public EOnlineGameSearchEntryType EntryType;
    }
    public class ROscillator
    {
        public FOscillator Pitch;
        public FOscillator Yaw;
        public FOscillator Roll;
    }
    public class VOscillator
    {
        public FOscillator X;
        public FOscillator Y;
        public FOscillator Z;
    }
    public class SoftBodyTetraLink
    {
        public Vector Bary;
        public int Index;
    }
    public class PBFaceUVInfo
    {
        public Vector2D Size;
        public Vector2D Offset;
    }
    public class PendingUnitTest
    {
        public int ClassType;
        public NameReference InstanceName;
        public int EntryPointInternal;
        public int nSortMarker;
    }
    public class ShakeParams
    {
        public EShakeParam X;
        public EShakeParam Y;
        public EShakeParam Z;
    }
    public class ScreenShakeStruct
    {
        public Vector RotAmplitude;
        public Vector RotFrequency;
        public Vector RotSinOffset;
        public Vector LocAmplitude;
        public Vector LocFrequency;
        public Vector LocSinOffset;
        public NameReference ShakeName;
        public float TimeToGo;
        public float TimeDuration;
        public ShakeParams RotParam;
        public ShakeParams LocParam;
        public float FOVAmplitude;
        public float FOVFrequency;
        public float FOVSinOffset;
        public float TargetingDampening;
        public bool bOverrideTargetingDampening;
        public EShakeParam FOVParam;
    }
    public class TakeHitInfo
    {
        public int DamageType;
        public Vector HitLocation;
        public Vector Momentum;
        public Vector RadialDamageOrigin;
        public int instigatedBy;
        public int PhysicalMaterial;
        public float Damage;
        public byte HitBoneIndex;
    }
    public class PropertyInfo
    {
        public string PropertyValue;
        public NameReference PropertyName;
        public bool bModifyProperty;
    }
    public class ScreenShakeAnimStruct
    {
        public bool bSingleInstance;
        public bool bRandomSegment;
        public int Anim;
        public int Anim_Left;
        public int Anim_Right;
        public int Anim_Rear;
        public float AnimPlayRate;
        public float AnimScale;
        public float AnimBlendInTime;
        public float AnimBlendOutTime;
        public float RandomSegmentDuration;
        public bool bUseDirectionalAnimVariants;
    }
    public class ExternalTexture
    {
        public string Resource;
        public int Texture;
    }
    public class GFxDataStoreBinding
    {
        public UIDataStoreBinding DataSource;
        public string VarPath;
        public string ModelId;
        public string ControlId;
        public NameReference[] CellTags;
        public bool bEditable;
    }
    public class ASValue
    {
        public string S;
        public float N;
        public bool B;
        public ASType Type;
    }
    public class GCReference
    {
        public int m_object;
        public int m_count;
        public int m_statid;
    }
    public class ASDisplayInfo
    {
        public float X;
        public float Y;
        public float Z;
        public float Rotation;
        public float XRotation;
        public float YRotation;
        public float XScale;
        public float YScale;
        public float ZScale;
        public float Alpha;
        public bool visible;
        public bool hasX;
        public bool hasY;
        public bool hasZ;
        public bool hasRotation;
        public bool hasXRotation;
        public bool hasYRotation;
        public bool hasXScale;
        public bool hasYScale;
        public bool hasZScale;
        public bool hasAlpha;
        public bool hasVisible;
    }
    public class ASColorTransform
    {
        public LinearColor Multiply;
        public LinearColor Add;
    }
    public class IpAddr
    {
        public int Addr;
        public int Port;
    }
    public class ConnectionBandwidthStats
    {
        public int UpstreamRate;
        public int DownstreamRate;
        public int RoundtripLatency;
    }
    public class PlayerMember
    {
        public UniqueNetId NetId;
        public int TeamNum;
        public int Skill;
    }
    public class ClientConnectionRequest
    {
        public UniqueNetId PlayerNetId;
        public ConnectionBandwidthStats[] BandwidthHistory;
        public float GoodHostRatio;
        public int MinutesSinceLastTest;
        public bool bCanHostVs;
        public ENATType NatType;
    }
    public class ClientBandwidthTestData
    {
        public int NumBytesToSendTotal;
        public int NumBytesSentTotal;
        public int NumBytesSentLast;
        public float ElapsedTestTime;
        public EMeshBeaconBandwidthTestType testType;
        public EMeshBeaconBandwidthTestState CurrentState;
    }
    public class ClientConnectionBandwidthTestData
    {
        public Double RequestTestStartTime;
        public Double TestStartTime;
        public ConnectionBandwidthStats BandwidthStats;
        public int BytesTotalNeeded;
        public int BytesReceived;
        public EMeshBeaconBandwidthTestState CurrentState;
        public EMeshBeaconBandwidthTestType testType;
    }
    public class ClientMeshBeaconConnection
    {
        public ClientConnectionBandwidthTestData BandwidthTest;
        public UniqueNetId PlayerNetId;
        public ConnectionBandwidthStats[] BandwidthHistory;
        public float ElapsedHeartbeatTime;
        public float GoodHostRatio;
        public int MinutesSinceLastTest;
        public bool bConnectionAccepted;
        public bool bCanHostVs;
        public ENATType NatType;
    }
    public class EventUploadConfig
    {
        public string UploadUrl;
        public float TimeOut;
        public bool bUseCompression;
        public EEventUploadType UploadType;
    }
    public class NewsCacheEntry
    {
        public string NewsUrl;
        public string NewsItem;
        public Pointer HttpDownloader;
        public float TimeOut;
        public bool bIsUnicode;
        public EOnlineEnumerationReadState ReadState;
        public EOnlineNewsType NewsType;
    }
    public class PlayerReservation
    {
        public UniqueNetId NetId;
        public Double Mu;
        public Double Sigma;
        public int Skill;
        public int XPLevel;
        public float ElapsedSessionTime;
    }
    public class PartyReservation
    {
        public UniqueNetId PartyLeader;
        public PlayerReservation[] PartyMembers;
        public int TeamNum;
    }
    public class ClientBeaconConnection
    {
        public UniqueNetId PartyLeader;
        public float ElapsedHeartbeatTime;
    }
    public class PlayerAnnexState
    {
        public Guid OmniToolGuid;
        public int Player;
        public float NextRemindTime;
        public bool bInAnnexZone;
        public bool bLeftAnnexZone;
    }
    public class PlayerUpdateInfo
    {
        public int Player;
        public float PlayerUpdateTime;
        public bool bIsEnteringZone;
    }
    public class ReplicatedAssassinationTarget
    {
        public int CurrentTarget;
        public float DamageReductionBonus;
        public EAssassinationEvent EAEvent;
        public byte NumTargetsKilled;
        public byte Trigger;
    }
    public class AssassinationTargetData
    {
        public int MinWavePointCost;
        public int TimeToKill;
        public float DamageReductionBonus;
    }
    public class Bio2DACellData
    {
        public byte nDataType_NATIVE_MIRROR;
        public Pointer nData_NATIVE_MIRROR;
    }
    public class Bio2daMasterRowIndexRec
    {
        public int nRowIndex;
        public int pTable;
    }
    public class DelayUpdateInfo
    {
        public NameReference EventName;
        public int Controller;
        public float UpdateTime;
        public EPerceptionType Type;
    }
    public class EnemyInfo
    {
        public Vector KnownLocation;
        public Vector InterpLocation;
        public CoverInfo Cover;
        public int Pawn;
        public float InterpTime;
        public float InitialSeenTime;
        public float LastSeenTime;
        public float LastFailedPathTime;
        public float LastKnownLocUpdateTime;
        public float LastHurtByTime;
        public bool bVisible;
    }
    public class BioAnimCheckBlendOutNode
    {
        public int Node;
        public int Index;
    }
    public class BioAnimCheckBlendOutPath
    {
        public BioAnimCheckBlendOutNode[] Nodes;
        public Pointer Next;
    }
    public class BioAnimMovementSyncNode
    {
        public int Node;
        public int NodeWeight;
    }
    public class RotTransitionInfo
    {
        public float RotationOffset;
        public int ChildIndex;
    }
    public class BlendTimeTo
    {
        public float m_fTime;
        public EBioActionAnimNode m_eAnimNode;
    }
    public class BlendTimeFrom
    {
        public BlendTimeTo m_aBlendTimeTo;
        public EBioActionAnimNode m_eAnimNode;
    }
    public class BioAnimNodeBlendByAimLimits
    {
        public float DegreesLeft;
        public float DegreesRight;
        public float DegreesUp;
        public float DegreesDown;
    }
    public class BioChildActivateData
    {
        public float fFinalWeight;
        public float fRemainingTime;
        public float fTotalBlendTime;
        public bool bApplyData;
    }
    public class BioEndBlendData
    {
        public float fEndBlendStartTime;
        public float fEndBlendDuration;
    }
    public class BioChildPinData
    {
        public int[] aChainedTrees;
        public float fEndBlendStartTime;
        public float fEndBlendDuration;
        public float fEndTime;
        public bool bPlayUntilNext;
        public bool bUseDynAnimSets;
    }
    public class BioScalarBlendParams
    {
        public float Min;
        public float Peak;
        public float Max;
    }
    public class BioScalarPrecomputedValues
    {
        public float fRangeLowerRatio;
        public float fRangeUpperRatio;
    }
    public class BioAnimScalarNodeChildDef
    {
        public BioScalarBlendParams BlendParams;
        public NameReference Name;
    }
    public class BioAnimScalarNodeBehaviorDef
    {
        public BioAnimScalarNodeChildDef[] Children;
        public string Description;
        public float BlendPctPerSecond;
        public float DefaultScalar;
        public bool BlendInstant;
    }
    public class BioAnimBlendParams
    {
        public float[] BlendToChildTimes;
        public EBioBlendStatePlayMode PlayMode;
    }
    public class BioAnimStateNodeChildDef
    {
        public BioAnimBlendParams BlendParams;
        public NameReference Name;
        public float DefaultWeight;
    }
    public class BioAnimStateNodeBehaviorDef
    {
        public BioAnimStateNodeChildDef[] Children;
    }
    public class AnimByRotation
    {
        public Rotator DesiredRotation;
        public NameReference AnimName;
    }
    public class PlayerCustomPatternValue
    {
        public LinearColor Stripe1ParameterValue;
        public LinearColor Stripe2ParameterValue;
        public LinearColor Stripe3ParameterValue;
        public NameReference Stripe1ParameterName;
        public NameReference Stripe2ParameterName;
        public NameReference Stripe3ParameterName;
        public int PatternName;
    }
    public class PlayerCustomPatternColorValue
    {
        public LinearColor Stripe1ColorValue;
        public LinearColor Stripe2ColorValue;
        public LinearColor Stripe3ColorValue;
        public NameReference Stripe1ColorName;
        public NameReference Stripe2ColorName;
        public NameReference Stripe3ColorName;
        public int ColorName;
    }
    public class PlayerCustomColor1Value
    {
        public LinearColor ParameterValue;
        public LinearColor PhongParameterValue;
        public NameReference ParameterName;
        public NameReference PhongParameterName;
        public int ColorName;
    }
    public class PlayerCustomColor2Value
    {
        public LinearColor ParameterValue;
        public NameReference ParameterName;
        public int ColorName;
    }
    public class PlayerCustomColor3Value
    {
        public LinearColor ParameterValue;
        public NameReference ParameterName;
        public int ColorName;
    }
    public class PlayerEmissiveColorValue
    {
        public LinearColor ParameterValue;
        public NameReference ParameterName;
        public int ColorName;
    }
    public class PlayerMeshInfo
    {
        public string Male;
        public string MaleVisor;
        public string MaleFaceplate;
        public string MaleMaterialOverride;
        public string Female;
        public string FemaleVisor;
        public string FemaleFaceplate;
        public string FemaleMaterialOverride;
        public bool bHasBreather;
        public bool bHideHead;
        public bool bHideHair;
    }
    public class PlayerSpecInfo
    {
        public ScalarParameterValue SpecParam;
        public ScalarParameterValue SpecPwrParam;
        public ScalarParameterValue EnvMapParam;
    }
    public class PlayerTintInfo
    {
        public VectorParameterValue TintParam;
        public VectorParameterValue PhongParam;
    }
    public class PlayerPatternInfo
    {
        public VectorParameterValue Stripe1Param;
        public VectorParameterValue Stripe2Param;
        public VectorParameterValue Stripe3Param;
    }
    public class CustomizableElement
    {
        public PlayerMeshInfo Mesh;
        public string[] GameEffects;
        public PlayerPatternInfo Pattern;
        public PlayerSpecInfo Spec;
        public PlayerTintInfo Tint;
        public int Id;
        public int Name;
        public int Description;
        public int PlotFlag;
        public bool bCustomizable;
        public ECustomizableElementType Type;
    }
    public class BioVersionedNativeObject
    {
        public int nInstanceVersion;
    }
    public class BioStateEventElement : BioVersionedNativeObject
    {
    }
    public class BioStateEvent : BioVersionedNativeObject
    {
        public Pointer[] lstEventElements;
    }
    public class BioSEEBool : BioStateEventElement
    {
        public int nGlobalBool;
        public bool bNewState;
        public bool bUseParam;
    }
    public class BioSEEConsequence : BioStateEventElement
    {
        public int nConsequence;
    }
    public class BioSEEFloat : BioStateEventElement
    {
        public int nGlobalFloat;
        public float fNewValue;
        public bool bUseParam;
        public bool bIncrement;
    }
    public class BioSEEFunction : BioStateEventElement
    {
        public NameReference FunctionPackage;
        public NameReference FunctionClass;
        public NameReference FunctionName;
        public int nParameter;
    }
    public class BioSEEInt : BioStateEventElement
    {
        public int nGlobalInt;
        public int nNewValue;
        public bool bUseParam;
        public bool bIncrement;
    }
    public class BioSEELocal : BioStateEventElement
    {
        public NameReference ObjectTag;
        public NameReference FunctionName;
        public int nObjectType;
        public bool bUseParam;
    }
    public class BioSEELocalBool : BioSEELocal
    {
        public bool bNewValue;
    }
    public class BioSEELocalFloat : BioSEELocal
    {
        public float fNewValue;
    }
    public class BioSEELocalInt : BioSEELocal
    {
        public int nNewValue;
    }
    public class BioSEESubstate : BioStateEventElement
    {
        public int[] lstSiblingIndices;
        public int nGlobalBool;
        public int nParentIndex;
        public bool bNewState;
        public bool bUseParam;
        public bool bParentTypeOr;
    }
    public class BioQuestProgress : BioVersionedNativeObject
    {
        public int[] lstTaskHistory;
        public int nQuestAdded;
        public int nActiveGoal;
        public bool bQuestUpdated;
    }
    public class BioQuestGoal : BioVersionedNativeObject
    {
        public int srName;
        public int srDescription;
        public int nConditional;
        public int nState;
    }
    public class BioQuestTask : BioVersionedNativeObject
    {
        public int[] lstPlotItemIndices;
        public string sWaypointTag;
        public NameReference nmPlanet;
        public int srName;
        public int srDescription;
        public bool bQuestCompleteTask;
    }
    public class BioPlotItem : BioVersionedNativeObject
    {
        public int srName;
        public int nIconIndex;
        public int nConditional;
        public int nState;
        public int nTargetItems;
    }
    public class BioQuest : BioVersionedNativeObject
    {
        public BioQuestGoal[] lstGoals;
        public BioQuestTask[] lstTasks;
        public BioPlotItem[] lstPlotItems;
        public bool bMission;
    }
    public class BioTaskEval : BioVersionedNativeObject
    {
        public int nQuest;
        public int nTask;
        public int nConditional;
        public int nState;
    }
    public class BioStateTaskList : BioVersionedNativeObject
    {
        public BioTaskEval[] lstTaskEvals;
    }
    public class BioCodexEntry : BioVersionedNativeObject
    {
        public int srTitle;
        public int srDescription;
        public int nTextureIndex;
        public int nPriority;
        public int CodexSound;
    }
    public class BioCodexPage : BioCodexEntry
    {
        public int nSection;
    }
    public class BioCodexSection : BioCodexEntry
    {
        public bool bPrimary;
    }
    public class BioDiscoveredCodexPage : BioVersionedNativeObject
    {
        public int nPage;
        public bool bNew;
    }
    public class BioDiscoveredCodex : BioVersionedNativeObject
    {
        public BioDiscoveredCodexPage[] lstDiscoveredPages;
    }
    public class HenchmanInfoStruct
    {
        public NameReference className;
        public NameReference Tag;
        public int PrettyName;
        public int AlternatePrettyName;
        public NameReference AlternateHenchNamePlotFlag;
        public int HenchAcquiredPlotID;
        public int HenchInSquadPlotID;
        public string HenchmanImage;
    }
    public class BioMassRelayLine
    {
        public string m_sStartClusterLabel;
        public string m_sEndClusterLabel;
        public Vector m_vLeftEndPosition;
        public Vector m_vRightEndPosition;
        public Vector m_vMiddlePosition;
        public Vector m_vDrawScale;
        public Rotator m_rOrientation;
        public int m_nStartClusterIdx;
        public int m_nEndClusterIdx;
        public int m_pLeftEndActor;
        public int m_pRighEndActor;
        public int m_pMiddleActor;
        public bool m_bIsGlowing;
    }
    public class SFXGalaxyMapSelector
    {
        public int[] m_PlotNames;
        public int m_GalaxyMapObject;
        public int m_actor;
        public int m_Name;
        public int m_nPctExplored;
        public int m_State;
        public bool m_Visible;
        public bool m_Visited;
        public bool m_HasCritPath;
    }
    public class SFXSystemScanData
    {
        public Vector vScanOrigin;
        public float fElapsedTime;
    }
    public class BioMessageBoxOptionalParams
    {
        public int srAText;
        public int srBText;
        public int nIconIndex;
        public bool bNoFade;
        public bool bModal;
        public bool bForcePlayersOnly;
        public BioMessageBoxIconSets nIconSet;
        public SFX_MB_Skin m_SkinType;
        public SFX_MB_TextAlign m_TextAlign;
    }
    public class BioZoomFocusConfig
    {
        public float m_fMaxFocusDistance;
        public float m_fNearClipFactor;
        public float m_fNearClipMax;
        public float m_fMinRate;
        public float m_fFocusFraction;
        public float m_fInnerRadiusFactor;
        public float m_fFalloffExponent;
        public float m_fBlurKernelSize;
        public float m_fMaxNearBlurAmount;
        public float m_fMaxFarBlurAmount;
        public Color m_clrModulateBlur;
    }
    public class BioZoomMagnificationConfig
    {
        public float m_fCamSpeedFactor;
        public float m_fFOVFactor;
        public int m_nLevelCount;
        public float m_fTransitionDuration;
    }
    public class ASParams
    {
        public string sVar;
        public int nVar;
        public float fVar;
        public bool bVar;
        public ASParamTypes Type;
    }
    public class PowerEvolveStatDetails
    {
        public string Title;
        public string TotalTitle;
        public int Pct;
        public int BonusPct;
    }
    public class ScarInfo
    {
        public Vector2D Threshold;
        public string Emissive;
        public string Normal;
        public string EyeEmissive;
        public string FemaleEmissive;
        public string FemaleNormal;
        public string FemaleEyeEmissive;
        public LinearColor Color;
    }
    public class EnemyWaveInfo
    {
        public NameReference EnemyType;
        public int MinCount;
        public int MaxCount;
        public int MaxPerWave;
    }
    public class ShieldLoadout
    {
        public int Shields;
        public Vector2D ShieldLevelRange;
        public Vector2D MaxShields;
    }
    public class PowerLevelUp
    {
        public int PowerClass;
        public int EvolvedPowerClass;
        public float Rank;
    }
    public class LoadoutWeaponInfo
    {
        public NameReference className;
        public NameReference UnlockPlotId;
        public int Rating;
        public bool bNotRegularWeaponGUI;
        public bool bStartsUnlocked;
    }
    public class PlotWeaponEditor
    {
        public int WeaponClass;
        public NameReference UnlockPlotId;
        public NameReference EquippedPlotId;
    }
    public class PlotWeapon
    {
        public string WeaponClassName;
        public NameReference UnlockPlotId;
        public NameReference EquippedPlotId;
    }
    public class UnlockableWeaponClass
    {
        public NameReference UnlockPlotId;
        public ELoadoutWeapons WeaponType;
    }
    public class PlayerLoadoutInfoStruct
    {
        public ELoadoutWeapons[] RequiredWeaponClasses;
        public ELoadoutWeapons[] StartingWeaponClasses;
        public NameReference className;
        public int NumOptionalSlots;
    }
    public class LoadoutInfo
    {
        public ELoadoutWeapons[] WeaponClasses;
        public NameReference className;
    }
    public class BonusWeaponInfo
    {
        public NameReference UnlockPlotName;
        public ELoadoutWeapons WeaponClass;
    }
    public class SpecialWeaponInfo
    {
        public NameReference WeaponClassName;
        public NameReference HenchmanClassName;
        public NameReference UnlockPlotName;
    }
    public class PowerSaveRecord
    {
        public int EvolvedChoices;
        public NameReference PowerName;
        public NameReference PowerClassName;
        public float CurrentRank;
        public int WheelDisplayIndex;
    }
    public class GAWAssetSaveInfo
    {
        public int Id;
        public int Strength;
    }
    public class HotKeySaveRecord
    {
        public NameReference PawnName;
        public NameReference PowerName;
    }
    public class PlayerSaveRecord
    {
        public AppearanceSaveRecord Appearance;
        public string firstName;
        public PowerSaveRecord[] Powers;
        public WeaponSaveRecord[] Weapons;
        public GAWAssetSaveInfo[] GAWAssets;
        public WeaponModSaveRecord[] WeaponMods;
        public int[] LoadoutWeaponGroups;
        public HotKeySaveRecord[] HotKeys;
        public string faceCode;
        public NameReference LoadoutWeapons;
        public Guid CharacterGUID;
        public NameReference PlayerClassName;
        public NameReference MappedPower1;
        public NameReference MappedPower2;
        public NameReference MappedPower3;
        public NameReference PrimaryWeapon;
        public NameReference SecondaryWeapon;
        public int srClassFriendlyName;
        public int Level;
        public float CurrentXP;
        public int LastName;
        public int TalentPoints;
        public float CurrentHealth;
        public int Credits;
        public int Medigel;
        public int Grenades;
        public int Eezo;
        public int Iridium;
        public int Palladium;
        public int Platinum;
        public int Probes;
        public float CurrentFuel;
        public bool bIsFemale;
        public bool bCombatPawn;
        public bool bInjuredPawn;
        public bool bUseCasualAppearance;
        public EOriginType Origin;
        public ENotorietyType Notoriety;
    }
    public class HenchmanSaveRecord
    {
        public PowerSaveRecord[] Powers;
        public WeaponSaveRecord[] Weapons;
        public WeaponModSaveRecord[] WeaponMods;
        public NameReference LoadoutWeapons;
        public NameReference Tag;
        public NameReference MappedPower;
        public int CharacterLevel;
        public int TalentPoints;
        public int Grenades;
    }
    public class SFXCareerDescriptor
    {
        public string Career;
        public SFXSavePair[] Saves;
        public SFXSaveDescriptor[] CorruptedSaves;
    }
    public class SFXSaveGameCommandEventArgs
    {
        public SFXSaveDescriptor Descriptor;
        public SFXCareerDescriptor[] Careers;
        public string[] CorruptedCareers;
        public int AdditionalFreeBytesNeeded;
        public int TotalFreeBytes;
        public int PreparedSaveSize;
        public bool bSuccess;
        public bool bRetry;
        public bool bPause;
        public bool bNeedsFreeSpace;
        public bool bTotalFreeBytesSet;
        public bool bPreparedSaveSizeSet;
        public ESFXSaveGameAction Action;
    }
    public class SFXCareerCacheEntry
    {
        public string Career;
        public string firstName;
        public int className;
        public int SaveTypes;
        public EOriginType Origin;
        public ENotorietyType Notoriety;
    }
    public class DynamicLoadInfo
    {
        public string ObjectName;
        public int[] RemotePlayerWithHandle;
        public NameReference SeekFreePackageName;
        public bool bReplicate;
    }
    public class SeekfreeCommonPackageInfo
    {
        public NameReference SeekfreeName;
        public NameReference CommonName;
    }
    public class PackageRemapInfo
    {
        public NameReference PackageName;
        public NameReference SeekFreePackageName;
    }
    public class HitReactionSet
    {
        public NameReference BodyPart;
        public float ReactionChance;
        public bool bIgnoreShields;
        public EReactionTypes Reaction;
        public EHitReactRange MaxRange;
    }
    public class ResistanceInfo
    {
        public float Shield;
        public float Armour;
        public float Biotic;
    }
    public class SFXVocalization
    {
        public SFXVocalizationEvent Event;
        public int Speaker;
        public int Specific;
        public ESFXVocalizationRole Role;
    }
    public class SFXVocalizationEventProperties
    {
        public ESFXVocalizationRole[] Roles;
        public float ChanceToPlay;
        public float MinTimeBetweenSec;
        public float TimeLastPlayed;
        public float Delay;
        public float MaxWitnessDistSq;
        public float MaxDelayedTime;
        public bool bQueueIfBlocked;
        public bool bCanInterrupt;
        public bool bCanPlayIfDead;
        public bool bCanPlayIfRagdolled;
    }
    public class SFXVocalizationEvent
    {
        public int Instigator;
        public int Recipient;
        public int Id;
        public float DelayTimeRemainingSec;
        public float TriggerTimeSec;
        public int DebugIndex;
    }
    public class SFXVocalizationLine
    {
        public ESFXVocalizationVariationType[] SpecificType;
        public int[] SpecificValue;
        public int Sound;
    }
    public class VocEventLog
    {
        public int Speaker;
        public int ReferredTo;
        public float Time;
        public ESFXVocalizationEventID Id;
    }
    public class ProfileData
    {
        public string Header;
        public string Description;
        public int Func;
        public int Utility;
        public NameReference Keyword;
        public bool bNoTarget;
    }
    public class GFxWatchData
    {
        public string Path;
        public string Value;
        public NameReference movie;
    }
    public class TimeDilationStruct
    {
        public InterpCurveFloat Curve;
        public NameReference Identifier;
        public float TotalTime;
        public float Time;
    }
    public class ScaledFloat
    {
        public int[] Bonuses;
        public float X;
        public float Y;
        public int MaxLevel;
        public int Level;
        public float Value;
        public float StaticBonus;
    }
    public class DamageCalculationAlgorithm
    {
        public EDamageCalculationSource Source;
        public NameReference TargetName;
        public NameReference DamageClassName;
        public float BaseDamage;
        public float Weapon_RangeMultiplier;
        public float Weapon_PawnEffectsDamageMultiplier;
        public float Weapon_PawnEffectsHeadshotDamageMultiplier;
        public float Weapon_WeaponEffectsDamageMultiplier;
        public float Weapon_StealthDamageMultiplier;
        public float Weapon_HeadshotDamageMultiplier;
        public float Weapon_RagdollDamageMultiplier;
        public float Weapon_DamageTakenMultiplier;
        public float Power_WeaponMeleeDamageMultiplier;
        public float Power_DamageTakenMultiplier;
        public float Global_DifficultyMultiplier;
        public float Global_CoverMultiplier;
        public float Global_PlayerPopupMultiplier;
        public float Global_OutOfCoverMultiplier;
        public float Global_DamageTakenMultiplier;
        public float Global_HeadshotTakenMultiplier;
        public float ActualDamageDealt;
        public float ActualDamageApplied;
    }
    public class SResourceBudget
    {
        public NameReference nmLevel;
        public int nCredits;
        public int nEezo;
        public int nIridium;
        public int nPlatinum;
        public int nPalladium;
        public int nID;
    }
    public class STreasure
    {
        public NameReference nmLevel;
        public NameReference nmTreasure;
        public NameReference nmTech;
        public NameReference nmRequiredTech;
        public int nTreasureId;
        public int ResourcePrice;
        public int RequiredTechLevel;
        public int DiscoverTechLevel;
        public bool bNoAnimation;
        public bool bMultiLevel;
        public EInventoryResourceTypes Resource;
    }
    public class STech
    {
        public string sImage;
        public string sLargeImage;
        public NameReference nmTech;
        public NameReference nmResearch;
        public int srTitle;
        public int srName;
        public int srMessage;
        public int srDescription;
        public int nLevels;
        public int UnlockId;
    }
    public class SFXSaveDescriptor
    {
        public string Career;
        public int Index;
        public ESFXSaveGameType Type;
    }
    public class SFXSavePair
    {
        public SFXSaveDescriptor Descriptor;
        public int Save;
    }
    public class Accomplishment
    {
        public string Icon;
        public NameReference Name;
        public NameReference Parent;
        public int Index;
        public int XboxAchievementID;
        public int XboxAvatarAwardID;
        public int PS3TrophyID;
        public int Title;
        public int Incomplete;
        public int Complete;
        public int PointValue;
        public int NotificationText;
        public int MPNotificationText;
        public bool IsMultiplayerOnly;
        public EAchievementID LinkedAchievementID;
    }
    public class AccomplishmentProgress
    {
        public NameReference Name;
        public int Index;
        public EProfileSetting LinkedProfileSetting;
    }
    public class GrinderAccomplishment
    {
        public NameReference AccomplishmentName;
        public NameReference AccomplishmentProgressName;
        public int Goal;
        public int Interval;
        public int Title;
        public int Description;
        public int MPDescription;
    }
    public class PlayerInfoEx
    {
        public string firstName;
        public string faceCode;
        public int CharacterClass;
        public Guid CharacterGUID;
        public NameReference BonusTalentClass;
        public int MorphHead;
        public bool bIsFemale;
        public EOriginType Origin;
        public ENotorietyType Notoriety;
    }
    public class LevelTreasureSaveRecord
    {
        public NameReference[] Items;
        public NameReference LevelName;
        public int nCredits;
        public int nXP;
    }
    public class KismetBoolSaveRecord
    {
        public Guid BoolGUID;
        public bool bValue;
    }
    public class ME1PlotTableRecord
    {
        public int[] BoolVariables;
        public int[] IntVariables;
        public float[] FloatVariables;
    }
    public class PlanetSaveRecord
    {
        public Vector2D[] Probes;
        public int PlanetID;
        public bool bVisited;
        public bool bShowAsScanned;
    }
    public class SystemSaveRecord
    {
        public int SystemID;
        public float fReaperAlertLevel;
        public bool bReapersDetected;
    }
    public class GalaxyMapSaveRecord
    {
        public PlanetSaveRecord[] Planets;
        public SystemSaveRecord[] Systems;
    }
    public class DependentDLCRecord
    {
        public NameReference Name;
        public NameReference CanonicalName;
        public int ModuleID;
    }
    public class LevelSaveRecord
    {
        public NameReference LevelName;
        public bool bShouldBeLoaded;
        public bool bShouldBeVisible;
    }
    public class StreamingStateSaveRecord
    {
        public NameReference Name;
        public bool bActive;
    }
    public class DoorSaveRecord
    {
        public Guid DoorGUID;
        public byte CurrentState;
        public byte OldState;
    }
    public class PlaceableSaveRecord
    {
        public Guid PlaceableGUID;
        public byte bIsDestroyed;
        public byte bIsDeactivated;
    }
    public class MorphFeatureSaveRecord
    {
        public NameReference Feature;
        public float Offset;
    }
    public class OffsetBoneSaveRecord
    {
        public Vector Offset;
        public NameReference Name;
    }
    public class ScalarParameterSaveRecord
    {
        public NameReference Name;
        public float Value;
    }
    public class VectorParameterSaveRecord
    {
        public LinearColor Value;
        public NameReference Name;
    }
    public class TextureParameterSaveRecord
    {
        public NameReference Name;
        public NameReference Texture;
    }
    public class MorphHeadSaveRecord
    {
        public NameReference[] AccessoryMeshes;
        public MorphFeatureSaveRecord[] MorphFeatures;
        public OffsetBoneSaveRecord[] OffsetBones;
        public Vector[] LOD0Vertices;
        public Vector[] LOD1Vertices;
        public Vector[] LOD2Vertices;
        public Vector[] LOD3Vertices;
        public ScalarParameterSaveRecord[] ScalarParameters;
        public VectorParameterSaveRecord[] VectorParameters;
        public TextureParameterSaveRecord[] TextureParameters;
        public NameReference HairMesh;
    }
    public class AppearanceSaveRecord
    {
        public MorphHeadSaveRecord MorphHead;
        public int CasualID;
        public int FullBodyID;
        public int TorsoID;
        public int ShoulderID;
        public int ArmID;
        public int LegID;
        public int SpecID;
        public int Tint1ID;
        public int Tint2ID;
        public int Tint3ID;
        public int PatternID;
        public int PatternColorID;
        public int HelmetID;
        public int EmissiveID;
        public bool bHasMorphHead;
        public EPlayerAppearanceType CombatAppearance;
    }
    public class WeaponSaveRecord
    {
        public NameReference WeaponClassName;
        public NameReference AmmoPowerName;
        public NameReference AmmoPowerSourceTag;
        public int AmmoUsedCount;
        public int TotalAmmo;
        public bool bLastWeapon;
        public bool bCurrentWeapon;
    }
    public class WeaponModSaveRecord
    {
        public NameReference[] WeaponModClassNames;
        public NameReference WeaponClassName;
    }
    public class RvrClientEffectParameter
    {
        public Vector ValueVector;
        public NameReference Module;
        public NameReference Variable;
        public float ValueFloat;
        public EParameterType Type;
        public EParameterDataType DataType;
    }
    public class RvrClientEffectTarget
    {
        public Guid Id;
        public Vector HitLocation;
        public Vector RefinedHitLocation;
        public Vector RefinedRayDir;
        public Vector HitNormal;
        public Vector RayDir;
        public Vector SpawnValue;
        public NameReference HitBone;
        public int Instigator;
        public int HitActor;
        public int HitMaterial;
        public bool bHasRefinedHitLocation;
    }
    public class GAWGUICategory
    {
        public string ImagePath;
        public int Id;
        public int srCategoryName;
        public int srCategoryDescription;
    }
    public class GAWAssetModificationTarget
    {
        public int TargetID;
        public int Value;
    }
    public class GAWAsset
    {
        public string AssetName;
        public string ImagePath;
        public string NotificationImagePath;
        public GAWAssetModificationTarget[] ModTargets;
        public int[] UnlockPlotStates;
        public string DebugConditionalDescription;
        public int Id;
        public int GUICategoryID;
        public int StartingStrength;
        public int GUIName;
        public int GUIDescription;
        public int CurrentStrength;
        public int MaxStrength;
        public int ConflictZoneID;
        public bool bIsExploration;
        public bool bShowNotificationOnAward;
        public EGAWAssetType Type;
        public EGAWAssetSubType SubType;
        public GAWExternalAssetID ExternalAssetEnum;
    }
    public class BonusPowerUnlockData
    {
        public string PowerClassName;
        public int BonusPowerID;
        public int PlotConditionalID;
        public int PlotStateID;
        public int srTitle;
    }
    public class TD
    {
        public string Level;
        public string[] TREASURE;
        public string[] ConditionalGAWAssets;
        public string[] UndetectableGAWAssets;
        public int PlotId;
        public int Credits;
        public int AllianceCredits;
        public int XP;
        public EME3Level LevelEnum;
    }
    public class ArmorTreasureData
    {
        public string ArmorString;
        public int srDisplayName;
        public int ArmorPlotState;
        public int Conditional;
        public EArmorTreasurePiece ArmorPiece;
    }
    public class SetMissionPlotIntPair
    {
        public int Id;
        public int V;
    }
    public class SetMissionCondSetPair
    {
        public int C;
        public int CA;
        public int T;
        public int TA;
    }
    public class SetupMissionData
    {
        public int[] PlotIDSet;
        public int[] PlotIDClear;
        public SetMissionPlotIntPair[] PlotInts;
        public SetMissionCondSetPair[] PlotCond;
        public NameReference Mission;
        public NameReference LoadMapName;
    }
    public class SetupModifierData
    {
        public int[] PlotIDSet;
        public int[] PlotIDClear;
        public SetMissionPlotIntPair[] PlotInts;
        public SetMissionCondSetPair[] PlotCond;
        public NameReference Modifier;
    }
    public class BioDialogReplyListDetails
    {
        public string sParaphrase;
        public int nIndex;
        public int srParaphrase;
        public EReplyCategory Category;
    }
    public class BioDialogNode
    {
        public string sText;
        public int srText;
        public int nConditionalFunc;
        public int nConditionalParam;
        public int nStateTransition;
        public int nStateTransitionParam;
        public int nExportID;
        public int nScriptIndex;
        public int nCameraIntimacy;
        public bool bFireConditional;
        public bool bAmbient;
        public bool bNonTextLine;
        public bool bIgnoreBodyGestures;
        public bool bAlwaysHideSubtitle;
        public EConvGUIStyles eGUIStyle;
    }
    public class BioDialogEntryNode : BioDialogNode
    {
        public BioDialogReplyListDetails[] ReplyListNew;
        public int[] aSpeakerList;
        public int nSpeakerIndex;
        public int nListenerIndex;
        public bool bSkippable;
    }
    public class BioDialogReplyNode : BioDialogNode
    {
        public int[] EntryList;
        public int nListenerIndex;
        public bool bUnskippable;
        public bool bIsDefaultAction;
        public bool bIsMajorDecision;
        public EReplyTypes ReplyType;
    }
    public class BioDialogSpeaker
    {
        public NameReference sSpeakerTag;
    }
    public class BioDialogScript
    {
        public NameReference sScriptTag;
    }
    public class BioStageDirection
    {
        public string sText;
        public int srStrRef;
    }
    public class BioSpeakerData
    {
        public NameReference nmSpeakerTag;
        public int pSpeakerActor;
    }
    public class BioDialogLookat
    {
        public int pActor;
        public float fLookAtDelay;
        public int pLookAtTarget;
    }
    public class BioNextCamData
    {
        public Vector vPos;
        public Rotator rRot;
        public BioStageDOFData tDOFData;
        public NameReference sCameraName;
        public float fFov;
        public float fNearPlane;
        public bool bUseThis;
    }
    public class BioSavedActorPos
    {
        public Vector vPos;
        public Rotator rRot;
        public int pActor;
    }
    public class BioInterruptReplyInfo
    {
        public int nReplyListIndex;
        public float fWindowStartTimeRemaining;
        public float fWindowTimeRemaining;
        public bool bEnabled;
        public bool bActivated;
        public EInterruptionType eInterruptType;
    }
    public class BioConvLightingData
    {
        public NameReference TargetBoneName;
        public float KeyLight_Scale_Red;
        public float KeyLight_Scale_Green;
        public float KeyLight_Scale_Blue;
        public float FillLight_Scale_Red;
        public float FillLight_Scale_Green;
        public float FillLight_Scale_Blue;
        public Color RimLightColor;
        public float RimLightScale;
        public float RimLightYaw;
        public float RimLightPitch;
        public float BouncedLightingIntensity;
        public int LightRig;
        public float LightRigOrientation;
        public bool bLockEnvironment;
        public bool bTriggerFullUpdate;
        public bool bUseForNextCamera;
        public bool bCastShadows;
        public ERimLightControlType RimLightControl;
        public EConvLightingType LightingType;
    }
    public class BioNextLightingData
    {
        public BioConvLightingData tData;
        public int pActor;
        public bool bUseThis;
    }
    public class BioConvActorPropData
    {
        public Map_Mirror mapMeshPropData;
        public Map_Mirror mapWeaponPropData;
    }
    public class BioConvActorInitMeshTrans
    {
        public Vector vOrigTranslation;
        public int pActor;
    }
    public class BodyStance
    {
        public NameReference[] AnimName;
    }
    public class BoneAndWeight
    {
        public NameReference BoneName;
        public float BoneWeight;
    }
    public class BoneListEmissionArea
    {
        public BoneAndWeight[] Bones;
        public NameReference AreaTag;
        public bool UseNumVertsAsWeights;
    }
    public class BioDisplayNotice
    {
        public string strTitle;
        public int nEventType;
        public int nTimeToLive;
        public int nIconIndex;
        public int nContext;
        public int srTitle;
        public int nQuantity;
        public int nQuantMin;
        public int nQuantMax;
    }
    public class BioTalentNotice
    {
        public string sName;
        public int nIcon;
        public int oCharacter;
    }
    public class BioDOFTrackData
    {
        public Vector vFocusPosition;
        public float fFalloffExponent;
        public float fBlurKernelSize;
        public float fMaxNearBlurAmount;
        public float fMaxFarBlurAmount;
        public Color cModulateBlurColor;
        public float fFocusInnerRadius;
        public float fFocusDistance;
        public float fInterpolateSeconds;
        public bool bEnableDOF;
    }
    public class BioGestTrackPriority
    {
        public int nTrackIndex;
        public int nPriority;
    }
    public class BioGestureRenameData
    {
        public NameReference nmOldAnim;
        public NameReference nmNewSet;
        public NameReference nmNewAnim;
    }
    public class BioGestureData
    {
        public int[] aChainedGestures;
        public NameReference nmPoseSet;
        public NameReference nmPoseAnim;
        public NameReference nmGestureSet;
        public NameReference nmGestureAnim;
        public NameReference nmTransitionSet;
        public NameReference nmTransitionAnim;
        public float fPlayRate;
        public float fStartOffset;
        public float fEndOffset;
        public float fStartBlendDuration;
        public float fEndBlendDuration;
        public float fWeight;
        public float fTransBlendTime;
        public bool bInvalidData;
        public bool bOneShotAnim;
        public bool bChainToPrevious;
        public bool bPlayUntilNext;
        public bool bTerminateAllGestures;
        public bool bUseDynAnimSets;
        public bool bSnapToPose;
        public EBioValidPoseGroups ePoseFilter;
        public EBioGestureValidPoses ePose;
        public EBioGestureGroups eGestureFilter;
        public EBioGestureValidGestures eGesture;
    }
    public class BioGesturePinScrubData
    {
        public NameReference nmAnimSet;
        public NameReference nmAnimSeq;
        public float fTime;
        public float fWeight;
    }
    public class BioGestureScrubData : BioGestureData
    {
        public BioGesturePinScrubData[] aGestPins;
        public NameReference nmNextPoseSet;
        public NameReference nmNextPoseAnim;
        public float fCurPoseTime;
        public float fNextPoseTime;
        public float fTransitionTime;
        public float fCurPoseWeight;
        public float fTransitionWeight;
        public float fNextPoseWeight;
    }
    public class BioInterruptTrackData
    {
        public bool bShowInterrupt;
    }
    public class BioLookAtTrackData
    {
        public NameReference nmFindActor;
        public bool bEnabled;
        public bool bInstantTransition;
        public bool bLockedToTarget;
        public ESFXFindByTagTypes eFindActorMode;
    }
    public class BioPropTrackData
    {
        public int pWeaponClass;
        public NameReference nmProp;
        public NameReference nmAction;
        public int pPropMesh;
        public int pActionPartSys;
        public int pActionClientEffect;
        public bool bEquip;
        public bool bForceGenericWeapon;
    }
    public class BioWeaponPropActionData
    {
        public int pfnExecute;
        public int pfnGetTiming;
    }
    public class BioActionPreviewResource
    {
        public NameReference nmAction;
        public NameReference nmAnimation;
        public int pPartSysCmp;
        public int pAnimSet;
        public bool bEquipped;
    }
    public class BioPropPreviewResource
    {
        public Map_Mirror mapActions;
        public NameReference nmProp;
        public int pPropCmp;
        public bool bEquipped;
    }
    public class BioSetFacingData
    {
        public NameReference nmStageNode;
        public float fOrientation;
        public bool bApplyOrientation;
        public EDynamicStageNodes eCurrentStageNode;
    }
    public class BioSubtitleTrackData
    {
        public int nStrRefID;
        public float fLength;
        public bool bShowAtTop;
        public bool bUseOnlyAsReplyWheelHint;
    }
    public class BioCameraSwitchData
    {
        public NameReference nmStageSpecificCam;
        public bool bForceCrossingLineOfAction;
        public bool bUseForNextCamera;
    }
    public class SFXGestureData
    {
        public int[] aChainedGestures;
        public NameReference nmPoseSet;
        public NameReference nmPoseAnim;
        public NameReference nmGestureSet;
        public NameReference nmGestureAnim;
        public NameReference nmTransitionSet;
        public NameReference nmTransitionAnim;
        public float fPlayRate;
        public float fStartOffset;
        public float fEndOffset;
        public float fStartBlendDuration;
        public float fEndBlendDuration;
        public float fWeight;
        public float fTransBlendTime;
        public bool bInvalidData;
        public bool bOneShotAnim;
        public bool bChainToPrevious;
        public bool bPlayUntilNext;
        public bool bTerminateAllGestures;
        public bool bUseDynAnimSets;
        public bool bSnapToPose;
    }
    public class BioGestDataKey
    {
        public SFXGestureData tRawData;
        public int pChainTree;
        public bool bUseDynamicAnimSets;
    }
    public class BioAnimSetReference
    {
        public int nRefCount;
        public int pAnimSet;
    }
    public class BioARPUBodyConfig
    {
        public NameReference nmCurveName;
        public NameReference nmAnimSet;
        public NameReference nmAnimSeq;
        public float fStartBlendDuration;
        public float fEndBlendDuration;
        public bool bUsesSingleKeyframe;
    }
    public class BioGestPose
    {
        public NameReference nmPose;
        public NameReference nmAnimSet;
        public NameReference nmAnimSeq;
        public IntPoint tPosition;
        public NameReference nmGroup;
        public NameReference nmFemaleNodeName;
    }
    public class BioGestGesture : BioGestPose
    {
        public NameReference nmGesture;
        public bool bOneShotAnim;
    }
    public class BioGestTransition : BioGestPose
    {
        public NameReference nmDestPose;
        public float fTransBlendTime;
        public bool bNoTransAnim;
    }
    public class BioAmbPerfGestKey
    {
        public NameReference nmPerfName;
        public NameReference nmPoseName;
    }
    public class BioAmbPerfBaseData : BioAmbPerfGestKey
    {
        public IntPoint tPosition;
        public NameReference nmPropAction;
        public float fPropActionTimeDelay;
        public bool bEnterEvent;
        public bool bExitEvent;
    }
    public class BioAmbPerfGesture : BioAmbPerfBaseData
    {
        public NameReference nmGestureName;
        public float fPlayRate;
        public float fPlayWeight;
        public int nWeighting;
        public float fRetriggerDelay;
    }
    public class BioAmbPerfPoseTransData
    {
        public NameReference nmPoseName;
        public int nWeighting;
    }
    public class BioAmbPerfPose : BioAmbPerfBaseData
    {
        public BioAmbPerfPoseTransData[] aTransData;
        public NameReference nmStartTrans;
        public int nPoseChangeChance;
        public int nPlayGestureChance;
        public float fChoiceTimeDelay;
        public bool bStartHere;
        public bool bEnterTransDoneEvent;
    }
    public class BioAmbientPerformance : BioAmbPerfBaseData
    {
        public NameReference nmGroup;
        public NameReference nmPropName;
        public NameReference nmOriginalName;
        public bool bValidForDLCOnly;
        public bool bSuppressDamage;
    }
    public class BioPropClientEffectParams
    {
        public Vector vHitLocation;
        public Vector vHitNormal;
        public Vector vRayDir;
        public Vector vSpawnValue;
        public NameReference nmHitBone;
    }
    public class BioMeshPropActionData
    {
        public string sParticleSys;
        public string sClientEffect;
        public BioPropClientEffectParams tSpawnParams;
        public Vector vOffsetLocation;
        public Rotator rOffsetRotation;
        public Vector vOffsetScale;
        public NameReference nmActionName;
        public NameReference nmAttachTo;
        public bool bActivate;
        public bool bCooldown;
    }
    public class BioMeshPropData
    {
        public Map_Mirror mapActions;
        public string sMesh;
        public Vector vOffsetLocation;
        public Rotator rOffsetRotation;
        public Vector vOffsetScale;
        public NameReference nmPropName;
        public NameReference nmAttachTo;
    }
    public class BioWeaponPropData
    {
        public string[] aWeaponClassPrefixes;
        public string[] aWeaponPackages;
        public NameReference nmWeaponBaseClassName;
    }
    public class TimedPlotUnlock_t
    {
        public int PlotBool;
        public int UnlockDay;
    }
    public class PlotIdenfitier
    {
        public int nIndex;
        public SFXPlotType nType;
    }
    public class NewGameCanonPlot
    {
        public PlotIdenfitier Id;
        public int nValue;
        public int nConditional;
        public int nConditionalParameter;
    }
    public class CopyPlot
    {
        public int nSId;
        public int nTId;
        public SFXPlotType nType;
    }
    public class BioHardLinkReference
    {
        public int Object;
    }
    public class HintTrackingData
    {
        public float Times;
        public int Num;
        public float LastTime;
        public float FirstTime;
        public int QueueHead;
    }
    public class HintDefinition
    {
        public NameReference HintName;
        public NameReference ClearEvent;
        public NameReference ClearContext;
        public int DefaultText;
        public int PS3Text;
        public int PCText;
        public float DisplayDuration;
        public float CooldownTime;
        public float UpdateTime;
        public float TimeRemaining;
        public int MaxDifficulty;
        public int HintFunction;
        public bool Enabled;
        public bool ImmediatelyRelevant;
        public SFXHintPosition HintPosition;
    }
    public class SFXNotification
    {
        public string sTitle;
        public string sSubtitle;
        public string sBody;
        public string sImageResource;
        public NameReference nmType;
        public NameReference nmRemoteEvent;
        public NameReference nmSound;
        public NameReference nmStopSound;
        public NameReference nmIcon;
        public int nID;
        public int nPriority;
        public float CreationTime;
        public float fDisplayTime;
        public int oImage;
        public int nFlourishID;
        public int nBarPercent;
        public int Data1;
        public bool bIsMini;
        public EAsyncLoadStatus eLoadStatus;
    }
    public class SFXNotificationData
    {
        public string sImageResource;
        public NameReference nmType;
        public NameReference nmRemoteEvent;
        public NameReference nmSound;
        public NameReference nmIcon;
        public int srTitle;
        public int srSubTitle;
        public int srBody;
        public int srAltTitle;
        public int srAltBody;
        public int srAltSubtitle;
        public float DisplayTime;
        public int Priority;
        public int nFlourishID;
        public bool bCanBeMerged;
        public bool bIsMini;
    }
    public class SavedMoveReplicationInfo
    {
        public Vector ForcedLocation;
        public int PC;
        public int ReplicatedMoves;
        public bool bForceNewLocation;
    }
    public class CoverAcquisitionParams
    {
        public float MinCameraDotCover;
        public float MinSlotDotPlayer;
        public float MinPlayerDotCoverOffset;
        public float MaxDist;
        public float MaxCoverHeightFactor;
    }
    public class LocalEnemy
    {
        public int Enemy;
        public bool bVisible;
        public bool bSeen;
        public bool bHasLOS;
    }
    public class LocalizedKeyName
    {
        public NameReference Key;
        public int Name;
    }
    public class StaticKeyBind
    {
        public string Command;
        public NameReference Name;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool bDebug;
    }
    public class SFXGUISceneView
    {
        public Matrix ViewProjectionMatrix;
        public Vector WorldspaceViewLocation;
    }
    public class ScreenRect
    {
        public float Top;
        public float Left;
        public float Width;
        public float Height;
    }
    public class GPtr_Mirror
    {
        public Pointer pObject;
    }
    public class SFXGUILegacyScaleformResource
    {
        public int GFxMovie;
    }
    public class BioSFQueuedCommand
    {
        public string sCommand;
        public ASParams[] lstParameters;
    }
    public class BioDUITimerDetails
    {
        public float fCurTime;
        public float fEndTime;
        public float fIntervalTime;
        public float fNextInterval;
        public bool bIncrementing;
        public bool bIntervalTriggered;
        public bool bCompleted;
        public bool bRunning;
        public bool bActive;
        public bool bFirstUpdate;
    }
    public class BioDUIPulseDetails
    {
        public float fHalfCycleTime;
        public float fMinAlpha;
        public float fCurCycle;
        public BioDUIElements nElement;
    }
    public class BioDUIElementStatus
    {
        public bool bVisible;
        public bool bFading;
    }
    public class TraceStripKey
    {
        public float fTime;
        public float fValue;
    }
    public class TraceStripChannel
    {
        public TraceStripKey[] Keys;
        public LinearColor DrawColor;
        public NameReference nmButton;
        public NameReference nmAxis;
        public NameReference nmProperty;
        public NameReference nmAnimnode;
        public int Owner;
        public float fDynamicMax;
        public int CachedProperty;
        public int CachedAnimNode;
    }
    public class DesignerText
    {
        public string Text;
        public NameReference Id;
        public float X;
        public float Y;
        public float Duration;
        public float Scale;
        public float TimeStamp;
        public bool Center;
    }
    public class DesignerBar
    {
        public NameReference Id;
        public float X;
        public float Y;
        public float Width;
        public float Lifetime;
        public float SpawnTime;
        public int Color;
        public bool Grows;
        public bool Shrinks;
    }
    public class RotationModeTrackKey
    {
        public NameReference FindActorTag;
        public float InterpTime;
    }
    public class LevelReward
    {
        public int Level;
        public int ExperienceRequired;
        public int TalentReward;
        public int HenchmanTalentReward;
    }
    public class LookAtBoneDefinition
    {
        public NameReference[] m_anTargetBones;
        public NameReference m_nBoneName;
        public NameReference m_nmMasterBoneName;
        public float m_fLimit;
        public float m_fUpDownLimit;
        public float m_fDelay;
        public float m_fSpeedFactor;
        public float m_fMaxAcceleration;
        public float m_fMaxDeceleration;
        public float m_fConversationStrength;
        public bool m_bSeparateUpDownLimit;
        public bool m_bUseUpAxis;
        public bool m_bUpAxisInLocalSpace;
        public bool m_bLookAtInverted;
        public bool m_bUpAxisInverted;
        public bool m_bUseAcceleration;
        public bool m_bUseMasterBone;
        public byte m_nLookAxis;
        public byte m_nUpAxis;
    }
    public class PowerUnlockRequirement
    {
        public int RequiredPowerClass;
        public int PowerClass;
        public float Rank;
        public int RequiredLevel;
        public int CustomUnlockText;
    }
    public class TextureParameter
    {
        public NameReference nName;
        public int m_pTexture;
    }
    public class ColorParameter
    {
        public LinearColor cValue;
        public NameReference nName;
    }
    public class ScalarParameter
    {
        public NameReference nName;
        public float sValue;
    }
    public class MorphFeature
    {
        public NameReference sFeatureName;
        public float Offset;
    }
    public class OffsetBonePos
    {
        public Vector vPos;
        public NameReference nName;
    }
    public class TextureData
    {
        public NameReference m_nParamName;
        public int m_oTexture;
    }
    public class ScalarData
    {
        public NameReference m_nParamName;
        public float m_fScalarValue;
    }
    public class HairComponent
    {
        public string StyleName;
        public string MeshName;
        public string ScalpMorphName;
        public TextureData[] m_aHairTextures;
        public ScalarData[] m_aHairScalars;
        public int HairMesh;
        public float ScalpMorphWeight;
        public EBioMorphUtilityHairComponentType HairType;
    }
    public class HairData
    {
        public string PackageName;
        public string HairMorphSpecMaskName;
        public HairComponent[] HairComponents;
    }
    public class MaterialComponent
    {
        public string Label;
        public string Name;
        public string Panel;
        public string ParameterName;
        public string[] Params;
        public EBioMorphUtilityComponentType Type;
    }
    public class MaterialGroup
    {
        public string Name;
        public MaterialComponent[] Components;
    }
    public class MaterialPanel
    {
        public string Name;
        public MaterialGroup[] Groups;
    }
    public class MaterialData
    {
        public MaterialPanel[] Panels;
    }
    public class AdditionalData
    {
        public HairData Hair;
    }
    public class SliderModifierSliderData
    {
        public int[] m_aoSliderData;
        public float[] m_fRandWeights;
        public float m_fRandWeightsTotal;
    }
    public class SliderModifier
    {
        public string m_sName;
        public float[] m_aRandMin;
        public float[] m_aRandMax;
        public SliderModifierSliderData[] m_aSliders;
    }
    public class Slider
    {
        public string m_sName;
        public int[] m_aoSliderData;
        public float[] m_fRandWeights;
        public SliderModifier[] m_aSliderModifiers;
        public int m_iIndex;
        public int m_iValue;
        public int m_iSteps;
        public int m_iStringRef;
        public int m_iDescriptionStringRef;
        public float m_fRandWeightsTotal;
        public float m_fRandMin;
        public float m_fRandMax;
        public bool m_bNotched;
    }
    public class Category
    {
        public string m_sCatName;
        public Slider[] m_aoSliders;
        public int m_iCatIndex;
        public int m_iStringRef;
        public int m_iDescriptionStringRef;
    }
    public class FaceData
    {
        public AdditionalData m_pAdditionalParams;
        public Category[] m_oCategories;
    }
    public class BaseSliders
    {
        public string m_sSliderName;
        public float m_fValue;
    }
    public class BaseHeads
    {
        public BaseSliders[] m_fBaseHeadSettings;
    }
    public class SliderRemapping
    {
        public string CategoryName;
        public string SliderName;
        public int[] Remappings;
    }
    public class EmissionAreaWeight
    {
        public NameReference AreaTag;
        public float Weight;
    }
    public class RootMotionOverrideEntry
    {
        public int Node;
        public ERootMotionMode RMMode;
        public ERootMotionRotationMode RMRMode;
    }
    public class AttackReservation
    {
        public int nID;
        public int nTicketCost;
        public float fTimeUntilExpiry;
        public bool bUsingTicket;
    }
    public class RigidBodyCallback
    {
        public int RBCallback;
        public int nPriority;
    }
    public class TemporaryAnimSetInfo
    {
        public int TempAnimSet;
        public int RefCount;
        public float TimeLeft;
    }
    public class ReactionPart
    {
        public NameReference[] BoneNames;
        public NameReference BodyPart;
    }
    public class AbilityTimeStamp
    {
        public NameReference AbilityName;
        public float TimeStamp;
    }
    public class WeaponAnimSpec
    {
        public int[] m_animSets;
        public int m_drawAnimSet;
    }
    public class ReplicatedCustomAction
    {
        public Vector TargetLocation;
        public int TriggerCounter;
        public int CustomActionType;
        public int Target;
        public int PowerCustomActionType;
        public EReplicatedCustomActionCmd Cmd;
    }
    public class ReplicatedWeaponImpact
    {
        public int oWeapon;
        public int oProjectile;
        public int CustomActionReactionType;
        public byte TriggerCounter;
        public byte Delay;
    }
    public class ReplicatedCustomActionImpact
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public int CustomActionType;
        public int Instigator;
        public int ImpactCount;
        public int CustomActionReactionType;
        public int PowerCustomActionType;
        public bool bFirstTarget;
        public byte TriggerCounter;
    }
    public class ReplicatedPowerSubsequentImpact
    {
        public int PowerType;
        public int Instigator;
        public int CustomActionReactionType;
        public int ImpactCount;
        public bool DoCallback;
        public byte TriggerCounter;
        public byte Duration;
        public byte Delay;
    }
    public class ReplicatedRadiusDamage
    {
        public int DamageType;
        public Vector HitLocation;
        public Vector Momentum;
        public float Damage;
        public int DamageCauser;
        public int CustomActionReactionType;
        public byte TriggerCounter;
    }
    public class ReplicatedAnimatedReaction
    {
        public int DamageType;
        public Vector HitLocation;
        public Vector HitNormal;
        public int CustomActionType;
        public int BoneIndex;
        public byte TriggerCounter;
        public byte RandomRoll;
    }
    public class ReplicatedPowerCombo
    {
        public int EffectClass;
        public Vector HitLocation;
        public int DetonatorPowerInstigator;
        public int SourcePowerInstigator;
        public byte TriggerCounter;
        public byte DetonatorPowerID;
        public byte SourcePowerID;
    }
    public class ReplicatedPowerComboImpact
    {
        public int PowerType;
        public int Instigator;
        public int CustomActionReactionType;
        public int PowerComboTypeUniqueID;
        public byte TriggerCounter;
        public byte PowerRank;
        public byte MiscFlags;
    }
    public class PowerSaveInfo
    {
        public int EvolvedChoices;
        public NameReference PowerName;
        public NameReference PowerClassName;
        public float CurrentRank;
        public int WheelDisplayIndex;
    }
    public class AimAssistBox
    {
        public float Width;
        public float Height;
        public float SoftMargin;
        public EAimNodes NodeType;
    }
    public class DamagePart
    {
        public NameReference PartName;
        public float DamageScale;
    }
    public class BioUsedMeshPropData
    {
        public int[] aPartSys;
        public int[] aClientEffects;
        public int pPropCmp;
    }
    public class BioFoundWeaponData
    {
        public int pWeapon;
        public bool bSpawned;
        public bool bCurrentlyEquipped;
    }
    public class BioQueuedAction
    {
        public Pointer pPropActionData;
        public Pointer pGestureData;
        public float fTimeQueued;
        public bool bAddedThisFrame;
    }
    public class BioGesturesPosePlaying
    {
        public NameReference nmSetName;
        public NameReference nmAnimName;
        public bool bUseDynSets;
        public bool bLockedAsPoseCache;
    }
    public class RTPCPair
    {
        public string RTPCName;
        public float RTPCValue;
    }
    public class BioVOSettings
    {
        public Color cSubtitleColour;
        public float fSubtitleLength;
        public int pSubtitleRefObject;
        public float fDelayStarting;
        public bool bSuppressSubtitlesIfVO;
        public bool bAlert;
        public bool bAlwaysHideSubtitle;
        public bool bHasPriority;
        public byte nSubtitleMode;
    }
    public class MantleInfo
    {
        public CoverSlot CurrentSlot;
        public CoverSlot LeftSlot;
        public CoverSlot RightSlot;
        public BasedPosition MantleStartLoc;
        public BasedPosition MantleEndLoc;
        public BasedPosition EstimatedLandingLoc;
        public float MantleDistance;
        public int DestLink;
        public int LeftLink;
        public int RightLink;
        public int CurrentLink;
        public int CurrentSlotIdx;
        public int LeftSlotIdx;
        public int RightSlotIdx;
        public float CurrentSlotPct;
        public float FallForwardVelocity;
        public float RootMotionScaleFactor;
        public float DefaultMantleDistance;
        public bool bForced;
        public bool bIsOnASlot;
    }
    public class WwiseAudioPair
    {
        public int Play;
        public int Stop;
    }
    public class PlanetSun
    {
        public LinearColor SunColor;
        public float Brightness;
    }
    public class KismetOrder
    {
        public int FireCallback;
        public int MoveCallback;
        public int oTargetActor;
        public float fDistOffset;
        public float fAttackDuration;
        public bool bWalk;
        public bool bForceShoot;
        public KismetOrderType eOrderType;
    }
    public class ValidEnemyCacheDatum
    {
        public CoverInfo EnemyCover;
        public int EnemyPawn;
    }
    public class HenchmanOrder
    {
        public Vector vTargetLocation;
        public NameReference nmPower;
        public int oTargetActor;
        public int oWeapon;
        public bool bInstantOrder;
        public bool bExecutingOrder;
        public bool bPowerUseIsInstant;
        public HenchmanOrderType eOrderType;
    }
    public class BioRadarData
    {
        public Vector vPosition;
        public int nIndex;
        public float fPassTime;
        public int nSize;
        public int nRelativeZ;
        public bool bPlayerLockedOn;
        public EBioRadarType eRadarType;
    }
    public class BioDamageIndicatorData
    {
        public float fCooldownTime;
    }
    public class PostProcessInfo
    {
        public float Shadows;
        public float MidTones;
        public float HighLights;
        public float Desaturation;
        public ETVType Preset;
    }
    public class PlayerOrder
    {
        public Vector vTarget;
        public Vector vOriginalCameraLocation;
        public Rotator rOriginalCameraRotation;
        public NameReference nmPower;
        public int oTarget;
        public int oSwitchWeapon;
    }
    public class SFXHotKeyDefinition
    {
        public NameReference nmPawn;
        public NameReference nmPower;
    }
    public class BioPPSettingsCallbackData
    {
        public Pointer fpCallback;
        public Pointer pData;
    }
    public class SFXWeaponAimMode
    {
        public NameReference ScopeResource;
        public float ZoomFOV;
        public float FrictionMultiplier;
        public float AdhesionMultiplier;
        public bool bScoped;
    }
    public class TracerSpec
    {
        public Vector Scale3D;
        public int StaticMesh;
        public int StandardPSTemplate;
        public int PlayerPSTemplate;
        public float AccelRate;
        public float Speed;
        public float MaxSpeed;
    }
    public class ZoomSnapInfo
    {
        public float OuterSnapAngle;
        public float InnerSnapAngle;
        public float SnapOffsetMag;
        public EAimNodes AimNode;
    }
    public class DebugMenuEntry
    {
        public string Name;
        public string Command;
    }
    public class InputOverride
    {
        public string Alias;
        public int InputDelegate;
        public bool bPress;
        public bool bExclusive;
    }
    public class PowerReservation
    {
        public int nID;
        public float fTimeUntilExpiry;
    }
    public class SFXGalaxyAudioData
    {
        public string BuyFuelSound_PctFullRTPCName;
        public int[] ShipHalfFuelVO;
        public int[] ShipNoFuelClusterVO;
        public int[] ShipNoFuelClusterReturnVO;
        public int[] ShipNoFuelSystemVO;
        public string ShipTravelSound_SpeedRTPCName;
        public string ShipTravelSound_ThrustRTPCName;
        public string ShipTravelSound_FuelQtyRTPCName;
        public string ShipTravelSound_SystemClusterRTPCName;
        public int ErrorSound;
        public int BuyFuelSound;
        public int BuyFuelSoundStop;
        public int BuyFuelSound_Full;
        public int ShipOutOfFuel_Start;
        public int ShipOutOfFuel_Stop;
        public int BuyProbeSound;
        public int ShipTravelSound_Start;
        public int ShipTravelSound_Stop;
    }
    public class SFXGalaxyTemplatePair
    {
        public NameReference Tag;
        public int pActor;
    }
    public class SFXGalaxyTemplates_Galaxy
    {
        public SFXGalaxyTemplatePair Cluster;
        public SFXGalaxyTemplatePair ClusterCircle;
        public SFXGalaxyTemplatePair GalaxySphere;
        public SFXGalaxyTemplatePair Twinkle;
        public SFXGalaxyTemplatePair Crosshair;
        public SFXGalaxyTemplatePair Camera;
        public SFXGalaxyTemplatePair ClusterPath;
        public SFXGalaxyTemplatePair DisabledClusterCircle;
        public SFXGalaxyTemplatePair ReaperIcon;
        public SFXGalaxyTemplatePair ReaperClusterCircle;
        public SFXGalaxyTemplatePair CurrentLocationIcon;
        public SFXGalaxyTemplatePair PulsingCircleHighlight;
    }
    public class SFXGalaxyTemplates_Cluster
    {
        public SFXGalaxyTemplatePair[] ClusterPlanes;
        public SFXGalaxyTemplatePair[] ClusterBackgrounds;
        public SFXGalaxyTemplatePair System;
        public SFXGalaxyTemplatePair SystemCircle;
        public SFXGalaxyTemplatePair ClusterSphere;
        public SFXGalaxyTemplatePair ClusterStars;
        public SFXGalaxyTemplatePair FuelElipse;
        public SFXGalaxyTemplatePair Emitter;
        public SFXGalaxyTemplatePair Crosshair;
        public SFXGalaxyTemplatePair Camera;
    }
    public class SFXGalaxyTemplates_System
    {
        public SFXGalaxyTemplatePair Planet;
        public SFXGalaxyTemplatePair PlanetCircle;
        public SFXGalaxyTemplatePair PlanetRing;
        public SFXGalaxyTemplatePair Object;
        public SFXGalaxyTemplatePair Arrow;
        public SFXGalaxyTemplatePair AsteroidBelt;
        public SFXGalaxyTemplatePair SystemSphere;
        public SFXGalaxyTemplatePair SystemCard1;
        public SFXGalaxyTemplatePair SystemCard2;
        public SFXGalaxyTemplatePair Emitter;
        public SFXGalaxyTemplatePair Sunlight;
        public SFXGalaxyTemplatePair Sun;
        public SFXGalaxyTemplatePair LensFlare;
        public SFXGalaxyTemplatePair MassRelay;
        public SFXGalaxyTemplatePair MassRelayRed;
        public SFXGalaxyTemplatePair MassRelayVFX;
        public SFXGalaxyTemplatePair FuelDepot;
        public SFXGalaxyTemplatePair Crosshair;
        public SFXGalaxyTemplatePair Camera;
        public SFXGalaxyTemplatePair ReaperArrow;
    }
    public class SFXGalaxyTemplates_Planet
    {
        public SFXGalaxyTemplatePair[] Nebulae;
        public SFXGalaxyTemplatePair[] Sunlight;
        public SFXGalaxyTemplatePair Planet;
        public SFXGalaxyTemplatePair PlanetSphere;
        public SFXGalaxyTemplatePair Corona;
        public SFXGalaxyTemplatePair Object;
        public SFXGalaxyTemplatePair Clouds;
        public SFXGalaxyTemplatePair Card;
        public SFXGalaxyTemplatePair PlanetRing;
        public SFXGalaxyTemplatePair BackgroundCloud;
        public SFXGalaxyTemplatePair Citadel;
        public SFXGalaxyTemplatePair Camera;
        public SFXGalaxyTemplatePair Scanner;
    }
    public class SFXGalaxyTemplates_SystemScanning
    {
        public SFXGalaxyTemplatePair ScanPulse;
        public SFXGalaxyTemplatePair ScanResult;
        public SFXGalaxyTemplatePair Reaper;
        public SFXGalaxyTemplatePair ReaperPing;
    }
    public class MissionReward
    {
        public NameReference MissionName;
        public int ObjectiveXP;
        public int CombatXP;
    }
    public class SFXSubtitleEntry
    {
        public string sSubtitle;
        public Color colFontColor;
        public float fTimeRemaining;
        public int pRefObject;
        public int pActor;
        public float fDelayStarting;
        public bool bAlert;
        public bool bHasPriority;
        public ESubtitlesRenderMode eRenderMode;
    }
    public class PlotStreamingElement
    {
        public NameReference ChunkName;
        public int Conditional;
        public bool bFallback;
    }
    public class PlotStreamingSet
    {
        public PlotStreamingElement[] Elements;
        public NameReference VirtualChunkName;
    }
    public class WorldStreamingState
    {
        public NameReference Name;
        public bool Enabled;
    }
    public class EffectsMaterialPriority
    {
        public NameReference EffectsMaterial;
        public int Priority;
    }
    public class SubPageState
    {
        public int nPadding;
        public MEBrowserWheelSubPages Page;
        public BioBrowserStates State;
    }
    public class SFXChoiceEntry
    {
        public SFXTokenMapping[] m_mapTokenIDToActual;
        public string sChoiceName;
        public string sChoiceTitle;
        public string sChoiceImageTitle;
        public string sChoiceDescription;
        public string sActionText;
        public NameReference WeaponClassRef;
        public NameReference WeaponModClassRef;
        public int srChoiceName;
        public int srChoiceTitle;
        public int oChoiceImage;
        public int srChoiceImageTitle;
        public int srChoiceDescription;
        public int nOptionalPaneItemValue;
        public int nChoiceID;
        public int srActionText;
        public bool bDefaultSelection;
        public bool bDisabled;
        public bool bNested;
        public bool bOptionalPaneHideCost;
        public SFXChoiceColors ChoiceColor;
        public EInventoryResourceTypes eResource;
        public EChoiceDisplayType eDisplayType;
    }
    public class PowerData
    {
        public int[] DynamicBonuses;
        public float RankBonuses;
        public float BaseValue;
        public float CurrentValue;
        public EPowerDataFormula Formula;
    }
    public class RankInfo2
    {
        public int Icon;
        public int Name;
        public int Description;
        public int Evolved1Name;
        public int Evolved1Description;
        public int Evolved2Name;
        public int Evolved2Description;
    }
    public class EvolvedChoiceInfo
    {
        public int Name;
        public int Description;
    }
    public class PowerStatBarInfo
    {
        public PowerData Data;
        public float EvolvedBonuses;
        public float BarLength;
        public int srDisplayTotalToken;
        public int srStatBarDisplayTitle;
        public EPowerStatBarFormula Formula;
    }
    public class AreaEffectParameters
    {
        public Vector ConeDirection;
        public Rotator HitDirectionOffset;
        public float ConeAngle;
        public bool ImpactFriends;
        public bool ImpactDeadPawns;
        public bool ImpactPlaceables;
        public bool BlockedByObjects;
        public bool DistancedSorted;
    }
    public class SavedPawnPowerData
    {
        public SavedPowerData[] Powers;
        public int Pawn;
        public int TalentPoints;
    }
    public class SavedPowerData
    {
        public EEvolveChoice[] EvolveChoices;
        public int Power;
        public int Rank;
    }
    public class BWPageStruct
    {
        public NameReference Tag;
        public int srLabel;
        public int oHandler;
        public MEBrowserWheelSubPages Type;
    }
    public class SFXUIControlState
    {
        public int Text;
        public bool Disabled;
        public ESFXGalaxyMapUIAction Action;
    }
    public class TemplateGenderPair
    {
        public NameReference UIWorldVar;
        public NameReference BuildClass;
        public int Placed;
        public int Spawned;
    }
    public class BonusTalentData
    {
        public NameReference PowerClassName;
        public int BonusPowerID;
        public int srChoiceName;
        public int srChoiceTitle;
        public int oChoiceImage;
        public int srChoiceDescription;
    }
    public class OptionText
    {
        public int Label;
        public int Story;
        public int Value;
    }
    public class OptionPage
    {
        public EGuiOptions[] Options;
        public int Label;
        public int Story;
    }
    public class TextSliderOption
    {
        public string TelemetryKey;
        public int AddTelemFunc;
        public OptionText[] Values;
        public string UClass;
        public int Label;
        public int Story;
        public int DefaultVal;
        public EGuiOptions Id;
    }
    public class RadioGroupOption
    {
        public string TelemetryKey;
        public int AddTelemFunc;
        public string UClass;
        public OptionText Value0;
        public OptionText Value1;
        public int Label;
        public int Story;
        public int DefaultVal;
        public EGuiOptions Id;
    }
    public class SliderOption
    {
        public string TelemetryKey;
        public int AddTelemFunc;
        public string UClass;
        public int Label;
        public int Story;
        public int Max;
        public int StepSize;
        public int DefaultVal;
        public EGuiOptions Id;
    }
    public class GamePopulatedOptionPage
    {
        public string PopulationFunc;
        public EGuiOptions Id;
    }
    public class OptionTelemetryInfo
    {
        public int Value_Initial;
        public int Value_ToSend;
        public EGuiOptions Id;
    }
    public class ModeAliasPair
    {
        public string Alias;
        public EGameModes GameMode;
    }
    public class SubordinateDesc : ModeAliasPair
    {
        public bool bAllGameModes;
    }
    public class GuiBind : ModeAliasPair
    {
        public KeyBind Keys;
        public string KeysLocName;
        public SubordinateDesc[] Subordinates;
        public int Name;
        public bool bCategory;
    }
    public class NonBindableKeyDefinition
    {
        public string Command;
        public NameReference Name;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool ModifierIndependent;
    }
    public class BioSFSoundAssetResource
    {
        public NameReference Tag;
        public int StartEvent;
        public int StopEvent;
    }
    public class SimpleDialogLine
    {
        public int srText;
        public int pCue;
    }
    public class SimpleVOEvent
    {
        public int[] Lines;
        public NameReference EventName;
        public int ReplyLine;
    }
    public class TurretConstraintData
    {
        public int PitchConstraint;
        public int YawConstraint;
        public int RollConstraint;
    }
    public class SquadTargetData
    {
        public Vector vLocation;
        public int oTarget;
        public int nActionIcon;
        public int nSquadIcon;
        public float fTimeOut;
        public bool bHidden;
        public bool bActive;
    }
    public class BioStageCamera
    {
        public BioStageDOFData tDOFData;
        public NameReference nmCameraTag;
        public float fFov;
        public float fNearPlane;
        public float fHeightDelta;
        public float fPitchDelta;
        public float fYawDelta;
        public bool bDisableHeightAdjustment;
    }
    public class TierDetails_t
    {
        public NameReference TierName;
        public Color Color;
        public int Priority;
        public bool IsEnabled;
        public bool IsFloor;
        public bool IsGlobal;
        public bool IsDynamic;
    }
    public class BioTimer
    {
        public int OnTimer;
        public string sTimerName;
        public int Params;
        public float fTickTime;
    }
    public class BioStreamingState
    {
        public NameReference StateName;
        public NameReference InChunkName;
        public NameReference[] VisibleChunkNames;
        public NameReference[] VisibleSoonChunkNames;
        public NameReference[] LoadChunkNames;
    }
    public class EnemyData
    {
        public Vector Direction;
        public int Enemy;
        public float Distance;
    }
    public class RvrCEParameterDistribution
    {
        public RawDistributionFloat DistributionFloat;
        public RawDistributionVector DistributionVector;
        public RvrClientEffectParameter Parameter;
        public bool bDistanceBased;
        public bool bNormalizeTime;
        public bool bSuppressDuringRegular;
        public bool bSuppressDuringCooldown;
        public EValueModifierOperation ValueModifierOperation;
        public EValueModifierSelection ValueModifierSelection;
    }
    public class RvrClientEffectSpawnedActor
    {
        public int[] MaterialInstances;
        public Vector Offset;
        public Rotator Rotation;
        public int Actor;
        public float SpawnTime;
        public float Lifetime;
        public float CopyDelay;
    }
    public class RvrClientEffectSavedAttachment
    {
        public BoneAtom[] Atoms;
        public Attachment Attachment;
        public int LOD;
    }
    public class RvrClientEffectSavedState
    {
        public BoneAtom[] Atoms;
        public RvrClientEffectSavedAttachment[] Attachments;
        public Vector location;
        public Rotator Rotation;
        public Vector Velocity;
        public Vector ComponentTranslation;
        public Rotator ComponentRotation;
        public Vector ComponentScale3D;
        public float ComponentScale;
        public int LOD;
        public float TimeStamp;
    }
    public class CEImpactByMaterial
    {
        public int ClientEffect;
        public EClientEffectMaterial MaterialType;
    }
    public class RvrClientEffectStack
    {
        public int[] Elements;
    }
    public class RvrClientEffectResource
    {
        public int Effect;
        public int Priority;
        public float TimeStamp;
    }
    public class RvrClientEffectList
    {
        public RvrClientEffectResource[] Resources;
        public int MaxEffects;
    }
    public class AchievementReward
    {
        public string Effect;
        public NameReference Name;
        public NameReference AccomplishmentName;
    }
    public class SFXAIPerceptionNoise
    {
        public NameReference NoiseType;
        public int NoiseMaker;
        public float Loudness;
    }
    public class SFXAPGDAnimData
    {
        public NameReference nmAnimSet;
        public NameReference nmAnimSeq;
        public int nPropActionIndex;
    }
    public class SFXAPGDTransition : SFXAPGDAnimData
    {
        public float fBlendTime;
        public int nPlayChance;
        public int nDestPoseIndex;
    }
    public class SFXAPGDGesture : SFXAPGDAnimData
    {
        public float fPlayRate;
        public float fPlayWeight;
        public int nPlayChance;
        public float fRetriggerDelay;
        public bool bOneShot;
        public bool bEnterEvent;
        public bool bExitEvent;
    }
    public class SFXAPGDPose : SFXAPGDAnimData
    {
        public SFXAPGDTransition[] aTrans;
        public SFXAPGDGesture[] aGests;
        public int nPoseChangeChance;
        public int nPlayGestureChance;
        public float fChoiceTimeDelay;
        public bool bEnterEvent;
        public bool bExitEvent;
        public bool bEnterTransDoneEvent;
    }
    public class SFXAFGDPropActionData
    {
        public NameReference nmActionName;
        public float fTimeDelay;
        public int pPartSys;
        public int pClientEffect;
    }
    public class MoveToIdleTransitionBlend
    {
        public NameReference ChildName;
        public float SyncGroupMin;
        public float SyncGroupMax;
        public float TransitionDelay;
        public float TransitionTime;
        public float AnimStartTime;
        public float BlendInTime;
        public float BlendOutTime;
    }
    public class SFXMapAssetData
    {
        public string Asset;
        public int GroupID;
        public SFXAreaMapLayout Floor;
    }
    public class SFXCharacterMapData
    {
        public PlotIdenfitier PlotId;
        public int srCharacter;
        public int srLocation;
        public int nValue;
        public int nConditional;
        public int nConditionalParam;
        public SFXAreaMapLayout Floor;
    }
    public class SFXMapLocationData
    {
        public int srLocation;
        public int nIndex;
        public SFXAreaMapLayout Floor;
    }
    public class SFXAsyncAssetRequest
    {
        public string FullAssetPath;
        public int AssetClass;
        public NameReference AltCookedPackageName;
        public int AssetReference;
    }
    public class SFXAsyncPackageRequest
    {
        public SFXAsyncAssetRequest[] AssetRequests;
        public NameReference PackageName;
        public NameReference AsyncLoadGroup;
    }
    public class SFXAsyncLoadGroupCallback
    {
        public int Callback;
        public NameReference AsyncLoadGroup;
    }
    public class SFXCameraNativeBaseTraceInfo
    {
        public Vector m_vCollVectorLocation;
        public Vector m_vCollVectorNormal;
        public int m_oCollVectorActor;
        public Color m_clrDebugDraw;
        public bool m_bCollDetected;
        public bool m_bCollisionDirty;
        public bool m_bDebugDraw;
    }
    public class PowerStartingRank
    {
        public int PowerClass;
        public float Rank;
    }
    public class PowerAutoLevelUp
    {
        public int PowerClass;
        public float Rank;
        public int EvolvedChoice;
    }
    public class SecondaryTargetData
    {
        public CoverInfo TargetCoverInfo;
        public int FireTarget;
        public int PawnTarget;
    }
    public class FriendlyLOFData
    {
        public Vector Source;
        public Vector Target;
    }
    public class TimelineEffect
    {
        public InterpCurveFloat TimeDilation;
        public EReactionTypes[] Reactions;
        public int AOEFunc;
        public string InputAlias;
        public int InputHandle;
        public int RumbleClass;
        public int ScreenShakeClass;
        public int DamageType;
        public int AOEFilterClass;
        public int GameEffectClass;
        public ScreenShakeStruct ScreenShake;
        public Guid ClientEffectID;
        public Rotator PS_Rotation;
        public NameReference SocketName;
        public NameReference Func;
        public float TimeIndex;
        public float TimeRemaining;
        public int PSC_Instance;
        public int RBC_BlurInstance;
        public int PS_Template;
        public float CrustDuration;
        public int Sound;
        public int PlayerSound;
        public int Rumble;
        public int ScreenShakeObject;
        public float TimeDilationLength;
        public float RagdollForce;
        public float Damage;
        public float AOERadius;
        public float AOEConeAngle;
        public int AOEImpactTimeline;
        public int SyncPartnerImpactTimeline;
        public int TimelineTemplate;
        public int nMatchedInputIndex;
        public int BlurMaterial;
        public float BlurScale;
        public float BlurFalloffExponent;
        public float BlurOpacity;
        public int CamAnim;
        public float CamStartTime;
        public float CamBlendInTime;
        public float CamBlendOutTime;
        public float CamPlayRate;
        public float CamDuration;
        public int RVR_CrustTemplate;
        public int CEStartIndex;
        public float GameEffectDuration;
        public float GameEffectValue;
        public bool bActivated;
        public bool bActiveInput;
        public bool bReceivedInput;
        public bool bUseWeaponMesh;
        public bool bApplyBloodColorParam;
        public bool bDilateSound;
        public bool bAOEAffectsTarget;
        public bool bOnPress;
        public bool bExclusive;
        public bool bBufferedInput;
        public bool bLoopCamAnim;
        public bool bCEAllowCooldown;
        public bool bCEStopAllMatching;
        public ETimelineType Type;
        public ETimelineTarget TargetType;
        public ESFXVocalizationEventID VocID;
        public EBioPartGroup Constraint;
        public ETimelineAOEType AOEType;
    }
    public class AbilityDifficultyData
    {
        public NameReference StatName;
        public Vector2D StatRange;
        public bool bStatActive;
    }
    public class DifficultySettings
    {
        public AbilityDifficultyData[] CategoryData;
        public NameReference Category;
    }
    public class ClientEffectWithGUID
    {
        public Guid EffectGUID;
        public int EffectInterface;
    }
    public class SFXDuringAsyncWorkQueuedImpactPSC
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public Vector VectorParameter;
        public NameReference HitBoneName;
        public NameReference VectorParameterName;
        public int Template;
        public int HitActor;
        public int HitComponent;
        public float Scale;
        public int Instigator;
    }
    public class SFXDuringAsyncWorkQueuedImpactDecal
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public NameReference HitBoneName;
        public int Material;
        public float FadeTime;
        public float Width;
        public float Height;
        public float FarPlane;
        public int HitComponent;
        public int HitItem;
        public int HitLevelIndex;
        public int Instigator;
        public bool bNoClip;
    }
    public class SFXDuringAsyncWorkQueuedTracer
    {
        public Vector TracerScale3D;
        public Vector StartLocation;
        public Vector HitLocation;
        public int TracerMesh;
        public int TracerVFX;
        public float TracerSpeed;
        public float TracerSpawnOffset;
        public int Instigator;
    }
    public class SFXDuringAsyncWorkQueuedEffect
    {
        public Vector location;
        public Rotator Rotation;
        public int Effect;
        public float Lifetime;
        public float Scale;
        public int Instigator;
    }
    public class SFXDuringAsyncWorkCachedInfo
    {
        public Vector LocalPlayerLocation;
        public float LODfactor;
        public int LocalPlayer;
        public float TimeSeconds;
    }
    public class DecayedCover
    {
        public int CoverMarker;
        public int ExtraCoverCost;
    }
    public class ReputationThreshold
    {
        public int PlotStateID;
        public int Threshold;
    }
    public class SFXChoiceEntryNoStrRef
    {
        public string sChoiceName;
        public string sChoiceTitle;
        public string sChoiceImageTitle;
        public string sChoiceDescription;
        public string sActionText;
        public SFXTokenMapping[] m_mapTokenIDToActual;
        public int oChoiceImage;
        public int nOptionalPaneItemValue;
        public int nChoiceID;
        public bool bDefaultSelection;
        public bool bDisabled;
        public bool bNested;
        public SFXChoiceColors ChoiceColor;
        public EInventoryResourceTypes eResource;
    }
    public class TechData
    {
        public SFXChoiceEntry ChoiceEntry;
        public STech stTech;
        public STreasure stTreasure;
        public NameReference PlotName;
        public int TechId;
        public int PlotId;
        public int TreasureId;
        public int UnlockId;
        public int nContextId;
        public int RMode;
        public int DisableId;
    }
    public class TreasureBudget
    {
        public int LevelId;
        public int Credits;
        public int Eezo;
        public int Palladium;
        public int Platinum;
        public int Iridium;
    }
    public class PurchasableItem
    {
        public string className;
        public int Id;
    }
    public class UniqueArmorEffects
    {
        public int childClass;
        public float Value;
    }
    public class BioProcFoleyData
    {
        public float m_fMaxThreshold;
        public float m_fSmoothingFactor;
        public bool m_bStart;
    }
    public class BioMicLockData
    {
        public NameReference m_nmFindActor;
        public bool m_bLock;
        public ESFXFindByTagTypes m_eFindActorMode;
    }
    public class GAWIntelRewardInfo
    {
        public NameReference UniqueName;
        public float Value;
        public EGAWAssetType Type;
        public EGAWAssetSubType SubType;
    }
    public class EndGameOptionSet
    {
        public EEndGameOption[] Brain;
        public EEndGameOption[] Heart;
        public float Threshold;
    }
    public class EndGameOption
    {
        public int[] PlotStates;
        public EEndGameOption Option;
    }
    public class CutscenePlotState
    {
        public int PlotStateID;
        public int BrainThreshold;
        public int HeartThreshold;
    }
    public class GAWZoneData
    {
        public int srZoneName;
        public int srZoneDescription;
        public int ZoneDisplayNumber;
        public EGAWZone ZoneID;
    }
    public class GAWZoneGUIData
    {
        public string ZoneName;
        public string ZoneDescription;
        public int CurrentRating;
        public int ZoneDisplayNumber;
        public EGAWZone ZoneID;
    }
    public class WarAssetSummaryWithThreshold
    {
        public int Summary;
        public int Threshold;
    }
    public class AccomplishmentUIData
    {
        public string AccomplishmentName;
        public string Title;
        public string Description;
        public string IconTextureRef;
        public string ParentName;
        public string SecondDescription;
        public int nIndex;
        public int nPoints;
        public int nInitialValue;
        public int nFinalValue;
        public int nSecondInitialValue;
        public int nSecondFinalValue;
        public bool bIsCompleted;
        public bool bIsGrinder;
        public bool bIsDoubleGrinder;
    }
    public class SFXCreditEntry
    {
        public int Title;
        public int Names;
        public float FontScale;
        public int Columns;
        public float StartTime;
        public float FadeInTime;
        public float HoldTime;
        public float FadeOutTime;
        public float DelayTime;
        public float BreakSpace;
        public ECreditEntryType Type;
    }
    public class ElevatorDestinationData
    {
        public string LargeImage;
        public string SmallImage;
        public int DestId;
        public int DestTitle;
        public int DestSubTitle;
        public int DestDesc;
        public int PlotUnlockID;
        public int AButtonText;
        public int BButtonText;
    }
    public class JCItem
    {
        public string sName;
        public int nID;
        public int srDesc;
        public bool bUpdated;
    }
    public class JCEntry : JCItem
    {
        public Pointer pCodexEntry;
        public int nQuestAdded;
        public bool bQuestComplete;
    }
    public class JCUIListItem
    {
        public string sName;
        public int nIndex;
        public int nID;
        public bool bComplete;
        public bool bUpdated;
        public bool bHasSubItems;
    }
    public class CodexImageDetails
    {
        public string sName;
        public int nID;
        public int oTexture;
    }
    public class SFXLeaderboardRequestData
    {
        public int PrimaryIndex;
        public int SecondaryIndex;
    }
    public class RatingThresholdMessage
    {
        public int nStart;
        public int nEnd;
        public int srMessage;
    }
    public class MMListEntry
    {
        public string ListID;
        public string FncOnSelect;
        public string FncActiveConditional;
        public string FncNotifyConditional;
        public string KmtOnSelect;
        public int Label;
        public bool ShowNotification;
    }
    public class MMListSequences
    {
        public string SeqID;
        public string ListID;
        public string FncOnEntry;
        public int SeqStep;
        public int Label;
    }
    public class ManualListItem
    {
        public string sName;
        public string sListTag;
        public int nIndex;
        public int nID;
    }
    public class ManualCategory
    {
        public int[] Chapters;
    }
    public class ManualChapter
    {
        public string Image;
        public NameReference FncChapterExclusion;
        public int Id;
        public int ChapterNum;
        public int Title;
        public int Body;
        public int ConsoleBody;
    }
    public class ManualPage
    {
        public string Image;
        public int Id;
        public int Chapter;
        public int PageNum;
        public int Title;
        public int Body;
        public int ConsoleBody;
    }
    public class SFXGUIScoreTag
    {
        public string Text;
    }
    public class LanguageOptionInfo
    {
        public string Code;
        public int Label;
    }
    public class CharDetails
    {
        public string CharName;
        public string Face;
        public string Abbrev;
        public string Thumb;
        public string XP;
        public string PrettyLevel;
        public string ShieldTitle;
        public int CharClass;
        public int allocated;
        public int Spendable;
        public int Level;
        public int PctToLevel;
        public int Health;
        public int Shield;
        public int Pgn;
        public int Rng;
    }
    public class PowerDetails
    {
        public string Name;
        public string Desc;
        public string Resource;
        public int State;
        public int IconFrame;
    }
    public class EvoDetails
    {
        public string Name;
        public string Desc;
        public int State;
        public int Cost;
    }
    public class WeaponModEffect
    {
        public NameReference EffectClassName;
        public int Level;
        public float EffectValue;
    }
    public class WeaponModStatConversion
    {
        public NameReference ModEffectClass;
        public float ConversionMultiplier;
    }
    public class SFXUIDataResource
    {
        public string Resource;
        public NameReference Package;
    }
    public class GlobalStoreDiscount
    {
        public string PlayerVariable;
        public float DiscountStrength;
    }
    public class SFXWeaponModUIStat
    {
        public int Level;
        public float Value;
        public EWeaponStatBars Type;
    }
    public class SFXWeaponModData
    {
        public string ClassPath;
        public SFXWeaponModUIStat[] Stats;
        public int[] ModLevelTokens;
        public string CookedPackage;
        public string[] Meshes;
        public NameReference className;
        public NameReference SocketName;
        public int Name;
        public int ShortName;
        public int Description;
        public int Image;
        public int LargeImage;
        public bool MaterialEmissiveChange;
        public bool MaterialGripColorChange;
        public bool MaterialBodyColorChange;
        public EWeaponModCategory ModCategory;
    }
    public class ModStrings
    {
        public string className;
        public float[] Custom0Tokens;
        public float[] Custom1Tokens;
        public int srModName;
        public int srModDescription;
    }
    public class ComparisonStat
    {
        public string StatName;
        public int StatBaseValue;
        public int StatBonusValue;
        public int StatCompValue;
    }
    public class SFXWeaponUICookData
    {
        public string CookedPackage;
        public string Mesh;
        public string AnimTree;
        public string[] AnimSets;
    }
    public class SFXWeaponSelectWeaponData
    {
        public SFXWeaponUICookData CookData;
        public string ClassPath;
        public LinearColor[] WeaponModGripColors;
        public LinearColor[] WeaponModBodyColors;
        public LinearColor[] WeaponModEmissiveValues;
        public LoadoutWeaponInfo LoadoutInfo;
        public SFXWeaponUIStats Stats;
        public NameReference className;
        public int Name;
        public int Description;
        public int ShortDescription;
        public int Image;
        public int IconResource;
        public int IconIndex;
        public float EncumbranceWeight;
        public ELoadoutWeapons Type;
    }
    public class SFXWeaponUIStats
    {
        public float Accuracy;
        public float Damage;
        public float FireRate;
        public float Magazine;
        public float Weight;
    }
    public class StoreItemData
    {
        public SFXChoiceEntry ChoiceEntry;
        public string ItemClassName;
        public int[] PlotPurchaseID;
        public string LargeImage;
        public string SmallImage;
        public string[] PVsToIncrement;
        public int[] CustomTokens;
        public int[] ItemConditionals;
        public int BaseCost;
        public int PlotUnlockID;
        public int PlotUnlockConditionalID;
        public int ArmorID;
        public int PlotPurchaseInt;
        public float Priority;
        public int CurrentModRank;
        public float Value;
        public bool bIsGameEffect;
        public bool bBuffsGAWAssets;
        public EItemType ItemType;
    }
    public class SFXInputEventCooldownStruct
    {
        public float fCooldown;
        public BioGuiEvents EventId;
    }
    public class SFXGUIMovieData
    {
        public string MovieClass;
        public string GFxResource;
        public string HUDTypeToAutoLoadFor;
        public NameReference Tag;
        public float CurvePixelError;
        public int ZOrder;
        public bool UseEdgeAA;
        public bool bAutoStart;
        public bool bAutoVisible;
        public bool bSwfDevAutoReopen;
        public EConsoleType Platform;
        public SFMovieStrokeStyle StrokeStyle;
    }
    public class SFXSharedAssetMap
    {
        public NameReference SharedFile;
        public NameReference GFxResource;
    }
    public class PowerInfo
    {
        public NameReference PowerName;
        public int PowerInfoID;
        public int DisplayName;
        public int Description;
    }
    public class SelectInfo
    {
        public NameReference MemberTag;
        public int InfoId;
        public int Ability1ID;
        public int Ability2ID;
        public int Ability3ID;
        public int Ability4ID;
        public int Ability5ID;
    }
    public class SFXFontMap
    {
        public string Locale;
        public string FontExportName;
        public string SubstituteFont;
        public NameReference FontGFxResource;
        public float ScaleFactor;
        public SFXFontStyle Style;
    }
    public class SFXSFControlToken
    {
        public string TexturePath;
        public string Resource;
        public string Align;
        public string VAlign;
        public NameReference token;
        public int Height;
        public int Width;
        public int FontVScale;
        public int VSpace;
        public bool Cook;
    }
    public class SFXControlTokenAlias
    {
        public NameReference From;
        public NameReference To;
    }
    public class SFXKeyNameControlToken
    {
        public NameReference Key;
        public NameReference token;
    }
    public class SFXStringMap
    {
        public int From;
        public int To;
    }
    public class BioMessageBoxData
    {
        public string m_sMessage;
        public BioMessageBoxOptionalParams m_stParams;
        public NameReference m_nmName;
        public NameReference m_nmCallbackFunction;
        public int m_nPriority;
        public int m_pCallbackObject;
        public int m_nContext;
        public int m_nControllerId;
        public bool m_bPersistThroughTravel;
        public bool m_bWasGamePaused;
        public bool m_bIsWeaponChoiceDlg;
    }
    public class PlayerGuiData
    {
        public BioMessageBoxData[] aMessageBoxQueue;
    }
    public class GUIDependency
    {
        public int OnDependency;
        public NameReference SourceGUI;
        public NameReference DependentGUI;
        public int OptContext;
    }
    public class AppearanceSet
    {
        public string HighlightImage;
        public string AvailableImage;
        public string DeadImage;
        public string SilhouetteImage;
        public int[] DescriptionText;
        public int[] CustomToken0;
        public NameReference MemberTag;
        public NameReference MemberAppearancePlotLabel;
        public int AppearanceId;
        public int MemberAppearanceValue;
        public int PlotUnlockCID;
    }
    public class SelectIdentity
    {
        public NameReference MemberTag;
        public NameReference MemberInPartyPlotLabel;
        public NameReference MemberAvailablePlotLabel;
        public int MemberId;
        public int MemberName;
        public int MemberDossier;
        public int MemberValidCID;
        public int MemberDeadPlotID;
    }
    public class TerminalItemData
    {
        public string LargeImage;
        public string SmallImage;
        public int ItemTitle;
        public int ItemDesc;
        public int PlotUnlockID;
        public int AButtonText;
        public int BButtonText;
    }
    public class WarAssetCategoryGUIData
    {
        public string CategoryName;
        public string CategoryStrength;
        public int categoryId;
        public bool bHasNewItems;
    }
    public class WarAssetGUIData
    {
        public string AssetName;
        public string AssetStrength;
        public int AssetID;
        public bool bIsNewItem;
    }
    public class SFXWeaponUIPawnPositioning
    {
        public Rotator RotationOffset;
        public Vector PositionOffset;
        public NameReference Tag;
    }
    public class WeaponStatesToKeep
    {
        public NameReference WeaponClassName;
        public NameReference AmmoPowerName;
        public NameReference AmmoPowerSourceTag;
        public int Pawn;
        public float CurrentSpareAmmo;
        public float AmmoUsedCount;
    }
    public class ModAttachSoundInfo
    {
        public NameReference location;
        public NameReference Sound;
    }
    public class SFXPowerWheelButtonIcon
    {
        public string sPath;
        public SFXPowerWheelMapButtonIcon eIcon;
    }
    public class WeaponModMeshOverride
    {
        public NameReference[] SocketOverrideNames;
        public NameReference SocketName;
    }
    public class CoverLeanPosition
    {
        public WeaponAnimType[] WeaponTypes;
        public Vector Offset;
        public ECoverDirection Direction;
        public ECoverType Type;
    }
    public class WeaponModInfo
    {
        public int ModClass;
        public int ModLevel;
    }
    public class ExtraMeshComponent
    {
        public int[] ExtraMeshes;
        public int Mod;
    }
    public class WaveEventInfo
    {
        public int HordeWaveType;
        public int Trigger;
        public int WaveNumber;
        public int HordeWaveIndex;
        public int OperationWaveIndex;
        public int SupplyDropWaveIndex;
        public bool WavesAreLoading;
        public EWaveCoordinator_HordeOpEvent WaveEvent;
    }
    public class SFXAttachCrustEffectTrackData
    {
        public float m_fLifeTime;
        public bool m_bAttach;
    }
    public class SFXBlackScreenTrackData
    {
        public int PlaceHolder;
        public BlackScreenActionSet BlackScreenState;
    }
    public class SFXLightEnvTrackData
    {
        public int PlaceHolder;
        public EDLEStateType Quality;
    }
    public class SFXMoviePlayStateData
    {
        public int PlaceHolder;
        public EMoviePlayState m_eState;
    }
    public class BioFOVOTrackData
    {
        public int pConversation;
        public int nLineStrRef;
        public int srActorNameOverride;
        public bool bForceHideSubtitles;
        public bool bPlaySoundOnly;
        public bool bDisableDelayUntilPreload;
        public bool bAllowInConversation;
        public bool bSubtitleHasPriority;
    }
    public class SFXNearClipTrackData
    {
        public float m_fValue;
        public bool m_bUseDefaultValue;
    }
    public class SFXWeaponClassData
    {
        public int cWeapon;
    }
    public class SFXToggleTrackData
    {
        public bool m_bToggle;
        public bool m_bEnable;
    }
    public class LoadingTip
    {
        public int GenericTip;
        public int PCOverrideTip;
        public int PS3OverrideTip;
        public int XBoxOverrideTip;
    }
    public class LoadingLevelTip
    {
        public string LevelName;
        public LoadingTip Tip;
        public bool UseRandomTip;
    }
    public class NativeLoadingMovie
    {
        public string Filename;
        public NameReference Tag;
        public int AudioEventPair;
        public int LoopBackFrame;
        public float MinPlayTime;
        public float FadeInTime;
        public float FadeOutTime;
        public int FadeOutAudioEvent;
    }
    public class PlayerScoreData
    {
        public UniqueNetId UniqueId;
        public string PlayerName;
        public int[] PlayerMedalIDs;
        public NameReference KitName;
        public int PlayerID;
        public float fScore;
        public int ClassLevel;
        public int nTotalXP;
    }
    public class PlayerRewardData
    {
        public float fOriginalExperience;
        public float fNewExperience;
    }
    public class MatchData
    {
        public int MapId;
        public int ZoneRatingIncrease;
        public int EnemyID;
        public int DifficultyID;
        public int Waves;
        public int TotalMatchTime;
        public int OverallRatingIncrease;
        public bool bResult;
        public EGAWZone ZoneID;
    }
    public class BioWoundSpec
    {
        public Matrix m_mWoundEllipse;
        public BoxSphereBounds HitBox;
        public NameReference m_nmPart;
        public NameReference HitBoxBone;
        public int m_pWoundModel;
        public int m_pBloodTexture;
        public int m_pEffect;
        public float m_fEffectDuration;
        public float m_fEffectRadius;
        public EWoundSeverity m_eWoundSeverity;
    }
    public class BioWrinkleConfig
    {
        public string WrinkleParameterName;
        public int WrinkleTexture;
    }
    public class IntVariablePair
    {
        public int Index;
        public int Value;
    }
    public class FloatVariablePair
    {
        public int Index;
        public float Value;
    }
    public class PlotQuest
    {
        public int[] History;
        public int QuestCounter;
        public int ActiveGoal;
        public bool bQuestUpdated;
    }
    public class PlotCodexPage
    {
        public int Page;
        public bool bNew;
    }
    public class PlotCodex
    {
        public PlotCodexPage[] Pages;
    }
    public class PlotTableSaveRecord
    {
        public int[] BoolVariables;
        public IntVariablePair[] IntVariables;
        public FloatVariablePair[] FloatVariables;
        public PlotQuest[] QuestProgress;
        public int[] QuestIDs;
        public PlotCodex[] CodexEntries;
        public int[] CodexIDs;
        public int QuestProgressCounter;
    }
    public class PlayerVariableSaveRecord
    {
        public string VariableName;
        public int VariableValue;
    }
    public class ObjectiveMarkerSaveRecord
    {
        public string MarkerOwnerPath;
        public Vector MarkerOffset;
        public NameReference BoneToAttachTo;
        public int MarkerLabel;
        public EObjectiveMarkerIconType MarkerIconType;
    }
    public class SaveTimeStamp
    {
        public int SecondsSinceMidnight;
        public int Day;
        public int Month;
        public int Year;
    }
    public class PowerRecord
    {
        public int EvolvedChoices;
        public NameReference PowerName;
        public NameReference PowerClassName;
        public float CurrentRank;
        public int WheelDisplayIndex;
        public bool bUsesTalentPoints;
    }
    public class WeaponRecord
    {
        public NameReference WeaponClassName;
    }
    public class WeaponModRecord
    {
        public NameReference[] WeaponModClassNames;
        public NameReference WeaponClassName;
    }
    public class SFXObjectiveSpawnLocationSize
    {
        public Vector Extents;
        public EObjectiveLocation Type;
    }
    public class SFXObjectPoolTracers
    {
        public int[] Tracers;
        public int Mesh;
        public int Template;
        public int NextIdx;
    }
    public class SFXObjectPoolProjectiles
    {
        public int[] Projectiles;
        public int ProjectileClass;
        public int NextIdx;
    }
    public class SFXObjectPoolDroppedAmmos
    {
        public int[] DroppedAmmos;
        public int DroppedAmmoClass;
        public int NextIdx;
    }
    public class SFXObjectPoolImpactPSCs
    {
        public int[] PSysComponents;
        public int Template;
        public int NextIdx;
    }
    public class SFXObjectPoolPSCs
    {
        public int[] PSysComponents;
        public int Template;
        public int NextIdx;
    }
    public class SFXObjectPoolEmitters
    {
        public int[] Emitters;
        public int Template;
        public int NextIdx;
    }
    public class WriteEvent
    {
        public int[] DelayedWriteDelegates;
        public int LocalUserNum;
        public float LastWriteTimestamp;
    }
    public class MPTutorialPromoMessage
    {
        public string ImageURL;
        public int TrackingID;
        public int offerId;
        public int MessageTitle;
        public int MessageText;
    }
    public class MPClassData
    {
        public NameReference className;
        public int srDisplayName;
        public int srDescription;
    }
    public class MPKitData
    {
        public string KitTextureRef;
        public string LockedKitTextureRef;
        public string SmallKitTextureRef;
        public string ArchetypeRef;
        public string PowerIconResource;
        public int[] RequiredDLCModuleIDs;
        public NameReference KitName;
        public NameReference BaseMPClassName;
        public int srDisplayName;
        public int srDefaultName;
        public int PowerIconIndex1;
        public int PowerIconIndex2;
        public int PowerIconIndex3;
        public int srPowerName1;
        public int srPowerName2;
        public int srPowerName3;
        public int MaxNewUnlockLevel;
        public bool bLockedByDefault;
        public bool bPermanentlyLocked;
        public bool bUsePrimaryColor;
        public bool bUseSecondaryColor;
        public bool bUsePattern;
        public bool bUsePatternColor;
        public bool bUsePhong;
        public bool bUseEmissive;
        public bool bUseSkinTone;
        public bool bHideIfLocked;
    }
    public class MPFaceCodeData
    {
        public string firstName;
        public string faceCode;
        public int Id;
    }
    public class StartingMPPlayerVariable
    {
        public string PlayerVariable;
        public int Value;
    }
    public class NewReinforcementData
    {
        public string VariableName;
        public EReinforcementGUICategory Category;
    }
    public class PendingSaveOperation
    {
        public int SaveDelegate;
        public bool ForceSaveBase;
    }
    public class PendingLoadOperation
    {
        public int LoadDelegate;
    }
    public class ActiveMatchConsumable
    {
        public int ClassNameID;
        public float Value;
    }
    public class SFXOperation_ObjectiveMeshInfo
    {
        public string UniqueString;
        public string MeshPath;
        public Vector Translation;
        public Rotator Rotation;
        public int GameName;
        public int MeshVOLine;
        public float Scale;
        public EObjectiveLocation SpawnLocation;
        public ETargetTipText TipText;
    }
    public class EnemyCoverInfo
    {
        public CoverInfo Cover;
        public int Enemy;
    }
    public class NavWeight
    {
        public float[] ConstraintWeights;
        public int Nav;
        public float Weight;
        public int FailedIndex;
    }
    public class DeathInfo
    {
        public int DamageType;
        public int KillerPawn;
        public int LastDamageSource;
    }
    public class PowerImpactNotification
    {
        public NameReference Label;
        public float TimeBeforeImpact;
    }
    public class ScoreRecord
    {
        public int Player;
        public float TotalDamage;
        public float TotalPowerAssistValue;
        public float LastScoreTime;
        public int LastScoreSourceName;
    }
    public class ReplicatedGib
    {
        public int DamageType;
        public Vector HitLocation;
        public Vector HitNormal;
        public int BoneIndex;
    }
    public class CompositeSourceMeshes
    {
        public int[] Parts;
        public int BaseMesh;
    }
    public class HelmetMetaData
    {
        public bool bHidesHead;
        public bool bHidesHair;
        public bool bAffectsVO;
    }
    public class PermanentGameEffect
    {
        public string UniqueName;
        public string className;
        public float Value;
        public EPermanentGameEffect_Type Type;
        public EGAWAssetType GAWAssetType;
        public EGAWAssetSubType GAWAssetSubType;
    }
    public class ArmorEffectDescription
    {
        public string ArmorEffect;
        public int[] EffectDescription;
        public string[] EffectToken;
    }
    public class DelayedPowerComboData
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public int ComboEffect;
        public int TargetPawn;
    }
    public class EvolvedSoundStruct
    {
        public int Sound;
        public bool bAnyEvolved;
        public bool bReplaceBaseSound;
        public EEvolveChoice EvolveChoice;
    }
    public class RankInfo
    {
        public int Icon;
        public int Name;
        public int Description;
        public int UnlockBlurb;
    }
    public class UnlockRequirement
    {
        public int PowerClass;
        public float Rank;
        public int CustomUnlockText;
    }
    public class SFXPreAsyncWorkQueuedShot
    {
        public ImpactInfo Impact;
        public int Weapon;
        public int NumHits;
        public int FrameCount;
        public bool bSuppressAudio;
        public byte FiringMode;
    }
    public class SFXPreAsyncWorkQueuedPowerImpact
    {
        public int ImpactCallback;
        public int DamageType;
        public int MaxRagdollDmgTypeOverride;
        public AreaEffectParameters Params;
        public Vector HitLocation;
        public Vector HitNormal;
        public int Power;
        public int Target;
        public float Damage;
        public int MaxRagdollOverride;
        public float Force;
        public int ImpactCount;
        public int Projectile;
        public int FrameCount;
        public bool bAreaExplosion;
        public bool bFirstTarget;
    }
    public class ScoreEvent
    {
        public string Text;
        public float ExpiryTime;
    }
    public class ScoreInfo
    {
        public float Score;
        public float Credits;
        public byte Trigger;
    }
    public class ReplicatedInit
    {
        public Vector Direction;
        public Vector location;
        public float Speed;
        public int Instigator;
        public byte Trigger;
    }
    public class ReplicatedExplosion
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public byte Trigger;
    }
    public class ReplicatedStick
    {
        public Vector HitLocation;
        public Vector HitNormal;
        public int StuckActor;
        public int BoneIndex;
        public int Reaction;
        public byte Trigger;
    }
    public class ReplicatedPowerProjInit
    {
        public Vector location;
        public Vector Direction;
        public int Caster;
        public int TargetActor;
        public float TravelSpeed;
        public float Radius;
        public int Power;
        public byte Trigger;
    }
    public class SFXSSPlotValue
    {
        public string sPinName;
        public float fValue;
    }
    public class DifficultyScoreMultiplier
    {
        public float Multiplier;
        public EDifficultyOptions Difficulty;
    }
    public class CreditBudget
    {
        public int Player;
        public int CreditsRewarded;
    }
    public class PlayerMedalRecord
    {
        public int[] TrackingCounts;
        public int Player;
    }
    public class MedalDefinition
    {
        public string Icon;
        public int MedalName;
        public int Threshold;
        public int Score;
        public int ReplacesIdx;
        public MPMedalType Type;
    }
    public class AISpawnClusterTracker
    {
    }
    public class AISpawnInfo
    {
        public int[] Types;
        public int[] SpawnPoints;
        public string[] VarLinkDescs;
        public string AutoDebugText;
        public int ObscuredSpawnProjectileClass;
        public Vector ObscuredSpawnOffset;
        public NameReference ActorTag;
        public float ClusterVisibilityDelay;
        public int SpawnTotal;
        public int MaxAlive;
        public int SpawnedCount;
        public int SpawnPointIdx;
        public float MaxSpawnDelay;
        public float MinSpawnDelay;
        public float CurrentDelay;
        public int TeamIdx;
        public int Squad;
        public bool bAutoAcquireEnemy;
        public bool bAutoNotifyEnemy;
        public bool bDisableFriendlyNotifications;
        public bool bDisableAI;
        public bool bCanDropAmmo;
        public bool bDisableShadowCasting;
        public ELightShadowMode ShadowMode;
    }
    public class MissionScore
    {
        public NameReference MissionName;
        public float Value;
    }
    public class DummyFireObjectListParams
    {
        public Vector2D SecondsPerObject;
        public Vector2D ObjectChangeDelay;
        public float TimeUntilObjectChange;
        public float DelayTimeRemaining;
        public int CurrentObjIdx;
        public bool bDelay;
        public DummyFireObjectCyclingMethod CyclingMethod;
    }
    public class ResearchMenu
    {
        public string sImagePath;
        public int Index;
        public int srTitle;
        public int srSubTitle;
        public int srAboutLabel;
        public int srAboutText;
    }
    public class SFXConvActorVar
    {
        public NameReference nmPinName;
        public int pActor;
        public int pSeqVar;
    }
    public class WheelInfo
    {
        public Vector LocationOffset;
        public Rotator RotationOffset;
        public Vector LastFramePosition;
        public Rotator TravelRotation;
        public int Wheel;
        public int GroundLevelIndicator;
        public float Radius;
        public bool bRightWheel;
        public bool bCanTurn;
    }
    public class SFXHudDmgIndicatorPaths
    {
        public string _alpha;
        public string _visible;
    }
    public class SFXHUDResistances
    {
        public float fHealthPct;
        public float fArmourPct;
        public float fBioticPct;
        public float fShieldPct;
        public bool bHasShield;
        public bool bHasArmour;
        public bool bHasBiotic;
        public bool bHasHealth;
    }
    public class SFXHUDSquadMemberInfo
    {
        public string sPath;
        public string sIconImagePath;
        public string sShieldPath;
        public string sBioticPath;
        public string sArmourPath;
        public string sHealthPath;
        public string sPowerPath;
        public SFXHUDResistances Resistances;
        public SFXHUDResistances DisplayedResistances;
        public int pPawn;
        public int pIcon;
        public float fCooldown;
        public float fElapsedFullResistTime;
        public int pPowerIcon;
        public bool bCooldownVisible;
        public bool bShieldVisible;
        public bool bBioticVisible;
        public bool bArmourVisible;
        public bool bHealthVisible;
        public bool bShieldDamage;
        public bool bBioticDamage;
        public bool bArmourDamage;
        public bool bHealthDamage;
        public bool bInvalidated;
        public bool bVisible;
        public bool bUpdateResistance;
        public bool bUpdateHealth;
        public bool bUpdatePower;
        public bool bUpdateIcon;
        public bool bDisplayVisible;
        public ESFXPortraitState ePortraitState;
    }
    public class SFXHUDTargetInfo
    {
        public string sName;
        public string sStatus;
        public SFXHUDResistances Resistances;
        public int nStatusFlags;
        public bool bInteractive;
        public bool bHostile;
        public bool bInRange;
    }
    public class SFXHUDNotification
    {
        public int oMovieClip;
        public int nID;
        public float fTimeToLive;
        public bool bVisible;
    }
    public class SFXHUDMiniNotification : SFXHUDNotification
    {
        public string sText;
        public NameReference nmIcon;
        public float fAnimTime;
    }
    public class SFXMPTargetUIState
    {
        public Pointer[] aBarPips;
        public int oResistBar;
        public int nNumVisible;
    }
    public class SaveGUIAreaInfo
    {
        public string ImageName;
        public NameReference AreaName;
        public int AreaStrRef;
    }
    public class SaveGUIRecord
    {
        public SFXSaveDescriptor SaveDescriptor;
        public string FriendlyName;
        public string ImagePath;
        public int SaveGame;
        public int AreaImage;
    }
    public class SaveGUICareerRecord
    {
        public string CareerName;
        public string firstName;
        public string className;
        public SFXSavePair[] CareerSaves;
        public SaveTimeStamp CreationDate;
        public int DeviceID;
        public bool bActiveCareer;
        public EOriginType Origin;
        public ENotorietyType Notoriety;
    }
    public class TintSwatchData
    {
        public int SwatchID;
        public LinearColor SwatchColor;
    }
    public class SFXPowerWheelIcon
    {
        public string sPath;
        public string sID;
        public float fBoundary;
        public bool bHenchIcon;
    }
    public class SFXPowerWheelIconWeapon : SFXPowerWheelIcon
    {
        public string sName;
        public string sPawnName;
        public string sDescription;
        public string sIconResource;
        public int oWeaponClass;
        public int nWeaponIcon;
        public int nAmmo;
        public int oIconMC;
        public bool bEquipped;
        public SFXPowerWheelWeaponState eWeaponState;
    }
    public class SFXPowerWheelPawnIndices
    {
        public int[] aPlayer;
        public int[] aHench1;
        public int[] aHench2;
    }
    public class SFXRadarElementData
    {
        public Vector vActorLocation;
        public Vector vPosition;
        public int nSize;
        public int nRelativeZ;
        public int nID;
        public bool bLocked;
        public bool bUpdate;
        public bool bUpdateLock;
        public EBioRadarType eRadarType;
    }
    public class SFXPowerIconData
    {
        public string Path;
        public string MappedIconPath;
        public string MappedIconBGPath;
        public string Id;
        public float Boundary;
        public bool IsHenchmanIcon;
        public bool IsQuickslotIcon;
    }
    public class SFXSlideshowEntry
    {
        public string ImagePath;
        public int Image;
        public float DisplayTime;
        public float MinDisplayTime;
        public int NextText;
        public int PrevText;
        public int ExitText;
        public bool CanExit;
    }
    public class SFXSlideshowParams
    {
        public SFXSlideshowEntry[] Slides;
        public NameReference Music;
        public float SlideFadeScalar;
        public bool AutoAdvance;
        public bool AllowRewind;
        public bool AllowAdvancePastEnd;
        public bool SlidesFadeIn;
        public bool SlidesFadeOut;
        public bool BlackBackground;
    }
    public class PRCInfo_t
    {
        public string sCreditsSection;
        public float fFadeTime;
        public float fHoldTime;
        public float fScrollTime;
    }
    public class ShieldBreachReplication
    {
        public int DamageType;
        public byte Trigger;
    }
    public class SkelControlProfile
    {
        public Vector EffectorLocation;
        public Vector JointTargetLocation;
        public NameReference EffectorSpaceBoneName;
        public NameReference JointTargetSpaceBoneName;
        public EBoneControlSpace EffectorLocationSpace;
        public EBoneControlSpace JointTargetLocationSpace;
    }
    public class SMAVectorParameter
    {
        public LinearColor Parameter;
        public NameReference ParameterName;
        public NameReference Group;
    }
    public class SMAScalarParameter
    {
        public NameReference ParameterName;
        public NameReference Group;
        public float Parameter;
    }
    public class SMATextureParameter
    {
        public NameReference ParameterName;
        public NameReference Group;
        public int Parameter;
    }
    public class TelemetryHookConfig
    {
        public string Name;
        public string Module;
        public string Group;
        public string String;
        public string CrossParameters;
        public string Channel;
    }
    public class TelemetryHook
    {
        public NameReference Name;
        public int ModuleID;
        public int GroupID;
        public int StringID;
        public int CrossParameters;
        public ETelemetryChannel Channel;
    }
    public class SFXVocalizationVariation
    {
        public SFXVocalizationLine[] Variations;
    }
    public class SFXVocalizationRole
    {
        public SFXVocalizationVariation[] Roles;
    }
    public class SFXVocalizationParam
    {
        public ESFXVocalizationVariationType[] SpecificType;
        public int[] SpecificValue;
    }
    public class SFXVocalizationLineV2
    {
        public SFXVocalizationParam Instigator;
        public SFXVocalizationParam Recipient;
        public SFXVocalizationParam ThirdParam;
        public string DebugText;
        public int Sound;
    }
    public class SFXVocalizationEventV2
    {
        public SFXVocalizationLineV2[] Lines;
    }
    public class SFXWaveAssetLoadData
    {
        public string AssetToLoad;
        public int LoadedAsset;
        public EAsyncLoadStatus AssetLoadStatus;
    }
    public class RvrEffectTargetSelection
    {
    }
    public class ME2ImportPowerMapping
    {
        public NameReference ME2PowerName;
        public NameReference ME2Evolve1ClassName;
        public NameReference ME2Evolve2ClassName;
        public NameReference ME3PowerName;
        public NameReference ME3PowerClassName;
    }
    public class BWOfferId
    {
        public int nID;
    }
    public class BWOfferInfo
    {
        public string sTitle;
        public string sShortDescription;
        public string sLongDescription;
        public string sPrice;
        public BWOfferId Id;
        public int nPrice;
    }
    public class BWEntitlementId
    {
        public int nID;
    }
    public class BWEntitlementToken
    {
        public string sKey;
        public string sName;
        public string sValue;
    }
    public class BWEntitlementInfo
    {
        public BWEntitlementToken[] sTokens;
        public BWEntitlementId Id;
    }
    public class BWConsumableId
    {
        public int nID;
    }
    public class BWConsumableInfo
    {
        public BWConsumableId Id;
        public int nCopies;
    }
    public class SFXOnline_OfferID
    {
        public int nHigh;
        public int nLow;
    }
    public class SFXOfferDescriptor
    {
        public string Name;
        public string Description;
        public string Image;
        public string grantEntitlementName;
        public string grantEntitlementGroup;
        public SFXOnline_OfferID externalId;
        public int Price;
        public int internalId;
        public bool UserHasIt;
    }
    public class SFXOnlineErrorMappingEntry
    {
        public string LocalizationKey;
        public SFXOnlineErrorContext ErrorContext;
        public SFXOnlineError errorCode;
    }
    public class LeaderboardStatScopeValuePair
    {
        public string KeyScope;
        public int Value;
    }
    public class LeaderboardStatScope
    {
        public LeaderboardStatScopeValuePair[] KeyScopePairs;
    }
    public class LeaderboardRecord
    {
        public UniqueNetId uidEntity;
        public string[] sRecordData;
        public int nRank;
    }
    public class LeaderboardColumn
    {
        public string Heading;
        public string sMetaData;
        public string HeaderMovieClip;
        public string RowEntryMovieClip;
        public int Width_Px;
    }
    public class SFXOnlineRankNotification
    {
        public string FriendPersonaName;
        public int MapId;
        public bool bBeaten;
    }
    public class MPDLCInfo
    {
        public string[] MapPackageNames;
        public int ModuleID;
        public int PrettyName;
    }
    public class LeaderboardDefinition
    {
        public string sPrettyName;
        public int nID;
        public bool bFriends;
    }
    public class LeaderboardMapGroup
    {
        public string MapName;
        public LeaderboardDefinition[] Entries;
    }
    public class RankBypassNotification
    {
        public string sEntityName;
        public bool bBeatenByMe;
    }
    public class SFXOnlineAccountCountryListItem
    {
        public string ISOCode;
        public string Description;
    }
    public class SFXOnlineDLCInfo
    {
        public string Name;
        public string Description;
        public string entitlementGroup;
        public string entitlementName;
        public string grantEntitlementGroup;
        public string grantEntitlementName;
        public string Image;
        public string offerKey;
        public SFXOnline_OfferID externalId;
        public int internalId;
        public int Price;
        public bool isEntitled;
    }
    public class SFXOnlineEntitlementLookupInfo
    {
        public BWEntitlementInfo BWEntitlement;
        public string sGroupName;
        public string sEntitlementName;
        public string sProductId;
    }
    public class SFXOnlineMOTDInfo
    {
        public string Message;
        public string Title;
        public string Image;
        public int TrackingID;
        public int Priority;
        public int BWEntId;
        public int offerId;
        public SFXOnlineConnection_MessageType Type;
    }
    public class TelemetryAttribute
    {
        public string sData;
        public int Key;
        public int nData;
        public float fData;
        public bool bData;
        public ETelemetryAttributeType Type;
    }
    public class SettingsPair
    {
        public string Key;
        public string Value;
    }
    public class SFXOnlineSubscriberEventType
    {
        public NameReference EventCallback;
        public SFXOnlineEventType EventType;
    }
    public class InviteData
    {
        public int[] InviteDelegates;
    }
    public class BlazeRequest
    {
        public UniqueNetId pUniquePlayerId;
        public LeaderboardStatScope Scope;
        public Pointer pJobId;
        public Pointer pExternalData;
        public int nRequestedRecordsStartRank;
        public int nRequestedRecordsRange;
        public bool bRequestedCenteredData;
        public bool bRequestedFriendData;
    }
    public class LeaderboardId
    {
        public string sLbName;
        public LeaderboardStatScope Scope;
    }
    public class LeaderboardScopeDefinition
    {
        public string KeyScope;
        public QWord[] StartKeyScopeValue;
        public QWord[] EndKeyScopeValue;
    }
    public class LeaderboardNameFormula
    {
        public string Name;
        public bool AppendLocaleCode;
    }
    public class AutoConnectAccount
    {
        public string email;
        public string Password;
    }
    public class BlazeMsgRequest
    {
        public string[] Params;
        public Pointer pJobId;
        public SFXOnlineMessageType MessageType;
    }
    public class BlazeStatsRequest
    {
        public UniqueNetId pUniquePlayerId;
        public LeaderboardStatScope Scope;
        public Pointer pJobId;
        public Pointer pExternalData;
        public int nRequestedRecordsRange;
    }
    public class MapEntry
    {
        public int EntryId;
        public int IncreaseValue;
    }
    public class AttributeMapEntry
    {
        public string Value;
        public int EntryId;
    }
    public class MessageEntry
    {
        public string SourceName;
        public string param1;
        public string param2;
        public string param3;
        public int messageId;
    }
    public class SFXOnlineImageRequest
    {
        public string mImageName;
        public int mJob;
        public int mDynamicImage;
        public bool mCompleted;
    }
    public class PCFriend
    {
        public QWord FriendID;
        public QWord PersonaId;
        public string FriendName;
        public string AvatarID;
        public string Title;
        public string TitleId;
        public string Group;
        public int PresenceState;
        public int FriendState;
    }
    public class SFXPS3_BootCheckData
    {
        public string DirName;
        public int Type;
        public int Attributes;
        public int hddFreeSizeKB;
        public int sizeKB;
        public int sysSizeKB;
        public int Commerce2Userdata;
    }
    public class SFXPS3_MinimumAgeData
    {
        public string Country;
        public int MinimumAge;
    }
    public class SFXOnlineXenonCustomPlayerListButton
    {
        public string CustomText;
        public SFXOnlineXenonPlayerListButtonType Type;
    }
    public class CachedLoginState
    {
        public UniqueNetId OnlineXuid;
        public UniqueNetId OfflineXuid;
        public ELoginStatus LoginStatus;
    }
    public class SFXProfileSettingsCache
    {
        public int[] ReadDelegates;
        public int[] WriteDelegates;
        public int[] ProfileDataChangedDelegates;
        public int Profile;
    }
    public class SFXCachedAchievements
    {
        public AchievementDetails[] Achievements;
        public int PlayerNum;
        public int TitleId;
        public int TempImage;
        public EOnlineEnumerationReadState ReadState;
    }
    public class BioPerUserDelegateLists
    {
        public int[] AchievementDelegates;
        public int[] AchievementReadDelegates;
    }
    public class LoginStatusDelegates
    {
        public int[] Delegates;
    }
    public class SFXDeviceIdCache
    {
        public int DeviceSelectionMulticast;
        public int[] DeviceSelectionDelegates;
        public int DeviceID;
    }
    public class TalkerPriority
    {
        public int CurrentPriority;
        public int LastPriority;
    }
    public class SFXOnlineRemoteTalker : RemoteTalker
    {
        public TalkerPriority LocalPriorities;
    }
    public class PartyGameInviteDelegates
    {
        public int[] Delegates;
    }
    public class SFXOnlineEventNotify
    {
        public int[] Subscribers;
        public int[] Waiters;
    }
    public class SFXOnlineNotifyQueueInfo
    {
        public int EventCallback;
        public SFXOnlineEventType EventType;
    }
    public class MPMapInfo
    {
        public string PackageName;
        public string Image;
        public Vector GalaxyAtWarMapPosition;
        public NameReference MusicEventName;
        public int Id;
        public int PrettyName;
        public int Description;
        public int GalaxyAtWarMapSubtitle;
        public bool EveryoneHasThisMap;
    }
    public class MPPrivacyInfo
    {
        public string Image;
        public int Id;
        public int Name;
        public int AllCapsName;
    }
    public class MPEnemyInfo
    {
        public string Image;
        public string WaveClass;
        public int Id;
        public int Name;
        public int AllCapsName;
    }
    public class MPChallengeInfo
    {
        public string Image;
        public int Id;
        public int Name;
        public int AllCapsName;
    }
    public class HTTPParameter
    {
        public string mName;
        public string mValue;
    }
    public class SFXOnlineComponentDescription
    {
        public int className;
        public NameReference PlatformName;
        public SFXOnlineComponentType ComponentType;
    }
    public class BioTestArrayPropStruct
    {
        public NameReference nmKey;
        public int nValue;
        public float fValue;
    }
    public class StructNoDefaultProp
    {
        public int N;
    }
    public class StructWithDefaultProp
    {
        public int N;
    }
    public class RvrUnitTestConditional_PlotBool
    {
        public int Id;
        public bool Value;
    }
    public class RvrUnitTestConditional_PlotInt
    {
        public int Id;
        public int Value;
    }
    public class RvrUnitTestConditional_PlotFloat
    {
        public int Id;
        public float Value;
    }
    public class CharacterDisplayInfo
    {
        public string Title;
        public string SubTitle;
    }
    public class SFXUnitTestAsyncLoading_CallbackState
    {
        public QWord RequestID;
        public int CallbackCalled;
        public int Canceled;
        public int PriorityResetRequired;
    }
    public class UnitTestInputEvent
    {
        public int Callback;
        public BioGuiEvents Event;
    }
    public class SubTestParams
    {
        public int RunTest;
        public int Success;
        public float TimeOut;
    }
    public class WwiseEventPair
    {
        public int Play;
        public int Stop;
    }
    public class WwiseRTPCForActorHandler
    {
        public string m_sRTPCName;
        public int m_actor;
        public float m_currentValue;
    }
    public class WwiseEventTrackKey
    {
        public float Time;
        public int Event;
    }
    public class WwiseComponentCallbackInfo
    {
        public int CallbackFlags;
        public int TargetEvent;
    }
    public class WwiseSHA1Digest
    {
        public byte Digest;
    }
    public class WwisePlatformData
    {
        public Pointer Data;
        public int Platform;
    }
    public class WwiseEventInstance : WwiseEventPair
    {
        public int WwisePlayingID;
    }
    public class WwiseRelationships
    {
        public int[] Streams;
        public int Bank;
    }
    public class WwisePlatformRelationships
    {
        public WwiseRelationships Relationships;
        public int Platform;
    }
    public class WwiseDialogueArgumentValue
    {
        public NameReference Name;
        public int Id;
    }
    public class WwiseDialogueArgument
    {
        public WwiseDialogueArgumentValue[] Values;
        public NameReference Name;
        public int Id;
    }
    public class ProcFoleyInfo
    {
        public Vector vLoc;
        public NameReference nmBoneName;
    }
    public class WwisePlatformGuid
    {
        public Guid Guid;
        public int Platform;
    }
    public class WwiseFileCacheGuids
    {
        public WwisePlatformGuid[] Guids;
    }
    public class AffectedPawn
    {
        public int Pawn;
        public bool bUpdated;
        public bool bFriendly;
        public bool bIsWarped;
    }
    public class KitDescriptionData
    {
        public string KitName;
        public int ClassString;
        public int IPNameString;
        public int IPEntryString;
        public int GameplayEntryString;
        public int StatsString1;
        public int StatsString2;
        public int StatsString3;
        public int StatsString4;
        public int StatsString5;
    }
    public class LashForceMultiplierShared
    {
        public NameReference PawnType;
        public float ForceMultiplier;
    }
    public class GearDescriptionTokens
    {
        public string UniqueName;
        public float[] Custom0Tokens;
        public float[] Custom1Tokens;
        public float[] Custom2Tokens;
        public int[] Custom3Tokens;
        public int[] Custom4Tokens;
    }
    public class ChallengeDisplayItem
    {
        public string ImageOverride;
        public int AccomplishmentIndex;
        public int TitleOverride;
        public int BodyOverride;
        public int Parent;
        public int VisibilityStartTime;
        public int VisibilityEndTime;
        public int VisibilityEntitlement;
    }
    public class ChallengeListItem
    {
        public string sName;
        public int nIndex;
        public int nID;
        public MPChallengeRankIcon eIcon;
    }
    public class ChallengeUIData
    {
        public string Title;
        public string DisplayTitle;
        public string DisplayBody;
        public string Image;
        public int CurrentValue;
        public int GoalValue;
        public int NumCompletions;
        public MPChallengeRankIcon Icon;
        public MPChallengeRank Rank;
    }
    public class DampingEyeHeightShared
    {
        public string PawnType;
        public float EyeHeight;
    }
    public class VictimWithNeedles
    {
        public int[] Needles;
        public int Victim;
    }
    public class PossessionVisualEffectData
    {
        public Guid PossessionGUID;
        public int PossessionEffect;
    }
    public class PossessionData
    {
        public bool[] Waves;
        public EDifficultyOptions Difficulty;
    }
    public class EyeLaserData
    {
        public NameReference Socket;
        public int BeamPSC;
        public int WwiseComp;
    }
    public class PendingMissileData
    {
        public int Target;
        public float CountdownTime;
        public bool Bright;
    }
    public class CollectorSwarmOverlay
    {
        public string Id;
        public Vector2D Offset;
        public int Overlay;
        public int PowerIcon;
    }
    public class ChargeData
    {
        public int CE_Charge;
    }
    public class HammerChargeData
    {
        public int CE_Charge;
    }
    public class BowChargeData
    {
        public int CE_Charge;
    }
    public class TargetAimOffset
    {
        public Vector AimOffset;
        public NameReference PawnName;
    }
    public class RetrieveMoveSpeedOverride
    {
        public NameReference CharacterClass;
        public float MovementSpeedDecrease;
    }
    public class DivingAtlasHUDParams
    {
        public int SeaLevelZ;
        public float DepthScalar;
        public float WaterDensity;
        public float AccelDueGravity;
        public float AirPressure;
    }
    public class Difficulty
    {
        public int PowerPerCell;
        public float TimeToLose1Power;
        public float SafePowerPercentage;
    }
    public class DockButton
    {
        public string ButtonID;
        public int srLabel;
        public int srButtonLabel;
        public int VisibilityConditional;
    }
    public class LashForceMultiplier
    {
        public NameReference PawnType;
        public float ForceMultiplier;
    }
    public class SFXMapAssetData_DLC
    {
        public string Asset;
        public NameReference Floor;
        public int GroupID;
    }
    public class SFXCharacterMapData_DLC
    {
        public NameReference Floor;
        public PlotIdenfitier PlotId;
        public int srCharacter;
        public int srLocation;
        public int nValue;
        public int nConditional;
        public int nConditionalParam;
    }
    public class SFXMapLocationData_DLC
    {
        public NameReference Floor;
        public int srLocation;
        public int nIndex;
    }
    public class MiniGameBasePay
    {
        public int BaseBet;
        public MiniGame GameType;
    }
    public class DenialZone
    {
        public Guid DenialGuid;
        public Vector DenialLocation;
    }
    public class PossessionVisualEffectData_Shared
    {
        public Guid PossessionGUID;
        public int PossessionEffect;
    }
    public class PossessionData_Shared
    {
        public bool[] Waves;
        public EDifficultyOptions Difficulty;
    }
    public class EyeLaserData_Shared
    {
        public NameReference Socket;
        public int BeamPSC;
        public int WwiseComp;
    }
    public class PendingMissileData_Shared
    {
        public int Target;
        public float CountdownTime;
        public bool Bright;
    }
}
