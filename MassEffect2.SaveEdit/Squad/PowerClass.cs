namespace MassEffect2.SaveEdit.Squad
{
	public class PowerClass
	{
		public PowerClass(string className = "", string name = "")
		{
			ClassName = className;
			Name = name;
		}

		public string ClassName { get; set; } 
		public string Name { get; set; }
	}
}