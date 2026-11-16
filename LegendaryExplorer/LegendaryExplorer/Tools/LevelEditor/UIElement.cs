using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Collections;
using System;
using System.Numerics;
using Rotator = LegendaryExplorerCore.Unreal.BinaryConverters.Rotator;

namespace LegendaryExplorer.Tools.LevelEditor;

public class UIElement
{
    //we assume that begindraw has already been called
    public virtual void Draw(LevelEditorRenderContext context)
    {

    }
}

[Flags]
public enum EWidgetAxis
{
    None = 0,
    X = 1,
    Y = 2,
    Z = 4,
    XY = X | Y,
    XZ = X | Z,
    YZ = Y | Z,
    XYZ = X | Y | Z,
}

public enum EWidgetMode
{
    Translate,
    Rotate,
    UniformScale,
    Scale
}

/// <summary>
/// How a viewport drag ended: a completed drag is recorded for undo, a cancelled one restores what it started from
/// </summary>
public enum DragEndReason
{
    Completed,
    Cancelled
}

public class Widget : UIElement
{
    public ActorProxy Attach;

    public EWidgetMode Mode = EWidgetMode.Translate;
    public bool UseLocalCoords = true;
    Matrix4x4 LocalRotation;

    public EWidgetAxis CurrentAxis;
    public bool IsDragging;
    private Vector2 DragStart;
    private Vector2 PrevDragPos;
    private TransformSnapshot _dragStartSnapshot;

    Vector2 Origin, XAxisEnd, YAxisEnd, ZAxisEnd;

    Matrix4x4 XMatrix;
    Matrix4x4 YMatrix;
    Matrix4x4 ZMatrix;

    private readonly int[] AxisHitIds = new int[8];

    static readonly Vector4 XColor = new Vector4(1, 0, 0, 1);
    static readonly Vector4 YColor = new Vector4(0, 1, 0, 1);
    static readonly Vector4 ZColor = new Vector4(0, 0, 1, 1);
    static readonly Vector4 SelectedColor = new Vector4(1, 1, 0, 1);

    private const float AxisLength = 40f;

    // Snapping settings
    public bool TranslateSnapEnabled = true;
    public float TranslateSnapValue = 10f;      // world units
    public bool RotateSnapEnabled = true;
    public float RotateSnapValue = 5f;          // degrees
    public bool ScaleSnapEnabled = true;
    public float ScaleSnapValue = 10f;          // percent (10 = 0.1 step)

    // Per-drag accumulators for snapping (reset on BeginDrag)
    private Vector3 _accumulatedTranslationLocal;
    private float _accumulatedRotation;         // radians
    private float _accumulatedScaleUniform;
    private Vector3 _accumulatedScale3D;
    //where a dual-axis drag grabbed its plane, found on its first move
    private Vector3? _planeGrab;
    private Vector3 _planeOrigin;

    private enum EAxisType
    {
        ReadOnly,
        Cube,
        Cone
    }

    public override void Draw(LevelEditorRenderContext context)
    {
        if (Attach is null || !Attach.CanEditTransform) return;

        var ltw = Attach.LocalToWorld;
        var origin = ltw.Translation;

        context.WorldToPixel(origin, out Origin);

        LocalRotation = UseLocalCoords || Mode is EWidgetMode.Scale ? ActorUtils.ComposeLocalToWorld(Vector3.Zero, Attach.Rotation, Vector3.One) : Matrix4x4.Identity;

        if (Mode is EWidgetMode.Rotate && !Attach.IsReadOnly)
        {
            DrawRotator(context, ltw, origin);
        }
        else
        {
            XMatrix = LocalRotation * Matrix4x4.CreateTranslation(origin);
            YMatrix = Matrix4x4.CreateRotationZ(MathF.PI / 2) * LocalRotation * Matrix4x4.CreateTranslation(origin);
            ZMatrix = Matrix4x4.CreateRotationY(-MathF.PI / 2) * LocalRotation * Matrix4x4.CreateTranslation(origin);

            (Vector4 xColor, Vector4 yColor, Vector4 zColor) = GetAxisColors();
            EAxisType axistype = Attach.IsReadOnly ? EAxisType.ReadOnly
                                 : Mode is EWidgetMode.Translate ? EAxisType.Cone
                                 : EAxisType.Cube;
            if (Mode is EWidgetMode.UniformScale)
            {
                yColor = zColor = xColor = CurrentAxis == EWidgetAxis.None ? XColor : SelectedColor;
            }

            float scale = GetScale(context, origin);
            XAxisEnd = DrawAxis(context, scale, XMatrix, xColor, AxisHitIds[(int)EWidgetAxis.X], axistype);
            YAxisEnd = DrawAxis(context, scale, YMatrix, yColor, AxisHitIds[(int)EWidgetAxis.Y], axistype);
            ZAxisEnd = DrawAxis(context, scale, ZMatrix, zColor, AxisHitIds[(int)EWidgetAxis.Z], axistype);

            if (axistype is EAxisType.Cone)
            {
                DrawPlaneHandle(context, scale, origin, EWidgetAxis.XY, Vector3.UnitX, Vector3.UnitY, xColor, yColor);
                DrawPlaneHandle(context, scale, origin, EWidgetAxis.XZ, Vector3.UnitX, Vector3.UnitZ, xColor, zColor);
                DrawPlaneHandle(context, scale, origin, EWidgetAxis.YZ, Vector3.UnitY, Vector3.UnitZ, yColor, zColor);
            }
        }
    }

