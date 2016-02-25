using System.IO;
using System.Text;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.UnrealEngine3.Serialization
{
	public class UnrealReader : DataReader
	{
		public UnrealReader(Stream input, ByteOrder byteOrder = ByteOrder.LittleEndian, Encoding encoding = default(UTF8Encoding),
			bool leaveOpen = false)
			: base(input, byteOrder, encoding, leaveOpen)
		{
			//
		}
	}
}
