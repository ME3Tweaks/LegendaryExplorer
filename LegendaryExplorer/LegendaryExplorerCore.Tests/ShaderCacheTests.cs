using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ShaderCacheTests
    {
        [TestMethod]
        public void TestGlobalShaderCacheReserialization()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var binsPath = GlobalTest.GetGlobalShaderCachesDirectory();
            var bins = Directory.GetFiles(binsPath, "*.bin", SearchOption.AllDirectories);
            foreach (var p in bins)
            {
                using var input = new MemoryStream(File.ReadAllBytes(p));
                var inCache = ShaderCache.ReadGlobalShaderCache(input, MEGame.LE3);
                var outS = new MemoryStream();
                ShaderCache.GlobalShaderCacheSerializingContainer container = new(outS, null)
                {
                    ActualGame = MEGame.LE3
                };
                inCache.WriteTo(container);
#if DEBUG
                if (outS.Length != input.Length)
                {
                    DebugTools.DebugUtilities.CompareByteArrays(input.ToArray(), outS.ToArray());
                }
#endif

                Assert.IsTrue(outS.ToArray().SequenceEqual(input.ToArray()), $"Serialization of GlobalShaderCache failed - data size or contents did not match. Source length: {input.Length}, Output length: {outS.Length}");
            }
        }
    }
}