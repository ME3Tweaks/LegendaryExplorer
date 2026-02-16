#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Gammtek.Collections;

public struct LambdaComparer<T>(Func<T?, T?, int> comparer) : IComparer<T>
{
    public readonly int Compare(T? x, T? y) => comparer(x, y);
}