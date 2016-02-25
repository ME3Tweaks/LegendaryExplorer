using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MassEffect3.FileFormats.Common.Xml
{
	[Serializable]
	public class SerializableDictionary<TKey, TVal> : Dictionary<TKey, TVal>, IXmlSerializable, ISerializable
	{
		private string _dictionaryNodeName = "Dictionary";
		private string _itemNodeName = "Item";
		private string _keyNodeName = "Key";
		private string _valueNodeName = "Value";
		private XmlSerializer keySerializer;
		private XmlSerializer valueSerializer;

		public SerializableDictionary()
		{}

		public SerializableDictionary(IDictionary<TKey, TVal> dictionary)
			: base(dictionary)
		{}

		public SerializableDictionary(IEqualityComparer<TKey> comparer)
			: base(comparer)
		{}

		public SerializableDictionary(int capacity)
			: base(capacity)
		{}

		public SerializableDictionary(IDictionary<TKey, TVal> dictionary, IEqualityComparer<TKey> comparer)
			: base(dictionary, comparer)
		{}

		public SerializableDictionary(int capacity, IEqualityComparer<TKey> comparer)
			: base(capacity, comparer)
		{}

		protected SerializableDictionary(SerializationInfo info, StreamingContext context)
		{
			var itemCount = info.GetInt32("ItemCount");

			for (var i = 0; i < itemCount; i++)
			{
				var kvp = (KeyValuePair<TKey, TVal>) info.GetValue(String.Format("Item{0}", i), typeof (KeyValuePair<TKey, TVal>));
				Add(kvp.Key, kvp.Value);
			}
		}

		public string DictionaryNodeName
		{
			get { return _dictionaryNodeName; }
			set { _dictionaryNodeName = value; }
		}

		public string ItemNodeName
		{
			get { return _itemNodeName; }
			set { _itemNodeName = value; }
		}

		public string KeyNodeName
		{
			get { return _keyNodeName; }
			set { _keyNodeName = value; }
		}

		public string ValueNodeName
		{
			get { return _valueNodeName; }
			set { _valueNodeName = value; }
		}

		protected XmlSerializer KeySerializer
		{
			get { return keySerializer ?? (keySerializer = new XmlSerializer(typeof(TKey))); }
		}

		protected XmlSerializer ValueSerializer
		{
			get { return valueSerializer ?? (valueSerializer = new XmlSerializer(typeof (TVal))); }
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("ItemCount", Count);
			var itemIdx = 0;
			
			foreach (var kvp in this)
			{
				info.AddValue(String.Format("Item{0}", itemIdx), kvp, typeof (KeyValuePair<TKey, TVal>));
				itemIdx++;
			}
		}

		XmlSchema IXmlSerializable.GetSchema()
		{
			return null;
		}

		void IXmlSerializable.ReadXml(XmlReader reader)
		{
			if (reader.IsEmptyElement)
			{
				return;
			}

			// Move past container
			if (!reader.Read())
			{
				throw new XmlException("Error in Deserialization of Dictionary");
			}

			//reader.ReadStartElement(DictionaryNodeName);
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				// Item element
				reader.ReadStartElement(ItemNodeName);

				// Key attribute
				reader.ReadStartElement(KeyNodeName);
				var key = (TKey)KeySerializer.Deserialize(reader);
				reader.ReadEndElement();

				// Value text
				reader.ReadStartElement(ValueNodeName);
				var value = (TVal)ValueSerializer.Deserialize(reader);
				reader.ReadEndElement();

				reader.ReadEndElement();

				Add(key, value);

				reader.MoveToContent();
			}
			//reader.ReadEndElement();

			reader.ReadEndElement(); // Read End Element to close Read of containing node
		}

		void IXmlSerializable.WriteXml(XmlWriter writer)
		{
			//writer.WriteStartElement(DictionaryNodeName);
			foreach (var kvp in this)
			{
				writer.WriteStartElement(ItemNodeName);

				// Key attribute
				writer.WriteStartElement(KeyNodeName);
				KeySerializer.Serialize(writer, kvp.Key);
				writer.WriteEndElement();

				// Value text
				writer.WriteStartElement(ValueNodeName);
				ValueSerializer.Serialize(writer, kvp.Value);
				writer.WriteEndElement();

				writer.WriteEndElement();
			}
			//writer.WriteEndElement();
		}
	}
}