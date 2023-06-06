using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Tools.AssetDatabase.Filters;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    /// <summary>
    /// Concurrent dictionaries used during database generation
    /// </summary>
    public class ConcurrentAssetDB
    {
        /// <summary>
        /// Dictionary that stores generated classes
        /// </summary>
        public ConcurrentDictionary<string, ScanTimeClassRecord> GeneratedClasses = new();
        /// <summary>
        /// Dictionary that stores generated Animations
        /// </summary>
        public ConcurrentDictionary<string, AnimationRecord> GeneratedAnims = new();
        /// <summary>
        /// Dictionary that stores generated Materials
        /// </summary>
        public ConcurrentDictionary<string, MaterialRecord> GeneratedMats = new();
        /// <summary>
        /// Dictionary that stores generated Meshes
        /// </summary>
        public ConcurrentDictionary<string, MeshRecord> GeneratedMeshes = new();
        /// <summary>
        /// Dictionary that stores generated Particle Systems
        /// </summary>
        public ConcurrentDictionary<string, ParticleSysRecord> GeneratedPS = new();
        /// <summary>
        /// Dictionary that stores generated Textures
        /// </summary>
        public ConcurrentDictionary<string, TextureRecord> GeneratedText = new();
        /// <summary>
        /// Dictionary that stores generated GFXMovies
        /// </summary>
        public ConcurrentDictionary<string, GUIElement> GeneratedGUI = new();
        /// <summary>
        /// Dictionary that stores generated convos
        /// </summary>
        public ConcurrentDictionary<string, Conversation> GeneratedConvo = new();
        /// <summary>
        /// Dictionary that stores generated lines
        /// </summary>
        public ConcurrentDictionary<string, ConvoLine> GeneratedLines = new();
        /// <summary>
        /// Dictionary that stores generated plot bool records
        /// </summary>
        public ConcurrentDictionary<int, PlotRecord> GeneratedBoolRecords = new();
        /// <summary>
        /// Dictionary that stores generated plot int records
        /// </summary>
        public ConcurrentDictionary<int, PlotRecord> GeneratedIntRecords = new();
        /// <summary>
        /// Dictionary that stores generated plot float records
        /// </summary>
        public ConcurrentDictionary<int, PlotRecord> GeneratedFloatRecords = new();
        /// <summary>
        /// Dictionary that store generated plot conditional records
        /// </summary>
        public ConcurrentDictionary<int, PlotRecord> GeneratedConditionalRecords = new();
        /// <summary>
        /// Dictionary that stores generated plot transition records
        /// </summary>
        public ConcurrentDictionary<int, PlotRecord> GeneratedTransitionRecords = new();
        /// <summary>
        /// Dictionary that stores generated material filter records
        /// </summary>
        public ConcurrentDictionary<string, MaterialBoolSpec> GeneratedMaterialSpecifications = new();
        /// <summary>
        /// Used to do per-class locking during generation
        /// </summary>
        public ConcurrentDictionary<string, object> ClassLocks = new();

        public void Clear()
        {
            GeneratedClasses.Clear();
            GeneratedAnims.Clear();
            GeneratedMats.Clear();
            GeneratedMeshes.Clear();
            GeneratedPS.Clear();
            GeneratedText.Clear();
            GeneratedGUI.Clear();
            GeneratedConvo.Clear();
            GeneratedLines.Clear();
            GeneratedBoolRecords.Clear();
            GeneratedIntRecords.Clear();
            GeneratedFloatRecords.Clear();
            GeneratedConditionalRecords.Clear();
            GeneratedTransitionRecords.Clear();
            GeneratedMaterialSpecifications.Clear();
        }

        public string GetProgressString()
        {
            return $"Classes: {GeneratedClasses.Count}\n" +
                   $"Animations: {GeneratedAnims.Count}\n" +
                   $"Materials: {GeneratedMats.Count}\n" +
                   $"Meshes: {GeneratedMeshes.Count}\n" +
                   $"Particles: {GeneratedPS.Count}\n" +
                   $"Textures: {GeneratedText.Count}\n" +
                   $"GUI Elements: {GeneratedGUI.Count}\n" +
                   $"Lines: {GeneratedLines.Count}\n" +
                   $"Plot Elements: {GeneratedBoolRecords.Count + GeneratedIntRecords.Count + GeneratedFloatRecords.Count + GeneratedConditionalRecords.Count + GeneratedTransitionRecords.Count}";
        }

        public AssetDB CollateDataBase()
        {
            var pdb = new AssetDB();
            pdb.ClassRecords.Capacity = GeneratedClasses.Count;
            foreach (ScanTimeClassRecord cr in GeneratedClasses.Values.OrderBy(x => x.Class))
            {
                pdb.ClassRecords.Add(new ClassRecord(cr.Class, cr.DefinitionFile, cr.DefinitionUIndex, cr.SuperClass, cr.PropertyRecords.Values.ToArray(), cr.Usages.ToArray()));
            }

            pdb.Conversations.AddRange(GeneratedConvo.Values);

            var animsSorted = GeneratedAnims.Values.OrderBy(x => x.AnimSequence).ToList();
            foreach (AnimationRecord anim in animsSorted)
            {
                anim.IsModOnly = anim.Usages.All(u => u.IsInMod);
            }
            pdb.Animations.AddRange(animsSorted);

            var matsSorted = GeneratedMats.Values.OrderBy(x => x.MaterialName).ToList();
            foreach (MaterialRecord mat in matsSorted)
            {
                mat.IsDLCOnly = mat.Usages.All(m => m.IsInDLC);
            }
            pdb.Materials.AddRange(matsSorted);

            var meshesSorted = GeneratedMeshes.Values.OrderBy(x => x.MeshName).ToList();
            foreach (MeshRecord meshRecord in meshesSorted)
            {
                meshRecord.IsModOnly = meshRecord.Usages.All(m => m.IsInMod);
            }
            pdb.Meshes.AddRange(meshesSorted);

            var particleSysSorted = GeneratedPS.Values.OrderBy(x => x.PSName).ToList();
            foreach (ParticleSysRecord particleSysRecord in particleSysSorted)
            {
                particleSysRecord.IsModOnly = particleSysRecord.Usages.All(p => p.IsInMod);
                particleSysRecord.IsDLCOnly = particleSysRecord.Usages.All(p => p.IsInDLC);
            }
            pdb.Particles.AddRange(particleSysSorted);

            var texSorted = GeneratedText.Values.OrderBy(x => x.TextureName).ToList();
            foreach (TextureRecord tex in texSorted)
            {
                tex.IsModOnly = tex.Usages.All(t => t.IsInMod);
                tex.IsDLCOnly = tex.Usages.All(t => t.IsInDLC);
            }
            pdb.Textures.AddRange(texSorted);

            var guisSorted = GeneratedGUI.Values.OrderBy(x => x.GUIName).ToList();
            foreach (GUIElement gui in guisSorted)
            {
                gui.IsModOnly = gui.Usages.All(g => g.IsInMod);
            }
            pdb.GUIElements.AddRange(guisSorted);

            pdb.Lines.AddRange(GeneratedLines.Values.OrderBy(x => x.StrRef).ToList());

            var boolsSorted = GeneratedBoolRecords.Values.OrderBy(x => x.ElementID).ToList();
            pdb.PlotUsages.Bools.AddRange(boolsSorted);

            var intsSorted = GeneratedIntRecords.Values.OrderBy(x => x.ElementID).ToList();
            pdb.PlotUsages.Ints.AddRange(intsSorted);

            var floatsSorted = GeneratedFloatRecords.Values.OrderBy(x => x.ElementID).ToList();
            pdb.PlotUsages.Floats.AddRange(floatsSorted);

            var conditionalsSorted = GeneratedConditionalRecords.Values.OrderBy(x => x.ElementID).ToList();
            pdb.PlotUsages.Conditionals.AddRange(conditionalsSorted);

            var transitionsSorted = GeneratedTransitionRecords.Values.OrderBy(x => x.ElementID).ToList();
            pdb.PlotUsages.Transitions.AddRange(transitionsSorted);

            var matFiltersSorted = GeneratedMaterialSpecifications.Values.OrderBy(x => x.FilterName).ToList();
            pdb.MaterialBoolSpecs.AddRange(matFiltersSorted);

            return pdb;
        }

        public class ScanTimeClassRecord
        {
            public string Class;

            public readonly int DefinitionFile;

            public readonly int DefinitionUIndex;

            public readonly string SuperClass;

            public bool IsModOnly;

            public readonly Dictionary<string, PropertyRecord> PropertyRecords = new();
            public readonly List<ClassUsage> Usages = new();

            public ScanTimeClassRecord(string @class, int definitionFile, int definitionUIndex, string superClass)
            {
                this.Class = @class;
                this.DefinitionFile = definitionFile;
                this.DefinitionUIndex = definitionUIndex;
                this.SuperClass = superClass;
            }

            public ScanTimeClassRecord()
            {
                //mark this as a stub, to be replaced once the class definition is found
                DefinitionFile = -1;
            }
        }
    }
}
