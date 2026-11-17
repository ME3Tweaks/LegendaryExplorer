using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>What an asset picker shows a material on</summary>
public enum PreviewShape
{
    Sphere,
    Cube,
    Plane
}

/// <summary>
/// An asset picker's preview, drawn as the Level Editor draws a level's meshes, with the game's shaders: a static mesh with its own materials, or a
/// material on a sphere, cube or plane. Lit by <see cref="MeshRenderContext.Lighting"/>, the light that follows the camera
/// </summary>
public sealed class AssetPreviewScene : IDisposable
{
    public MeshRenderContext Context { get; } = new() { UseGameShaders = true, BackgroundColor = System.Windows.Media.Color.FromRgb(58, 58, 58) };
    private ModelPreview<LEVertex> model;
    //the radius the camera frames, or 0 for the model's bounds'
    private float framedRadius, pendingFramedRadius;
    //what to show next, made when the device exists (UpdateScene), since the control creates it once it's loaded
    private Func<ModelPreview<LEVertex>> pending;
    private bool hasPending;
    private bool loadingShaders;
    private bool disposed;

    public AssetPreviewScene()
    {
        Context.Camera.FirstPerson = false;
        Context.UpdateScene += OnUpdateScene;
        Context.RenderScene += OnRenderScene;
    }

    /// <summary>Shows a static mesh, with its materials</summary>
    public void ShowMesh(ExportEntry staticMesh) => Show(() =>
    {
        StaticMesh mesh = Context.GetStaticMesh(staticMesh);
        return mesh.LODModels.Length == 0 ? null : new ModelPreview<LEVertex>(Context, mesh, 0);
    }, 0);

    /// <summary>Shows a material on a shape</summary>
    public void ShowMaterial(IEntry material, PreviewShape shape) => Show(() =>
    {
        var (vertices, triangles) = Geometry(shape);
        return ModelPreview<LEVertex>.CreateSurface(Context, new Mesh<LEVertex>(Context.Device, triangles, vertices), material, null);
    }, shape switch
    {
        PreviewShape.Sphere => Radius,
        //a square of side 200; its tilted bounding box's sphere is far larger
        PreviewShape.Plane => Radius * MathF.Sqrt(2),
        _ => 0
    });

    /// <summary>
    /// Shows nothing, at once. Then, if the context holds more than <paramref name="maxPackages"/> packages (an asset's file, and those its imports
    /// resolve to), lets them all go: each asset previewed opens its own, which would otherwise be kept until the picker closes
    /// </summary>
    public void Clear(int maxPackages)
    {
        pending = null;
        hasPending = false;
        model?.Dispose();
        model = null;
        Context.ErrorText = null;
        //not while a shader load or another thread reads the packages
        if (!loadingShaders && packageReads == 0 && Context.PackageCache.Cache.Count > maxPackages) Context.EmptyCaches();
    }

    //reads of the context's packages running on other threads (ReadPackages); counted on the UI thread
    private int packageReads;

    /// <summary>
    /// Runs <paramref name="read"/> on another thread with the context's package cache, which <see cref="Clear"/> doesn't empty until it's done, even if
    /// its result is no longer wanted. Called from the UI thread. Closing the picker disposes the context, and its packages, regardless: a read still
    /// running may then throw, or open more packages, which are let go with it; either way its result is dropped. Nothing locks the packages
    /// themselves: the read and what the preview draws may use one at the same time
    /// </summary>
    public async Task<T> ReadPackages<T>(Func<PackageCache, T> read)
    {
        var cache = Context.PackageCache;
        packageReads++;
        try
        {
            return await Task.Run(() => read(cache));
        }
        finally
        {
            packageReads--;
        }
    }

    //a bounding box's sphere is larger than a sphere or plane in it, so they're framed by their own size
    private void Show(Func<ModelPreview<LEVertex>> make, float radius)
    {
        pending = make;
        pendingFramedRadius = radius;
        hasPending = true;
    }

