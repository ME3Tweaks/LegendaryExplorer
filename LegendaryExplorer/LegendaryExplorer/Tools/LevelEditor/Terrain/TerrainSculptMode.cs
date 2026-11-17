using System;
using System.Linq;
using System.Numerics;
using System.Windows.Input;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Sculpting in the viewport, as UE3's terrain editing mode does it: a left drag that starts on a terrain applies the tool to it every frame the button
/// is held (FModeTool_Terrain::Tick), with the brush where the cursor meets that terrain. Shift inverts the tool. A drag that starts anywhere else moves
/// the camera, as the other buttons do. The brush is outlined on the terrain under the cursor
/// </summary>
public sealed class TerrainSculptMode
{
    public TerrainSculptTool Tool { get; set; }
    /// <summary>World units from the brush's centre within which it's at full strength</summary>
    public float Radius { get; set; } = 512;
    /// <summary>World units beyond <see cref="Radius"/> over which it fades to nothing</summary>
    public float Falloff { get; set; } = 512;
    /// <summary>UE3's tool strength: Paint moves the brush's centre by 10 × it world units a second, times the terrain's DrawScale</summary>
    public float Strength { get; set; } = 100;

    /// <summary>Why a terrain can't be sculpted now, apart from its own <see cref="TerrainProxy.SculptRefusal"/> (its file is read-only, say), or null</summary>
    public Func<TerrainProxy, string> Refusal { get; set; }
    /// <summary>A stroke that changed something ended</summary>
    public Action<TerrainSculptAction> OnStrokeComplete { get; set; }
    /// <summary>A stroke couldn't start, and why</summary>
    public Action<string> OnRefused { get; set; }

    //UE3's HALF_WORLD_MAX, how far its terrain trace reaches
    private const float TraceDistance = 262144;
    //a frame that took longer (a hitch while shaders load) is applied as this long, so the brush doesn't jump
    private const float MaxTick = 0.1f;

    private (int X, int Y)? cursor;
    //where the cursor met a terrain this frame (the stroke's, during one), found once a frame by Update for it and Draw
    private (TerrainProxy Terrain, Vector3 Local)? hover;
    private TerrainProxy terrain;
    private TerrainStroke stroke;
    private bool inverted;

    public bool IsStroking => stroke is not null;

    /// <summary>Where the cursor is in the viewport; null once it has left it (<see cref="LevelEditorRenderContext.MouseLeft"/>)</summary>
    public void MoveCursor((int X, int Y)? pixel)
    {
        cursor = pixel;
        if (pixel is null) hover = null;
    }

    /// <summary>
    /// A left press: starts a stroke if it's on a terrain. Returns whether the press is the brush's (on a terrain, sculpted or refused). A terrain
    /// that can't be sculpted, or can't be read to find out, is refused (<see cref="OnRefused"/>) rather than thrown out of the mouse handler
    /// </summary>
    public bool BeginStroke(LevelEditorRenderContext context, int x, int y, ModifierKeys modifiers)
    {
        cursor = (x, y);
        if (stroke is not null || TerrainUnder(context, x, y) is not var (target, local)) return false;
        //(the terrain's sections and their archetypes are read to check it)
        try
        {
            if ((Refusal?.Invoke(target) ?? target.SculptRefusal) is { } refusal)
            {
                OnRefused?.Invoke(refusal);
                return true;
            }
            stroke = target.BeginStroke(Tool, local);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            OnRefused?.Invoke($"{target.DisplayText} can't be sculpted: {error.Message}");
            return true;
        }
        terrain = target;
        inverted = modifiers.HasFlag(ModifierKeys.Shift);
        return true;
    }

    /// <summary>A frame of the stroke: the tool applied for its time where the cursor meets the stroke's terrain, if it does</summary>
    public void Update(LevelEditorRenderContext context, float deltaTime)
    {
        hover = cursor is var (x, y) ? TerrainUnder(context, x, y, terrain) : null;
        if (stroke is null || hover is not var (_, local)) return;
        terrain.Sculpt(stroke, local, Radius, Falloff, Strength, MathF.Min(deltaTime, MaxTick), inverted);
    }

