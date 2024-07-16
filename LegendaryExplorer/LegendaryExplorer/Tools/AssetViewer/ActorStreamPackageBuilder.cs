using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorer.Tools.AssetViewer
{
    internal class ActorStreamPackageBuilder
    {
        public static string GetStreamingPackageName(MEGame game, bool isDebug = false)
        {
            if (isDebug)
            {
                return $"{game}AssetViewer_StreamActorDebug.pcc";
            }
            return $"{game}AssetViewer_StreamActor.pcc";
        }
        public static IMEPackage BuildActorPackage(ExportEntry sourceAsset)
        {
            var package = MEPackageHandler.CreateMemoryEmptyLevel(GetStreamingPackageName(sourceAsset.Game), sourceAsset.Game);
            EntryExporter.ExportExportToPackage(sourceAsset, package, out var portedAsset);

            // Set up actor in level
            var actor = SetupActor(package, portedAsset);
            BuildKismet(package, portedAsset, actor);
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    PackageEditorWindow pe = new PackageEditorWindow();
            //    pe.LoadPackage(package);
            //    pe.Show();
            //});
            // package.Save($@"{MEDirectories.GetCookedPath(sourceAnimation.Game)}\{GetStreamingPackageName(sourceAnimation.Game, true)}");

            return package;
        }

        private static ExportEntry SetupActor(IMEPackage package, IEntry sourceAsset)
        {
            if (sourceAsset.IsA("ParticleSystem"))
                return SetupParticleSystem(package, sourceAsset);

            return null;
        }

        // Actor: Emitter
        private static ExportEntry SetupParticleSystem(IMEPackage package, IEntry template)
        {
            var level = package.GetLevel();
            var emitter = ExportCreator.CreateExport(package, "Emitter", "Emitter", level, createWithStack: true);
            PreviewLevelBuilder.AddActorToLevel(emitter);

            var emitterPSC = ExportCreator.CreateExport(package, "ParticleSystemComponent", "ParticleSystemComponent", emitter, prePropBinary: new byte[8]);
            emitterPSC.Archetype = PreviewLevelBuilder.GetImportArchetype(package, "Engine", "Default__Emitter.ParticleSystemComponent0");

            emitter.WriteProperty(new ObjectProperty(emitterPSC, "ParticleSystemComponent"));
            PathEdUtils.SetLocation(emitter, 0, 0, 30);

            emitterPSC.WriteProperty(new ObjectProperty(template, "Template"));
            emitterPSC.WriteProperty(new ObjectProperty(0, "ReplacementPrimitive"));
            PreviewLevelBuilder.SetLightingChannels(emitterPSC, "Static", "Dynamic", "CompositeDynamic");

            return emitter;
        }

        private static void SetupPawn(IMEPackage package, IEntry archetype)
        {
            var level = package.GetLevel();
            var pawnActor = ExportCreator.CreateExport(package, archetype.ClassName, archetype.ClassName, level, createWithStack: true);
            PreviewLevelBuilder.AddActorToLevel(pawnActor);

            // Draw the rest of the owl

            //var emitterPSC = ExportCreator.CreateExport(package, "ParticleSystemComponent", "ParticleSystemComponent", pawnActor, prePropBinary: new byte[8]);
            //emitterPSC.Archetype = PreviewLevelBuilder.GetImportArchetype(package, "Engine", "Default__Emitter.ParticleSystemComponent0");

            //pawnActor.WriteProperty(new ObjectProperty(emitterPSC, "ParticleSystemComponent"));
            //PathEdUtils.SetLocation(pawnActor, 0, 0, 30);

            //emitterPSC.WriteProperty(new ObjectProperty(template, "Template"));
            //emitterPSC.WriteProperty(new ObjectProperty(0, "ReplacementPrimitive"));
            //PreviewLevelBuilder.SetLightingChannels(emitterPSC, "Static", "Dynamic", "CompositeDynamic");
        }

        private static void BuildKismet(IMEPackage package, IEntry newEntry, ExportEntry actor)
        {
            PackageCache cache = new PackageCache();


            ExportEntry actorAsset = newEntry as ExportEntry;
            ;
            if (actorAsset is null && newEntry is ImportEntry imp)
            {
                actorAsset = EntryImporter.ResolveImport(imp, cache);
            }

            var mainSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence");

            // Shared variables
            var actorTarget = SequenceObjectCreator.CreateObject(mainSeq, actor, cache); // Create object for actor.

            // Create loading handshake
            PreviewLevelBuilder.CreateLoadingHandshake(package, "re_StreamActorLoaded");

            // Create control remote events
            // Particle system
            var startEffect = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StartEffect", cache);
            var stopEffect = SequenceObjectCreator.CreateSeqEventRemoteActivated(mainSeq, "re_StopEffect", cache);


            // Events to start/stop effects.
            //KismetHelper.CreateOutputLink(startEffect, "Out", interpAct, 0);
            //KismetHelper.CreateOutputLink(stopEffect, "Out", interpAct, 2);


            // Hook up variables
            //KismetHelper.CreateVariableLink(startEffect, "Instigator", actorTarget);
            //KismetHelper.CreateVariableLink(interpAct, "Data", interpData);
            //KismetHelper.CreateVariableLink(interpAct, "Animation", actorTarget);


            // Hook up animation data.
            //var interpGroupEx = ExportCreator.CreateExport(package, "InterpGroup", "InterpGroup", interpData);
            //interpData.WriteProperty(new ArrayProperty<ObjectProperty>([interpGroupEx], "InterpGroups"));

            //var track = ExportCreator.CreateExport(package, "InterpTrackAnimControl", "InterpTrackAnimControl", interpGroupEx);


            //interpGroupEx.WriteProperty(new ArrayProperty<ObjectProperty>([new ObjectProperty(track)], "InterpTracks"));
            //interpGroupEx.WriteProperty(new ArrayProperty<ObjectProperty>([new ObjectProperty(dynAnimSet)], "GroupAnimSets"));
            //interpGroupEx.WriteProperty(new NameProperty("Animation", "GroupName"));

            //var animStructProps = GlobalUnrealObjectInfo.getDefaultStructValue(package.Game, "AnimControlTrackKey", true, package, cache);
            //animStructProps.AddOrReplaceProp(new NameProperty(sequenceName, "AnimSeqName"));
            //var animStruct = new StructProperty("AnimControlTrackKey", animStructProps);
            //track.WriteProperty(new ArrayProperty<StructProperty>([animStruct], "AnimSeqs"));
        }
    }
}
