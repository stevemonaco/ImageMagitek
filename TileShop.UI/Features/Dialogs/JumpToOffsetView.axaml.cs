using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class JumpToOffsetView : UserControl
{
    static HashSet<Key> _acceptedHexKeys = new()
    {
        Key.D0, Key.D1, Key.D2 ,Key.D3 ,Key.D4 ,Key.D5 ,Key.D6 ,Key.D7 ,Key.D8, Key.D9,
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F
    };

    static HashSet<Key> _acceptedDecimalKeys = new()
    {
        Key.D0, Key.D1, Key.D2 ,Key.D3 ,Key.D4 ,Key.D5 ,Key.D6 ,Key.D7 ,Key.D8, Key.D9
    };

    private JumpToOffsetViewModel? _viewModel;

    public JumpToOffsetView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _viewModel = DataContext as JumpToOffsetViewModel;
        base.OnDataContextChanged(e);
    }

    public void JumpBox_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        _jumpBox.Focus();
        _jumpBox.SelectAll();
    }

    public void JumpBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null)
            return;

        // Enter and Escape bubble up to the dialog's accept/cancel handling
        if (e.Key is Key.Enter or Key.Escape)
            return;

        if (_viewModel.NumericBase == NumericBase.Hexadecimal && _acceptedHexKeys.Contains(e.Key))
            return;
        else if (_viewModel.NumericBase == NumericBase.Decimal && _acceptedDecimalKeys.Contains(e.Key))
            return;

        e.Handled = true;
    }
}
