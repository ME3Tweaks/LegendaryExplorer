using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    //When using these classes, DON'T specify T where you us it, just put one or more of these in the file.
    //These are the only valid versions
    using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<LegendaryExplorerCore.SharpDX.Vector3>;
    using InterpCurveVector2D = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<LegendaryExplorerCore.SharpDX.Vector2>;
    using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;

    using InterpCurvePointVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<LegendaryExplorerCore.SharpDX.Vector3>;
    using InterpCurvePointVector2D = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<LegendaryExplorerCore.SharpDX.Vector2>;
    using InterpCurvePointFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<float>;

    //ONLY WORKS WHEN T is float, LegendaryExplorerCore.SharpDX.Vector3 or LegendaryExplorerCore.SharpDX.Vector2
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

        public StructProperty ToStructProperty(MEGame game) => Specialization<T>.Inst.ToStructProperty(this, game);

        public static InterpCurvePoint<T> FromStructProperty(StructProperty prop) => Specialization<T>.Inst.FromStructProperty(prop);

        class Specializations : ISpec<float>, ISpec<Vector3>, ISpec<Vector2>
        {
            #region float
            InterpCurvePoint<float> ISpec<float>.FromStructProperty(StructProperty prop) =>
                new InterpCurvePoint<float>(prop.GetProp<FloatProperty>("InVal"),
                                            prop.GetProp<FloatProperty>("OutVal"),
                                            prop.GetProp<FloatProperty>("ArriveTangent"),
                                            prop.GetProp<FloatProperty>("LeaveTangent"),
                                            GetCurveMode(prop));

            StructProperty ISpec<float>.ToStructProperty(InterpCurvePoint<float> icp, MEGame game)
            {
                return new StructProperty("InterpCurvePointFloat", new PropertyCollection
                {
                    new FloatProperty(icp.InVal, "InVal"),
                    new FloatProperty(icp.OutVal, "OutVal"),
                    new FloatProperty(icp.ArriveTangent, "ArriveTangent"),
                    new FloatProperty(icp.LeaveTangent, "LeaveTangent"),
                    icp.GetEnumProp(game)
                });
            }
            #endregion

            #region Vector3
            InterpCurvePoint<Vector3> ISpec<Vector3>.FromStructProperty(StructProperty prop) =>
                new InterpCurvePoint<Vector3>(prop.GetProp<FloatProperty>("InVal"),
                                              CommonStructs.GetVector3(prop.GetProp<StructProperty>("OutVal")),
                                              CommonStructs.GetVector3(prop.GetProp<StructProperty>("ArriveTangent")),
                                              CommonStructs.GetVector3(prop.GetProp<StructProperty>("LeaveTangent")),
                                              GetCurveMode(prop));

            StructProperty ISpec<Vector3>.ToStructProperty(InterpCurvePoint<Vector3> icp, MEGame game)
            {
                return new StructProperty("InterpCurvePointVector", new PropertyCollection
                {
                    new FloatProperty(icp.InVal, "InVal"),
                    CommonStructs.Vector3Prop(icp.OutVal, "OutVal"),
                    CommonStructs.Vector3Prop(icp.ArriveTangent, "ArriveTangent"),
                    CommonStructs.Vector3Prop(icp.LeaveTangent, "LeaveTangent"),
                    icp.GetEnumProp(game)
                });
            }
            #endregion

            #region Vector2
            InterpCurvePoint<Vector2> ISpec<Vector2>.FromStructProperty(StructProperty prop) =>
                new InterpCurvePoint<Vector2>(prop.GetProp<FloatProperty>("InVal"),
                                              CommonStructs.GetVector2(prop.GetProp<StructProperty>("OutVal")),
                                              CommonStructs.GetVector2(prop.GetProp<StructProperty>("ArriveTangent")),
                                              CommonStructs.GetVector2(prop.GetProp<StructProperty>("LeaveTangent")),
                                              GetCurveMode(prop));


            StructProperty ISpec<Vector2>.ToStructProperty(InterpCurvePoint<Vector2> icp, MEGame game)
            {
                return new StructProperty("InterpCurvePointVector2D", new PropertyCollection
                {
                    new FloatProperty(icp.InVal, "InVal"),
                    CommonStructs.Vector2Prop(icp.OutVal, "OutVal"),
                    CommonStructs.Vector2Prop(icp.ArriveTangent, "ArriveTangent"),
                    CommonStructs.Vector2Prop(icp.LeaveTangent, "LeaveTangent"),
                    icp.GetEnumProp(game)
                });
            }


            #endregion

            #region GenericSpecializationFramework

            public static readonly Specializations Inst = new Specializations();
            private Specializations() { }
        }

        //generic specialization code is based on https://stackoverflow.com/a/29379250
        private interface ISpec<U>
        {
            InterpCurvePoint<U> FromStructProperty(StructProperty prop);
            StructProperty ToStructProperty(InterpCurvePoint<U> icp, MEGame game);
        }

        private class Specialization<U> : ISpec<U>
        {
            //When U is specialized, this will be a working implementation, for all other U, it will bethe defaults below.
            public static readonly ISpec<U> Inst = Specializations.Inst as ISpec<U> ?? new Specialization<U>();
            InterpCurvePoint<U> ISpec<U>.FromStructProperty(StructProperty prop) => throw new NotSupportedException();
            StructProperty ISpec<U>.ToStructProperty(InterpCurvePoint<U> icp, MEGame game) => throw new NotSupportedException();

            #endregion
        }
        private EnumProperty GetEnumProp(MEGame game)
        {
            return new EnumProperty(InterpMode.ToString(), "EInterpCurveMode", game, "InterpMode");
        }
        private static EInterpCurveMode GetCurveMode(StructProperty prop)
        {
            Enum.TryParse(prop.GetProp<EnumProperty>("InterpMode")?.Value, out EInterpCurveMode curveMode);
            return curveMode;
        }
    }

    //ONLY WORKS WHEN T is float, LegendaryExplorerCore.SharpDX.Vector3 or LegendaryExplorerCore.SharpDX.Vector2
    public class InterpCurve<T>
    {
        public List<InterpCurvePoint<T>> Points = new List<InterpCurvePoint<T>>();
        public EInterpMethodType InterpMethod = EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents;

        public int AddPoint(float inVal, T outVal)
        {
            int i = 0;
            for (; i < Points.Count && Points[i].InVal < inVal; i++) { }

            Points.Insert(i, new InterpCurvePoint<T>(inVal, outVal));
            return i;
        }
        public int AddPoint(float inVal, T outVal, T arriveTangent, T leaveTangent, EInterpCurveMode curveMode)
        {
            int i = 0;
            for (; i < Points.Count && Points[i].InVal < inVal; i++) { }

            Points.Insert(i, new InterpCurvePoint<T>(inVal, outVal, arriveTangent, leaveTangent, curveMode));
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
                    return Lerp(Points[i - 1].OutVal, Points[i].OutVal, amount);
                }

                if (InterpMethod == EInterpMethodType.IMT_UseBrokenTangentEval)
                {
                    return CubicInterp(Points[i - 1].OutVal, Points[i - 1].LeaveTangent, Points[i].OutVal, Points[i].ArriveTangent, amount);
                }

                return CubicInterp(Points[i - 1].OutVal, Mul(Points[i - 1].LeaveTangent, diff), Points[i].OutVal, Mul(Points[i].ArriveTangent, diff), amount);
            }

            return Points[len - 1].OutVal;
        }

        public StructProperty ToStructProperty(MEGame game, NameReference? name = null)
        {
            return new StructProperty(VectorType<T>(), new PropertyCollection
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
        private class Specializations : ISpec<float>, ISpec<Vector3>, ISpec<Vector2>
        {
            #region float
            float ISpec<float>.Lerp(float start, float end, float amount)
            {
                return start + amount * (end - start);
            }

            float ISpec<float>.CubicInterp(float point0, float tan0, float point1, float tan1, float amount)
            {
                float a2 = amount * amount;
                float a3 = a2 * amount;

                return (2 * a3 - 3 * a2 + 1) * point0 + (a3 - 2 * a2 + amount) * tan0 + (a3 - a2) * tan1 + (-2 * a3 + 3 * a2) * point1;
            }

            float ISpec<float>.Mul(float val, float multiplier)
            {
                return val * multiplier;
            }

            string ISpec<float>.VectorType() => "InterpCurveFloat";
            #endregion

            #region Vector3
            Vector3 ISpec<Vector3>.Lerp(Vector3 start, Vector3 end, float amount)
            {
                return start + amount * (end - start);
            }

            Vector3 ISpec<Vector3>.CubicInterp(Vector3 point0, Vector3 tan0, Vector3 point1, Vector3 tan1, float amount)
            {
                float a2 = amount * amount;
                float a3 = a2 * amount;

                return (2 * a3 - 3 * a2 + 1) * point0 + (a3 - 2 * a2 + amount) * tan0 + (a3 - a2) * tan1 + (-2 * a3 + 3 * a2) * point1;
            }

            Vector3 ISpec<Vector3>.Mul(Vector3 val, float multiplier)
            {
                return val * multiplier;
            }

            string ISpec<Vector3>.VectorType() => "InterpCurveVector";
            #endregion

            #region Vector2
            Vector2 ISpec<Vector2>.Lerp(Vector2 start, Vector2 end, float amount)
            {
                return start + amount * (end - start);
            }

            Vector2 ISpec<Vector2>.CubicInterp(Vector2 point0, Vector2 tan0, Vector2 point1, Vector2 tan1, float amount)
            {
                float a2 = amount * amount;
                float a3 = a2 * amount;

                return (2 * a3 - 3 * a2 + 1) * point0 + (a3 - 2 * a2 + amount) * tan0 + (a3 - a2) * tan1 + (-2 * a3 + 3 * a2) * point1;
            }

            Vector2 ISpec<Vector2>.Mul(Vector2 val, float multiplier)
            {
                return val * multiplier;
            }

            string ISpec<Vector2>.VectorType() => "InterpCurveVector2D";
            #endregion

            #region GenericSpecializationFramework

            public static readonly Specializations Inst = new Specializations();
            private Specializations() { }
        }


        //generic specialization code is based on https://stackoverflow.com/a/29379250
        private static U Lerp<U>(U start, U end, float amount) => Specialization<U>.Inst.Lerp(start, end, amount);
        private static U CubicInterp<U>(U point0, U tan0, U point1, U tan1, float amount) => Specialization<U>.Inst.CubicInterp(point0, tan0, point1, tan1, amount);
        private static U Mul<U>(U val, float multiplier) => Specialization<U>.Inst.Mul(val, multiplier);
        private static string VectorType<U>() => Specialization<U>.Inst.VectorType();

        private interface ISpec<U>
        {
            U Lerp(U start, U end, float amount);
            U CubicInterp(U point0, U tan0, U point1, U tan1, float amount);
            U Mul(U val, float multiplier);
            string VectorType();
        }

        private class Specialization<U> : ISpec<U>
        {
            //When U is specialized, this will be a working implementation, for all other U, it will bethe defaults below.
            public static readonly ISpec<U> Inst = Specializations.Inst as ISpec<U> ?? new Specialization<U>();
            U ISpec<U>.Lerp(U start, U end, float amount) => throw new NotSupportedException();
            U ISpec<U>.CubicInterp(U point0, U tan0, U point1, U tan1, float amount) => throw new NotSupportedException();
            U ISpec<U>.Mul(U val, float multiplier) => throw new NotSupportedException();
            string ISpec<U>.VectorType() => throw new NotSupportedException();

            #endregion
        }
    }
}
