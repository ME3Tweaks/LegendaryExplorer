using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System.Collections.Concurrent;

namespace LegendaryExplorer.Tools.LevelEditor;

public enum ActorRepresentation
{
    SceneRendered,
    PartialScene,
    EditorMarker,
    DataOnly,
    Unsupported
}

public sealed record ActorCoverage(ActorRepresentation Representation, string Reason)
{
    public string Label => Representation switch
    {
        ActorRepresentation.SceneRendered => "Scene rendered",
        ActorRepresentation.PartialScene => "Partial scene rendering",
        ActorRepresentation.EditorMarker => "Editor marker only",
        ActorRepresentation.DataOnly => "Data only",
        _ => "Unsupported / failed"
    };
}

internal enum ActorKind
{
    StaticMesh, SFXSkeletalMesh, SkeletalMesh, KAsset, DynamicMesh, Brush, Stunt,
    Placeable, BioPawn, Pawn, Prefab, Grenade, Ammo, Pickup, Light, Collection, Helper
}

internal sealed record ActorClassInfo(ActorKind Kind, string Category = null, string Reason = null, bool DataOnly = false);

/// <summary>One ordered classification for loading, reloading and preview eligibility. Instance failures remain separate.</summary>
internal static class ActorClassification
{
    private static readonly ConcurrentDictionary<(string, MEGame), ActorClassInfo> Classes = new();

    public static ActorClassInfo Get(ExportEntry export) => Get(export.ClassName, export.Game);
    public static ActorClassInfo Get(string name, MEGame game) => Classes.GetOrAdd((name, game), key => Classify(key.Item1, key.Item2));

    private static ActorClassInfo Classify(string name, MEGame game)
    {
        bool Is(string parent) => name == parent || GlobalUnrealObjectInfo.IsA(name, parent, game);
        if (name is "StaticMeshCollectionActor" or "StaticLightCollectionActor") return new(ActorKind.Collection);
        if (Is("StaticMeshActor")) return new(ActorKind.StaticMesh);
        if (Is("SFXSkeletalMeshActor") || Is("SFXSkeletalMeshActorMAT")) return new(ActorKind.SFXSkeletalMesh);
        if (Is("SkeletalMeshActor")) return new(ActorKind.SkeletalMesh);
        if (Is("KAsset")) return new(ActorKind.KAsset);
        if (Is("DynamicSMActor")) return new(ActorKind.DynamicMesh);
        if (Is("Brush")) return new(ActorKind.Brush);
        if (Is("SFXStuntActor")) return new(ActorKind.Stunt);
        if (Is("BioArtPlaceable")) return new(ActorKind.Placeable);
        if (Is("BioPawn")) return new(ActorKind.BioPawn);
        if (Is("Pawn")) return new(ActorKind.Pawn);
        if (Is("PrefabInstance")) return new(ActorKind.Prefab);
        if (Is("SFXDroppedGrenade")) return new(ActorKind.Grenade);
        if (Is("SFXDroppedAmmo")) return new(ActorKind.Ammo);
        if (Is("SFXDroppedPickup")) return new(ActorKind.Pickup);
        if (Is("Light")) return new(ActorKind.Light);

        if (Is("WorldInfo") || Is("MaterialInstanceActor") || Is("BioSquad")
            || name is "BioPlayerSquad" or "BioWaypointSet" or "Route" or "BioSquadAmbient" or "BioSquadCombat"
                or "BioSquadCommoner" or "BioSquadSitAndShoot" or "BioShop")
            return new(ActorKind.Helper, "Data", "Level settings, references or runtime bookkeeping; no scene geometry.", true);
        // Slots are represented through their owner, never by a second independently movable marker.
        if (Is("CoverSlotMarker"))
            return new(ActorKind.Helper, "Navigation", "Cover-slot bookkeeping. Inspect OwningSlot in Package Editor; the owning cover link is the spatial helper.", true);
        if (Is("PlayerStart") || name is "BioStartLocation" or "BioStartPoint")
            return new(ActorKind.Helper, "Gameplay", "Editable player/start transform; runtime spawning is not simulated.");
        if (Is("NavigationPoint") || Is("BioPathPoint") || Is("SplineActor") || name == "PathTargetPoint")
            return new(ActorKind.Helper, "Navigation", Is("SplineLoftActor")
                ? "Editable actor transform only; spline loft deformation and connected spline data are not rebuilt."
                : "Editable actor transform only; navigation topology and cover-slot shapes are not rebuilt.");
        if (Is("CameraActor") || Is("SceneCaptureActor"))
            return new(ActorKind.Helper, "Cameras", "Camera position and orientation only; camera preview and scene captures are not implemented.");
        if (Is("AmbientSound") || Is("WwiseAmbientSound") || name is "WwiseDistanceRTPC" or "WwiseMicPosOrient")
            return new(ActorKind.Helper, "Audio", "Sound-source position only; attenuation preview and playback are not implemented.");
        if (Is("Emitter") || Is("DecalActorBase") || Is("LensFlareSource") || Is("HeightFog")
            || Is("FogVolumeDensityInfo") || Is("Terrain") || Is("WindDirectionalSource") || Is("WindPointSource")
            || name is "BioSunActor" or "RadialBlurActor" or "SFXAtlasSmokeActor")
            return new(ActorKind.Helper, "Effects helpers", "Editable actor transform only; scene effects are not rendered and cooked projection/geometry data is not rebuilt.");
        if (Is("Trigger") || Is("TargetPoint") || Is("Note") || Is("RB_ConstraintActor")
            || name is "BioLookAtTarget" or "BioMapNote" or "BioStartLocation" or "BioStartPoint" or "BioTrigger"
                or "BioSquadHoldMarker" or "BioStage" or "SFXSignText" or "SFXPointOfInterest"
                or "SFXEnemySpawnPoint" or "SFXOperation_ObjectiveSpawnPoint" or "SFXArmorNode"
                or "SFXMiningNode" or "SFXTreasureNode" or "SFXWeaponFactory" or "SFXWeaponModFactory"
                or "SFXWeaponModNode" or "SFXWeaponNode" or "SFXWeaponNode_BonusWeapon"
                or "RB_LineImpulseActor" or "RB_RadialForceActor" or "RB_RadialImpulseActor" or "RB_Thruster")
            return new(ActorKind.Helper, "Gameplay", "Editable actor transform; runtime behavior is not simulated.");
        return new(ActorKind.Helper, "Unsupported", "No renderer is registered for this actor type.");
    }
}
