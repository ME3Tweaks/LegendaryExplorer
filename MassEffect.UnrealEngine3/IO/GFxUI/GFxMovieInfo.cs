using System.Collections.Generic;
using MassEffect.UnrealEngine.Core;

namespace MassEffect.UnrealEngine3.IO.GFxUI
{
	public class GFxMovieInfo : UObject
	{
		public IList<UObject> CompressedTextureReferences { get; set; }

		// UImport
		public string GFxExportCmdLine { get; set; }

		// UImport
		public string SourceFile { get; set; }

		// UImport
		public bool UseCompressedTextures { get; set; }

		// UImport
		public bool UseGFxExport { get; set; }

		public bool UsesFontlib { get; set; }

		public IList<UObject> UserReferences { get; set; }

		public IList<UObject> References { get; set; }

		public IEnumerable<byte> RawData { get; set; }


		// RawData
		// References
		// UserReferences
		// bUsesFontlib
		// bUseGFxExport
		// bUseCompressedTextures
		// SourceFile
		// GFxExportCmdLine
		// CompressedTextureReferences
		// CompressedTextureReferences.CompressedTextureReferences
		// UserReferences.UserReferences
		// References.References
		// RawData.RawData


		// RawData
		// int (byte[4]) Unknown
		// References
		// UserReferences
		// CompressedTextureReferences
		// bUsesFontLib
	}
}
