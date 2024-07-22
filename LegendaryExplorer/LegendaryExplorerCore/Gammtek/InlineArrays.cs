using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Gammtek;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members

[InlineArray(2)]
public struct Fixed2<T>
{
    private T _element0;

    public int Length => 2;
}

[InlineArray(3)]
public struct Fixed3<T>
{
    private T _element0;
    public int Length => 3;
}

[InlineArray(4)]
public struct Fixed4<T>
{
    private T _element0;
    public int Length => 4;
}

[InlineArray(8)]
public struct Fixed8<T>
{
    private T _element0;
    public int Length => 8;
}

//just copy, paste, and change the numbers if you need another size
//why did they design the feature like this... microsoft pls


#pragma warning restore IDE0044
#pragma warning restore IDE0051