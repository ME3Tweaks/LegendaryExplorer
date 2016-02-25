using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gammtek.Conduit.Extensions.IO;

namespace MassEffect3.TocEditor
{
	public class TocHandler
	{
		public bool Changed = false;
		public List<Chunk> ChunkList;

		public string GamePath;
		public string TocFilePath;

		public TocHandler(string tocFileName, string gamePath)
		{
			using (var tocStream = File.OpenRead(tocFileName))
			{
				if (tocStream.ReadUInt32() != 0x3AB70C13)
				{
					throw new Exception("not a toc.bin file");
				}

				TocFilePath = Path.GetFullPath(tocFileName);
				GamePath = gamePath;

				ChunkList = new List<Chunk>();

				tocStream.Seek(8, SeekOrigin.Begin);

				var numChunks = tocStream.ReadInt32();
				
				for (var i = 0; i < numChunks; i++)
				{
					var newChunk = new Chunk();
					newChunk.Offset = tocStream.Position;
					newChunk.RelPosition = tocStream.ReadInt32();
					var countBlockFiles = tocStream.ReadInt32();

					if (countBlockFiles == 0)
					{
						ChunkList.Add(newChunk);
						continue;
					}

					newChunk.FileList = new List<FileStruct>();
					tocStream.Seek(newChunk.RelPosition - 8, SeekOrigin.Current);

					for (var j = 0; j < countBlockFiles; j++)
					{
						var newFileStruct = new FileStruct();

						var fileOffset = tocStream.Position;
						tocStream.Seek(2, SeekOrigin.Current);
						newFileStruct.Flag = tocStream.ReadInt16();
						newFileStruct.FileSize = tocStream.ReadInt32();
						newFileStruct.Sha1 = tocStream.ReadBytes(20);
						newFileStruct.FilePath = tocStream.ReadStringZ();
						newFileStruct.Exist = true;

						tocStream.Seek(fileOffset + newFileStruct.BlockSize, SeekOrigin.Begin);

						newChunk.FileList.Add(newFileStruct);
					}

					tocStream.Seek(newChunk.Offset + 8, SeekOrigin.Begin);

					ChunkList.Add(newChunk);
				}
			}
		}

		public string SaveToFile(bool fileOverwrite = true)
		{
			Changed = false;

			var finalTocFile = fileOverwrite ? TocFilePath : TocFilePath + ".tmp";
			using (var newFileStream = File.Create(finalTocFile))
			{
				newFileStream.WriteUInt32(0x3AB70C13);
				newFileStream.WriteInt32(0x0);
				newFileStream.WriteInt32(ChunkList.Count);

				var chunkOffset = 12;
				var fileOffset = 12 + (ChunkList.Count * 8);

				var lastFile =
					ChunkList.Last(x => (x.FileList != null) && x.FileList.Count( /*y => y.exist*/) != 0)
						.FileList.Last( /*z => z.exist*/)
						.FilePath;

				//foreach (chunk element in chunkList)
				for (var i = 0; i < ChunkList.Count; i++)
				{
					var element = ChunkList[i];
					newFileStream.Seek(chunkOffset, SeekOrigin.Begin);

					if (element.CountNextFiles == 0) // || element.fileList.Count(x => x.exist) == 0)
					{
						newFileStream.WriteUInt64(0x0);
						chunkOffset = (int) newFileStream.Position;
					}
					else
					{
						newFileStream.WriteInt32(fileOffset - chunkOffset);
						newFileStream.WriteInt32(element.FileList.Count /*(x => x.exist)*/);
						chunkOffset = (int) newFileStream.Position;

						newFileStream.Seek(fileOffset, SeekOrigin.Begin);
						
						//foreach (fileStruct fileElement in element.fileList.Where(x => x.exist))
						for (var j = 0; j < element.FileList.Count; j++)
						{
							var fileElement = element.FileList[j];

							//if (!fileElement.exist)
							//    continue;
							var buffer = new MemoryStream(fileElement.BlockSize);
							{
								if (fileElement.FilePath == lastFile)
								{
									buffer.WriteInt16(0x0);
								}
								else
								{
									buffer.WriteInt16(fileElement.BlockSize);
								}
								
								buffer.WriteInt16(fileElement.Flag);
								buffer.WriteInt32(fileElement.FileSize);
								buffer.WriteBytes(fileElement.Sha1);
								buffer.WriteStringZ(fileElement.FilePath);
								
								var byteBuff = new byte[fileElement.BlockSize];
								
								buffer.ToArray().CopyTo(byteBuff, 0);
								newFileStream.WriteBytes(byteBuff);
							}
							//newFileStream.Seek(fileOffset, SeekOrigin.Begin);
						}

						fileOffset = (int) newFileStream.Position;
					}
				}
			}

			return finalTocFile;
		}

