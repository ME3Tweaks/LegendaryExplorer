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

            (float x, float y, float z) = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
            (float scaleX, float scaleY, float scaleZ) = drawScale * (drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : Vector3.One);
            (float pivotX, float pivotY, float pivotZ) = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
            (int uuPitch, int uuYaw, int uuRoll) = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);
            double pitch = uuPitch.ToRadians();
            double yaw = uuYaw.ToRadians();
            double roll = uuRoll.ToRadians();

            float sp = (float)Math.Sin(pitch);
            float sy = (float)Math.Sin(yaw);
            float sr = (float)Math.Sin(roll);
            float cp = (float)Math.Cos(pitch);
            float cy = (float)Math.Cos(yaw);
            float cr = (float)Math.Cos(roll);

            return new Matrix(M11: cp * cy * scaleX, 
                              M12: cp * scaleX * sy, 
                              M13: scaleX * sp,
                              M14: 0f,
                
                              M21: scaleY * (cy * sp * sr - cr * sy),
                              M22: scaleY * (cr * cy + sp * sr * sy),
                              M23: -cp * scaleY * sr,
                              M24: 0f,

                              M31: -scaleZ * (cr * cy * sp + sr * sy),
                              M32: scaleZ * (cy * sr - cr * sp * sy),
                              M33: cp * cr * scaleZ,
                              M34: 0f,

                              M41: x - cp * cy * scaleX * pivotX + cr * cy * scaleZ * pivotZ * sp - cy * scaleY * pivotY * sp * sr + cr * scaleY * pivotY * sy + scaleZ * pivotZ * sr * sy,
                              M42: y - (cr * cy * scaleY * pivotY + cy * scaleZ * pivotZ * sr + cp * scaleX * pivotX * sy - cr * scaleZ * pivotZ * sp * sy + scaleY * pivotY * sp * sr * sy),
                              M43: z - (cp * cr * scaleZ * pivotZ + scaleX * pivotX * sp - cp * scaleY * pivotY * sr),
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

        //todo: switch to using one in Extesnsions.cs after merge with AnimViewer branch
        private static double ToRadians(this int unrealRotationUnits) => unrealRotationUnits * 360.0 / 65536.0 * Math.PI / 180.0;

        public static Matrix InverseRotation(Rotator rot)
        {
            double pitch = rot.Pitch.ToRadians();
            double yaw = rot.Yaw.ToRadians();
            double roll = rot.Roll.ToRadians();

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