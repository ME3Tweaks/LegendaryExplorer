using System;
using System.Numerics;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    public class SceneCamera
    {
        public Vector3 Position = Vector3.Zero;
        public float FocusDepth = 0; // Depth of rotation center for 3rd person mode.
        public float Pitch = 0;
        public float Yaw = 0;
        // Ignore Roll for now. Who would ever roll their preview camera?
        public float aspect = 1.0f;
        public float FOV = MathF.PI / 3; // 60 degrees.
        public float ZNear = 0.1f;
        public float ZFar = 40000;
        public bool FirstPerson = false;
        public Vector3 CameraUp => Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateRotationX(Pitch) * Matrix4x4.CreateRotationY(Yaw)) * new Vector3(1, 1, -1);

        public Vector3 CameraLeft => Vector3.Transform(-Vector3.UnitX, Matrix4x4.CreateRotationY(-Yaw));

        public Vector3 CameraForward => Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationX(-Pitch) * Matrix4x4.CreateRotationY(-Yaw));

        public SceneCamera()
        {
        }

        public SceneCamera(Vector3 Position)
        {
            this.Position = Position;
        }

        public Matrix4x4 ViewMatrix
        {
            get
            {
                if (FirstPerson)
                {
                    return Matrix4x4.CreateTranslation(-Position) * Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateRotationX(Pitch);
                }
                else
                {
                    return Matrix4x4.CreateTranslation(-Position) * Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateRotationX(Pitch) * Matrix4x4.CreateTranslation(0, 0, FocusDepth);
                }
            }
        }

        public Matrix4x4 ProjectionMatrix
        {
            get
            {
                // This creates a left handed FoV matrix - code ported from SharpDX

                var matrix = new Matrix4x4();
                float yScale = (float)(1.0f / Math.Tan(FOV * 0.5f));
                float q = ZFar / (ZFar - ZNear);

                matrix.M11 = yScale / aspect;
                matrix.M22 = yScale;
                matrix.M33 = q;
                matrix.M34 = 1.0f;
                matrix.M43 = -q * ZNear;
                return matrix;
            }
        }
    }
}
