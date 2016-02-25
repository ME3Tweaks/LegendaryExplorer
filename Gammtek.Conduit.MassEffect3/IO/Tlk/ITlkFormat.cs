using System.IO;

namespace Gammtek.Conduit.IO.Tlk
{
	public interface ITlkFormat
	{
		string Description { get; set; }

		string Name { get; }

		TlkFile Load(string path);

		TlkFile Load(Stream stream);

		void Save(TlkFile tlkFile, string path);

		void Save(TlkFile tlkFile, Stream stream);
	}
}
