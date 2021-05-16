using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Shaders
{
    public static class ShaderCacheReader
    {
        public static string GlobalShaderFileName(MEGame game) => game.IsLEGame() ? "RefShaderCache-PC-D3D-SM5.upk" : "RefShaderCache -PC-D3D-SM3.upk";
        private static string shaderfilePath(MEGame game) => Path.Combine(MEDirectories.GetCookedPath(game), GlobalShaderFileName(game));

        private static Dictionary<Guid, int> ME3ShaderOffsets;
        private static Dictionary<Guid, int> ME2ShaderOffsets;
        private static Dictionary<Guid, int> ME1ShaderOffsets;

        private static Dictionary<Guid, int> LE3ShaderOffsets;
        private static Dictionary<Guid, int> LE2ShaderOffsets;
        private static Dictionary<Guid, int> LE1ShaderOffsets;
        private static Dictionary<Guid, int> ShaderOffsets(MEGame game) => game switch
        {
            MEGame.ME3 => ME3ShaderOffsets,
            MEGame.ME2 => ME2ShaderOffsets,
            MEGame.ME1 => ME1ShaderOffsets,
            MEGame.LE3 => LE3ShaderOffsets,
            MEGame.LE2 => LE2ShaderOffsets,
            MEGame.LE1 => LE1ShaderOffsets,
            _ => null
        };

        private static int ME3MaterialShaderMapsOffset;
        private static int ME2MaterialShaderMapsOffset;
        private static int ME1MaterialShaderMapsOffset;

        private static int LE3MaterialShaderMapsOffset;
        private static int LE2MaterialShaderMapsOffset;
        private static int LE1MaterialShaderMapsOffset;
        private static int MaterialShaderMapsOffset(MEGame game) => game switch
        {
            MEGame.ME3 => ME3MaterialShaderMapsOffset,
            MEGame.ME2 => ME2MaterialShaderMapsOffset,
            MEGame.ME1 => ME1MaterialShaderMapsOffset,
            MEGame.LE3 => LE3MaterialShaderMapsOffset,
            MEGame.LE2 => LE2MaterialShaderMapsOffset,
            MEGame.LE1 => LE1MaterialShaderMapsOffset,
            _ => 0
        };

        private static void populateOffsets(MEGame game, int binaryOffset)
        {
            string filePath = shaderfilePath(game);
            if (File.Exists(filePath))
            {
                Dictionary<Guid, int> offsetDict = game switch
                {
                    MEGame.ME3 => ME3ShaderOffsets = new Dictionary<Guid, int>(),
                    MEGame.ME2 => ME2ShaderOffsets = new Dictionary<Guid, int>(),
                    MEGame.ME1 => ME1ShaderOffsets = new Dictionary<Guid, int>(),
                    MEGame.LE3 => LE3ShaderOffsets = new Dictionary<Guid, int>(),
                    MEGame.LE2 => LE2ShaderOffsets = new Dictionary<Guid, int>(),
                    MEGame.LE1 => LE1ShaderOffsets = new Dictionary<Guid, int>(),
                    _ => null
                };
                if (offsetDict == null || offsetDict.Count > 0) return;

                using FileStream fs = File.OpenRead(filePath);
                fs.JumpTo(binaryOffset);
                fs.Skip(1);
                int nameCount = fs.ReadInt32();
                fs.Skip(nameCount * 12);
                if (game is not MEGame.ME2)
                {
                    nameCount = fs.ReadInt32();
                    fs.Skip(nameCount * 12);
                }

                int shaderCount = fs.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    fs.Skip(8);
                    Guid shaderGuid = fs.ReadGuid();
                    int shaderEndOffset = fs.ReadInt32();
                    offsetDict.Add(shaderGuid, (int)fs.Position + 2);
                    fs.Skip(shaderEndOffset - fs.Position);
                }

                if (game != MEGame.ME1)
                {
                    nameCount = fs.ReadInt32();
                    fs.Skip(nameCount * 12);
                }
                switch (game)
                {
                    case MEGame.ME3:
                        ME3MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.ME2:
                        ME2MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.ME1:
                        ME1MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.LE3:
                        LE3MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.LE2:
                        LE2MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.LE1:
                        LE1MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                }
            }
        }

        public static MaterialShaderMap GetMaterialShaderMap(MEGame game, StaticParameterSet staticParameterSet)
        {
            string filePath = shaderfilePath(game);
            if (File.Exists(filePath))
            {
                using IMEPackage shaderCachePackage = MEPackageHandler.OpenMEPackage(filePath);
                int shaderCacheOffset = shaderCachePackage.Exports[0].DataOffset + 12;
                populateOffsets(game, shaderCacheOffset);

                using FileStream fs = File.OpenRead(filePath);
                var sc = new SerializingContainer2(fs, shaderCachePackage, true);
                sc.ms.JumpTo(MaterialShaderMapsOffset(game));

                int count = fs.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    StaticParameterSet sps = null;
                    sc.Serialize(ref sps);
                    if (sps == staticParameterSet)
                    {
                        MaterialShaderMap msm = null;
                        sc.Serialize(ref msm);
                        return msm;
                    }

                    if (game >= MEGame.ME3)
                    {
                        sc.ms.Skip(8);
                    }

                    int nextMSMOffset = sc.ms.ReadInt32();
                    sc.ms.Skip(nextMSMOffset - sc.ms.Position);
                }
            }

            return null;
        }

        public static string GetShaderDissasembly(MEGame game, Guid shaderGuid)
        {
            Dictionary<Guid, int> offsets = ShaderOffsets(game);
            if (offsets != null && offsets.TryGetValue(shaderGuid, out int offset))
            {
                using FileStream fs = File.OpenRead(shaderfilePath(game));
                fs.JumpTo(offset);
                int size = fs.ReadInt32();
                ShaderReader.DisassembleShader(fs.ReadToBuffer(size), out string disassembly);
                return disassembly;
            }

            return "";
        }
    }
}
