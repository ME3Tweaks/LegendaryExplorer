using System.Windows.Forms;

namespace MassEffect2.SaveEdit
{
	public class DoubleBufferedListView : ListView
	{
		public DoubleBufferedListView()
		{
			// ReSharper disable DoNotCallOverridableMethodsInConstructor
			DoubleBuffered = true;
			// ReSharper restore DoNotCallOverridableMethodsInConstructor
		}
	}
}