    //how far along each of its axes a dual-axis handle's corner is, as in UE3
    private const float PlaneHandleSize = 16f;

    //a plane seen closer to edge-on than this (the sine of the angle) can't be dragged in usefully, so its handle isn't drawn
    private const float MinPlaneFacing = 0.1f;

    /// <summary>
    /// Draws the right-angled lines joining two axes near where they meet, which drag in the plane of both (UE3's FWidget::Render_Translate).
    /// Each line is drawn in the color of the axis it starts from
    /// </summary>
    private void DrawPlaneHandle(LevelEditorRenderContext context, float scale, Vector3 origin, EWidgetAxis plane, Vector3 axis1, Vector3 axis2,
        Vector4 color1, Vector4 color2)
    {
        //the one being dragged stays, though the drag may turn it edge-on
        if (!(IsDragging && CurrentAxis == plane) && MathF.Abs(Vector3.Dot(ViewDirection(context, origin), PlaneNormal(plane))) < MinPlaneFacing) return;
        var ltw = Matrix4x4.CreateScale(scale) * XMatrix;
        Vector3 end1 = Vector3.Transform(axis1 * PlaneHandleSize, ltw);
        Vector3 end2 = Vector3.Transform(axis2 * PlaneHandleSize, ltw);
        Vector3 corner = Vector3.Transform((axis1 + axis2) * PlaneHandleSize, ltw);
        int hitId = AxisHitIds[(int)plane];
        context.UIPrimitives.AddLine(end1, corner, color1, hitId);
        context.UIPrimitives.AddLine(corner, end2, color2, hitId);
    }

    private static Vector3 ViewDirection(LevelEditorRenderContext context, Vector3 point)
    {
        Vector3 toPoint = point - context.Camera.EyePosition;
        return context.Camera.IsOrthographic || toPoint.LengthSquared() is 0 ? context.Camera.CameraForward : Vector3.Normalize(toPoint);
    }

    /// <summary>The normal of the plane a dual-axis handle drags in: the axis it leaves out</summary>
    private Vector3 PlaneNormal(EWidgetAxis plane) => plane switch
    {
        EWidgetAxis.XY => LocalRotation.Z.AsVector3(),
        EWidgetAxis.XZ => LocalRotation.Y.AsVector3(),
        EWidgetAxis.YZ => LocalRotation.X.AsVector3(),
        _ => throw new ArgumentOutOfRangeException(nameof(plane))
    };

    /// <summary>Where the ray through a pixel meets the plane a dual-axis drag is in, which goes through where the widget started. Null if it misses</summary>
    private Vector3? PlaneHit(LevelEditorRenderContext context, Vector2 pixel)
    {
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay((int)pixel.X, (int)pixel.Y);
        Vector3 normal = PlaneNormal(CurrentAxis);
        float facing = Vector3.Dot(rayDirection, normal);
        if (MathF.Abs(facing) < 1e-4f) return null;
        float distance = Vector3.Dot(_planeOrigin - rayOrigin, normal) / facing;
        return distance < 0 ? null : rayOrigin + rayDirection * distance;
    }

