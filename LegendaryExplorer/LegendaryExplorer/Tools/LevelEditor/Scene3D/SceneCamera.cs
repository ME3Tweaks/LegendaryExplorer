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
    public bool IsOrthographic = false;
    public float OrthoWidth = 5000f; // world units visible horizontally in ortho mode

    // Saved perspective state for toggling back from ortho
    private Vector3 savedPerspectivePosition;
    private float savedPitch, savedYaw, savedFocusDepth;
    private bool hasSavedPerspectiveState;

    public void SavePerspectiveState()
    {
        savedPerspectivePosition = Position;
        savedPitch = Pitch;
        savedYaw = Yaw;
        savedFocusDepth = FocusDepth;
        hasSavedPerspectiveState = true;
    }

    public void RestorePerspectiveState()
    {
        if (hasSavedPerspectiveState)
        {
            position = savedPerspectivePosition;
            pitch = savedPitch;
            yaw = savedYaw;
            FocusDepth = savedFocusDepth;
            CalcViewMatrix();
            hasSavedPerspectiveState = false;
        }
    }
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

    /// <summary>
    /// The world-space position the scene is viewed from. In orbit mode, <see cref="Position"/> is the point being orbited, not the camera's location.
    /// </summary>
    public Vector3 EyePosition => FirstPerson || IsOrthographic ? Position : Position - CameraForward * FocusDepth;

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
            if (IsOrthographic)
            {
                return Matrix4x4.CreateLookToLeftHanded(Position, -Vector3.UnitZ, Vector3.UnitY);
            }
            if (FirstPerson)
            {
                return firstPersonViewMatrix;
            }
            return firstPersonViewMatrix * Matrix4x4.CreateTranslation(0, 0, FocusDepth);
        }
    }

    private void CalcViewMatrix()
    {
        firstPersonViewMatrix = Matrix4x4.CreateLookToLeftHanded(Position, CameraForward, Vector3.UnitZ);
    }

    /// <summary>
    /// Left-handed projection with reversed depth: the near plane maps to a depth of 1, and the far plane to 0.
    /// Combined with a floating-point depth buffer, this keeps precision roughly even over the whole range, which prevents z-fighting in large levels.
    /// Depth tests must use Greater instead of Less, and the depth buffer must be cleared to 0.
    /// </summary>
    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            float depthRange = ZFar - ZNear;
            if (IsOrthographic)
            {
                return new Matrix4x4(
                    2f / OrthoWidth, 0, 0, 0,
                    0, 2f / (OrthoWidth / aspect), 0, 0,
                    0, 0, -1f / depthRange, 0,
                    0, 0, ZFar / depthRange, 1);
            }
            float yScale = 1f / MathF.Tan(FOV * 0.5f);
            return new Matrix4x4(
                yScale / aspect, 0, 0, 0,
                0, yScale, 0, 0,
                0, 0, -ZNear / depthRange, 1,
                0, 0, ZNear * ZFar / depthRange, 0);
        }
    }


    public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;
}
