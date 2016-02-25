using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using MassEffect3.Options;

namespace MassEffect3.PackageDecompress
{
	internal class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		public static void Main(string[] args)
		{
			bool showHelp = false;

			var options = new OptionSet
			{
				{
					"h|help",
					"show this message and exit",
					v => showHelp = v != null
				},
			};

			List<string> extras;

			try
			{
				extras = options.Parse(args);
			}
			catch (OptionException e)
			{
				Console.Write("{0}: ", GetExecutableName());
				Console.WriteLine(e.Message);
				Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
				return;
			}

			if (extras.Count < 1 || extras.Count > 2 ||
				showHelp)
			{
				Console.WriteLine("Usage: {0} [OPTIONS]+ -j input_pcc [output_pcc]", GetExecutableName());
				Console.WriteLine();
				Console.WriteLine("Options:");
				options.WriteOptionDescriptions(Console.Out);
				return;
			}

			string inputPath = extras[0];
			string outputPath = extras.Count >= 2 ? extras[1] : inputPath;
			string tempPath = Path.ChangeExtension(inputPath, "decompressing");

			if (File.Exists(tempPath))
			{
				throw new InvalidOperationException("temporary path already exists?");
			}

			using (FileStream input = File.OpenRead(inputPath))
			{
				uint magic = input.ReadValueU32(Endian.Little);
				if (magic != 0x9E2A83C1 &&
					magic.Swap() != 0x9E2A83C1)
				{
					throw new FormatException("not a package");
				}
				Endian endian = magic == 0x9E2A83C1 ? Endian.Little : Endian.Big;

				ushort versionLo = input.ReadValueU16(endian);
				ushort versionHi = input.ReadValueU16(endian);

				if (versionLo != 684 &&
					versionHi != 194)
				{
					throw new FormatException("unsupported version");
				}

				long headerSize = 8;

				input.Seek(4, SeekOrigin.Current);
				headerSize += 4;

				int folderNameLength = input.ReadValueS32(endian);
				headerSize += 4;

				int folderNameByteLength =
					folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
				input.Seek(folderNameByteLength, SeekOrigin.Current);
				headerSize += folderNameByteLength;

				long packageFlagsOffset = input.Position;
				uint packageFlags = input.ReadValueU32(endian);
				headerSize += 4;

				if ((packageFlags & 0x02000000u) == 0)
				{
					throw new FormatException("package is not flagged as compressed");
				}

				if ((packageFlags & 8) != 0)
				{
					input.Seek(4, SeekOrigin.Current);
					headerSize += 4;
				}

				input.Seek(60, SeekOrigin.Current);
				headerSize += 60;

				uint generationsCount = input.ReadValueU32(endian);
				headerSize += 4;

				input.Seek(generationsCount * 12, SeekOrigin.Current);
				headerSize += generationsCount * 12;

				input.Seek(20, SeekOrigin.Current);
				headerSize += 20;

				uint blockCount = input.ReadValueU32(endian);

				var blockInfos = new CompressedBlockInfo[blockCount];
				for (uint i = 0; i < blockCount; i++)
				{
					blockInfos[i].UncompressedOffset = input.ReadValueU32(endian);
					blockInfos[i].UncompressedSize = input.ReadValueU32(endian);
					blockInfos[i].CompressedOffset = input.ReadValueU32(endian);
					blockInfos[i].CompressedSize = input.ReadValueU32(endian);
				}

				long outputHeaderSize = headerSize + 4 + 8;
				long afterBlockTableOffset = input.Position;

				if (outputHeaderSize != blockInfos.First().UncompressedOffset)
				{
					throw new FormatException();
				}

				using (FileStream output = File.Create(tempPath))
				{
					input.Seek(0, SeekOrigin.Begin);
					output.Seek(0, SeekOrigin.Begin);

					output.WriteFromStream(input, headerSize);
					output.WriteValueU32(0, endian); // block count
					input.Seek(afterBlockTableOffset, SeekOrigin.Begin);
					output.WriteFromStream(input, 8);

					output.Seek(packageFlagsOffset, SeekOrigin.Begin);
					output.WriteValueU32(packageFlags & ~0x02000000u, endian);

					foreach (var blockInfo in blockInfos)
					{
						input.Seek(blockInfo.CompressedOffset, SeekOrigin.Begin);
						uint blockMagic = input.ReadValueU32(endian);
						if (blockMagic != 0x9E2A83C1)
						{
							throw new FormatException("bad compressed block magic");
						}

						uint blockSegmentSize = input.ReadValueU32(endian);
						/*var blockCompressedSize =*/
						input.ReadValueU32(endian);
						uint blockUncompressedSize = input.ReadValueU32(endian);
						if (blockUncompressedSize != blockInfo.UncompressedSize)
						{
							throw new FormatException("uncompressed size mismatch");
						}

						uint segmentCount = ((blockUncompressedSize + blockSegmentSize) - 1) / blockSegmentSize;

						var segmentInfos = new CompressedSegmentInfo[segmentCount];
						for (uint i = 0; i < segmentCount; i++)
						{
							segmentInfos[i].CompressedSize = input.ReadValueU32(endian);
							segmentInfos[i].UncompressedSize = input.ReadValueU32(endian);
						}

						if (segmentInfos.Sum(si => si.UncompressedSize) != blockInfo.UncompressedSize)
						{
							throw new FormatException("uncompressed size mismatch");
						}

						output.Seek(blockInfo.UncompressedOffset, SeekOrigin.Begin);
						foreach (var segmentInfo in segmentInfos)
						{
							using (MemoryStream temp = input.ReadToMemoryStream(segmentInfo.CompressedSize))
							{
								var zlib = new InflaterInputStream(temp);
								output.WriteFromStream(zlib, segmentInfo.UncompressedSize);
							}
						}
					}
				}
			}

			File.Delete(outputPath);
			File.Move(tempPath, outputPath);
		}
	}
}