using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A mesh that casts a projected shadow: drawn into the shadow map with its own local to world transform
/// </summary>
internal readonly record struct ShadowCaster(Func<Matrix4x4> GetLocalToWorld, Action<DeviceContext> Draw);

/// <summary>
/// What a projected shadow needs to know about the light that casts it
/// </summary>
internal readonly record struct ShadowLight(SceneLightType Type, Vector3 Position, Vector3 Direction, float Radius, float ShadowRadiusMultiplier,
    int MinShadowResolution, int MaxShadowResolution, int ShadowFadeResolution, MEGame Game)
{
    public static ShadowLight FromSceneLight(SceneLight light) => new(light.Type, light.Position, light.Direction, light.Radius, light.ShadowRadiusMultiplier,
        light.MinShadowResolution, light.MaxShadowResolution, light.ShadowFadeResolution, light.Export.Game);
}

/// <summary>
/// A per-object projected shadow: one movable mesh's (or one light environment's meshes') shadow from one light, as UE3's FProjectedShadowInfo.
/// The meshes' depth is rendered from the light into a shadow map, which is then projected onto the scene (see <see cref="LightAttenuationRenderer"/>).
/// </summary>
internal sealed class ProjectedShadowInfo
{
    //BioEngine.ini [SystemSettings], which are the same in each game except for ShadowFilterRadius
    public const int MinShadowResolution = 64;
    public const int MaxShadowResolution = 1280;
    public const int ShadowFadeResolution = 128;
    public const float ShadowFadeExponent = 0.25f;
    public const float ShadowTexelsPerPixel = 1.27324f;

    /// <summary>
    /// ShadowFilterRadius: how far apart the projection's PCF samples are, in shadow map texels
    /// </summary>
    public static float GetShadowFilterRadius(MEGame game) => game.IsGame3() ? 4 : 2;

    public const float ShadowDepthBias = 0.012f;

    /// <summary>
    /// The size of the shadow depth buffer: GSceneRenderTargets.GetShadowDepthTextureResolution, the max shadow resolution
    /// </summary>
    public const int ShadowBufferSize = MaxShadowResolution;

    /// <summary>
    /// Each shadow is rendered inside this border of the shadow depth buffer, which is cleared to the far depth
    /// </summary>
    public const int ShadowBorder = 5;

    /// <summary>
    /// The meshes whose shadow this is
    /// </summary>
    public readonly IReadOnlyList<ShadowCaster> Subject;
    public readonly ShadowLight Light;

    /// <summary>
    /// Added to world positions before transforming them by <see cref="SubjectMatrix"/> or <see cref="ReceiverMatrix"/>
    /// </summary>
    public Vector3 PreShadowTranslation;

    /// <summary>
    /// Translated world space to the shadow map's clip space, covering the subject's depth. Used for both rendering and projecting the shadow
    /// </summary>
    public Matrix4x4 SubjectMatrix;

    /// <summary>
    /// Like <see cref="SubjectMatrix"/>, but extending as far from the light as receivers can be: the frustum the shadow is projected onto
    /// </summary>
    public Matrix4x4 ReceiverMatrix;

    public float MaxSubjectDepth;
    public int Resolution;

    /// <summary>
    /// How strong the shadow is: it fades out as its resolution approaches the minimum. (LE3's projection shader ignores this)
    /// </summary>
    public float FadeAlpha;

    private ProjectedShadowInfo(IReadOnlyList<ShadowCaster> subject, ShadowLight light)
    {
        Subject = subject;
        Light = light;
    }

    /// <summary>
    /// FSceneRenderer::CreateProjectedShadow. Null if the shadow wouldn't be rendered: too small on screen, or the light type doesn't support per-object shadows
    /// </summary>
    /// <param name="bounds">The bounds of all of the subject's meshes</param>
    public static ProjectedShadowInfo Create(IReadOnlyList<ShadowCaster> subject, ShadowLight light, BoxSphereBounds bounds, SceneCamera camera, int viewWidth, int viewHeight)
    {
        var shadow = new ProjectedShadowInfo(subject, light);
        if (!shadow.SetupInitializer(bounds) || !shadow.IsReceiverFrustumInView(camera.ViewMatrix * camera.ProjectionMatrix))
        {
            return null;
        }

        //how many texels the shadow needs to match the screen resolution of its subject
        Matrix4x4 projection = camera.ProjectionMatrix;
        float screenScale = MathF.Max(projection.M11 * 0.5f * viewWidth, projection.M22 * 0.5f * viewHeight);
        float viewDepth = MathF.Max(Vector3.Transform(bounds.Origin, camera.ViewMatrix).Z, 1);
        float unclampedResolution = ShadowTexelsPerPixel * screenScale * bounds.SphereRadius / viewDepth;
        int minResolution = light.MinShadowResolution > 0 ? light.MinShadowResolution : MinShadowResolution;
        int maxResolution = light.MaxShadowResolution > 0 ? light.MaxShadowResolution : MaxShadowResolution;
        int lowerBound = Math.Min(minResolution, ShadowBufferSize - 10);
        int upperBound = Math.Min(maxResolution, ShadowBufferSize) - 10;
        shadow.Resolution = Math.Clamp((int)unclampedResolution, lowerBound, Math.Max(lowerBound, upperBound));

        //shadows fade out as their resolution approaches the minimum, and aren't rendered once they're nearly invisible
        int fadeResolution = light.ShadowFadeResolution > 0 ? light.ShadowFadeResolution : ShadowFadeResolution;
        float fadeAlpha = unclampedResolution > fadeResolution ? 1
            : unclampedResolution > minResolution ? MathF.Pow((unclampedResolution - minResolution) / (fadeResolution - minResolution), ShadowFadeExponent)
            : 0;
        shadow.FadeAlpha = fadeAlpha;
        return fadeAlpha > 1 / 256f ? shadow : null;
    }