    /// <summary>
    /// Moves the actor in a dual-axis handle's plane, keeping the point of the plane the mouse grabbed under it. Snapping rounds the move along each axis
    /// </summary>
    private void DragInPlane(LevelEditorRenderContext context, Vector2 mousePos)
    {
        //(the grab is where the drag started, unless the plane was edge-on there)
        if (_planeGrab is null) _planeOrigin = Attach.LocalToWorld.Translation;
        _planeGrab ??= PlaneHit(context, DragStart) ?? PlaneHit(context, mousePos);
        if (_planeGrab is not { } grab || PlaneHit(context, mousePos) is not { } hit) return;
        //into the widget's axes, without the rounding error off the plane
        Vector3 local = Vector3.TransformNormal(hit - grab, Matrix4x4.Transpose(LocalRotation));
        local = CurrentAxis switch
        {
            EWidgetAxis.XY => local with { Z = 0 },
            EWidgetAxis.XZ => local with { Y = 0 },
            _ => local with { X = 0 }
        };
        if (TranslateSnapEnabled) local = SnapVec(local, TranslateSnapValue);
        DragTo(context, _dragStartSnapshot.Location + Vector3.TransformNormal(local, LocalRotation));
    }

    private static bool IsPlane(EWidgetAxis axis) => axis is EWidgetAxis.XY or EWidgetAxis.XZ or EWidgetAxis.YZ;

    /// <summary>How big the widget is drawn, in every mode</summary>
    //(to become a user setting)
    public const float WidgetSize = 2.5f;

    private static float GetScale(LevelEditorRenderContext context, Vector3 origin)
    {
        const float scaleFactor = WidgetSize;
        if (context.Camera.IsOrthographic)
        {
            return context.Camera.OrthoWidth * scaleFactor / context.Width;
        }
        return context.WorldToScreen(origin).W * (scaleFactor / context.Width / context.Camera.ProjectionMatrix[0, 0]);
    }

    private (Vector4 xColor, Vector4 yColor, Vector4 zColor) GetAxisColors()
    {
        return (CurrentAxis.HasFlag(EWidgetAxis.X) ? SelectedColor : XColor,
               CurrentAxis.HasFlag(EWidgetAxis.Y) ? SelectedColor : YColor,
               CurrentAxis.HasFlag(EWidgetAxis.Z) ? SelectedColor : ZColor);
    }

    private void DrawRotator(LevelEditorRenderContext context, Matrix4x4 ltw, Vector3 origin)
    {
        float scale = GetScale(context, origin);

        Vector3 camPos = context.Camera.Position;
        Vector3 camDir;
        if (UseLocalCoords)
        {
            Matrix4x4.Invert(ltw, out var wtl);
            camDir = Vector3.Transform(camPos, wtl);
        }
        else
        {
            camDir = camPos - origin;
        }
        bool xSign = camDir.X > 0;
        bool ySign = camDir.Y > 0;
        bool zSign = camDir.Z > 0;
        const float halfpi = MathF.PI / 2;
        const float pi = MathF.PI;
        //there's probably some formula to do this properly...
        (float xAngle, float yAngle, float zAngle) = (xSign, ySign, zSign) switch
        {
            (true, true, true) => (0, halfpi, 0),
            (true, true, false) => (-halfpi, pi, 0),
            (true, false, true) => (halfpi, halfpi, -halfpi),
            (true, false, false) => (pi, pi, -halfpi),
            (false, false, false) => (pi, -halfpi, pi),
            (false, false, true) => (halfpi, 0, pi),
            (false, true, false) => (-halfpi, -halfpi, halfpi),
            (false, true, true) => (0, 0, halfpi),
        };
        XMatrix = Matrix4x4.CreateRotationX(xAngle) * LocalRotation * Matrix4x4.CreateTranslation(origin);
        YMatrix = Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateRotationY(yAngle) * LocalRotation * Matrix4x4.CreateTranslation(origin);
        ZMatrix = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateRotationZ(zAngle) * LocalRotation * Matrix4x4.CreateTranslation(origin);

        (Vector4 xColor, Vector4 yColor, Vector4 zColor) = GetAxisColors();
        XAxisEnd = DrawRingSegment(context, SweptAngle(EWidgetAxis.X), scale, XMatrix, xColor, AxisHitIds[(int)EWidgetAxis.X]);
        YAxisEnd = DrawRingSegment(context, SweptAngle(EWidgetAxis.Y), scale, YMatrix, yColor, AxisHitIds[(int)EWidgetAxis.Y]);
        ZAxisEnd = DrawRingSegment(context, SweptAngle(EWidgetAxis.Z), scale, ZMatrix, zColor, AxisHitIds[(int)EWidgetAxis.Z]);

        float SweptAngle(EWidgetAxis axis)
        {
            return IsDragging && CurrentAxis == axis ? MathF.PI * 2 : MathF.PI / 2;
        }
    }

