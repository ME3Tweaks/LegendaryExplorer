using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// Finds where on a mesh a lightmap UV lies: the triangle containing it in lightmap-UV space (or, for padding texels
/// and chart gaps, the nearest one), with the interpolated LE3 tangent frame and world position. The LE3 encoders
/// need the frame to take world-space lighting into each texel's tangent space.
/// </summary>
public sealed class UvLocator
{
    const int GridSize = 64;
    readonly ComponentGeometry geometry;
    readonly List<int>[] cells = new List<int>[GridSize * GridSize];
    readonly Vector2 min, max;

    public readonly record struct Hit(int Triangle, Vector3 Bary, bool Inside, TangentFrame Frame, Vector3 Position);

    public UvLocator(ComponentGeometry g)
    {
        geometry = g;
        min = new Vector2(float.MaxValue); max = new Vector2(float.MinValue);
        foreach (var uv in g.LightMapUVs) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
        if (min.X > max.X) { min = Vector2.Zero; max = Vector2.One; }
        max = Vector2.Max(max, min + new Vector2(1e-6f));
        for (int t = 0; t < g.TriangleCount; t++)
        {
            var (a, b, c) = g.Triangle(t);
            var (lo, hi) = (Vector2.Min(g.LightMapUVs[a], Vector2.Min(g.LightMapUVs[b], g.LightMapUVs[c])), Vector2.Max(g.LightMapUVs[a], Vector2.Max(g.LightMapUVs[b], g.LightMapUVs[c])));
            var (x0, y0) = Cell(lo); var (x1, y1) = Cell(hi);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                (cells[y * GridSize + x] ??= []).Add(t);
        }
    }

    (int, int) Cell(Vector2 uv)
    {
        var f = (uv - min) / (max - min);
        return (Math.Clamp((int)(f.X * GridSize), 0, GridSize - 1), Math.Clamp((int)(f.Y * GridSize), 0, GridSize - 1));
    }

    public Hit Locate(Vector2 uv)
    {
        var (cx, cy) = Cell(uv);
        // The containing triangle if any; otherwise the nearest, searching outward ring by ring.
        int bestTri = -1; Vector3 bestBary = default; float bestDist = float.MaxValue;
        int firstHitRing = -1;
        for (int ring = 0; ring < GridSize; ring++)
        {
            for (int y = cy - ring; y <= cy + ring; y++)
            for (int x = cx - ring; x <= cx + ring; x++)
            {
                if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != ring || x < 0 || y < 0 || x >= GridSize || y >= GridSize) continue;
                var list = cells[y * GridSize + x];
                if (list == null) continue;
                foreach (int t in list)
                {
                    var (a, b, c) = geometry.Triangle(t);
                    var (bary, dist) = Closest(uv, geometry.LightMapUVs[a], geometry.LightMapUVs[b], geometry.LightMapUVs[c]);
                    if (dist < bestDist) { bestDist = dist; bestTri = t; bestBary = bary; }
                }
            }
            if (bestDist == 0) break;
            // A nearer triangle can still sit one ring further out than the first hit; stop after that ring.
            if (bestTri >= 0 && firstHitRing < 0) firstHitRing = ring;
            if (firstHitRing >= 0 && ring > firstHitRing) break;
        }
        if (bestTri < 0) return new Hit(-1, default, false, new TangentFrame(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), default);

        var (ia, ib, ic) = geometry.Triangle(bestTri);
        Vector3 L(Vector3[] v) => v[ia] * bestBary.X + v[ib] * bestBary.Y + v[ic] * bestBary.Z;
        var frame = new TangentFrame(Norm(L(geometry.TangentX)), Norm(L(geometry.TangentY)), Norm(L(geometry.TangentZ)));
        return new Hit(bestTri, bestBary, bestDist == 0, frame, L(geometry.Positions));
    }

    static Vector3 Norm(Vector3 v) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : Vector3.UnitZ;

    /// <summary>Barycentric weights of the closest point of a 2D triangle to p, and the distance (0 when inside).</summary>
    static (Vector3 Bary, float Distance) Closest(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        if (MathF.Abs(area) > 1e-12f)
        {
            float w0 = ((b.X - p.X) * (c.Y - p.Y) - (c.X - p.X) * (b.Y - p.Y)) / area;
            float w1 = ((c.X - p.X) * (a.Y - p.Y) - (a.X - p.X) * (c.Y - p.Y)) / area;
            float w2 = 1 - w0 - w1;
            if (w0 >= -1e-6f && w1 >= -1e-6f && w2 >= -1e-6f) return (new Vector3(w0, w1, w2), 0);
        }
        // Outside (or degenerate): nearest point on the three edges.
        var best = (Bary: new Vector3(1, 0, 0), Distance: Vector2.Distance(p, a));
        void Edge(Vector2 e0, Vector2 e1, int i0, int i1)
        {
            var d = e1 - e0;
            float t = d.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - e0, d) / d.LengthSquared(), 0, 1) : 0;
            float dist = Vector2.Distance(p, e0 + t * d);
            if (dist < best.Distance)
            {
                var w = Vector3.Zero;
                w = i0 == 0 ? w with { X = 1 - t } : i0 == 1 ? w with { Y = 1 - t } : w with { Z = 1 - t };
                w = i1 == 0 ? w with { X = w.X + t } : i1 == 1 ? w with { Y = w.Y + t } : w with { Z = w.Z + t };
                best = (w, dist);
            }
        }
        Edge(a, b, 0, 1); Edge(b, c, 1, 2); Edge(c, a, 2, 0);
        return best;
    }
}
