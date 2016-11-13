using System;
using SharpDX;

namespace ME3Explorer.Scene3D
{
    public class SceneCamera
    {
        public Vector3 Position = Vector3.Zero;
        public float FocusDepth = 0; // Depth of rotation center for 3rd person mode. Set to 0 for first person mode.
        public float Pitch = 0;
        public float Yaw = 0;
        // Ignore Roll for now. Who would ever roll their preview camera?
        public float aspect = 1.0f;
        public float FOV = (float)Math.PI / 3; // 60 degrees.
        public float ZNear = 0.1f;
        public float ZFar = 40000;
        public Vector3 CameraUp
        {
            get
            {
                return (Vector3) Vector3.Transform(Vector3.UnitY, Matrix.RotationX(Pitch) * Matrix.RotationY(Yaw)) * new Vector3(1, 1, -1); 
            }
        }
        public Vector3 CameraLeft

        {
            get
            {
                return (Vector3) Vector3.Transform(-Vector3.UnitX, Matrix.RotationY(-Yaw));
            }
        }

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
                return Matrix.Translation(-Position) * Matrix.RotationY(Yaw) * Matrix.RotationX(Pitch) * Matrix.Translation(0, 0, FocusDepth);
            }
        }

        public Matrix ProjectionMatrix
        {
            get
            {
                return Matrix.PerspectiveFovLH(FOV, aspect, ZNear, ZFar);
            }
        }
    }
}