    private static Vector2 DrawAxis(LevelEditorRenderContext context, float scale, Matrix4x4 matrix, Vector4 color, int hitId, EAxisType axisType)
    {
        float lineStart = axisType is EAxisType.ReadOnly ? (AxisLength / -2) : 2f;
        float lineEnd = axisType is EAxisType.ReadOnly ? (AxisLength / 2) : AxisLength;

        const int numArrowSegments = 6;
        const float arrowRadius = 6f;
        const float arrowHeight = 12f;

        const float cubeWidth = 5;

        var ltw = Matrix4x4.CreateScale(scale) * matrix;

        var p1 = Vector3.Transform(new Vector3(lineStart, 0, 0), ltw);
        var p2 = Vector3.Transform(new Vector3(lineEnd, 0, 0), ltw);

        context.WorldToPixel(p2, out Vector2 axisEnd);

        context.UIPrimitives.AddLine(p1, p2, color, hitId);

        if (axisType is EAxisType.ReadOnly)
        {
            return axisEnd;
        }

        var mesh = context.UIPrimitives.BuildMesh(color, hitId, ltw);

        if (axisType is EAxisType.Cone)
        {
            //base ring
            for (int i = 0; i < numArrowSegments; i++)
            {
                float theta = MathF.PI * 2f * i / numArrowSegments;
                mesh.AddVertex(lineEnd, arrowRadius * MathF.Sin(theta) * 0.5f, arrowRadius * MathF.Cos(theta) * 0.5f);
            }

            //point
            mesh.AddVertex(lineEnd + arrowHeight, 0, 0);

            for (int s = 0; s < numArrowSegments; s++)
            {
                mesh.AddTriangle(numArrowSegments, s, (s + 1) % numArrowSegments);
            }
        }
        else
        {   //cube
            const float halfCubeWidth = cubeWidth / 2;
            mesh.AddVertex(lineEnd, halfCubeWidth, halfCubeWidth);
            mesh.AddVertex(lineEnd, halfCubeWidth, -halfCubeWidth);
            mesh.AddVertex(lineEnd, -halfCubeWidth, -halfCubeWidth);
            mesh.AddVertex(lineEnd, -halfCubeWidth, halfCubeWidth);
            mesh.AddVertex(lineEnd + cubeWidth, halfCubeWidth, halfCubeWidth);
            mesh.AddVertex(lineEnd + cubeWidth, halfCubeWidth, -halfCubeWidth);
            mesh.AddVertex(lineEnd + cubeWidth, -halfCubeWidth, -halfCubeWidth);
            mesh.AddVertex(lineEnd + cubeWidth, -halfCubeWidth, halfCubeWidth);

            mesh.AddTriangle(0, 3, 2);
            mesh.AddTriangle(2, 1, 0);

            mesh.AddTriangle(0, 1, 4);
            mesh.AddTriangle(4, 1, 5);

            mesh.AddTriangle(4, 5, 6);
            mesh.AddTriangle(6, 7, 4);

            mesh.AddTriangle(7, 6, 2);
            mesh.AddTriangle(2, 3, 7);

            mesh.AddTriangle(5, 1, 2);
            mesh.AddTriangle(2, 6, 5);

            mesh.AddTriangle(0, 4, 7);
            mesh.AddTriangle(7, 3, 0);
        }

        return axisEnd;
    }

