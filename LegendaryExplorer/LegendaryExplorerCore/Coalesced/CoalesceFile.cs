using System.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.Coalesced
{
	public abstract class CoalesceFile
	{
		private ByteOrder _byteOrder;

		protected CoalesceFile(string source = "", string name = "", string id = "",
			IList<CoalesceAsset> assets = null, CoalesceSettings settings = null,
			ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			Assets = assets ?? new List<CoalesceAsset>();
			ByteOrder = byteOrder;
			Id = id ?? "";
			Name = name ?? "";
			Settings = settings ?? new CoalesceSettings();
			Source = source ?? "";
		}

		public IList<CoalesceAsset> Assets { get; set; }

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

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSettings Settings { get; set; }

		public string Source { get; set; }
	}
}
