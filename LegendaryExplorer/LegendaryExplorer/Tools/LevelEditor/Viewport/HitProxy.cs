using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.LevelEditor;

public interface IHitProxy
{
    const int StandardPriority = 1;
    const int WireFramePriority = 2;
    const int UIPriority = 3;

    int HitID { get; set; }

    int HitPriority { get; }
}


public class AxisHitProxy(EWidgetAxis axis) : IHitProxy
{
    public EWidgetAxis Axis { get; } = axis;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}