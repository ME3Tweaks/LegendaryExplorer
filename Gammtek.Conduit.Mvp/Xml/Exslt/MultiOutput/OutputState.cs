using System.IO;
using System.Text;
using System.Xml;
using Gammtek.Conduit.Mvp.Xml.Common.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	internal class OutputState
	{
		//private string storedDir;

		public OutputState()
		{
			Encoding = Encoding.UTF8;
			Indent = false;
			Standalone = false;
			OmitXmlDeclaration = false;
			Method = OutputMethod.Xml;
		}

		public bool Standalone { get; set; }

		public OutputMethod Method { get; set; }

		public string Href { get; set; }

		public Encoding Encoding { get; set; }

		public bool Indent { get; set; }

		public string PublicDoctype { get; set; }

		public string SystemDoctype { get; set; }

		public XmlTextWriter XmlWriter { get; private set; }

		public StreamWriter TextWriter { get; private set; }

		public int Depth { get; set; }

		public bool OmitXmlDeclaration { get; set; }

		public void CloseWriter()
		{
			if (Method == OutputMethod.Xml)
			{
				if (!OmitXmlDeclaration)
				{
					XmlWriter.WriteEndDocument();
				}

				XmlWriter.Close();
			}
			else
			{
				TextWriter.Close();
			}
			// Restore previous current directory
			//Directory.SetCurrentDirectory(storedDir);
		}

		public void InitWriter(XmlResolver outResolver)
		{
			if (outResolver == null)
			{
				outResolver = new OutputResolver(Directory.GetCurrentDirectory());
			}
			// Save current directory
			//storedDir = Directory.GetCurrentDirectory();
			var outFile = outResolver.ResolveUri(null, Href).LocalPath;
			var dir = Directory.GetParent(outFile);
			if (!dir.Exists)
			{
				dir.Create();
			}
			// Create writer
			if (Method == OutputMethod.Xml)
			{
				XmlWriter = new XmlTextWriter(outFile, Encoding);
				if (Indent)
				{
					XmlWriter.Formatting = Formatting.Indented;
				}
				if (!OmitXmlDeclaration)
				{
					if (Standalone)
					{
						XmlWriter.WriteStartDocument(true);
					}
					else
					{
						XmlWriter.WriteStartDocument();
					}
				}
			}
			else
			{
				TextWriter = new StreamWriter(outFile, false, Encoding);
			}
			// Set new current directory            
			//Directory.SetCurrentDirectory(dir.ToString());                                    
			Href = ""; // clean the href for the next usage
		}
	}
}
