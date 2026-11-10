using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>A TerrainComponent's part of the height field, in quads of the terrain's vertex grid.</summary>
/// <param name="Step">Quads per patch side (TrueSectionSize / SectionSize, MaxTesselationLevel in practice).</param>
public sealed record TerrainSection(ExportEntry Component, PropertyCollection Properties, int BaseX, int BaseY, int SizeX, int SizeY,
    int TrueSizeX, int TrueSizeY)
{
    public int Step => SizeX > 0 ? Math.Max(1, TrueSizeX / SizeX) : 1;

    /// <summary>The component's frame: the terrain's, moved to the section's base (UTerrainComponent::SetParentToWorld)</summary>
    public Matrix4x4 LocalToWorld(Matrix4x4 terrainLocalToWorld) => Matrix4x4.CreateTranslation(BaseX, BaseY, 0) * terrainLocalToWorld;

    /// <summary>
    /// The quads of padding on each side of a section's light-map: TERRAIN_PATCH_EXPAND_SCALAR (1) times DXT1's 4-texel block, in quads at the
    /// terrain's StaticLightingResolution (texels per quad)
    /// </summary>
    public static int LightMapExpandQuads(int lightingResolution) => Math.Max(1, 4 / lightingResolution);

    /// <summary>
    /// FTerrainVertexFactoryShaderParameters' TerrainLightmapCoordinateScaleBias: from section-local XY to UVs in the section's padded light-map, as
    /// (scale X, scale Y, bias Y, bias X)
    /// </summary>
    public Vector4 LightMapCoordinateScaleBias(int lightingResolution)
    {
        int expand = LightMapExpandQuads(lightingResolution);
        float scaleX = lightingResolution / ((TrueSizeX + 2f * expand) * lightingResolution + 1);
        float scaleY = lightingResolution / ((TrueSizeY + 2f * expand) * lightingResolution + 1);
        return new Vector4(scaleX, scaleY, expand * scaleY, expand * scaleX);
    }
}

/// <summary>
/// A Terrain actor's height field, shared by the bake and the viewport so both place it identically. Terrain-local space has one unit per
/// vertex in X and Y, and <see cref="ZScale"/> per height unit in Z, heights centred on 32768; the actor's transform scales it into the world.
/// </summary>
public sealed class TerrainHeightField
{
    /// <summary>TERRAIN_ZSCALE</summary>
    public const float ZScale = 1f / 128;

    public ExportEntry Export { get; }
    public PropertyCollection Properties { get; }
    public Terrain Binary { get; }
    public int NumVerticesX { get; }
    public int NumVerticesY { get; }
    public int NumPatchesX { get; }
    public int NumPatchesY { get; }
    public int MaxTesselationLevel { get; }

    public TerrainHeightField(ExportEntry export, PropertyCollection properties, Terrain binary = null)
    {
        Export = export;
        Properties = properties;
        Binary = binary ?? ObjectBinary.From<Terrain>(export);
        int Int(string name, int fallback) => properties.GetProp<IntProperty>(name)?.Value ?? fallback;
        NumVerticesX = Int("NumVerticesX", 0);
        NumVerticesY = Int("NumVerticesY", 0);
        NumPatchesX = Int("NumPatchesX", 0);
        NumPatchesY = Int("NumPatchesY", 0);
        MaxTesselationLevel = Int("MaxTesselationLevel", 4);
    }

    /// <summary>Whether there's a height and info flags for each vertex; neither the bake nor the viewport can use a height field that doesn't</summary>
    public bool IsValid => NumVerticesX > 1 && NumVerticesY > 1 && Binary.Heights.Length == NumVerticesX * NumVerticesY
                           && Binary.InfoData.Length == NumVerticesX * NumVerticesY;

    private int Index(int x, int y) => Math.Clamp(y, 0, NumVerticesY - 1) * NumVerticesX + Math.Clamp(x, 0, NumVerticesX - 1);

    /// <summary>ATerrain::Height: clamped to the grid</summary>
    public ushort Height(int x, int y) => Binary.Heights[Index(x, y)];

    /// <summary>TID_Visibility_Off clear: not a hole</summary>
    public bool IsQuadVisible(int x, int y) => (Binary.InfoData[Index(x, y)] & TerrainInfoFlags.TID_Visibility_Off) == 0;

    /// <summary>TID_OrientationFlip: the quad is split along its other diagonal</summary>
    public bool IsQuadFlipped(int x, int y) => (Binary.InfoData[Index(x, y)] & TerrainInfoFlags.TID_OrientationFlip) != 0;

