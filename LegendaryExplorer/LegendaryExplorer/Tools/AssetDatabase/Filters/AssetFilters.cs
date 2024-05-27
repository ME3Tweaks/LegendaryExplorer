using System.Linq;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    /// <summary>
    /// Class to manage and expose filters for all record types
    /// </summary>
    public class AssetFilters
    {
        public GenericAssetFilter<ClassRecord> ClassFilter { get; set; }

        public GenericAssetFilter<AnimationRecord> AnimationFilter { get; }
        public GenericAssetFilter<MeshRecord> MeshFilter { get; }
        public GenericAssetFilter<ParticleSysRecord> ParticleFilter { get; }
        public GenericAssetFilter<GUIElement> GUIFilter { get; }
        public GenericAssetFilter<PlotRecord> PlotElementFilter { get; }
        public MaterialFilter MaterialFilter { get; }
        public TextureFilter TextureFilter { get;  }

        public AssetFilters(FileListSpecification fileList)
        {
            ////////////////////////
            // Add new filters and specifications here
            ////////////////////////
            MaterialFilter = new MaterialFilter(fileList);
            TextureFilter = new TextureFilter(fileList);

            ClassFilter = new GenericAssetFilter<ClassRecord>(new IAssetSpecification<ClassRecord>[]
            {
                fileList,
                new PredicateSpecification<ClassRecord>("Only Sequence Classes", cr =>
                {
                    var prefixes = new [] {"seq", "bioseq", "sfxseq", "rvrseq"};
                    return prefixes.Any(pr => cr.Class.ToLower().StartsWith(pr));
                }),
                new PredicateSpecification<ClassRecord>("Only Matinee Classes", cr =>
                {
                    var prefixes = new [] {"interp", "bioevtsys"};
                    var contains = new [] {"interptrack", "sfxscene"};
                    return prefixes.Any(pr => cr.Class.ToLower().StartsWith(pr)) || contains.Any(pr => cr.Class.ToLower().Contains(pr));
                }),
            }, searchPredicate: ClassSearch);

            AnimationFilter = new SingleOptionFilter<AnimationRecord>(new IAssetSpecification<AnimationRecord>[]
            {
                fileList,
                new PredicateSpecification<AnimationRecord>("Only Animations", ar => !ar.IsAmbPerf, "Show animsequences only"),
                new PredicateSpecification<AnimationRecord>("Only Performances (ME3)", ar => ar.IsAmbPerf),
            }, searchPredicate: t => t.Record.AnimSequence.ToLower().Contains(t.SearchText.ToLower()));

            MeshFilter = new SingleOptionFilter<MeshRecord>(new IAssetSpecification<MeshRecord>[]
            {
                fileList,
                new PredicateSpecification<MeshRecord>("Only Skeletal Meshes", mr => mr.IsSkeleton),
                new PredicateSpecification<MeshRecord>("Only Static Meshes", mr => !mr.IsSkeleton),
            }, searchPredicate: MeshSearch);

            ParticleFilter = new SingleOptionFilter<ParticleSysRecord>(new IAssetSpecification<ParticleSysRecord>[]
            {
                fileList,
                new PredicateSpecification<ParticleSysRecord>("Only Particle Systems",
                    pr => pr.VFXType == ParticleSysRecord.VFXClass.ParticleSystem),
                new PredicateSpecification<ParticleSysRecord>("Only Client Effects",
                    pr => pr.VFXType != ParticleSysRecord.VFXClass.ParticleSystem)
            }, searchPredicate: t => t.Record.PSName.ToLower().Contains(t.SearchText.ToLower()));

            GUIFilter = new GenericAssetFilter<GUIElement>(new IAssetSpecification<GUIElement>[] {fileList},
                searchPredicate: t => t.Record.GUIName.ToLower().Contains(t.SearchText.ToLower()));

            PlotElementFilter = new GenericAssetFilter<PlotRecord>(new IAssetSpecification<PlotRecord>[] {fileList},
                searchPredicate: t => t.Record.DisplayText.ToLower().Contains(t.SearchText.ToLower()));
        }

        /// <summary>
        /// Applies the search box text to all record filters
        /// </summary>
        /// <param name="filterBoxText"></param>
        public void SetSearch(string filterBoxText)
        {
            // TODO: Is there a way to make this nicer?
            ClassFilter.Search.SearchText = filterBoxText;
            AnimationFilter.Search.SearchText = filterBoxText;
            MeshFilter.Search.SearchText = filterBoxText;
            ParticleFilter.Search.SearchText = filterBoxText;
            MaterialFilter.Search.SearchText = filterBoxText;
            GUIFilter.Search.SearchText = filterBoxText;
            PlotElementFilter.Search.SearchText = filterBoxText;
            TextureFilter.Search.SearchText = filterBoxText;
        }

        /// <summary>
        /// Attempts to toggle on/off a filter of any type
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Whether a filter was toggled</returns>
        public bool ToggleFilter(object obj)
        {
            switch(obj)
            {
                case IAssetSpecification<MaterialRecord> mr:
                    MaterialFilter.SetSelected(mr);
                    break;
                case IAssetSpecification<TextureRecord> tr:
                    TextureFilter.SetSelected(tr);
                    break;
                case IAssetSpecification<ClassRecord> cr:
                    ClassFilter.SetSelected(cr);
                    break;
                case IAssetSpecification<AnimationRecord> ar:
                    AnimationFilter.SetSelected(ar);
                    break;
                case IAssetSpecification<MeshRecord> mr:
                    MeshFilter.SetSelected(mr);
                    break;
                case IAssetSpecification<ParticleSysRecord> pr:
                    ParticleFilter.SetSelected(pr);
                    break;
                default: return false;
            }
            return true;
        }

        public static bool MeshSearch((string, MeshRecord) tuple)
        {
            var (text, mr) = tuple;
            if (mr.IsSkeleton && text.ToLower().StartsWith("bones:") && text.Length > 6 && int.TryParse(text.Remove(0, 6).ToLower(), out int bonecount))
            {
                return mr.BoneCount == bonecount;
            }
            else
            {
                return mr.MeshName.ToLower().Contains(text.ToLower());
            }
        }

        public static bool ClassSearch((string, ClassRecord) tuple)
        {
            var (text, cr) = tuple;
            text = text.ToLower();
            bool matches = false;
            
            var textSplit = text.Split(' ');
            foreach (var t in textSplit)
            {
                if (t.StartsWith("prop:"))
                {
                    var propName = t.Substring(5);
                    matches |= cr.PropertyRecords.Any(propRecord => propRecord.Property.ToLower().Contains(propName));
                }
            }
            matches |= cr.Class.ToLower().Contains(text);
            return matches;
        }
    }
}