    /// <summary>
    /// The lights' GetPerObjectProjectedShadowInitializer, then FProjectedShadowInitializer::CalcTransforms
    /// </summary>
    private bool SetupInitializer(BoxSphereBounds bounds)
    {
        float radius = bounds.SphereRadius;
        if (radius <= 0)
        {
            return false;
        }
        switch (Light.Type)
        {
            case SceneLightType.Point or SceneLightType.Spot:
            {
                //a perspective projection looking at the subject from the light
                Vector3 lightPosition = Light.Position;
                Vector3 lightVector = bounds.Origin - lightPosition;
                float lightDistance = lightVector.Length();
                float silhouetteRadius = 0;
                if (radius < lightDistance)
                {
                    silhouetteRadius = MathF.Min(radius / MathF.Sqrt((lightDistance - radius) * (lightDistance + radius)), 1);
                }
                float closest = radius * Light.ShadowRadiusMultiplier;
                //(the light is also moved out if it's inside the bounds, which a ShadowRadiusMultiplier below 1 allows, since it has no silhouette from there)
                if (lightDistance <= closest || silhouetteRadius <= 0)
                {
                    //make the subject fit in a single < 90 degree FOV projection
                    float distance = MathF.Max(closest, radius);
                    lightVector = (lightDistance > 0 ? Vector3.Normalize(lightVector) : Vector3.UnitX) * distance;
                    lightPosition = bounds.Origin - lightVector;
                    lightDistance = distance;
                    silhouetteRadius = 1;
                }
                Matrix4x4 worldToLight = InverseRotationTo(lightVector / lightDistance) * Matrix4x4.CreateScale(1, 1 / silhouetteRadius, 1 / silhouetteRadius);
                CalcTransforms(-lightPosition, worldToLight, bounds.Origin - lightPosition, radius, new Vector4(0, 0, 1, 0), 0.1f, Light.Radius);
                return true;
            }
            case SceneLightType.Directional:
            {
                //an orthographic projection along the light's direction, centered on the subject
                Matrix4x4 worldToLight = InverseRotationTo(Light.Direction) * Matrix4x4.CreateScale(1, 1 / radius, 1 / radius);
                CalcTransforms(-bounds.Origin, worldToLight, Vector3.Zero, radius, new Vector4(0, 0, 0, 1), -262144, 262144);
                return true;
            }
            default:
                return false;
        }
    }

    /// <param name="preShadowTranslation">Translates world space so that the light (or the subject, for directional lights) is at the origin</param>
    /// <param name="worldToLight">Rotation (and scale) from translated world space to the light's space, where +X points at the subject</param>
    /// <param name="subjectOrigin">The subject's bounds origin in translated world space</param>
    /// <param name="wAxis">(0,0,1,0) for a perspective projection, (0,0,0,1) for orthographic</param>
    private void CalcTransforms(Vector3 preShadowTranslation, Matrix4x4 worldToLight, Vector3 subjectOrigin, float subjectRadius, Vector4 wAxis, float minLightW, float maxLightW)
    {
        PreShadowTranslation = preShadowTranslation;
        //FBasisVectorMatrix(-XAxis, YAxis, FaceDirection), where XAxis and YAxis are FaceDirection.FindBestAxisVectors(). The face direction is +X
        var faceBasis = new Matrix4x4(
            0, 0, 1, 0,
            0, 1, 0, 0,
            -1, 0, 0, 0,
            0, 0, 0, 1);
        Matrix4x4 worldToFace = worldToLight * faceBasis;
        float maxSubjectZ = Vector3.Transform(subjectOrigin, worldToFace).Z + subjectRadius;
        float minSubjectZ = MathF.Max(maxSubjectZ - subjectRadius * 2, minLightW);
        SubjectMatrix = worldToFace * ShadowProjectionMatrix(minSubjectZ, maxSubjectZ, wAxis);
        ReceiverMatrix = worldToFace * ShadowProjectionMatrix(minSubjectZ, maxLightW, wAxis);
        //the depth of the subject's far side
        Matrix4x4.Invert(worldToLight, out Matrix4x4 lightToWorld);
        Vector3 faceDirection = Vector3.TransformNormal(Vector3.UnitX, lightToWorld);
        MaxSubjectDepth = Vector4.Transform(new Vector4(subjectOrigin + faceDirection * subjectRadius, 1), SubjectMatrix).Z;
    }

