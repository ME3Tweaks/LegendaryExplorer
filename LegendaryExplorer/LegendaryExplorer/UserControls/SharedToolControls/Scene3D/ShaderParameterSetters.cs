using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    internal static class ShaderParameterSetters
    {
        public static void WriteValues(this ref FMaterialVertexShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh)
        {
            p.CameraWorldPosition.WriteVal(buffer, context.Camera.Position);
            //p.ObjectWorldPositionAndRadius.WriteVal(buffer, new Vector4(mesh.AABBCenter, mesh.AABBHalfSize));
        }










        private static unsafe void WriteVal<T>(this FShaderParameter param, Span<byte> buff, T val) where T : unmanaged
        {
            int bytesToWrite = Math.Min(sizeof(T), param.NumBytes);
            val.AsBytes()[..bytesToWrite].CopyTo(buff[param.BufferIndex..]);
        }
    }
}
