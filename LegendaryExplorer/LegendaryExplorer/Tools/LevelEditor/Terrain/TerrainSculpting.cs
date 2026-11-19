using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>The tools of UE3's terrain editing mode that edit a terrain's height field</summary>
public enum TerrainSculptTool
{
    /// <summary>FModeTool_TerrainPaint on the height map: raises the heights under the brush, or lowers them when inverted</summary>
    Paint,
    /// <summary>FModeTool_TerrainSmooth: blends each height towards the average of its 3 × 3 neighbourhood, or away from it when inverted</summary>
    Smooth,
    /// <summary>FModeTool_TerrainFlatten: sets the heights under the brush to the height of the vertex nearest where the stroke began</summary>
    Flatten,
    /// <summary>FModeTool_TerrainVisibility: makes holes in the patches under the brush, or fills them when inverted</summary>
    Holes,
}

/// <summary>A rectangle of the height field's vertices, inclusive</summary>
public readonly record struct TerrainRegion(int MinX, int MinY, int MaxX, int MaxY)
{
    public TerrainRegion Union(TerrainRegion other) =>
        new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

    /// <summary>Whether a section draws any of its vertices, or the heights next to them that its vertices' gradients are taken from</summary>
    public bool Affects(TerrainSection section) => section.BaseX <= MaxX + 1 && section.BaseX + section.TrueSizeX >= MinX - 1
                                                   && section.BaseY <= MaxY + 1 && section.BaseY + section.TrueSizeY >= MinY - 1;
}

/// <summary>
/// One stroke of a tool on a height field: what one press of the button does while it's held, as UE3's terrain editing mode applies a tool each tick
/// (FModeTool_Terrain::Tick). Height changes too small to move a height by a whole unit are kept per vertex until they add up to one, as
/// FModeTool_Terrain's partial data keeps them for the stroke. It edits the height field in place, and remembers what each value was before it
/// </summary>
public sealed class TerrainStroke
{
    public TerrainHeightField Field { get; }
    public TerrainSculptTool Tool { get; }
    private readonly Dictionary<int, float> partial = [];
    private readonly Dictionary<int, ushort> heightsBefore = [];
    private readonly Dictionary<int, TerrainInfoFlags> infoBefore = [];
    private readonly ushort flatHeight;
    private TerrainRegion? changed;

    /// <param name="field">One that <see cref="TerrainHeightField.IsValid"/></param>
    /// <param name="localStart">Where the stroke began, in terrain-local space</param>
    public TerrainStroke(TerrainHeightField field, TerrainSculptTool tool, Vector3 localStart)
    {
        Field = field;
        Tool = tool;
        // FModeTool_TerrainFlatten::BeginApplyTool: the height of the vertex ATerrain::GetClosestLocalSpaceVertex finds (appRound, halves up)
        flatHeight = field.Height((int)MathF.Floor(localStart.X + 0.5f), (int)MathF.Floor(localStart.Y + 0.5f));
    }

    /// <summary>
    /// UE3's terrain tool radii and strength for a brush on a terrain: <paramref name="radius"/> (where the brush is at full strength) and
    /// <paramref name="falloff"/> (beyond it, fading to nothing) in world units, divided by the terrain's X scale; and the strength a tick of
    /// <paramref name="deltaTime"/> seconds applies, in height units: <paramref name="strength"/> × the time, divided by the terrain's Z scale
    /// and TERRAIN_ZSCALE
    /// </summary>
    public static (float Inner, float Outer, float Strength) LocalBrush(float radius, float falloff, float strength, float deltaTime, float drawScale,
        Vector3 drawScale3D) =>
        (radius / MathF.Abs(drawScale3D.X) / drawScale, (radius + falloff) / MathF.Abs(drawScale3D.X) / drawScale,
            strength * deltaTime / drawScale3D.Z / TerrainHeightField.ZScale);

