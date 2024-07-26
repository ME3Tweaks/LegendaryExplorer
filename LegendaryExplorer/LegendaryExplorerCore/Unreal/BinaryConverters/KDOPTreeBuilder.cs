using LegendaryExplorerCore.Helpers;
using System.Numerics;
using System;
using System.Linq;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static class KDOPTreeBuilder
    {
        class kDopBuildTriangle
        {
            public readonly ushort Vertex1;
            public readonly ushort Vertex2;
            public readonly ushort Vertex3;
            public readonly ushort MaterialIndex;
            public readonly Vector3 Centroid;
            public readonly Vector3 V0;
            public readonly Vector3 V1;
            public readonly Vector3 V2;

            public kDopBuildTriangle(ushort i1, ushort i2, ushort i3, ushort matIndex, Vector3 v0, Vector3 v1, Vector3 v2)
            {
                Vertex1 = i1;
                Vertex2 = i2;
                Vertex3 = i3;
                MaterialIndex = matIndex;
                V0 = v0;
                V1 = v1;
                V2 = v2;
                Centroid = (v0 + v1 + v2) / 3f;
            }
            public kDopBuildTriangle(kDOPCollisionTriangle tri, Vector3 v0, Vector3 v1, Vector3 v2) 
                : this(tri.Vertex1, tri.Vertex2, tri.Vertex3, tri.MaterialIndex, v0, v1, v2){}

            public static implicit operator kDOPCollisionTriangle(kDopBuildTriangle buildTri) =>
                new(buildTri.Vertex1, buildTri.Vertex2, buildTri.Vertex3, buildTri.MaterialIndex);
        }

        public static kDOPTreeCompact ToCompact(kDOPCollisionTriangle[] oldTriangles, Vector3[] vertices)
        {
            var rootBound = new kDOP();
            for (int i = 0; i < 3; i++)
            {
                rootBound.Max[i] = float.MaxValue;
                rootBound.Min[i] = -float.MaxValue;
            }

            if (oldTriangles.IsEmpty())
            {
                return new kDOPTreeCompact
                {
                    RootBound = rootBound,
                    Nodes = [],
                    Triangles = []
                };
            }

            var buildTriangles = new kDopBuildTriangle[oldTriangles.Length];
            for (int i = 0; i < oldTriangles.Length; i++)
            {
                kDOPCollisionTriangle oldTri = oldTriangles[i];
                buildTriangles[i] = new kDopBuildTriangle(oldTri, vertices[oldTri.Vertex1], vertices[oldTri.Vertex2], vertices[oldTri.Vertex3]);
            }

            int numNodes = 0;
            if (buildTriangles.Length > 5)
            {
                numNodes = 1;
                while ((buildTriangles.Length + numNodes - 1) / numNodes > 10)
                {
                    numNodes *= 2;
                }
                numNodes = 2 * numNodes;
            }

            var nodes = new kDOPCompact[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                nodes[i] = new kDOPCompact();
                for (int j = 0; j < 3; j++)
                {
                    nodes[i].Min[j] = 0;
                    nodes[i].Max[j] = 0;
                }
            }

            rootBound.AddTriangles(buildTriangles, 0, buildTriangles.Length);

            if (numNodes > 1 && buildTriangles.Length > 1)
            {
                nodes[0].SplitTriangleList(0, 0, buildTriangles.Length, buildTriangles, rootBound, nodes);
            }

            return new kDOPTreeCompact
            {
                RootBound = rootBound,
                Nodes = nodes,
                Triangles = buildTriangles.Select(tri => (kDOPCollisionTriangle)tri).ToArray()
            };
        }

        static void SplitTriangleList(this kDOPCompact kdop, int idx, int start, int numTris, kDopBuildTriangle[] triangles, kDOP bound, kDOPCompact[] nodes)
        {
            int numRight = numTris / 2;
            int numLeft = numTris - numRight;
            int firstRight = start + numLeft;

            int bestPlane = FindBestPlane(triangles, start, numTris);

            PartialSort(start, start + numTris - 1, firstRight, triangles, PlaneNormals[bestPlane]);

            kDOP leftBound = new kDOP();
            leftBound.AddTriangles(triangles, start, numLeft);
            kDOP rightBound = new kDOP();
            rightBound.AddTriangles(triangles, firstRight, numRight);

            kdop.Compress(bound, leftBound, rightBound);

            int leftIdx = 2 * idx + 1;
            int rightIdx = leftIdx + 1;

            if (leftIdx < nodes.Length - 1)
            {
                nodes[leftIdx].SplitTriangleList(leftIdx, start, numLeft, triangles, leftBound, nodes);
                nodes[rightIdx].SplitTriangleList(rightIdx, firstRight, numRight, triangles, rightBound, nodes);
            }
        }

        static void AddTriangles(this kDOP kdop, kDopBuildTriangle[] triangles, int start, int numTris)
        {
            for (int i = 0; i < 3; ++i)
            {
                kdop.Min[i] = float.MaxValue;
                kdop.Max[i] = float.MinValue;
            }

            for (int i = start; i < start + numTris; i++)
            {
                kdop.AddPoint(triangles[i].V0);
                kdop.AddPoint(triangles[i].V1);
                kdop.AddPoint(triangles[i].V2);
            }
        }

        static readonly Vector3[] PlaneNormals =
        [
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ
        ];

        static void AddPoint(this kDOP kdop, Vector3 point)
        {
            for (int i = 0; i < 3; i++)
            {
                var dotProd = Vector3.Dot(point, PlaneNormals[i]);
                if (dotProd < kdop.Min[i])
                {
                    kdop.Min[i] = dotProd;
                }
                if (dotProd > kdop.Max[i])
                {
                    kdop.Max[i] = dotProd;
                }
            }
        }

        static void Compress(this kDOPCompact kdop, kDOP bound, kDOP left, kDOP right)
        {
            for (int i = 0; i< 3; ++i)
            {
                kdop.Min[i] = CompressAxis(bound.Min[i], bound.Max[i], left.Min[i], right.Min[i]);
                kdop.Max[i] = CompressAxis(bound.Max[i], bound.Min[i], left.Max[i], right.Max[i]);
            }
        }

        static byte CompressAxis(float targetBound, float otherBound, float left, float right)
        {
            float range = otherBound - targetBound;
            if (range == 0.0f)
            {
                return 1;
            }
            float lFrac = (left - targetBound) / range;
            float rFrac = (right - targetBound) / range;
            return (byte)(lFrac >= rFrac
                            ? 128 + Math.Max((int)Math.Floor(127.0f * lFrac) - 1, 0)
                            : 127 - Math.Max((int)Math.Floor(127.0f * rFrac) - 1, 0));
        }

        static int FindBestPlane(kDopBuildTriangle[] triangles, int start, int numTris)
        {
            int bestPlane = -1;
            float bestVariance = 0;

            for (int i = 0; i< 3; ++i)
            {
                float mean = 0;
                float variance = 0;
                for (int j = 0; j < start + numTris; ++j)
                {
                    mean += Vector3.Dot(triangles[j].Centroid, PlaneNormals[i]);
                }
                mean /= numTris;
                for (int j = 0; j<start + numTris; ++j)
                {
                    float dotProd = Vector3.Dot(triangles[j].Centroid, PlaneNormals[i]);
                    variance += (dotProd - mean) * (dotProd - mean);
                }
                variance /= numTris;
                if (variance >= bestVariance)
                {
                    bestPlane = i;
                    bestVariance = variance;
                }
            }
            return bestPlane;
        }

        static void PartialSort(int start, int end, int split, kDopBuildTriangle[] triangles, Vector3 bestPlaneNormal)
        {
            while (start < end)
            {
                int pivot = Partition((start + end) / 2);
                if (pivot<split)
                {
                    end = pivot - 1;
                }
                else
                {
                    start = pivot + 1;
                }
            }

            int Partition(int pivot)
            {
                float pivotDot = Vector3.Dot(triangles[pivot].Centroid, bestPlaneNormal);
                (triangles[end], triangles[pivot]) = (triangles[pivot], triangles[end]);
                int writeIdx = start;

                for (int i = start; i < end; ++i)
                {
                    float iDot = Vector3.Dot(triangles[i].Centroid, bestPlaneNormal);
                    if (iDot <= pivotDot)
                    {
                        (triangles[writeIdx], triangles[i]) = (triangles[i], triangles[writeIdx]);
                        writeIdx++;
                    }
                }
                (triangles[writeIdx], triangles[end]) = (triangles[end], triangles[writeIdx]);
                return writeIdx;
            }
        }
    }
}