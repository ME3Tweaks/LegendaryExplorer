namespace MassEffect3.SaveEdit.Squad
{
	public class PowerClass
	{
		public PowerClass(string className, string name, PowerClassType powerType = PowerClassType.Normal, string customName = null)
		{
			ClassName = className;
			CustomName = customName;
			Name = name;
			PowerType = powerType;
		}

		public string ClassName { get; set; }

		public string CustomName { get; set; }

		public string Name { get; set; }

		public PowerClassType PowerType { get; set; }
	}
}
