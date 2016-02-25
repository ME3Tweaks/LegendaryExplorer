using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect.ModBuilder
{
	public class ProjectSettings
	{
		// MassEffect3Mod [modName=]
		//		CoalescedFiles
		//			CoalescedFile [name=] [source=] [destination=]
		//		TlkFiles
		//			TlkFile [name=] [source=] [destination=]
		//		Defines
		//			Define [name=] [value=]
	}

	public class ProjectDocument
	{
		public IDictionary<string, ProjectCoalescedFile> CoalescedFiles { get; set; }

		public IDictionary<string, ProjectTlkFile> TlkFiles { get; set; }

		public IDictionary<string, ProjectDefine> Defines { get; set; }
	}

	public class ProjectCoalescedFile
	{
		//public string Name { get; set; }

		public string Source { get; set; }

		public string Destination { get; set; }
	}

	public class ProjectTlkFile
	{
		//public string Name { get; set; }

		public string Source { get; set; }

		public string Destination { get; set; }
	}
}
