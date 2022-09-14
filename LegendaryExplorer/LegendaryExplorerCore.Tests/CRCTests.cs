using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;
using LegendaryExplorerCore.Textures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class CRCTests
    {
        [TestMethod]
        public void TestTextureCRC()
        {
            GlobalTest.Init();

            var files = Directory.GetFiles(GlobalTest.GetTestTexturesDirectory(), "*.*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                var bytes = File.ReadAllBytes(file);

                var standardCRC = ~BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(bytes));
                var LEC_CRC = TextureCRC.Compute(bytes);
                Assert.AreEqual(standardCRC, LEC_CRC, "LEC's CRC32 does not match System.Hashing.IO's!");
            }
        }
    }
}
