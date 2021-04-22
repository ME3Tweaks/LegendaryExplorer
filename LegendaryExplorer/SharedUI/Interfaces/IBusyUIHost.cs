namespace LegendaryExplorer.SharedUI.Interfaces
{
    public interface IBusyUIHost
    {
        bool IsBusy { get;set; }
        string BusyText { get; set; }
    }
}
