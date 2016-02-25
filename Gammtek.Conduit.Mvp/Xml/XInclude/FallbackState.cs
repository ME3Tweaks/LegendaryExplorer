namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal struct FallbackState
	{
		//Fallback is being processed
		//xi:fallback element depth
		public int FallbackDepth;
		//Fallback processed flag
		public bool FallbackProcessed;
		public bool Fallbacking;
	}
}
