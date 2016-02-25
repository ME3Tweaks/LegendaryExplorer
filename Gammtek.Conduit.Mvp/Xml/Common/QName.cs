namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class QName
	{
		public QName(string local, string nsUri, string prefix)
		{
			Local = local;
			NsUri = nsUri;
			Prefix = prefix;
		}

		public string Local { get; private set; }

		public string NsUri { get; private set; }

		public string Prefix { get; private set; }

		public override string ToString()
		{
			return !string.IsNullOrEmpty(Prefix) ? (Prefix + ':' + Local) : Local;
		}
	}
}