    private static Vector2 DrawRingSegment(LevelEditorRenderContext context, float sweptAngle, float scale, Matrix4x4 matrix, Vector4 color, int hitId)
    {
        int numRingSegments = (int)(48 / (MathF.PI / sweptAngle));
        Span<float> radii = [70f, 60f];

        var ltw = Matrix4x4.CreateScale(scale) * matrix;

        var mesh = context.UIPrimitives.BuildMesh(color with { W = color.W / 2 }, hitId, ltw);

        Vector3 prevPoint = default;

        for (int i = 0; i < radii.Length; i++)
        {
            for (int j = 0; j < numRingSegments; j++)
            {
                float theta = sweptAngle / (numRingSegments - 1) * j;
                var point = new Vector3(0, radii[i] * MathF.Sin(theta) * 0.5f, radii[i] * MathF.Cos(theta) * 0.5f);
                mesh.AddVertex(point);
                if (j > 0)
                {
                    context.UIPrimitives.AddLine(Vector3.Transform(prevPoint, ltw), Vector3.Transform(point, ltw), color, hitId);
                }
                prevPoint = point;
            }
        }

        for (int i = 1; i < numRingSegments; i++)
        {
            mesh.AddTriangle(i - 1, i - 1 + numRingSegments, i);
            mesh.AddTriangle(i, i - 1 + numRingSegments, i + numRingSegments);
        }

        context.WorldToPixel(Vector3.Transform(new Vector3(radii[1], 0, 0), ltw), out Vector2 axisEnd);
        return axisEnd;
    }

    public void GetAxisHitProxies(ref USparseArray<IHitProxy> hitProxies)
    {
        foreach (EWidgetAxis axis in Enum.GetValues<EWidgetAxis>())
        {
            AxisHitIds[(int)axis] = hitProxies.Add(new AxisHitProxy(axis));
        }
    }

