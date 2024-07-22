using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BrushComponent : ObjectBinary
    {
        public KCachedConvexData CachedPhysBrushData;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref CachedPhysBrushData);
        }

        public static BrushComponent Create()
        {
            return new()
            {
                CachedPhysBrushData = new KCachedConvexData
                {
                    CachedConvexElements = Array.Empty<KCachedConvexDataElement>()
                }
            };
        }

        //appears to work, but the resulting data is untested
        public void RegenCachedPhysBrushData()
        {
            if (Export is null)
            {
                throw new Exception($"No export associated with this {nameof(BrushComponent)}");
            }
            if (!Export.Game.IsLEGame())
            {
                throw new Exception("Can only regenerate CachedPhysBrushData for LE games");
            }
            var props = Export.GetProperties();
            var convexElemsProp = props.GetProp<StructProperty>("BrushAggGeom")?.GetProp<ArrayProperty<StructProperty>>("ConvexElems");
            if (convexElemsProp is null || convexElemsProp.Count is 0)
            {
                //should this be an error?
                return;
            }
            var scale3DProp = props.GetProp<StructProperty>("Scale3D");
            Vector3 scale = (props.GetProp<FloatProperty>("Scale") ?? 1f) * (scale3DProp is null ? Vector3.One : CommonStructs.GetVector3(scale3DProp));
            if (Export.Parent is ExportEntry parent && parent.IsA("Actor"))
            {
                scale *= ActorUtils.GetActorTotalScale(parent.GetProperties());
            }

            using (var cooker = new PhysXCooker())
            {
                var verts = new List<Vector3>();
                CachedPhysBrushData.CachedConvexElements = new KCachedConvexDataElement[convexElemsProp.Count];
                for (int i = 0; i < convexElemsProp.Count; i++)
                {
                    var vertexDataProp = convexElemsProp[i].GetProp<ArrayProperty<StructProperty>>("VertexData") ?? throw new Exception("Malformed BrushAggGeom");
                    verts.EnsureCapacity(vertexDataProp.Count);
                    verts.Clear();
                    foreach (StructProperty vertProp in vertexDataProp)
                    {
                        verts.Add(CommonStructs.GetVector3(vertProp) * scale * 0.02f);
                    }
                    CachedPhysBrushData.CachedConvexElements[i] = new KCachedConvexDataElement
                    {
                        ConvexElementData = cooker.GenerateCachedPhysicsData(verts.AsSpan())
                    };
                }
            }

            props.AddOrReplaceProp(new IntProperty(Export.Game is MEGame.LE1 ? 33882375 : 34079762, "CachedPhysBrushDataVersion"));
            Export.WriteProperties(props);
        }
    }
}
