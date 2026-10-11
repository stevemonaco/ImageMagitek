using System;
using Avalonia;
using Avalonia.Controls;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
    }

    /// <remarks>
    /// Scales from the available width because Dock centers empty content, so the view's own bounds only fit its content.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var scale = WelcomeViewModel.ScaleFor(availableSize.Width - ContentPanel.Margin.Left - ContentPanel.Margin.Right);
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var physicalScale = Math.Max(1, (int)Math.Floor(scale * renderScaling));
        BannerImage.Width = WelcomeViewModel.BannerWidth * physicalScale / renderScaling;
        BannerImage.Height = WelcomeViewModel.BannerHeight * physicalScale / renderScaling;

        return base.MeasureOverride(availableSize);
    }
}
