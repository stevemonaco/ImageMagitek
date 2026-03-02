using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace TileShop.UI.Controls;

[TemplatePart(Name = "PART_ItemsHost", Type = typeof(StackPanel), IsRequired = true)]
[TemplatePart(Name = "PART_Indicator", Type = typeof(Border), IsRequired = true)]
public partial class SegmentControl : TemplatedControl
{
    private StackPanel? _itemsHost;
    private Border? _indicator;
    private bool _updatingSelection;
    private bool _hasAppliedTemplate;

    static SegmentControl()
    {
        ItemsSourceProperty.Changed.AddClassHandler<SegmentControl>((c, e) => c.OnItemsSourceChanged(e));
        ItemTemplateProperty.Changed.AddClassHandler<SegmentControl>((c, _) => c.RegenerateItems());
        SelectedItemProperty.Changed.AddClassHandler<SegmentControl>((c, _) => c.OnSelectedItemChanged());
        SelectedIndexProperty.Changed.AddClassHandler<SegmentControl>((c, _) => c.OnSelectedIndexChanged());
        ItemContainerThemeProperty.Changed.AddClassHandler<SegmentControl>((c, _) => c.RegenerateItems());
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
            var container = new SegmentItem
            {
                Content = item,
                ContentTemplate = ItemTemplate,
            };

            if (ItemContainerTheme is { } theme)
                container.Theme = theme;

            container.PointerPressed += OnItemPointerPressed;
            _itemsHost.Children.Add(container);
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
        UpdateItemSelectionStates();
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
            if (_itemsHost.Children[i] is SegmentItem container && Equals(container.Content, selectedItem))
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
        if (sender is not SegmentItem container || _itemsHost is null)
            return;

        var index = _itemsHost.Children.IndexOf(container);
        if (index < 0)
            return;

        _updatingSelection = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
            SetCurrentValue(SelectedItemProperty, container.Content);
            UpdateIndicator(snap: false);
            UpdateItemSelectionStates();
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
                    if (_itemsHost.Children[i] is SegmentItem container && Equals(container.Content, selectedItem))
                    {
                        index = i;
                        break;
                    }
                }
            }

            SetCurrentValue(SelectedIndexProperty, index);
            UpdateIndicator(snap: false);
            UpdateItemSelectionStates();
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
                _itemsHost.Children[index] is SegmentItem container)
            {
                SetCurrentValue(SelectedItemProperty, container.Content);
            }
            else
            {
                SetCurrentValue(SelectedItemProperty, null);
            }

            UpdateIndicator(snap: false);
            UpdateItemSelectionStates();
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

    private void UpdateItemSelectionStates()
    {
        if (_itemsHost is null)
            return;

        var selectedIndex = SelectedIndex;

        for (var i = 0; i < _itemsHost.Children.Count; i++)
        {
            if (_itemsHost.Children[i] is SegmentItem container)
            {
                container.IsSelected = i == selectedIndex;
            }
        }
    }
}
