using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using CommunityToolkit.HighPerformance;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BrushComponent : ObjectBinary
    {
        public KCachedConvexData CachedPhysBrushData;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref CachedPhysBrushData);
        }

        public static BrushComponent Create()
        {
            return new()
            {
                CachedPhysBrushData = new KCachedConvexData
                {
                    CachedConvexElements = []
                }
            };
        }

        /// <summary>
        /// Cooks BrushAggGeom for rigid-body collision at its total scale, as UBrushComponent::BuildPhysBrushData does.
        /// Verified to produce identical data as BW's cooker.
        /// </summary>
        /// <param name="packageCache">For values inherited from archetypes in other packages</param>
        public void RegenCachedPhysBrushData(PackageCache packageCache = null)
        {
            if (Export is null)
            {
                throw new Exception($"No export associated with this {nameof(BrushComponent)}");
            }
            if (!Export.Game.IsLEGame())
            {
                throw new Exception("Can only regenerate CachedPhysBrushData for LE games");
            }
            //(its hulls and scale may be its archetype's)
            var effective = Export.GetCondensedProperties(packageCache, resolveImports: true, mergeStructs: true);
            var convexElemsProp = effective.GetProp<StructProperty>("BrushAggGeom")?.GetProp<ArrayProperty<StructProperty>>("ConvexElems");
            if (convexElemsProp is null || convexElemsProp.Count is 0)
            {
                // Nothing to cook: with no cached data, the game builds the hulls and cooks them from the brush at load (UBrushComponent::InitComponentRBPhys),
                // as it does for the few shipping blocking volumes that have their own brush and no hulls. Data from before can't be left to mismatch it
                CachedPhysBrushData.CachedConvexElements = [];
                return;
            }
            var scale3DProp = effective.GetProp<StructProperty>("Scale3D");
            Vector3 scale = (effective.GetProp<FloatProperty>("Scale") ?? 1f) * (scale3DProp is null ? Vector3.One : CommonStructs.GetVector3(scale3DProp));
            if (Export.Parent is ExportEntry parent && parent.IsA("Actor"))
            {
                scale *= ActorUtils.GetActorTotalScale(parent.GetCondensedProperties(packageCache, resolveImports: true, mergeStructs: true));
            }

            // As MakeCachedConvexDataForAggGeom does, a hull with no vertices is skipped, and one PhysX can't cook (as a flat one) is left out
            var cooked = new List<KCachedConvexDataElement>(convexElemsProp.Count);
            using (var cooker = new PhysXCooker())
            {
                var verts = new List<Vector3>();
                foreach (var hull in convexElemsProp)
                {
                    if (hull.GetProp<ArrayProperty<StructProperty>>("VertexData") is not { Count: > 0 } vertexDataProp) continue;
                    verts.Clear();
                    foreach (StructProperty vertProp in vertexDataProp)
                    {
                        verts.Add(CommonStructs.GetVector3(vertProp) * scale * PhysXSDK.UnrealtoPhysXScale);
                    }
                    if (cooker.TryGenerateCachedPhysicsData(verts.AsSpan()) is { } data) cooked.Add(new KCachedConvexDataElement { ConvexElementData = data });
                }
            }
            CachedPhysBrushData.CachedConvexElements = [.. cooked];

            // What every LE1-LE3 blocking volume with cached data has. Volumes that never build rigid-body collision have none or older versions
            var props = Export.GetProperties();
            props.AddOrReplaceProp(new IntProperty(34079762, "CachedPhysBrushDataVersion"));
            Export.WriteProperties(props);
        }
    }
}
