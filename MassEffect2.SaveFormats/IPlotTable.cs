namespace MassEffect2.SaveFormats
{
	public interface IPlotTable
	{
		bool GetBoolVariable(int index);
		void SetBoolVariable(int index, bool value);
		int GetIntVariable(int index);
		void SetIntVariable(int index, int value);
		float GetFloatVariable(int index);
		void SetFloatVariable(int index, float value);
	}
}