		public bool ExistsFile(string fileName)
		{
			var fullFileName = Path.GetFullPath(fileName);

			if (fullFileName.Length < GamePath.Length)
			{
				return false;
			}

			if (fullFileName.Substring(0, GamePath.Length).ToLowerInvariant() != GamePath.ToLowerInvariant())
			{
				return false;
			}

			var tocFileName = fullFileName.Substring(GamePath.Length);

			return ChunkList.Exists(x => x.FileList != null && x.FileList.Exists(file => file.FilePath == tocFileName));
		}

		public void RemoveNotExistingFiles(WindowProgressForm dbprog, object args)
		{
			Changed = true;
			var count = 0;
			var totalBlocks = ChunkList.Count;

			dbprog.Invoke((Action) delegate
			{
				dbprog.Text = "Cleaning toc.bin";
				dbprog.lblCommand.Text = "Removing not existing files from PCConsoleTOC.bin";
				dbprog.progressBar.Maximum = totalBlocks;
				dbprog.progressBar.Value = 0;
			});

			foreach (var element in ChunkList)
			{
				dbprog.Invoke((Action) delegate
				{
					dbprog.progressBar.Value = count++;
					dbprog.richTextBox.Text = "Cleaning block " + count + " of " + totalBlocks;
				});

				if (element.FileList != null)
				{
					element.FileList = element.FileList.Where(x => x.Exist).ToList();
				}

				//element.countNextFiles = (element.fileList == null) ? 0 : element.fileList.Count/*(x => x.exist)*/;
			}
		}

		public void ClearFiles()
		{
			Changed = true;

			using (var newFileStream = File.Create(TocFilePath + ".tmp"))
			{
				newFileStream.WriteUInt32(0x3AB70C13);
				newFileStream.WriteInt32(0x0);
				newFileStream.WriteInt32(ChunkList.Count);

				var chunkOffset = 12;
				var fileOffset = 12 + (ChunkList.Count * 8);

				foreach (var element in ChunkList)
				{
					newFileStream.Seek(chunkOffset, SeekOrigin.Begin);

					if (element.CountNextFiles == 0 || element.FileList.Count(x => x.Exist) == 0)
					{
						newFileStream.WriteUInt64(0x0);
						chunkOffset = (int) newFileStream.Position;
					}
					else
					{
						newFileStream.WriteInt32(fileOffset - chunkOffset);
						newFileStream.WriteInt32(element.FileList.Count(x => x.Exist));
						chunkOffset = (int) newFileStream.Position;

						newFileStream.Seek(fileOffset, SeekOrigin.Begin);

						foreach (var fileElement in element.FileList)
						{
							if (!fileElement.Exist)
							{
								continue;
							}

							newFileStream.WriteInt16(fileElement.BlockSize);
							newFileStream.WriteInt16(fileElement.Flag);
							newFileStream.WriteInt32(fileElement.FileSize);
							newFileStream.WriteBytes(fileElement.Sha1);
							newFileStream.WriteStringZ(fileElement.FilePath);

							fileOffset += fileElement.BlockSize;
							newFileStream.Seek(fileOffset, SeekOrigin.Begin);
						}
					}
				}
			}
		}

