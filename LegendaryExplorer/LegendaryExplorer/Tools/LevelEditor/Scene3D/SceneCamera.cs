using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public class SceneCamera
{
    private Vector3 position = Vector3.Zero;
    private float pitch = 0;
    private float yaw = 0;
    // Ignore Roll for now. Who would ever roll their preview camera?
    public Vector3 Position
    {
        get => position;
        set
        {
            position = value;
            CalcViewMatrix();
        }
    }
    public float Pitch
    {
        get => pitch;
        set
        {
            pitch = value;
            CalcViewMatrix();
        }
    }
    public float Yaw
    {
        get => yaw;
        set
        {
            yaw = value.Wrap(0, MathF.PI * 2);
            CalcViewMatrix();
        }
    }

    public float FocusDepth = 0; // Depth of rotation center for 3rd person mode.
    public float aspect = 1.0f;
    public float FOV = MathF.PI / 3; // 60 degrees.
    public float ZNear = 0.1f;
    public float ZFar = 100_000;
    public bool FirstPerson = false;
    public Vector3 CameraUp
    {
        get
        {
            float sr = MathF.Sin(0/*Roll*/);
            float sp = MathF.Sin(Pitch);
            float sy = MathF.Sin(Yaw);
            float cr = MathF.Cos(0/*Roll*/);
            float cp = MathF.Cos(Pitch);
            float cy = MathF.Cos(Yaw);

            return new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp).Normal();

            //return Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateRotationX(Pitch) * Matrix4x4.CreateRotationY(Yaw)) * new Vector3(1, 1, -1);
        }
    }

    public Vector3 CameraRight
    {
        get
        {
            float sr = MathF.Sin(0/*Roll*/);
            float sp = MathF.Sin(Pitch);
            float sy = MathF.Sin(Yaw);
            float cr = MathF.Cos(0/*Roll*/);
            float cp = MathF.Cos(Pitch);
            float cy = MathF.Cos(Yaw);

            return new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp).Normal();

            //return Vector3.Transform(Vector3.UnitX, Matrix4x4.CreateRotationZ(Yaw));
        }
    }

    public Vector3 CameraForward
    {
        get
        {
            var cp = MathF.Cos(Pitch);
            var cy = MathF.Cos(Yaw);
            var sp = MathF.Sin(Pitch);
            var sy = MathF.Sin(Yaw);
            return new Vector3(cp * cy, cp * sy, sp).Normal();
        }
    }

    public SceneCamera()
    {
        CalcViewMatrix();
    }

    public SceneCamera(Vector3 Position)
    {
        this.Position = Position;
        CalcViewMatrix();
    }

    public void OrientTowards(Vector3 point)
    {
        Vector3 dirVec = point - Position;
        if (dirVec == Vector3.Zero)
        {
            return;
        }
        dirVec = dirVec.Normal();
        float x = dirVec.X;
        float y = dirVec.Y;
        float z = dirVec.Z;
        Pitch = MathF.Atan2(z, MathF.Sqrt(MathF.Pow(x, 2) + MathF.Pow(y, 2)));
        Yaw = MathF.Atan2(y, x);
    }

    private Matrix4x4 firstPersonViewMatrix;
    public Matrix4x4 ViewMatrix
    {
        get
        {
            if (FirstPerson)
            {
                return firstPersonViewMatrix;
            }
            else
            {
                return firstPersonViewMatrix * Matrix4x4.CreateTranslation(0, 0, FocusDepth);
            }
        }
    }

    private void CalcViewMatrix()
    {
        firstPersonViewMatrix = Matrix4x4.CreateLookToLeftHanded(Position, CameraForward, Vector3.UnitZ);
    }

    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            return Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(FOV, aspect, ZNear, ZFar);
        }
    }


    public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;
}
