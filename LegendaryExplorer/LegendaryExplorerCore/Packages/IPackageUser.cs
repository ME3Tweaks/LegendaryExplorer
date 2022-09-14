using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Packages
{
    public interface IWeakPackageUser
    {
        void HandleUpdate(List<PackageUpdate> updates);
    }
    public interface IPackageUser : IWeakPackageUser
    {
        void RegisterClosed(Action handler);
        void ReleaseUse();
        void HandleSaveStateChange(bool isSaving);
    }
}