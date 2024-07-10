using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    //When using these classes, DON'T specify T where you use it, just put one or more of these in the file.
    //These are the only valid versions
    //ONLY WORKS WHEN T is float, System.Numerics.Vector3 or System.Numerics.Vector2
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

    //ONLY WORKS WHEN T is float, System.Numerics.Vector3 or System.Numerics.Vector2
    public class InterpCurve<T>
    {
        public readonly List<InterpCurvePoint<T>> Points = new();
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
                InterpCurvePoint<T> cur = Points[i];
                if (inVal >= cur.InVal) continue;

                InterpCurvePoint<T> prev = Points[i - 1];
                float diff = cur.InVal - prev.InVal;

                if (diff == 0f || prev.InterpMode == EInterpCurveMode.CIM_Constant)
                {
                    return prev.OutVal;
                }

                float amount = (inVal - prev.InVal) / diff;

                if (prev.InterpMode == EInterpCurveMode.CIM_Linear)
                {
                    return Lerp(prev.OutVal, cur.OutVal, amount);
                }

                if (InterpMethod == EInterpMethodType.IMT_UseBrokenTangentEval)
                {
                    return CubicInterp(prev.OutVal, prev.LeaveTangent, cur.OutVal, cur.ArriveTangent, amount);
                }

                return CubicInterp(prev.OutVal, Mul(diff, prev.LeaveTangent), cur.OutVal, Mul(diff, cur.ArriveTangent), amount);
            }

            return Points[len - 1].OutVal;
        }

        public void ReCalculateTangents(float tension = 0f)
        {
            var zero = Zero<T>();
            tension = 1f - tension;
            if (Points.Count == 1)
            {
                Points[0].ArriveTangent = Points[0].LeaveTangent = zero;
            }
            else if (Points.Count > 1)
            {
                if (Points[0].InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                {
                    Points[0].ArriveTangent = Points[0].LeaveTangent = zero;
                }
                if (Points[^1].InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                {
                    Points[^1].ArriveTangent = Points[^1].LeaveTangent = zero;
                }
                for (int i = 1; i < Points.Count - 1; i++)
                {
                    InterpCurvePoint<T> cur = Points[i];
                    if (cur.InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                    {
                        InterpCurvePoint<T> prev = Points[i - 1];
                        if (prev.InterpMode is EInterpCurveMode.CIM_Constant)
                        {
                            cur.ArriveTangent = cur.LeaveTangent = zero;
                        }
                        else if (prev.InterpMode is not EInterpCurveMode.CIM_Linear)
                        {
                            InterpCurvePoint<T> next = Points[i + 1];
                            if (InterpMethod is EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents)
                            {
                                if (cur.InterpMode is EInterpCurveMode.CIM_CurveAutoClamped)
                                {
                                    cur.ArriveTangent = cur.LeaveTangent = Mul(tension, ClampedTangent(prev.OutVal, prev.InVal, cur.OutVal, cur.InVal, next.OutVal, next.InVal));
                                }
                                else
                                {
                                    cur.ArriveTangent = cur.LeaveTangent = Mul(1 / MathF.Max(next.InVal - prev.InVal, TOLERANCE) * tension, Tangent(prev.OutVal, cur.OutVal, next.OutVal));
                                }
                            }
                            else
                            {
                                cur.ArriveTangent = cur.LeaveTangent = Mul(0.5f * tension, Tangent(prev.OutVal, cur.OutVal, next.OutVal));
                            }
                        }
                    }
                }
            }
        }

        public StructProperty ToStructProperty(MEGame game, NameReference? name = null)
        {
            var props = new PropertyCollection
            {
                new ArrayProperty<StructProperty>(Points.Select(p => p.ToStructProperty(game)), "Points"),
            };
            EInterpMethodType defaultInterpMethod = game is MEGame.ME1 or MEGame.ME2
                ? EInterpMethodType.IMT_UseFixedTangentEval
                : EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents;
            if (InterpMethod != defaultInterpMethod)
            {
                props.AddOrReplaceProp(new EnumProperty(InterpMethod.ToString(), "EInterpMethodType", game, "InterpMethod"));
            }
            return new StructProperty(VectorType<T>(), props, name);
        }

        public static InterpCurve<T> FromStructProperty(StructProperty prop, MEGame game)
        {
            var result = new InterpCurve<T>();
            if (prop.GetProp<ArrayProperty<StructProperty>>("Points") is { } pointProps)
            {
                result.Points.AddRange(pointProps.Select(InterpCurvePoint<T>.FromStructProperty));
            }
            if (prop.GetProp<EnumProperty>("InterpMethod") is { } interpMethodProp 
                && Enum.TryParse(interpMethodProp.Value.Name, out EInterpMethodType interpMethodType))
            {
                result.InterpMethod = interpMethodType;
            }
            else if (game is MEGame.ME1 or MEGame.ME2) //pre-ME3 engine versions did not have IMT_UseFixedTangentEvalAndNewAutoTangents
            {
                result.InterpMethod = EInterpMethodType.IMT_UseFixedTangentEval;
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

            float ISpec<float>.Tangent(float prev, float cur, float next)
            {
                return cur - prev + (next - cur);
            }

            float ISpec<float>.ClampedTangent(float prevVal, float prevTime, float curVal, float curTime, float nextVal, float nextTime)
            {
                return ClampedTangent(prevVal, prevTime, curVal, curTime, nextVal, nextTime);
            }

            float ISpec<float>.Zero() => 0f;

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

            Vector3 ISpec<Vector3>.Tangent(Vector3 prev, Vector3 cur, Vector3 next)
            {
                return cur - prev + (next - cur);
            }

            Vector3 ISpec<Vector3>.ClampedTangent(Vector3 prevVal, float prevTime, Vector3 curVal, float curTime, Vector3 nextVal, float nextTime)
            {
                return new Vector3(
                    ClampedTangent(prevVal.X, prevTime, curVal.X, curTime, nextVal.X, nextTime), 
                    ClampedTangent(prevVal.Y, prevTime, curVal.Y, curTime, nextVal.Y, nextTime), 
                    ClampedTangent(prevVal.Z, prevTime, curVal.Z, curTime, nextVal.Z, nextTime));
            }

            Vector3 ISpec<Vector3>.Zero() => Vector3.Zero;

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

            Vector2 ISpec<Vector2>.Tangent(Vector2 prev, Vector2 cur, Vector2 next)
            {
                return cur - prev + (next - cur);
            }

            Vector2 ISpec<Vector2>.ClampedTangent(Vector2 prevVal, float prevTime, Vector2 curVal, float curTime, Vector2 nextVal, float nextTime)
            {
                return new Vector2(
                    ClampedTangent(prevVal.X, prevTime, curVal.X, curTime, nextVal.X, nextTime), 
                    ClampedTangent(prevVal.Y, prevTime, curVal.Y, curTime, nextVal.Y, nextTime));
            }

            Vector2 ISpec<Vector2>.Zero() => Vector2.Zero;

            string ISpec<Vector2>.VectorType() => "InterpCurveVector2D";
            #endregion

            #region GenericSpecializationFramework

            public static readonly Specializations Inst = new();
            private Specializations() { }
        }

        //generic specialization code is based on https://stackoverflow.com/a/29379250
        private static U Lerp<U>(U start, U end, float amount) => Specialization<U>.Inst.Lerp(start, end, amount);
        private static U CubicInterp<U>(U point0, U tan0, U point1, U tan1, float amount) => Specialization<U>.Inst.CubicInterp(point0, tan0, point1, tan1, amount);
        private static U Mul<U>(float multiplier, U val) => Specialization<U>.Inst.Mul(val, multiplier);
        private static U Tangent<U>(U prev, U cur, U next) => Specialization<U>.Inst.Tangent(prev, cur, next);
        private static U ClampedTangent<U>(U prevVal, float prevTime, U curVal, float curTime, U nextVal, float nextTime) => Specialization<U>.Inst.ClampedTangent(prevVal, prevTime, curVal, curTime, nextVal, nextTime);
        private static U Zero<U>() => Specialization<U>.Inst.Zero();
        private static string VectorType<U>() => Specialization<U>.Inst.VectorType();

        private interface ISpec<U>
        {
            U Lerp(U start, U end, float amount);
            U CubicInterp(U point0, U tan0, U point1, U tan1, float amount);
            U Mul(U val, float multiplier);
            U Tangent(U prev, U cur, U next);
            U ClampedTangent(U prevVal, float prevTime, U curVal, float curTime, U nextVal, float nextTime);
            U Zero();
            string VectorType();
        }

        private class Specialization<U> : ISpec<U>
        {
            //When U is specialized, this will be a working implementation, for all other U, it will be the defaults below.
            public static readonly ISpec<U> Inst = Specializations.Inst as ISpec<U> ?? new Specialization<U>();
            U ISpec<U>.Lerp(U start, U end, float amount) => throw new NotSupportedException();
            U ISpec<U>.CubicInterp(U point0, U tan0, U point1, U tan1, float amount) => throw new NotSupportedException();
            U ISpec<U>.Mul(U val, float multiplier) => throw new NotSupportedException();
            U ISpec<U>.Tangent(U prev, U cur, U next) => throw new NotSupportedException();
            U ISpec<U>.ClampedTangent(U prevVal, float prevTime, U curVal, float curTime, U nextVal, float nextTime) => throw new NotSupportedException();
            U ISpec<U>.Zero() => throw new NotSupportedException();
            string ISpec<U>.VectorType() => throw new NotSupportedException();

            #endregion
        }

        private const float TOLERANCE = 0.0001f;

        public static float ClampedTangent(float prevVal, float prevTime, float curVal, float curTime, float nextVal, float nextTime)
        {
            float prevToNextHeightDiff = nextVal - prevVal;
            float prevToCurHeightDiff = curVal - prevVal;
            float curToNextHeightDiff = nextVal - curVal;

            if (prevToCurHeightDiff >= 0.0f && curToNextHeightDiff <= 0.0f ||
                prevToCurHeightDiff <= 0.0f && curToNextHeightDiff >= 0.0f)
            {
                //Either a local max or min, so tangent should be flat
                return 0f;
            }

            const float clampThreshold = 0.333f;
            const float upperClampThreshold = 1.0f - clampThreshold;
            float curHeightAlpha = prevToCurHeightDiff / prevToNextHeightDiff;
            Func<float, float, float> minOrMax = prevToNextHeightDiff > 0f ? MathF.Min : MathF.Max;
            float prevToNextTangent = prevToNextHeightDiff / MathF.Max(TOLERANCE, nextTime - prevTime);

            switch (curHeightAlpha)
            {
                case < clampThreshold:
                    {
                        float prevToCurTangent = prevToCurHeightDiff / MathF.Max(TOLERANCE, curTime - prevTime);
                        float lerpAmount = 1.0f - curHeightAlpha / clampThreshold;
                        float clampedTangent = Lerp(prevToNextTangent, prevToCurTangent, lerpAmount);
                        return minOrMax(prevToNextTangent, clampedTangent);
                    }
                case > upperClampThreshold:
                    {
                        float curToNextTangent = curToNextHeightDiff / MathF.Max(TOLERANCE, nextTime - curTime);
                        float lerpAmount = (curHeightAlpha - upperClampThreshold) / clampThreshold;
                        float clampedTangent = Lerp(prevToNextTangent, curToNextTangent, lerpAmount);
                        return minOrMax(prevToNextTangent, clampedTangent);
                    }
                default:
                    return prevToNextTangent;
            }
        }
    }
}