    /// <summary>
    /// Applies the tool once with the brush at <paramref name="localPosition"/> (terrain-local space), as FModeTool_Terrain::Tick and the tool's
    /// ApplyTool do: <paramref name="localInner"/>, <paramref name="localOuter"/> and <paramref name="localStrength"/> are <see cref="LocalBrush"/>'s.
    /// Returns the vertices whose height, or whose quad's flags, it changed; null if it changed none
    /// </summary>
    public TerrainRegion? Apply(Vector3 localPosition, float localInner, float localOuter, float localStrength, bool inverted)
    {
        int lastX = Field.NumVerticesX - 1, lastY = Field.NumVerticesY - 1;
        var (minX, minY, maxX, maxY) = BrushVertices(Field, localPosition, localOuter);
        float direction = inverted ? -1 : 1;
        TerrainRegion? applied = null;
        void Changed(int x, int y)
        {
            var vertex = new TerrainRegion(x, y, x, y);
            applied = applied?.Union(vertex) ?? vertex;
        }
        float Strength(int x, int y) => RadiusStrength(localPosition, x, y, localInner, localOuter);
        switch (Tool)
        {
            case TerrainSculptTool.Paint:
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (AddPartialHeight(x, y, direction * localStrength * 10 * Strength(x, y))) Changed(x, y);
                }
                break;
            case TerrainSculptTool.Smooth:
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float amount = Strength(x, y) * localStrength * direction / 9;
                    float height = HeightWithPartial(x, y), average = 0;
                    //(the neighbours as they are now: those before it in this pass are already smoothed, as UE3 smooths them)
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    for (int nx = x - 1; nx <= x + 1; nx++)
                        average += HeightWithPartial(Math.Clamp(nx, 0, lastX), Math.Clamp(ny, 0, lastY));
                    average /= 9;
                    //UE3's Lerp(Height, SmoothHeight, Min(StrengthScalar, 1)) - Height, kept in its order so it rounds the same
                    if (AddPartialHeight(x, y, height + (average - height) * MathF.Min(amount, 1) - height)) Changed(x, y);
                }
                break;
            case TerrainSculptTool.Flatten:
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (Strength(x, y) > 0 && SetHeight(x, y, flatHeight)) Changed(x, y);
                }
                break;
            case TerrainSculptTool.Holes:
            {
                var (startX, startY, endX, endY) = HoleQuads(Field, new TerrainRegion(minX, minY, maxX, maxY));
                bool hole = !inverted;
                for (int y = startY; y < endY; y++)
                for (int x = startX; x < endX; x++)
                {
                    //clamped to the grid, as ATerrain::GetInfoData clamps
                    int cx = Math.Clamp(x, 0, lastX), cy = Math.Clamp(y, 0, lastY);
                    if (SetHole(cx, cy, hole)) Changed(cx, cy);
                }
                break;
            }
        }
        if (applied is { } region) changed = changed?.Union(region) ?? region;
        return applied;
    }

    /// <summary>The vertices a brush at <paramref name="localPosition"/> reaches: those within <paramref name="localOuter"/> of it in X and Y, rounded out to whole vertices, in the grid</summary>
    public static TerrainRegion BrushVertices(TerrainHeightField field, Vector3 localPosition, float localOuter)
    {
        int lastX = field.NumVerticesX - 1, lastY = field.NumVerticesY - 1;
        return new TerrainRegion(Math.Clamp((int)MathF.Floor(localPosition.X - localOuter), 0, lastX), Math.Clamp((int)MathF.Floor(localPosition.Y - localOuter), 0, lastY),
            Math.Clamp((int)MathF.Ceiling(localPosition.X + localOuter), 0, lastX), Math.Clamp((int)MathF.Ceiling(localPosition.Y + localOuter), 0, lastY));
    }

    /// <summary>
    /// The quads the Holes tool sets for a brush reaching <paramref name="brush"/>'s vertices, from (StartX, StartY) up to but not including (EndX, EndY):
    /// whole patches, from the one its first vertex is in up to the one its last is in, which isn't included unless it's the first (at least one patch
    /// each way), as FModeTool_TerrainVisibility rounds them. They can reach past the grid, where the tool sets its edge's quads instead
    /// </summary>
    public static (int StartX, int StartY, int EndX, int EndY) HoleQuads(TerrainHeightField field, TerrainRegion brush)
    {
        int patch = Math.Max(1, field.MaxTesselationLevel);
        int startX = brush.MinX - brush.MinX % patch, startY = brush.MinY - brush.MinY % patch;
        int endX = brush.MaxX - brush.MaxX % patch, endY = brush.MaxY - brush.MaxY % patch;
        if (endX < startX + patch) endX += patch;
        if (endY < startY + patch) endY += patch;
        return (startX, startY, endX, endY);
    }

    /// <summary>UE3's RadiusStrength: 1 within the inner radius, fading linearly to 0 at the outer one, by distance in X and Y</summary>
    private static float RadiusStrength(Vector3 center, int x, int y, float inner, float outer)
    {
        float distance = new Vector2(x - center.X, y - center.Y).Length();
        return distance <= inner ? 1 : distance < outer ? 1 - (distance - inner) / (outer - inner) : 0;
    }

    private int Index(int x, int y) => y * Field.NumVerticesX + x;

    private float HeightWithPartial(int x, int y) => Field.Height(x, y) + partial.GetValueOrDefault(Index(x, y));

    //FModeTool_Terrain::SetPartialData on the height map: whole units move the height (clamped to a WORD), the rest is kept
    private bool AddPartialHeight(int x, int y, float amount)
    {
        int index = Index(x, y);
        float value = partial.GetValueOrDefault(index) + amount;
        int whole = (int)value;
        bool moved = false;
        if (whole != 0)
        {
            moved = SetHeight(x, y, (ushort)Math.Clamp(Field.Height(x, y) + whole, 0, ushort.MaxValue));
            value -= whole;
        }
        partial[index] = value;
        return moved;
    }

    private bool SetHeight(int x, int y, ushort height)
    {
        int index = Index(x, y);
        ushort current = Field.Binary.Heights[index];
        if (current == height) return false;
        heightsBefore.TryAdd(index, current);
        Field.Binary.Heights[index] = height;
        return true;
    }

    private bool SetHole(int x, int y, bool hole)
    {
        int index = Index(x, y);
        TerrainInfoFlags current = Field.Binary.InfoData[index];
        TerrainInfoFlags flags = hole ? current | TerrainInfoFlags.TID_Visibility_Off : current & ~TerrainInfoFlags.TID_Visibility_Off;
        if (current == flags) return false;
        infoBefore.TryAdd(index, current);
        Field.Binary.InfoData[index] = flags;
        return true;
    }

    /// <summary>What the stroke changed, from before it to now; empty if it changed nothing in the end</summary>
    public TerrainEdit Finish()
    {
        ushort[] heights = Field.Binary.Heights;
        TerrainInfoFlags[] info = Field.Binary.InfoData;
        var heightChanges = heightsBefore.Where(change => heights[change.Key] != change.Value).OrderBy(change => change.Key).ToArray();
        var infoChanges = infoBefore.Where(change => info[change.Key] != change.Value).OrderBy(change => change.Key).ToArray();
        return new TerrainEdit(Field.NumVerticesX, heights.Length, heightChanges.Select(c => c.Key).ToArray(), heightChanges.Select(c => c.Value).ToArray(),
            heightChanges.Select(c => heights[c.Key]).ToArray(), infoChanges.Select(c => c.Key).ToArray(), infoChanges.Select(c => c.Value).ToArray(),
            infoChanges.Select(c => info[c.Key]).ToArray());
    }

    /// <summary>Puts back everything the stroke changed. Returns the vertices it changed; null if none</summary>
    public TerrainRegion? Revert()
    {
        foreach (var (index, height) in heightsBefore) Field.Binary.Heights[index] = height;
        foreach (var (index, flags) in infoBefore) Field.Binary.InfoData[index] = flags;
        heightsBefore.Clear();
        infoBefore.Clear();
        partial.Clear();
        TerrainRegion? region = changed;
        changed = null;
        return region;
    }
}