    /// <summary>A vertex in terrain-local space, clamped to the grid as Lightmass and ATerrain::GetLocalVertex clamp it (no displacement)</summary>
    public Vector3 LocalVertex(int x, int y) =>
        new(Math.Clamp(x, 0, NumVerticesX - 1), Math.Clamp(y, 0, NumVerticesY - 1), (Height(x, y) - 32768f) * ZScale);

    /// <summary>
    /// Where a ray in terrain-local space (from <paramref name="origin"/> along <paramref name="direction"/>, to <paramref name="maxT"/> of it)
    /// first meets the height field at maximum tessellation as <paramref name="sections"/> draw it (<see cref="Draws"/>), as a multiple of
    /// <paramref name="direction"/>; null if it doesn't. Each quad is split as its orientation flag says. The quads the ray passes over are tested in
    /// order, from where it enters the grid. With <paramref name="ignoreHoles"/>, hole quads are met too, as UE3's terrain tools trace
    /// (TRACE_TerrainIgnoreHoles), so a hole can be filled
    /// </summary>
    public float? RayHit(Vector3 origin, Vector3 direction, float maxT, IReadOnlyList<TerrainSection> sections, bool ignoreHoles = false)
    {
        float near = 0, far = maxT;
        for (int axis = 0; axis < 2; axis++)
        {
            float start = origin[axis], step = direction[axis], max = (axis == 0 ? NumVerticesX : NumVerticesY) - 1;
            if (step == 0)
            {
                if (start < 0 || start > max) return null;
                continue;
            }
            float t1 = -start / step, t2 = (max - start) / step;
            near = MathF.Max(near, MathF.Min(t1, t2));
            far = MathF.Min(far, MathF.Max(t1, t2));
            if (near > far) return null;
        }
        //walks the quads under the ray (Amanatides & Woo), so the first that it meets is the nearest
        Vector3 entry = origin + direction * near;
        int x = Math.Clamp((int)MathF.Floor(entry.X), 0, NumVerticesX - 2), y = Math.Clamp((int)MathF.Floor(entry.Y), 0, NumVerticesY - 2);
        int stepX = Math.Sign(direction.X), stepY = Math.Sign(direction.Y);
        float deltaX = stepX != 0 ? 1 / MathF.Abs(direction.X) : float.PositiveInfinity, deltaY = stepY != 0 ? 1 / MathF.Abs(direction.Y) : float.PositiveInfinity;
        float nextX = stepX > 0 ? near + (x + 1 - entry.X) / direction.X : stepX < 0 ? near + (x - entry.X) / direction.X : float.PositiveInfinity;
        float nextY = stepY > 0 ? near + (y + 1 - entry.Y) / direction.Y : stepY < 0 ? near + (y - entry.Y) / direction.Y : float.PositiveInfinity;
        while (true)
        {
            if (InSections(x, y) && QuadHit(x, y, origin, direction, maxT) is { } hit) return hit;
            if (nextX < nextY)
            {
                if (nextX > far) return null;
                x += stepX;
                nextX += deltaX;
            }
            else
            {
                if (nextY > far) return null;
                y += stepY;
                nextY += deltaY;
            }
            if (x < 0 || y < 0 || x > NumVerticesX - 2 || y > NumVerticesY - 2) return null;
        }

        //(a loop, not a lambda: it runs for every quad a ray passes, every frame while sculpting)
        bool InSections(int quadX, int quadY)
        {
            foreach (TerrainSection section in sections)
            {
                if (ignoreHoles ? InPatch(section, quadX, quadY) : Draws(section, quadX, quadY)) return true;
            }
            return false;
        }
    }

    //where a ray meets the quad whose first vertex is (x, y), split as AddQuad splits it
    private float? QuadHit(int x, int y, Vector3 origin, Vector3 direction, float maxT)
    {
        Vector3 v00 = LocalVertex(x, y), v10 = LocalVertex(x + 1, y), v01 = LocalVertex(x, y + 1), v11 = LocalVertex(x + 1, y + 1);
        bool flipped = IsQuadFlipped(x, y);
        float? first = flipped ? RayTriangle(origin, direction, v00, v01, v10) : RayTriangle(origin, direction, v00, v01, v11);
        float? second = flipped ? RayTriangle(origin, direction, v10, v01, v11) : RayTriangle(origin, direction, v00, v11, v10);
        float? nearest = null;
        if (first is { } t1 && t1 >= 0 && t1 <= maxT) nearest = t1;
        if (second is { } t2 && t2 >= 0 && t2 <= maxT && (nearest is null || t2 < nearest)) nearest = t2;
        return nearest;
    }

