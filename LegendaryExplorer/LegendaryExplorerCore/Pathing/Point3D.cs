using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Pathing
{
    public class Point3D
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Point3D()
        {

        }

        public Point3D(float X, float Y, float Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public double GetDistanceToOtherPoint(Point3D other)
        {
            double deltaX = X - other.X;
            double deltaY = Y - other.Y;
            double deltaZ = Z - other.Z;

            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
        }

        public Vector3 GetDirectionToOtherPoint(Point3D destPoint, double distance)
        {
            float dirX = (float)((destPoint.X - X) / distance);
            float dirY = (float)((destPoint.Y - Y) / distance);
            float dirZ = (float)((destPoint.Z - Z) / distance);
            return new Vector3(dirX, dirY, dirZ);
        }

        public Point3D GetDelta(Point3D other)
        {
            float deltaX = X - other.X;
            float deltaY = Y - other.Y;
            float deltaZ = Z - other.Z;
            return new Point3D(deltaX, deltaY, deltaZ);
        }

        public override string ToString()
        {
            return $"{X},{Y},{Z}";
        }

        public Point3D ApplyDelta(Point3D other)
        {
            float deltaX = X + other.X;
            float deltaY = Y + other.Y;
            float deltaZ = Z + other.Z;
            return new Point3D(deltaX, deltaY, deltaZ);
        }

        public static implicit operator Point3D(Vector3 vec) => new Point3D(vec.X, vec.Y, vec.Z);
    }
}
