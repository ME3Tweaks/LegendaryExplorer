using System.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for ParticleSystemExportLoader.xaml
    /// </summary>
    public partial class ParticleSystemExportLoader : ExportLoaderControl
    {
        public ObservableCollectionExtended<ParticleSystemNode> ParticleNodes { get; } = new ObservableCollectionExtended<ParticleSystemNode>();

        public class ParticleSystemNode
        {
            public IEntry Entry { get; set; }
            public string Header { get; set; }
            public ObservableCollectionExtended<ParticleSystemNode> Children { get; } = new ObservableCollectionExtended<ParticleSystemNode>();
        }

        public ParticleSystemExportLoader() : base("Particle System Viewer")
        {
            DataContext = this;
            InitializeComponent();
        }

        //Random distributions "Op"
        //
        // 0 = NONE
        // 1 = Random
        // 2 = Extremes (ends)
        // 3 = Random (range) take random within range

        public override bool CanParse(ExportEntry exportEntry) =>
            !exportEntry.IsDefaultObject && exportEntry.ClassName == "ParticleSystem";

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            var props = exportEntry.GetProperties();
            List<ParticleSystemNode> rootNodes = new List<ParticleSystemNode>();

            var emitters = props.GetProp<ArrayProperty<ObjectProperty>>("Emitters");
            foreach (var emitter in emitters)
            {
                if (emitter.Value != 0)
                {
                    if (CurrentLoadedExport.FileRef.IsUExport(emitter.Value))
                    {
                        var emitterExport = CurrentLoadedExport.FileRef.GetUExport(emitter.Value);
                        var emitterName = emitterExport.GetProperty<NameProperty>("EmitterName");
                        string header = emitterName?.Value.Name ?? "Emitter";
                        ParticleSystemNode p = new() { 
                            Entry = emitterExport, 
                            Header = $"{emitterExport.UIndex} {header}" 
                        };
                        rootNodes.Add(p);
                        var emitterLODs = emitterExport.GetProperty<ArrayProperty<ObjectProperty>>("LODLevels");
                        int lodNumber = 0;
                        if (emitterLODs != null)
                        {
                            foreach (var lod in emitterLODs)
                            {
                                var lodExport = CurrentLoadedExport.FileRef.GetUExport(lod.Value);
                                ParticleSystemNode psLod = new() {
                                    Entry = lodExport,
                                    Header = $"LOD {lodNumber}: {lodExport.UIndex} {lodExport.InstancedFullPath}"
                                };
                                p.Children.Add(psLod);

                                {
                                    var requiredModule = (ExportEntry) lodExport.GetProperty<ObjectProperty>("RequiredModule")?.ResolveToEntry(CurrentLoadedExport.FileRef);
                                    if (requiredModule != null)
                                    {
                                        ParticleSystemNode reqModule = new(){
                                            Entry = requiredModule,
                                            Header = $"Required Module: {requiredModule.UIndex} {requiredModule.InstancedFullPath}"
                                        };
                                        psLod.Children.Add(reqModule);

                                        var materialEntry = requiredModule.GetProperty<ObjectProperty>("Material")?.ResolveToEntry(CurrentLoadedExport.FileRef);
                                        if (materialEntry != null)
                                        {
                                            ParticleSystemNode matNode = new() {
                                                Entry = materialEntry,
                                                Header = $"Material: {materialEntry.UIndex} {materialEntry.InstancedFullPath}"
                                            };
                                            reqModule.Children.Add(matNode);
                                        }
                                    }
                                }

                                var typeDataExport = (ExportEntry) lodExport.GetProperty<ObjectProperty>("TypeDataModule")?.ResolveToEntry(CurrentLoadedExport.FileRef);
                                if (typeDataExport != null)
                                {
                                    ParticleSystemNode typeModuleNode = new() { 
                                        Entry = typeDataExport, 
                                        Header = $"Type Data Module: {typeDataExport.UIndex} {typeDataExport.InstancedFullPath}"
                                    };
                                    psLod.Children.Add(typeModuleNode);

                                    var meshes = typeDataExport.GetProperty<ArrayProperty<ObjectProperty>>("m_Meshes");
                                    if (meshes != null)
                                    {
                                        int meshIndex = 0;
                                        foreach (var mesh in meshes)
                                        {
                                            var meshExp = mesh.ResolveToEntry(CurrentLoadedExport.FileRef);
                                            if (meshExp != null)
                                            {
                                                ParticleSystemNode meshNode = new() { 
                                                    Entry = meshExp,
                                                    Header = $"Mesh {meshIndex}: {meshExp.UIndex} {meshExp.InstancedFullPath}"
                                                };
                                                typeModuleNode.Children.Add(meshNode);
                                            }

                                            meshIndex++;
                                        }
                                    }
                                }

                                var modules = lodExport.GetProperty<ArrayProperty<ObjectProperty>>("Modules");
                                if (modules != null)
                                {
                                    int modIndex = 0;
                                    foreach (var module in modules)
                                    {
                                        var moduleExp = module.ResolveToEntry(CurrentLoadedExport.FileRef);
                                        if (moduleExp != null)
                                        {
                                            ParticleSystemNode moduleNode = new() { 
                                                Entry = moduleExp, 
                                                Header = $"Module {modIndex}: {moduleExp.UIndex} {moduleExp.InstancedFullPath}"
                                            };

                                            psLod.Children.Add(moduleNode);
                                            GenerateNode(moduleNode);
                                        }

                                        modIndex++;
                                    }
                                }

                                lodNumber++;
                            }
                        }
                    }
                }
            }

            ParticleNodes.ReplaceAll(rootNodes);
        }

        private void GenerateNode(ParticleSystemNode moduleNode)
        {
            var export = (ExportEntry)moduleNode.Entry;
            if (export.ClassName == "ParticleModuleSize")
            {
                GenerateParticleModuleSize(export, moduleNode);
            }
            //throw new System.NotImplementedException();
        }

        private void GenerateParticleModuleSize(ExportEntry export, ParticleSystemNode moduleNode)
        {
        }

        public override void UnloadExport()
        {
            ParticleNodes.ClearEx();
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new ParticleSystemExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Particle System - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
        }
    }
}