    //Möller-Trumbore, from either side
    private static float? RayTriangle(Vector3 origin, Vector3 direction, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 edge1 = v1 - v0, edge2 = v2 - v0, p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (determinant == 0) return null;
        float inverse = 1 / determinant;
        Vector3 s = origin - v0;
        float u = Vector3.Dot(s, p) * inverse;
        if (u < 0 || u > 1) return null;
        Vector3 q = Vector3.Cross(s, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0 || u + v > 1) return null;
        return Vector3.Dot(edge2, q) * inverse;
    }

    /// <summary>
    /// dZ/dX and dZ/dY at a vertex in terrain-local units, from the neighbouring heights as FTerrainVertexBuffer::FillData takes them at maximum
    /// tessellation (the clamped neighbour at an edge)
    /// </summary>
    public Vector2 Gradient(int x, int y) => new((Height(x + 1, y) - Height(x - 1, y)) * 0.5f * ZScale, (Height(x, y + 1) - Height(x, y - 1)) * 0.5f * ZScale);

    /// <summary>
    /// The gradients FTerrainVertexBuffer::FillData stores in a vertex at maximum tessellation: the central height differences, in height units,
    /// times DrawScale3D.Z / DrawScale3D.X and .Z / .Y, truncated to shorts
    /// </summary>
    public (short X, short Y) PackedGradient(int x, int y, float scaleX, float scaleY) =>
        ((short)(int)((Height(x + 1, y) - Height(x - 1, y)) / 2f * scaleX), (short)(int)((Height(x, y + 1) - Height(x, y - 1)) / 2f * scaleY));

    /// <summary>StaticLightingResolution: light-map texels per quad (Default__Terrain's is 4)</summary>
    public int StaticLightingResolution => Math.Max(1, Properties.GetProp<IntProperty>("StaticLightingResolution")?.Value ?? 4);

    /// <summary>
    /// A component's properties with the lighting settings ATerrain::PostLoad copies onto every TerrainComponent from the terrain: CastShadow,
    /// bCastDynamicShadow, bForceDirectLightMap, bAcceptsDynamicLights and LightingChannels. Where the terrain doesn't set one, Default__Terrain's
    /// (the same in LE1 and LE2's Engine.pcc): all true, and the Static channel
    /// </summary>
    public PropertyCollection WithTerrainLightingSettings(PropertyCollection componentProps)
    {
        var result = new PropertyCollection();
        foreach (Property prop in componentProps)
        {
            if (prop.Name.Name is not ("CastShadow" or "bCastDynamicShadow" or "bForceDirectLightMap" or "bAcceptsDynamicLights" or "LightingChannels"))
                result.Add(prop);
        }
        bool Bool(string name) => Properties.GetProp<BoolProperty>(name)?.Value ?? true;
        result.Add(new BoolProperty(Bool("bCastShadow"), "CastShadow"));
        result.Add(new BoolProperty(Bool("bCastDynamicShadow"), "bCastDynamicShadow"));
        result.Add(new BoolProperty(Bool("bForceDirectLightMap"), "bForceDirectLightMap"));
        result.Add(new BoolProperty(Bool("bAcceptsDynamicLights"), "bAcceptsDynamicLights"));
        result.Add(Properties.GetProp<StructProperty>("LightingChannels")?.DeepClone() as StructProperty
                   ?? new StructProperty("LightingChannelContainer", new PropertyCollection { new BoolProperty(true, "bInitialized"), new BoolProperty(true, "Static") },
                       "LightingChannels", true));
        return result;
    }

