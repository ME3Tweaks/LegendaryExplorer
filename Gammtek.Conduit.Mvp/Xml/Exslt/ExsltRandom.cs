using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltRandom
	{
		public XPathNodeIterator RandomSequence()
		{
			return RandomSequenceImpl(1, (int) DateTime.Now.Ticks);
		}

		public XPathNodeIterator RandomSequence(double number)
		{
			return RandomSequenceImpl(number, (int) DateTime.Now.Ticks);
		}

		public XPathNodeIterator RandomSequence(double number, double seed)
		{
			return RandomSequenceImpl(number, (int) (seed % int.MaxValue));
		}

		public XPathNodeIterator RandomSequenceImpl(double number, int seed)
		{
			var doc = new XmlDocument();
			doc.LoadXml("<randoms/>");

			if (seed == int.MinValue)
			{
				seed += 1;
			}

			var rand = new Random(seed);

			//Negative number is bad idea - fallback to default
			if (number < 0)
			{
				number = 1;
			}

			//we limit number of generated numbers to int.MaxValue
			if (number > int.MaxValue)
			{
				number = int.MaxValue;
			}
			for (var i = 0; i < Convert.ToInt32(number); i++)
			{
				var elem = doc.CreateElement("random");
				elem.InnerText = rand.NextDouble().ToString(CultureInfo.InvariantCulture);
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("/randoms/random");
		}

		public XPathNodeIterator randomSequence_RENAME_ME()
		{
			return RandomSequence();
		}

		public XPathNodeIterator randomSequence_RENAME_ME(double number)
		{
			return RandomSequence(number);
		}

		public XPathNodeIterator randomSequence_RENAME_ME(double number, double seed)
		{
			return RandomSequence(number, seed);
		}
	}
}