/// <summary>
/// Heights and quad flags a stroke changed, by index into the arrays of a height field <paramref name="numVerticesX"/> wide with
/// <paramref name="vertexCount"/> vertices, with their values before and after it
/// </summary>
public sealed class TerrainEdit(int numVerticesX, int vertexCount, int[] heightIndices, ushort[] heightsBefore, ushort[] heightsAfter, int[] infoIndices,
    TerrainInfoFlags[] infoBefore, TerrainInfoFlags[] infoAfter)
{
    public int NumVerticesX { get; } = numVerticesX;
    public int VertexCount { get; } = vertexCount;
    public int[] HeightIndices { get; } = heightIndices;
    public ushort[] HeightsBefore { get; } = heightsBefore;
    public ushort[] HeightsAfter { get; } = heightsAfter;
    public int[] InfoIndices { get; } = infoIndices;
    public TerrainInfoFlags[] InfoBefore { get; } = infoBefore;
    public TerrainInfoFlags[] InfoAfter { get; } = infoAfter;

    public bool IsEmpty => HeightIndices.Length == 0 && InfoIndices.Length == 0;

    /// <summary>The vertices it changes</summary>
    public TerrainRegion Region
    {
        get
        {
            TerrainRegion? region = null;
            foreach (int index in HeightIndices.Concat(InfoIndices))
            {
                int x = index % NumVerticesX, y = index / NumVerticesX;
                var vertex = new TerrainRegion(x, y, x, y);
                region = region?.Union(vertex) ?? vertex;
            }
            return region ?? throw new InvalidOperationException("The edit changes nothing.");
        }
    }

    /// <summary>Gives the height field the values from after the edit, or from before it</summary>
    public void Apply(TerrainHeightField field, bool after)
    {
        //(a valid field has a height and flags for each vertex)
        if (!field.IsValid || field.NumVerticesX != NumVerticesX || field.Binary.Heights.Length != VertexCount)
            throw new InvalidOperationException($"{field.Export.InstancedFullPath}'s height field has changed size since it was sculpted.");
        ushort[] heights = after ? HeightsAfter : HeightsBefore;
        for (int i = 0; i < HeightIndices.Length; i++) field.Binary.Heights[HeightIndices[i]] = heights[i];
        TerrainInfoFlags[] info = after ? InfoAfter : InfoBefore;
        for (int i = 0; i < InfoIndices.Length; i++) field.Binary.InfoData[InfoIndices[i]] = info[i];
    }
}

/// <summary>A stroke of a terrain tool, undone and redone on the terrain's view</summary>
public sealed class TerrainSculptAction(TerrainProxy terrain, TerrainEdit edit, TerrainSculptTool tool) : IUndoAction
{
    private readonly ActorRef actor = new(terrain);

    public string Description { get; } = tool switch
    {
        TerrainSculptTool.Paint => "Paint terrain heights",
        TerrainSculptTool.Smooth => "Smooth terrain",
        TerrainSculptTool.Flatten => "Flatten terrain",
        TerrainSculptTool.Holes => "Edit terrain holes",
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
    };
    public IEnumerable<AssetReference> Assets => [];

    public void Undo() => ((TerrainProxy)actor.View).ApplyEdit(edit, after: false);
    public void Redo() => ((TerrainProxy)actor.View).ApplyEdit(edit, after: true);
    public bool IsOrphaned => actor.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [actor.Id];
}
