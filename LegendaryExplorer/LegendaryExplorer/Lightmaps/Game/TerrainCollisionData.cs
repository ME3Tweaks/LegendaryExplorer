using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// What a TerrainComponent saves of its section of the height field for collision and culling, made as UE3's editor makes it when the terrain is saved
/// (UTerrainComponent::PreSave): its collision vertices, the bounding-volume tree line checks use, and its patches' height bounds. The game builds the
/// section's PhysX height field from the collision vertices at load (UTerrainComponent::InitComponentRBPhys), and has no physics for a section without them.
/// Verified to produce identical data as BW's cooker.
/// </summary>
public static class TerrainCollisionData
{
    //FTerrainBVTree: a region no larger than this many quads a side is a leaf
    private const int MaxLeafRegionSize = 2;
    private const ushort NullNode = ushort.MaxValue;

    /// <summary>
    /// Writes each of the terrain's components' collision vertices and BV tree for the terrain at <paramref name="terrainLocalToWorld"/>, as
    /// UTerrainComponent::BuildCollisionData makes them. Every one is made before any is written, and nothing is written if the height field can't be
    /// read, a TerrainComponents entry isn't an export, or a component isn't the terrain's own section within its height field: the refusal names it
    /// </summary>
    /// <param name="readProperties">A component's properties, with those it takes from its archetypes (the section's size and base)</param>
    /// <param name="collides"><see cref="HasCollision"/>: a terrain that doesn't collide has no BV trees</param>
    public static void WriteCollision(TerrainHeightField field, Func<ExportEntry, PropertyCollection> readProperties, Matrix4x4 terrainLocalToWorld, bool collides)
    {
        if (!field.IsValid)
            throw new InvalidOperationException($"{field.Export.InstancedFullPath}'s height field can't be read, so its collision can't be made again.");
        //(UE3 skips a null entry, which has no collision to go stale; anything else that ReadSections can't resolve would)
        foreach (ObjectProperty entry in field.Properties.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents") ?? Enumerable.Empty<ObjectProperty>())
        {
            if (entry.Value != 0 && !field.Export.FileRef.TryGetUExport(entry.Value, out _))
                throw new InvalidOperationException($"{field.Export.InstancedFullPath}'s TerrainComponents refers to #{entry.Value}, which isn't an export, so its collision can't be made again.");
        }
        List<TerrainSection> sections = field.ReadSections(readProperties);
        foreach (TerrainSection section in sections)
        {
            if (section.Component.Parent != field.Export || !field.Contains(section))
                throw new InvalidOperationException($"{section.Component.InstancedFullPath} isn't a section within {field.Export.InstancedFullPath}'s height field, so its collision can't be made again.");
        }
        var made = new List<(ExportEntry Component, TerrainComponent Binary)>(sections.Count);
        foreach (TerrainSection section in sections)
        {
            var binary = section.Component.GetBinaryData<TerrainComponent>();
            binary.CollisionVertices = CollisionVertices(field, section, terrainLocalToWorld);
            binary.BVTree = BVTree(field, section, binary.CollisionVertices, collides);
            made.Add((section.Component, binary));
        }
        foreach (var (component, binary) in made) component.WriteBinary(binary);
    }

    /// <summary>
    /// Whether the terrain's CollisionType isn't COLLIDE_NoCollision, which is what decides if UTerrainComponent::BuildCollisionData builds BV trees.
    /// CollisionType isn't saved: AActor::SetDefaultCollisionType works it out at load, as no collision when neither the actor collides (bCollideActors)
    /// nor its CollisionComponent, if it has one, blocks rigid bodies. Terrains have none by default
    /// </summary>
    /// <param name="properties">The terrain's own properties</param>
    public static bool HasCollision(ExportEntry terrain, PropertyCollection properties, PackageCache cache)
    {
        if ((properties.GetProp<BoolProperty>("bCollideActors")
             ?? EffectiveLightingProperties.ClassDefaultProperty<BoolProperty>(terrain, "bCollideActors", cache))?.Value == true)
            return true;
        return properties.GetProp<ObjectProperty>("CollisionComponent")?.ResolveToEntry(terrain.FileRef) is ExportEntry component
               && EffectiveLightingProperties.ReadPrimitive(component, cache).GetProp<BoolProperty>("BlockRigidBody")?.Value == true;
    }

    /// <summary>
    /// UTerrainComponent::GetCollisionData's vertices: the section's vertices at maximum tessellation, (TrueSizeX + 1) per row, in world space (where
    /// <paramref name="terrainLocalToWorld"/> puts the terrain)
    /// </summary>
    public static Vector3[] CollisionVertices(TerrainHeightField field, TerrainSection section, Matrix4x4 terrainLocalToWorld)
    {
        var vertices = new Vector3[(section.TrueSizeX + 1) * (section.TrueSizeY + 1)];
        int i = 0;
        for (int y = 0; y <= section.TrueSizeY; y++)
        {
            int globalY = section.BaseY + y;
            for (int x = 0; x <= section.TrueSizeX; x++)
            {
                int globalX = section.BaseX + x;
                //ATerrain::GetCollisionVertex at a patch's corner: its bicubic patch sampled there is the vertex's own height
                var local = new Vector3(globalX, globalY, (-32768f + field.Height(globalX, globalY)) * TerrainHeightField.ZScale);
                vertices[i++] = TransformPosition(local, terrainLocalToWorld);
            }
        }
        return vertices;
    }

    /// <summary>FMatrix::TransformFVector as the game computes it: x * row 0 + y * row 1 + z * row 2 + row 3</summary>
    private static Vector3 TransformPosition(Vector3 v, Matrix4x4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43);

    /// <summary>
    /// FTerrainBVTree::Build: the section's quads split into quarters (FTerrainBVNode::SplitTerrain) until a region is at most two quads a side, nodes in
    /// the order they're made, the root first. A quarter with no visible quad has no node. Each node's bounds are its vertices' (<paramref name="vertices"/>,
    /// from <see cref="CollisionVertices"/>): unlike UE3's, shipping trees' aren't expanded by FUDGE_SIZE. A terrain that doesn't collide
    /// (<paramref name="collides"/>: bCollideActors, which CollisionType sets) has no tree. The float each node is saved with after its region is left 0:
    /// in shipping files it's uninitialised memory, mostly 0
    /// </summary>
    public static TerrainBVNode[] BVTree(TerrainHeightField field, TerrainSection section, Vector3[] vertices, bool collides)
    {
        if (!collides) return [];
        var nodes = new List<TerrainBVNode>(section.TrueSizeX * section.TrueSizeY * 2) { new() };
        Split(0, 0, 0, section.TrueSizeX, section.TrueSizeY);
        return [.. nodes];

        void Split(int index, int xPos, int yPos, int xSize, int ySize)
        {
            var node = nodes[index];
            var bounds = new Box();
            for (int y = yPos; y < yPos + ySize + 1; y++)
            for (int x = xPos; x < xPos + xSize + 1; x++)
                bounds.Add(vertices[y * (section.TrueSizeX + 1) + x]);
            node.BoundingVolume = bounds;
            if (xSize <= MaxLeafRegionSize && ySize <= MaxLeafRegionSize)
            {
                node.bIsLeaf = true;
                (node.XPos, node.YPos, node.XSize, node.YSize) = ((ushort)xPos, (ushort)yPos, (ushort)xSize, (ushort)ySize);
                return;
            }
            node.bIsLeaf = false;
            //  Y
            //  ^
            //  |C  D
            //  |A  B
            //   --> X
            int aSize = Math.Max(xSize / 2, ySize / 2);
            int aX = Math.Min(aSize, xSize), aY = Math.Min(aSize, ySize);
            int bX = xSize > aSize ? xSize - aSize : 0, cY = ySize > aSize ? ySize - aSize : 0;
            (node.NodeIndex0, node.NodeIndex1, node.NodeIndex2, node.NodeIndex3) = (NullNode, NullNode, NullNode, NullNode);
            if (HasVisibleQuad(xPos, yPos, aX, aY)) node.NodeIndex0 = Child(xPos, yPos, aX, aY);
            if (bX > 0 && aY > 0 && HasVisibleQuad(xPos + aSize, yPos, bX, aY)) node.NodeIndex1 = Child(xPos + aSize, yPos, bX, aY);
            if (aX > 0 && cY > 0 && HasVisibleQuad(xPos, yPos + aSize, aX, cY)) node.NodeIndex2 = Child(xPos, yPos + aSize, aX, cY);
            if (bX > 0 && cY > 0 && HasVisibleQuad(xPos + aSize, yPos + aSize, bX, cY)) node.NodeIndex3 = Child(xPos + aSize, yPos + aSize, bX, cY);
        }

        ushort Child(int xPos, int yPos, int xSize, int ySize)
        {
            nodes.Add(new TerrainBVNode());
            int index = nodes.Count - 1;
            Split(index, xPos, yPos, xSize, ySize);
            return (ushort)index;
        }

        //CollisionInRegion
        bool HasVisibleQuad(int xPos, int yPos, int xSize, int ySize)
        {
            for (int y = yPos; y < yPos + ySize; y++)
            for (int x = xPos; x < xPos + xSize; x++)
                if (field.IsQuadVisible(section.BaseX + x, section.BaseY + y)) return true;
            return false;
        }
    }

    /// <summary>
    /// UTerrainComponent::UpdatePatchBounds: for each of the section's patches, row by row, the lowest and highest of its vertices' terrain-local heights.
    /// MaxDisplacement is as UE3 works it out from those vertices' offsets from the patch's corner, which comes to MaxTesselationLevel - 1
    /// </summary>
    public static TerrainPatchBounds[] PatchBounds(TerrainHeightField field, TerrainSection section)
    {
        int tessellation = field.MaxTesselationLevel;
        var bounds = new TerrainPatchBounds[section.SizeX * section.SizeY];
        int i = 0;
        for (int y = 0; y < section.SizeY; y++)
        for (int x = 0; x < section.SizeX; x++)
        {
            int globalX = section.BaseX + x * tessellation, globalY = section.BaseY + y * tessellation;
            var patch = new TerrainPatchBounds { MinHeight = 32768f * TerrainHeightField.ZScale, MaxHeight = -32768f * TerrainHeightField.ZScale, MaxDisplacement = 0 };
            for (int subY = 0; subY <= tessellation; subY++)
            for (int subX = 0; subX <= tessellation; subX++)
            {
                float vertexX = globalX + subX, vertexY = globalY + subY;
                float z = (-32768f + field.Height(globalX + subX, globalY + subY)) * TerrainHeightField.ZScale;
                patch.MinHeight = MathF.Min(patch.MinHeight, z);
                patch.MaxHeight = MathF.Max(patch.MaxHeight, z);
                patch.MaxDisplacement = MathF.Max(patch.MaxDisplacement,
                    MathF.Max(MathF.Max(vertexX - globalX - 1f, globalX - vertexX), MathF.Max(vertexY - globalY - 1f, globalY - vertexY)));
            }
            bounds[i++] = patch;
        }
        return bounds;
    }
}
