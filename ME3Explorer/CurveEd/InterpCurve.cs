using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using Gibbed.IO;
using System.IO;

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

        private PCCObject pcc;
        private PropertyReader.Property prop;
        private CurveType curveType;

        public string Name { get; set; }
        public ObservableCollection<Curve> Curves { get; set; }


        public InterpCurve(PCCObject _pcc, PropertyReader.Property p)
        {
            pcc = _pcc;
            prop = p;

            Curves = new ObservableCollection<Curve>();
            Name = pcc.getNameEntry(p.Name);
            curveType = (CurveType)Enum.Parse(typeof(CurveType), pcc.getNameEntry(p.Value.IntValue));

            float InVal = 0f;
            CurveMode InterpMode = CurveMode.CIM_Linear;
            var points = PropertyReader.ReadStructArrayProp(pcc, PropertyReader.getPropOrNull(pcc, p.raw, 32, "Points"));
            switch (curveType)
            {
                case CurveType.InterpCurveQuat:
                    throw new NotImplementedException($"InterpCurveQuat has not been implemented yet.");
                    break;
                case CurveType.InterpCurveFloat:
                    float OutVal = 0f;
                    float ArriveTangent = 0f;
                    float LeaveTangent = 0f;
                    LinkedList<CurvePoint> vals = new LinkedList<CurvePoint>();
                    foreach (var point in points)
                    {
                        foreach (var prop in point)
                        {
                            switch (pcc.getNameEntry(prop.Name))
                            {
                                case "InVal":
                                    InVal = BitConverter.ToSingle(prop.raw, 24);
                                    break;
                                case "OutVal":
                                    OutVal = BitConverter.ToSingle(prop.raw, 24);
                                    break;
                                case "ArriveTangent":
                                    ArriveTangent = BitConverter.ToSingle(prop.raw, 24);
                                    break;
                                case "LeaveTangent":
                                    LeaveTangent = BitConverter.ToSingle(prop.raw, 24);
                                    break;
                                case "InterpMode":
                                    InterpMode = (CurveMode)Enum.Parse(typeof(CurveMode), pcc.getNameEntry(prop.Value.IntValue));
                                    break;
                                default:
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
                        foreach (var prop in point)
                        {
                            switch (pcc.getNameEntry(prop.Name))
                            {
                                case "InVal":
                                    InVal = BitConverter.ToSingle(prop.raw, 24);
                                    break;
                                case "OutVal":
                                    OutValVec = GetVector(prop);
                                    break;
                                case "ArriveTangent":
                                    ArriveTangentVec = GetVector(prop);
                                    break;
                                case "LeaveTangent":
                                    LeaveTangentVec = GetVector(prop);
                                    break;
                                case "InterpMode":
                                    InterpMode = (CurveMode)Enum.Parse(typeof(CurveMode), pcc.getNameEntry(prop.Value.IntValue));
                                    break;
                                default:
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
                    break;
                case CurveType.InterpCurveTwoVectors:
                    throw new NotImplementedException($"InterpCurveTwoVectors has not been implemented yet.");
                    break;
                case CurveType.InterpCurveLinearColor:
                    throw new NotImplementedException($"InterpCurveLinearColor has not been implemented yet.");
                    break;
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

        public byte[] Serialize()
        {
            MemoryStream m = new MemoryStream();
            MemoryStream temp = new MemoryStream();

            int count = Curves[0].CurvePoints.Count;

            switch (curveType)
            {
                case CurveType.InterpCurveQuat:
                    break;
                case CurveType.InterpCurveFloat:
                    for (int i = 0; i < count; i++)
                    {
                        m.WriteFloatProperty(pcc, "InVal", Curves[0].CurvePoints.ElementAt(i).InVal);
                        m.WriteFloatProperty(pcc, "OutVal", Curves[0].CurvePoints.ElementAt(i).OutVal);
                        m.WriteFloatProperty(pcc, "ArriveTangent", Curves[0].CurvePoints.ElementAt(i).ArriveTangent);
                        m.WriteFloatProperty(pcc, "LeaveTangent", Curves[0].CurvePoints.ElementAt(i).LeaveTangent);
                        m.WriteByteProperty(pcc, "InterpMode", "EInterpCurveMode", Curves[0].CurvePoints.ElementAt(i).InterpMode.ToString());
                        m.WriteNoneProperty(pcc);
                    }
                    temp.WriteArrayProperty(pcc, "Points", count, m.ToArray());
                    temp.WriteNoneProperty(pcc);
                    m = new MemoryStream();
                    m.WriteStructProperty(pcc, Name, "InterpCurveFloat", temp.ToArray());
                    break;
                case CurveType.InterpCurveVector:
                    for (int i = 0; i < count; i++)
                    {
                        m.WriteFloatProperty(pcc, "InVal", Curves[0].CurvePoints.ElementAt(i).InVal);
                        m.WriteStructPropVector(pcc, "OutVal", Curves[0].CurvePoints.ElementAt(i).OutVal,
                                                               Curves[1].CurvePoints.ElementAt(i).OutVal,
                                                               Curves[2].CurvePoints.ElementAt(i).OutVal);
                        m.WriteStructPropVector(pcc, "ArriveTangent", Curves[0].CurvePoints.ElementAt(i).ArriveTangent,
                                                                      Curves[1].CurvePoints.ElementAt(i).ArriveTangent,
                                                                      Curves[2].CurvePoints.ElementAt(i).ArriveTangent);
                        m.WriteStructPropVector(pcc, "LeaveTangent", Curves[0].CurvePoints.ElementAt(i).LeaveTangent,
                                                                     Curves[1].CurvePoints.ElementAt(i).LeaveTangent,
                                                                     Curves[2].CurvePoints.ElementAt(i).LeaveTangent);
                        m.WriteByteProperty(pcc, "InterpMode", "EInterpCurveMode", Curves[0].CurvePoints.ElementAt(i).InterpMode.ToString());
                        m.WriteNoneProperty(pcc);
                    }
                    temp.WriteArrayProperty(pcc, "Points", count, m.ToArray());
                    temp.WriteNoneProperty(pcc);
                    m = new MemoryStream();
                    m.WriteStructProperty(pcc, Name, "InterpCurveVector", temp.ToArray());
                    break;
                case CurveType.InterpCurveVector2D:
                    break;
                case CurveType.InterpCurveTwoVectors:
                    break;
                case CurveType.InterpCurveLinearColor:
                    break;
                default:
                    break;
            }

            return m.ToArray();
        }

        private static Vector GetVector(PropertyReader.Property prop)
        {
            Vector vec;
            vec.X = BitConverter.ToSingle(prop.raw, 32);
            vec.Y = BitConverter.ToSingle(prop.raw, 36);
            vec.Z = BitConverter.ToSingle(prop.raw, 40);
            return vec;
        }


    }
}