    /// <summary>Ends the stroke: a completed one is recorded (<see cref="OnStrokeComplete"/>), a cancelled one puts back what it changed</summary>
    public void EndStroke(DragEndReason reason)
    {
        if (stroke is null) return;
        var (target, ending) = (terrain, stroke);
        (terrain, stroke) = (null, null);
        if (reason is DragEndReason.Cancelled)
        {
            target.CancelStroke(ending);
            return;
        }
        if (target.FinishStroke(ending) is { } action) OnStrokeComplete?.Invoke(action);
    }

    /// <summary>The terrain a pixel's ray meets first, holes and all, of those drawn (only <paramref name="only"/>, if given), and where in its own space</summary>
    private static (TerrainProxy Terrain, Vector3 Local)? TerrainUnder(LevelEditorRenderContext context, int x, int y, TerrainProxy only = null)
    {
        if (context.Width <= 0 || context.Height <= 0) return null;
        var (origin, direction) = context.GetPixelRay(x, y);
        TerrainProxy nearest = null;
        float nearestDistance = TraceDistance;
        foreach (TerrainProxy candidate in only is null ? context.DrawList_3D.OfType<TerrainProxy>() : [only])
        {
            //(holes included, as UE3's terrain tools trace, so they can be filled)
            if (context.IsActorVisible(candidate) && candidate.RayHit(origin, direction, nearestDistance, ignoreHoles: true) is { } distance && distance < nearestDistance)
            {
                (nearest, nearestDistance) = (candidate, distance);
            }
        }
        return nearest?.ToTerrainSpace(origin + direction * nearestDistance) is { } local ? (nearest, local) : null;
    }

    private static readonly Vector4 InnerColor = new(1, 0.85f, 0.2f, 1);
    private static readonly Vector4 OuterColor = new(1, 0.85f, 0.2f, 0.45f);

    /// <summary>
    /// Outlines the brush on the terrain under the cursor (the stroke's, during one), as this frame's <see cref="Update"/> found it: its full-strength radius and how far it fades, or for the Holes
    /// tool, the patches it sets (within the grid: past its edge, the tool sets the edge's quads)
    /// </summary>
    public void Draw(LevelEditorRenderContext context)
    {
        if (hover is not var (target, local)) return;
        var (inner, outer, _) = TerrainStroke.LocalBrush(Radius, Falloff, Strength, 0, target.DrawScale, target.DrawScale3D);
        if (Tool is TerrainSculptTool.Holes)
        {
            if (target.HeightField is not { } field) return;
            var (startX, startY, endX, endY) = TerrainStroke.HoleQuads(field, TerrainStroke.BrushVertices(field, local, outer));
            (endX, endY) = (Math.Min(endX, field.NumVerticesX - 1), Math.Min(endY, field.NumVerticesY - 1));
            Outline(context, target, [new(startX, startY), new(endX, startY), new(endX, endY), new(startX, endY), new(startX, startY)], InnerColor);
            return;
        }
        Circle(context, target, local, inner, InnerColor);
        if (outer > inner) Circle(context, target, local, outer, OuterColor);
    }

    private static void Circle(LevelEditorRenderContext context, TerrainProxy target, Vector3 center, float radius, Vector4 color)
    {
        const int segments = 64;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            (float sin, float cos) = MathF.SinCos(MathF.Tau * i / segments);
            points[i] = new Vector2(center.X + cos * radius, center.Y + sin * radius);
        }
        Outline(context, target, points, color);
    }

    //a line through terrain-space points, along the surface (kept on the grid, so the part of a brush past its edge is drawn along it): each edge is
    //divided once a vertex it passes, up to 64 times, so it follows hills
    private static void Outline(LevelEditorRenderContext context, TerrainProxy target, Vector2[] points, Vector4 color)
    {
        Vector3? previous = null;
        for (int i = 0; i < points.Length; i++)
        {
            int steps = i == 0 ? 1 : Math.Clamp((int)MathF.Ceiling(Vector2.Distance(points[i - 1], points[i])), 1, 64);
            for (int step = i == 0 ? 0 : 1; step <= steps; step++)
            {
                Vector2 point = i == 0 ? points[0] : Vector2.Lerp(points[i - 1], points[i], (float)step / steps);
                if (target.SurfacePoint(point.X, point.Y) is not { } surface) return;
                Vector3 world = target.ToWorld(surface);
                if (previous is { } start) context.UIPrimitives.AddLine(start, world, color, BatchedPrimitives.NoHitId);
                previous = world;
            }
        }
    }
}
