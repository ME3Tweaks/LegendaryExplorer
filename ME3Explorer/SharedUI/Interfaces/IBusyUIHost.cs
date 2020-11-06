namespace ME3Explorer.SharedUI.Interfaces
{
    public interface IBusyUIHost
    {
        bool IsBusy { get;set; }
        string BusyText { get; set; }
    }
}
