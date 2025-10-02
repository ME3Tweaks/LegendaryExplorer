using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Diagnostics;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ShaderCacheTests
    {
        [TestMethod]
        public void TestGlobalShaderCacheReserialization()
        {
            GlobalTest.Init();
            var binsPath = GlobalTest.GetGlobalShaderCachesDirectory();
            var bins = Directory.GetFiles(binsPath, "*.bin", SearchOption.AllDirectories);
            foreach (var shaderCacheFile in bins)
            {
                Debug.WriteLine($"GlobalShaderCache Serialization Test: {shaderCacheFile}");
                MEGame game = Enum.Parse<MEGame>(Path.GetFileNameWithoutExtension(shaderCacheFile));
                using var input = new MemoryStream(File.ReadAllBytes(shaderCacheFile));
                var inCache = GlobalShaderCache.ReadGlobalShaderCache(input, game);
                var outS = new MemoryStream();
                var container = new PackagelessSerializingContainer(outS, null)
                {
                    Game = game
                };
                inCache.WriteTo(container);
#if DEBUG
                if (outS.Length != input.Length)
                {
                    DebugTools.DebugUtilities.CompareByteArrays(input.ToArray(), outS.ToArray());
                }
#endif

                Assert.IsTrue(outS.ToArray().SequenceEqual(input.ToArray()), $"Serialization of {game} GlobalShaderCache failed - data size or contents did not match. Source length: {input.Length}, Output length: {outS.Length}");
            }
        }

        [TestMethod]
        public void TestPackagelessShaderCacheDeserializatin()
        {
            GlobalTest.Init();
            var binsPath = GlobalTest.GetPackagelessShaderCachesDirectory();
            var bins = Directory.GetFiles(binsPath, "*.bin", SearchOption.AllDirectories);
            foreach (var shaderCacheFile in bins)
            {
                Debug.WriteLine($"PackagelessShaderCache Serialization Test: {shaderCacheFile}");
                using var input = new MemoryStream(File.ReadAllBytes(shaderCacheFile));
                var container = new PackagelessSerializingContainer(input, null, true)
                {
                    Game = MEGame.LE3
                };

                ShaderCache cache = new ShaderCache() { Packageless = true };
                // If it crashes and dies here, then deserialization probably failed.
                cache.PublicSerialize(container);

            }
        }
    }
}