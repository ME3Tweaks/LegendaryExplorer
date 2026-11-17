using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Collections;
using System;
using System.Numerics;
using Rotator = LegendaryExplorerCore.Unreal.BinaryConverters.Rotator;

namespace LegendaryExplorer.Tools.LevelEditor;

public class UIElement
{
    //we assume that begindraw has already been called
    public virtual void Draw(LevelEditorRenderContext context)
    {

    }
}
