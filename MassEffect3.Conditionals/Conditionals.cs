namespace MassEffect3.Conditionals
{
	public abstract class Conditionals
	{
		public ConditionalEntries Entries { get; set; }

		public virtual uint Version { get; set; }

		public virtual byte[] this[int index]
		{
			get { throw new System.NotImplementedException(); }
			set { throw new System.NotImplementedException(); }
		}
	}
}