using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using Gibbed.IO;

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
                    throw new NotImplementedException($"{pcc.getNameEntry(p.Value.IntValue)} has not been implemented yet.");
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
                    throw new NotImplementedException($"{pcc.getNameEntry(p.Value.IntValue)} has not been implemented yet.");
                    break;
                case CurveType.InterpCurveTwoVectors:
                    throw new NotImplementedException($"{pcc.getNameEntry(p.Value.IntValue)} has not been implemented yet.");
                    break;
                case CurveType.InterpCurveLinearColor:
                    throw new NotImplementedException($"{pcc.getNameEntry(p.Value.IntValue)} has not been implemented yet.");
                    break;
                default:
                    break;
            }
            foreach (var curve in Curves)
            {
                curve.SharedValueChanged += Curve_SharedValueChanged;
            }
        }

        private bool updatingSharedValue = false;
        private void Curve_SharedValueChanged(object sender, EventArgs e)
        {
            if (updatingSharedValue)
            {
                return;
            }
            updatingSharedValue = true;
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
            updatingSharedValue = false;
        }

        public byte[] Serialize()
        {
            byte[] res = prop.raw;
            
            var points = PropertyReader.ReadStructArrayProp(pcc, PropertyReader.getPropOrNull(pcc, res, 32, "Points"));
            switch (curveType)
            {
                case CurveType.InterpCurveQuat:
                    break;
                case CurveType.InterpCurveFloat:
                    for (int i = 0; i < points.Count; i++)
                    {
                        foreach (var p in points[i])
                        {
                            switch (pcc.getNameEntry(p.Name))
                            {
                                case "InVal":
                                    res.OverwriteRange(p.offsetval + 32, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).InVal));
                                    break;
                                case "OutVal":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).OutVal));
                                    break;
                                case "ArriveTangent":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).ArriveTangent));
                                    break;
                                case "LeaveTangent":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).LeaveTangent));
                                    break;
                                case "InterpMode":
                                    res.OverwriteRange(p.offsetval + 32, BitConverter.GetBytes(pcc.FindNameOrAdd(Curves[0].CurvePoints.ElementAt(i).InterpMode.ToString())));
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    break;
                case CurveType.InterpCurveVector:
                    for (int i = 0; i < points.Count; i++)
                    {
                        foreach (var p in points[i])
                        {
                            switch (pcc.getNameEntry(p.Name))
                            {
                                case "InVal":
                                    res.OverwriteRange(p.offsetval + 32, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).InVal));
                                    break;
                                case "OutVal":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).OutVal));
                                    res.OverwriteRange(p.offsetval + 44, BitConverter.GetBytes(Curves[1].CurvePoints.ElementAt(i).OutVal));
                                    res.OverwriteRange(p.offsetval + 48, BitConverter.GetBytes(Curves[2].CurvePoints.ElementAt(i).OutVal));
                                    break;
                                case "ArriveTangent":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).ArriveTangent));
                                    res.OverwriteRange(p.offsetval + 44, BitConverter.GetBytes(Curves[1].CurvePoints.ElementAt(i).ArriveTangent));
                                    res.OverwriteRange(p.offsetval + 48, BitConverter.GetBytes(Curves[2].CurvePoints.ElementAt(i).ArriveTangent));
                                    break;
                                case "LeaveTangent":
                                    res.OverwriteRange(p.offsetval + 40, BitConverter.GetBytes(Curves[0].CurvePoints.ElementAt(i).LeaveTangent));
                                    res.OverwriteRange(p.offsetval + 44, BitConverter.GetBytes(Curves[1].CurvePoints.ElementAt(i).LeaveTangent));
                                    res.OverwriteRange(p.offsetval + 48, BitConverter.GetBytes(Curves[2].CurvePoints.ElementAt(i).LeaveTangent));
                                    break;
                                case "InterpMode":
                                    res.OverwriteRange(p.offsetval + 32, BitConverter.GetBytes(pcc.FindNameOrAdd(Curves[0].CurvePoints.ElementAt(i).InterpMode.ToString())));
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
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

            return res;
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
