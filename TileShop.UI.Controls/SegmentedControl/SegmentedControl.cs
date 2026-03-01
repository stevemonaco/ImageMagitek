using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TileShop.UI.Controls;

[TemplatePart(Name = "PART_ItemsHost", Type = typeof(StackPanel), IsRequired = true)]
[TemplatePart(Name = "PART_Indicator", Type = typeof(Border), IsRequired = true)]
public partial class SegmentedControl : TemplatedControl
{
    private StackPanel? _itemsHost;
    private Border? _indicator;
    private bool _updatingSelection;
    private bool _hasAppliedTemplate;

    static SegmentedControl()
    {
        ItemsSourceProperty.Changed.AddClassHandler<SegmentedControl>((c, e) => c.OnItemsSourceChanged(e));
        ItemTemplateProperty.Changed.AddClassHandler<SegmentedControl>((c, _) => c.RegenerateItems());
        SelectedItemProperty.Changed.AddClassHandler<SegmentedControl>((c, _) => c.OnSelectedItemChanged());
        SelectedIndexProperty.Changed.AddClassHandler<SegmentedControl>((c, _) => c.OnSelectedIndexChanged());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _itemsHost = e.NameScope.Get<StackPanel>("PART_ItemsHost");
        _indicator = e.NameScope.Get<Border>("PART_Indicator");

        _hasAppliedTemplate = true;

        RegenerateItems();
    }

    private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= OnItemsCollectionChanged;

        if (e.NewValue is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += OnItemsCollectionChanged;

        RegenerateItems();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RegenerateItems();
    }

    private void RegenerateItems()
    {
        if (!_hasAppliedTemplate || _itemsHost is null)
            return;

        // Detach old handlers
        foreach (var child in _itemsHost.Children)
            child.PointerPressed -= OnItemPointerPressed;

        _itemsHost.Children.Clear();

        if (ItemsSource is null)
        {
            UpdateIndicator(snap: true);
            return;
        }

        foreach (var item in ItemsSource)
        {
            var presenter = new ContentPresenter
            {
                Background = Brushes.Transparent,
                Content = item,
                ContentTemplate = ItemTemplate,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Thickness(12, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            presenter.PointerPressed += OnItemPointerPressed;
            _itemsHost.Children.Add(presenter);
        }

        // Sync selection state after items are regenerated
        SyncSelectionFromItem();

        // Defer indicator positioning until layout is complete
        _itemsHost.LayoutUpdated += OnFirstLayout;
    }

    private void OnFirstLayout(object? sender, EventArgs e)
    {
        if (_itemsHost is not null)
            _itemsHost.LayoutUpdated -= OnFirstLayout;

        UpdateIndicator(snap: true);
        UpdateItemForegrounds();
    }

    private void SyncSelectionFromItem()
    {
        if (_itemsHost is null)
            return;

        var selectedItem = SelectedItem;
        if (selectedItem is null)
            return;

        for (var i = 0; i < _itemsHost.Children.Count; i++)
        {
            if (_itemsHost.Children[i] is ContentPresenter cp && Equals(cp.Content, selectedItem))
            {
                _updatingSelection = true;
                try
                {
                    SetCurrentValue(SelectedIndexProperty, i);
                }
                finally
                {
                    _updatingSelection = false;
                }
                return;
            }
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ContentPresenter presenter || _itemsHost is null)
            return;

        var index = _itemsHost.Children.IndexOf(presenter);
        if (index < 0)
            return;

        _updatingSelection = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
            SetCurrentValue(SelectedItemProperty, presenter.Content);
            UpdateIndicator(snap: false);
            UpdateItemForegrounds();
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void OnSelectedItemChanged()
    {
        if (_updatingSelection || _itemsHost is null)
            return;

        _updatingSelection = true;
        try
        {
            var selectedItem = SelectedItem;
            var index = -1;

            if (selectedItem is not null)
            {
                for (var i = 0; i < _itemsHost.Children.Count; i++)
                {
                    if (_itemsHost.Children[i] is ContentPresenter cp && Equals(cp.Content, selectedItem))
                    {
                        index = i;
                        break;
                    }
                }
            }

            SetCurrentValue(SelectedIndexProperty, index);
            UpdateIndicator(snap: false);
            UpdateItemForegrounds();
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void OnSelectedIndexChanged()
    {
        if (_updatingSelection || _itemsHost is null)
            return;

        _updatingSelection = true;
        try
        {
            var index = SelectedIndex;

            if (index >= 0 && index < _itemsHost.Children.Count &&
                _itemsHost.Children[index] is ContentPresenter cp)
            {
                SetCurrentValue(SelectedItemProperty, cp.Content);
            }
            else
            {
                SetCurrentValue(SelectedItemProperty, null);
            }

            UpdateIndicator(snap: false);
            UpdateItemForegrounds();
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void UpdateIndicator(bool snap)
    {
        if (_indicator is null || _itemsHost is null)
            return;

        var index = SelectedIndex;

        if (index < 0 || index >= _itemsHost.Children.Count)
        {
            _indicator.IsVisible = false;
            return;
        }

        var selectedChild = _itemsHost.Children[index];
        var bounds = selectedChild.Bounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            _indicator.IsVisible = false;
            return;
        }

        _indicator.IsVisible = true;
        _indicator.Height = bounds.Height;

        if (snap)
        {
            var savedTransitions = _indicator.Transitions;
            _indicator.Transitions = null;
            _indicator.Width = bounds.Width;
            Canvas.SetLeft(_indicator, bounds.X);
            _indicator.Transitions = savedTransitions;
        }
        else
        {
            _indicator.Width = bounds.Width;
            Canvas.SetLeft(_indicator, bounds.X);
        }
    }

    private void UpdateItemForegrounds()
    {
        if (_itemsHost is null)
            return;

        var selectedIndex = SelectedIndex;

        for (var i = 0; i < _itemsHost.Children.Count; i++)
        {
            if (_itemsHost.Children[i] is ContentPresenter cp)
            {
                cp.Foreground = i == selectedIndex
                    ? Brushes.White
                    : new SolidColorBrush(Color.Parse("#888888"));
            }
        }
    }
}
