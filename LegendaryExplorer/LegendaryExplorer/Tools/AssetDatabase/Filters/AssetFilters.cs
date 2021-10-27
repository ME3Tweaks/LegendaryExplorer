using System.Linq;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public class AssetFilters
    {
        public GenericAssetFilter<ClassRecord> ClassFilter { get; set; }

        public GenericAssetFilter<AnimationRecord> AnimationFilter { get; set; }
        public GenericAssetFilter<MeshRecord> MeshFilter { get; set; }
        public GenericAssetFilter<TextureRecord> TextureFilter { get; set; }
        public GenericAssetFilter<ParticleSysRecord> ParticleFilter { get; set; }
        public MaterialFilter MatFilter { get; set; } = new();

        public AssetFilters()
        {
            ////////////////////////
            // Add new filters here
            ////////////////////////
            ClassFilter = new GenericAssetFilter<ClassRecord>(new[]
            {
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
                })
            });

            AnimationFilter = new SingleOptionFilter<AnimationRecord>(new[]
            {
                new PredicateSpecification<AnimationRecord>("Only Animations", ar => !ar.IsAmbPerf, "Show animsequences only"),
                new PredicateSpecification<AnimationRecord>("Only Performances (ME3)", ar => ar.IsAmbPerf)
            });

            MeshFilter = new SingleOptionFilter<MeshRecord>(new[]
            {
                new PredicateSpecification<MeshRecord>("Only Skeletal Meshes", mr => mr.IsSkeleton),
                new PredicateSpecification<MeshRecord>("Only Static Meshes", mr => !mr.IsSkeleton),
            });

            ParticleFilter = new SingleOptionFilter<ParticleSysRecord>(new[]
            {
                new PredicateSpecification<ParticleSysRecord>("Only Particle Systems",
                    pr => pr.VFXType == ParticleSysRecord.VFXClass.ParticleSystem),
                new PredicateSpecification<ParticleSysRecord>("Only Client Effects",
                    pr => pr.VFXType != ParticleSysRecord.VFXClass.ParticleSystem)
            });

        }

        public void SetFilters(object obj)
        {
            switch(obj)
            {
                case IAssetSpecification<MaterialRecord> mr:
                    MatFilter.SetSelected(mr);
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
            }
        }
    }
}