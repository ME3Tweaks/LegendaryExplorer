using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.ME3Enums;
using SharpDX;

namespace ME3Explorer.Unreal.BinaryConverters
{
    //works when T is float or Vector
    public class InterpCurvePoint<T>
    {
        public float InVal { get; set; }
        public T OutVal { get; set; }
        public T ArriveTangent { get; set; }
        public T LeaveTangent { get; set; }
        public EInterpCurveMode InterpMode { get; set; }

        public InterpCurvePoint() { }

        public InterpCurvePoint(float inVal, T outVal)
        {
            InVal = inVal;
            OutVal = outVal;
        }

        public InterpCurvePoint(float inVal, T outVal, T arriveTangent, T leaveTangent, EInterpCurveMode interpMode) : this(inVal, outVal)
        {
            ArriveTangent = arriveTangent;
            LeaveTangent = leaveTangent;
            InterpMode = interpMode;
        }

        public StructProperty ToStructProperty(MEGame game) => Helper.ToStructProperty(this, game);

        public static InterpCurvePoint<T> FromStructProperty(StructProperty prop) => Helper.FromStructProperty<T>(prop);

        static class Helper
        {
            class Specialization : ISpecialization<float>, ISpecialization<Vector3>
            {
                public static readonly Specialization Inst = new Specialization();
                InterpCurvePoint<float> ISpecialization<float>.FromStructProperty(StructProperty prop)
                {
                    Enum.TryParse(prop.GetProp<EnumProperty>("InterpMode")?.Value, out EInterpCurveMode curveMode);

                    return new InterpCurvePoint<float>(prop.GetProp<FloatProperty>("InVal"),
                                                     prop.GetProp<FloatProperty>("OutVal"),
                                                     prop.GetProp<FloatProperty>("ArriveTangent"),
                                                     prop.GetProp<FloatProperty>("LeaveTangent"),
                                                     curveMode);
                }

                public StructProperty ToStructProperty(InterpCurvePoint<float> icp, MEGame game)
                {
                    return new StructProperty("InterpCurvePointFloat", new PropertyCollection
                    {
                        new FloatProperty(icp.InVal, "InVal"),
                        new FloatProperty(icp.OutVal, "OutVal"),
                        new FloatProperty(icp.ArriveTangent, "ArriveTangent"),
                        new FloatProperty(icp.LeaveTangent, "LeaveTangent"),
                        new EnumProperty(icp.InterpMode.ToString(), "EInterpCurveMode", game, "InterpMode")
                    });
                }

                InterpCurvePoint<Vector3> ISpecialization<Vector3>.FromStructProperty(StructProperty prop)
                {
                    Enum.TryParse(prop.GetProp<EnumProperty>("InterpMode")?.Value, out EInterpCurveMode curveMode);

                    return new InterpCurvePoint<Vector3>(prop.GetProp<FloatProperty>("InVal"),
                                                      CommonStructs.GetVector3(prop.GetProp<StructProperty>("OutVal")),
                                                      CommonStructs.GetVector3(prop.GetProp<StructProperty>("ArriveTangent")),
                                                      CommonStructs.GetVector3(prop.GetProp<StructProperty>("LeaveTangent")),
                                                      curveMode);
                }

                public StructProperty ToStructProperty(InterpCurvePoint<Vector3> icp, MEGame game)
                {
                    return new StructProperty("InterpCurvePointVector", new PropertyCollection
                    {
                        new FloatProperty(icp.InVal, "InVal"),
                        CommonStructs.Vector3(icp.OutVal, "OutVal"),
                        CommonStructs.Vector3(icp.ArriveTangent, "ArriveTangent"),
                        CommonStructs.Vector3(icp.LeaveTangent, "LeaveTangent"),
                        new EnumProperty(icp.InterpMode.ToString(), "EInterpCurveMode", game, "InterpMode")
                    });
                }
            }
            public static InterpCurvePoint<U> FromStructProperty<U>(StructProperty prop) => Specialization<U>.Inst.FromStructProperty(prop);
            public static StructProperty ToStructProperty<U>(InterpCurvePoint<U> icp, MEGame game) => Specialization<U>.Inst.ToStructProperty(icp, game);

            //the following generic specialization code is based on https://stackoverflow.com/a/29379250
            interface ISpecialization<U>
            {
                InterpCurvePoint<U> FromStructProperty(StructProperty prop);
                StructProperty ToStructProperty(InterpCurvePoint<U> icp, MEGame game);
            }

            class Specialization<U> : ISpecialization<U>
            {
                public static readonly ISpecialization<U> Inst = Specialization.Inst as ISpecialization<U> ?? new Specialization<U>();

                //default implementation
                InterpCurvePoint<U> ISpecialization<U>.FromStructProperty(StructProperty prop) => throw new NotImplementedException();
                StructProperty ISpecialization<U>.ToStructProperty(InterpCurvePoint<U> icp, MEGame game) => throw new NotImplementedException();
            }
        }
    }

    //works when T is float or Vector
    public class InterpCurve<T>
    {
        public List<InterpCurvePoint<T>> Points = new List<InterpCurvePoint<T>>();
        public EInterpMethodType InterpMethod = EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents;