		public void FixAll(WindowProgressForm dbprog, object args)
		{
			Changed = true;

			var count = 0;
			var total = ChunkList.Count;

			dbprog.Invoke((Action) delegate
			{
				dbprog.Text = "Fixing toc.bin";
				dbprog.lblCommand.Text = "Updating PCConsoleTOC.bin";
				dbprog.progressBar.Maximum = total;
				dbprog.progressBar.Value = 0;
			});

			using (File.OpenWrite(TocFilePath))
			{
				var tocPath = Path.GetDirectoryName(TocFilePath);

				foreach (var entry in ChunkList)
				{
					dbprog.Invoke((Action) delegate
					{
						dbprog.progressBar.Value = count++;
						dbprog.richTextBox.Text = "Fixing block " + count + " of " + total;
					});

					if (entry.FileList == null)
					{
						continue;
					}

					foreach (var fileStruct in entry.FileList)
					{
						var filePath = tocPath + "\\" + fileStruct.FilePath;

						if (!File.Exists(filePath))
						{
							filePath = GamePath + "\\" + fileStruct.FilePath;
						}

						if (!File.Exists(filePath) || fileStruct.FilePath == "BioGame\\PCConsoleTOC.bin" || fileStruct.FilePath.EndsWith("PCConsoleTOC.bin", StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						using (var fileStream = File.OpenRead(filePath))
						{
							if (fileStruct.FileSize == fileStream.Length)
							{
								continue;
							}

							fileStruct.FileSize = (int) fileStream.Length;

							using (var sha = SHA1.Create())
							{
								sha.Initialize();
								var buffer = new byte[fileStream.Length];
								var inputCount = fileStream.Read(buffer, 0, buffer.Length);
								sha.TransformBlock(buffer, 0, inputCount, null, 0);
								sha.TransformFinalBlock(buffer, 0, 0);
								fileStruct.Sha1 = sha.Hash;
							}
						}
					}
				}
			}
		}

		public void RemoveFile(string fileName)
		{
			var selBlock = ChunkList.Single(x => x.FileList != null && x.FileList.Exists(file => file.FilePath == fileName));
			selBlock.FileList.Remove(selBlock.FileList.Single(x => x.FilePath == fileName));
			Changed = true;
		}

		public void AddFile(string newFileName, int blockIndex)
		{
			if (newFileName.Substring(0, GamePath.Length) != GamePath)
			{
				throw new Exception("Can't add \"" + Path.GetFileName(newFileName) + "\", it must reside inside \n" + GamePath);
			}

			var tocBinFilePath = newFileName.Substring(GamePath.Length);
			if (ExistsFile(newFileName))
			{
				throw new Exception("Can't add \"" + tocBinFilePath + "\",\nit already exist inside PCConsoleTOC.bin.");
			}

			/*foreach (chunk chunkElem in chunkList)
			{
				if (chunkElem.fileList == null)
					continue;
				foreach (fileStruct fileElem in chunkElem.fileList)
				{
					if (tocBinFilePath.ToLower() == fileElem.filePath.ToLower())
					{
						throw new Exception("Can't add \"" + tocBinFilePath + "\",\nit already exist inside PCConsoleTOC.bin.");
					}
				}
			}*/

			var addFileStruct = new FileStruct();

			switch (Path.GetExtension(newFileName))
			{
				case ".tlk":
				case ".tfc":
					addFileStruct.Flag = 0x09;
					break;
				default:
					addFileStruct.Flag = 0x01;
					break;
			}

			addFileStruct.FilePath = tocBinFilePath;
			addFileStruct.Exist = true;

			using (var fileStream = File.OpenRead(newFileName))
			{
				addFileStruct.FileSize = (int) fileStream.Length;
				using (var sha = SHA1.Create())
				{
					sha.Initialize();
					var buffer = new byte[fileStream.Length];
					var inputCount = fileStream.Read(buffer, 0, buffer.Length);
					sha.TransformBlock(buffer, 0, inputCount, null, 0);
					sha.TransformFinalBlock(buffer, 0, 0);
					addFileStruct.Sha1 = sha.Hash;
				}
			}

			if (ChunkList[blockIndex].FileList == null)
			{
				ChunkList[blockIndex].FileList = new List<FileStruct>();
			}

			ChunkList[blockIndex].FileList.Add(addFileStruct);

			Changed = true;
		}

		public class Chunk
		{
			public List<FileStruct> FileList;
			public long Offset;
			public int RelPosition;

			public int GlobalSize
			{
				get { return (FileList == null) ? 0 : FileList.Sum(x => x.FileSize); }
			}

			public int CountNextFiles
			{
				get { return (FileList == null) ? 0 : FileList.Count; }
			}
		}

		public class FileStruct
		{
			// fileblock size must be a multiple of 4 (4,8,...,64,72,...,88,92,96,...)
			public bool Exist = true;
			public string FilePath = "";
			public int FileSize;
			public short Flag;
			public byte[] Sha1;

			public short BlockSize
			{
				get { return (short) (4 * (1 + (28 + FilePath.Length) / 4)); }
			}
		}
	}
}