    public void Drag(LevelEditorRenderContext context, int x, int y)
    {
        if (Attach is null || CurrentAxis is EWidgetAxis.None) return;

        Vector2 mousePos = new(x, y);
        //an Alt+drag only duplicates once it's clearly a drag, so a click makes no copy. Until then the drag stays where it started
        if (duplicateArmed && duplication is null
            && Vector2.Distance(mousePos, DragStart) < System.Windows.SystemParameters.MinimumHorizontalDragDistance) return;
        Vector2 dragDiff = PrevDragPos - mousePos;

        if (Mode is EWidgetMode.Translate && IsPlane(CurrentAxis))
        {
            DragInPlane(context, mousePos);
        }
        else if (Mode is EWidgetMode.Rotate)
        {
            Vector2 startDir = Vector2.Normalize(PrevDragPos - Origin);
            Vector2 mouseDir = Vector2.Normalize(mousePos - Origin);
            float theta = MathF.Atan2(startDir.X * mouseDir.Y - startDir.Y * mouseDir.X, Vector2.Dot(startDir, mouseDir));

            Vector3 rotAxis = CurrentAxis switch
            {
                EWidgetAxis.X => LocalRotation.X.AsVector3(),
                EWidgetAxis.Y => LocalRotation.Y.AsVector3(),
                EWidgetAxis.Z => LocalRotation.Z.AsVector3(),
                _ => Vector3.Zero
            };
            if (Vector3.Dot(rotAxis, context.Camera.Position - Attach.Location) < 0)
            {
                theta = -theta;
            }

            if (RotateSnapEnabled)
            {
                _accumulatedRotation += theta;
                float snapRad = RotateSnapValue * MathF.PI / 180f;
                float snappedAngle = SnapF(_accumulatedRotation, snapRad);
                Matrix4x4 startRot = ActorUtils.ComposeLocalToWorld(Vector3.Zero, _dragStartSnapshot.Rotation, Vector3.One);
                Matrix4x4 deltaRot = CurrentAxis switch
                {
                    EWidgetAxis.X => Matrix4x4.CreateRotationX(snappedAngle),
                    EWidgetAxis.Y => Matrix4x4.CreateRotationY(snappedAngle),
                    EWidgetAxis.Z => Matrix4x4.CreateRotationZ(snappedAngle),
                    _ => Matrix4x4.Identity
                };
                // Local coords: pre-multiply (rotate around actor's own axis)
                // World coords: post-multiply (rotate around world axis)
                Attach.Rotation = (UseLocalCoords ? deltaRot * startRot : startRot * deltaRot).GetRotator();
            }
            else
            {
                Matrix4x4 currentRot = ActorUtils.ComposeLocalToWorld(Vector3.Zero, Attach.Rotation, Vector3.One);
                Matrix4x4 deltaRot = CurrentAxis switch
                {
                    EWidgetAxis.X => Matrix4x4.CreateRotationX(theta),
                    EWidgetAxis.Y => Matrix4x4.CreateRotationY(theta),
                    EWidgetAxis.Z => Matrix4x4.CreateRotationZ(theta),
                    _ => Matrix4x4.Identity
                };
                // Local coords: pre-multiply (rotate around actor's own axis)
                // World coords: post-multiply (rotate around world axis)
                Matrix4x4 newRot = UseLocalCoords ? deltaRot * currentRot : currentRot * deltaRot;
                Attach.Rotation = newRot.GetRotator();
            }
        }
        else
        {
            Vector2 axisEnd = CurrentAxis switch
            {
                EWidgetAxis.X => XAxisEnd,
                EWidgetAxis.Y => YAxisEnd,
                EWidgetAxis.Z => ZAxisEnd,
                EWidgetAxis.XY => dragDiff.X != 0 ? XAxisEnd : YAxisEnd,
                EWidgetAxis.XZ => dragDiff.X != 0 ? XAxisEnd : ZAxisEnd,
                EWidgetAxis.YZ => dragDiff.X != 0 ? YAxisEnd : ZAxisEnd,
                EWidgetAxis.XYZ => dragDiff.X != 0 ? YAxisEnd : ZAxisEnd,
                _ => XAxisEnd
            };

            Vector2 axisDir = Vector2.Normalize(axisEnd - Origin);

            float screenAxisLength = (axisEnd - Origin).Length();
            float dragAmount = screenAxisLength > 0
                ? Vector2.Dot(dragDiff, axisDir) * (GetScale(context, Attach.Location) * AxisLength / screenAxisLength)
                : 0f;

            switch (Mode)
            {
                case EWidgetMode.Translate:
                case EWidgetMode.Scale:
                    Vector3 vecDiff = CurrentAxis switch
                    {
                        EWidgetAxis.X => new Vector3(dragAmount, 0, 0),
                        EWidgetAxis.Y => new Vector3(0, dragAmount, 0),
                        EWidgetAxis.Z => new Vector3(0, 0, dragAmount),
                        EWidgetAxis.XY => throw new NotImplementedException(),
                        EWidgetAxis.XZ => throw new NotImplementedException(),
                        EWidgetAxis.YZ => throw new NotImplementedException(),
                        EWidgetAxis.XYZ => throw new NotImplementedException(),
                        _ => throw new NotImplementedException(),
                    };
                    if (Mode is EWidgetMode.Translate)
                    {
                        if (TranslateSnapEnabled)
                        {
                            _accumulatedTranslationLocal += vecDiff;
                            Vector3 snapped = SnapVec(_accumulatedTranslationLocal, TranslateSnapValue);
                            DragTo(context, _dragStartSnapshot.Location - Vector3.Transform(snapped, LocalRotation));
                        }
                        else
                        {
                            DragTo(context, Attach.Location - Vector3.Transform(vecDiff, LocalRotation));
                        }
                    }
                    else
                    {
                        if (ScaleSnapEnabled)
                        {
                            _accumulatedScale3D += vecDiff / 100;
                            float snapStep = ScaleSnapValue / 100f;
                            Attach.DrawScale3D = _dragStartSnapshot.DrawScale3D - SnapVec(_accumulatedScale3D, snapStep);
                        }
                        else
                        {
                            Attach.DrawScale3D -= vecDiff / 100;
                        }
                    }
                    break;
                case EWidgetMode.UniformScale:
                    if (ScaleSnapEnabled)
                    {
                        _accumulatedScaleUniform += dragAmount / 100;
                        float snapStep = ScaleSnapValue / 100f;
                        Attach.DrawScale = _dragStartSnapshot.DrawScale - SnapF(_accumulatedScaleUniform, snapStep);
                    }
                    else
                    {
                        Attach.DrawScale -= dragAmount / 100;
                    }
                    break;
            }
        }

        PrevDragPos = new Vector2(x, y);
    }

    private static float SnapF(float value, float snapSize) =>
        MathF.Round(value / snapSize) * snapSize;

    private static Vector3 SnapVec(Vector3 v, float snapSize) =>
        new(SnapF(v.X, snapSize), SnapF(v.Y, snapSize), SnapF(v.Z, snapSize));

