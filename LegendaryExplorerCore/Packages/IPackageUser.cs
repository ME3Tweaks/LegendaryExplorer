using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Packages
{
    public interface IPackageUser
    {
        void handleUpdate(List<PackageUpdate> updates);
        void RegisterClosed(Action handler);
        void ReleaseUse();
    }
}