using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class JumpToOffsetView : UserControl
{
    private JumpToOffsetViewModel? _viewModel;

    public JumpToOffsetView()
    {
        InitializeComponent();
        _jumpBox.AddHandler(TextInputEvent, JumpBox_TextInput, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _viewModel = DataContext as JumpToOffsetViewModel;
        base.OnDataContextChanged(e);
    }

    // Only typed characters are filtered; pasted text is left to validation so "0x" prefixes can be stripped
    private void JumpBox_TextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel is null || string.IsNullOrEmpty(e.Text))
            return;

        if (_viewModel.NumericBase == NumericBase.Hexadecimal)
        {
            e.Handled = !e.Text.All(char.IsAsciiHexDigit);
            e.Text = e.Text.ToUpperInvariant();
        }
        else
        {
            e.Handled = !e.Text.All(char.IsAsciiDigit);
        }
    }
}
