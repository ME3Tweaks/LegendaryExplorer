using LegendaryExplorer.Misc;
using System;
using System.Numerics;

namespace LegendaryExplorer.UserControls.SharedToolControls;

/// <summary>
/// Observable DataGrid row representing one skeleton bone's component-space transform.
/// Position is in Unreal units; rotation is Pitch/Yaw/Roll in degrees.
/// </summary>
public class BoneTransformRow : NotifyPropertyChangedBase
{
    public string Name { get; }

    private float _posX;
    public float PosX { get => _posX; set => SetProperty(ref _posX, value); }

    private float _posY;
    public float PosY { get => _posY; set => SetProperty(ref _posY, value); }

    private float _posZ;
    public float PosZ { get => _posZ; set => SetProperty(ref _posZ, value); }

    private float _rotX;
    public float RotX { get => _rotX; set => SetProperty(ref _rotX, value); }

    private float _rotY;
    public float RotY { get => _rotY; set => SetProperty(ref _rotY, value); }

    private float _rotZ;
    public float RotZ { get => _rotZ; set => SetProperty(ref _rotZ, value); }

    public BoneTransformRow(string name)
    {
        Name = name;
    }

    public void Update(Matrix4x4 m)
    {
        // Extract translation
        PosX = m.M41; PosY = m.M42; PosZ = m.M43;
        // Euler from rotation matrix (ZYX / Pitch-Yaw-Roll convention)
        float pitch = MathF.Atan2(m.M32, m.M33) * (180f / MathF.PI);
        float yaw   = MathF.Atan2(-m.M31, MathF.Sqrt(m.M32 * m.M32 + m.M33 * m.M33)) * (180f / MathF.PI);
        float roll  = MathF.Atan2(m.M21, m.M11) * (180f / MathF.PI);
        RotX = pitch; RotY = yaw; RotZ = roll;
    }
}
