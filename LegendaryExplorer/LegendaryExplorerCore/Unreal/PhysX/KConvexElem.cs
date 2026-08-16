using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.PhysX
{
    public class KConvexElem
    {
        public List<Vector3> VertexData = [];
        public List<Plane> PermutedVertexData = [];
        public List<int> FaceTriData = [];
        public List<Vector3> EdgeDirections = [];
        public List<Vector3> FaceNormalDirections = [];
        public List<Plane> FacePlaneData = [];
        public Box ElemBox = new();

        public static KConvexElem CreateFromHull(ReadOnlySpan<Vector3> hullVerts)
        {
            return PhysXSDK.GenerateHullData(hullVerts);
        }

        public StructProperty GetStructProperty()
        {
            var vertexDataProp = new ArrayProperty<StructProperty>(VertexData.Select(vert => CommonStructs.Vector3Prop(vert)), "VertexData");
            var permutedVertexDataProp = new ArrayProperty<StructProperty>(PermutedVertexData.Select(plane => CommonStructs.PlaneProp(plane)), "PermutedVertexData");
            var faceTriDataProp = new ArrayProperty<IntProperty>(FaceTriData.Select(i => new IntProperty(i)), "FaceTriData");
            var edgeDirectionsProp = new ArrayProperty<StructProperty>(EdgeDirections.Select(vert => CommonStructs.Vector3Prop(vert)), "EdgeDirections");
            var faceNormalDirectionsProp = new ArrayProperty<StructProperty>(FaceNormalDirections.Select(vert => CommonStructs.Vector3Prop(vert)), "FaceNormalDirections");
            var facePlaneDataProp = new ArrayProperty<StructProperty>(FacePlaneData.Select(plane => CommonStructs.PlaneProp(plane)), "FacePlaneData");
            var elemBoxProp = ElemBox.GetStructProperty();
            elemBoxProp.Name = "ElemBox";

            return new StructProperty("KConvexElem", false, vertexDataProp, permutedVertexDataProp, faceTriDataProp, edgeDirectionsProp, faceNormalDirectionsProp, facePlaneDataProp, elemBoxProp);
        }
    }
}
