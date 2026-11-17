using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The triangles of one ModelComponent element, as UModelComponent::BuildRenderData builds its index buffer from the model's cooked
/// vertex buffer: a fan per node, a second (reversed) set of vertices for two-sided surfaces, only the sides facing the component's zone,
/// and no portals. Vertices are the element's subset of Model.VertexBuffer, in world space.
/// </summary>
/// <param name="SourceVertices">Each vertex's index in Model.VertexBuffer</param>
public sealed record BspElementGeometry(ModelVertex[] Vertices, int[] SourceVertices, int[] Indices, int DrawnNodes, int SkippedNodes)
{
    public const int PF_TwoSided = 0x100;
    public const int PF_Portal = 0x04000000;

    /// <param name="zoneIndex">The component's ZoneIndex; INDEX_NONE (-1) draws both sides' zones</param>
    public static BspElementGeometry Build(Model model, int zoneIndex, ModelElement element)
    {
        var vertices = new List<ModelVertex>();
        var sources = new List<int>();
        var remap = new Dictionary<int, int>();
        var indices = new List<int>();
        int drawn = 0, skipped = 0;
        foreach (ushort nodeIndex in element.Nodes)
        {
            if (nodeIndex >= model.Nodes.Length) { skipped++; continue; }
            BspNode node = model.Nodes[nodeIndex];
            if (node.iSurf < 0 || node.iSurf >= model.Surfs.Length) { skipped++; continue; }
            int flags = model.Surfs[node.iSurf].PolyFlags;
            // Don't put portal polygons in the static index buffer
            if ((flags & PF_Portal) != 0) { skipped++; continue; }
            int sides = (flags & PF_TwoSided) != 0 ? 2 : 1;
            if (node.iVertexIndex < 0 || node.iVertexIndex + node.NumVertices * sides > model.VertexBuffer.Length) { skipped++; continue; }
            bool any = false;
            for (int backFace = 0; backFace < sides; backFace++)
            {
                int zone = backFace == 0 ? node.iZone1 : node.iZone0;
                if (zone != zoneIndex && zoneIndex != -1) continue;
                int first = node.iVertexIndex + node.NumVertices * backFace;
                for (int v = 2; v < node.NumVertices; v++)
                {
                    indices.Add(Map(first));
                    indices.Add(Map(first + v));
                    indices.Add(Map(first + v - 1));
                }
                any |= node.NumVertices >= 3;
            }
            if (any) drawn++; else skipped++;
        }
        return new BspElementGeometry(vertices.ToArray(), sources.ToArray(), indices.ToArray(), drawn, skipped);

        int Map(int source)
        {
            if (!remap.TryGetValue(source, out int index))
            {
                remap[source] = index = vertices.Count;
                vertices.Add(model.VertexBuffer[source]);
                sources.Add(source);
            }
            return index;
        }
    }
}
