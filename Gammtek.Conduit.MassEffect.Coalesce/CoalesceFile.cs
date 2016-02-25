using System.Collections.Generic;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public abstract class CoalesceFile
	{
		private ByteOrder _byteOrder;

		protected CoalesceFile(string source = "", string name = "", string id = "",
			IList<CoalesceDocument> documents = null, CoalesceSettings settings = null,
			ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			Documents = documents ?? new List<CoalesceDocument>();
			ByteOrder = byteOrder;
			Id = id ?? "";
			Name = name ?? "";
			Settings = settings ?? new CoalesceSettings();
			Source = source ?? "";
		}

		public IList<CoalesceDocument> Documents { get; set; }

		public ByteOrder ByteOrder
		{
			get { return _byteOrder; }
			set
			{
				_byteOrder = value;

				Converter = (value == ByteOrder.LittleEndian) ? ByteOrderConverter.LittleEndian : ByteOrderConverter.BigEndian;
			}
		}

		public ByteOrderConverter Converter { get; private set; }

		public IDictionary<string, CoalesceDefine> Defines { get; set; }

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSettings Settings { get; set; }

		public string Source { get; set; }
	}
}
