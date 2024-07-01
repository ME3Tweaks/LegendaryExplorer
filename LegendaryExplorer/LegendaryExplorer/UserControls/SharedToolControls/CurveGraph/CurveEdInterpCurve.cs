using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveVector2D = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector2>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;

using InterpCurvePointVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<System.Numerics.Vector3>;
using InterpCurvePointVector2D = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<System.Numerics.Vector2>;
using InterpCurvePointFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<float>;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    public enum CurveType : byte
    {
        InterpCurveQuat,
        InterpCurveFloat,
        InterpCurveVector,
        InterpCurveVector2D,
        InterpCurveTwoVectors,
        InterpCurveLinearColor,
    }

    public class CurveEdInterpCurve : NotifyPropertyChangedBase
    {
        private readonly MEGame game;
        private readonly CurveType curveType;
        private readonly EInterpMethodType interpMethod = EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents;

        public string Name { get; set; }
        public ObservableCollectionExtended<Curve> Curves { get; set; }
        public CurveEdInterpCurve(IMEPackage pcc, StructProperty prop)
        {
            game = pcc.Game;

            Curves = new ObservableCollectionExtended<Curve>();
            Name = prop.Name;
            curveType = Enums.Parse<CurveType>(prop.StructType);

            float inVal = 0f;
            var interpMode = EInterpCurveMode.CIM_Linear;
            var points = prop.Properties.GetProp<ArrayProperty<StructProperty>>("Points");
            LinkedList<CurvePoint> x;
            LinkedList<CurvePoint> y;
            switch (curveType)
            {
                case CurveType.InterpCurveFloat:
                    float outVal = 0f;
                    float arriveTangent = 0f;
                    float leaveTangent = 0f;
                    var vals = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var p in point.Properties)
                        {
                            switch (p)
                            {
                                case FloatProperty floatProp when floatProp.Name == "InVal":
                                    inVal = floatProp.Value;
                                    break;
                                case FloatProperty floatProp when floatProp.Name == "OutVal":
                                    outVal = floatProp.Value;
                                    break;
                                case FloatProperty floatProp when floatProp.Name == "ArriveTangent":
                                    arriveTangent = floatProp.Value;
                                    break;
                                case FloatProperty floatProp when floatProp.Name == "LeaveTangent":
                                    leaveTangent = floatProp.Value;
                                    break;
                                case EnumProperty enumProp when enumProp.Name == "InterpMode" && Enum.TryParse(enumProp.Value, out EInterpCurveMode enumVal):
                                    interpMode = enumVal;
                                    break;
                            }
                        }
                        vals.AddLast(new CurvePoint(inVal, outVal, arriveTangent, leaveTangent, interpMode));
                    }
                    Curves.Add(new Curve("X", vals));
                    break;
                case CurveType.InterpCurveVector:
                    var outValVec = new Vector3(0, 0, 0);
                    var arriveTangentVec = new Vector3(0, 0, 0);
                    var leaveTangentVec = new Vector3(0, 0, 0);
                    x = new LinkedList<CurvePoint>();
                    y = new LinkedList<CurvePoint>();
                    var z = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var p in point.Properties)
                        {
                            switch (p)
                            {
                                case FloatProperty floatProp when floatProp.Name == "InVal":
                                    inVal = floatProp.Value;
                                    break;
                                case StructProperty structProp when structProp.Name == "OutVal":
                                    outValVec = CommonStructs.GetVector3(structProp);
                                    break;
                                case StructProperty structProp when structProp.Name == "ArriveTangent":
                                    arriveTangentVec = CommonStructs.GetVector3(structProp);
                                    break;
                                case StructProperty structProp when structProp.Name == "LeaveTangent":
                                    leaveTangentVec = CommonStructs.GetVector3(structProp);
                                    break;
                                case EnumProperty enumProp when enumProp.Name == "InterpMode" && Enum.TryParse(enumProp.Value, out EInterpCurveMode enumVal):
                                    interpMode = enumVal;
                                    break;
                            }
                        }
                        x.AddLast(new CurvePoint(inVal, outValVec.X, arriveTangentVec.X, leaveTangentVec.X, interpMode));
                        y.AddLast(new CurvePoint(inVal, outValVec.Y, arriveTangentVec.Y, leaveTangentVec.Y, interpMode));
                        z.AddLast(new CurvePoint(inVal, outValVec.Z, arriveTangentVec.Z, leaveTangentVec.Z, interpMode));
                    }
                    if (Name == "EulerTrack")
                    {
                        Curves.Add(new Curve("Roll", x));
                        Curves.Add(new Curve("Pitch", y));
                        Curves.Add(new Curve("Yaw", z));
                    }
                    else
                    {
                        Curves.Add(new Curve("X", x));
                        Curves.Add(new Curve("Y", y));
                        Curves.Add(new Curve("Z", z));
                    }
                    break;
                case CurveType.InterpCurveVector2D:
                    var outValVec2 = new Vector2(0, 0);
                    var arriveTangentVec2 = new Vector2(0, 0);
                    var leaveTangentVec2 = new Vector2(0, 0);
                    x = new LinkedList<CurvePoint>();
                    y = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var p in point.Properties)
                        {
                            switch (p)
                            {
                                case FloatProperty floatProp when floatProp.Name == "InVal":
                                    inVal = floatProp.Value;
                                    break;
                                case StructProperty structProp when structProp.Name == "OutVal":
                                    outValVec2 = CommonStructs.GetVector2(structProp);
                                    break;
                                case StructProperty structProp when structProp.Name == "ArriveTangent":
                                    arriveTangentVec2 = CommonStructs.GetVector2(structProp);
                                    break;
                                case StructProperty structProp when structProp.Name == "LeaveTangent":
                                    leaveTangentVec2 = CommonStructs.GetVector2(structProp);
                                    break;
                                case EnumProperty enumProp when enumProp.Name == "InterpMode" && Enum.TryParse(enumProp.Value, out EInterpCurveMode enumVal):
                                    interpMode = enumVal;
                                    break;
                            }
                        }
                        x.AddLast(new CurvePoint(inVal, outValVec2.X, arriveTangentVec2.X, leaveTangentVec2.X, interpMode));
                        y.AddLast(new CurvePoint(inVal, outValVec2.Y, arriveTangentVec2.Y, leaveTangentVec2.Y, interpMode));
                    }
                    Curves.Add(new Curve("X", x));
                    Curves.Add(new Curve("Y", y));
                    break;
                case CurveType.InterpCurveQuat:
                    throw new NotImplementedException($"InterpCurveQuat has not been implemented yet.");
                case CurveType.InterpCurveTwoVectors:
                    throw new NotImplementedException($"InterpCurveTwoVectors has not been implemented yet.");
                case CurveType.InterpCurveLinearColor:
                    throw new NotImplementedException($"InterpCurveLinearColor has not been implemented yet.");
            }
            foreach (var curve in Curves)
            {
                curve.SharedValueChanged += Curve_SharedValueChanged;
                curve.ListModified += Curve_ListModified;
            }
        }

        private bool updatingCurves;
        private void Curve_ListModified(object sender, (bool added, int index) e)
        {
            if (updatingCurves)
            {
                return;
            }
            updatingCurves = true;
            int index = e.index;
            var c = sender as Curve;
            //added
            if (e.added)
            {
                foreach (var curve in Curves)
                {
                    if (curve != c)
                    {
                        CurvePoint p = c.CurvePoints.ElementAt(index);
                        if (index == 0)
                        {
                            curve.CurvePoints.AddFirst(new CurvePoint(p.InVal, Enumerable.First(curve.CurvePoints).OutVal, 0, 0, p.InterpMode));
                        }
                        else
                        {
                            LinkedListNode<CurvePoint> prevNode = curve.CurvePoints.NodeAt(index - 1);
                            float outVal = prevNode.Value.OutVal;
                            if (prevNode.Next != null)
                            {
                                outVal += (prevNode.Next.Value.OutVal - outVal) / 2;
                            }
                            curve.CurvePoints.AddAfter(prevNode, new CurvePoint(p.InVal, outVal, 0, 0, p.InterpMode));
                        }
                    }
                }
            }
            //removed
            else
            {
                foreach (var curve in Curves)
                {
                    if (curve != c)
                    {
                        curve.CurvePoints.RemoveAt(index);
                    }
                }
            }
            updatingCurves = false;
        }

        private void Curve_SharedValueChanged(object sender, EventArgs e)
        {
            if (updatingCurves)
            {
                return;
            }
            updatingCurves = true;
            var c = sender as Curve;
            foreach (var curve in Curves)
            {
                if (curve != c)
                {
                    for (int i = 0; i < curve.CurvePoints.Count; i++)
                    {
                        curve.CurvePoints.ElementAt(i).InterpMode = c.CurvePoints.ElementAt(i).InterpMode;
                        curve.CurvePoints.ElementAt(i).InVal = c.CurvePoints.ElementAt(i).InVal;
                    }
                }
            }
            updatingCurves = false;
        }

        public StructProperty WriteProperties()
        {
            switch (curveType)
            {
                case CurveType.InterpCurveFloat:
                {
                    var interpCurveFloat = new InterpCurveFloat
                    {
                        InterpMethod = interpMethod
                    };
                    interpCurveFloat.Points.AddRange(Curves[0].CurvePoints.Select(point => new InterpCurvePointFloat(point.InVal, point.OutVal, point.ArriveTangent, point.LeaveTangent, point.InterpMode)));
                    interpCurveFloat.ReCalculateTangents();

                    var node = Curves[0].CurvePoints.First;
                    for (int i = 0; i < interpCurveFloat.Points.Count && node is not null; i++, node = node.Next)
                    {
                        node.Value.ArriveTangent = interpCurveFloat.Points[i].ArriveTangent;
                        node.Value.LeaveTangent = interpCurveFloat.Points[i].LeaveTangent;
                    }
                    return interpCurveFloat.ToStructProperty(game, Name);
                }
                case CurveType.InterpCurveVector:
                {
                    var interpCurveVector = new InterpCurveVector
                    {
                        InterpMethod = interpMethod
                    };
                    
                    LinkedListNode<CurvePoint> xNode = Curves[0].CurvePoints.First;
                    LinkedListNode<CurvePoint> yNode = Curves[1].CurvePoints.First;
                    LinkedListNode<CurvePoint> zNode = Curves[2].CurvePoints.First;
                    while (xNode != null && yNode != null && zNode != null)
                    {
                        interpCurveVector.Points.Add(new InterpCurvePointVector(
                            xNode.Value.InVal,
                            new Vector3(xNode.Value.OutVal, yNode.Value.OutVal, zNode.Value.OutVal),
                            new Vector3(xNode.Value.ArriveTangent, yNode.Value.ArriveTangent, zNode.Value.ArriveTangent),
                            new Vector3(xNode.Value.LeaveTangent, yNode.Value.LeaveTangent, zNode.Value.LeaveTangent),
                            xNode.Value.InterpMode));
                        xNode = xNode.Next;
                        yNode = yNode.Next;
                        zNode = zNode.Next;
                    }
                    interpCurveVector.ReCalculateTangents();

                    xNode = Curves[0].CurvePoints.First;
                    yNode = Curves[1].CurvePoints.First;
                    zNode = Curves[2].CurvePoints.First;

                    for (int i = 0; 
                        i < interpCurveVector.Points.Count && xNode is not null && yNode is not null && zNode is not null;
                        i++, xNode = xNode.Next, yNode = yNode.Next, zNode = zNode.Next)
                    {
                        xNode.Value.ArriveTangent = interpCurveVector.Points[i].ArriveTangent.X;
                        xNode.Value.LeaveTangent = interpCurveVector.Points[i].LeaveTangent.X;
                        yNode.Value.ArriveTangent = interpCurveVector.Points[i].ArriveTangent.Y;
                        yNode.Value.LeaveTangent = interpCurveVector.Points[i].LeaveTangent.Y;
                        zNode.Value.ArriveTangent = interpCurveVector.Points[i].ArriveTangent.Z;
                        zNode.Value.LeaveTangent = interpCurveVector.Points[i].LeaveTangent.Z;
                    }

                    return interpCurveVector.ToStructProperty(game, Name);
                }
                case CurveType.InterpCurveVector2D:
                {
                    var interpCurveVector2D = new InterpCurveVector2D
                    {
                        InterpMethod = interpMethod
                    };

                    LinkedListNode<CurvePoint> xNode = Curves[0].CurvePoints.First;
                    LinkedListNode<CurvePoint> yNode = Curves[1].CurvePoints.First;
                    while (xNode != null && yNode != null)
                    {
                        interpCurveVector2D.Points.Add(new InterpCurvePointVector2D(
                            xNode.Value.InVal,
                            new Vector2(xNode.Value.OutVal, yNode.Value.OutVal),
                            new Vector2(xNode.Value.ArriveTangent, yNode.Value.ArriveTangent),
                            new Vector2(xNode.Value.LeaveTangent, yNode.Value.LeaveTangent),
                            xNode.Value.InterpMode));
                        xNode = xNode.Next;
                        yNode = yNode.Next;
                    }
                    interpCurveVector2D.ReCalculateTangents();

                    xNode = Curves[0].CurvePoints.First;
                    yNode = Curves[1].CurvePoints.First;

                    for (int i = 0;
                        i < interpCurveVector2D.Points.Count && xNode is not null && yNode is not null;
                        i++, xNode = xNode.Next, yNode = yNode.Next)
                    {
                        xNode.Value.ArriveTangent = interpCurveVector2D.Points[i].ArriveTangent.X;
                        xNode.Value.LeaveTangent = interpCurveVector2D.Points[i].LeaveTangent.X;
                        yNode.Value.ArriveTangent = interpCurveVector2D.Points[i].ArriveTangent.Y;
                        yNode.Value.LeaveTangent = interpCurveVector2D.Points[i].LeaveTangent.Y;
                    }

                    return interpCurveVector2D.ToStructProperty(game, Name);
                }
                case CurveType.InterpCurveQuat:
                case CurveType.InterpCurveTwoVectors:
                case CurveType.InterpCurveLinearColor:
                    throw new NotImplementedException();
            }

            return null;
        }
    }
}
