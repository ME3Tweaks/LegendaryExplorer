namespace ME1Explorer
{
    public interface ITalkFile
    {
        string findDataById(int strRefID, bool withFileName = false);
    }
}