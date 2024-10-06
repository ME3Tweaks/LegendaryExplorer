using System;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;

namespace LegendaryExplorer.UserControls.SharedToolControls;

public abstract class CodeEditorBase : TextEditor
{
    protected CodeEditorBase()
    {
        SearchPanel.Install(TextArea);
        Options.ConvertTabsToSpaces = true;
        ShowLineNumbers = true;

        PreviewMouseWheel += OnPreviewMouseWheel;
        PreviewKeyDown += OnPreviewKeyDown;
    }
        
    //from https://github.com/icsharpcode/AvalonEdit/issues/143#issuecomment-411834415
    #region FontSize

    // Reasonable max and min font size values
    private const double FONT_MAX_SIZE = 60d;
    private const double FONT_MIN_SIZE = 5d;

    // Update function, increases/decreases by a specific increment
    public void UpdateFontSize(bool increase)
    {
        double currentSize = FontSize;

        if (increase)
        {
            if (currentSize < FONT_MAX_SIZE)
            {
                double newSize = Math.Min(FONT_MAX_SIZE, currentSize + 1);
                FontSize = newSize;
            }
        }
        else
        {
            if (currentSize > FONT_MIN_SIZE)
            {
                double newSize = Math.Max(FONT_MIN_SIZE, currentSize - 1);
                FontSize = newSize;
            }
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            UpdateFontSize(e.Delta > 0);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.OemPlus:
                    UpdateFontSize(true);
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                    UpdateFontSize(false);
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion
}