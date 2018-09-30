using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using Gibbed.IO;
using System.IO;
using ME3Explorer.Packages;

namespace ME3Explorer.CurveEd
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

    public struct Vector
    {
        public float X;
        public float Y;
        public float Z;

        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class InterpCurve
    {

        private IMEPackage pcc;
        private CurveType curveType;

        public string Name { get; set; }
        public ObservableCollection<Curve> Curves { get; set; }

        public InterpCurve(IMEPackage _pcc, StructProperty prop)
        {
            pcc = _pcc;

            Curves = new ObservableCollection<Curve>();
            Name = prop.Name;
            curveType = (CurveType)Enum.Parse(typeof(CurveType), prop.StructType);

            float InVal = 0f;
            CurveMode InterpMode = CurveMode.CIM_Linear;
            var points = prop.Properties.GetProp<ArrayProperty<StructProperty>>("Points");
            switch (curveType)
            {
                case CurveType.InterpCurveQuat:
                    throw new NotImplementedException($"InterpCurveQuat has not been implemented yet.");
                case CurveType.InterpCurveFloat:
                    float OutVal = 0f;
                    float ArriveTangent = 0f;
                    float LeaveTangent = 0f;
                    LinkedList<CurvePoint> vals = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var p in point.Properties)
                        {
                            switch (p.Name)
                            {
                                case "InVal":
                                    InVal = (p as FloatProperty).Value;
                                    break;
                                case "OutVal":
                                    OutVal = (p as FloatProperty).Value;
                                    break;
                                case "ArriveTangent":
                                    ArriveTangent = (p as FloatProperty).Value;
                                    break;
                                case "LeaveTangent":
                                    LeaveTangent = (p as FloatProperty).Value;
                                    break;
                                case "InterpMode":
                                    InterpMode = (CurveMode)Enum.Parse(typeof(CurveMode), (p as EnumProperty).Value);
                                    break;
                            }
                        }
                        vals.AddLast(new CurvePoint(InVal, OutVal, ArriveTangent, LeaveTangent, InterpMode));
                    }
                    Curves.Add(new Curve("X", vals));
                    break;
                case CurveType.InterpCurveVector:
                    Vector OutValVec = new Vector(0, 0, 0);
                    Vector ArriveTangentVec = new Vector(0, 0, 0);
                    Vector LeaveTangentVec = new Vector(0, 0, 0);
                    LinkedList<CurvePoint> x = new LinkedList<CurvePoint>();
                    LinkedList<CurvePoint> y = new LinkedList<CurvePoint>();
                    LinkedList<CurvePoint> z = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var p in point.Properties)
                        {
                            switch (p.Name)
                            {
                                case "InVal":
                                    InVal = (p as FloatProperty).Value;
                                    break;
                                case "OutVal":
                                    OutValVec = GetVector(p as StructProperty);
                                    break;
                                case "ArriveTangent":
                                    ArriveTangentVec = GetVector(p as StructProperty);
                                    break;
                                case "LeaveTangent":
                                    LeaveTangentVec = GetVector(p as StructProperty);
                                    break;
                                case "InterpMode":
                                    InterpMode = (CurveMode)Enum.Parse(typeof(CurveMode), (p as EnumProperty).Value);
                                    break;
                            }
                        }
                        x.AddLast(new CurvePoint(InVal, OutValVec.X, ArriveTangentVec.X, LeaveTangentVec.X, InterpMode));
                        y.AddLast(new CurvePoint(InVal, OutValVec.Y, ArriveTangentVec.Y, LeaveTangentVec.Y, InterpMode));
                        z.AddLast(new CurvePoint(InVal, OutValVec.Z, ArriveTangentVec.Z, LeaveTangentVec.Z, InterpMode));
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
                    throw new NotImplementedException($"InterpCurveVector2D has not been implemented yet.");
                case CurveType.InterpCurveTwoVectors:
                    throw new NotImplementedException($"InterpCurveTwoVectors has not been implemented yet.");
                case CurveType.InterpCurveLinearColor:
                    throw new NotImplementedException($"InterpCurveLinearColor has not been implemented yet.");
                default:
                    break;
            }
            foreach (var curve in Curves)
            {
                curve.SharedValueChanged += Curve_SharedValueChanged;
                curve.ListModified += Curve_ListModified;
            }
        }

        private bool updatingCurves = false;
        private void Curve_ListModified(object sender, Tuple<bool, int> e)
        {
            if (updatingCurves)
            {
                return;
            }
            updatingCurves = true;
            int index = e.Item2;
            Curve c = sender as Curve;
            //added
            if (e.Item1)
            {
                foreach (var curve in Curves)
                {
                    if (curve != c)
                    {
                        CurvePoint p = c.CurvePoints.ElementAt(index);
                        if (index == 0)
                        {
                            curve.CurvePoints.AddFirst(new CurvePoint(p.InVal, curve.CurvePoints.First().OutVal, 0, 0, p.InterpMode));
                        }
                        else
                        {
                            LinkedListNode<CurvePoint> prevNode = curve.CurvePoints.NodeAt(index - 1);
                            float outVal = prevNode.Value.OutVal;
                            if (prevNode.Next != null)
                            {
                                outVal = outVal + (prevNode.Next.Value.OutVal - outVal) / 2;
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
            Curve c = sender as Curve;
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
                case CurveType.InterpCurveQuat:
                    break;
                case CurveType.InterpCurveFloat:
                    return new StructProperty("InterpCurveFloat", new PropertyCollection
                    {
                        new ArrayProperty<StructProperty>(Curves[0].CurvePoints.Select(point => 
                        {
                            return new StructProperty("InterpCurvePointFloat", new PropertyCollection
                            {
                                new FloatProperty(point.InVal, "InVal"),
                                new FloatProperty(point.OutVal, "OutVal"),
                                new FloatProperty(point.ArriveTangent, "ArriveTangent"),
                                new FloatProperty(point.LeaveTangent, "LeaveTangent"),
                                new EnumProperty(point.InterpMode.ToString(), "EInterpCurveMode", pcc, "InterpMode")
                            });
                        }).ToList(), ArrayType.Struct, "Points")
                    }, Name);
                case CurveType.InterpCurveVector:
                    var points = new List<StructProperty>();
                    LinkedListNode<CurvePoint> xNode = Curves[0].CurvePoints.First;
                    LinkedListNode<CurvePoint> yNode = Curves[1].CurvePoints.First;
                    LinkedListNode<CurvePoint> zNode = Curves[2].CurvePoints.First;
                    while (xNode != null)
                    {
                        points.Add(new StructProperty("InterpCurvePointVector", new PropertyCollection
                        {
                            new FloatProperty(xNode.Value.InVal, "InVal"),
                            new StructProperty("Vector", new PropertyCollection
                            {
                                new FloatProperty(xNode.Value.OutVal),
                                new FloatProperty(yNode.Value.OutVal),
                                new FloatProperty(zNode.Value.OutVal)
                            }, "OutVal", true),
                            new StructProperty("Vector", new PropertyCollection
                            {
                                new FloatProperty(xNode.Value.ArriveTangent),
                                new FloatProperty(yNode.Value.ArriveTangent),
                                new FloatProperty(zNode.Value.ArriveTangent)
                            }, "ArriveTangent", true),
                            new StructProperty("Vector", new PropertyCollection
                            {
                                new FloatProperty(xNode.Value.LeaveTangent),
                                new FloatProperty(yNode.Value.LeaveTangent),
                                new FloatProperty(zNode.Value.LeaveTangent)
                            }, "LeaveTangent", true),
                            new EnumProperty(xNode.Value.InterpMode.ToString(), "EInterpCurveMode", pcc, "InterpMode")
                        }));
                        xNode = xNode.Next;
                        yNode = yNode.Next;
                        zNode = zNode.Next;
                    }
                    return new StructProperty("InterpCurveVector", new PropertyCollection
                    {
                        new ArrayProperty<StructProperty>(points, ArrayType.Struct, "Points")
                    }, Name);
                case CurveType.InterpCurveVector2D:
                    break;
                case CurveType.InterpCurveTwoVectors:
                    break;
                case CurveType.InterpCurveLinearColor:
                    break;
            }

            return null;
        }

        private static Vector GetVector(StructProperty props)
        {
            Vector vec = new Vector();
            foreach (var prop in props.Properties)
            {
                switch (prop.Name)
                {
                    case "X":
                        vec.X = (prop as FloatProperty).Value;
                        break;
                    case "Y":
                        vec.Y = (prop as FloatProperty).Value;
                        break;
                    case "Z":
                        vec.Z = (prop as FloatProperty).Value;
                        break;
                }
            }
            return vec;
        }


    }
}
