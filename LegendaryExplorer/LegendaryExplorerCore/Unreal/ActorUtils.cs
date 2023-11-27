using System;
using System.Numerics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    public static class ActorUtils
    {
        //actor must be an Actor
        public static Matrix4x4 GetLocalToWorld(ExportEntry actor)
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

        //inverse of this method is Matrix4x4::UnrealDecompose
        public static Matrix4x4 ComposeLocalToWorld(Vector3 location, Rotator rotation, Vector3 scale, Vector3 pivot = default)
        {
            float pitch = rotation.Pitch.UnrealRotationUnitsToRadians();
            float yaw = rotation.Yaw.UnrealRotationUnitsToRadians();
            float roll = rotation.Roll.UnrealRotationUnitsToRadians();

            float sp = MathF.Sin(pitch);
            float sy = MathF.Sin(yaw);
            float sr = MathF.Sin(roll);
            float cp = MathF.Cos(pitch);
            float cy = MathF.Cos(yaw);
            float cr = MathF.Cos(roll);

            (float x, float y, float z) = (location.X, location.Y, location.Z);
            (float sX, float sY, float sZ) = (scale.X, scale.Y, scale.Z);
            (float pX, float pY, float pZ) = (pivot.X, pivot.Y, pivot.Z);
            return new Matrix4x4(m11: cp * cy * sX,
                              m12: cp * sX * sy,
                              m13: sX * sp,
                              m14: 0f,
                              m21: sY * (cy * sp * sr - cr * sy),
                              m22: sY * (cr * cy + sp * sr * sy),
                              m23: -cp * sY * sr,
                              m24: 0f,
                              m31: -sZ * (cr * cy * sp + sr * sy),
                              m32: sZ * (cy * sr - cr * sp * sy),
                              m33: cp * cr * sZ,
                              m34: 0f,
                              m41: x - cp * cy * sX * pX + cr * cy * sZ * pZ * sp - cy * sY * pY * sp * sr + cr * sY * pY * sy + sZ * pZ * sr * sy,
                              m42: y - (cr * cy * sY * pY + cy * sZ * pZ * sr + cp * sX * pX * sy - cr * sZ * pZ * sp * sy + sY * pY * sp * sr * sy),
                              m43: z - (cp * cr * sZ * pZ + sX * pX * sp - cp * sY * pY * sr),
                              m44: 1f);
        }

        public static Matrix4x4 GetWorldToLocal(ExportEntry actor)
        {
            PropertyCollection props = actor.GetProperties();
            var rotationProp = props.GetProp<StructProperty>("Rotation");
            var locationsProp = props.GetProp<StructProperty>("location");
            var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
            var prePivotProp = props.GetProp<StructProperty>("PrePivot");

            float drawScale = props.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;

            Vector3 location = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
            var scaleVector = drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : Vector3.One;
            (float scaleX, float scaleY, float scaleZ) = (scaleVector.X, scaleVector.Y, scaleVector.Z);
            Vector3 prePivot = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
            Rotator rotation = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);

            return Matrix4x4.CreateTranslation(-location) *
                   InverseRotation(rotation) * 
                   Matrix4x4.CreateScale(new Vector3(1f / scaleX, 1f / scaleY, 1f / scaleZ) / drawScale) * 
                   Matrix4x4.CreateTranslation(prePivot);
        }

        public static Vector3 TransformNormal(this Matrix4x4 m, Vector3 v)
        {
            (float x, float y, float z) = (v.X, v.Y, v.Z);
            return new Vector3(x * m.M11 + y * m.M21 + z * m.M31,
                               x * m.M12 + y * m.M22 + z * m.M32,
                               x * m.M13 + y * m.M23 + z * m.M33);
        }

        public static Matrix4x4 InverseRotation(Rotator rot)
        {
            float pitch = rot.Pitch.UnrealRotationUnitsToRadians();
            float yaw = rot.Yaw.UnrealRotationUnitsToRadians();
            float roll = rot.Roll.UnrealRotationUnitsToRadians();

            float sp = MathF.Sin(pitch);
            float sy = MathF.Sin(yaw);
            float sr = MathF.Sin(roll);
            float cp = MathF.Cos(pitch);
            float cy = MathF.Cos(yaw);
            float cr = MathF.Cos(roll);

            return new Matrix4x4(+cy, -sy, 0f, 0f,
                              +sy, +cy, 0f, 0f,
                              0f, 0f, 1f, 0f,
                              0f, 0f, 0f, 1f) *
                   new Matrix4x4(+cp, -0f, -sp, 0f,
                              0f, 1f, 0f, 0f,
                              +sp, 0f, +cp, 0f,
                              0f, 0f, 0f, 1f) *
                   new Matrix4x4(1f, -0f, 0f, 0f,
                              0f, +cr, +sr, 0f,
                              0f, -sr, -cr, 0f,
                              0f, 0f, 0f, 1f);
        }

        public static Vector3 GetActorTotalScale(PropertyCollection props)
        {
            var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
            return (props.GetProp<FloatProperty>("DrawScale") ?? 1f) * (drawScale3DProp is null ? Vector3.One : CommonStructs.GetVector3(drawScale3DProp));
        }
    }
}