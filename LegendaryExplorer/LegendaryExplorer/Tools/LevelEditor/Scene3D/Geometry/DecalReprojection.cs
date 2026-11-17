using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Clips render triangles to an orthographic decal volume, retaining receiver-local vertex attributes.</summary>
internal static class DecalReprojection
{
    private readonly record struct ClipVertex(Vector3 Position, Vector3 Weights);

    public static StaticReceiverData Project(DecalProjection projection, StaticMeshRenderData mesh,
        Matrix4x4 localToWorld, int component, int lightmapUv, bool backfaces, float backfaceAngle, bool noClip)
    {
        if (!Matrix4x4.Invert(localToWorld, out var inverse)) throw new InvalidOperationException("Receiver transform is singular.");
        var normals = Matrix4x4.Transpose(inverse);
        var positions = mesh.PositionVertexBuffer.VertexData;
        var attributes = mesh.VertexBuffer.VertexData;
        if (attributes.Length != positions.Length || mesh.IndexBuffer.Length % 3 != 0)
            throw new InvalidOperationException("Receiver has malformed render geometry.");
        List<DecalVertex> vertices = [];
        List<ushort> indices = [];
        List<ClipVertex> polygon = new(12), scratch = new(12);
        for (int i = 0; i < mesh.IndexBuffer.Length; i += 3)
        {
            int a = mesh.IndexBuffer[i], b = mesh.IndexBuffer[i + 1], c = mesh.IndexBuffer[i + 2];
            if (a >= positions.Length || b >= positions.Length || c >= positions.Length)
                throw new InvalidOperationException("Receiver triangle index is out of range.");
            // UE3's render triangles use the opposite winding to System.Numerics' right-handed cross product.
            Vector3 face = Vector3.TransformNormal(Vector3.Cross(positions[c] - positions[a], positions[b] - positions[a]), normals);
            if (face.LengthSquared() < 1e-12f) continue;
            if (!backfaces && Vector3.Dot(Vector3.Normalize(face), projection.Normal) < backfaceAngle) continue;
            Vector3 wa = Vector3.Transform(positions[a], localToWorld), wb = Vector3.Transform(positions[b], localToWorld), wc = Vector3.Transform(positions[c], localToWorld);
            if (!DecalProjection.Finite(wa) || !DecalProjection.Finite(wb) || !DecalProjection.Finite(wc))
                throw new InvalidOperationException("Receiver contains non-finite positions.");
            polygon.Clear();
            polygon.Add(new(wa, Vector3.UnitX)); polygon.Add(new(wb, Vector3.UnitY)); polygon.Add(new(wc, Vector3.UnitZ));
            for (int plane = 0; plane < 6 && polygon.Count >= 3; plane++)
            {
                scratch.Clear();
                var previous = polygon[^1];
                float before = Distance(previous.Position, plane);
                foreach (var current in polygon)
                {
                    float after = Distance(current.Position, plane);
                    if ((before >= 0) != (after >= 0))
                    {
                        float t = before / (before - after);
                        scratch.Add(new(Vector3.Lerp(previous.Position, current.Position, t), Vector3.Lerp(previous.Weights, current.Weights, t)));
                    }
                    if (after >= 0) scratch.Add(current);
                    previous = current; before = after;
                }
                (polygon, scratch) = (scratch, polygon);
            }
            if (polygon.Count < 3) continue;
            if (noClip)
            {
                polygon.Clear();
                polygon.Add(new(wa, Vector3.UnitX)); polygon.Add(new(wb, Vector3.UnitY)); polygon.Add(new(wc, Vector3.UnitZ));
            }
            if (vertices.Count + polygon.Count > ushort.MaxValue)
                throw new InvalidOperationException("Projection exceeds the receiver's 16-bit vertex limit. Reduce the decal's size or depth.");
            int start = vertices.Count;
            foreach (var vertex in polygon)
            {
                var w = vertex.Weights;
                Vector3 tangent = (Vector3)attributes[a].TangentX * w.X + (Vector3)attributes[b].TangentX * w.Y + (Vector3)attributes[c].TangentX * w.Z;
                Vector4 packedNormal = (Vector4)attributes[a].TangentZ * w.X + (Vector4)attributes[b].TangentZ * w.Y + (Vector4)attributes[c].TangentZ * w.Z;
                Vector3 normal = new(packedNormal.X, packedNormal.Y, packedNormal.Z);
                if (normal.LengthSquared() < 1e-8f || tangent.LengthSquared() < 1e-8f)
                    throw new InvalidOperationException("Receiver has an invalid vertex tangent basis.");
                vertices.Add(new DecalVertex
                {
                    Position = positions[a] * w.X + positions[b] * w.Y + positions[c] * w.Z,
                    TangentX = (PackedNormal)Vector3.Normalize(tangent),
                    TangentZ = (PackedNormal)new Vector4(Vector3.Normalize(normal), packedNormal.W < 0 ? -1 : 1),
                    LightMapCoordinate = UV(a) * w.X + UV(b) * w.Y + UV(c) * w.Z
                });
            }
            for (int v = 1; v < polygon.Count - 1; v++)
            {
                if (Vector3.Cross(polygon[v].Position - polygon[0].Position, polygon[v + 1].Position - polygon[0].Position).LengthSquared() < 1e-12f) continue;
                indices.Add((ushort)start); indices.Add((ushort)(start + v)); indices.Add((ushort)(start + v + 1));
            }
        }
        return new StaticReceiverData
        {
            Component = component, Vertices = vertices.ToArray(), Indices = indices.ToArray(), NumTriangles = (uint)(indices.Count / 3),
            LightMap = new LightMap { LightMapType = ELightMapType.LMT_None }, ShadowMap1D = [], Data = 0, InstanceIndex = 0
        };

        float Distance(Vector3 world, int plane)
        {
            Vector3 delta = world - projection.Location;
            return plane switch
            {
                0 => projection.Width * 0.5f - Vector3.Dot(delta, projection.Tangent),
                1 => projection.Width * 0.5f + Vector3.Dot(delta, projection.Tangent),
                2 => projection.Height * 0.5f - Vector3.Dot(delta, projection.Binormal),
                3 => projection.Height * 0.5f + Vector3.Dot(delta, projection.Binormal),
                4 => -Vector3.Dot(delta, projection.Normal) - projection.NearPlane,
                _ => projection.FarPlane + Vector3.Dot(delta, projection.Normal)
            };
        }
        Vector2 UV(int index)
        {
            if (lightmapUv < 0 || lightmapUv >= mesh.VertexBuffer.NumTexCoords) return Vector2.Zero;
            var vertex = attributes[index];
            return mesh.VertexBuffer.bUseFullPrecisionUVs ? vertex.FullPrecisionUVs[lightmapUv]
                : new Vector2(vertex.HalfPrecisionUVs[lightmapUv].X, vertex.HalfPrecisionUVs[lightmapUv].Y);
        }
    }
}
