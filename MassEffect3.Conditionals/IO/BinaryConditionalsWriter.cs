using System.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.Conditionals.IO
{
	public class BinaryConditionalsWriter : DataWriter
	{
		public new BinaryConditionalsWriter Null = new BinaryConditionalsWriter();

		protected BinaryConditionalsWriter() {}

		public BinaryConditionalsWriter(Stream output, ByteOrder byteOrder = ByteOrder.LittleEndian) 
			: base(output, byteOrder) {}
	}
}