    /// <summary>The terrain's components, in its TerrainComponents order. Components that can't be resolved are skipped</summary>
    public List<TerrainSection> ReadSections(Func<ExportEntry, PropertyCollection> readProperties)
    {
        var sections = new List<TerrainSection>();
        foreach (var componentProp in Properties.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents") ?? Enumerable.Empty<ObjectProperty>())
        {
            if (!Export.FileRef.TryGetUExport(componentProp.Value, out var component)) continue;
            var p = readProperties(component);
            int I(string name) => p.GetProp<IntProperty>(name)?.Value ?? 0;
            sections.Add(new TerrainSection(component, p, I("SectionBaseX"), I("SectionBaseY"), I("SectionSizeX"), I("SectionSizeY"),
                I("TrueSectionSizeX"), I("TrueSectionSizeY")));
        }
        return sections;
    }

    /// <summary>Whether the section's quads are all within the height field</summary>
    public bool Contains(TerrainSection section) => section.BaseX >= 0 && section.BaseY >= 0 && section.TrueSizeX > 0 && section.TrueSizeY > 0
        && section.BaseX + section.TrueSizeX < NumVerticesX && section.BaseY + section.TrueSizeY < NumVerticesY;

    /// <summary>Whether all of the section's quads are drawn with the default diagonal</summary>
    public bool IsRegular(TerrainSection section)
    {
        for (int y = 0; y < section.TrueSizeY; y++)
        for (int x = 0; x < section.TrueSizeX; x++)
            if (!IsQuadVisible(section.BaseX + x, section.BaseY + y) || IsQuadFlipped(section.BaseX + x, section.BaseY + y)) return false;
        return true;
    }

    /// <summary>
    /// The section's triangles at maximum tessellation, as FTerrainTessellationIndexBuffer::FillData builds them: indices into a
    /// (TrueSizeX + 1) × (TrueSizeY + 1) grid of section-local vertices. A patch is drawn when its first quad is visible (holes are painted per
    /// patch); each quad is split as its orientation flag says.
    /// </summary>
    public List<int> SectionIndices(TerrainSection section)
    {
        int step = section.Step, stride = section.TrueSizeX + 1;
        var indices = new List<int>(section.TrueSizeX * section.TrueSizeY * 6);
        for (int py = 0; py < section.SizeY; py++)
        for (int px = 0; px < section.SizeX; px++)
        {
            if (!IsQuadVisible(section.BaseX + px * step, section.BaseY + py * step)) continue;
            for (int sy = 0; sy < step; sy++)
            for (int sx = 0; sx < step; sx++)
            {
                int x = px * step + sx, y = py * step + sy;
                if (x >= section.TrueSizeX || y >= section.TrueSizeY) continue;
                AddQuad(indices, y * stride + x, stride, IsQuadFlipped(section.BaseX + x, section.BaseY + y));
            }
        }
        return indices;
    }

    /// <summary>Whether <see cref="SectionIndices"/> draws the quad whose first vertex is (<paramref name="x"/>, <paramref name="y"/>): it's in one of the section's patches, whose first quad is visible</summary>
    public bool Draws(TerrainSection section, int x, int y) =>
        InPatch(section, x, y) && IsQuadVisible(section.BaseX + (x - section.BaseX) / section.Step * section.Step, section.BaseY + (y - section.BaseY) / section.Step * section.Step);

    /// <summary>Whether the quad whose first vertex is (<paramref name="x"/>, <paramref name="y"/>) is in one of the section's patches, hole or not</summary>
    public bool InPatch(TerrainSection section, int x, int y)
    {
        int sx = x - section.BaseX, sy = y - section.BaseY, step = section.Step;
        return sx >= 0 && sy >= 0 && sx < section.TrueSizeX && sy < section.TrueSizeY && sx / step < section.SizeX && sy / step < section.SizeY;
    }

    /// <summary>How many triangles <see cref="SectionIndices"/> makes, without making them</summary>
    public int SectionTriangleCount(TerrainSection section)
    {
        int step = section.Step, quads = 0;
        for (int py = 0; py < section.SizeY; py++)
        for (int px = 0; px < section.SizeX; px++)
        {
            if (!IsQuadVisible(section.BaseX + px * step, section.BaseY + py * step)) continue;
            quads += Math.Clamp(section.TrueSizeX - px * step, 0, step) * Math.Clamp(section.TrueSizeY - py * step, 0, step);
        }
        return quads * 2;
    }

    /// <summary><see cref="SectionIndices"/> of a section without holes or flipped quads</summary>
    public static List<int> RegularIndices(int trueSizeX, int trueSizeY)
    {
        int stride = trueSizeX + 1;
        var indices = new List<int>(trueSizeX * trueSizeY * 6);
        for (int y = 0; y < trueSizeY; y++)
        for (int x = 0; x < trueSizeX; x++)
            AddQuad(indices, y * stride + x, stride, flipped: false);
        return indices;
    }

    private static void AddQuad(List<int> indices, int v00, int stride, bool flipped)
    {
        int v10 = v00 + 1, v01 = v00 + stride, v11 = v01 + 1;
        if (!flipped)
            indices.AddRange([v00, v01, v11, v00, v11, v10]);
        else
            indices.AddRange([v00, v01, v10, v10, v01, v11]);
    }
}