    private void OnUpdateScene(object sender, float deltaTime)
    {
        if (!hasPending || Context.Device is null) return;
        hasPending = false;
        var make = pending;
        pending = null;
        framedRadius = pendingFramedRadius;
        model?.Dispose();
        model = null;
        Context.ErrorText = null;
        try
        {
            model = make?.Invoke();
        }
        catch (Exception e)
        {
            Context.ErrorText = $"Couldn't preview it: {e.Message}";
        }
        if (model is null) return;
        model.UpdateLocalToWorld(Matrix4x4.Identity);
        Frame(model.LODs[0].Mesh.TransformedBounds);
        LoadShaders();
    }

    //a three-quarter view from above, far enough back to see all of it
    private void Frame(BoxSphereBounds bounds)
    {
        var camera = Context.Camera;
        float radius = Math.Max(framedRadius > 0 ? framedRadius : bounds.SphereRadius, 1);
        camera.Position = bounds.Origin;
        camera.Pitch = DefaultPitch;
        camera.Yaw = DefaultYaw;
        camera.FocusDepth = radius / MathF.Sin(camera.FOV / 2) * 1.3f;
        camera.ZNear = Math.Max(0.1f, camera.FocusDepth / 1000);
    }

    // The first load of a material's game shaders takes a moment, so it's done in the background, and the model isn't drawn until it's done: drawing it
    // with LEX's shader meanwhile flickers. A material whose game shaders failed to load is drawn with LEX's shader
    private async void LoadShaders()
    {
        if (loadingShaders || !Context.HasPendingGameShaderLoads) return;
        loadingShaders = true;
        try
        {
            await Task.Run(() => Context.LoadPendingGameShaders());
        }
        catch (Exception e)
        {
            //those that failed are retried, and fall back to LEX's shader, when drawn
            Debug.WriteLine($"Failed to load game shaders: {e}");
        }
        finally
        {
            loadingShaders = false;
        }
        if (disposed) return;
        //another asset was shown meanwhile
        LoadShaders();
    }

    private static readonly RenderPass[] Passes = [RenderPass.Base, RenderPass.Hair, RenderPass.Lighting, RenderPass.Decals, RenderPass.Translucent];

    private void OnRenderScene(object sender, EventArgs e)
    {
        //(drawing it would load them on this thread)
        if (loadingShaders) return;
        foreach (RenderPass pass in Passes)
        {
            model?.Render(pass, Context, 0);
            if (pass is RenderPass.Lighting) Context.EndLightingPass();
        }
    }

    private const int Segments = 48, Rings = 24;
    private const float Radius = 100;
    //the camera's starting view: from three quarters, above
    private const float DefaultPitch = -20 * MathF.PI / 180, DefaultYaw = 45 * MathF.PI / 180;

