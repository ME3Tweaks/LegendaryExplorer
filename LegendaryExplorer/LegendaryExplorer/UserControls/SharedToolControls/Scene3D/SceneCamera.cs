using System;
using SharpDX;

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
        public float FOV = (float)Math.PI / 3; // 60 degrees.
        public float ZNear = 0.1f;
        public float ZFar = 40000;
        public bool FirstPerson = false;
        public Vector3 CameraUp => (Vector3) Vector3.Transform(Vector3.UnitY, Matrix.RotationX(Pitch) * Matrix.RotationY(Yaw)) * new Vector3(1, 1, -1);

        public Vector3 CameraLeft => (Vector3) Vector3.Transform(-Vector3.UnitX, Matrix.RotationY(-Yaw));

        public Vector3 CameraForward => (Vector3)Vector3.Transform(Vector3.UnitZ, Matrix.RotationX(-Pitch) * Matrix.RotationY(-Yaw));

        public SceneCamera()
        {

        }

        public SceneCamera(Vector3 Position)
        {
            this.Position = Position;
        }

        public Matrix ViewMatrix
        {
            get
            {
                if (FirstPerson)
                {
                    return Matrix.Translation(-Position) * Matrix.RotationY(Yaw) * Matrix.RotationX(Pitch);
                }
                else
                {
                    return Matrix.Translation(-Position) * Matrix.RotationY(Yaw) * Matrix.RotationX(Pitch) * Matrix.Translation(0, 0, FocusDepth);
                }
            }
        }

        public Matrix ProjectionMatrix => Matrix.PerspectiveFovLH(FOV, aspect, ZNear, ZFar);
    }
}
