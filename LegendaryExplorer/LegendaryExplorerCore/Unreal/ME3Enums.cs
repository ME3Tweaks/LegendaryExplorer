namespace LegendaryExplorerCore.Unreal
{
    public enum EPickupObjectEvent
    {
        EPO_PickedUp,
        EPO_Dropped,
        EPO_Retrieved,
    }
    public enum ESetMaterialParameterType
    {
        SetVectorParameter,
        SetScalarParameter,
        SetTextureParameter,
        StartVectorParameter,
        StartScalarParameter,
    }
    public enum EGetHealthType
    {
        GetHealthType_Current,
        GetHealthType_Percent,
        GetHealthType_Maximum,
    }
    public enum EKismetAllowedCustomAction
    {
        KCA_None,
        KCA_Reaction_Standard,
        KCA_Reaction_Stagger,
        KCA_Reaction_Knockback,
        KCA_Reaction_StaggerForward,
        KCA_Reaction_StandardForward,
        KCA_Reaction_OnFire,
        KCA_Reaction_OnFireII,
        KCA_Reaction_PlayerStagger,
        KCA_Reaction_PlayerKnockback,
        KCA_Use,
        KCA_Revive,
        KCA_ACTMNT_ExplosionBack,
        KCA_ACTMNT_ExplosionFront,
        KCA_ACTMNT_ExplosionLeft,
        KCA_ACTMNT_ExplosionRight,
        KCA_ACTMNT_ShieldFace,
        KCA_Melee,
    }
    public enum AIKismetMoods
    {
        AIKismet_Normal,
        AIKismet_Fallback,
        AIKismet_Aggressive,
    }
    public enum EShieldType
    {
        EShieldType_All,
        EShieldType_Energy,
        EShieldType_Barrier,
    }
    public enum EKaiLengRechargeState
    {
        KLRS_None,
        KLRS_First,
        KLRS_Second,
        KLRS_Third,
        KLRS_Fourth,
    }
    public enum EKaiLengState
    {
        KaiLeng_Normal,
        KaiLeng_Fleeing,
        KaiLeng_Aggressive,
    }
    public enum EMailState
    {
        MAIL_Undelivered,
        MAIL_New,
        MAIL_Read,
        MAIL_Archived,
    }
    public enum StuntActorTargettingMode
    {
        SATM_Running,
        SATM_StoppingGracefully,
        SATM_StoppingImmediately,
    }
    public enum HenchMeleeStage
    {
        HM_Attack1,
        HM_Attack2,
        HM_Attack3,
    }
    public enum EFuelAwardType
    {
        SFXFuelAward_PercentMax,
        SFXFuelAward_PercentRemaining,
        SFXFuelAward_ExplicitValue,
    }
    public enum ECardRarity
    {
        Rarity_Common,
        Rarity_Uncommon,
        Rarity_Rare,
        Rarity_UltraRare,
    }
    public enum EPurchaseType
    {
        EPurchaseType_CreditsOnly,
        EPurchaseType_PlatformOnly,
        EPurchaseType_PlatformAndCredits,
        EPurchaseType_Free,
        EPurchaseType_Unknown,
    }
    public enum EStoreImageLocation
    {
        EStoreImageLocation_Local,
        EStoreImageLocation_Remote,
        EStoreImageLocation_Default,
    }
    public enum EUpgradeSlotType
    {
        Slot_Ammo,
        Slot_Weapon,
        Slot_Armor,
        Slot_Gear,
    }
    public enum EGameStatus
    {
        GS_None,
        GS_PendingMatch,
        GS_MatchInProgress,
        GS_MatchOver_Win,
        GS_MatchOver_Lost,
        GS_ReturningToMainMenu,
    }
    public enum EAppearanceMenuItemState
    {
        EAppearanceMenuItemState_Normal,
        EAppearanceMenuItemState_Disabled,
        EAppearanceMenuItemState_New,
    }
    public enum ELobbySubscreen
    {
        LSS_None,
        LSS_SelectCharacter,
        LSS_MissionSetup,
        LSS_Character,
        LSS_Weapons,
        LSS_Store,
        LSS_Leaderboards,
        LSS_MatchResults,
        LSS_MatchRewards,
        LSS_Appearance,
        LSS_MatchConsumables,
        LSS_MultiplayerMenu,
        LSS_Lobby,
        LSS_Squad,
        LSS_TalentsLevelUp,
    }
    public enum DISABLED_SAVE_REASON
    {
        DSR_LevelEvent1,
        DSR_LevelEvent2,
        DSR_LevelEvent3,
        DSR_UnsafeArea1,
        DSR_UnsafeArea2,
        DSR_UnsafeArea3,
        DSR_Combat1,
        DSR_Combat2,
        DSR_Combat3,
        DSR_Misc1,
        DSR_Misc2,
        DSR_Misc3,
        DSR_All,
    }
    public enum EInterpMethodType
    {
        IMT_UseFixedTangentEvalAndNewAutoTangents,
        IMT_UseFixedTangentEval,
        IMT_UseBrokenTangentEval,
    }
    public enum EInterpCurveMode
    {
        CIM_Linear,
        CIM_CurveAuto,
        CIM_Constant,
        CIM_CurveUser,
        CIM_CurveBreak,
        CIM_CurveAutoClamped, //ME3/LE only!
    }
    public enum ETickingGroup
    {
        TG_PreAsyncWork,
        TG_DuringAsyncWork,
        TG_PostAsyncWork,
        TG_PostUpdateWork,
        TG_PostDirtyComponentsWork,
    }
    public enum ENetRole
    {
        ROLE_None,
        ROLE_SimulatedProxy,
        ROLE_AutonomousProxy,
        ROLE_Authority,
    }
    public enum EAutomatedRunResult
    {
        ARR_Unknown,
        ARR_OOM,
        ARR_Passed,
    }
    public enum EDebugBreakType
    {
        DEBUGGER_NativeOnly,
        DEBUGGER_ScriptOnly,
        DEBUGGER_Both,
    }
    public enum EAxis
    {
        AXIS_NONE,
        AXIS_X,
        AXIS_Y,
        AXIS_BLANK,
        AXIS_Z,
    }
    public enum EDistributionVectorLockFlags
    {
        EDVLF_None,
        EDVLF_XY,
        EDVLF_XZ,
        EDVLF_YZ,
        EDVLF_XYZ,
    }
    public enum EDistributionVectorMirrorFlags
    {
        EDVMF_Same,
        EDVMF_Different,
        EDVMF_Mirror,
    }
    public enum ESFXLanguageContentType
    {
        ESFXLanguageContentType_Package,
        ESFXLanguageContentType_Text,
        ESFXLanguageContentType_Speech,
    }
    public enum ESFXLanguageSetting
    {
        ESFXLanguageSetting_Current,
        ESFXLanguageSetting_SystemPreferred,
        ESFXLanguageSetting_SKUDefault,
    }
    public enum EInputEvent
    {
        IE_Pressed,
        IE_Released,
        IE_Repeat,
        IE_DoubleClick,
        IE_Axis,
    }
    public enum AlphaBlendType
    {
        ABT_Linear,
        ABT_Cubic,
        ABT_Sinusoidal,
        ABT_EaseInOutExponent2,
        ABT_EaseInOutExponent3,
        ABT_EaseInOutExponent4,
        ABT_EaseInOutExponent5,
        ABT_EaseIn,
        ABT_EaseOut,
    }
    public enum EPhysics
    {
        PHYS_None,
        PHYS_Walking,
        PHYS_Falling,
        PHYS_Swimming,
        PHYS_Flying,
        PHYS_Rotating,
        PHYS_Projectile,
        PHYS_Interpolating,
        PHYS_Spider,
        PHYS_Ladder,
        PHYS_RigidBody,
        PHYS_SoftBody,
        PHYS_NavMeshWalking,
        PHYS_PathApproximation,
        PHYS_Unused,
        PHYS_Custom,
    }
    public enum EMoveDir
    {
        MD_Stationary,
        MD_Forward,
        MD_Backward,
        MD_Left,
        MD_Right,
        MD_Up,
        MD_Down,
    }
    public enum ECollisionType
    {
        COLLIDE_CustomDefault,
        COLLIDE_NoCollision,
        COLLIDE_BlockAll,
        COLLIDE_BlockWeapons,
        COLLIDE_TouchAll,
        COLLIDE_TouchWeapons,
        COLLIDE_BlockAllButWeapons,
        COLLIDE_TouchAllButWeapons,
        COLLIDE_WaterSurface,
        COLLIDE_BlockWeaponsKickable,
    }
    public enum EPowerResistance
    {
        Resistance_Full,
        Resistance_Partial,
        Resistance_None,
    }
    public enum EComponentType
    {
        COMPONENT_Unknown,
        COMPONENT_Animation,
        COMPONENT_AI,
        COMPONENT_Gameplay,
        COMPONENT_Graphics,
        COMPONENT_Particles,
        COMPONENT_StaticMesh,
        COMPONENT_SkinMesh,
        COMPONENT_Lights,
        COMPONENT_Audio,
        COMPONENT_Physics,
        COMPONENT_Engine,
    }
    public enum GJKResult
    {
        GJK_Intersect,
        GJK_NoIntersection,
        GJK_Fail,
    }
    public enum EDetailMode
    {
        DM_Low,
        DM_Medium,
        DM_High,
    }
    public enum ESceneDepthPriorityGroup
    {
        SDPG_UnrealEdBackground,
        SDPG_World,
        SDPG_Foreground,
        SDPG_UnrealEdForeground,
        SDPG_PostProcess,
    }
    public enum ERadialImpulseFalloff
    {
        RIF_Constant,
        RIF_Linear,
    }
    public enum ERBCollisionChannel
    {
        RBCC_Default,
        RBCC_Nothing,
        RBCC_Pawn,
        RBCC_Vehicle,
        RBCC_Water,
        RBCC_GameplayPhysics,
        RBCC_EffectPhysics,
        RBCC_Untitled1,
        RBCC_Untitled2,
        RBCC_Untitled3,
        RBCC_Untitled4,
        RBCC_Cloth,
        RBCC_FluidDrain,
        RBCC_SoftBody,
        RBCC_FracturedMeshPart,
        RBCC_BlockingVolume,
        RBCC_DeadPawn,
        RBCC_Clothing,
        RBCC_ClothingCollision,
    }
    public enum ETravelType
    {
        TRAVEL_Absolute,
        TRAVEL_Partial,
        TRAVEL_Relative,
    }
    public enum EDoubleClickDir
    {
        DCLICK_None,
        DCLICK_Left,
        DCLICK_Right,
        DCLICK_Forward,
        DCLICK_Back,
        DCLICK_Active,
        DCLICK_Done,
    }
    public enum EViewTargetBlendFunction
    {
        VTBlend_Linear,
        VTBlend_Cubic,
        VTBlend_EaseIn,
        VTBlend_EaseOut,
        VTBlend_EaseInOut,
    }
    public enum EFocusType
    {
        FOCUS_Distance,
        FOCUS_Position,
    }
    public enum ECameraAnimPlaySpace
    {
        CAPS_CameraLocal,
        CAPS_World,
        CAPS_UserDefined,
    }
    public enum KismetVarTypes
    {
        KVT_Int,
        KVT_Float,
        KVT_Bool,
        KVT_String,
        KVT_Object,
        KVT_Name,
        KVT_Vector,
    }
    public enum EMovieControlType
    {
        MCT_Play,
        MCT_Stop,
        MCT_Pause,
    }
    public enum ELoginStatus
    {
        LS_NotLoggedIn,
        LS_UsingLocalProfile,
        LS_LoggedIn,
    }
    public enum EFeaturePrivilegeLevel
    {
        FPL_Disabled,
        FPL_EnabledFriendsOnly,
        FPL_Enabled,
    }
    public enum ENetworkNotificationPosition
    {
        NNP_TopLeft,
        NNP_TopCenter,
        NNP_TopRight,
        NNP_CenterLeft,
        NNP_Center,
        NNP_CenterRight,
        NNP_BottomLeft,
        NNP_BottomCenter,
        NNP_BottomRight,
    }
    public enum EOnlineGameState
    {
        OGS_NoSession,
        OGS_Pending,
        OGS_Starting,
        OGS_InProgress,
        OGS_Ending,
        OGS_Ended,
    }
    public enum EOnlineEnumerationReadState
    {
        OERS_NotStarted,
        OERS_InProgress,
        OERS_Done,
        OERS_Failed,
    }
    public enum EOnlineFriendState
    {
        OFS_Offline,
        OFS_Online,
        OFS_Away,
        OFS_Busy,
    }
    public enum EOnlineServerConnectionStatus
    {
        OSCS_NotConnected,
        OSCS_Connected,
        OSCS_ConnectionDropped,
        OSCS_NoNetworkConnection,
        OSCS_ServiceUnavailable,
        OSCS_UpdateRequired,
        OSCS_ServersTooBusy,
        OSCS_DuplicateLoginDetected,
        OSCS_InvalidUser,
    }
    public enum ENATType
    {
        NAT_Unknown,
        NAT_Open,
        NAT_Moderate,
        NAT_Strict,
    }
    public enum EProgressMessageType
    {
        PMT_Clear,
        PMT_Information,
        PMT_AdminMessage,
        PMT_DownloadProgress,
        PMT_ConnectionFailure,
        PMT_SocketFailure,
    }
    public enum EPhysBodyOp
    {
        PBO_None,
        PBO_Term,
        PBO_Disable,
    }
    public enum EFaceFXRegOp
    {
        FXRO_Add,
        FXRO_Multiply,
        FXRO_Replace,
    }
    public enum ERootMotionRotationMode
    {
        RMRM_Ignore,
        RMRM_RotateActor,
    }
    public enum EFaceFXBlendMode
    {
        FXBM_Overwrite,
        FXBM_Additive,
    }
    public enum ERootMotionMode
    {
        RMM_Translate,
        RMM_Velocity,
        RMM_Ignore,
        RMM_Accel,
        RMM_Relative,
    }
    public enum EInputTypes
    {
        IT_XAxis,
        IT_YAxis,
    }
    public enum EInputMatchAction
    {
        IMA_GreaterThan,
        IMA_LessThan,
    }
    public enum ELanBeaconState
    {
        LANB_NotUsingLanBeacon,
        LANB_Hosting,
        LANB_Searching,
    }
    public enum ENavMeshEdgeType
    {
        NAVEDGE_Normal,
        NAVEDGE_Mantle,
        NAVEDGE_Coverslip,
        NAVEDGE_SwatTurn,
        NAVEDGE_DropDown,
        NAVEDGE_PathObject,
    }
    public enum EAmbientOcclusionQuality
    {
        AO_High,
        AO_Medium,
        AO_Low,
    }
    public enum ESoundDistanceCalc
    {
        SOUNDDISTANCE_Normal,
        SOUNDDISTANCE_InfiniteXYPlane,
        SOUNDDISTANCE_InfiniteXZPlane,
        SOUNDDISTANCE_InfiniteYZPlane,
    }
    public enum SoundDistanceModel
    {
        ATTENUATION_Linear,
        ATTENUATION_Logarithmic,
        ATTENUATION_Inverse,
        ATTENUATION_LogReverse,
        ATTENUATION_NaturalSound,
    }
    public enum AnimationKeyFormat
    {
        AKF_ConstantKeyLerp,
        AKF_VariableKeyLerp,
    }
    public enum AnimationCompressionFormat
    {
        ACF_None,
        ACF_Float96NoW,
        ACF_Fixed48NoW,
        ACF_IntervalFixed32NoW,
        ACF_Fixed32NoW,
        ACF_Float32NoW,
        ACF_BioFixed48,
    }
    public enum ESliderType
    {
        ST_1D,
        ST_2D,
    }
    public enum EWeightCheck
    {
        EWC_AnimNodeSlotNotPlaying,
        EWC_ChildIndexFullWeight,
        EWC_ChildIndexNotFullWeight,
        EWC_ChildIndexRelevant,
        EWC_ChildIndexNotRelevant,
    }
    public enum EBlendType
    {
        EBT_ParentBoneSpace,
        EBT_MeshSpace,
    }
    public enum EAnimAimDir
    {
        ANIMAIM_LEFTUP,
        ANIMAIM_CENTERUP,
        ANIMAIM_RIGHTUP,
        ANIMAIM_LEFTCENTER,
        ANIMAIM_CENTERCENTER,
        ANIMAIM_RIGHTCENTER,
        ANIMAIM_LEFTDOWN,
        ANIMAIM_CENTERDOWN,
        ANIMAIM_RIGHTDOWN,
    }
    public enum EAimID
    {
        EAID_LeftUp,
        EAID_LeftDown,
        EAID_RightUp,
        EAID_RightDown,
        EAID_ZeroUp,
        EAID_ZeroDown,
        EAID_ZeroLeft,
        EAID_ZeroRight,
        EAID_CellLU,
        EAID_CellCU,
        EAID_CellRU,
        EAID_CellLC,
        EAID_CellCC,
        EAID_CellRC,
        EAID_CellLD,
        EAID_CellCD,
        EAID_CellRD,
    }
    public enum EBaseBlendType
    {
        BBT_ByActorTag,
        BBT_ByActorClass,
    }
    public enum ERootRotationOption
    {
        RRO_Default,
        RRO_Discard,
        RRO_Extract,
    }
    public enum ERootBoneAxis
    {
        RBA_Default,
        RBA_Discard,
        RBA_Translate,
    }
    public enum ELightAffectsClassification
    {
        LAC_USER_SELECTED,
        LAC_DYNAMIC_AFFECTING,
        LAC_STATIC_AFFECTING,
        LAC_DYNAMIC_AND_STATIC_AFFECTING,
    }
    public enum ELightShadowMode
    {
        LightShadow_Normal,
        LightShadow_Modulate,
        LightShadow_ModulateBetter,
    }
    public enum EShadowProjectionTechnique
    {
        ShadowProjTech_Default,
        ShadowProjTech_PCF,
        ShadowProjTech_VSM,
        ShadowProjTech_BPCF_Low,
        ShadowProjTech_BPCF_Medium,
        ShadowProjTech_BPCF_High,
    }
    public enum EShadowFilterQuality
    {
        SFQ_Low,
        SFQ_Medium,
        SFQ_High,
    }
    public enum EDynamicLightEnvironmentBoundsMethod
    {
        DLEB_OwnerComponents,
        DLEB_ManualOverride,
        DLEB_ActiveComponents,
    }
    public enum ESoundClassName
    {
        Master,
    }
    public enum EDebugState
    {
        DEBUGSTATE_None,
        DEBUGSTATE_IsolateDryAudio,
        DEBUGSTATE_IsolateReverb,
        DEBUGSTATE_TestLPF,
        DEBUGSTATE_TestStereoBleed,
        DEBUGSTATE_TestLFEBleed,
        DEBUGSTATE_DisableLPF,
    }
    public enum ECsgOper
    {
        CSG_Active,
        CSG_Add,
        CSG_Subtract,
        CSG_Intersect,
        CSG_Deintersect,
    }
    public enum ReverbPreset
    {
        REVERB_Default,
        REVERB_Bathroom,
        REVERB_StoneRoom,
        REVERB_Auditorium,
        REVERB_ConcertHall,
        REVERB_Cave,
        REVERB_Hallway,
        REVERB_StoneCorridor,
        REVERB_Alley,
        REVERB_Forest,
        REVERB_City,
        REVERB_Mountains,
        REVERB_Quarry,
        REVERB_Plain,
        REVERB_ParkingLot,
        REVERB_SewerPipe,
        REVERB_Underwater,
        REVERB_SmallRoom,
        REVERB_MediumRoom,
        REVERB_LargeRoom,
        REVERB_MediumHall,
        REVERB_LargeHall,
        REVERB_Plate,
    }
    public enum ETTSSpeaker
    {
        TTSSPEAKER_Paul,
        TTSSPEAKER_Harry,
        TTSSPEAKER_Frank,
        TTSSPEAKER_Dennis,
        TTSSPEAKER_Kit,
        TTSSPEAKER_Betty,
        TTSSPEAKER_Ursula,
        TTSSPEAKER_Rita,
        TTSSPEAKER_Wendy,
    }
    public enum EDLEStateType
    {
        DLEST_Default,
        DLEST_Cinematic,
        DLEST_Simple,
    }
    public enum ERimLightControlType
    {
        RLCT_Key,
        RLCT_Camera,
    }
    public enum ETrackActiveCondition
    {
        ETAC_Always,
        ETAC_GoreEnabled,
        ETAC_GoreDisabled,
        ETAC_BioFemalePlayer,
        ETAC_BioMalePlayer,
        ETAC_BioSingleHandWeapon,
        ETAC_BioDualHandWeapon,
    }
    public enum EMaterialUsage
    {
        MATUSAGE_SkeletalMesh,
        MATUSAGE_FracturedMeshes,
        MATUSAGE_ParticleSprites,
        MATUSAGE_BeamTrails,
        MATUSAGE_ParticleSubUV,
        MATUSAGE_Foliage,
        MATUSAGE_SpeedTree,
        MATUSAGE_StaticLighting,
        MATUSAGE_GammaCorrection,
        MATUSAGE_LensFlare,
        MATUSAGE_InstancedMeshParticles,
        MATUSAGE_FluidSurface,
        MATUSAGE_Decals,
        MATUSAGE_MaterialEffect,
        MATUSAGE_MorphTargets,
        MATUSAGE_FogVolumes,
        MATUSAGE_RadialBlur,
        MATUSAGE_InstancedMeshes,
        MATUSAGE_SplineMesh,
        MATUSAGE_ScreenDoorFade,
        MATUSAGE_APEXMesh,
        MATUSAGE_LightEnvironments,
        MATUSAGE_VectorLightMaps,
        MATUSAGE_SimpleLightMaps,
    }
    public enum EMaterialProperty
    {
        MP_EmissiveColor,
        MP_Opacity,
        MP_OpacityMask,
        MP_Distortion,
        MP_TwoSidedLightingMask,
        MP_DiffuseColor,
        MP_DiffusePower,
        MP_SpecularColor,
        MP_SpecularPower,
        MP_Normal,
        MP_CustomLighting,
        MP_CustomLightingDiffuse,
        MP_AnisotropicDirection,
        MP_WorldPositionOffset,
        MP_TMissionMask,
        MP_TMissionColor,
        MP_CustomSkylightDiffuse,
    }
    public enum eventMPEnumID
    {
        ENEMY_SPAWNED,
        ENEMY_DIED,
        SHIELDS_DOWN,
        PLAYER_DOWNED,
        REVIVAL_STARTED,
        REVIVAL_CANCELLED,
        PLAYER_REVIVED,
        SHIELD_RESTORED,
        WEAPON_PICKED_UP,
        SWITCH_WEAPON,
        RELOAD,
        AMMO_PICKED_UP,
        HEAVY_MELEE,
        CUSTOM_ACTION_IMPACT,
        CAST_POWER,
        POWER_IMPACT,
        PROJECTILE_CREATED,
        PROJECTILE_EXPLODED,
        ANIMATED_REACTION,
        START_CLIMBING_LADDER,
        FINISH_CLIMBING_LADDER,
        Roll,
        ENTER_COVER,
        EXIT_COVER,
        COVER_SLIP,
        SWAT_TURN,
        Mantle,
        GAME_STARTED,
        WAVE_STARTED,
        WAVE_COMPLETED,
        ALL_WAVES_COMPLETED,
        GAME_OVER,
        ANNEX_STARTED,
        ENTER_ANNEX_ZONE,
        LEAVE_ANNEX_ZONE,
        ANNEX_COMPLETE,
        LAG_REPORTED,
        LOW_FRAMERATE,
        BANDWIDTH_SATURATED,
        CUSTOM_ACTION_STARTED,
        NET_PERF_REPORTED,
        CUSTOM_EVENT_REPORTED,
        LOG_SPAM,
        POWER_SUBSEQUENT_IMPACT,
    }
    public enum sessionStatus
    {
        SESSION_INACTIVE,
        SESSION_ACTIVE,
    }
    public enum EInitialOscillatorOffset
    {
        EOO_OffsetRandom,
        EOO_OffsetZero,
    }
    public enum EFontImportCharacterSet
    {
        FontICS_Default,
        FontICS_Ansi,
        FontICS_Symbol,
    }
    public enum TextureCompressionSettings
    {
        TC_Default,
        TC_Normalmap,
        TC_Displacementmap,
        TC_NormalmapAlpha,
        TC_Grayscale,
        TC_HighDynamicRange,
        TC_OneBitAlpha,
        TC_NormalmapUncompressed,
        TC_NormalmapBC5,
        TC_OneBitMonochrome,
        TC_NormalmapHQ,
    }
    public enum EPixelFormat
    {
        PF_Unknown,
        PF_A32B32G32R32F,
        PF_A8R8G8B8,
        PF_G8,
        PF_G16,
        PF_DXT1,
        PF_DXT3,
        PF_DXT5,
        PF_UYVY,
        PF_FloatRGB,
        PF_FloatRGBA,
        PF_DepthStencil,
        PF_ShadowDepth,
        PF_FilteredShadowDepth,
        PF_R32F,
        PF_G16R16,
        PF_G16R16F,
        PF_G16R16F_FILTER,
        PF_G32R32F,
        PF_A2B10G10R10,
        PF_A16B16G16R16,
        PF_D24,
        PF_R16F,
        PF_R16F_FILTER,
        PF_BC5,
        PF_V8U8,
        PF_A1,
        PF_NormalMap_LQ,
        PF_NormalMap_HQ,
    }
    public enum TextureFilter
    {
        TF_Nearest,
        TF_Linear,
    }
    public enum TextureAddress
    {
        TA_Wrap,
        TA_Clamp,
        TA_Mirror,
    }
    public enum TextureGroup
    {
        TEXTUREGROUP_World,
        TEXTUREGROUP_WorldNormalMap,
        TEXTUREGROUP_Lightmap,
        TEXTUREGROUP_Shadowmap,
        TEXTUREGROUP_RenderTarget,
        TEXTUREGROUP_Character_Diff,
        TEXTUREGROUP_Character_Norm,
        TEXTUREGROUP_Character_Spec,
        TEXTUREGROUP_Environment_512,
        TEXTUREGROUP_Environment_256,
        TEXTUREGROUP_Environment_128,
        TEXTUREGROUP_Environment_64,
        TEXTUREGROUP_VFX_512,
        TEXTUREGROUP_VFX_256,
        TEXTUREGROUP_VFX_128,
        TEXTUREGROUP_VFX_64,
        TEXTUREGROUP_UI,
        TEXTUREGROUP_AmbientLightMap,
        TEXTUREGROUP_Environment_1024,
        TEXTUREGROUP_VFX_1024,
        TEXTUREGROUP_APL_128,
        TEXTUREGROUP_APL_256,
        TEXTUREGROUP_APL_512,
        TEXTUREGROUP_APL_1024,
        TEXTUREGROUP_Character_1024,
        TEXTUREGROUP_Promotional,
        TEXTUREGROUP_ColorLookupTable,
    }
    public enum TextureMipGenSettings
    {
        TMGS_FromTextureGroup,
        TMGS_SimpleAverage,
        TMGS_Sharpen0,
        TMGS_Sharpen1,
        TMGS_Sharpen2,
        TMGS_Sharpen3,
        TMGS_Sharpen4,
        TMGS_Sharpen5,
        TMGS_Sharpen6,
        TMGS_Sharpen7,
        TMGS_Sharpen8,
        TMGS_Sharpen9,
        TMGS_Sharpen10,
    }
    public enum EUIWidgetFace
    {
        UIFACE_Left,
        UIFACE_Top,
        UIFACE_Right,
        UIFACE_Bottom,
    }
    public enum EUIOrientation
    {
        UIORIENT_Horizontal,
        UIORIENT_Vertical,
    }
    public enum ENetMode
    {
        NM_Standalone,
        NM_DedicatedServer,
        NM_ListenServer,
        NM_Client,
    }
    public enum EConsoleType
    {
        CONSOLE_Any,
        CONSOLE_Xbox360,
        CONSOLE_PS3,
        CONSOLE_Mobile,
        CONSOLE_IPhone,
        CONSOLE_Tegra,
    }
    public enum EInputPlatformType
    {
        IPT_PC,
        IPT_360,
        IPT_PS3,
    }
    public enum EUIAlignment
    {
        UIALIGN_Left,
        UIALIGN_Center,
        UIALIGN_Right,
        UIALIGN_Default,
    }
    public enum ETextClipMode
    {
        CLIP_None,
        CLIP_Normal,
        CLIP_Ellipsis,
        CLIP_Wrap,
    }
    public enum ETextAutoScaleMode
    {
        UIAUTOSCALE_None,
        UIAUTOSCALE_Normal,
        UIAUTOSCALE_Justified,
        UIAUTOSCALE_ResolutionBased,
    }
    public enum EUIDefaultPenColor
    {
        UIPEN_White,
        UIPEN_Black,
        UIPEN_Grey,
    }
    public enum EMaterialAdjustmentType
    {
        ADJUST_None,
        ADJUST_Normal,
        ADJUST_Justified,
        ADJUST_Bound,
        ADJUST_Stretch,
    }
    public enum EUIDataProviderFieldType
    {
        DATATYPE_Property,
        DATATYPE_Provider,
        DATATYPE_RangeProperty,
        DATATYPE_NetIdProperty,
        DATATYPE_Collection,
        DATATYPE_ProviderCollection,
    }
    public enum ERotationAnchor
    {
        RA_Absolute,
        RA_Center,
        RA_PivotLeft,
        RA_PivotRight,
        RA_PivotTop,
        RA_PivotBottom,
        RA_UpperLeft,
        RA_UpperRight,
        RA_LowerLeft,
        RA_LowerRight,
    }
    public enum EUIExtentEvalType
    {
        UIEXTENTEVAL_Pixels,
        UIEXTENTEVAL_PercentSelf,
        UIEXTENTEVAL_PercentOwner,
        UIEXTENTEVAL_PercentScene,
        UIEXTENTEVAL_PercentViewport,
    }
    public enum EUIDockPaddingEvalType
    {
        UIPADDINGEVAL_Pixels,
        UIPADDINGEVAL_PercentTarget,
        UIPADDINGEVAL_PercentOwner,
        UIPADDINGEVAL_PercentScene,
        UIPADDINGEVAL_PercentViewport,
    }
    public enum EPositionEvalType
    {
        EVALPOS_None,
        EVALPOS_PixelViewport,
        EVALPOS_PixelScene,
        EVALPOS_PixelOwner,
        EVALPOS_PercentageViewport,
        EVALPOS_PercentageOwner,
        EVALPOS_PercentageScene,
    }
    public enum EUIPostProcessGroup
    {
        UIPostProcess_None,
        UIPostProcess_Background,
        UIPostProcess_Foreground,
        UIPostProcess_BackgroundAndForeground,
        UIPostProcess_Dynamic,
    }
    public enum EUIAspectRatioConstraint
    {
        UIASPECTRATIO_AdjustNone,
        UIASPECTRATIO_AdjustWidth,
        UIASPECTRATIO_AdjustHeight,
    }
    public enum ECoverGroupFillAction
    {
        CGFA_Overwrite,
        CGFA_Add,
        CGFA_Remove,
        CGFA_Clear,
        CGFA_Cylinder,
    }
    public enum ECoverAction
    {
        CA_Default,
        CA_BlindLeft,
        CA_BlindRight,
        CA_LeanLeft,
        CA_LeanRight,
        CA_PopUp,
        CA_BlindUp,
        CA_PeekLeft,
        CA_PeekRight,
        CA_PeekUp,
        CA_SwatTurn,
        CA_Aimback,
    }
    public enum ECoverDirection
    {
        CD_Default,
        CD_Left,
        CD_Right,
        CD_Up,
    }
    public enum ECoverType
    {
        CT_None,
        CT_Standing,
        CT_MidLevel,
    }
    public enum ECoverLocationDescription
    {
        CoverDesc_None,
        CoverDesc_InWindow,
        CoverDesc_InDoorway,
        CoverDesc_BehindCar,
        CoverDesc_BehindTruck,
        CoverDesc_OnTruck,
        CoverDesc_BehindBarrier,
        CoverDesc_BehindColumn,
        CoverDesc_BehindCrate,
        CoverDesc_BehindWall,
        CoverDesc_BehindStatue,
        CoverDesc_BehindSandbags,
    }
    public enum EFireLinkID
    {
        FLI_FireLink,
        FLI_RejectedFireLink,
    }
    public enum LightMapEncodingType
    {
        LMET_UE3,
        LMET_Vector,
        LMET_Simple,
    }
    public enum EProviderAccessType
    {
        ACCESS_ReadOnly,
        ACCESS_PerField,
        ACCESS_WriteAll,
    }
    public enum EDecalTransform
    {
        DecalTransform_OwnerAbsolute,
        DecalTransform_OwnerRelative,
        DecalTransform_SpawnRelative,
    }
    public enum EFilterMode
    {
        FM_None,
        FM_Ignore,
        FM_Affect,
    }
    public enum EBlendMode
    {
        BLEND_Opaque,
        BLEND_Masked,
        BLEND_Translucent,
        BLEND_Additive,
        BLEND_Modulate,
        BLEND_SoftMasked,
        BLEND_AlphaComposite,
    }
    public enum EMaterialLightingModel
    {
        MLM_Phong,
        MLM_NonDirectional,
        MLM_Unlit,
        MLM_SHPRT,
        MLM_Custom,
        MLM_Anisotropic,
    }
    public enum EBIOPhysicalMaterialAutoEnum
    {
        PHYM_Empty,
    }
    public enum EXbox360GammaQuality
    {
        XGQ_Default,
        XGQ_High,
        XGQ_Low,
    }
    public enum ELightingBuildQuality
    {
        Quality_Preview,
        Quality_Medium,
        Quality_High,
        Quality_Production,
    }
    public enum DistributionParamMode
    {
        DPM_Normal,
        DPM_Abs,
        DPM_Direct,
    }
    public enum EDoorType
    {
        DOOR_Shoot,
        DOOR_Touch,
    }
    public enum EParticleSystemUpdateMode
    {
        EPSUM_RealTime,
        EPSUM_FixedTime,
    }
    public enum EParticleSystemOcclusionBoundsMethod
    {
        EPSOBM_None,
        EPSOBM_ParticleBounds,
        EPSOBM_CustomBounds,
    }
    public enum ParticleSystemLODMethod
    {
        PARTICLESYSTEMLODMETHOD_Automatic,
        PARTICLESYSTEMLODMETHOD_DirectSet,
        PARTICLESYSTEMLODMETHOD_ActivateAutomatic,
    }
    public enum EParticleSysParamType
    {
        PSPT_None,
        PSPT_Scalar,
        PSPT_Vector,
        PSPT_Color,
        PSPT_Actor,
        PSPT_Material,
    }
    public enum ParticleReplayState
    {
        PRS_Disabled,
        PRS_Capturing,
        PRS_Replaying,
    }
    public enum EParticleEventType
    {
        EPET_Any,
        EPET_Spawn,
        EPET_Death,
        EPET_Collision,
        EPET_Kismet,
    }
    public enum EBioUnTexCompressSetting
    {
        BioTCS_NvTT_NoCuda,
        BioTCS_NvTT_Cuda,
    }
    public enum ETransitionType
    {
        TT_None,
        TT_Paused,
        TT_Loading,
        TT_Saving,
        TT_Connecting,
        TT_Precaching,
    }
    public enum FWFileType
    {
        FWFT_Log,
        FWFT_Stats,
        FWFT_HTML,
        FWFT_User,
        FWFT_Debug,
    }
    public enum EInfluenceType
    {
        Fluid_Flow,
        Fluid_Raindrops,
        Fluid_Wave,
        Fluid_Sphere,
    }
    public enum EWaveformFunction
    {
        WF_Constant,
        WF_LinearIncreasing,
        WF_LinearDecreasing,
        WF_Sin0to90,
        WF_Sin90to180,
        WF_Sin0to180,
        WF_Noise,
    }
    public enum EFullyLoadPackageType
    {
        FULLYLOAD_Map,
        FULLYLOAD_Game_PreLoadClass,
        FULLYLOAD_Game_PostLoadClass,
        FULLYLOAD_Always,
        FULLYLOAD_Mutator,
    }
    public enum EStandbyType
    {
        STDBY_Rx,
        STDBY_Tx,
        STDBY_BadPing,
    }
    public enum ESettingsDataType
    {
        SDT_Empty,
        SDT_Int32,
        SDT_Int64,
        SDT_Double,
        SDT_String,
        SDT_Float,
        SDT_Blob,
        SDT_DateTime,
    }
    public enum EOnlineDataAdvertisementType
    {
        ODAT_DontAdvertise,
        ODAT_OnlineService,
        ODAT_QoS,
        ODAT_OnlineServiceAndQoS,
    }
    public enum EPropertyValueMappingType
    {
        PVMT_RawValue,
        PVMT_PredefinedValues,
        PVMT_Ranged,
        PVMT_IdMapped,
    }
    public enum ESplitScreenType
    {
        eSST_NONE,
        eSST_2P_HORIZONTAL,
        eSST_2P_VERTICAL,
        eSST_3P_FAVOR_TOP,
        eSST_3P_FAVOR_BOTTOM,
        eSST_4P,
    }
    public enum ESafeZoneType
    {
        eSZ_TOP,
        eSZ_BOTTOM,
        eSZ_LEFT,
        eSZ_RIGHT,
    }
    public enum EEdgeHandlingStatus
    {
        EHS_AddedBothDirs,
        EHS_Added0to1,
        EHS_Added1to0,
        EHS_AddedNone,
    }
    public enum EBioBinkAsyncState
    {
        BioBinkAsync_Closed,
        BioBinkAsync_Preloading,
        BioBinkAsync_PreloadComplete,
        BioBinkAsync_Running,
    }
    public enum ESFXFindByTagTypes
    {
        FindActorByTag,
        FindActorByNode,
        UseGroupActor,
    }
    public enum EBioAutoSetFXAnimTrack
    {
        FaceFXAnimTrack_Unset,
    }
    public enum EBioAutoSetFXAnimGroupTrack
    {
        FaceFXAnimGroupTrack_Unset,
    }
    public enum EBioAutoSetFXAnimSeqTrack
    {
        FaceFXAnimSeqTrack_Unset,
    }
    public enum ETrackToggleAction
    {
        ETTA_Off,
        ETTA_On,
        ETTA_Toggle,
        ETTA_Trigger,
    }
    public enum EVisibilityTrackCondition
    {
        EVTC_Always,
        EVTC_GoreEnabled,
        EVTC_GoreDisabled,
    }
    public enum EVisibilityTrackAction
    {
        EVTA_Hide,
        EVTA_Show,
        EVTA_Toggle,
    }
    public enum EInterpTrackMoveFrame
    {
        IMF_World,
        IMF_RelativeToInitial,
        IMF_AnchorObject,
    }
    public enum EInterpTrackMoveRotMode
    {
        IMR_Keyframed,
        IMR_LookAtGroup,
    }
    public enum EStreamingVolumeUsage
    {
        SVB_Loading,
        SVB_LoadingAndVisibility,
        SVB_VisibilityBlockingOnLoad,
        SVB_BlockingOnLoad,
        SVB_LoadingNotVisible,
    }
    public enum EAddPostProcessEffectCombineType
    {
        EAPPE_Override,
        EAPPE_Combine,
    }
    public enum ETextureColorChannel
    {
        TCC_Red,
        TCC_Green,
        TCC_Blue,
        TCC_Alpha,
    }
    public enum ECustomMaterialOutputType
    {
        CMOT_Float1,
        CMOT_Float2,
        CMOT_Float3,
        CMOT_Float4,
    }
    public enum ESceneTextureType
    {
        SceneTex_Lighting,
    }
    public enum EMaterialVectorCoordTransformSource
    {
        TRANSFORMSOURCE_World,
        TRANSFORMSOURCE_Local,
        TRANSFORMSOURCE_Tangent,
    }
    public enum EMaterialVectorCoordTransform
    {
        TRANSFORM_World,
        TRANSFORM_View,
        TRANSFORM_Local,
        TRANSFORM_Tangent,
    }
    public enum EMaterialPositionTransform
    {
        TRANSFORMPOS_World,
    }
    public enum EPathSearchType
    {
        PST_Default,
        PST_Breadth,
        PST_NewBestPathTo,
        PST_Constraint,
    }
    public enum FFG_ForceFieldCoordinates
    {
        FFG_CARTESIAN,
        FFG_SPHERICAL,
        FFG_CYLINDRICAL,
        FFG_TOROIDAL,
    }
    public enum FFB_ForceFieldCoordinates
    {
        FFB_CARTESIAN,
        FFB_SPHERICAL,
        FFB_CYLINDRICAL,
        FFB_TOROIDAL,
    }
    public enum EOnlineAccountCreateStatus
    {
        OACS_CreateSuccessful,
        OACS_UnknownError,
        OACS_InvalidUserName,
        OACS_InvalidPassword,
        OACS_InvalidUniqueUserName,
        OACS_UniqueUserNameInUse,
        OACS_ServiceUnavailable,
    }
    public enum EOnlineGameSearchEntryType
    {
        OGSET_Property,
        OGSET_LocalizedSetting,
        OGSET_ObjectProperty,
    }
    public enum EOnlineGameSearchSortType
    {
        OGSSO_Ascending,
        OGSSO_Descending,
    }
    public enum EOnlineNewsType
    {
        ONT_Unknown,
        ONT_GameNews,
        ONT_ContentAnnouncements,
        ONT_Misc,
    }
    public enum EOnlineProfilePropertyOwner
    {
        OPPO_None,
        OPPO_OnlineService,
        OPPO_Game,
    }
    public enum EOnlinePlayerStorageAsyncState
    {
        OPAS_None,
        OPAS_Read,
        OPAS_Write,
    }
    public enum EProfileSettingID
    {
        PSI_Unknown,
        PSI_ControllerVibration,
        PSI_YInversion,
        PSI_GamerCred,
        PSI_GamerRep,
        PSI_VoiceMuted,
        PSI_VoiceThruSpeakers,
        PSI_VoiceVolume,
        PSI_GamerPictureKey,
        PSI_GamerMotto,
        PSI_GamerTitlesPlayed,
        PSI_GamerAchievementsEarned,
        PSI_GameDifficulty,
        PSI_ControllerSensitivity,
        PSI_PreferredColor1,
        PSI_PreferredColor2,
        PSI_AutoAim,
        PSI_AutoCenter,
        PSI_MovementControl,
        PSI_RaceTransmission,
        PSI_RaceCameraLocation,
        PSI_RaceBrakeControl,
        PSI_RaceAcceleratorControl,
        PSI_GameCredEarned,
        PSI_GameAchievementsEarned,
        PSI_EndLiveIds,
        PSI_ProfileVersionNum,
        PSI_ProfileSaveCount,
    }
    public enum EProfileDifficultyOptions
    {
        PDO_Normal,
        PDO_Easy,
        PDO_Hard,
    }
    public enum EProfileControllerSensitivityOptions
    {
        PCSO_Medium,
        PCSO_Low,
        PCSO_High,
    }
    public enum EProfilePreferredColorOptions
    {
        PPCO_None,
        PPCO_Black,
        PPCO_White,
        PPCO_Yellow,
        PPCO_Orange,
        PPCO_Pink,
        PPCO_Red,
        PPCO_Purple,
        PPCO_Blue,
        PPCO_Green,
        PPCO_Brown,
        PPCO_Silver,
    }
    public enum EProfileAutoAimOptions
    {
        PAAO_Off,
        PAAO_On,
    }
    public enum EProfileAutoCenterOptions
    {
        PACO_Off,
        PACO_On,
    }
    public enum EProfileMovementControlOptions
    {
        PMCO_L_Thumbstick,
        PMCO_R_Thumbstick,
    }
    public enum EProfileRaceTransmissionOptions
    {
        PRTO_Auto,
        PRTO_Manual,
    }
    public enum EProfileRaceCameraLocationOptions
    {
        PRCLO_Behind,
        PRCLO_Front,
        PRCLO_Inside,
    }
    public enum EProfileRaceBrakeControlOptions
    {
        PRBCO_Trigger,
        PRBCO_Button,
    }
    public enum EProfileRaceAcceleratorControlOptions
    {
        PRACO_Trigger,
        PRACO_Button,
    }
    public enum EProfileYInversionOptions
    {
        PYIO_Off,
        PYIO_On,
    }
    public enum EProfileXInversionOptions
    {
        PXIO_Off,
        PXIO_On,
    }
    public enum EProfileControllerVibrationToggleOptions
    {
        PCVTO_Off,
        PCVTO_IgnoreThis,
        PCVTO_IgnoreThis2,
        PCVTO_On,
    }
    public enum EProfileVoiceThruSpeakersOptions
    {
        PVTSO_Off,
        PVTSO_On,
        PVTSO_Both,
    }
    public enum EParticleBurstMethod
    {
        EPBM_Instant,
        EPBM_Interpolated,
    }
    public enum EParticleSubUVInterpMethod
    {
        PSUVIM_None,
        PSUVIM_Linear,
        PSUVIM_Linear_Blend,
        PSUVIM_Random,
        PSUVIM_Random_Blend,
    }
    public enum EEmitterRenderMode
    {
        ERM_Normal,
        ERM_Point,
        ERM_Cross,
        ERM_None,
    }
    public enum EModuleType
    {
        EPMT_General,
        EPMT_TypeData,
        EPMT_Beam,
        EPMT_Trail,
        EPMT_Spawn,
        EPMT_Required,
        EPMT_Event,
    }
    public enum EParticleSourceSelectionMethod
    {
        EPSSM_Random,
        EPSSM_Sequential,
    }
    public enum EAttractorParticleSelectionMethod
    {
        EAPSM_Random,
        EAPSM_Sequential,
    }
    public enum Beam2SourceTargetMethod
    {
        PEB2STM_Default,
        PEB2STM_UserSet,
        PEB2STM_Emitter,
        PEB2STM_Particle,
        PEB2STM_Actor,
    }
    public enum Beam2SourceTargetTangentMethod
    {
        PEB2STTM_Direct,
        PEB2STTM_UserSet,
        PEB2STTM_Distribution,
        PEB2STTM_Emitter,
    }
    public enum BeamModifierType
    {
        PEB2MT_Source,
        PEB2MT_Target,
    }
    public enum EParticleCollisionComplete
    {
        EPCC_Kill,
        EPCC_Freeze,
        EPCC_HaltCollisions,
        EPCC_FreezeTranslation,
        EPCC_FreezeRotation,
        EPCC_FreezeMovement,
    }
    public enum ELocationEmitterSelectionMethod
    {
        ELESM_Random,
        ELESM_Sequential,
    }
    public enum CylinderHeightAxis
    {
        PMLPC_HEIGHTAXIS_X,
        PMLPC_HEIGHTAXIS_Y,
        PMLPC_HEIGHTAXIS_Z,
    }
    public enum EOrbitChainMode
    {
        EOChainMode_Add,
        EOChainMode_Scale,
        EOChainMode_Link,
    }
    public enum EParticleAxisLock
    {
        EPAL_NONE,
        EPAL_X,
        EPAL_Y,
        EPAL_Z,
        EPAL_NEGATIVE_X,
        EPAL_NEGATIVE_Y,
        EPAL_NEGATIVE_Z,
        EPAL_ROTATE_X,
        EPAL_ROTATE_Y,
        EPAL_ROTATE_Z,
    }
    public enum EEmitterDynamicParameterValue
    {
        EDPV_UserSet,
        EDPV_VelocityX,
        EDPV_VelocityY,
        EDPV_VelocityZ,
        EDPV_VelocityMag,
    }
    public enum EParticleScreenAlignment
    {
        PSA_Square,
        PSA_Rectangle,
        PSA_Velocity,
        PSA_TypeSpecific,
    }
    public enum EParticleSortMode
    {
        PSORTMODE_None,
        PSORTMODE_ViewProjDepth,
        PSORTMODE_DistanceToView,
        PSORTMODE_Age_OldestFirst,
        PSORTMODE_Age_NewestFirst,
    }
    public enum EEmitterNormalsMode
    {
        ENM_CameraFacing,
        ENM_Spherical,
        ENM_Cylindrical,
    }
    public enum ETrail2SourceMethod
    {
        PET2SRCM_Default,
        PET2SRCM_Particle,
        PET2SRCM_Actor,
    }
    public enum ETrail2SpawnMethod
    {
        PET2SM_Emitter,
        PET2SM_Velocity,
        PET2SM_Distance,
    }
    public enum ETrailTaperMethod
    {
        PETTM_None,
        PETTM_Full,
        PETTM_Partial,
    }
    public enum EBeamMethod
    {
        PEBM_Distance,
        PEBM_EndPoints,
        PEBM_EndPoints_Interpolated,
        PEBM_UserSet_EndPoints,
        PEBM_UserSet_EndPoints_Interpolated,
    }
    public enum EBeamEndPointMethod
    {
        PEBEPM_Calculated,
        PEBEPM_Distribution,
        PEBEPM_Distribution_Constant,
    }
    public enum EBeam2Method
    {
        PEB2M_Distance,
        PEB2M_Target,
        PEB2M_Branch,
    }
    public enum EBeamTaperMethod
    {
        PEBTM_None,
        PEBTM_Full,
        PEBTM_Partial,
    }
    public enum EMeshScreenAlignment
    {
        PSMA_MeshFaceCameraWithRoll,
        PSMA_MeshFaceCameraWithSpin,
        PSMA_MeshFaceCameraWithLockedAxis,
    }
    public enum EMeshCameraFacingUpAxis
    {
        CameraFacing_NoneUP,
        CameraFacing_ZUp,
        CameraFacing_NegativeZUp,
        CameraFacing_YUp,
        CameraFacing_NegativeYUp,
    }
    public enum EMeshCameraFacingOptions
    {
        XAxisFacing_NoUp,
        XAxisFacing_ZUp,
        XAxisFacing_NegativeZUp,
        XAxisFacing_YUp,
        XAxisFacing_NegativeYUp,
        LockedAxis_ZAxisFacing,
        LockedAxis_NegativeZAxisFacing,
        LockedAxis_YAxisFacing,
        LockedAxis_NegativeYAxisFacing,
        VelocityAligned_ZAxisFacing,
        VelocityAligned_NegativeZAxisFacing,
        VelocityAligned_YAxisFacing,
        VelocityAligned_NegativeYAxisFacing,
    }
    public enum EPhysXMeshRotationMethod
    {
        PMRM_Disabled,
        PMRM_Spherical,
        PMRM_Box,
        PMRM_LongBox,
        PMRM_FlatBox,
        PMRM_Velocity,
    }
    public enum ETrailsRenderAxisOption
    {
        Trails_CameraUp,
        Trails_SourceUp,
        Trails_WorldUp,
    }
    public enum EProcBuildingAxis
    {
        EPBAxis_X,
        EPBAxis_Z,
    }
    public enum EScopeEdge
    {
        EPSA_Top,
        EPSA_Bottom,
        EPSA_Left,
        EPSA_Right,
        EPSA_None,
    }
    public enum EBuildingStatsBrowserColumns
    {
        BSBC_Name,
        BSBC_Ruleset,
        BSBC_NumStaticMeshComps,
        BSBC_NumInstancedStaticMeshComps,
        BSBC_NumInstancedTris,
        BSBC_LightmapMemBytes,
        BSBC_ShadowmapMemBytes,
        BSBC_LODDiffuseMemBytes,
        BSBC_LODLightingMemBytes,
    }
    public enum EPBCornerType
    {
        EPBC_Default,
        EPBC_Chamfer,
        EPBC_Round,
    }
    public enum EProcBuildingEdge
    {
        EPBE_Top,
        EPBE_Bottom,
        EPBE_Left,
        EPBE_Right,
    }
    public enum EPhysEffectType
    {
        EPMET_Impact,
        EPMET_Slide,
    }
    public enum EPhysXDestructibleChunkState
    {
        DCS_StaticRoot,
        DCS_StaticChild,
        DCS_DynamicRoot,
        DCS_DynamicChild,
        DCS_Hidden,
    }
    public enum ESimulationMethod
    {
        ESM_SPH,
        ESM_NO_PARTICLE_INTERACTION,
        ESM_MIXED_MODE,
    }
    public enum EPacketSizeMultiplier
    {
        EPSM_4,
        EPSM_8,
        EPSM_16,
        EPSM_32,
        EPSM_64,
        EPSM_128,
    }
    public enum ESceneCaptureViewMode
    {
        SceneCapView_Lit,
        SceneCapView_Unlit,
        SceneCapView_LitNoShadows,
        SceneCapView_Wire,
    }
    public enum EBioPartGroup
    {
        BIOPARTGROUP_NONE,
        BIOPARTGROUP_INHERIT_FROM_PARENT,
        BIOPARTGROUP_HEAD,
        BIOPARTGROUP_LEFT_LEG,
        BIOPARTGROUP_RIGHT_LEG,
        BIOPARTGROUP_LEFT_ARM,
        BIOPARTGROUP_RIGHT_ARM,
        BIOPARTGROUP_TORSO,
        BIOPARTGROUP_SPECIAL,
    }
    public enum ESleepFamily
    {
        SF_Normal,
        SF_Sensitive,
    }
    public enum ERadialForceType
    {
        RFT_Force,
        RFT_Impulse,
    }
    public enum ERouteFillAction
    {
        RFA_Overwrite,
        RFA_Add,
        RFA_Remove,
        RFA_Clear,
    }
    public enum ERouteDirection
    {
        ERD_Forward,
        ERD_Reverse,
    }
    public enum ERouteType
    {
        ERT_Linear,
        ERT_Loop,
        ERT_Circle,
    }
    public enum EPointSelection
    {
        PS_Normal,
        PS_Random,
        PS_Reverse,
    }
    public enum EMeshType
    {
        MeshType_StaticMesh,
        MeshType_SkeletalMesh,
    }
    public enum EWhoTriggers
    {
        WT_PlayerOnly,
        WT_PlayerOnlyLocal,
        WT_PlayerAndSquad,
        WT_Everyone,
        WT_TagList,
    }
    public enum EParticleEventOutputType
    {
        ePARTICLEOUT_Spawn,
        ePARTICLEOUT_Death,
        ePARTICLEOUT_Collision,
        ePARTICLEOUT_Kismet,
    }
    public enum ESFXSceneDataProcessMode
    {
        SceneProcessMode_InitGroupInst,
        SceneProcessMode_DontCare,
    }
    public enum EPostProcessAAType
    {
        PostProcessAA_Off,
        PostProcessAA_FXAA0,
        PostProcessAA_FXAA1,
        PostProcessAA_FXAA2,
        PostProcessAA_FXAA3,
        PostProcessAA_FXAA4,
        PostProcessAA_FXAA5,
        PostProcessAA_MLAA,
        PostProcessAA_SFX_FXAA,
    }
    public enum TextureFlipBookMethod
    {
        TFBM_UL_ROW,
        TFBM_UL_COL,
        TFBM_UR_ROW,
        TFBM_UR_COL,
        TFBM_LL_ROW,
        TFBM_LL_COL,
        TFBM_LR_ROW,
        TFBM_LR_COL,
        TFBM_RANDOM,
    }
    public enum EBoneControlSpace
    {
        BCS_WorldSpace,
        BCS_ActorSpace,
        BCS_ComponentSpace,
        BCS_ParentBoneSpace,
        BCS_BoneSpace,
        BCS_OtherBoneSpace,
        BCS_BaseMeshSpace,
    }
    public enum ESplineControlRotMode
    {
        SCR_NoChange,
        SCR_AlongSpline,
        SCR_Interpolate,
    }
    public enum SoftBodyBoneType
    {
        SOFTBODYBONE_Fixed,
        SOFTBODYBONE_BreakableAttachment,
        SOFTBODYBONE_TwoWayAttachment,
    }
    public enum EDecompressionType
    {
        DTYPE_Setup,
        DTYPE_Invalid,
        DTYPE_Preview,
        DTYPE_Native,
        DTYPE_RealTime,
        DTYPE_Procedural,
        DTYPE_Xenon,
    }
    public enum ESpeedTreeMeshType
    {
        STMT_MinMinusOne,
        STMT_Branches1,
        STMT_Branches2,
        STMT_Fronds,
        STMT_LeafCards,
        STMT_LeafMeshes,
        STMT_Billboards,
    }
    public enum EWheelSide
    {
        SIDE_None,
        SIDE_Left,
        SIDE_Right,
    }
    public enum ETerrainMappingType
    {
        TMT_Auto,
        TMT_XY,
        TMT_XZ,
        TMT_YZ,
    }
    public enum EMovieStreamSource
    {
        MovieStream_File,
        MovieStream_Memory,
    }
    public enum EUIAnimationLoopMode
    {
        UIANIMLOOP_None,
        UIANIMLOOP_Continuous,
        UIANIMLOOP_Bounce,
    }
    public enum EUIAnimType
    {
        EAT_None,
        EAT_Position,
        EAT_PositionOffset,
        EAT_RelPosition,
        EAT_Rotation,
        EAT_RelRotation,
        EAT_Color,
        EAT_Opacity,
        EAT_Visibility,
        EAT_Scale,
        EAT_Left,
        EAT_Top,
        EAT_Right,
        EAT_Bottom,
        EAT_PPBloom,
        EAT_PPBlurSampleSize,
        EAT_PPBlurAmount,
    }
    public enum EUIAnimationInterpMode
    {
        UIANIMMODE_Linear,
        UIANIMMODE_EaseIn,
        UIANIMMODE_EaseOut,
        UIANIMMODE_EaseInOut,
    }
    public enum EUIAnimNotifyType
    {
        EANT_WidgetFunction,
        EANT_SceneFunction,
        EANT_KismetEvent,
        EANT_Sound,
    }
    public enum EFadeType
    {
        EFT_None,
        EFT_Fading,
        EFT_Pulsing,
    }
    public enum ECalloutButtonLayoutType
    {
        CBLT_None,
        CBLT_DockLeft,
        CBLT_DockRight,
        CBLT_Centered,
        CBLT_Justified,
    }
    public enum EUIListElementState
    {
        ELEMENT_Normal,
        ELEMENT_Active,
        ELEMENT_Selected,
        ELEMENT_UnderCursor,
    }
    public enum EColumnHeaderState
    {
        COLUMNHEADER_Normal,
        COLUMNHEADER_PrimarySort,
        COLUMNHEADER_SecondarySort,
    }
    public enum ECellAutoSizeMode
    {
        CELLAUTOSIZE_None,
        CELLAUTOSIZE_Uniform,
        CELLAUTOSIZE_Constrain,
        CELLAUTOSIZE_AdjustList,
    }
    public enum ECellLinkType
    {
        LINKED_None,
        LINKED_Rows,
        LINKED_Columns,
    }
    public enum EListWrapBehavior
    {
        LISTWRAP_None,
        LISTWRAP_Smooth,
        LISTWRAP_Jump,
    }
    public enum EContextMenuItemType
    {
        CMIT_Normal,
        CMIT_Submenu,
        CMIT_Separator,
        CMIT_Check,
        CMIT_Radio,
    }
    public enum EMenuOptionType
    {
        MENUOT_ComboReadOnly,
        MENUOT_ComboNumeric,
        MENUOT_CheckBox,
        MENUOT_Slider,
        MENUOT_Spinner,
        MENUOT_EditBox,
        MENUOT_CollectionCheckBox,
        MENUOT_CollapsingList,
    }
    public enum EEditBoxCharacterSet
    {
        CHARSET_All,
        CHARSET_NoSpecial,
        CHARSET_AlphaOnly,
        CHARSET_NumericOnly,
        CHARSET_AlphaNumeric,
    }
    public enum EStatsFetchType
    {
        SFT_Player,
        SFT_CenteredOnPlayer,
        SFT_Friends,
        SFT_TopRankings,
    }
    public enum ENavigationLinkType
    {
        NAVLINK_Automatic,
        NAVLINK_Manual,
    }
    public enum EScreenInputMode
    {
        INPUTMODE_None,
        INPUTMODE_Locked,
        INPUTMODE_Selective,
        INPUTMODE_MatchingOnly,
        INPUTMODE_ActiveOnly,
        INPUTMODE_Free,
        INPUTMODE_Simultaneous,
    }
    public enum ESplitscreenRenderMode
    {
        SPLITRENDER_Fullscreen,
        SPLITRENDER_PlayerOwner,
    }
    public enum ESafeRegionType
    {
        ESRT_FullRegion,
        ESRT_TextSafeRegion,
    }
    public enum EWeaponFireType
    {
        EWFT_InstantHit,
        EWFT_Projectile,
        EWFT_Custom,
        EWFT_None,
    }
    public enum ClothBoneType
    {
        CLOTHBONE_Fixed,
        CLOTHBONE_BreakableAttachment,
        CLOTHBONE_TearLine,
    }
    public enum TriangleSortOption
    {
        TRISORT_None,
        TRISORT_CenterRadialDistance,
        TRISORT_Random,
        TRISORT_Tootle,
        TRISORT_MergeContiguous,
        TRISORT_Custom,
    }
    public enum ClothMovementScaleGen
    {
        ECMDM_DistToFixedVert,
        ECMDM_VertexBoneWeight,
        ECMDM_Empty,
    }
    public enum EOnlineGameSearchComparisonType
    {
        OGSCT_Equals,
        OGSCT_NotEquals,
        OGSCT_GreaterThan,
        OGSCT_GreaterThanEquals,
        OGSCT_LessThan,
        OGSCT_LessThanEquals,
    }
    public enum eventEnumID
    {
        OUT_OF_WORLD,
        OUT_OF_TEXTUREMEMORY,
        OUT_OF_SYSTEMMEMORY,
        COMBAT_START,
        COMBAT_END,
        GAME_START,
        GAME_END,
        GAME_LOADGAME,
        GAME_SAVEGAME,
        GAME_PROFILINGTIME,
        CONVERSATION_MISSINGVO,
        CONVERSATION_MISSINGLIPSYNC,
        CONVERSATION_FAILEDSTAGING,
        CONVERSATION_START,
        CONVERSATION_END,
        CONVERSATION_SKIPPEDLINE,
        CONVERSATION_SELECTRESPONSE,
        CONVERSATION_NODETRANSITION,
        PAWN_DEATH,
        PAWN_LEVELUP,
        PAWN_FAILEDPATHFIND,
        PAWN_TELEPORT,
        PAWN_USEPLACEABLE,
        PAWN_USEPOWER,
        PAWN_USEGRENADE,
        OUT_OF_TRIGGERSTREAM,
        BAD_STREAMING,
        SLOW_STREAMING,
        ERROR_LOADING,
        ERROR_NOAREAMAP,
        GAME_ENTERMAP,
        GAME_EXITMAP,
        PLACEABLE_STATECHANGE,
        PLOTSTATE_CHANGE,
        GAME_STATISTICS,
        SCRIPTING_FAILED,
        SCRIPTING_PASSED,
        USE_COVER,
        TREASURE,
        MISC_DEBUG,
        PURPLE_LEVEL,
        USE_ZOOM,
        PLAYER_DEALTDAMAGE,
        PLAYER_TOOKDAMAGE,
        PLAYER_FIREDWEAPON,
        PLAYER_DREWWEAPON,
        PLAYER_OBTAINEDMEDIGEL,
        PLAYER_OBTAINEDCREDITS,
        PLAYER_STARTEDSTORM,
        PLAYER_ENDEDSTORM,
        AUTOMATION_START,
        AUTOMATION_WARNING,
        AUTOMATION_ERROR,
        AUTOMATION_PRINT,
        AUTOMATION_END,
        AUTOMATION_OPERROR,
        TEXTUREMEMORY_SACRIFICED,
        PAWN_KILL_INFO,
        PLAYER_OBTAINEDEEZO,
        PLAYER_OBTAINEDIRIDIUM,
        PLAYER_OBTAINEDPALLADIUM,
        PLAYER_OBTAINEDAMMO,
        PLAYER_OBTAINEDPLATINUM,
        TEXTUREMEMORY_FACTOR,
        PLAYER_OBTAINEDPROBES,
        BLOCKING_ADDTOWORLD,
        ENDGM1,
        ENDGM2,
        ENDGM3,
        PLAYER_OUTOFAMMO,
        PAWN_AIBARK,
        CONVERSATION_ENTRYNODE,
        CONVERSATION_REPLYNODE,
        CONVERSATION_MISCLOG,
        PLAYER_NOTFUN,
        CONVAMBIENT_IGNOREBODYGESTURESNOTSET,
        STRREF_NOT_FOUND,
        PLAYER_OBTAINEDFUEL,
        VSYNC_ENABLED,
        LEVEL_LOAD_TIME,
        BLAZE_LOGIN_INFO,
        BLAZE_TELEMETRY,
        STRING_LAST_USE,
        PLAYER_OBTAINEDGRENADE,
        UNIT_TEST_RESULT,
        PACKAGE_HAS_LOAD_WARNINGS,
        PACKAGE_HAS_LOAD_ERRORS,
        KISMET_MAP_REFERENCE,
        KISMET_SEQUENCE_COUNT,
        PATHNODE_NETWORK_SIZE,
        PATHNODE_COUNT,
        PATHNODE_ONE_WAY,
        PATHNODE_DESTINATION_ONLY,
        PATHNODE_UNMATCHED,
        PATHNODE_SOURCE_ONLY,
        JUMPNODE_BAD_DISTANCE,
        JUMPNODE_NO_BLOCKVOL,
        BLOCKING_VOLUME_COUNT,
        BLOCKING_VOLUME_COMPLEXCOLLISION,
        TEXTURE_SIZE,
        TEXTURE_NOMIPS,
        PAWN_LOC_ONPLAYERDEATH,
        PLAYER_LOC_ONPAWNDEATH,
        PLAYER_OBTAINEDPICKUP,
        DRAWSCALE_NEARZERO,
        DRAWSCALE_PHYSICS_INVALID,
        PATHNODE_OUTSIDE_STREAMINGTRIGGER,
        PATHNODE_LINKED_EXTERNAL_CHUNKS,
        HENCHMEN_SELECTED,
        MAP_PLAYED_THROUGH_COMPLETELY,
        FAST_RESUME_LOAD_TIME,
        GAWLOG_AWARD_ASSET,
        GAWLOG_MODIFY_ASSET,
        GAWLOG_END_GAME_OPTIONS,
        GAWLOG_ENG_GAME_OPTION_CHOSEN,
        GAWLOG_CONFLICT_ZONE_UPDATED,
        GAWLOG_PLACEHOLDER_2,
        GAWLOG_PLACEHOLDER_3,
        CONVERSATION_PLAYEDFOVO,
        KISMET_DUPLICATE_EVENT_COUNT,
    }
    public enum EUIAutoSizeConstraintType
    {
        UIAUTOSIZEREGION_Minimum,
        UIAUTOSIZEREGION_Maximum,
    }
    public enum EShakeParam
    {
        ESP_OffsetRandom,
        ESP_OffsetZero,
    }
    public enum GFxTimingMode
    {
        TM_Game,
        TM_Real,
    }
    public enum GFxRenderTextureMode
    {
        RTM_Opaque,
        RTM_Alpha,
        RTM_AlphaComposite,
    }
    public enum ASType
    {
        AS_Undefined,
        AS_Null,
        AS_Number,
        AS_String,
        AS_Boolean,
    }
    public enum GFxAlign
    {
        Align_Center,
        Align_TopCenter,
        Align_BottomCenter,
        Align_CenterLeft,
        Align_CenterRight,
        Align_TopLeft,
        Align_TopRight,
        Align_BottomLeft,
        Align_BottomRight,
    }
    public enum GFxScaleMode
    {
        SM_NoScale,
        SM_ShowAll,
        SM_ExactFit,
        SM_NoBorder,
    }
    public enum ELinkMode
    {
        MODE_Text,
        MODE_Line,
        MODE_Binary,
    }
    public enum ELineMode
    {
        LMODE_auto,
        LMODE_DOS,
        LMODE_UNIX,
        LMODE_MAC,
    }
    public enum EReceiveMode
    {
        RMODE_Manual,
        RMODE_Event,
    }
    public enum EMeshBeaconPacketType
    {
        MB_Packet_UnknownType,
        MB_Packet_ClientNewConnectionRequest,
        MB_Packet_ClientBeginBandwidthTest,
        MB_Packet_ClientCreateNewSessionResponse,
        MB_Packet_HostNewConnectionResponse,
        MB_Packet_HostBandwidthTestRequest,
        MB_Packet_HostCompletedBandwidthTest,
        MB_Packet_HostTravelRequest,
        MB_Packet_HostCreateNewSessionRequest,
        MB_Packet_DummyData,
        MB_Packet_Heartbeat,
    }
    public enum EMeshBeaconConnectionResult
    {
        MB_ConnectionResult_Succeeded,
        MB_ConnectionResult_Duplicate,
        MB_ConnectionResult_Timeout,
        MB_ConnectionResult_Error,
    }
    public enum EMeshBeaconBandwidthTestState
    {
        MB_BandwidthTestState_NotStarted,
        MB_BandwidthTestState_RequestPending,
        MB_BandwidthTestState_StartPending,
        MB_BandwidthTestState_InProgress,
        MB_BandwidthTestState_Completed,
        MB_BandwidthTestState_Incomplete,
        MB_BandwidthTestState_Timeout,
        MB_BandwidthTestState_Error,
    }
    public enum EMeshBeaconBandwidthTestResult
    {
        MB_BandwidthTestResult_Succeeded,
        MB_BandwidthTestResult_Timeout,
        MB_BandwidthTestResult_Error,
    }
    public enum EMeshBeaconBandwidthTestType
    {
        MB_BandwidthTestType_Upstream,
        MB_BandwidthTestType_Downstream,
        MB_BandwidthTestType_RoundtripLatency,
    }
    public enum EMeshBeaconClientState
    {
        MBCS_None,
        MBCS_Connecting,
        MBCS_Connected,
        MBCS_ConnectionFailed,
        MBCS_AwaitingResponse,
        MBCS_Closed,
    }
    public enum EEventUploadType
    {
        EUT_GenericStats,
        EUT_ProfileData,
        EUT_HardwareData,
        EUT_MatchmakingData,
    }
    public enum EReservationPacketType
    {
        RPT_UnknownPacketType,
        RPT_ClientReservationRequest,
        RPT_ClientReservationUpdateRequest,
        RPT_ClientCancellationRequest,
        RPT_HostReservationResponse,
        RPT_HostReservationCountUpdate,
        RPT_HostTravelRequest,
        RPT_HostIsReady,
        RPT_HostHasCancelled,
        RPT_Heartbeat,
    }
    public enum EPartyReservationResult
    {
        PRR_GeneralError,
        PRR_PartyLimitReached,
        PRR_IncorrectPlayerCount,
        PRR_RequestTimedOut,
        PRR_ReservationDuplicate,
        PRR_ReservationNotFound,
        PRR_ReservationAccepted,
    }
    public enum EPartyBeaconClientState
    {
        PBCS_None,
        PBCS_Connecting,
        PBCS_Connected,
        PBCS_ConnectionFailed,
        PBCS_AwaitingResponse,
        PBCS_Closed,
    }
    public enum EPartyBeaconClientRequest
    {
        PBClientRequest_NewReservation,
        PBClientRequest_UpdateReservation,
    }
    public enum ELinkState
    {
        STATE_Initialized,
        STATE_Ready,
        STATE_Listening,
        STATE_Connecting,
        STATE_Connected,
        STATE_ListenClosePending,
        STATE_ConnectClosePending,
        STATE_ListenClosing,
        STATE_ConnectClosing,
    }
    public enum ERequestType
    {
        Request_GET,
        Request_POST,
    }
    public enum EAssassinationEvent
    {
        EA_TargetKilled,
        EA_TargetSpawned,
    }
    public enum ELocationType
    {
        LT_Known,
        LT_Interp,
        LT_Exact,
    }
    public enum EPerceptionType
    {
        PT_Sight,
        PT_Heard,
        PT_HurtBy,
        PT_NotifySight,
        PT_Force,
    }
    public enum eWalkWaypointsTypes
    {
        WWT_Linear,
        WWT_Looping,
        WWT_OutAndBack,
        WWT_OutAndBackLooping,
        WWT_Random,
    }
    public enum EAimInputType
    {
        AimInput_Pawn,
        AimInput_Vehicle,
        AimInput_Kismet,
        AimInput_PawnMotion,
    }
    public enum EBioAnimBlendDirection
    {
        eBioAnimBlend_NOBLEND,
        eBioAnimBlend_BLENDUP,
        eBioAnimBlend_BLENDDOWN,
        eBioAnimBlend_BLENDDIRECT,
    }
    public enum EBioAnimAdditive
    {
        eBioAnimAdd_Primary,
        eBioAnimAdd_Additive,
        eBioAnimAdd_BasePose,
    }
    public enum EBioActionAnimNode
    {
        ACTION_ANIM_NODE_POSTURE,
        ACTION_ANIM_NODE_MOUNT,
        ACTION_ANIM_NODE_HESITATE,
        ACTION_ANIM_NODE_FALL,
        ACTION_ANIM_NODE_RAGDOLL,
        ACTION_ANIM_NODE_SNAPSHOT,
        ACTION_ANIM_NODE_DIE,
        ACTION_ANIM_NODE_TECH,
        ACTION_ANIM_NODE_MATINEE,
        ACTION_ANIM_NODE_GETUP,
        ACTION_ANIM_NODE_GESTURES,
    }
    public enum EBioAnimNodeBlendByAim
    {
        eBioAnimNodeBlendByAim_LevelFront,
        eBioAnimNodeBlendByAim_LevelLeft,
        eBioAnimNodeBlendByAim_LevelRight,
        eBioAnimNodeBlendByAim_UpFront,
        eBioAnimNodeBlendByAim_UpLeft,
        eBioAnimNodeBlendByAim_UpRight,
        eBioAnimNodeBlendByAim_DownFront,
        eBioAnimNodeBlendByAim_DownLeft,
        eBioAnimNodeBlendByAim_DownRight,
    }
    public enum EBioAnimDamage
    {
        eBioAnimDamage_Front,
        eBioAnimDamage_Rear,
        eBioAnimDamage_Left,
        eBioAnimDamage_Right,
    }
    public enum EBioAnimDeath
    {
        eBioAnimDeath_Head,
        eBioAnimDeath_Stomach,
        eBioAnimDeath_ArmLeft,
        eBioAnimDeath_ArmRight,
        eBioAnimDeath_LegLeft,
        eBioAnimDeath_LegRight,
    }
    public enum EBioAnimNodeBlendByFireSequenceChild
    {
        BIO_ANIM_NODE_BLEND_BY_FIRE_SEQUENCE_CHILD_IDLE,
        BIO_ANIM_NODE_BLEND_BY_FIRE_SEQUENCE_CHILD_START,
        BIO_ANIM_NODE_BLEND_BY_FIRE_SEQUENCE_CHILD_LOOP,
        BIO_ANIM_NODE_BLEND_BY_FIRE_SEQUENCE_CHILD,
        BIO_ANIM_NODE_BLEND_BY_FIRE_SEQUENCE_CHILD_END,
    }
    public enum EBioAnimIncline
    {
        eBioAnimIncline_Up,
        eBioAnimIncline_Level,
        eBioAnimIncline_Down,
    }
    public enum EBioAnimNodePower
    {
        eBioAnimNodePower_Idle,
        eBioAnimNodePower_Casting,
        eBioAnimNodePower_Release,
        eBioAnimNodePower_Using,
    }
    public enum EBioAnimNodePowerNotifyActive
    {
        eBioAnimNodePowerNotifyActive_None,
        eBioAnimNodePowerNotifyActive_Casting,
        eBioAnimNodePowerNotifyActive_Release,
    }
    public enum EBioReloadAnimNode
    {
        RELOAD_ANIM_NODE_IDLE,
        RELOAD_ANIM_NODE_RELOADING,
    }
    public enum EBioAnimNodeBlendByStorm
    {
        eBioAnimNodeBlendByStorm_Idle,
        eBioAnimNodeBlendByStorm_Storm,
    }
    public enum WeaponAnimState
    {
        WeaponState_Expanded,
        WeaponState_Expanding,
        WeaponState_Collapsing,
        WeaponState_Collapsed,
    }
    public enum EBioAnimNodeBlendByWeaponEquip
    {
        eBioAnimNodeBlendByWeaponEquip_Idle,
        eBioAnimNodeBlendByWeaponEquip_Draw,
        eBioAnimNodeBlendByWeaponEquip_Holster,
    }
    public enum EBioAnimStartDirection
    {
        eBioAnimDirStart_ForwardRight,
        eBioAnimDirStart_ForwardLeft,
        eBioAnimDirStart_Right,
        eBioAnimDirStart_Left,
        eBioAnimDirStart_BackwardRight,
        eBioAnimDirStart_BackwardLeft,
    }
    public enum EBioAnimNodeFall
    {
        eBioAnimNodeFall_Falling,
        eBioAnimNodeFall_Landing,
    }
    public enum EBioAnimNodeBlendMovement
    {
        eBioAnimNodeBlendMovement_Idle,
        eBioAnimNodeBlendMovement_Walk,
        eBioAnimNodeBlendMovement_Run,
    }
    public enum EBioAnimMoveStop
    {
        eBioAnimMoveStop_StopRight,
        eBioAnimMoveStop_StopLeft,
    }
    public enum EBoneBlendType
    {
        BLENDTYPE_ALWAYS,
        BLENDTYPE_ALWAYS_BONE_SWITCH,
        BLENDTYPE_CROSSFADE_BONE_SWITCH,
        BLENDTYPE_SWITCH,
        BLENDTYPE_TOGGLE,
        BLENDTYPE_WEIGHT,
    }
    public enum EBoneBlendTestType
    {
        BLENDTESTTYPE_NONE,
        BLENDTESTTYPE_ANIM,
        BLENDTESTTYPE_BONE,
    }
    public enum EBioAnimNodeBlendScalarMovementBehavior
    {
        BScMv_None,
        BScMv_TurnAngle,
        BScMv_SpeedVelocity,
        BScMv_SpeedTacticalVelocity,
        BScMv_AxisDirection,
    }
    public enum EBioAnimNodeBlendScalarMoveSpeedStates
    {
        BScMvSS_Idle,
        BScMvSS_Walk,
        BScMvSS_Run,
        BScMvSS_Sprint,
    }
    public enum EBioAnimNodeBlendScalarMoveAxis
    {
        BScMvAxis_All,
        BScMvAxis_2D,
        BScMvAxis_X,
        BScMvAxis_Y,
        BScMvAxis_Z,
    }
    public enum EBioAnimNodeBlendScalarMoveAxisDir
    {
        BScMvAxisDir_X,
        BScMvAxisDir_Y,
        BScMvAxisDir_Z,
    }
    public enum EBioAnimNodeBlendScalarMoveAxisDirMode
    {
        BScMvAxisDirMode_WorldRotation,
        BScMvAxisDirMode_WorldVelDir,
        BScMvAxisDirMode_WorldAccelDir,
        BScMvAxisDirMode_LocalVelDir,
        BScMvAxisDirMode_LocalAccelDir,
    }
    public enum EBioAnimSkidTurn
    {
        eBioAnimSkidTurn_StartRight,
        eBioAnimSkidTurn_StartLeft,
        eBioAnimSkidTurn_TurnRightNear,
        eBioAnimSkidTurn_TurnRightFar,
        eBioAnimSkidTurn_TurnLeftNear,
        eBioAnimSkidTurn_TurnLeftFar,
    }
    public enum EBioAnim_SpeedType
    {
        eBioAnim_SpeedStandard,
        eBioAnim_SpeedStarting,
        eBioAnim_SpeedSnapshot,
    }
    public enum EBioBlendStatePlayMode
    {
        eBioBlendStatePlayMode_None,
        eBioBlendStatePlayMode_OneShot,
        eBioBlendStatePlayMode_Looping,
        eBioBlendStatePlayMode_Query,
    }
    public enum EBioBlendStatePlayAction
    {
        eBioBlendStatePlayAction_NoAction,
        eBioBlendStatePlayAction_Play,
        eBioBlendStatePlayAction_Stop,
        eBioBlendStatePlayAction_Reset,
        eBioBlendStatePlayAction_PlayFromStart,
        eBioBlendStatePlayAction_PlayFromTime,
    }
    public enum EBioAnimNodeBlendStateActionBehavior
    {
        BSAct_None,
        BSAct_PawnState,
        BSAct_PawnGesturesState,
        BSAct_ActiveState,
        BSAct_Posture,
        BSAct_ArtPlaceable,
        BSAct_IdleState,
    }
    public enum EBioPawnAnimActionStates
    {
        PAAS_Posture,
        PAAS_Dying,
        PAAS_Death,
        PAAS_Matinee,
        PAAS_Recover,
        PAAS_Gestures,
    }
    public enum EBioPawnAnimActiveStates
    {
        PAActiveS_Active,
        PAActiveS_ActiveToInactive,
        PAActiveS_InactiveToActive,
        PAActiveS_Inactive,
    }
    public enum EBioArtPlaceableActionStates
    {
        APAS_Default,
        APAS_Matinee,
    }
    public enum EBioAnimNodeBlendStateCombatBehavior
    {
        BSCbt_None,
        BSCbt_CoverSwitch,
        BSCbt_CoverDirection,
        BSCbt_CoverState,
        BSCbt_CoverBlocked,
        BSCbt_CombatSwitch,
        BSCbt_CoverPredictDirection,
        BSCbt_CoverBlockType,
    }
    public enum EBioAnimNodeBlendStateMovementBehavior
    {
        BSMove_None,
        BSMove_SpeedVelocity,
        BSMove_SpeedTacticalVelocity,
        BSMove_ScaleRate,
        BSMove_ScaleRateByWalkSpeed,
        BSMove_ScaleRateByRunSpeed,
        BSMove_ScaleRateBySprintSpeed,
        BSMove_ScaleRateByTacticalWalkSpeed,
        BSMove_ScaleRateByTacticalRunSpeed,
        BSMove_LookAtTurning,
        BSMove_TurningDirection,
        BSMove_AxisDirection,
        BSMove_FlyingState,
        BSMove_StopSwitch,
        BSMove_StopOnFoot,
        BSMove_StartSwitch,
        BSMove_ScaleRateByWalkRunRatio,
        BSMove_SkidTurnSwitch,
    }
    public enum EBioMovementSpeedStates
    {
        MSS_Idle,
        MSS_Walk,
        MSS_Run,
        MSS_Sprint,
    }
    public enum EBioAnimNodeBlendStateMoveAxisDir
    {
        BSMoveAxisDir_X,
        BSMoveAxisDir_Y,
        BSMoveAxisDir_Z,
    }
    public enum EBioAnimNodeBlendStateMoveAxisDirMode
    {
        BSMoveAxisDirMode_WorldRotation,
        BSMoveAxisDirMode_WorldVelDir,
        BSMoveAxisDirMode_WorldAccelDir,
        BSMoveAxisDirMode_LocalVelDir,
        BSMoveAxisDirMode_LocalAccelDir,
    }
    public enum EBioAnimNodeBlendStrafe
    {
        eBioAnimNodeBlendStrafe_Forward,
        eBioAnimNodeBlendStrafe_Backward,
        eBioAnimNodeBlendStrafe_Left,
        eBioAnimNodeBlendStrafe_Right,
    }
    public enum EBioAnimNodeBlendTurn
    {
        eBioAnimNodeBlendTurn_Idle,
        eBioAnimNodeBlendTurn_TurnLeft,
        eBioAnimNodeBlendTurn_TurnRight,
    }
    public enum EBioAnimNodeCombatModeChild
    {
        BIO_ANIM_NODE_COMBAT_MODE_CHILD_NONCOMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_CHILD_COMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_CHILD_ENTERCOMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_CHILD_EXITCOMBAT,
    }
    public enum EBioAnimNodeCombatModeState
    {
        BIO_ANIM_NODE_COMBAT_MODE_STATE_NONCOMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_STATE_COMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_STATE_ANIMATING_TO_COMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_STATE_ANIMATING_TO_NONCOMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_STATE_BLENDING_TO_COMBAT,
        BIO_ANIM_NODE_COMBAT_MODE_STATE_BLENDING_TO_NONCOMBAT,
    }
    public enum EBioAnimNodeCover2Actions
    {
        eBioAnimNodeCover2Actions_Default,
        eBioAnimNodeCover2Actions_Lean,
        eBioAnimNodeCover2Actions_PopUp,
        eBioAnimNodeCover2Actions_PeekSide,
        eBioAnimNodeCover2Actions_PeekUp,
        eBioAnimNodeCover2Actions_PartialLean,
        eBioAnimNodeCover2Actions_PartialPopUp,
        eBioAnimNodeCover2Actions_Aimback,
    }
    public enum EBioAnimNodeCover2ChangeDirection
    {
        eBioAnimNodeCover2ChangeDirection_Idle,
        eBioAnimNodeCover2ChangeDirection_TransitionToMirror,
        eBioAnimNodeCover2ChangeDirection_TransitionToDefault,
    }
    public enum EBioAnimNodeCover2Move
    {
        eBioAnimNodeCover2Move_Idle,
        eBioAnimNodeCover2Move_Move,
    }
    public enum EBioAnimNodeCover2Neutral
    {
        eBioAnimNodeCover2Neutral_Idle,
        eBioAnimNodeCover2Neutral_Active,
    }
    public enum EBioAnimNodeCover2Transition
    {
        eBioAnimNodeCover2Transition_Intro,
        eBioAnimNodeCover2Transition_Body,
        eBioAnimNodeCover2Transition_Outro,
    }
    public enum EBioAnimNodeLocomotion
    {
        eBioAnimNodeLocomotion_Idle,
        eBioAnimNodeLocomotion_MoveStart,
        eBioAnimNodeLocomotion_Moving,
        eBioAnimNodeLocomotion_MoveStop,
        eBioAnimNodeLocomotion_SkidTurn,
        eBioAnimNodeLocomotion_PoseHolder,
    }
    public enum EBioAnimNodeLocomotionMoving
    {
        eBioAnimNodeLocomotionMoving_Fwd,
        eBioAnimNodeLocomotionMoving_LeanLeft,
        eBioAnimNodeLocomotionMoving_LeanRight,
        eBioAnimNodeLocomotionMoving_Ascend,
        eBioAnimNodeLocomotionMoving_Descend,
    }
    public enum EBioAnimNodeLocomotionSpeed
    {
        eBioAnimNodeLocomotionSpeed_Idle,
        eBioAnimNodeLocomotionSpeed_Walk,
        eBioAnimNodeLocomotionSpeed_Run,
    }
    public enum EBioAnimNodeLocomotionStart
    {
        eBioAnimNodeLocomotionStart_ForwardRight,
        eBioAnimNodeLocomotionStart_ForwardLeft,
        eBioAnimNodeLocomotionStart_Right,
        eBioAnimNodeLocomotionStart_Left,
        eBioAnimNodeLocomotionStart_BackwardRight,
        eBioAnimNodeLocomotionStart_BackwardLeft,
    }
    public enum EBioAnimNodeLocomotionStop
    {
        eBioAnimNodeLocomotionStop_LeftFoot,
        eBioAnimNodeLocomotionStop_RightFoot,
    }
    public enum EBioCapMode
    {
        BIO_CAPMODE_WEAPON,
        BIO_CAPMODE_BIOTICS,
        BIO_CAPMODE_TECH,
        BIO_CAPMODE_COMBAT,
        BIO_CAPMODE_GRENADES,
        BIO_CAPMODE_ERROR,
    }
    public enum ECustomizableElementType
    {
        CustomizableType_None,
        CustomizableType_Torso,
        CustomizableType_Shoulders,
        CustomizableType_Arms,
        CustomizableType_Legs,
        CustomizableType_Helmet,
        CustomizableType_Spec,
        CustomizableType_Tint,
        CustomizableType_Pattern,
        CustomizableType_PatternColor,
        CustomizableType_Emissive,
    }
    public enum ELookAtTransitionType
    {
        LookAt_Default,
        LookAt_Locked,
        LookAt_InstantTrans,
    }
    public enum EStateEventElementTypes
    {
        SEE_Bool,
        SEE_Consequence,
        SEE_Float,
        SEE_Function,
        SEE_Int,
        SEE_LocalBool,
        SEE_LocalFloat,
        SEE_LocalInt,
        SEE_Substate,
    }
    public enum ECharacterType
    {
        CharacterType_None,
        CharacterType_Human,
        CharacterType_Cerberus_Trooper,
        CharacterType_Cerberus_Centurion,
        CharacterType_Cerberus_Guardian,
        CharacterType_Cerberus_Nemesis,
        CharacterType_Cerberus_Phantom,
        CharacterType_Cerberus_Engineer,
        CharacterType_Cerberus_Atlas,
        CharacterType_Reaper_Husk,
        CharacterType_Reaper_Cannibal,
        CharacterType_Reaper_Marauder,
        CharacterType_Reaper_Brute,
        CharacterType_Reaper_Ravager,
        CharacterType_Reaper_Banshee,
        CharacterType_Geth_Trooper,
        CharacterType_Geth_Rocket_Trooper,
        CharacterType_Geth_Pyro,
        CharacterType_Geth_Hunter,
        CharacterType_Geth_Prime,
    }
    public enum EChallengeType
    {
        ChallengeType_None,
        ChallengeType_Minion,
        ChallengeType_Elite,
        ChallengeType_SubBoss,
        ChallengeType_Boss,
    }
    public enum EAffiliationType
    {
        AffiliationType_None,
        AffiliationType_GenericMerc,
        AffiliationType_Reaper,
        AffiliationType_Cerberus,
        AffiliationType_Geth,
    }
    public enum ERaceType
    {
        RaceType_None,
        RaceType_Humanoid,
        RaceType_Machine,
        RaceType_Animal,
    }
    public enum EResistanceType
    {
        ResistanceType_None,
        ResistanceType_Shield,
        ResistanceType_Biotic,
        ResistanceType_Armour,
    }
    public enum EGuiHandlers
    {
        GUI_HANDLER_NONE,
        GUI_HANDLER_INVENTORY,
        GUI_HANDLER_INGAMEGUI,
        GUI_HANDLER_CHARACTER_RECORD,
        GUI_HANDLER_LOOT,
        GUI_HANDLER_CONVERSATION,
        GUI_HANDLER_SHOP,
        GUI_HANDLER_GALAXYMAP,
        GUI_HANDLER_MAINMENU,
        GUI_HANDLER_NEW_CHARACTER,
        GUI_HANDLER_SELECT_CHARACTER,
        GUI_HANDLER_JOURNAL,
        GUI_HANDLER_HUD,
        GUI_HANDLER_PARTYSELECT,
        GUI_HANDLER_XMODS,
        GUI_HANDLER_SQUADCOMMAND,
        GUI_HANDLER_DATACODEX,
        GUI_HANDLER_SAVELOAD,
        GUI_HANDLER_ACHIEVEMENT,
        GUI_HANDLER_AREAMAP,
        GUI_HANDLER_SHAREDINGAMEGUI,
        GUI_HANDLER_MENUBROWSER,
        GUI_HANDLER_GAMEOVER,
        GUI_HANDLER_SPECIALIZATION,
        GUI_HANDLER_MESSAGEBOX,
        GUI_HANDLER_INTROTEXT,
        GUI_HANDLER_BLACKSCREEN,
        GUI_HANDLER_CREDITS,
        GUI_HANDLER_OPTIONS,
        GUI_HANDLER_ADDITONALCONTENT,
        GUI_HANDLER_SKILLGAME,
        GUI_HANDLER_SPLASH_SCREEN,
        GUI_HANDLER_REPLAYCHARACTERSELECT,
        GUI_HANDLER_CHOICEGUI,
        GUI_HANDLER_SNIPEROVERLAY,
    }
    public enum BioGuiEvents
    {
        BIOGUI_EVENT_ON_ENTER,
        BIOGUI_EVENT_ON_EXIT,
        BIOGUI_EVENT_AXIS_LSTICK_X,
        BIOGUI_EVENT_AXIS_LSTICK_Y,
        BIOGUI_EVENT_AXIS_RSTICK_X,
        BIOGUI_EVENT_AXIS_RSTICK_Y,
        BIOGUI_EVENT_AXIS_MOUSE_X,
        BIOGUI_EVENT_AXIS_MOUSE_Y,
        BIOGUI_EVENT_KEY_WHEEL_UP,
        BIOGUI_EVENT_KEY_WHEEL_DOWN,
        BIOGUI_EVENT_CONTROL_COOLDOWN_EXPIRE,
        BIOGUI_EVENT_CONTROL_DOWN,
        BIOGUI_EVENT_CONTROL_LEFT,
        BIOGUI_EVENT_CONTROL_RIGHT,
        BIOGUI_EVENT_CONTROL_UP,
        BIOGUI_EVENT_BUTTON_A,
        BIOGUI_EVENT_BUTTON_B,
        BIOGUI_EVENT_BUTTON_X,
        BIOGUI_EVENT_BUTTON_Y,
        BIOGUI_EVENT_BUTTON_LT,
        BIOGUI_EVENT_BUTTON_RT,
        BIOGUI_EVENT_BUTTON_LB,
        BIOGUI_EVENT_BUTTON_RB,
        BIOGUI_EVENT_BUTTON_BACK,
        BIOGUI_EVENT_BUTTON_START,
        BIOGUI_EVENT_BUTTON_LTHUMB,
        BIOGUI_EVENT_BUTTON_RTHUMB,
        BIOGUI_EVENT_KEY_ESCAPE,
        BIOGUI_EVENT_KEY_DELETE,
        BIOGUI_EVENT_KEY_TAB,
        BIOGUI_EVENT_MOUSE_BUTTON_RIGHT,
        BIOGUI_EVENT_MOUSE_BUTTON_LEFT,
        BIOGUI_EVENT_CONTROL_DOWN_RELEASE,
        BIOGUI_EVENT_CONTROL_LEFT_RELEASE,
        BIOGUI_EVENT_CONTROL_RIGHT_RELEASE,
        BIOGUI_EVENT_CONTROL_UP_RELEASE,
        BIOGUI_EVENT_BUTTON_A_RELEASE,
        BIOGUI_EVENT_BUTTON_B_RELEASE,
        BIOGUI_EVENT_BUTTON_X_RELEASE,
        BIOGUI_EVENT_BUTTON_Y_RELEASE,
        BIOGUI_EVENT_BUTTON_LT_RELEASE,
        BIOGUI_EVENT_BUTTON_RT_RELEASE,
        BIOGUI_EVENT_BUTTON_LB_RELEASE,
        BIOGUI_EVENT_BUTTON_RB_RELEASE,
        BIOGUI_EVENT_BUTTON_BACK_RELEASE,
        BIOGUI_EVENT_BUTTON_START_RELEASE,
        BIOGUI_EVENT_BUTTON_LTHUMB_RELEASE,
        BIOGUI_EVENT_BUTTON_RTHUMB_RELEASE,
        BIOGUI_EVENT_KEY_ESCAPE_RELEASE,
        BIOGUI_EVENT_KEY_DELETE_RELEASE,
        BIOGUI_EVENT_KEY_TAB_RELEASE,
        BIOGUI_EVENT_MOUSE_BUTTON_RIGHT_RELEASE,
        BIOGUI_EVENT_MOUSE_BUTTON_LEFT_RELEASE,
    }
    public enum BioThumbstickDir
    {
        BTD_Centered,
        BTD_Negative,
        BTD_Positive,
    }
    public enum SFMovieStrokeStyle
    {
        SF_MSS_Correct,
        SF_MSS_Normal,
        SF_MSS_Hairline,
    }
    public enum BioTutorialPosition
    {
        BTP_Top,
        BTP_Bottom,
        BTP_MessageBox,
    }
    public enum SFXDisplayableSquadCommands
    {
        SFX_DSC_READY,
        SFX_DSC_ATTACK,
        SFX_DSC_MOVETO,
        SFX_DSC_FOLLOW,
    }
    public enum Cerberus3DState
    {
        C3D_Default,
        C3D_RightPanel_Open,
        C3D_RightPanel_Closed,
        C3D_RightPanel_Opening,
        C3D_RightPanel_Closing,
    }
    public enum BioMessageBoxIconSets
    {
        ICONSET_None,
        ICONSET_Manufacturer,
        ICONSET_Combat,
        ICONSET_Plot,
        ICONSET_ItemProperties,
    }
    public enum SFXHintPosition
    {
        SFXHINTPOS_Top,
        SFXHINTPOS_Middle,
        SFXHINTPOS_Bottom,
    }
    public enum SFXXBoxHintIcon
    {
        XBICON_None,
        XBICON_XBOX_A,
        XBICON_XBOX_B,
        XBICON_XBOX_Y,
        XBICON_XBOX_X,
        XBICON_XBOX_Right_Thumb,
        XBICON_XBOX_Right_Thumb_Pressed,
        XBICON_XBOX_Right_Thumb_Released,
        XBICON_XBOX_Left_Thumb,
        XBICON_XBOX_Left_Thumb_Pressed,
        XBICON_XBOX_Left_Thumb_Released,
        XBICON_XBOX_Right_Thumb_Up,
        XBICON_XBOX_Right_Thumb_UpRight,
        XBICON_XBOX_Right_Thumb_Right,
        XBICON_XBOX_Right_Thumb_DownRight,
        XBICON_XBOX_Right_Thumb_Down,
        XBICON_XBOX_Right_Thumb_DownLeft,
        XBICON_XBOX_Right_Thumb_Left,
        XBICON_XBOX_Right_Thumb_UpLeft,
        XBICON_XBOX_Left_Thumb_Up,
        XBICON_XBOX_Left_Thumb_UpRight,
        XBICON_XBOX_Left_Thumb_Right,
        XBICON_XBOX_Left_Thumb_DownRight,
        XBICON_XBOX_Left_Thumb_Down,
        XBICON_XBOX_Left_Thumb_DownLeft,
        XBICON_XBOX_Left_Thumb_Left,
        XBICON_XBOX_Left_Thumb_UpLeft,
        XBICON_XBOX_DPad,
        XBICON_XBOX_DPad_Up,
        XBICON_XBOX_DPad_Right,
        XBICON_XBOX_DPad_Down,
        XBICON_XBOX_DPad_Left,
        XBICON_XBOX_Start,
        XBICON_XBOX_Back,
        XBICON_XBOX_Right_Shoulder,
        XBICON_XBOX_Right_Trigger,
        XBICON_XBOX_Left_Shoulder,
        XBICON_XBOX_Left_Trigger,
    }
    public enum SFXPS3HintIcon
    {
        PS3ICON_None,
    }
    public enum SFXPCHintIcon
    {
        PCICON_None,
    }
    public enum SFXGenericHintIcon
    {
        HINTICON_None,
    }
    public enum SFX_MB_Skin
    {
        SFX_MB_Skin_User,
        SFX_MB_Skin_Shepard,
    }
    public enum SFX_MB_TextAlign
    {
        SFX_MB_Centered,
        SFX_MB_Left,
        SFX_MB_Right,
    }
    public enum EBioSkillGame
    {
        SKILL_GAME_DECRYPTION,
        SKILL_GAME_ELECTRONICS,
        SKILL_GAME_CUSTOM,
    }
    public enum EBioSkillGameDifficulty
    {
        SKILL_GAME_DIFFICULTY_EASY,
        SKILL_GAME_DIFFICULTY_MEDIUM,
        SKILL_GAME_DIFFICULTY_HARD,
    }
    public enum EBioRadarType
    {
        BRT_None,
        BRT_Pawn_Friendly,
        BRT_Pawn_Neutral,
        BRT_Pawn_Hostile,
        BRT_Vehicle,
        BRT_Store,
        BRT_Destination,
        BRT_Plot,
        BRT_Mineral,
        BRT_Anomaly,
        BRT_Point_Of_Interest,
        BRT_Debris,
        BRT_Surveyed,
        BRT_Henchmen,
        BRT_Transition,
        BRT_TextNote,
    }
    public enum EActionComplete_Combat
    {
        ACC_Cancelled,
        ACC_Success,
        ACC_Failed,
        ACC_Dead,
        ACC_TargetKilled,
        ACC_TimeOut,
        ACC_LowTargeting,
        ACC_LostSight,
        ACC_Disabled,
        ACC_PowerCooldown,
        ACC_WeaponOverheat,
        ACC_WeaponCoolDown,
    }
    public enum EInventoryResourceTypes
    {
        INV_RESOURCE_CREDITS,
        INV_RESOURCE_MEDIGEL,
        INV_RESOURCE_SALVAGE,
        INV_RESOURCE_GRENADES,
        INV_RESOURCE_RARE1_EEZO,
        INV_RESOURCE_RARE2_IRIDIUM,
        INV_RESOURCE_RARE3_PALLADIUM,
        INV_RESOURCE_RARE4_PLATINUM,
        INV_RESOURCE_PROBES,
        INV_RESOURCE_FUEL,
    }
    public enum EBioGalaxyMap_PlanetType
    {
        eBioGM_PlanetType_None,
        eBioGM_PlanetType_Planet,
        eBioGM_PlanetType_Anomaly,
        eBioGM_PlanetType_PlanetAndAnomaly,
        eBioGM_PlanetType_Citadel,
        eBioGM_PlanetType_Prefab,
        eBioGM_PlanetType_PlanetAndRing,
        eBioGM_PlanetType_2DImage,
    }
    public enum EBioGalaxyMapState
    {
        GalaxyMapState_None,
        GalaxyMapState_Galaxy,
        GalaxyMapState_Cluster,
        GalaxyMapState_System,
        GalaxyMapState_Planet,
        GalaxyMapState_PlanetScan,
    }
    public enum EProfileType
    {
        Profile_None,
        Profile_AI,
        Profile_Camera,
        Profile_Combat,
        Profile_MPGame,
        Profile_Weapon,
        Profile_CombatStats,
        Profile_Difficulty,
        Profile_Angst,
        Profile_Cooldown,
        Profile_Damage,
        Profile_Pawn,
        Profile_Power,
        Profile_Tech,
        Profile_Treasure,
        Profile_Locomotion,
        Profile_AnimTree,
        Profile_Ticket,
        Profile_Vehicle,
        Profile_Henchmen,
        Profile_Settings,
        Profile_Effects,
        Profile_Scaleform,
        Profile_SaveGame,
        Profile_GAWAssets,
        Profile_GAWAssets_Military,
        Profile_GAWAssets_Device,
        Profile_GAWAssets_Intel,
        Profile_GAWAssets_Salvage,
        Profile_GAWAssets_Artifact,
        Profile_Focus,
        Profile_LoadSeekFreeAsync,
        Profile_Placeable,
        Profile_Reinforcements,
        Profile_Anim,
        Profile_Cover,
        Profile_Door,
        Profile_Conversation,
        Profile_ConversationBug,
        Profile_Gestures,
        Profile_Bonuses,
        Profile_Multipliers,
        Profile_LookAt,
        Profile_Wwise,
        Profile_Kinect,
        Profile_AnimPreload,
        Profile_Galaxy,
    }
    public enum ASParamTypes
    {
        ASParam_Integer,
        ASParam_Float,
        ASParam_String,
        ASParam_Boolean,
        ASParam_Undefined,
    }
    public enum ESFXHUDPOIIconState
    {
        SFXHUD_POI_Off,
        SFXHUD_POI_On,
        SFXHUD_POI_Activated,
    }
    public enum ESFXHUDActionIcon
    {
        SFXHUD_Action_NONE,
        SFXHUD_Cover_Enter,
        SFXHUD_Cover_Mantle,
        SFXHUD_Cover_Climb,
        SFXHUD_Cover_90DegreeRight,
        SFXHUD_Cover_90DegreeLeft,
        SFXHUD_Cover_SlipRight,
        SFXHUD_Cover_SlipLeft,
        SFXHUD_Cover_SwatTurnRight,
        SFXHUD_Cover_SwatTurnLeft,
        SFXHUD_Cover_Grab,
        SFXHUD_LadderUp,
        SFXHUD_LadderDown,
        SFXHUD_GapJump,
        SFXHUD_AtlasSuit,
    }
    public enum EAsyncLoadStatus
    {
        ASYNC_LOAD_ERROR,
        ASYNC_LOAD_STARTED,
        ASYNC_LOAD_INPROGRESS,
        ASYNC_LOAD_COMPLETE,
    }
    public enum EGAWAssetType
    {
        GAWAssetType_Military,
        GAWAssetType_Device,
        GAWAssetType_Intel,
        GAWAssetType_Artifact,
        GAWAssetType_Salvage,
        GAWAssetType_Treasure,
        GAWAssetType_External,
        GAWAssetType_Modifier,
        GAWAssetType_Quest,
    }
    public enum EGAWAssetSubType
    {
        GAWAssetSubType_None,
        GAWAssetSubType_Ground,
        GAWAssetSubType_Fleet,
    }
    public enum EObjectiveMarkerIconType
    {
        EOMIT_None,
        EOMIT_Attack,
        EOMIT_Supply,
        EOMIT_Alert,
    }
    public enum ETargetTipText
    {
        TargetTipText_None,
        TargetTipText_Talk,
        TargetTipText_Examine,
        TargetTipText_Use,
        TargetTipText_Open,
        TargetTipText_Salvage,
        TargetTipText_PickUp,
        TargetTipText_Bypass,
        TargetTipText_Support,
        TargetTipText_Reactivate,
        TargetTipText_Deactivate,
        TargetTipText_Activate,
        TargetTipText_Warn,
        TargetTipText_Revive,
    }
    public enum EEndGameOption
    {
        EGO_ReapersDestroyedEarthDestroyed,
        EGO_ReapersDestroyedEarthDevastated,
        EGO_ReapersDestroyedEarthOk,
        EGO_ReapersDestroyedEarthOkShepardAlive,
        EGO_BecomeAReaperAndEarthDestroyedAndReapersLeave,
        EGO_BecomeAReaperAndEarthOkAndReapersLeave,
        EGO_HarmonyOfManAndMachine,
        EGO_Demo,
        EGO_None,
    }
    public enum GAWExternalAssetID
    {
        GAWExternalAssetID_Multiplayer,
        GAWExternalAssetID_Iphone,
        GAWExternalAssetID_FaceBook,
    }
    public enum EParameterType
    {
        ParamType_FaceValue,
        ParamType_Location,
        ParamType_Normal,
        ParamType_ScreenLocation,
        ParamType_ScreenNormal,
    }
    public enum EParameterDataType
    {
        ParamDataType_Ambiguous,
        ParamDataType_Float,
        ParamDataType_Vector,
        ParamDataType_ColorWithAlpha,
    }
    public enum EEndGameState
    {
        EGS_NotFinished,
        EGS_OutInABlazeOfGlory,
        EGS_LivedToFightAgain,
    }
    public enum EPlayerAppearanceType
    {
        PlayerAppearanceType_Parts,
        PlayerAppearanceType_Full,
    }
    public enum EOriginType
    {
        OriginType_None,
        OriginType_Spacer,
        OriginType_Colony,
        OriginType_Earthborn,
    }
    public enum ENotorietyType
    {
        NotorietyType_None,
        NotorietyType_Survivor,
        NotorietyType_Warhero,
        NotorietyType_Ruthless,
    }
    public enum ESFXNetworkErrorStatus
    {
        ErrorStatus_NoError,
        ErrorStatus_DisplayingPrompt,
        ErrorStatus_DisplayPromptAfterTravel,
    }
    public enum EWaitMessage
    {
        EWaitMessage_Generic,
        EWaitMessage_MatchStarted,
        EWaitMessage_MatchEnded,
    }
    public enum ESFXSaveGameAction
    {
        SaveGame_DoNothing,
        SaveGame_Load,
        SaveGame_Save,
        SaveGame_Delete,
        SaveGame_CreateCareer,
        SaveGame_DeleteCareer,
        SaveGame_EnumerateCareers,
        SaveGame_EnumerateSaves,
        SaveGame_QueryFreeSpace,
        SaveGame_PrepareSave,
        SaveGame_DeletePreparedSave,
    }
    public enum ELoadoutWeaponFlags
    {
        LoadoutWeaponFlag_NotNew,
    }
    public enum ELoadoutWeapons
    {
        LoadoutWeapons_AssaultRifles,
        LoadoutWeapons_Shotguns,
        LoadoutWeapons_SniperRifles,
        LoadoutWeapons_AutoPistols,
        LoadoutWeapons_HeavyPistols,
        LoadoutWeapons_HeavyWeapons,
    }
    public enum ESFXSaveGameType
    {
        SaveGameType_Manual,
        SaveGameType_Quick,
        SaveGameType_Auto,
        SaveGameType_Chapter,
        SaveGameType_Export,
        SaveGameType_Legend,
    }
    public enum EHitReactRange
    {
        HitReactRange_Invalid,
        HitReactRange_Melee,
        HitReactRange_Short,
        HitReactRange_Medium,
        HitReactRange_Long,
    }
    public enum ESFXDamageFalloffType
    {
        DamageFalloffType_Constant,
        DamageFalloffType_Linear,
    }
    public enum EAICustomAction
    {
        CA_None,
        CA_Ragdoll,
        CA_AnimRagdoll_Singularity,
        CA_SyncAttackVictim,
        CA_Use,
        CA_Revive,
        CA_PickUpWeapon,
        CA_BovineFortitude,
        CA_Frozen,
        CA_Reload,
        CA_MountedGunReload,
        CA_HolsterWeapon,
        CA_DrawWeapon,
        CA_EnterVehicle,
        CA_ExitVehicle,
        CA_SpawnEntrance,
        CA_PrecisionMove,
        CA_HackDoor,
        CA_OmniWave,
        CA_ActivateWeaponFlashlight,
        CA_LookAt_Mantle,
        CA_LookAt_MidCoverSlip,
        CA_LookAt_StdCoverSlip,
        CA_LookAt_MidSwatTurn,
        CA_LookAt_StdSwatTurn,
        CA_MP_RetrieveFlag,
        CA_MP_DisarmBomb,
        CA_MP_AnnexHack,
        CA_GapJump,
        CA_StandingGapJump,
        CA_JumpDown,
        CA_MantleOver,
        CA_EarlyMantleOver,
        CA_MantleUp,
        CA_MantleDown,
        CA_SlotToSlot,
        CA_CoverSlipLeft,
        CA_CoverSlipLeftStanding,
        CA_CoverSlipRight,
        CA_CoverSlipRightStanding,
        CA_Cover90TurnRight,
        CA_Cover90TurnRightStanding,
        CA_Cover90TurnLeft,
        CA_Cover90TurnLeftStanding,
        CA_SwatTurnLeft,
        CA_SwatTurnRight,
        CA_LadderClimbUp,
        CA_LadderClimbDown,
        CA_BoostDown,
        CA_BoostUp,
        CA_ClimbUpWall,
        CA_ClimbDownWall,
        CA_LeapHumanoid,
        CA_LeapLarge,
        CA_RollLeft,
        CA_RollRight,
        CA_RollForward,
        CA_RollBackward,
        CA_ClassMelee,
        CA_MantleMelee,
        CA_BackTakeDown,
        CA_InfiltratorCloakPunch,
        CA_MidCoverMeleeGrab,
        CA_MidCoverMeleeOver,
        CA_StdCoverMeleeLeft,
        CA_StdCoverMeleeRight,
        CA_CoverMeleeRight,
        CA_CoverMeleeLeft,
        CA_HvyStdCoverMeleeLeft,
        CA_HvyStdCoverMeleeRight,
        CA_HvyCoverMeleeRight,
        CA_HvyCoverMeleeLeft,
        CA_HeavyStdCoverMeleeRight,
        CA_StormPunch,
        CA_BioticStormPunch,
        CA_RifleMeleeOne,
        CA_RifleMeleeTwo,
        CA_RifleMeleeThree,
        CA_PistolMeleeOne,
        CA_PistolMeleeTwo,
        CA_PistolMeleeThree,
        CA_Reaction_Standard,
        CA_Reaction_StandardII,
        CA_Reaction_StandardForward,
        CA_Reaction_StandardLeft,
        CA_Reaction_StandardRight,
        CA_Reaction_StandardKnee,
        CA_Reaction_Stagger,
        CA_Reaction_StaggerII,
        CA_Reaction_StaggerForward,
        CA_Reaction_StaggerLeft,
        CA_Reaction_StaggerRight,
        CA_Reaction_Knockback,
        CA_Reaction_KnockbackForward,
        CA_Reaction_KnockbackLeft,
        CA_Reaction_KnockbackRight,
        CA_Reaction_Meleed,
        CA_Reaction_MeleedForward,
        CA_Reaction_MeleedLeft,
        CA_Reaction_MeleedRight,
        CA_Reaction_ShieldBreach,
        CA_Reaction_FlinchLight,
        CA_Reaction_FlinchHeavy,
        CA_Reaction_OnFire,
        CA_Reaction_OnFireII,
        CA_Reaction_GreatPain,
        CA_Reaction_GreatPainII,
        CA_Reaction_Freezing,
        CA_Reaction_FreezingII,
        CA_Reaction_FreezingIII,
        CA_Reaction_LargeStagger,
        CA_Reaction_LargeStandard,
        CA_Reaction_LargeStandardForward,
        CA_Reaction_LargeStandardLeft,
        CA_Reaction_LargeStandardRight,
        CA_ACTMNT_ExplosionBack,
        CA_ACTMNT_ExplosionFront,
        CA_ACTMNT_ExplosionLeft,
        CA_ACTMNT_ExplosionRight,
        CA_ACTMNT_ShieldFace,
        CA_DeathReaction_Standard,
        CA_DeathReaction_HeadShot,
        CA_DeathReaction_HitLeftArm,
        CA_DeathReaction_HitRightArm,
        CA_DeathReaction_HeavyHitLeftArm,
        CA_DeathReaction_HeavyHitRightArm,
        CA_DeathReaction_HitLeftLeg,
        CA_DeathReaction_HitRightLeg,
        CA_DeathReaction_Stomach,
        CA_DeathReaction_Knockback,
        CA_DeathReaction_Corkscrew,
        CA_DeathReaction_FlinchDeath,
        CA_Power,
        CA_AI_Melee,
        CA_AI_Melee2,
        CA_AI_SyncMelee,
        CA_AI_KillingBlow,
        CA_Engineer_DeployTurret,
        CA_Engineer_Repair,
        CA_Engineer_Breach,
        CA_Cannibal_ConsumeBody,
        CA_Marauder_BuffAllies,
        CA_Marauder_Breach,
        CA_Marauder_BuffedByMarauder,
        CA_Swarmer_Spawn,
        CA_KaiLeng_Vortex,
        CA_KaiLeng_SuperVortex,
        CA_Phantom_Shield,
        CA_Phantom_AirRecover,
        CA_Roar,
        CA_Brute_Block,
        CA_Brute_Charge,
        CA_Harvester_TakeOff,
        CA_Harvester_Land,
        CA_Harvester_EnterAim,
        CA_Harvester_LeaveAim,
        CA_Harvester_FlyingDeath,
        CA_Guardian_ShieldBash,
        CA_Guardian_Breach,
        CA_Guardian_LoseShield,
        CA_Guardian_Stagger,
        CA_Atlas_Smoke,
        CA_Atlas_Block,
        CA_Atlas_OpenCockpitDeath,
        CA_Atlas_CloseCockpitCombat,
        CA_Atlas_OpenCockpitIdle,
        CA_Atlas_CloseCockpitIdle,
        CA_Atlas_DriverEnter,
        CA_Atlas_DriverExit,
        CA_Banshee_Blast,
        CA_Banshee_AOEBlast,
        CA_Banshee_Shield,
        CA_Banshee_Phase,
        CA_Banshee_Breach,
        CA_Ravager_SpawnSwarmers,
        CA_Ravager_PopSacks,
        CA_Idle_StandGuard,
        CA_Idle_InspectWeapon,
        CA_Idle_InspectOmniTool,
        CA_Idle_OmniToolScan,
        CA_Idle_UseConsole,
        CA_Idle_Talking,
        CA_Idle_Talking2,
        CA_Idle_Talking3,
        CA_Idle_Listening,
        CA_Idle_Listening2,
        CA_Idle_Listening3,
        CA_Idle_Centurion,
        CA_Idle_GuardPose,
        CA_Idle_SniperSweep,
        CA_Idle_SwordFlourish,
        CA_Idle_Cannibal,
        CA_Idle_Brute,
        CA_Idle_Husk,
        CA_Idle_Ravager,
        CA_Idle_Banshee,
        CA_OmniTool,
        CA_StandTyping,
        CA_DesignerSpecified,
        CA_BeckonFront,
        CA_BeckonRear,
        CA_OmniToolCrouch,
        CA_Crouch,
        CA_InteractLow,
        CA_StandIdle,
        CA_DLC1,
        CA_DLC2,
        CA_DLC3,
        CA_DLC4,
        CA_DLC5,
        CA_DLC6,
        CA_DLC7,
        CA_DLC8,
        CA_DLC9,
        CA_DLC10,
        CA_DLC11,
        CA_DLC12,
        CA_DLC13,
        CA_DLC14,
        CA_DLC15,
        CA_DLC16,
        CA_DLC17,
        CA_DLC18,
        CA_DLC19,
        CA_DLC20,
        CA_DLC21,
        CA_DLC22,
        CA_DLC23,
        CA_DLC24,
        CA_DLC25,
        CA_DLC26,
        CA_DLC27,
        CA_DLC28,
        CA_DLC29,
    }
    public enum EPowerCustomAction
    {
        PCA_None,
        PCA_Power_Reave,
        PCA_Power_Throw,
        PCA_Power_Pull,
        PCA_Power_Singularity,
        PCA_Power_Warp,
        PCA_Power_Shockwave,
        PCA_Power_Stasis,
        PCA_Power_Overpower,
        PCA_Power_Slam,
        PCA_Power_Barrier,
        PCA_Power_Dominate,
        PCA_Power_LiftGrenade,
        PCA_Power_AdrenalineRush,
        PCA_Power_ConcussiveShot,
        PCA_Power_Carnage,
        PCA_Power_Fortification,
        PCA_Power_IncendiaryAmmo,
        PCA_Power_DisruptorAmmo,
        PCA_Power_CryoAmmo,
        PCA_Power_ArmorPiercingAmmo,
        PCA_Power_WarpAmmo,
        PCA_Power_ShredderAmmo,
        PCA_Power_InfernoGrenade,
        PCA_Power_FlashbangGrenade,
        PCA_Power_FragGrenade,
        PCA_Power_StickyGrenade,
        PCA_Power_BioticGrenade,
        PCA_Power_Overload,
        PCA_Power_Incinerate,
        PCA_Power_CryoBlast,
        PCA_Power_CombatDrone,
        PCA_Power_SentryTurret,
        PCA_Power_Hacking,
        PCA_Power_GethShieldBoost,
        PCA_Power_EnergyDrain,
        PCA_Power_NeuralShock,
        PCA_Power_BioticCharge,
        PCA_Power_TechArmor,
        PCA_Power_Amplification,
        PCA_Power_Cloak,
        PCA_Power_Regeneration,
        PCA_Power_ClassPassive,
        PCA_Power_ClassMeleePassive,
        PCA_Power_Cannibal_BioticBlast,
        PCA_Power_Atlas_ConcussiveShot,
        PCA_Power_GethPrime_ShieldDrone,
        PCA_Power_GethPrime_Turret,
        PCA_Power_CombatDrone_Zap,
        PCA_Power_CombatDrone_Shock,
        PCA_Power_CombatDrone_Rocket,
        PCA_Power_ProtectorDrone,
        PCA_Power_Decoy,
        PCA_Power_Titan_Rocket,
        PCA_Power_Titan_Rocket_Player,
        PCA_Power_EnemyGrenade,
        PCA_Power_SentryTurret_Rocket,
        PCA_Power_SentryTurret_Shock,
        PCA_Power_GethReaper_Attack,
        PCA_Power_Marksman,
        PCA_Power_ProximityMine,
        PCA_Power_ShieldDroneBuff,
        PCA_Power_BioticFocus,
        PCA_Power_Lockdown,
        PCA_Power_Discharge,
        PCA_Power_DarkChannel,
        PCA_Power_Consumable_Rocket,
        PCA_Power_Consumable_Revive,
        PCA_Power_Consumable_Shield,
        PCA_Power_Consumable_Ammo,
        PCA_Power_MatchConsumableAmmo,
        PCA_POWER_DLC1,
        PCA_POWER_DLC2,
        PCA_POWER_DLC3,
        PCA_POWER_DLC4,
        PCA_POWER_DLC5,
        PCA_POWER_DLC6,
        PCA_POWER_DLC7,
        PCA_POWER_DLC8,
        PCA_POWER_DLC9,
        PCA_POWER_DLC10,
        PCA_POWER_DLC11,
        PCA_POWER_DLC12,
        PCA_POWER_DLC13,
        PCA_POWER_DLC14,
        PCA_POWER_DLC15,
        PCA_POWER_DLC16,
        PCA_POWER_DLC17,
        PCA_POWER_DLC18,
        PCA_POWER_DLC19,
        PCA_POWER_DLC20,
        PCA_POWER_DLC21,
        PCA_POWER_DLC22,
        PCA_POWER_DLC23,
        PCA_POWER_DLC24,
        PCA_POWER_DLC25,
        PCA_POWER_DLC26,
        PCA_POWER_DLC27,
        PCA_POWER_DLC28,
        PCA_POWER_DLC29,
        PCA_Power_Unity,
    }
    public enum EReactionTypes
    {
        Reaction_Light,
        Reaction_Medium,
        Reaction_Heavy,
        Reaction_Pain,
        Reaction_Fire,
        Reaction_ShieldBreach,
        Reaction_DeathLight,
        Reaction_DeathHeavy,
    }
    public enum EWoundDamage
    {
        WoundDamage_None,
        WoundDamage_Light,
        WoundDamage_Medium,
        WoundDamage_Heavy,
    }
    public enum EDamageCalculationSource
    {
        DamageCalcWeapon,
        DamageCalcPower,
    }
    public enum EAICombatRange
    {
        AI_Range_Melee,
        AI_Range_Short,
        AI_Range_Medium,
        AI_Range_Long,
    }
    public enum EAICombatMood
    {
        AI_NoMood,
        AI_Unaware,
        AI_Fallback,
        AI_Normal,
        AI_Aggressive,
        AI_Berserk,
    }
    public enum ESFXVocalizationRole
    {
        SFXVocalizationRole_None,
        SFXVocalizationRole_Instigator,
        SFXVocalizationRole_Instigator_NonCombat,
        SFXVocalizationRole_Instigator_Stealth,
        SFXVocalizationRole_Recipient,
        SFXVocalizationRole_EnemyWitness,
        SFXVocalizationRole_TeammateWitness,
        SFXVocalizationRole_HenchmanWitness,
        SFXVocalizationRole_ReferencedPawn,
    }
    public enum ESFXVocalizationVariationType
    {
        SFXVocalizationSpecificType_None,
        SFXVocalizationSpecificType_Location,
        SFXVocalizationSpecificType_CharacterName,
        SFXVocalizationSpecificType_CharacterType,
        SFXVocalizationSpecificType_Affiliation,
        SFXVocalizationSpecificType_Gender,
        SFXVocalizationSpecificType_Weapon,
        SFXVocalizationSpecificType_Challenge,
        SFXVocalizationSpecificType_Me,
        SFXVocalizationSpecificType_IsFriendly,
    }
    public enum ESFXVocalizationBool
    {
        SFXVocalizationBool_False,
        SFXVocalizationBool_True,
    }
    public enum ESFXVocalizationLocation
    {
        SFXVocalizationLocation_None,
        SFXVocalizationLocation_Above,
        SFXVocalizationLocation_Below,
        SFXVocalizationLocation_Right,
        SFXVocalizationLocation_Left,
        SFXVocalizationLocation_Ahead,
        SFXVocalizationLocation_Behind,
        SFXVocalizationLocation_Specific,
    }
    public enum ESFXVocalizationGender
    {
        SFXVocalizationGender_None,
        SFXVocalizationGender_Male,
        SFXVocalizationGender_Female,
    }
    public enum ESFXVocalizationWeapon
    {
        SFXVocalizationWeapon_None,
        SFXVocalizationWeapon_Pistol,
        SFXVocalizationWeapon_SMG,
        SFXVocalizationWeapon_AssaultRifle,
        SFXVocalizationWeapon_Shotgun,
        SFXVocalizationWeapon_SniperRifle,
        SFXVocalizationWeapon_HeavyWeapon,
    }
    public enum ESFXVocalizationName
    {
        SFXVocalizationCharacter_None,
        SFXVocalizationCharacter_Shepard,
        SFXVocalizationCharacter_Garrus,
        SFXVocalizationCharacter_Tali,
        SFXVocalizationCharacter_Legion,
        SFXVocalizationCharacter_Samara,
        SFXVocalizationCharacter_Morinth,
        SFXVocalizationCharacter_Jacob,
        SFXVocalizationCharacter_Miranda,
        SFXVocalizationCharacter_Grunt,
        SFXVocalizationCharacter_Mordin,
        SFXVocalizationCharacter_Thane,
        SFXVocalizationCharacter_Jack,
        SFXVocalizationCharacter_Kasumi,
        SFXVocalizationCharacter_Zaeed,
    }
    public enum ESFXVocalizationEventID
    {
        SFXVocalizationEvent_None,
        SFXVocalizationEvent_EnteredCombat,
        SFXVocalizationEvent_ReceivedOrder_Attack,
        SFXVocalizationEvent_ReceivedOrder_Attack_TargetValid,
        SFXVocalizationEvent_ReceivedOrder_Attack_MovingFirst,
        SFXVocalizationEvent_ReceivedOrder_Attack_KilledTarget,
        SFXVocalizationEvent_ReceivedOrder_ChangeWeapon,
        SFXVocalizationEvent_ReceivedOrder_Move,
        SFXVocalizationEvent_ReceivedOrder_Follow,
        SFXVocalizationEvent_ReceivedOrder_TakeCover,
        SFXVocalizationEvent_ReceivedOrder_Hold,
        SFXVocalizationEvent_CancellingHoldOrder,
        SFXVocalizationEvent_FailedMoveOrder,
        SFXVocalizationEvent_Attacking,
        SFXVocalizationEvent_Attacking_Henchman,
        SFXVocalizationEvent_KilledTarget_Henchman,
        SFXVocalizationEvent_LostSight,
        SFXVocalizationEvent_EnemySighted,
        SFXVocalizationEvent_MovingToCover,
        SFXVocalizationEvent_UsingPower,
        SFXVocalizationEvent_ChangingWeapon,
        SFXVocalizationEvent_Death,
        SFXVocalizationEvent_Death_NonTrivial,
        SFXVocalizationEvent_Death_Henchman,
        SFXVocalizationEvent_ReceivedDamage,
        SFXVocalizationEvent_ReceivedDamage_LowHealth,
        SFXVocalizationEvent_ReceivedDamage_ExtremelyLowHealth,
        SFXVocalizationEvent_RequireHealing,
        SFXVocalizationEvent_ShieldsDown,
        SFXVocalizationEvent_OneEnemyRemaining,
        SFXVocalizationEvent_ZeroEnemiesRemaining,
        SFXVocalizationEvent_Agitation_High,
        SFXVocalizationEvent_Agitation_Medium,
        SFXVocalizationEvent_DamageReaction_GreatPain,
        SFXVocalizationEvent_DamageReaction_OnFire,
        SFXVocalizationEvent_DamageReaction_KnockedBack,
        SFXVocalizationEvent_Falling,
        SFXVocalizationEvent_Tossed,
        SFXVocalizationEvent_Brainwashed,
        SFXVocalizationEvent_Taunt,
        SFXVocalizationEvent_Ambient,
        SFXVocalizationEvent_Power_Failed_ShieldsUp,
        SFXVocalizationEvent_Power_Failed_BarrierUp,
        SFXVocalizationEvent_Power_Failed_ArmorUp,
        SFXVocalizationEvent_Power_EnergyDrain,
        SFXVocalizationEvent_Power_Reave,
        SFXVocalizationEvent_Power_ShockWave,
        SFXVocalizationEvent_Power_Crush,
        SFXVocalizationEvent_Power_CombatDrone,
        SFXVocalizationEvent_Power_Incinerate,
        SFXVocalizationEvent_Power_Lift,
        SFXVocalizationEvent_Power_Pull,
        SFXVocalizationEvent_Power_Singularity,
        SFXVocalizationEvent_Power_Stasis,
        SFXVocalizationEvent_Power_Throw,
        SFXVocalizationEvent_Power_Warp,
        SFXVocalizationEvent_Power_Biotic_Misc,
        SFXVocalizationEvent_Power_AIHack,
        SFXVocalizationEvent_Power_Fissure,
        SFXVocalizationEvent_Power_Flashbang,
        SFXVocalizationEvent_Power_NeuralShock,
        SFXVocalizationEvent_Power_Overload,
        SFXVocalizationEvent_Power_Sabotage,
        SFXVocalizationEvent_Power_Tech_Misc,
        SFXVocalizationEvent_Power_Ammo,
        SFXVocalizationEvent_Power_Explosion,
        SFXVocalizationEvent_Power_Melee,
        SFXVocalizationEvent_Power_Projectile,
        SFXVocalizationEvent_Power_Combat_Misc,
        SFXVocalizationEvent_Power_Buff,
        SFXVocalizationEvent_Power_Cloak,
        SFXVocalizationEvent_Power_Regeneration,
        SFXVocalizationEvent_Power_Resurrection,
        SFXVocalizationEvent_Power_KroganCharge,
        SFXVocalizationEvent_Power_KroganResurrection,
        SFXVocalizationEvent_MultipleAttackers,
        SFXVocalizationEvent_ViolenceAwe,
        SFXVocalizationEvent_Bored,
        SFXVocalizationEvent_DrewWeapon_OutOfCombat,
        SFXVocalizationEvent_StaredAt,
        SFXVocalizationEvent_Bumped,
        SFXVocalizationEvent_FriendlyFire,
        SFXVocalizationEvent_Headshot,
        SFXVocalizationEvent_ShootDeadBody,
        SFXVocalizationEvent_UsedNuclearWeapon,
        SFXVocalizationEvent_CoverCrateExplosion,
        SFXVocalizationEvent_HeavyMechGoingToExplode,
        SFXVocalizationEvent_Flanked,
        SFXVocalizationEvent_UnCloaked,
        SFXVocalizationEvent_ChargeFailed,
        SFXVocalizationEvent_BlockedPower,
        SFXVocalizationEvent_PowerStillOnCooldown,
        SFXVocalizationEvent_Varren_Charge,
        SFXVocalizationEvent_Husk_Charge,
        SFXVocalizationEvent_DamageReaction_PlayerStagger,
        SFXVocalizationEvent_DamageReaction_PlayerMeleed,
        SFXVocalizationEvent_DamageReaction_PlayerMeleedII,
        SFXVocalizationEvent_DamageReaction_PlayerMeleedNoRotate,
        SFXVocalizationEvent_DamageReaction_PlayerStandardImpact,
        SFXVocalizationEvent_DamageReaction_PlayerKnockback,
        SFXVocalizationEvent_DamageReaction_BloodyPlayerStandardImpact,
        SFXVocalizationEvent_DamageReaction_PlayerOnFire,
        SFXVocalizationEvent_CollectorPossession,
        SFXVocalizationEvent_VorchaBloodlust,
        SFXVocalizationEvent_CausedDamage,
        SFXVocalizationEvent_LowAmmo,
        SFXVocalizationEvent_OutOfAmmo,
        SFXVocalizationEvent_Loot_AmmoFound,
        SFXVocalizationEvent_Loot_AmmoFull,
        SFXVocalizationEvent_Loot_TreasureFound,
        SFXVocalizationEvent_Saw_HeavyMech,
        SFXVocalizationEvent_CollectorGeneral_ReceivedDamage,
        SFXVocalizationEvent_Aggressive_Flank,
        SFXVocalizationEvent_PraetorianImmune_ReceivedDamage,
        SFXVocalizationEvent_Krogan_ReceivedDamage,
        SFXVocalizationEvent_CollectorPossessed,
        SFXVocalizationEvent_Multiplayer_Revive,
        SFXVocalizationEvent_Multiplayer_PlayerAssisted,
        SFXVocalizationEvent_Multiplayer_ObjectiveBegin,
        SFXVocalizationEvent_Multiplayer_RequestAssistMelee,
        SFXVocalizationEvent_Multiplayer_RequestAssistRanged,
        SFXVocalizationEvent_Multiplayer_RequestCoveringFire,
        SFXVocalizationEvent_Multiplayer_RangedAssistMelee,
        SFXVocalizationEvent_Multiplayer_RangedAssistRanged,
        SFXVocalizationEvent_Multiplayer_PowerAssist,
        SFXVocalizationEvent_Multiplayer_Combo,
        SFXVocalizationEvent_Multiplayer_Headshot,
        SFXVocalizationEvent_Mutliplayer_Cloaked,
        SFXVocalizationEvent_Multiplayer_Flanking,
        SFXVocalizationEvent_Power_HeavyMelee,
        SFXVocalizationEvent_Custom1,
        SFXVocalizationEvent_Custom2,
        SFXVocalizationEvent_Custom3,
        SFXVocalizationEvent_Custom4,
        SFXVocalizationEvent_Custom5,
        SFXVocalizationEvent_Custom6,
        SFXVocalizationEvent_Custom7,
        SFXVocalizationEvent_Custom8,
        SFXVocalizationEvent_Custom9,
        SFXVocalizationEvent_PlayerControllingAtlas,
        SFXVocalizationEvent_EngineerSetUpTurret,
        SFXVocalizationEvent_EngineerRepairingAtlas,
    }
    public enum ECharacterClass
    {
        ClassType_Invalid,
        ClassType_Soldier,
        ClassType_Adept,
        ClassType_Infiltrator,
        ClassType_Engineer,
        ClassType_Vanguard,
        ClassType_Sentinel,
    }
    public enum EAccomplishmentStorage
    {
        ACCSTOR_None,
        ACCSTOR_AsAchievement,
        ACCSTOR_InOnlineStorage,
        ACCSTOR_InProfileSettings,
    }
    public enum EEffectLocationTarget
    {
        ELT_None,
        ELT_Effect,
        ELT_Instigator,
        ELT_HitActor,
        ELT_Tool,
        ELT_HitLocation,
        ELT_HitCharacter,
        ELT_LocalPlayer,
        ELT_Camera,
    }
    public enum EValueModifierSelection
    {
        VMS_Spawn_Value_X,
        VMS_Spawn_Value_Y,
        VMS_Spawn_Value_Z,
        VMS_Spawn_Value_Vector,
        VMS_Parameter_X,
        VMS_Parameter_Y,
        VMS_Parameter_Z,
        VMS_Parameter_Vector,
        VMS_Blood_Color,
    }
    public enum EValueModifierOperation
    {
        VMO_None,
        VMO_Add,
        VMO_Subtract,
        VMO_Multiply,
        VMO_Divide,
        VMO_Power,
        VMO_DotProduct,
        VMO_CrossProduct,
        VMO_Greater,
        VMO_Less,
        VMO_NearlyEqual,
        VMO_NotNearlyEqual,
    }
    public enum EAchievementID
    {
        ACHIEVEMENT_00_PROEAR,
        ACHIEVEMENT_01_PROMAR,
        ACHIEVEMENT_02_KROGAR,
        ACHIEVEMENT_03_KRO001,
        ACHIEVEMENT_04_KRO002,
        ACHIEVEMENT_05_KROGRU,
        ACHIEVEMENT_06_GTH001,
        ACHIEVEMENT_07_GTH002,
        ACHIEVEMENT_08_GTHLEG,
        ACHIEVEMENT_09_CAT003,
        ACHIEVEMENT_10_CAT002,
        ACHIEVEMENT_11_CAT004,
        ACHIEVEMENT_12_CERMIR,
        ACHIEVEMENT_13_CITSAM,
        ACHIEVEMENT_14_OMGJCK,
        ACHIEVEMENT_15_CERJCB,
        ACHIEVEMENT_16_END001,
        ACHIEVEMENT_17_END002,
        ACHIEVEMENT_18_STORE,
        ACHIEVEMENT_19_ENDGAMEMAX,
        ACHIEVEMENT_20_FINDSALVAGE,
        ACHIEVEMENT_21_IMPORT,
        ACHIEVEMENT_22_INSANITY,
        ACHIEVEMENT_23_WEAPONMOD,
        ACHIEVEMENT_24_ROMANCE,
        ACHIEVEMENT_25_POWERCOMBO,
        ACHIEVEMENT_26_MAXPOWER,
        ACHIEVEMENT_27_KILLA,
        ACHIEVEMENT_28_KILLB,
        ACHIEVEMENT_29_KILLC,
        ACHIEVEMENT_30_MELEE,
        ACHIEVEMENT_31_ESCAPEREAPER,
        ACHIEVEMENT_32_MAXSECURITY,
        ACHIEVEMENT_33_OVERLOADSHIELDS,
        ACHIEVEMENT_34_ENEMIESFLYING,
        ACHIEVEMENT_35_ENEMIESONFIRE,
        ACHIEVEMENT_36_BRUTECHARGE,
        ACHIEVEMENT_37_GUARDIANMAILSLOT,
        ACHIEVEMENT_38_HIJACKATLAS,
        ACHIEVEMENT_39_HARVESTER,
        ACHIEVEMENT_40_CREATECHAR,
        ACHIEVEMENT_41_PLAYALLMAPS,
        ACHIEVEMENT_42_PREPARED,
        ACHIEVEMENT_43_MISSIONSA,
        ACHIEVEMENT_44_MISSIONSB,
        ACHIEVEMENT_45_WEAPONMAXED,
        ACHIEVEMENT_46_HIGHLEVEL,
        ACHIEVEMENT_47_MAXLEVEL,
        ACHIEVEMENT_48_NEWGAME,
        ACHIEVEMENT_49_ALLMAPSGOLD,
        AVATAR_00_OMNIBLADE,
        ACHIEVEMENT_DLC_1,
        ACHIEVEMENT_DLC_2,
        ACHIEVEMENT_DLC_3,
        ACHIEVEMENT_DLC_4,
        ACHIEVEMENT_DLC_5,
        ACHIEVEMENT_DLC_6,
        ACHIEVEMENT_DLC_7,
        ACHIEVEMENT_DLC_8,
        ACHIEVEMENT_DLC_9,
        ACHIEVEMENT_DLC_10,
        ACHIEVEMENT_DLC_11,
        ACHIEVEMENT_DLC_12,
        ACHIEVEMENT_DLC_13,
        ACHIEVEMENT_DLC_14,
        ACHIEVEMENT_DLC_15,
        ACHIEVEMENT_DLC_16,
        ACHIEVEMENT_DLC_17,
        ACHIEVEMENT_DLC_18,
        ACHIEVEMENT_DLC_19,
        ACHIEVEMENT_DLC_20,
        ACHIEVEMENT_DLC_21,
        ACHIEVEMENT_DLC_22,
        ACHIEVEMENT_DLC_23,
        ACHIEVEMENT_DLC_24,
        ACHIEVEMENT_DLC_25,
        ACHIEVEMENT_PS3_SPECIAL_N7ELITE_NO_USE,
        ACHIEVEMENT_NONE,
    }
    public enum EStickConfigOptions
    {
        SCO_Default,
        SCO_SouthPaw,
    }
    public enum ETriggerConfigOptions
    {
        TCO_Default,
        TCO_SouthPaw,
        TCO_DefaultSwapped,
        TCO_SouthPawSwapped,
    }
    public enum EAimAssistOptions
    {
        AAO_Low,
        AAO_Normal,
        AAO_High,
    }
    public enum EDifficultyOptions
    {
        DO_Level1,
        DO_Level2,
        DO_Level3,
        DO_Level4,
        DO_Level5,
        DO_Level6,
    }
    public enum EAutoLevelOptions
    {
        ALO_Off,
        ALO_Squad,
        ALO_All,
    }
    public enum ETVType
    {
        TVT_Default,
        TVT_Soft,
        TVT_Lucent,
        TVT_Vibrant,
    }
    public enum EAutoReplyModeOptions
    {
        ARMO_All_Decisions,
        ARMO_Major_Decisions,
        ARMO_No_Decisions,
    }
    public enum EHenchHelmetOptions
    {
        HHO_DefaultOff,
        HHO_DefaultOn,
        HHO_ConversationOff,
    }
    public enum EAudioDynamicRangeOptions
    {
        ADR_High,
        ADR_Low,
    }
    public enum EOptionYesNo
    {
        OYN_Yes,
        OYN_No,
    }
    public enum EOptionOnOff
    {
        OOO_On,
        OOO_Off,
    }
    public enum EProfileSetting
    {
        Setting_Unknown,
        Setting_ControllerVibration,
        Setting_YInversion,
        Setting_GamerCred,
        Setting_GamerRep,
        Setting_VoiceMuted,
        Setting_VoiceThruSpeakers,
        Setting_VoiceVolume,
        Setting_GamerPictureKey,
        Setting_GamerMotto,
        Setting_GamerTitlesPlayed,
        Setting_GamerAchievementsEarned,
        Setting_GameDifficulty,
        Setting_ControllerSensitivity,
        Setting_PreferredColor1,
        Setting_PreferredColor2,
        Setting_AutoAim,
        Setting_AutoCenter,
        Setting_MovementControl,
        Setting_RaceTransmission,
        Setting_RaceCameraLocation,
        Setting_RaceBrakeControl,
        Setting_RaceAcceleratorControl,
        Setting_GameCredEarned,
        Setting_GameAchievementsEarned,
        Setting_EndLiveIds,
        Setting_ProfileVersionNum,
        Setting_ProfileSaveCount,
        Setting_StickConfiguration,
        Setting_TriggerConfiguration,
        Setting_Subtitles,
        Setting_AimAssist,
        Setting_Difficulty,
        Setting_InitialGameDifficulty,
        Setting_AutoLevel,
        Setting_SquadPowers,
        Setting_AutoSave,
        Setting_MusicVolume,
        Setting_FXVolume,
        Setting_DialogVolume,
        Setting_MotionBlur,
        Setting_FilmGrain,
        Setting_SelectedDeviceID,
        Setting_CurrentCareer,
        Setting_DaysSinceRegistration,
        Setting_AutoLogin,
        Setting_LoginInfo,
        Setting_PersonaID,
        Setting_NucleusRefused,
        Setting_NucleusSuccessful,
        Setting_CerberusRefused,
        Setting_Achievement_FieldA,
        Setting_Achievement_FieldB,
        Setting_Achievement_FieldC,
        Setting_TelemetryCollectionEnabled,
        Setting_KeyBindings,
        Setting_DisplayGamma,
        Setting_CurrentSaveGame,
        Setting_HideCinematicHelmet,
        Setting_ActionIconUIHints,
        Setting_NumGameCompletions,
        Setting_ShowHints,
        Setting_MorinthNotSamara,
        Setting_MaxWeaponUpgradeCount,
        Setting_LastFinishedCareer,
        Setting_SwapTriggersShoulders,
        Setting_PS3_RedeemedProductCode,
        Setting_LastSelectedPawn,
        Setting_ShowScoreIndicators,
        Setting_Accomplishment_FieldA,
        Setting_Accomplishment_FieldB,
        Setting_Accomplishment_FieldC,
        Setting_Accomplishment_FieldD,
        Setting_Accomplishment_FieldE,
        Setting_Accomplishment_FieldF,
        Setting_Accomplishment_FieldG,
        Setting_Accomplishment_FieldH,
        Setting_NumSalvageFound,
        Setting_SPLevel,
        Setting_NumKills,
        Setting_NumMeleeKills,
        Setting_NumShieldsOverloaded,
        Setting_NumEnemiesFlying,
        Setting_NumEnemiesOnFire,
        Setting_CachedDisconnectError,
        Setting_CachedDisconnectFromState,
        Setting_CachedDisconnectToState,
        Setting_CachedDisconnectSessionId,
        Setting_Language_VO,
        Setting_Language_Text,
        Setting_Language_Speech,
        Setting_MPAutoLevel,
        Setting_GalaxyAtWarLevel,
        Setting_N7Rating_LocalUser,
        Setting_N7Rating_FriendBlob,
        Setting_AutoReplyMode,
        Setting_BonusPower,
        Setting_NumGuardianHeadKilled,
        Setting_MPCreateNewMatchPrivacySetting,
        Setting_MPCreateNewMatchMapName,
        Setting_MPCreateNewMatchEnemyType,
        Setting_MPCreateNewMatchDifficulty,
        Setting_HenchmenHelmetOption,
        Setting_AudioDynamicRange,
        Setting_NumPowerCombos,
        Setting_SPMaps,
        Setting_SPMapsCount,
        Setting_NumArmorBought,
        Setting_WeaponLevel,
        Setting_SPMapsInsane,
        Setting_SPMapsInsaneCount,
        Setting_PowerLevel,
        Setting_MPQuickMatchMapName,
        Setting_MPQuickMatchEnemyType,
        Setting_MPQuickMatchDifficulty,
        Setting_KinectTutorialPromptViewed,
        Setting_GAW_Readiness,
        Setting_GAW_ZoneIndex0,
        Setting_GAW_ZoneIndex1,
        Setting_GAW_ZoneIndex2,
        Setting_GAW_ZoneIndex3,
        Setting_GAW_ZoneIndex4,
        Setting_GAW_ZoneIndex5,
        Setting_GAW_AssetMultiplayer,
        Setting_GAW_AssetIPhone,
        Setting_GAW_AssetFacebook,
    }
    public enum EME3Level
    {
        ME3Level_None,
        ME3Level_ProEar,
        ME3Level_ProMar,
        ME3Level_SPRctr,
        ME3Level_CitHub,
        ME3Level_Nor,
        ME3Level_KroGar,
        ME3Level_OmgJck,
        ME3Level_SPCer,
        ME3Level_Kro001,
        ME3Level_KroN7a,
        ME3Level_KroGru,
        ME3Level_KroN7b,
        ME3Level_SPNov,
        ME3Level_Kro002,
        ME3Level_Cat003,
        ME3Level_CitSam,
        ME3Level_CerJcb,
        ME3Level_SPDish,
        ME3Level_SPTowr,
        ME3Level_SPSlum,
        ME3Level_Gth001,
        ME3Level_GthLeg,
        ME3Level_GthN7a,
        ME3Level_Gth002,
        ME3Level_Cat002,
        ME3Level_CerMir,
        ME3Level_Cat004,
        ME3Level_End001,
        ME3Level_End002,
        ME3Level_End003,
        ME3Level_EricCombat,
        ME3Level_Cat001,
        ME3Level_DLC1,
        ME3Level_DLC2,
        ME3Level_DLC3,
        ME3Level_DLC4,
        ME3Level_DLC5,
        ME3Level_DLC6,
        ME3Level_DLC7,
        ME3Level_DLC8,
        ME3Level_DLC9,
        ME3Level_DLC10,
        ME3Level_DLC11,
        ME3Level_DLC12,
        ME3Level_DLC13,
        ME3Level_DLC14,
        ME3Level_DLC15,
        ME3Level_DLC16,
        ME3Level_DLC17,
        ME3Level_DLC18,
        ME3Level_DLC19,
        ME3Level_DLC20,
    }
    public enum EArmorTreasurePiece
    {
        ArmorTreasure_None,
        ArmorTreasure_Helmet_Health,
        ArmorTreasure_Helmet_Shield,
        ArmorTreasure_Helmet_ShieldRegen,
        ArmorTreasure_Helmet_PowerDamage,
        ArmorTreasure_Helmet_PowerRecharge,
        ArmorTreasure_Helmet_Movement,
        ArmorTreasure_Helmet_WeaponDamage,
        ArmorTreasure_Helmet_ConstraintDamage,
        ArmorTreasure_Helmet_AmmoCapacity,
        ArmorTreasure_Helmet_MeleeDamage,
        ArmorTreasure_Torso_Health,
        ArmorTreasure_Torso_Shield,
        ArmorTreasure_Torso_ShieldRegen,
        ArmorTreasure_Torso_PowerDamage,
        ArmorTreasure_Torso_PowerRecharge,
        ArmorTreasure_Torso_Movement,
        ArmorTreasure_Torso_WeaponDamage,
        ArmorTreasure_Torso_ConstraintDamage,
        ArmorTreasure_Torso_AmmoCapacity,
        ArmorTreasure_Torso_MeleeDamage,
        ArmorTreasure_Shoulders_Health,
        ArmorTreasure_Shoulders_Shield,
        ArmorTreasure_Shoulders_ShieldRegen,
        ArmorTreasure_Shoulders_PowerDamage,
        ArmorTreasure_Shoulders_PowerRecharge,
        ArmorTreasure_Shoulders_Movement,
        ArmorTreasure_Shoulders_WeaponDamage,
        ArmorTreasure_Shoulders_ConstraintDamage,
        ArmorTreasure_Shoulders_AmmoCapacity,
        ArmorTreasure_Shoulders_MeleeDamage,
        ArmorTreasure_Legs_Health,
        ArmorTreasure_Legs_Shield,
        ArmorTreasure_Legs_ShieldRegen,
        ArmorTreasure_Legs_PowerDamage,
        ArmorTreasure_Legs_PowerRecharge,
        ArmorTreasure_Legs_Movement,
        ArmorTreasure_Legs_WeaponDamage,
        ArmorTreasure_Legs_ConstraintDamage,
        ArmorTreasure_Legs_AmmoCapacity,
        ArmorTreasure_Legs_MeleeDamage,
        ArmorTreasure_Arms_Health,
        ArmorTreasure_Arms_Shield,
        ArmorTreasure_Arms_ShieldRegen,
        ArmorTreasure_Arms_PowerDamage,
        ArmorTreasure_Arms_PowerRecharge,
        ArmorTreasure_Arms_Movement,
        ArmorTreasure_Arms_WeaponDamage,
        ArmorTreasure_Arms_ConstraintDamage,
        ArmorTreasure_Arms_AmmoCapacity,
        ArmorTreasure_Arms_MeleeDamage,
        ArmorTreasure_DLC_01,
        ArmorTreasure_DLC_02,
        ArmorTreasure_DLC_03,
        ArmorTreasure_DLC_04,
        ArmorTreasure_DLC_05,
        ArmorTreasure_DLC_06,
        ArmorTreasure_DLC_07,
        ArmorTreasure_DLC_08,
        ArmorTreasure_DLC_09,
        ArmorTreasure_DLC_10,
        ArmorTreasure_DLC_11,
        ArmorTreasure_DLC_12,
        ArmorTreasure_DLC_13,
        ArmorTreasure_DLC_14,
        ArmorTreasure_DLC_15,
        ArmorTreasure_DLC_16,
        ArmorTreasure_DLC_17,
        ArmorTreasure_DLC_18,
        ArmorTreasure_DLC_19,
        ArmorTreasure_DLC_20,
        ArmorTreasure_DLC_21,
        ArmorTreasure_DLC_22,
        ArmorTreasure_DLC_23,
        ArmorTreasure_DLC_24,
        ArmorTreasure_DLC_25,
        ArmorTreasure_DLC_26,
        ArmorTreasure_DLC_27,
        ArmorTreasure_DLC_28,
        ArmorTreasure_DLC_29,
        ArmorTreasure_DLC_30,
        ArmorTreasure_CollectorsEdition_1,
        ArmorTreasure_CollectorsEdition_2,
        ArmorTreasure_CollectorsEdition_3,
        ArmorTreasure_Helmet_Mnemonic,
        ArmorTreasure_Helmet_Delumcore,
        ArmorTreasure_Helmet_Securitel,
    }
    public enum EPlotElementTypes
    {
        BIO_SE_ELEMENT_TYPE_INT,
        BIO_SE_ELEMENT_TYPE_FLOAT,
        BIO_SE_ELEMENT_TYPE_BOOL,
        BIO_SE_ELEMENT_TYPE_FUNCTION,
        BIO_SE_ELEMENT_TYPE_LOCAL_INT,
        BIO_SE_ELEMENT_TYPE_LOCAL_FLOAT,
        BIO_SE_ELEMENT_TYPE_LOCAL_BOOL,
        BIO_SE_ELEMENT_TYPE_SUBSTATE,
        BIO_SE_ELEMENT_TYPE_CONSEQUENCE,
    }
    public enum EConvGUIStyles
    {
        GUI_STYLE_NONE,
        GUI_STYLE_CHARM,
        GUI_STYLE_INTIMIDATE,
        GUI_STYLE_PLAYER_ALERT,
        GUI_STYLE_ILLEGAL,
    }
    public enum EReplyCategory
    {
        REPLY_CATEGORY_DEFAULT,
        REPLY_CATEGORY_AGREE,
        REPLY_CATEGORY_DISAGREE,
        REPLY_CATEGORY_FRIENDLY,
        REPLY_CATEGORY_HOSTILE,
        REPLY_CATEGORY_INVESTIGATE,
        REPLY_CATEGORY_RENEGADE_INTERRUPT,
        REPLY_CATEGORY_PARAGON_INTERRUPT,
    }
    public enum EInterruptionType
    {
        INTERRUPTION_RENEGADE,
        INTERRUPTION_PARAGON,
    }
    public enum EReplyTypes
    {
        REPLY_STANDARD,
        REPLY_AUTOCONTINUE,
        REPLY_DIALOGEND,
    }
    public enum EBioConversationType
    {
        BIOCONV_NULL,
        BIOCONV_FOVO,
        BIOCONV_Ambient,
        BIOCONV_Full,
    }
    public enum EConvLightingType
    {
        ConvLighting_Cinematic,
        ConvLighting_Exploration,
        ConvLighting_Dynamic,
    }
    public enum ECustomActionPriority
    {
        CA_Priority_None,
        CA_Priority_Low,
        CA_Priority_Medium,
        CA_Priority_High,
        CA_Priority_SuperHigh,
    }
    public enum EBodyStance
    {
        BS_FullBody,
        BS_Standing_Upper,
        BS_Standing_Lower,
        BS_Standing_Cov_Upper,
        BS_Standing_Cov_Lean_Upper,
        BS_Mid_Cov_Upper,
        BS_Mid_Cov_Lean_Upper,
        BS_Mid_Cov_Popup_Upper,
        BS_Standing_Cov_PartLean_Upper,
        BS_Mid_Cov_PartLean_Upper,
        BS_Mid_Cov_PartPopup_Upper,
        BS_Crouching_Upper,
    }
    public enum ECoverBodyStanceID
    {
        ECS_None,
        ECS_FromExplore,
        ECS_FromCombat,
        ECS_FromCover,
    }
    public enum EKroganChargeActions
    {
        EKC_Start,
        EKC_Miss,
        EKC_Hit,
    }
    public enum BioNoticeDisplayTypes
    {
        NOTICE_TYPE_DELTA,
        NOTICE_TYPE_TEXT,
        NOTICE_TYPE_QUANTITY,
        NOTICE_TYPE_QUANTITY_TEXT,
    }
    public enum BioNoticeIcons
    {
        NOTICE_ICON_UNASSIGNED_0,
        NOTICE_ICON_QUEST_UPDATE,
        NOTICE_ICON_LEVELUP,
        NOTICE_ICON_DEFICIENCY,
        NOTICE_ICON_XP,
        NOTICE_ICON_PARAGON,
        NOTICE_ICON_RENEGADE,
        NOTICE_ICON_OMNITOOL,
        NOTICE_ICON_BIOAMP,
        NOTICE_ICON_XMOD,
        NOTICE_ICON_CODEX_ADDED,
        NOTICE_ICON_COIN,
        NOTICE_ICON_MEDIGEL,
        NOTICE_ICON_SALVAGE,
        NOTICE_ICON_PISTOL,
        NOTICE_ICON_SHOTGUN,
        NOTICE_ICON_ASSAULT_RIFLE,
        NOTICE_ICON_SNIPER_RIFLE,
        NOTICE_ICON_ARMOR,
        NOTICE_ICON_GRENADE,
        NOTICE_ICON_QUEST_ADDED,
        NOTICE_ICON_AREAMAPNODE,
    }
    public enum BioNoticeContexts
    {
        NOTICE_CONTEXT_JOURNAL,
        NOTICE_CONTEXT_CODEX,
        NOTICE_CONTEXT_INVENTORY,
        NOTICE_CONTEXT_PARTYLEVEL,
        NOTICE_CONTEXT_XP,
        NOTICE_CONTEXT_MEDIGEL,
        NOTICE_CONTEXT_SALVAGE,
        NOTICE_CONTEXT_CREDITS,
        NOTICE_CONTEXT_GRENADES,
        NOTICE_CONTEXT_PARAGON,
        NOTICE_CONTEXT_RENEGADE,
        NOTICE_CONTEXT_AREAMAP,
        NOTICE_CONTEXT_ABILITY,
    }
    public enum BioQuestEventTypes
    {
        QET_New,
        QET_Updated,
        QET_Completed,
    }
    public enum EBioGestureAllPoses
    {
        GestPose_Unset,
    }
    public enum EBioGestureOverrideType
    {
        DEFAULT_TRACK,
        FEMALE_PLAYER_TRACK,
    }
    public enum EBioGestureValidPoses
    {
        GestValidPoses_Unset,
    }
    public enum EBioGestureValidGestures
    {
        GestValidGest_Unset,
    }
    public enum EBioGestureGroups
    {
        GestGroups_Unset,
    }
    public enum EBioValidPoseGroups
    {
        ValidPoseGroups_Unset,
    }
    public enum EBioTrackAllPoseGroups
    {
        AllPoseGroups_Unset,
    }
    public enum EDynPropList
    {
        DynPropList_Unset,
    }
    public enum EDynPropActionList
    {
        DynPropActionList_Unset,
    }
    public enum EDynamicStageNodes
    {
        EDynamicStageNodes_UNSET,
    }
    public enum EBioSwitchCamSpecific
    {
        SwitchCam_Unset,
    }
    public enum SFXRomanced
    {
        SFXRomanced_NO_ONE,
        SFXRomanced_Ashley,
        SFXRomanced_Kaidan,
        SFXRomanced_Liara,
        SFXRomanced_Miranda,
        SFXRomanced_Garrus,
        SFXRomanced_Jacob,
        SFXRomanced_Thane,
        SFXRomanced_Jack,
        SFXRomanced_Tali,
    }
    public enum SFXME2Plot_CollectorBaseState
    {
        CollectorBase_Irradiate,
        CollectorBase_Destroyed,
    }
    public enum SFXME2Plot_HereticsState
    {
        Heretics_Rewrite,
        Heretics_Destroyed,
        Heretics_NotComplete,
    }
    public enum SFXME1Plot_WrexState
    {
        WREX_ALIVE,
        WREX_DEAD,
        WREX_IGNORED,
    }
    public enum SFXPlotType
    {
        SFXPlotType_Float,
        SFXPlotType_Integer,
        SFXPlotType_Boolean,
    }
    public enum SFXNotificationPriotity
    {
        NOTIFICATIONPRIORITY_UNDEFINED,
        NOTIFICATIONPRIORITY_NORMAL,
        NOTIFICATIONPRIORITY_HIGH,
    }
    public enum SFXPowerTutorialType
    {
        PowerTutorial_Singularity,
    }
    public enum ECommandInputMethod
    {
        CIM_Default,
        CIM_Kinect,
    }
    public enum GUILayout
    {
        GUILayout_PC,
        GUILayout_XBox,
        GUILayout_PS3,
    }
    public enum ESpeechContext
    {
        SpeechContext_Combat,
        SpeechContext_Explore,
        SpeechContext_Global,
        SpeechContext_Conversation,
    }
    public enum EAxisBuffer
    {
        AxisBuffer_LX,
        AxisBuffer_LY,
        AxisBuffer_RX,
        AxisBuffer_RY,
        AxisBuffer_MouseX,
        AxisBuffer_MouseY,
    }
    public enum EGameModes
    {
        GameMode_Default,
        GameMode_Vehicle,
        GameMode_Atlas,
        GameMode_PowerWheel,
        GameMode_WeaponWheel,
        GameMode_Command,
        GameMode_InjuredShepard,
        GameMode_Conversation,
        GameMode_Cinematic,
        GameMode_GUI,
        GameMode_Movie,
        GameMode_Galaxy,
        GameMode_Orbital,
        GameMode_MultiLand,
        GameMode_CheatMenu,
        GameMode_AIDebug,
        GameMode_Prototyping,
        GameMode_DreamSequence,
        GameMode_IllusiveManConflict,
        GameMode_Spectator,
        GameMode_Dying,
        GameMode_FlyCam,
        GameMode_ReplicationDebug,
        GameMode_Lobby,
    }
    public enum BioDUIElements
    {
        BIO_DUI_PassiveTimer,
        BIO_DUI_PassiveCounter,
        BIO_DUI_PassiveText,
        BIO_DUI_PassiveBar,
        BIO_DUI_PassiveBarMarker1,
        BIO_DUI_PassiveBarMarker2,
        BIO_DUI_ModalBar,
        BIO_DUI_ModalBarMarker1,
        BIO_DUI_ModalBarMarker2,
        BIO_DUI_ModalCounter,
        BIO_DUI_ModalTimer,
        BIO_DUI_ModalText,
        BIO_DUI_ModalBackground,
        BIO_DUI_ButtonA,
        BIO_DUI_ButtonAText,
        BIO_DUI_ButtonB,
        BIO_DUI_ButtonBText,
        BIO_DUI_ButtonX,
        BIO_DUI_ButtonXText,
        BIO_DUI_ButtonY,
        BIO_DUI_ButtonYText,
        BIO_DUI_ModalBackground2,
    }
    public enum EBioInterpolationMethod
    {
        BIO_INTERPOLATION_METHOD_LINEAR,
        BIO_INTERPOLATION_METHOD_LOG_E,
        BIO_INTERPOLATION_METHOD_QUARTER_SIN,
    }
    public enum EBioPlotAutoSet
    {
        Plot_Unset,
    }
    public enum EBioAutoSet
    {
        Unset,
    }
    public enum WeaponAnimType
    {
        WeaponAnimType_Pistol,
        WeaponAnimType_Shotgun,
        WeaponAnimType_Rifle,
        WeaponAnimType_Sniper,
        WeaponAnimType_GrenadeLauncher,
        WeaponAnimType_MissileLauncher,
        WeaponAnimType_NukeLauncher,
        WeaponAnimType_ParticleBeam,
        WeaponAnimType_RepulsorBeam,
        WeaponAnimType_AutoShotgun,
        WeaponAnimType_AutoSniper,
        WeaponAnimType_AutoPistol,
    }
    public enum EBioRegionAutoSet
    {
        Region_Unset,
    }
    public enum EBioMorphUtilityComponentType
    {
        BMU_Component_Unknown,
        BMU_Component_Picker,
        BMU_Component_Slider,
        BMU_Component_Combo,
        BMU_Component_RGBA,
        BMU_Component_Compound,
    }
    public enum EBioMorphUtilityHairComponentType
    {
        BMU_HairComponent_Hair,
        BMU_HairComponent_Other,
    }
    public enum EBioMorphFrontendSliderType
    {
        BMFE_SLIDER_MORPH_SINGLE,
        BMFE_SLIDER_MORPH_DOUBLE,
        BMFE_SLIDER_MATERIAL,
    }
    public enum EmissionAreaSpecificationType
    {
        EAST_UniformDensityPerVertex,
        EAST_UniformDensityPerBone,
        EAST_WeightedDensityPerBone,
        EAST_WeightedDensityPerEmissionArea,
        EAST_UniformDensityPerEmissionArea,
    }
    public enum ELocationNearestSurface
    {
        eLocationNearestSurface_Stay,
        eLocationNearestSurface_StayAtRadius,
        eLocationNearestSurface_Kill,
    }
    public enum EBioParticleCollisionComplete
    {
        EBPCC_DoNothing,
        EBPCC_Kill,
        EBPCC_Freeze,
        EBPCC_FreezeTranslation,
        EBPCC_FreezeRotation,
        EBPCC_FreezeMovement,
    }
    public enum MultiplyByEmitterSpeedProperty
    {
        MESProperty_SpawnRate,
    }
    public enum EInstanceVersion
    {
        ParticleModSound_OriginalVer,
        ParticleModSound_PerParticleVer,
        ParticleModSound_MaxVer,
    }
    public enum EBioPathNodeAlignment
    {
        BIO_PATH_ALIGN_NONE,
        BIO_PATH_ALIGN_CENTER,
        BIO_PATH_ALIGN_JUSTIFY,
    }
    public enum EBioPathNodeGenerators
    {
        PATHNODE_SQUARE,
    }
    public enum EWeaponRange
    {
        WeaponRange_Invalid,
        WeaponRange_Melee,
        WeaponRange_Short,
        WeaponRange_Medium,
        WeaponRange_Long,
    }
    public enum EAimNodes
    {
        AimNode_Cover,
        AimNode_Head,
        AimNode_LeftShoulder,
        AimNode_RightShoulder,
        AimNode_Chest,
        AimNode_Groin,
        AimNode_LeftKnee,
        AimNode_RightKnee,
    }
    public enum EBioAnimNodeCombatModeFadeOut
    {
        BIO_ANIM_NODE_COMBAT_MODE_FADEOUT_NONE,
        BIO_ANIM_NODE_COMBAT_MODE_FADEOUT_ANIMATING_ENTER,
        BIO_ANIM_NODE_COMBAT_MODE_FADEOUT_ENTER,
        BIO_ANIM_NODE_COMBAT_MODE_FADEOUT_ANIMATING_EXIT,
        BIO_ANIM_NODE_COMBAT_MODE_FADEOUT_EXIT,
    }
    public enum EReplicatedCustomActionCmd
    {
        eRCACmd_Start,
        eRCACmd_Override,
        eRCACmd_Interrupt,
    }
    public enum EBioAnimStopState
    {
        eBioAnimStop_NoState,
        eBioAnimStop_StopLeftMove,
        eBioAnimStop_StopRightMove,
        eBioAnimStop_FinishLeftMove,
        eBioAnimStop_FinishRightMove,
        eBioAnimStop_InterruptLeftMove,
        eBioAnimStop_InterruptRightMove,
        eBioAnimStop_DoneFinishLeftMove,
        eBioAnimStop_DoneFinishRightMove,
        eBioAnimStop_DoneIntLeftMove,
        eBioAnimStop_DoneIntRightMove,
    }
    public enum EBioAnimSkidTurnState
    {
        eBioAnimSkid_NoState,
        eBioAnimSkid_StartingLeft,
        eBioAnimSkid_StartingRight,
        eBioAnimSkid_TurningLeft,
        eBioAnimSkid_TurningRight,
        eBioAnimSkid_FinishingLeft,
        eBioAnimSkid_FinishingRight,
    }
    public enum EBioAnimTurnDirState
    {
        eBioAnimTurn_NoTurn,
        eBioAnimTurn_ReqStartLeft,
        eBioAnimTurn_ReqStartRight,
        eBioAnimTurn_AckStartLeft,
        eBioAnimTurn_AckStartRight,
        eBioAnimTurn_ProcessLeft,
        eBioAnimTurn_ProcessRight,
    }
    public enum EBioAnimStartState
    {
        eBioAnimStart_NoState,
        eBioAnimStart_StartingMove,
        eBioAnimStart_FinishStartMove,
        eBioAnimStart_DoneStartMove,
        eBioAnimStart_RotationUnlocked,
        eBioAnimStart_PlayingMove,
    }
    public enum EBioAnimGetUpState
    {
        eBioAnimGetUp_Idle,
        eBioAnimGetUp_Start,
        eBioAnimGetUp_Processing,
    }
    public enum ESFXLocomotionState
    {
        eSFXLocomotionState_Inactive,
        eSFXLocomotionState_Moving,
        eSFXLocomotionState_Idle,
        eSFXLocomotionState_MoveStart,
        eSFXLocomotionState_MoveStop,
        eSFXLocomotionState_SkidTurn,
    }
    public enum EWalkingSpeedMode
    {
        eWalkingSpeedMode_ExploreRun,
        eWalkingSpeedMode_ExploreWalk,
        eWalkingSpeedMode_ExploreStorming,
        eWalkingSpeedMode_ExploreCrouched,
        eWalkingSpeedMode_CombatRun,
        eWalkingSpeedMode_CombatWalk,
        eWalkingSpeedMode_CombatStorming,
        eWalkingSpeedMode_CombatCrouched,
        eWalkingSpeedMode_CombatSniping,
        eWalkingSpeedMode_CombatZoomed,
        eWalkingSpeedMode_CoverMove,
        eWalkingSpeedMode_CoverCrouched,
    }
    public enum EAttachSlot
    {
        EASlot_Holster,
        EASlot_LowerBack,
        EASlot_LeftShoulder,
        EASlot_RightShoulder,
        EASlot_CenterBack,
    }
    public enum EBonusFormula
    {
        BonusFormula_Add,
        BonusFormula_Substract,
        BonusFormula_LargestValue,
        BonusFormula_Custom,
    }
    public enum EDurationType
    {
        DurationType_Instant,
        DurationType_Temporary,
        DurationType_Permanent,
    }
    public enum EHealthType
    {
        HealthType_Default,
        HealthType_Shields,
        HealthType_Barrier,
        HealthType_Armour,
    }
    public enum ESFXAmbientPerfGroupEnum
    {
        SFXAmbPerfGroup_Unset,
    }
    public enum ESFXAmbientPerformanceEnum
    {
        SFXAmbPerf_Unset,
    }
    public enum ESFXAmbientPoseGroupEnum
    {
        SFXAmbPoseGroup_Unset,
    }
    public enum ESFXDefaultPoseEnum
    {
        SFXDefPose_Unset,
    }
    public enum ESFXGalaxyMapObjectLevel
    {
        GalaxyMapObjType_Undefined,
        GalaxyMapObjType_GalaxyLevel,
        GalaxyMapObjType_ClusterLevel,
        GalaxyMapObjType_SystemLevel,
        GalaxyMapObjType_PlanetLevel,
    }
    public enum EGalaxyObjectVisiblePlotAutoSet
    {
        GalaxyObjectVisiblePlot_Unset,
    }
    public enum EGalaxyObjectVisibleAutoSet
    {
        GalaxyObjectVisible_Unset,
    }
    public enum EGalaxyObjectUsablePlotAutoSet
    {
        GalaxyObjectUsablePlot_Unset,
    }
    public enum EGalaxyObjectUsableAutoSet
    {
        GalaxyObjectUsable_Unset,
    }
    public enum ESystemLevelType
    {
        SL_PLANET,
        SL_ANOMALY,
        SL_RINGPLANET,
        SL_MASSRELAY,
        SL_DEPOT,
        SL_SUN,
    }
    public enum EOrbitRingType
    {
        OR_NONE,
        OR_ORBIT,
        OR_ASTEROID,
    }
    public enum EPlanetType
    {
        NOSCAN_PLANET,
        ROCK_PLANET,
        DESERT_PLANET,
        OCEAN_PLANET,
        GARDEN_PLANET,
        GIANT_ICE_PLANET,
        GIANT_JOVIAN_PLANET,
        GIANT_PEGASID_PLANET,
        POST_GARDEN,
        BROWN_DWARF,
        TIDAL_LOCK,
    }
    public enum EBioPlanetEventConditionPlotAutoSet
    {
        BioPlanetEventConditionPlot_Unset,
    }
    public enum EBioPlanetEventConditionAutoSet
    {
        BioPlanetEventCondition_Unset,
    }
    public enum EBioPlanetEventTransitionPlotAutoSet
    {
        BioPlanetEventTransitionPlot_Unset,
    }
    public enum EBioPlanetEventTransitionAutoSet
    {
        BioPlanetEventTransition_Unset,
    }
    public enum EBioPlanetLandConditionPlotAutoSet
    {
        BioPlanetLandConditionPlot_Unset,
    }
    public enum EBioPlanetLandConditionAutoSet
    {
        BioPlanetLandCondition_Unset,
    }
    public enum EBioPlanetPlotLabelConditionPlotAutoSet
    {
        BioPlanetPlotLabelConditionPlot_Unset,
    }
    public enum EBioPlanetPlotLabelConditionAutoSet
    {
        BioPlanetPlotLabelCondition_Unset,
    }
    public enum EMineralType
    {
        MINERAL_RED,
        MINERAL_BLUE,
        MINERAL_GREEN,
        MINERAL_ALPHA,
    }
    public enum ESFXPlanetFeatureEventTransitionPlotAutoSet
    {
        SFXPlanetFeatureEventTransitionPlot_Unset,
    }
    public enum ESFXPlanetFeatureEventTransitionAutoSet
    {
        SFXPlanetFeatureEventTransition_Unset,
    }
    public enum EFeatureType
    {
        FEATURE_INVALID,
        FEATURE_MINERAL,
        FEATURE_LABEL,
        FEATURE_PROBES,
        FEATURE_ARTIFACT,
        FEATURE_LANDINGSITE,
        FEATURE_ANOMOLY,
    }
    public enum ETutorialHooks
    {
        TUT_Storm,
        TUT_Cover,
        TUT_Mantle,
        TUT_MeleeHeavy,
        TUT_MeleeLight,
        TUT_AmmoPickup,
        TUT_Reload,
        TUT_WeaponSwapWheel,
        TUT_WeaponSwapButton,
        TUT_CastPowerWheel,
        TUT_CastPowerButton,
        TUT_AssignedPower,
        TUT_MeleeFromCover,
        TUT_CoverSlip,
        TUT_SWATRoll,
        TUT_TurnCoverCorner,
        TUT_SquadCommand,
        TUT_ClimbUp,
        TUT_ObjectiveCheck,
    }
    public enum ECoverVisualizations
    {
        CV_None,
        CV_Mantle,
    }
    public enum AttackResult
    {
        ATTACK_SUCCESS,
        ATTACK_FAIL,
        ATTACK_FAIL_RELOADING,
        ATTACK_FAIL_NO_LOS,
    }
    public enum EAITicketType
    {
        AI_NoTicket,
        AI_TargetTicket,
        AI_AttackTicket,
    }
    public enum KismetOrderType
    {
        KISMET_ORDER_NONE,
        KISMET_ORDER_FIRE_WEAPON,
        KISMET_ORDER_MOVE,
    }
    public enum HenchmanOrderType
    {
        HENCHMAN_ORDER_NONE,
        HENCHMAN_ORDER_USE_POWER,
        HENCHMAN_ORDER_SWITCH_WEAPON,
        HENCHMAN_ORDER_ATTACK_TARGET,
        HENCHMAN_ORDER_FOLLOW,
        HENCHMAN_ORDER_HOLD_POSITION,
    }
    public enum AimProfiles
    {
        AimProfile_Rifle,
        AimProfile_Pistol,
        AimProfile_PistolShield,
    }
    public enum IKProfiles
    {
        IKProfile_Rifle,
        IKProfile_Pistol,
        IKProfile_SMG,
    }
    public enum FireModes
    {
        FireMode_None,
        FireMode_SemiAuto,
        FireMode_FullAuto,
        FireMode_Burst,
        FireMode_Reload,
    }
    public enum EWeaponStatBars
    {
        EWeaponStatBarAccuracy,
        EWeaponStatBarDamage,
        EWeaponStatBarFireRate,
        EWeaponStatBarMagSize,
        EWeaponStatBarWeight,
        EWeaponStatBarOther,
        EWeaponStatBar_MAX,
    }
    public enum EExperienceSourceType
    {
        EXPSourceType_SimpleDeath,
        EXPSourceType_SkillUse,
        EXPSourceType_QuestCompletion,
        EXPSourceType_Generic,
    }
    public enum EBioPowerType
    {
        BIO_POWER_TYPE_UNKNOWN,
        BIO_POWER_TYPE_CYLINDER,
        BIO_POWER_TYPE_TARGET,
        BIO_POWER_TYPE_PARTY,
        BIO_POWER_TYPE_IMPACT_VOLUME,
        BIO_POWER_TYPE_MELEE,
    }
    public enum BlackScreenActionSet
    {
        BlackScreenAction_TurnBlackOn,
        BlackScreenAction_TurnBlackOff,
        BlackScreenAction_FadeToBlack,
        BlackScreenAction_FadeFromBlack,
    }
    public enum EBioFOVOLines
    {
        FOVOLines_Unset,
    }
    public enum EBioFOVOSpeakers
    {
        FOVOSpeakers_Unset,
    }
    public enum EScalarMathOps
    {
        SMO_Add,
        SMO_Subtract,
        SMO_Multiply,
        SMO_Divide,
        SMO_Exponent,
        SMO_Modulo,
    }
    public enum BioBrowserStates
    {
        BBS_NORMAL,
        BBS_ALERT,
        BBS_DISABLED,
    }
    public enum BioLocalVariableObjectType
    {
        BIO_LVOT_PLAYER,
        BIO_LVOT_OWNER,
        BIO_LVOT_TARGET,
        BIO_LVOT_BYTAG,
        BIO_LVOT_SPEAKER,
    }
    public enum EPlayerRenderStateSetting
    {
        PRSS_NEARCLIP,
    }
    public enum JournalSortMethods
    {
        JSM_Newest,
        JSM_Name,
        JSM_Oldest,
    }
    public enum ESubtitlesRenderMode
    {
        SUBTITLE_RENDER_NONE,
        SUBTITLE_RENDER_DEFAULT,
        SUBTITLE_RENDER_TOP,
        SUBTITLE_RENDER_BOTTOM,
        SUBTITLE_RENDER_ABOVE_WHEEL,
        SUBTITLE_RENDER_LOADSCREEN,
    }
    public enum MEBrowserWheelSubPages
    {
        MBW_SP_Map,
        MBW_SP_Save,
        MBW_SP_SquadRecord,
        MBW_SP_Load,
        MBW_SP_Journal,
        MBW_SP_DataPad,
        MBW_SP_Options,
        MBW_SP_ReturnToMainMenu,
        MBW_SP_ExitGame,
        MBW_SP_Manual,
    }
    public enum EBioSetGestureModes
    {
        GestureMode_On,
        GestureMode_Off,
    }
    public enum EBioSeqActSetWeaponLinks
    {
        EBioSeqActSetWeaponLinks_Success,
        EBioSeqActSetWeaponLinks_Failure,
    }
    public enum ESkillGameComplete
    {
        SK_CANCEL,
        SK_SUCCESS,
        SK_FAIL,
        SK_UNDEFINED,
    }
    public enum ToggleLightEnvType
    {
        TLET_ENABLE,
        TLET_DISABLE,
        TLET_TOGGLE,
    }
    public enum EConversationScriptType
    {
        NodeEnd,
        NodeStart,
        StartConversationScript,
        EndConversationScript,
        SwitchFromFullToAmbient,
    }
    public enum SFXChoiceColors
    {
        CHOICECOLOR_Orange,
        CHOICECOLOR_Red,
        CHOICECOLOR_Green,
    }
    public enum EChoiceDisplayType
    {
        EChoiceDisplayType_Normal,
        EChoiceDisplayType_Nested,
        EChoiceDisplayType_Special,
        EChoiceDisplayType_None,
    }
    public enum BlackScreenDisplayModes
    {
        BlackScreenMode_None,
        BlackScreenMode_TurnBlackOn,
        BlackScreenMode_TurnBlackOff,
        BlackScreenMode_FadeToBlack,
        BlackScreenMode_FadeFromBlack,
    }
    public enum EPowerType
    {
        PowerType_Instant,
        PowerType_Projectile,
        PowerType_Melee,
        PowerType_Buff,
    }
    public enum EPowerStatBarFormula
    {
        EPowerStatBarFormula_Normal,
        EPowerStatBarFormula_Percent,
        EPowerStatBarFormula_Distance,
    }
    public enum EPowerDataFormula
    {
        Normal,
        BonusIsHardValue,
        DivideByBonusSum,
    }
    public enum EEvolveChoice
    {
        EvolveChoice1,
        EvolveChoice2,
        EvolveChoice3,
        EvolveChoice4,
        EvolveChoice5,
        EvolveChoice6,
    }
    public enum EChoiceGUIHandlerID
    {
        CHOICEHANDLER_NONE,
        CHOICEHANDLER_SELECTED,
        CHOICEHANDLER_EXIT,
        CHOICEHANDLER_UPDATE_IMAGE,
        CHOICEHANDLER_SHOW_CREDITS,
    }
    public enum BioConvWheelPositions
    {
        REPLY_WHEEL_MIDDLE_RIGHT,
        REPLY_WHEEL_BOTTOM_RIGHT,
        REPLY_WHEEL_BOTTOM_LEFT,
        REPLY_WHEEL_MIDDLE_LEFT,
        REPLY_WHEEL_TOP_LEFT,
        REPLY_WHEEL_TOP_RIGHT,
    }
    public enum ESFXGalaxyMapUIAction
    {
        GalaxyAction_None,
        GalaxyAction_Exit,
        GalaxyAction_LeavePlanet,
        GalaxyAction_EnterPlanetScan,
        GalaxyAction_LeavePlanetScan,
        GalaxyAction_SystemObjectAction,
        GalaxyAction_PlanetAction,
        GalaxyAction_MassRelayJump,
        GalaxyAction_BuyFuel,
        GalaxyAction_ClusterSelect,
        GalaxyAction_SystemSelect,
        GalaxyAction_MultiLandLand,
    }
    public enum SFXWeaponPickupUIOption
    {
        WEAPPICKUP_AddToInventory,
        WEAPPICKUP_OpenInventory,
        WEAPPICKUP_Equip,
    }
    public enum EPlotChoice
    {
        EPlotChoice_None,
        EPlotChoice_KaidenDies,
        EPlotChoice_AshleyDies,
    }
    public enum EDataOrigin
    {
        DataOrigin_NewGame,
        DataOrigin_ME1,
        DataOrigin_ME2,
        DataOrigin_ME3,
    }
    public enum ECreateCharacterGUIGender
    {
        ECreateCharacterGUIGender_Male,
        ECreateCharacterGUIGender_Female,
    }
    public enum BioNewCharacterTemplates
    {
        BNCT_ICONIC,
        BNCT_CUSTOM,
        BNCT_IMPORTED,
    }
    public enum NewCharacterLookAtTarget
    {
        NCLAT_Ahead,
        NCLAT_Left,
        NCLAT_Right,
        NCLAT_Up,
        NCLAT_Down,
    }
    public enum EGuiOptions
    {
        OPTION_NULL,
        OPTION_Difficulty,
        OPTION_AutoLvlUp,
        OPTION_Subtitles,
        OPTION_SquadPower,
        OPTION_AutoSave,
        OPTION_ControlBindings,
        OPTION_MouseInvert,
        OPTION_MouseSmooth,
        OPTION_MouseSensitivity,
        OPTION_Resolution,
        OPTION_WindowMode,
        OPTION_Gamma,
        OPTION_Bloom,
        OPTION_DynShadows,
        OPTION_EnvShadows,
        OPTION_FilmGrain,
        OPTION_MusicVolume,
        OPTION_SFXVolume,
        OPTION_DlgVolume,
        OPTION_OnlineTelemetry,
        OPTION_OnlineAutoLogin,
        OPTION_ControllerRumble,
        OPTION_InvertYAxis,
        OPTION_ControllerSensitivity,
        OPTION_StickConfig,
        OPTION_TriggerConfig,
        OPTION_SwapTriggersShoulders,
        OPTION_MotionBlur,
        OPTION_AimAssist,
        OPTION_HideCinematicHelmet,
        OPTION_HenchHelmetOption,
        OPTION_ActionUIHints,
        OPTION_TextLanguage,
        OPTION_VOLanguage,
        OPTION_SpeechLanguage,
        OPTION_MPAutoLvlUp,
        OPTION_MPVoiceChatInputDevice,
        OPTION_MPVoiceChatOutputDevice,
        OPTION_MPVoiceChatVolume,
        OPTION_MPVoiceChatMode,
        OPTION_MPVoiceContinueOnLostFocus,
        OPTION_AutoReplyMode,
        OPTION_AudioDynamicRange,
        OPTION_Hints,
        OPTION_AntiAliasing,
        OPTION_CONTENT_1,
        OPTION_CONTENT_2,
        OPTION_CONTENT_3,
        OPTION_CONTENT_4,
        OPTION_CONTENT_5,
        OPTION_CONTENT_6,
        OPTION_CONTENT_7,
        OPTION_CONTENT_8,
        OPTION_CONTENT_9,
        OPTION_CONTENT_10,
        OPTION_CONTENT_11,
        OPTION_CONTENT_12,
        OPTION_CONTENT_13,
        OPTION_CONTENT_14,
        OPTION_CONTENT_15,
        OPTION_CONTENT_16,
        OPTION_CONTENT_17,
        OPTION_CONTENT_18,
        OPTION_CONTENT_19,
        OPTION_CONTENT_20,
        OPTION_CONTENT_21,
        OPTION_CONTENT_22,
        OPTION_CONTENT_23,
        OPTION_CONTENT_24,
        OPTION_CONTENT_25,
    }
    public enum EOptionsGuiMode
    {
        GuiMode_BrowserWheel,
        GuiMode_MainMenu,
        GuiMode_NewGame,
        GuiMode_Multiplayer,
    }
    public enum MessageBoxIcon
    {
        MBI_None,
        MBI_Error,
        MBI_Warning,
        MBI_Alert,
    }
    public enum EEffectLocationReference
    {
        ELR_Actor,
        ELR_Bone,
        ELR_Socket,
        ELR_HitBone,
        ELR_TargetSocket,
        ELR_TargetBone,
    }
    public enum EModuleTickGroup
    {
        MTG_Main,
        MTG_Location,
        MTG_Parameters,
    }
    public enum EEffectRotationTarget
    {
        ERotTarget_OppositeRayDir,
        ERotTarget_RayDir,
        ERotTarget_AwayFromImpact,
        ERotTarget_IntoImpact,
        ERotTarget_World,
        ERotTarget_Actor,
        ERotTarget_Bone,
    }
    public enum EEffectBoneAxis
    {
        EBoneAxis_All,
        EBoneAxis_X,
        EBoneAxis_Y,
        EBoneAxis_Z,
    }
    public enum EClientEffectMaterial
    {
        CEM_Dummy,
    }
    public enum FallBackMethod
    {
        eFBM_None,
        eFBM_MoveToPlayer,
        eFBM_TeleportToNearbyNode,
        eFBM_DirectTeleport,
        eFBM_TeleportWhileVisible,
    }
    public enum ESFXAnimNodeBlendStrafe
    {
        eSFXAnimNodeBlendStrafe_Forward,
        eSFXAnimNodeBlendStrafe_ForwardLeft,
        eSFXAnimNodeBlendStrafe_Left,
        eSFXAnimNodeBlendStrafe_BackwardLeft,
        eSFXAnimNodeBlendStrafe_Backward,
        eSFXAnimNodeBlendStrafe_BackwardRight,
        eSFXAnimNodeBlendStrafe_Right,
        eSFXAnimNodeBlendStrafe_ForwardRight,
    }
    public enum SFXAreaMapLayout
    {
        AM_CitDock,
        AM_CitEmb,
        AM_CitHosp,
        AM_CitPurg,
        AM_CitCommons,
        AM_CitCamp,
        AM_Biop_nor_1,
        AM_Biop_nor_2,
        AM_Biop_nor_3,
        AM_Biop_nor_4,
        AM_Biop_nor_5,
    }
    public enum EMoveStage
    {
        EMS_Sync,
        EMS_Start,
        EMS_Loop,
        EMS_End,
    }
    public enum ETimelineType
    {
        TLT_None,
        TLT_Visual,
        TLT_Sound,
        TLT_Voc,
        TLT_Rumble,
        TLT_ScreenShake,
        TLT_TimeDilation,
        TLT_Ragdoll,
        TLT_Reaction,
        TLT_Damage,
        TLT_AOE,
        TLT_AOEVisiblePawns,
        TLT_AOESingle,
        TLT_SyncPartner,
        TLT_Timeline,
        TLT_InputOn,
        TLT_InputOff,
        TLT_Function,
        TLT_RadialBlurOn,
        TLT_RadialBlurOff,
        TLT_CameraAnim,
        TLT_ClientEffect,
        TLT_ClientEffect_Stop,
        TLT_GameEffect,
    }
    public enum ETimelineTarget
    {
        TRG_Source,
        TRG_Target,
    }
    public enum ETimelineAOEType
    {
        AOE_Radius,
        AOE_Cone,
    }
    public enum ECustomActionLoopState
    {
        LoopState_Start,
        LoopState_Loop,
        LoopState_End,
    }
    public enum EInteractionAnimStage
    {
        IS_Start,
        IS_Loop,
        IS_End,
    }
    public enum ESFXDoorType
    {
        EDT_Manual,
        EDT_Proximity,
        EDT_AutoEntrance,
        EDT_AutoExit,
    }
    public enum ESFXDoorState
    {
        EDS_Closed,
        EDS_Open,
        EDS_Hackable,
        EDS_PlotLocked,
        EDS_Disabled,
        EDS_Delayed,
        EDS_DelayedActive,
    }
    public enum EGameModePriority2
    {
        ModePriority_Base,
        ModePriority_CheatMenu,
        ModePriority_Conversation,
        ModePriority_Menus,
        ModePriority_Popup,
    }
    public enum EGAWZone
    {
        EGAWZone_InnerCouncil,
        EGAWZone_Terminus,
        EGAWZone_Earth,
        EGAWZone_Council,
        EGAWZone_Attican,
    }
    public enum ECreditEntryType
    {
        CREDIT_Heading,
        CREDIT_Flashing,
        CREDIT_Scrolling,
        CREDIT_Delay,
        CREDIT_LineBreak,
    }
    public enum EFlashingCreditState
    {
        FLASHCREDIT_FadeIn,
        FLASHCREDIT_Hold,
        FLASHCREDIT_FadeOut,
        FLASHCREDIT_Delay,
    }
    public enum MMM_Status
    {
        MMM_DataUnloaded,
        MMM_DataLoading,
        MMM_DataLoadFailed,
        MMM_DataLoadSuccess,
        MMM_DataLoadProcessed,
    }
    public enum EMM_GameType
    {
        MM_GameType_None,
        MM_GameType_New,
        MM_GameType_Plus,
        MM_GameType_Legacy,
    }
    public enum PowerStateEnum
    {
        STATE_LOCKED,
        STATE_UNLOCKED,
    }
    public enum EvolutionStateEnum
    {
        STATE_LOCKED,
        STATE_BUYABLE,
        STATE_BOUGHT,
    }
    public enum EItemType
    {
        TYPE_MOD,
        TYPE_QUEST,
        TYPE_DECORATION,
        TYPE_WEAPON,
        TYPE_WEAPONUPGRADE,
        TYPE_HELMET,
        TYPE_TORSO,
        TYPE_SHOULDERS,
        TYPE_LEGS,
        TYPE_ARMS,
        TYPE_UNIQUEARMOR,
        TYPE_PARTBASEDARMOR,
        TYPE_MEDIGEL,
        TYPE_POWER,
        TYPE_TALENTRESET,
        TYPE_INTELREWARD,
        TYPE_NESTEDCATEGORY,
        TYPE_RETURN,
        TYPE_INTELSUMMARY,
    }
    public enum EWeaponModCategory
    {
        WModCategory_Uncategorized,
        WModCategory_Barrel,
        WModCategory_Scope,
        WModCategory_Body,
        WModCategory_Grip,
        WModCategory_Emissive,
        WModCategory_Blade,
        WModCategory_DLC1,
        WModCategory_DLC2,
        WModCategory_DLC3,
        WModCategory_DLC4,
        WModCategory_DLC5,
        WModCategory_DLC6,
        WModCategory_DLC7,
        WModCategory_DLC8,
        WModCategory_DLC9,
        WModCategory_DLC10,
    }
    public enum EPlayerCountdownTypes
    {
        PCT_Death,
        PCT_ThisPlayerRevive,
        PCT_OtherPlayerRevive,
    }
    public enum SFXFontStyle
    {
        SFXFONT_Normal,
        SFXFONT_Italic,
        SFXFONT_Bold,
        SFXFONT_BoldItalic,
        SFXFONT_FauxItalic,
        SFXFONT_FauxBold,
        SFXFONT_FauxBoldItalic,
    }
    public enum WeaponUIModSlot
    {
        UIModSlot_1,
        UIModSlot_2,
    }
    public enum Keyboard_Options
    {
        KEYBOARD_STANDARD,
        KEYBOARD_EMAIL,
        KEYBOARD_PASSWORD,
        KEYBOARD_CODE,
    }
    public enum MouseSupportLevel
    {
        Mouse_None,
        Mouse_Click,
        Mouse_ClickAndMove,
    }
    public enum SFXPowerWheelMapButtonIcon
    {
        PWBI_Icon_NONE,
        PWBI_FaceButtonTop,
        PWBI_FaceButtonLeft,
        PWBI_DPadLeft,
        PWBI_DPadRight,
        PWBI_ShoulderLeft,
        PWBI_ShoulderRight,
        PWBI_TriggerLeft,
        PWBI_TriggerRight,
        PWBI_ICON_COUNT,
    }
    public enum SFXPowerWheelPowerState
    {
        PWPS_Selectable,
        PWPS_Selected,
        PWPS_Inactive,
        PWPS_Activated,
        PWPS_Overload,
        PWPS_EmptySelectable,
        PWPS_EmptySelected,
        PWPS_NotSuggested,
        PWPS_STATE_COUNT,
    }
    public enum EPlayerPositionRTPC
    {
        EXPLORE,
        UPRIGHT_NORMAL,
        UPRIGHT_IRONSIGHTS,
        UPRIGHT_SCOPE_SNIPER,
        UPRIGHT_SCOPE_OTHER,
        COVER_NORMAL,
        COVER_IRONSIGHTS,
        COVER_SCOPE_SNIPER,
        COVER_SCOPE_OTHER,
    }
    public enum ChargeEffectType
    {
        CET_ShutOffAll,
        CET_StopCharge,
        CET_StartCharge,
    }
    public enum EWaveCoordinator_HordeOpEvent
    {
        EW_StartWaves,
        EW_BeginWave,
        EW_BeginSupplyWave,
        EW_FinishWave,
        EW_JoinInProgress,
    }
    public enum EMoviePlayState
    {
        EMPS_Play,
        EMPS_Stop,
        EMPS_Pause,
    }
    public enum ETreasureType
    {
        AMMO_TREASURE,
        MEDIGEL_TREASURE,
        CREDITS_TREASURE,
        GRENADE_TREASURE,
    }
    public enum CREDCurveSet
    {
        CREDCurve_NoReward,
        CREDCurve_Minor,
        CREDCurve_Small,
        CREDCurve_Medium,
        CREDCurve_Large,
        CREDCurve_Major,
    }
    public enum LoadingMovieState
    {
        LMS_NotPlaying,
        LMS_Playing,
    }
    public enum EBleedoutState
    {
        BleedOutState_None,
        BleedOutState_InBleedOut,
        BleedOutState_ShieldGate,
        BleedOutState_HealthGate,
    }
    public enum EWoundSeverity
    {
        WoundSev_Light,
        WoundSev_Medium,
        WoundSev_Heavy,
    }
    public enum EObjectiveLocation
    {
        EObjectiveLocation_Table,
        EObjectiveLocation_Floor,
    }
    public enum EBotGameContext
    {
        BGC_Unknown,
        BGC_Splash,
        BGC_MainMenu,
        BGC_LobbyCharacter,
        BGC_LobbyMain,
        BGC_InGame,
        BGC_GameOver,
        BGC_Prompt,
    }
    public enum EBotAction
    {
        BOT_NoAction,
        BOT_MoveForward,
        BOT_MoveBackward,
        BOT_Disconnect,
    }
    public enum EAGUI_MsgBoxResult
    {
        EAG_MSGBOX_BTN_1,
        EAG_MSGBOX_BTN_2,
        EAG_MSGBOX_BTN_3,
        EAG_MSGBOX_BTN_4,
    }
    public enum EAGUI_CerberusIntroResult
    {
        EAG_CI_REDEEM_CODE,
        EAG_CI_BUY_CODE,
        EAG_CI_CANCEL,
    }
    public enum EAGUI_NucleusLoginResult
    {
        EAG_NL_LOGIN,
        EAG_NL_CREATE,
        EAG_NL_CANCEL,
    }
    public enum EAGUI_EmailPswdMismatchResult
    {
        EAG_EPM_SUBMIT,
        EAG_EPM_EMAIL_PSWD,
        EAG_EPM_CANCEL,
    }
    public enum MPFlowType
    {
        MPF_Inactive,
        MPF_Connect,
        MPF_ResolvingInvite,
        MPF_LobbyAccess,
    }
    public enum EStorageField
    {
        SGF_Base,
        SGF_Class00,
        SGF_Class01,
        SGF_Class02,
        SGF_Class03,
        SGF_Class04,
        SGF_Class05,
        SGF_Class06,
        SGF_Class07,
        SGF_Character00,
        SGF_Character01,
        SGF_Character02,
        SGF_Character03,
        SGF_Character04,
        SGF_Character05,
        SGF_Character06,
        SGF_Character07,
        SGF_Character08,
        SGF_Character09,
        SGF_Character10,
        SGF_Character11,
        SGF_Character12,
        SGF_Character13,
        SGF_Character14,
        SGF_Character15,
        SGF_Character16,
        SGF_Character17,
        SGF_Character18,
        SGF_Character19,
        SGF_Character20,
        SGF_Character21,
        SGF_Character22,
        SGF_Character23,
        SGF_Character24,
        SGF_Character25,
        SGF_Character26,
        SGF_Character27,
        SGF_Character28,
        SGF_Character29,
        SGF_Character30,
        SGF_Character31,
        SGF_Character32,
        SGF_Character33,
        SGF_Character34,
        SGF_Character35,
        SGF_Character36,
        SGF_Character37,
        SGF_Character38,
        SGF_Character39,
        SGF_Character40,
        SGF_Character41,
        SGF_Character42,
        SGF_Character43,
        SGF_Character44,
        SGF_Character45,
        SGF_Character46,
        SGF_Character47,
        SGF_Character48,
        SGF_Character49,
        SGF_Character50,
        SGF_Character51,
        SGF_Character52,
        SGF_Character53,
        SGF_Character54,
        SGF_Character55,
        SGF_Character56,
        SGF_Character57,
        SGF_Character58,
        SGF_Character59,
        SGF_Character60,
        SGF_Character61,
        SGF_Character62,
        SGF_Character63,
        SGF_Character64,
        SGF_Character65,
        SGF_Character66,
        SGF_Character67,
        SGF_Character68,
        SGF_Character69,
        SGF_Character70,
        SGF_Character71,
        SGF_Character72,
        SGF_Character73,
        SGF_Character74,
        SGF_Character75,
        SGF_Character76,
        SGF_Character77,
        SGF_FaceCodes,
        SGF_NewReinforcements,
        ASF_Completion,
        ASF_Progress,
    }
    public enum EMPSerializationResult
    {
        EMPSerializationResult_Success,
        EMPSerializationResult_Failure,
        EMPSerializationResult_TooOld,
        EMPSerializationResult_TooNew,
        EMPSerializationResult_ForcedWipe,
    }
    public enum ECharacterNameResult
    {
        ECharacterNameResult_AllGood,
        ECharacterNameResult_NotUnique,
        ECharacterNameResult_NameTooLong,
        ECharacterNameResult_Empty,
    }
    public enum EReinforcementGUICategory
    {
        EReinforcementGUICategory_None,
        EReinforcementGUICategory_AssaultRifle,
        EReinforcementGUICategory_SMG,
        EReinforcementGUICategory_SniperRifle,
        EReinforcementGUICategory_Pistol,
        EReinforcementGUICategory_Shotgun,
        EReinforcementGUICategory_Mod,
        EReinforcementGUICategory_Kit,
        EReinforcementGUICategory_MatchConsumableAmmo,
        EReinforcementGUICategory_MatchConsumableWeapon,
        EReinforcementGUICategory_MatchConsumableArmor,
        EReinforcementGUICategory_MatchConsumableGear,
        EReinforcementGUICategory_NewlyAffordableStoreItems,
        EReinforcementGUICategory_KitAppearance,
    }
    public enum EStyleEventMultiplier
    {
        STYLE_EVENT_NORMAL,
        STYLE_EVENT_NONE,
        STYLE_EVENT_VERYWEAK,
        STYLE_EVENT_WEAK,
        STYLE_EVENT_GOOD,
        STYLE_EVENT_VERYGOOD,
    }
    public enum EHelmetState
    {
        HS_Default,
        HS_ForcedOn,
        HS_ForcedOn_Full,
        HS_ForcedOff,
    }
    public enum EHelmetStateController
    {
        HSC_Kismet,
        HSC_Conversation,
        HSC_Cinematic,
        HSC_UserOptions_Default,
    }
    public enum EHelmetPart
    {
        HelmetPart_Helmet,
        HelmetPart_Visor,
        HelmetPart_Breather,
    }
    public enum EPermanentGameEffect_Type
    {
        PermanentGEType_Player,
        PermanentGEType_Weapon,
        PermanentGEType_GAWAsset,
    }
    public enum EClientNoCooldownDecision
    {
        NoCooldown_NoDecision,
        NoCooldown_Success,
        NoCooldown_Failure,
    }
    public enum ECastingPhase
    {
        CP_Start,
        CP_End,
    }
    public enum EBioCapabilityTypes
    {
        BioCaps_Normal,
        BioCaps_Death,
    }
    public enum ESFXSavedMoveType
    {
        SavedMoveType_Walk,
        SavedMoveType_Storm,
        SavedMoveType_InCover,
        SavedMoveType_AimBack,
        SavedMoveType_RootMotion,
        SavedMoveType_Standard,
    }
    public enum ESFXSSPlotVarType
    {
        PlotVar_Unset,
        PlotVar_State,
        PlotVar_Int,
        PlotVar_Float,
    }
    public enum MPMedalType
    {
        MPMedalType_Invalid,
        MPMedalType_Kill,
        MPMedalType_MeleeKill,
        MPMedalType_OverCoverKill,
        MPMedalType_Headshot,
        MPMedalType_AssaultRifle,
        MPMedalType_SniperRifle,
        MPMedalType_Shotgun,
        MPMedalType_Pistol,
        MPMedalType_SMG,
        MPMedalType_HeavyWeapon,
        MPMedalType_Biotics,
        MPMedalType_Tech,
        MPMedalType_Revive,
        MPMedalType_Assist,
        MPMedalType_Survival,
        MPMedalType_ChallengeLevel,
        MPMedalType_Extraction,
        MPMedalType_Killstreak,
        MPMedalType_RandomMap,
        MPMedalType_RandomFaction,
    }
    public enum ScoreTagType
    {
        ScoreTagType_Kill,
        ScoreTagType_Assist,
        ScoreTagType_Objective,
        ScoreTagType_Medal,
    }
    public enum ScoreType
    {
        SCORETYPE_DAMAGE,
        SCORETYPE_POWER,
        SCORETYPE_OBJECTIVE,
        SCORETYPE_MEDAL,
    }
    public enum ESpawnSortType
    {
        SST_Shuffle,
        SST_Linear,
    }
    public enum ETreasureIndex
    {
        TREASURE_ONE,
        TREASURE_TWO,
        TREASURE_THREE,
        TREASURE_FOUR,
        TREASURE_FIVE,
        TREASURE_SIX,
        TREASURE_SEVEN,
        TREASURE_EIGHT,
        TREASURE_NINE,
        TREASURE_TEN,
    }
    public enum DummyFireObjectCyclingMethod
    {
        DFOCM_Sequential,
        DFOCM_Random,
    }
    public enum EMoviePlatform
    {
        MoviePlatform_None,
        MoviePlatform_PC,
        MoviePlatform_PS3,
        MoviePlatform_Xbox360,
    }
    public enum eMode
    {
        MODE_TOPLEVEL,
        MODE_RESEARCH,
        MODE_TECH,
    }
    public enum EResearchMode
    {
        MODE_RESEARCH_TOP,
        MODE_RESEARCH_WEAPON,
        MODE_RESEARCH_ARMOR,
        MODE_RESEARCH_SHIP,
        MODE_RESEARCH_GEAR,
    }
    public enum ESFXAmbPerfEventType
    {
        AmbPerf_UNSET,
        AmbPerf_PerformanceStart,
        AmbPerf_PerformanceEnd,
        AmbPerf_PoseStart,
        AmbPerf_PoseEnd,
        AmbPerf_GestureStart,
        AmbPerf_GestureEnd,
        AmbPerf_PoseEnterTransDone,
    }
    public enum ESFXAmbPerfEventPoseEnum
    {
        SFXAPEPose_Unset,
    }
    public enum ESFXAmbPerfEventGestureEnum
    {
        SFXAPEGesture_Unset,
    }
    public enum ESFXVarHenchTag
    {
        VarHenchTag_Unset,
    }
    public enum ESFXPortraitState
    {
        PORTRAIT_STATE_NORMAL,
        PORTRAIT_STATE_DEAD,
        PORTRAIT_STATE_BUSY,
    }
    public enum ESaveGuiMode
    {
        SaveGuiMode_BrowserWheel,
        SaveGuiMode_MainMenu,
        SaveGuiMode_GameOver,
    }
    public enum ELoadGuiMode
    {
        LoadGuiMode_Default,
        LoadGuiMode_NGPlus,
        LoadGuiMode_LegacyME2,
    }
    public enum ESFXPersonalizationOption
    {
        SFXPersOpt_Casual,
        SFXPersOpt_Type,
        SFXPersOpt_Helmet,
        SFXPersOpt_Torso,
        SFXPersOpt_Shoulder,
        SFXPersOpt_Arm,
        SFXPersOpt_Leg,
        SFXPersOpt_Spec,
        SFXPersOpt_Pattern,
        SFXPersOpt_PatternColor,
        SFXPersOpt_Tint1,
        SFXPersOpt_Tint2,
        SFXPersOpt_Emissive,
    }
    public enum SFXPowerWheelMode
    {
        PWM_NONE,
        PWM_Powers,
        PWM_Weapons,
        PWM_PC,
    }
    public enum SFXPowerWheelPawnID
    {
        PWPID_Player,
        PWPID_Hench1,
        PWPID_Hench2,
    }
    public enum SFXPowerWheelWeaponState
    {
        PWWS_Normal,
        PWWS_Hover,
        PWWS_Disabled,
        PWWS_Selected,
        PWWS_WEAPSTATE_COUNT,
    }
    public enum EBioReaperControlConditionPlotAutoSet
    {
        BioReaperControlConditionPlot_Unset,
    }
    public enum EBioReaperControlConditionAutoSet
    {
        BioReaperControlCondition_Unset,
    }
    public enum ESFXTeam
    {
        TEAM_PLAYER,
        TEAM_ENEMY,
        TEAM_DUMMY,
        TEAM_EVERYONE,
    }
    public enum ESFXTracerState
    {
        eSFXTracerState_Idle,
        eSFXTracerState_ScaleUp,
        eSFXTracerState_ScaleDown,
    }
    public enum EBioPowerResource
    {
        BIO_POWER_RESOURCE_VFX_PLAYER_CRUST,
        BIO_POWER_RESOURCE_VFX_PLAYER_MATERIAL,
        BIO_POWER_RESOURCE_VFX_TARGET_CRUST,
        BIO_POWER_RESOURCE_VFX_TARGET_MATERIAL,
        BIO_POWER_RESOURCE_VFX_FRAMEBUFFER,
        BIO_POWER_RESOURCE_VFX_TRAVELLING,
        BIO_POWER_RESOURCE_VFX_IMPACT,
        BIO_POWER_RESOURCE_VFX_WORLD_IMPACT,
        BIO_POWER_RESOURCE_VFX_RELEASE,
        BIO_POWER_RESOURCE_VFX_CASTING_BEAM,
        BIO_POWER_RESOURCE_CASTING,
        BIO_POWER_RESOURCE_RELEASE,
    }
    public enum EAICompletionReasons
    {
        AI_Cancelled,
        AI_Success,
        AI_Failed,
        AI_LOS,
        AI_Cooldown,
        AI_Disabled,
    }
    public enum CodeRedemptionResult
    {
        REDEMPTION_SUCCESS,
        REDEMPTION_CANCELED,
        REDEMPTION_INVALID,
        REDEMPTION_ERROR,
    }
    public enum EGawParamType
    {
        GawMsgParamType_Bool,
        GawMsgParamType_Int,
        GawMsgParamType_Float,
    }
    public enum SFXOnlineUIState
    {
        SFXONLINE_UISTATE_NONE,
        SFXONLINE_UISTATE_NUCLEUS_CONNECTING,
        SFXONLINE_UISTATE_NUCLEUS_CONNECTED,
        SFXONLINE_UISTATE_CERBERUS_CONNECTING,
        SFXONLINE_UISTATE_CERBERUS_ENTITLED,
        SFXONLINE_UISTATE_CERBERUS_CONNECTED,
    }
    public enum SFXOnlineConnection_MessageType
    {
        SFXONLINE_MT_MESSAGEOFTHEDAY,
        SFXONLINE_MT_DOWNLOAD_PROMPT,
        SFXONLINE_MT_GAW_SUMMARY,
        SFXONLINE_MT_GAW_STATUS_UPDATE,
        SFXONLINE_MT_FRIEND_ACHIVEMENT,
        SFXONLINE_MT_FRIEND_LEADERBOARD_RANK_CHANGE,
        SFXONLINE_MT_MESSAGEOFTHEDAY_TICKERONLY,
        SFXONLINE_MT_DISCONNECTED_TICKERONLY,
        SFXONLINE_MT_MP_PROMO,
    }
    public enum SFXOnlinePurchaseSource
    {
        SFXONLINE_PS_CERBERUS_MAIN,
        SFXONLINE_PS_SHOW_LIVE_CONTENT,
        SFXONLINE_PS_DLC_AVAILABLE,
        SFXONLINE_PS_MARKETPLACE,
    }
    public enum SFXOnlineError
    {
        SFXONLINE_ERR_OK,
        SFXONLINE_AUTH_ERR_DEFAULT,
        SFXONLINE_AUTH_ERR_SYSTEM,
        SFXONLINE_ERR_AUTHORIZATION_REQUIRED,
        SFXONLINE_AUTH_ERR_TOS_REQUIRED,
        SFXONLINE_AUTH_ERR_INVALID_COUNTRY,
        SFXONLINE_AUTH_ERR_INVALID_USER,
        SFXONLINE_AUTH_ERR_INVALID_PASSWORD,
        SFXONLINE_AUTH_ERR_MISSING_PASSWORD,
        SFXONLINE_AUTH_ERR_INVALID_TOKEN,
        SFXONLINE_AUTH_ERR_EXPIRED_TOKEN,
        SFXONLINE_AUTH_ERR_EXISTS,
        SFXONLINE_AUTH_ERR_TOO_YOUNG,
        SFXONLINE_AUTH_ERR_NO_ACCOUNT,
        SFXONLINE_AUTH_ERR_PERSONA_NOT_FOUND,
        SFXONLINE_AUTH_ERR_PERSONA_INACTIVE,
        SFXONLINE_AUTH_ERR_INVALID_PMAIL,
        SFXONLINE_AUTH_ERR_INVALID_FIELD,
        SFXONLINE_AUTH_ERR_INVALID_EMAIL,
        SFXONLINE_AUTH_ERR_MISSING_EMAIL,
        SFXONLINE_AUTH_ERR_INVALID_STATUS,
        SFXONLINE_AUTH_ERR_INVALID_SESSION_KEY,
        SFXONLINE_AUTH_ERR_PERSONA_BANNED,
        SFXONLINE_AUTH_ERR_DEACTIVATED,
        SFXONLINE_AUTH_ERR_PENDING,
        SFXONLINE_AUTH_ERR_BANNED,
        SFXONLINE_AUTH_ERR_DISABLED,
        SFXONLINE_AUTH_ERR_MISSING_PERSONAID,
        SFXONLINE_AUTH_ERR_USER_DOES_NOT_MATCH_PERSONA,
        SFXONLINE_AUTH_ERR_USER_INACTIVE,
        SFXONLINE_AUTH_ERR_NAME_MISMATCH,
        SFXONLINE_AUTH_ERR_INVALID_PS3_TICKET,
        SFXONLINE_AUTH_ERR_INVALID_NAMESPACE,
        SFXONLINE_AUTH_ERR_FIELD_INVALID_CHARS,
        SFXONLINE_AUTH_ERR_FIELD_TOO_SHORT,
        SFXONLINE_AUTH_ERR_FIELD_TOO_LONG,
        SFXONLINE_AUTH_ERR_FIELD_MUST_BEGIN_WITH_LETTER,
        SFXONLINE_AUTH_ERR_FIELD_MISSING,
        SFXONLINE_AUTH_ERR_FIELD_INVALID,
        SFXONLINE_AUTH_ERR_FIELD_NOT_ALLOWED,
        SFXONLINE_AUTH_ERR_FIELD_NEEDS_SPECIAL_CHARS,
        SFXONLINE_AUTH_ERR_FIELD_ALREADY_EXISTS,
        SFXONLINE_AUTH_ERR_FIELD_NEEDS_CONSENT,
        SFXONLINE_AUTH_ERR_FIELD_TOO_YOUNG,
        SFXONLINE_ERR_DUPLICATE_LOGIN,
        SFXONLINE_ERR_DISCONNECTED,
        SFXONLINE_SDK_ERR_SERVER_DISCONNECT,
        SFXONLINE_SDK_ERR_XBL_DISCONNECT,
        SFXONLINE_SDK_ERR_PSN_DISCONNECT,
        SFXONLINE_REDIRECTOR_NO_MATCHING_INSTANCE,
        SFXONLINE_REDIRECTOR_CLIENT_NOT_COMPATIBLE,
        SFXONLINE_REDIRECTOR_CLIENT_UNKNOWN,
        SFXONLINE_REDIRECTOR_SERVER_DOWN,
        SFXONLINE_REDIRECTOR_SERVER_SUNSET,
        SFXONLINE_REDIRECTOR_NO_SERVER_CAPACITY,
        SFXONLINE_SDK_ERR_NO_MULTIPLAYER_PRIVILEGE,
        SFXONLINE_REDIRECTOR_SERVER_NOT_FOUND,
        SFXONLINE_AUTH_ERR_PASSWORD_NOT_MATCH,
        SFXONLINE_AUTH_ERR_INVALID_CODE,
        SFXONLINE_AUTH_ERR_CODE_ALREADY_USED,
        SFXONLINE_SDK_ERR_MINIMUM_AGE_CHECK_FAILED,
    }
    public enum SFXOnlineErrorContext
    {
        SFXONLINE_ERRCONTEXT_NONE,
        SFXONLINE_ERRCONTEXT_Connect,
        SFXONLINE_ERRCONTEXT_Login,
        SFXONLINE_ERRCONTEXT_CreatePersona,
        SFXONLINE_ERRCONTEXT_LoginPersona,
        SFXONLINE_ERRCONTEXT_CreateAccount,
        SFXONLINE_ERRCONTEXT_AssociateAccount,
        SFXONLINE_ERRCONTEXT_SignOut,
    }
    public enum SFXOnlineErrorField
    {
        SFXONLINE_FIELD_UNKNOWN,
        SFXONLINE_FIELD_PASSWORD,
        SFXONLINE_FIELD_VERIFY_PASSWORD,
        SFXONLINE_FIELD_EMAIL,
        SFXONLINE_FIELD_PARENTAL_EMAIL,
        SFXONLINE_FIELD_DISPLAY_NAME,
        SFXONLINE_FIELD_STATUS,
        SFXONLINE_FIELD_DOB,
        SFXONLINE_FIELD_TOKEN,
        SFXONLINE_FIELD_EXPIRATION,
    }
    public enum SFXOnlineErrorFieldCause
    {
        SFXONLINE_FIELD_ERR_UNKNOWN,
        SFXONLINE_FIELD_ERR_INVALID_VALUE,
        SFXONLINE_FIELD_ERR_ILLEGAL_VALUE,
        SFXONLINE_FIELD_ERR_MISSING_VALUE,
        SFXONLINE_FIELD_ERR_DUPLICATE_VALUE,
        SFXONLINE_FIELD_ERR_INVALID_EMAIL_DOMAIN,
        SFXONLINE_FIELD_ERR_SPACES_NOT_ALLOWED,
        SFXONLINE_FIELD_ERR_TOO_SHORT,
        SFXONLINE_FIELD_ERR_TOO_LONG,
        SFXONLINE_FIELD_ERR_TOO_YOUNG,
        SFXONLINE_FIELD_ERR_TOO_OLD,
        SFXONLINE_FIELD_ERR_ILLEGAL_FOR_COUNTRY,
        SFXONLINE_FIELD_ERR_BANNED_COUNTRY,
        SFXONLINE_FIELD_ERR_NOT_ALLOWED,
        SFXONLINE_FIELD_ERR_NOT_MATCH,
    }
    public enum SFXOnlineStatsID
    {
        SFXONLINE_STATS_N7RATING,
    }
    public enum SFXOnlineSettingPropertiesID
    {
        SFXONLINESETTINGS_PROPERTY_MAP_INDEX,
    }
    public enum SFXOnlineGameStatus
    {
        SFXONLINE_IN_MATCH_MAKING,
        SFXONLINE_IN_LOBBY,
        SFXONLINE_IN_LOBBY_LONGTIME,
        SFXONLINE_IN_GAME_STARTING,
        SFXONLINE_IN_GAME_MIDGAME,
        SFXONLINE_IN_GAME_FINISHING,
        SFXONLINE_IN_UNKNOWN_STATE,
    }
    public enum SFXOnlineGameEnemyType
    {
        SFXONLINE_ENEMY_RANDOM,
        SFXONLINE_ENEMY_CERBERUS,
        SFXONLINE_ENEMY_GETH,
        SFXONLINE_ENEMY_REAPER,
        SFXONLINE_ENEMY_ANY,
    }
    public enum SFXOnlineGameDifficulty
    {
        SFXONLINE_DIFFICULTY_BRONZE,
        SFXONLINE_DIFFICULTY_SILVER,
        SFXONLINE_DIFFICULTY_GOLD,
        SFXONLINE_DIFFICULTY_ANY,
    }
    public enum SFXOnlineMessageType
    {
        SFXONLINE_MESSAGE_ACHIEVEMENT,
    }
    public enum LeaderboardStatsError
    {
        LBS_OK,
        LBS_FAILED,
        LBS_LB_RANK_NOT_FOUND,
    }
    public enum JoinFailureReason
    {
        JFR_Unknown,
        JFR_GameFull,
        JFR_InviterLeft,
        JFR_ProtocolMismatch,
        JFR_MissingDLCInviter,
        JFR_MissingDLCInvitee,
    }
    public enum SFXOnlineConnectMode
    {
        SFXONLINE_CM_NONE,
        SFXONLINE_CM_IMPLICIT,
        SFXONLINE_CM_EXPLICIT,
        SFXONLINE_CM_SILENT,
        SFXONLINE_CM_FORCEDAUTOMATIC,
    }
    public enum SFXOnlineNotificationPriority
    {
        SFXONLINE_NOTIFICATION_PRIORITY_CERBERUS_CONTENT,
        SFXONLINE_NOTIFICATION_PRIORITY_NEW_UNLOCK,
        SFXONLINE_NOTIFICATION_PRIORITY_SOON_DLC,
        SFXONLINE_NOTIFICATION_PRIORITY_MOTD,
        SFXONLINE_NOTIFICATION_PRIORITY_UPCOMING_UNLOCK,
        SFXONLINE_NOTIFICATION_PRIORITY_UPCOMING_DLC,
    }
    public enum SFXOnlineNotificationOfferPurchaseStatus
    {
        SFXONLINE_NOTIFICATION_PURHASE_UNKNOWN,
        SFXONLINE_NOTIFICATION_PURHASE_COMPLETED,
        SFXONLINE_NOTIFICATION_PURHASE_NONE,
    }
    public enum SFXOnlineEntitlementLookupInfoType
    {
        SFX_OELIT_NAM_ENTITLEMENT,
        SFX_OELIT_NAM_GROUP,
        SFX_OELIT_SERVER_ENTITLEMENT,
        SFX_OELIT_SERVER_REVOKE,
    }
    public enum EKeyboardType
    {
        KT_Standard,
        KT_Password,
        KT_Email,
        KT_Code,
    }
    public enum ETelemetryChannel
    {
        Channel_Normal,
        Channel_Anonymous,
    }
    public enum ETelemetryAttributeType
    {
        AttributeType_None,
        AttributeType_String,
        AttributeType_Int,
        AttributeType_Float,
        AttributeType_Bool,
        AttributeType_ClassName,
    }
    public enum SFXOnlineEventStatusFinished
    {
        SFXONLINE_EVENT_STATUS_FINISHED_SUCCESS,
        SFXONLINE_EVENT_STATUS_FINISHED_FAILED,
        SFXONLINE_EVENT_STATUS_FINISHED_CANCELED,
        SFXONLINE_EVENT_STATUS_FINISHED_TIMEOUT,
    }
    public enum SFXOnlineEventStatus
    {
        SFXONLINE_EVENT_STATUS_NONE,
        SFXONLINE_EVENT_STATUS_PENDING,
        SFXONLINE_EVENT_STATUS_COMPLETE,
    }
    public enum SFXOnlineEventType
    {
        SFXONLINE_EVENT_NONE,
        SFXONLINE_EVENT_TICK,
        SFXONLINE_EVENT_TIMER,
        SFXONLINE_EVENT_MP_GAME_STATUS_CHANGE,
        SFXONLINE_EVENT_PLATFORM_CONTROLLERCHANGE_0,
        SFXONLINE_EVENT_PLATFORM_CONTROLLERCHANGE_1,
        SFXONLINE_EVENT_PLATFORM_CONTROLLERCHANGE_2,
        SFXONLINE_EVENT_PLATFORM_CONTROLLERCHANGE_3,
        SFXONLINE_EVENT_PLATFORM_CONNECT,
        SFXONLINE_EVENT_PLATFORM_DISCONNECT,
        SFXONLINE_EVENT_PLATFORM_LOGINCHANGE_0,
        SFXONLINE_EVENT_PLATFORM_LOGINCHANGE_1,
        SFXONLINE_EVENT_PLATFORM_LOGINCHANGE_2,
        SFXONLINE_EVENT_PLATFORM_LOGINCHANGE_3,
        SFXONLINE_EVENT_PLATFORM_UI_OPEN,
        SFXONLINE_EVENT_PLATFORM_UI_CLOSE,
        SFXONLINE_EVENT_PLATFORM_LOGINCANCEL,
        SFXONLINE_EVENT_PLATFORM_LOGINSUCCESS,
        SFXONLINE_EVENT_PLATFORM_UI_KEYBOARD,
        SFXONLINE_EVENT_LOGIN_SIGNED_IN,
        SFXONLINE_EVENT_UTIL_GET_CONFIG_SECTION,
        SFXONLINE_EVENT_ACHIEVEMENT_GRANT,
        SFXONLINE_EVENT_QUICKMATCH,
        SFXONLINE_EVENT_INVITE,
        SFXONLINE_EVENT_SEEN_PLAYER,
        SFXONLINE_EVENT_NETWORK_WAIT_START,
        SFXONLINE_EVENT_NETWORK_WAIT_FINISHED,
    }
    public enum SFXOnlineComponentBlazeHubEnvironment
    {
        SFXONLINE_BLAZEHUB_ENV_DISABLED,
        SFXONLINE_BLAZEHUB_ENV_DEV,
        SFXONLINE_BLAZEHUB_ENV_TEST,
        SFXONLINE_BLAZEHUB_ENV_CERT,
        SFXONLINE_BLAZEHUB_ENV_PROD,
        SFXONLINE_BLAZEHUB_ENV_LOCAL,
    }
    public enum SFXOnlineComponentBlazeHubDirtyAllocationContext
    {
        SFXONLINE_BLAZEHUB_DIRTY_ALLOC_DEFAULT,
        SFXONLINE_BLAZEHUB_DIRTY_ALLOC_BUGSENTRY,
    }
    public enum EDownloadDataStep
    {
        DDS_Init,
        DDS_GalaxyAtWarLevel,
        DDS_BinaryLiveIniData,
        DDS_LiveTlkTable,
        DDS_GetLeaderboardList,
        DDS_ReadPlayerStorage,
    }
    public enum OnlineJobCategory
    {
        OJC_Default,
        OJC_Storage,
        OJC_Leaderboards,
        OJC_FriendList,
        OJC_HTTPSystem,
        OJC_GetAuthToken,
        OJC_ImageSystem,
    }
    public enum OnlineJobType
    {
        OJT_None,
        OJT_SaveSettings,
        OJT_LoadSettings,
        OJT_GameReporting,
        OJT_GetLeaderboardData,
        OJT_GetLeaderboardList,
        OJT_GetStatsGroupList,
        OJT_ImportFriendListToBlaze,
        OJT_SendMessage,
        OJT_FetchAllMessages,
        OJT_PurgeMessages,
        OJT_HTTPRequest,
        OJT_GetAuthToken,
        OJT_GalaxyAtWarHTTPRequest,
        OJT_HTTPImageRequest,
        OJT_AllJobs,
    }
    public enum OnlineJobErrorCode
    {
        OJEC_None,
        OJEC_Cancelled,
        OJEC_FailedToStart,
        OJEC_Disconnected,
        OJEC_System,
        OJEC_Timeout,
        OJEC_AuthorizationRequired,
        OJEC_RecordNotFound,
        OJEC_TooManyKeys,
        OJEC_DBError,
    }
    public enum ECHTTPManagerState
    {
        HTTP_MANAGER_STATE_IDLE,
        HTTP_MANAGER_STATE_DOWNLOAD,
    }
    public enum EPCPresenceStates
    {
        PC_PRESENCE_OFFLINE,
        PC_PRESENCE_ONLINE,
        PC_PRESENCE_INGAME,
        PC_PRESENCE_BUSY,
        PC_PRESENCE_IDLE,
        PC_PRESENCE_JOINABLE,
    }
    public enum SFXOnlineXenonPlayerListButtonType
    {
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_TITLECUSTOM,
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_PLAYERREVIEW,
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_GAMEINVITE,
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_MESSAGE,
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_FRIENDREQUEST,
        SFXONLINE_XENON_PLAYERLIST_BUTTON_TYPE_NONE,
    }
    public enum SFXOnlineQuickMatchOutcome
    {
        SFXONLINE_MATCHMAKER_IN_PROGRESS,
        SFXONLINE_MATCHMAKER_CREATE,
        SFXONLINE_MATCHMAKER_JOIN,
        SFXONLINE_MATCHMAKER_SEARCH_TIMEOUT,
        SFXONLINE_MATCHMAKER_FAILED,
    }
    public enum EPersonalMatchSettingsType
    {
        SETTINGS_FOR_SEARCH,
        SETTINGS_FOR_CREATE,
    }
    public enum EHTTPRequest
    {
        HTTP_REQUEST_INVALID,
    }
    public enum SFXOnlineComponentType
    {
        SFXONLINE_COMPONENT_TYPE_COORDINATOR,
        SFXONLINE_COMPONENT_TYPE_ONLINE_API,
        SFXONLINE_COMPONENT_TYPE_ONLINE_UI,
        SFXONLINE_COMPONENT_TYPE_PLATFORM,
        SFXONLINE_COMPONENT_TYPE_LOGIN,
        SFXONLINE_COMPONENT_TYPE_LEADERBOARD,
        SFXONLINE_COMPONENT_TYPE_STATS,
        SFXONLINE_COMPONENT_TYPE_ACHIEVEMENT,
        SFXONLINE_COMPONENT_TYPE_PLAYGROUP,
        SFXONLINE_COMPONENT_TYPE_MATCH_MAKER,
        SFXONLINE_COMPONENT_TYPE_GAME_MANAGER,
        SFXONLINE_COMPONENT_TYPE_VOICE,
        SFXONLINE_COMPONENT_TYPE_ANTICHEAT,
        SFXONLINE_COMPONENT_TYPE_SERVER_LIST,
        SFXONLINE_COMPONENT_TYPE_GAMEFLOW,
        SFXONLINE_COMPONENT_TYPE_ASSOCIATION,
        SFXONLINE_COMPONENT_TYPE_UNREALSYSTEM,
        SFXONLINE_COMPONENT_TYPE_UNREALPLAYER,
        SFXONLINE_COMPONENT_TYPE_UNREALPLAYEREX,
        SFXONLINE_COMPONENT_TYPE_NOTIFICATION,
        SFXONLINE_COMPONENT_TYPE_ORIGIN,
        SFXONLINE_COMPONENT_TYPE_JOBQUEUE,
        SFXONLINE_COMPONENT_TYPE_MATCH_MAKING_BOT,
        SFXONLINE_COMPONENT_TYPE_MESSAGING,
        SFXONLINE_COMPONENT_TYPE_GAME_ENTRY_FLOW,
        SFXONLINE_COMPONENT_TYPE_TELEMETRY,
        SFXONLINE_COMPONENT_TYPE_LIVE_PARTY,
        SFXONLINE_COMPONENT_TYPE_HTTP_MANAGER,
        SFXONLINE_COMPONENT_TYPE_XML_PARSER,
        SFXONLINE_COMPONENT_TYPE_GALAXYATWAR,
        SFXONLINE_COMPONENT_TYPE_COMMERCE,
        SFXONLINE_COMPONENT_TYPE_IMAGE_MANAGER,
        SFXONLINE_COMPONENT_TYPE_AVATAR_AWARD,
    }
    public enum EGaWMsgType
    {
        GaWMsgType_Zero,
        GaWMsgType_PlotEvent,
    }
    public enum ECreatureLogState
    {
        ECREATURELOGSTATE_INITIAL,
        ECREATURELOGSTATE_LOADING,
        ECREATURELOGSTATE_SPAWNING,
        ECREATURELOGSTATE_SPAWNING_2nd,
        ECREATURELOGSTATE_UNLOADING,
        ECREATURELOGSTATE_ENDING,
    }
    public enum SFXUnitTestAsyncLoading_RequestAction
    {
        SFXRequestAction_None,
        SFXRequestAction_SkipPriorityRequests,
        SFXRequestAction_Suspend,
    }
    public enum WwiseEventPrepareState
    {
        WwiseEvent_Unprepared,
        WwiseEvent_Preparing,
        WwiseEvent_PrepareSuccess,
        WwiseEvent_PrepareFailed,
        WwiseEvent_UnPrepareFailed,
    }
    public enum MPChallengeCategory
    {
        MPChallengeCat_NONE,
        MPChallengeCat_General,
        MPChallengeCat_Aliens,
        MPChallengeCat_Weapons,
    }
    public enum MPChallengeRankIcon
    {
        MPChallengeRankIcon_Bronze,
        MPChallengeRankIcon_Bronze_Complete,
        MPChallengeRankIcon_Silver,
        MPChallengeRankIcon_Silver_Complete,
        MPChallengeRankIcon_Gold,
        MPChallengeRankIcon_Gold_Complete,
    }
    public enum MPChallengeRank
    {
        MPChallengeRank_Unknown,
        MPChallengeRank_Gold,
        MPChallengeRank_Silver,
        MPChallengeRank_Bronze,
    }
    public enum ePowerState
    {
        CPS_PowerStable,
        CPS_PowerDraining,
        CPS_PowerCharging,
    }
    public enum MiniGame
    {
        MiniGame_ClawMachine,
        MiniGame_Roullette,
        MiniGame_RobotFighting,
        MiniGame_MissileDefense,
        MiniGame_VarrenRacing,
        MiniGame_Qasar,
        MiniGame_CombatSim1,
        MiniGame_CombatSim2,
        MiniGame_CombatSim3,
        MiniGame_Qasar2,
        MiniGame_Qasar3,
        MiniGame_VarrenRacing2,
        MiniGame_VarrenRacing3,
    }
    public enum CloneClassType
    {
        CloneClass_Soldier,
        CloneClass_Adept,
        CloneClass_Engineer,
        CloneClass_Infiltrator,
        CloneClass_Sentinel,
        CloneClass_Vanguard,
    }
    public enum ConeCheck
    {
        ConeCheck_Front,
        ConeCheck_Back,
        ConeCheck_Right,
        ConeCheck_Left,
    }
}