    /// <summary>
    /// Called when a drag completes with the before and after transform snapshots.
    /// Wired by LevelEditor to push undo actions.
    /// </summary>
    public Action<ActorProxy, TransformSnapshot, TransformSnapshot> OnDragComplete;

    /// <summary>
    /// Starts duplicating the actor an Alt+translation drag is on, when the drag first moves it: returns the duplicate, whose copy the rest of the drag
    /// moves instead, or null if the actor can't be duplicated, which ends the drag. Wired by LevelEditor
    /// </summary>
    public Func<ActorProxy, IActorDuplication> OnDuplicateDragStart;

    /// <summary>Called with the undo step of a completed duplicate drag, in place of <see cref="OnDragComplete"/>. Wired by LevelEditor</summary>
    public Action<IUndoAction> OnDuplicateDragComplete;

    /// <summary>Called with why a duplicate drag couldn't be completed, once its copy is discarded and the drag has ended. Wired by LevelEditor</summary>
    public Action<Exception> OnDuplicateDragFailed;

    //latched when the drag starts, so releasing Alt mid-drag changes nothing
    private bool duplicateArmed;
    private IActorDuplication duplication;
    private ActorProxy duplicateSource;

    /// <param name="duplicate">Whether a translation drag moves a copy of the actor (an Alt+drag)</param>
    public void BeginDrag(int x, int y, bool duplicate = false)
    {
        IsDragging = true;
        duplicateArmed = duplicate && Mode is EWidgetMode.Translate && Attach is not null && OnDuplicateDragStart is not null;
        duplication = null;
        PrevDragPos = DragStart = new Vector2(x, y);
        _accumulatedTranslationLocal = Vector3.Zero;
        _accumulatedRotation = 0f;
        _accumulatedScaleUniform = 0f;
        _accumulatedScale3D = Vector3.Zero;
        _planeGrab = null;
        if (Attach is not null)
        {
            _dragStartSnapshot = Attach.SnapshotTransform();
        }
    }

    /// <summary>
    /// Moves what a translation drag is on to <paramref name="location"/>, as moving the mouse along an axis does. An Alt+drag's first move
    /// duplicates the actor instead, and moves the copy; the copy starts where the actor is, so the drag carries on from where it started.
    /// A spline point's connection to its copy follows the copy to whichever side of the point it's on
    /// </summary>
    public void DragTo(LevelEditorRenderContext context, Vector3 location)
    {
        if (!IsDragging || Attach is null || location == Attach.Location) return;
        if (duplicateArmed && duplication is null)
        {
            duplicateSource = Attach;
            duplication = OnDuplicateDragStart(Attach);
            if (duplication is null)
            {
                context.EndDrag(DragEndReason.Cancelled);
                return;
            }
            Attach = duplication.Copy;
        }
        Attach.Location = location;
        duplication?.Follow();
    }

    public void EndDrag(DragEndReason reason = DragEndReason.Completed)
    {
        if (IsDragging && duplication is not null)
        {
            //the duplicate's step includes moving the copy. Completing one left where its actor is cancels it, as a failure to complete it does
            IUndoAction step = null;
            Exception failure = null;
            try
            {
                if (reason is DragEndReason.Completed) step = duplication.Complete();
                else duplication.Cancel();
            }
            catch (Exception e) when (e is not OutOfMemoryException) { failure = e; }
            if (step is null) Attach = duplicateSource;
            duplication = null;
            duplicateSource = null;
            if (step is not null) OnDuplicateDragComplete?.Invoke(step);
            if (failure is not null) OnDuplicateDragFailed?.Invoke(failure);
        }
        else if (IsDragging && Attach is not null)
        {
            if (reason is DragEndReason.Cancelled)
            {
                //restored while still dragging, so the editor doesn't record it as an edit
                Attach.RestoreTransform(_dragStartSnapshot);
            }
            else
            {
                var afterSnapshot = Attach.SnapshotTransform();
                if (!_dragStartSnapshot.Equals(afterSnapshot))
                {
                    OnDragComplete?.Invoke(Attach, _dragStartSnapshot, afterSnapshot);
                }
            }
        }
        IsDragging = false;
        duplicateArmed = false;
    }
}
