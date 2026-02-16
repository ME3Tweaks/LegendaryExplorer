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

    public override void Draw(LevelEditorRenderContext context)
    {
        if (Attach is null) return;

        var ltw = Attach.LocalToWorld;
        var origin = ltw.Translation;

        context.WorldToPixel(origin, out Origin);

        LocalRotation = UseLocalCoords || Mode is EWidgetMode.Scale ? ActorUtils.ComposeLocalToWorld(Vector3.Zero, Attach.Rotation, Vector3.One) : Matrix4x4.Identity;

        if (Mode is EWidgetMode.Rotate)
        {
            //currently disabled in UI because the rotation math is wrong and causes incorrect rotations when the widget is dragged
            DrawRotator(context, ltw, origin);
        }
        else
        {
            XMatrix = LocalRotation * Matrix4x4.CreateTranslation(origin);
            YMatrix = Matrix4x4.CreateRotationZ(MathF.PI / 2) * LocalRotation * Matrix4x4.CreateTranslation(origin);
            ZMatrix = Matrix4x4.CreateRotationY(-MathF.PI / 2) * LocalRotation * Matrix4x4.CreateTranslation(origin);

            (Vector4 xColor, Vector4 yColor, Vector4 zColor) = GetAxisColors();
            bool useConeGrabber = Mode is EWidgetMode.Translate;
            if (Mode is EWidgetMode.UniformScale)
            {
                yColor = zColor = xColor = CurrentAxis == EWidgetAxis.None ? XColor : SelectedColor;
            }

            float scale = GetScale(context, origin);
            XAxisEnd = DrawAxis(context, scale, XMatrix, xColor, AxisHitIds[(int)EWidgetAxis.X], useConeGrabber);
            YAxisEnd = DrawAxis(context, scale, YMatrix, yColor, AxisHitIds[(int)EWidgetAxis.Y], useConeGrabber);
            ZAxisEnd = DrawAxis(context, scale, ZMatrix, zColor, AxisHitIds[(int)EWidgetAxis.Z], useConeGrabber);
        }
    }

    private static float GetScale(LevelEditorRenderContext context, Vector3 origin)
    {
        const float scaleFactor = 4f;
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

    private static Vector2 DrawAxis(LevelEditorRenderContext context, float scale, Matrix4x4 matrix, Vector4 color, int hitId, bool useConeGrabber)
    {
        const float lineStart = 2f;
        const float lineEnd = 40f;

        const int numArrowSegments = 6;
        const float arrowRadius = 6f;
        const float arrowHeight = 12f;

        const float cubeWidth = 5;

        var ltw = Matrix4x4.CreateScale(scale) * matrix;

        var p1 = Vector3.Transform(new Vector3(lineStart, 0, 0), ltw);
        var p2 = Vector3.Transform(new Vector3(lineEnd, 0, 0), ltw);

        context.WorldToPixel(p2, out Vector2 axisEnd);

        context.Primitives.AddLine(p1, p2, color, hitId);

        var mesh = context.Primitives.BuildMesh(color, hitId, ltw);

        if (useConeGrabber)
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

        var mesh = context.Primitives.BuildMesh(color with { W = color.W / 2 }, hitId, ltw);

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
                    context.Primitives.AddLine(Vector3.Transform(prevPoint, ltw), Vector3.Transform(point, ltw), color, hitId);
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
        Vector2 dragDiff = PrevDragPos - mousePos;

        if (Mode is EWidgetMode.Rotate)
        {
            Vector2 startDir = Vector2.Normalize(PrevDragPos - Origin);
            Vector2 mouseDir = Vector2.Normalize(mousePos - Origin);
            float theta = MathF.Atan2(startDir.X * mouseDir.Y - startDir.Y * mouseDir.X, Vector2.Dot(startDir, mouseDir));

            if (UseLocalCoords)
            {
                switch (CurrentAxis)
                {
                    case EWidgetAxis.X:
                        Attach.Rotation += new Rotator(0, 0, -theta.RadiansToUnrealRotationUnits());
                        break;
                    case EWidgetAxis.Y:
                        Attach.Rotation += new Rotator(-theta.RadiansToUnrealRotationUnits(), 0, 0);
                        break;
                    case EWidgetAxis.Z:
                        Attach.Rotation += new Rotator(0, theta.RadiansToUnrealRotationUnits(), 0);
                        break;
                }
            }
            else
            {
                var axis = LocalRotation.TransformNormal(CurrentAxis switch
                {
                    EWidgetAxis.X => Vector3.UnitX,
                    EWidgetAxis.Y => Vector3.UnitY,
                    EWidgetAxis.Z => Vector3.UnitZ,
                });
                Attach.Rotation += Rotator.FromQuaternion(Quaternion.CreateFromAxisAngle(axis, theta));
            }
        }
        else
        {
            float multiplier = (context.Camera.Position - Attach.Location).Length() / 256;

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


            //dragDiff.Y *= -1;
            Vector2 axisDir = Vector2.Normalize(axisEnd - Origin);

            float dragAmount = Vector2.Dot(dragDiff, axisDir);

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
                    };
                    if (Mode is EWidgetMode.Translate)
                    {
                        vecDiff = Vector3.Transform(vecDiff, LocalRotation);
                        Attach.Location -= vecDiff;
                    }
                    else
                    {
                        Attach.DrawScale3D -= vecDiff / 100;
                    }
                    break;
                case EWidgetMode.UniformScale:
                    Attach.DrawScale -= dragAmount / 100;
                    break;
            }
        }

        PrevDragPos = new Vector2(x, y);
    }

    /// <summary>
    /// Called when a drag completes with the before and after transform snapshots.
    /// Wired by LevelEditor to push undo actions.
    /// </summary>
    public Action<ActorProxy, TransformSnapshot, TransformSnapshot> OnDragComplete;

    public void BeginDrag(int x, int y)
    {
        IsDragging = true;
        PrevDragPos = DragStart = new Vector2(x, y);
        if (Attach is not null)
        {
            _dragStartSnapshot = Attach.SnapshotTransform();
        }
    }

    public void EndDrag()
    {
        if (IsDragging && Attach is not null)
        {
            var afterSnapshot = Attach.SnapshotTransform();
            if (!_dragStartSnapshot.Equals(afterSnapshot))
            {
                OnDragComplete?.Invoke(Attach, _dragStartSnapshot, afterSnapshot);
            }
        }
        IsDragging = false;
    }
}