    //FShadowProjectionMatrix
    private static Matrix4x4 ShadowProjectionMatrix(float minZ, float maxZ, Vector4 wAxis)
    {
        float zScale = (wAxis.Z * maxZ + wAxis.W) / (maxZ - minZ);
        return new Matrix4x4(
            1, 0, 0, wAxis.X,
            0, 1, 0, wAxis.Y,
            0, 0, zScale, wAxis.Z,
            0, 0, -minZ * zScale, wAxis.W);
    }

    //FInverseRotationMatrix(Direction.Rotation()): rotates the direction onto +X (FRotator has no roll)
    private static Matrix4x4 InverseRotationTo(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        float yaw = MathF.Atan2(direction.Y, direction.X);
        float pitch = MathF.Atan2(direction.Z, MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        float cp = MathF.Cos(pitch), sp = MathF.Sin(pitch), cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        //FRotationMatrix's axes
        var x = new Vector3(cp * cy, cp * sy, sp);
        var y = new Vector3(-sy, cy, 0);
        var z = new Vector3(-sp * cy, -sp * sy, cp);
        //the inverse (transpose) has the axes as columns
        return new Matrix4x4(
            x.X, y.X, z.X, 0,
            x.Y, y.Y, z.Y, 0,
            x.Z, y.Z, z.Z, 0,
            0, 0, 0, 1);
    }

    /// <summary>
    /// FProjectedShadowInfo::GetShaderDepthBias: added to the subject's depth in the shadow map
    /// </summary>
    public float DepthBias => MaxShadowResolution * ShadowDepthBias / Resolution;

    /// <summary>
    /// FProjectedShadowInfo::GetScreenToShadowMatrix: from (ScreenPosition.xy * SceneDepth, SceneDepth, 1) to the shadow map's texture coordinates and normalized depth.
    /// The shadow is rendered at the top left of the shadow depth buffer, inside its border
    /// </summary>
    public Matrix4x4 GetScreenToShadowMatrix(Matrix4x4 projection, Matrix4x4 invViewProjection)
    {
        var screenToClip = new Matrix4x4(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, projection.M33, 1,
            0, 0, projection.M43, 0);
        float resolutionFraction = 0.5f * Resolution / ShadowBufferSize;
        float offset = (float)ShadowBorder / ShadowBufferSize + resolutionFraction;
        var clipToTexture = new Matrix4x4(
            resolutionFraction, 0, 0, 0,
            0, -resolutionFraction, 0, 0,
            0, 0, 1 / MaxSubjectDepth, 0,
            offset, offset, 0, 1);
        return screenToClip * invViewProjection * Matrix4x4.CreateTranslation(PreShadowTranslation) * SubjectMatrix * clipToTexture;
    }

    /// <summary>
    /// Whether the shadow could be seen: the receiver frustum isn't entirely outside one of the view's side planes, or behind the camera
    /// </summary>
    private bool IsReceiverFrustumInView(Matrix4x4 viewProjection)
    {
        int allOutside = 0b11111;
        foreach (Vector3 corner in GetReceiverFrustumCorners())
        {
            Vector4 clip = Vector4.Transform(new Vector4(corner, 1), viewProjection);
            int outside = (clip.X < -clip.W ? 1 : 0) | (clip.X > clip.W ? 2 : 0) | (clip.Y < -clip.W ? 4 : 0) | (clip.Y > clip.W ? 8 : 0) | (clip.W <= 0 ? 16 : 0);
            allOutside &= outside;
        }
        return allOutside == 0;
    }

    /// <summary>
    /// The 8 corners of the receiver frustum in world space, indexed by (x, y, z) bits, z = 0 being the near side
    /// </summary>
    public Vector3[] GetReceiverFrustumCorners()
    {
        Matrix4x4.Invert(ReceiverMatrix, out Matrix4x4 invReceiver);
        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            var clip = new Vector4((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : 0, 1);
            Vector4 translated = Vector4.Transform(clip, invReceiver);
            corners[i] = new Vector3(translated.X, translated.Y, translated.Z) / translated.W - PreShadowTranslation;
        }
        return corners;
    }
}
