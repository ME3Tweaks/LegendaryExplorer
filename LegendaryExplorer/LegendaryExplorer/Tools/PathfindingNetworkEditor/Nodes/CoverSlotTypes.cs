namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    public enum ECoverAction
    {
        CA_Default = 0,
        CA_LeanLeft = 1,
        CA_LeanRight = 2,
        CA_PopUp = 3,
        CA_BlindLeft = 4,
        CA_BlindRight = 5,
        CA_BlindUp = 6,
        CA_PeekLeft = 7,
        CA_PeekRight = 8,
        CA_PeekUp = 9,
    }

    public enum ECoverType
    {
        CT_None = 0,
        CT_Standing = 1,
        CT_MidLevel = 2,
    }

    public enum ECoverLocationDescription
    {
        CoverDesc_None = 0,
        CoverDesc_Inside = 1,
        CoverDesc_Outside = 2,
        CoverDesc_LowLeft = 3,
        CoverDesc_LowRight = 4,
        CoverDesc_HighLeft = 5,
        CoverDesc_HighRight = 6,
        CoverDesc_NearLeft = 7,
        CoverDesc_NearRight = 8,
    }
}