        public int AddPoint(float inVal, T outVal)
        {
            int i = 0;
            for (; i < Points.Count && Points[i].InVal < inVal; i++) { }

            Points.Insert(i, Helper.CreatePoint(inVal, outVal));
            return i;
        }
        public T Eval(float inVal, T defaultVal)
        {
            int len = Points.Count;

            if (len == 0)
            {
                return defaultVal;
            }

            if (len == 1 || inVal < Points[0].InVal)
            {
                return Points[0].OutVal;
            }

            if (inVal >= Points[len - 1].InVal)
            {
                return Points[len - 1].OutVal;
            }

            for (int i = 1; i < len; i++)
            {
                if (inVal >= Points[i].InVal) continue;

                float diff = Points[i].InVal - Points[i - 1].InVal;

                if (diff == 0f  || Points[i - 1].InterpMode == EInterpCurveMode.CIM_Constant)
                {
                    return Points[i - 1].OutVal;
                }

                float amount = (inVal - Points[i - 1].InVal) / diff;

                if (Points[i - 1].InterpMode == EInterpCurveMode.CIM_Linear)
                {
                    return Helper.Lerp(Points[i - 1].OutVal, Points[i].OutVal, amount);
                }

                if (InterpMethod == EInterpMethodType.IMT_UseBrokenTangentEval)
                {
                    return Helper.CubicInterp(Points[i - 1].OutVal, Points[i - 1].LeaveTangent, Points[i].OutVal, Points[i].ArriveTangent, amount);
                }

                return Helper.CubicInterp(Points[i - 1].OutVal, Helper.Mul(Points[i - 1].LeaveTangent, diff), Points[i].OutVal, Helper.Mul(Points[i].ArriveTangent, diff), amount);
            }

            return Points[len - 1].OutVal;
        }

        public StructProperty ToStructProperty(MEGame game, NameReference? name = null)
        {
            return new StructProperty(Helper.VectorType<T>(), new PropertyCollection
            {
                new ArrayProperty<StructProperty>(Points.Select(p => p.ToStructProperty(game)), "Points"),
            }, name);
        }

        public static InterpCurve<T> FromStructProperty(StructProperty prop)
        {
            var result = new InterpCurve<T>();
            if (prop.GetProp<ArrayProperty<StructProperty>>("Points") is { } pointProps)
            {
                result.Points.AddRange(pointProps.Select(InterpCurvePoint<T>.FromStructProperty));
            }

            return result;
        }

        static class Helper
        {
            class Specialization : ISpecialization<float>, ISpecialization<Vector3>
            {
                InterpCurvePoint<float> ISpecialization<float>.CreatePoint(float inVal, float outVal)
                {
                    return new InterpCurvePoint<float>(inVal, outVal);
                }

                float ISpecialization<float>.Lerp(float start, float end, float amount)
                {
                    return start + amount * (end - start);
                }

                float ISpecialization<float>.CubicInterp(float point0, float tan0, float point1, float tan1, float amount)
                {
                    float a2 = amount * amount;
                    float a3 = a2 * amount;

                    return (2 * a3 - 3 * a2 + 1) * point0 + (a3 - 2 * a2 + amount) * tan0 + (a3 - a2) * tan1 + (-2 * a3 + 3 * a2) * point1;
                }

                float ISpecialization<float>.Mul(float val, float multiplier)
                {
                    return val * multiplier;
                }

                string ISpecialization<float>.VectorType() => "InterpCurveFloat";

                InterpCurvePoint<Vector3> ISpecialization<Vector3>.CreatePoint(float inVal, Vector3 outVal)
                {
                    return new InterpCurvePoint<Vector3>(inVal, outVal);
                }

                Vector3 ISpecialization<Vector3>.Lerp(Vector3 start, Vector3 end, float amount)
                {
                    return start + amount * (end - start);
                }

                Vector3 ISpecialization<Vector3>.CubicInterp(Vector3 point0, Vector3 tan0, Vector3 point1, Vector3 tan1, float amount)
                {
                    float a2 = amount * amount;
                    float a3 = a2 * amount;

                    return (2 * a3 - 3 * a2 + 1) * point0 + (a3 - 2 * a2 + amount) * tan0 + (a3 - a2) * tan1 + (-2 * a3 + 3 * a2) * point1;
                }

                Vector3 ISpecialization<Vector3>.Mul(Vector3 val, float multiplier)
                {
                    return val * multiplier;
                }

                string ISpecialization<Vector3>.VectorType() => "InterpCurveVector";

                public static readonly Specialization Inst = new Specialization();
            }
            public static InterpCurvePoint<U> CreatePoint<U>(float inVal, U outVal) => Specialization<U>.Inst.CreatePoint(inVal, outVal);
            public static U Lerp<U>(U start, U end, float amount) => Specialization<U>.Inst.Lerp(start, end, amount);

            public static U CubicInterp<U>(U point0, U tan0, U point1, U tan1, float amount) => Specialization<U>.Inst.CubicInterp(point0, tan0, point1, tan1, amount);

            public static U Mul<U>(U val, float multiplier) => Specialization<U>.Inst.Mul(val, multiplier);

            public static string VectorType<U>() => Specialization<U>.Inst.VectorType();

            //the following generic specialization code is based on https://stackoverflow.com/a/29379250
            interface ISpecialization<U>
            {
                InterpCurvePoint<U> CreatePoint(float inVal, U outVal);
                U Lerp(U start, U end, float amount);
                U CubicInterp(U point0, U tan0, U point1, U tan1, float amount);
                U Mul(U val, float multiplier);

                string VectorType();
            }

            class Specialization<U> : ISpecialization<U>
            {
                public static readonly ISpecialization<U> Inst = Specialization.Inst as ISpecialization<U> ?? new Specialization<U>();

                //default implementation
                InterpCurvePoint<U> ISpecialization<U>.CreatePoint(float inVal, U outVal) => throw new NotSupportedException();
                U ISpecialization<U>.Lerp(U start, U end, float amount) => throw new NotImplementedException();
                U ISpecialization<U>.CubicInterp(U point0, U tan0, U point1, U tan1, float amount) => throw new NotImplementedException();
                U ISpecialization<U>.Mul(U val, float multiplier) => throw new NotImplementedException();
                string ISpecialization<U>.VectorType() => throw new NotImplementedException();
            }
        }
    }
}