    /// <summary>
    /// A shape to show a material on, around the origin: a UV sphere of radius 100, its texture once around and once from top to bottom; a cube
    /// of side 140, the texture once on each face, upright on its sides; or a square of side 200, facing the camera as it starts out, the texture upright
    /// </summary>
    public static (List<LEVertex> Vertices, List<Triangle> Triangles) Geometry(PreviewShape shape)
    {
        var vertices = new List<LEVertex>();
        var triangles = new List<Triangle>();
        switch (shape)
        {
            case PreviewShape.Sphere:
                for (int ring = 0; ring <= Rings; ring++)
                {
                    float polar = MathF.PI * ring / Rings;
                    for (int segment = 0; segment <= Segments; segment++)
                    {
                        float azimuth = 2 * MathF.PI * segment / Segments;
                        var normal = new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Sin(polar) * MathF.Sin(azimuth), MathF.Cos(polar));
                        //U increases around it, V down it
                        var uDirection = new Vector3(-MathF.Sin(azimuth), MathF.Cos(azimuth), 0);
                        var vDirection = new Vector3(MathF.Cos(polar) * MathF.Cos(azimuth), MathF.Cos(polar) * MathF.Sin(azimuth), -MathF.Sin(polar));
                        vertices.Add(Vertex(normal * Radius, normal, uDirection, vDirection, new Vector2((float)segment / Segments, (float)ring / Rings)));
                    }
                }
                for (int ring = 0; ring < Rings; ring++)
                {
                    for (int segment = 0; segment < Segments; segment++)
                    {
                        uint a = (uint)(ring * (Segments + 1) + segment), b = a + 1, c = a + Segments + 1, d = c + 1;
                        var outward = Vector3.Normalize(vertices[(int)a].Position + vertices[(int)d].Position);
                        //a pole's ring is one point, so next to it, each quad is one triangle
                        if (ring > 0) AddWound(vertices, triangles, outward, a, b, c);
                        if (ring < Rings - 1) AddWound(vertices, triangles, outward, b, d, c);
                    }
                }
                break;
            case PreviewShape.Cube:
                foreach (var normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
                {
                    //upright on the sides; on the top and bottom, along X and Y
                    Vector3 vDirection = normal.Z == 0 ? -Vector3.UnitZ : Vector3.UnitY;
                    Vector3 uDirection = Vector3.Cross(vDirection, normal);
                    AddQuad(vertices, triangles, normal * 70, normal, uDirection * 140, vDirection * 140);
                }
                break;
            default:
                var camera = new SceneCamera { Pitch = DefaultPitch, Yaw = DefaultYaw };
                AddQuad(vertices, triangles, Vector3.Zero, -camera.CameraForward, camera.CameraRight * 200, -camera.CameraUp * 200);
                break;
        }
        return (vertices, triangles);
    }

    //a square, (0,0) at its corner at -uSide/2 -vSide/2
    private static void AddQuad(List<LEVertex> vertices, List<Triangle> triangles, Vector3 center, Vector3 normal, Vector3 uSide, Vector3 vSide)
    {
        uint first = (uint)vertices.Count;
        for (int v = 0; v <= 1; v++)
        for (int u = 0; u <= 1; u++)
            vertices.Add(Vertex(center + uSide * (u - 0.5f) + vSide * (v - 0.5f), normal, Vector3.Normalize(uSide), Vector3.Normalize(vSide), new Vector2(u, v)));
        AddWound(vertices, triangles, normal, first, first + 1, first + 2);
        AddWound(vertices, triangles, normal, first + 1, first + 3, first + 2);
    }

    //UE3's tangent basis: TangentX along U; TangentZ the normal, its W the sign that makes TangentZ × TangentX point along V
    private static LEVertex Vertex(Vector3 position, Vector3 normal, Vector3 uDirection, Vector3 vDirection, Vector2 uv)
    {
        float sign = Vector3.Dot(Vector3.Cross(normal, uDirection), vDirection) >= 0 ? 1 : -1;
        Fixed4<Vector4> uvs = default;
        uvs[0] = new Vector4(uv, 0, 0);
        return (LEVertex)LEVertex.Create(position, uDirection, new Vector4(normal, sign), uvs);
    }

    //wound as the game's meshes are: the right-hand rule on their order points against their normal (AssetPickingTests measures it)
    private static void AddWound(List<LEVertex> vertices, List<Triangle> triangles, Vector3 normal, uint a, uint b, uint c)
    {
        Vector3 pa = vertices[(int)a].Position;
        bool along = Vector3.Dot(Vector3.Cross(vertices[(int)b].Position - pa, vertices[(int)c].Position - pa), normal) > 0;
        triangles.Add(along ? new Triangle(a, c, b) : new Triangle(a, b, c));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Context.UpdateScene -= OnUpdateScene;
        Context.RenderScene -= OnRenderScene;
        model?.Dispose();
        model = null;
    }
}
