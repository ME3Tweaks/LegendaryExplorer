using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.SFXArchive;
using MassEffect3.Options;
using MassEffect3.ProjectData;

namespace MassEffect3.SFXArchiveUnpack
{
	internal class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);
		}

		public static void Main(string[] args)
		{
			bool showHelp = false;
			bool extractUnknowns = true;
			bool overwriteFiles = false;
			bool verbose = false;

			var options = new OptionSet
			{
				{
					"o|overwrite",
					"overwrite existing files",
					v => overwriteFiles = v != null
				},
				{
					"nu|no-unknowns",
					"don't extract unknown files",
					v => extractUnknowns = v == null
				},
				{
					"v|verbose",
					"be verbose",
					v => verbose = v != null
				},
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

			if (extras.Count < 1 || extras.Count > 2 || showHelp)
			{
				Console.WriteLine("Usage: {0} [OPTIONS]+ input_sfar [output_dir]", GetExecutableName());
				Console.WriteLine();
				Console.WriteLine("Options:");
				options.WriteOptionDescriptions(Console.Out);
				return;
			}

			string inputPath = extras[0];
			string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, null);

			Manager manager = Manager.Load();
			if (manager.ActiveProject == null)
			{
				Console.WriteLine("Warning: no active project loaded.");
			}
			HashList<FileNameHash> hashes = manager.LoadLists(
				"*.filelist",
				FileNameHash.Compute,
				s => s.Replace("\\", "/"));

			using (FileStream input = File.OpenRead(inputPath))
			{
				var sfx = new SFXArchiveFile();
				sfx.Deserialize(input);

				long current = 0;
				long total = sfx.Entries.Count;
				int padding = total.ToString(CultureInfo.InvariantCulture).Length;

				if (sfx.CompressionScheme != CompressionScheme.None &&
					sfx.CompressionScheme != CompressionScheme.Lzma &&
					sfx.CompressionScheme != CompressionScheme.Lzx)
				{
					Console.WriteLine("Unsupported compression scheme!");
					return;
				}

				var inputBlock = new byte[sfx.MaximumBlockSize];
				var outputBlock = new byte[sfx.MaximumBlockSize];

				var hashesFromFile = new Dictionary<FileNameHash, string>();

				// todo: figure out what the file name is
				var fileNameListNameHash = new FileNameHash(
					new byte[]
					{
						0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C,
					});
				Entry fileNameListEntry = sfx.Entries
					.FirstOrDefault(e =>
						e.NameHash == fileNameListNameHash);
				if (fileNameListEntry != null)
				{
					using (var temp = new MemoryStream())
					{
						DecompressEntry(
							sfx, fileNameListEntry,
							input, inputBlock,
							temp, outputBlock);
						temp.Position = 0;

						var reader = new StreamReader(temp);
						while (reader.EndOfStream == false)
						{
							string line = reader.ReadLine();
							hashesFromFile.Add(FileNameHash.Compute(line), line);
						}
					}
				}

				foreach (var entry in sfx.Entries)
				{
					current++;

					string entryName = hashes[entry.NameHash];

					if (entryName == null)
					{
						if (hashesFromFile.ContainsKey(entry.NameHash))
						{
							entryName = hashesFromFile[entry.NameHash];
						}
					}

					if (entryName == null)
					{
						if (extractUnknowns == false)
						{
							continue;
						}

						entryName = entry.NameHash.ToString();
						entryName = Path.Combine("__UNKNOWN", entryName);
					}
					else
					{
						entryName = entryName.Replace("/", "\\");
						if (entryName.StartsWith("\\"))
						{
							entryName = entryName.Substring(1);
						}
					}

					string entryPath = Path.Combine(outputPath, entryName);
					if (overwriteFiles == false &&
						File.Exists(entryPath))
					{
						continue;
					}

					if (verbose)
					{
						Console.WriteLine("[{0}/{1}] {2}",
							current.ToString(CultureInfo.InvariantCulture).PadLeft(padding), total, entryName);
					}

					input.Seek(entry.Offset, SeekOrigin.Begin);

					Directory.CreateDirectory(Path.GetDirectoryName(entryPath));
					using (FileStream output = File.Create(entryPath))
					{
						DecompressEntry(
							sfx, entry,
							input, inputBlock,
							output, outputBlock);
					}
				}
			}
		}

		private static void DecompressEntry(
			SFXArchiveFile sfx,
			Entry entry,
			Stream input,
			byte[] inputBlock,
			Stream output,
			byte[] outputBlock)
		{
			long left = entry.UncompressedSize;
			input.Seek(entry.Offset, SeekOrigin.Begin);

			if (entry.BlockSizeIndex == -1)
			{
				output.WriteFromStream(input, entry.UncompressedSize);
			}
			else
			{
				int blockSizeIndex = entry.BlockSizeIndex;
				while (left > 0)
				{
					uint compressedBlockSize = sfx.BlockSizes[blockSizeIndex];
					if (compressedBlockSize == 0)
					{
						compressedBlockSize = sfx.MaximumBlockSize;
					}

					if (sfx.CompressionScheme == CompressionScheme.None)
					{
						output.WriteFromStream(input, compressedBlockSize);
						left -= compressedBlockSize;
					}
					else if (sfx.CompressionScheme == CompressionScheme.Lzma)
					{
						if (compressedBlockSize == sfx.MaximumBlockSize ||
							compressedBlockSize == left)
						{
							output.WriteFromStream(input, compressedBlockSize);
							left -= compressedBlockSize;
						}
						else
						{
							var uncompressedBlockSize = (uint) Math.Min(
								left, sfx.MaximumBlockSize);

							if (compressedBlockSize < 5)
							{
								throw new InvalidOperationException();
							}

							byte[] properties = input.ReadBytes(5);
							compressedBlockSize -= 5;

							if (input.Read(inputBlock, 0, (int) compressedBlockSize)
								!= compressedBlockSize)
							{
								throw new EndOfStreamException();
							}

							uint actualUncompressedBlockSize = uncompressedBlockSize;
							uint actualCompressedBlockSize = compressedBlockSize;

							LZMA.ErrorCode error = LZMA.Decompress(
								outputBlock,
								ref actualUncompressedBlockSize,
								inputBlock,
								ref actualCompressedBlockSize,
								properties,
								(uint) properties.Length);

							if (error != LZMA.ErrorCode.Ok ||
								uncompressedBlockSize != actualUncompressedBlockSize ||
								compressedBlockSize != actualCompressedBlockSize)
							{
								throw new InvalidOperationException();
							}

							output.Write(outputBlock, 0, (int) actualUncompressedBlockSize);
							left -= uncompressedBlockSize;
						}
					}
					else
					{
						throw new NotImplementedException();
					}

					blockSizeIndex++;
				}
			}
		}
	}
}