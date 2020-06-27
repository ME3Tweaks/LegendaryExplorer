using System;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;
using SharpDX;

namespace ME3Explorer.Unreal
{
    public static class ActorUtils
    {
        //actor must be an Actor
        public static Matrix GetLocalToWorld(ExportEntry actor)
        {
            PropertyCollection props = actor.GetProperties();
            var rotationProp = props.GetProp<StructProperty>("Rotation");
            var locationsProp = props.GetProp<StructProperty>("location");
            var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
            var prePivotProp = props.GetProp<StructProperty>("PrePivot");

            float drawScale = props.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;

            Vector3 location = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
            Vector3 scale = drawScale * (drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : Vector3.One);
            Vector3 pivot = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
            Rotator rotator = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);
            return ComposeLocalToWorld(location, rotator, scale, pivot);
        }

        public static Matrix ComposeLocalToWorld(Vector3 location, Rotator rotation, Vector3 scale, Vector3 pivot = default)
        {
            double pitch = rotation.Pitch.UnrealRotationUnitsToRadians();
            double yaw = rotation.Yaw.UnrealRotationUnitsToRadians();
            double roll = rotation.Roll.UnrealRotationUnitsToRadians();

            float sp = (float)Math.Sin(pitch);
            float sy = (float)Math.Sin(yaw);
            float sr = (float)Math.Sin(roll);
            float cp = (float)Math.Cos(pitch);
            float cy = (float)Math.Cos(yaw);
            float cr = (float)Math.Cos(roll);

            (float x, float y, float z) = location;
            (float sX, float sY, float sZ) = scale;
            (float pX, float pY, float pZ) = pivot;
            return new Matrix(M11: cp * cy * sX,
                              M12: cp * sX * sy,
                              M13: sX * sp,
                              M14: 0f,
                              M21: sY * (cy * sp * sr - cr * sy),
                              M22: sY * (cr * cy + sp * sr * sy),
                              M23: -cp * sY * sr,
                              M24: 0f,
                              M31: -sZ * (cr * cy * sp + sr * sy),
                              M32: sZ * (cy * sr - cr * sp * sy),
                              M33: cp * cr * sZ,
                              M34: 0f,
                              M41: x - cp * cy * sX * pX + cr * cy * sZ * pZ * sp - cy * sY * pY * sp * sr + cr * sY * pY * sy + sZ * pZ * sr * sy,
                              M42: y - (cr * cy * sY * pY + cy * sZ * pZ * sr + cp * sX * pX * sy - cr * sZ * pZ * sp * sy + sY * pY * sp * sr * sy),
                              M43: z - (cp * cr * sZ * pZ + sX * pX * sp - cp * sY * pY * sr),
                              M44: 1f);
        }

        public static Matrix GetWorldToLocal(ExportEntry actor)
        {
            PropertyCollection props = actor.GetProperties();
            var rotationProp = props.GetProp<StructProperty>("Rotation");
            var locationsProp = props.GetProp<StructProperty>("location");
            var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
            var prePivotProp = props.GetProp<StructProperty>("PrePivot");

            float drawScale = props.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;

            Vector3 location = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
            (float scaleX, float scaleY, float scaleZ) = drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : Vector3.One;
            Vector3 prePivot = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
            Rotator rotation = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);

            return Matrix.Translation(-location) *
                   InverseRotation(rotation) * 
                   Matrix.Scaling(new Vector3(1f / scaleX, 1f / scaleY, 1f / scaleZ) / drawScale) * 
                   Matrix.Translation(prePivot);
        }

        public static Vector3 TransformNormal(this Matrix m, Vector3 v)
        {
            (float x, float y, float z) = v;
            return new Vector3(x * m.M11 + y * m.M21 + z * m.M31,
                               x * m.M12 + y * m.M22 + z * m.M32,
                               x * m.M13 + y * m.M23 + z * m.M33);
        }

        public static Matrix InverseRotation(Rotator rot)
        {
            double pitch = rot.Pitch.UnrealRotationUnitsToRadians();
            double yaw = rot.Yaw.UnrealRotationUnitsToRadians();
            double roll = rot.Roll.UnrealRotationUnitsToRadians();

            float sp = (float)Math.Sin(pitch);
            float sy = (float)Math.Sin(yaw);
            float sr = (float)Math.Sin(roll);
            float cp = (float)Math.Cos(pitch);
            float cy = (float)Math.Cos(yaw);
            float cr = (float)Math.Cos(roll);

            return new Matrix(+cy, -sy, 0f, 0f,
                              +sy, +cy, 0f, 0f,
                              0f, 0f, 1f, 0f,
                              0f, 0f, 0f, 1f) *
                   new Matrix(+cp, -0f, -sp, 0f,
                              0f, 1f, 0f, 0f,
                              +sp, 0f, +cp, 0f,
                              0f, 0f, 0f, 1f) *
                   new Matrix(1f, -0f, 0f, 0f,
                              0f, +cr, +sr, 0f,
                              0f, -sr, -cr, 0f,
                              0f, 0f, 0f, 1f);
        }
    }
}