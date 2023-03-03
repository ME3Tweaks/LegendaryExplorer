using System.Collections.Generic;

namespace LegendaryExplorerCore.Gammtek.Collections;

public interface IRefEnumerator<T> : IEnumerator<T>
{
    public ref T CurrentRef { get; }
}