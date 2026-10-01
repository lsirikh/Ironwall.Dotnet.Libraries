using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

public sealed class SymbolPaletteView : Control
{
    private Point? _down;
    private SymbolPaletteItem? _pressed;
    private EnumMarkerCategory _category = EnumMarkerCategory.PIDS_EQUIPMENT;
    private MapViewModel? ViewModel => DataContext as MapViewModel;
    private TextBox SearchBox = null!;
    private ListBox Items = null!;
    private TextBlock CountLabel = null!, EmptyLabel = null!, FeatureStatus = null!;
    private CheckBox Enable3D = null!;
    private Thumb? _header;
    private ContentPresenter? _host;
    private Canvas? _canvas;
    private readonly List<(Button button, RoutedEventHandler handler)> _buttons = new();

    static SymbolPaletteView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SymbolPaletteView),
        new FrameworkPropertyMetadata(typeof(SymbolPaletteView)));

    public SymbolPaletteView()
    {
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(CategoryChanged));
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _down = null; _pressed = null; DetachCanvas(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) ConstrainToCanvas(); };
    }

    public override void OnApplyTemplate()
    {
        foreach (var (button, handler) in _buttons) button.Click -= handler;
        _buttons.Clear();
        if (SearchBox != null) SearchBox.TextChanged -= SearchChanged;
        if (_header != null) _header.DragDelta -= HeaderDragDelta;
        if (Enable3D != null) Enable3D.Click -= Enable3DClicked;
        if (Items != null)
        {
            Items.PreviewMouseLeftButtonDown -= ItemMouseDown;
            Items.PreviewMouseMove -= ItemMouseMove;
            Items.PreviewMouseLeftButtonUp -= ItemMouseUp;
            Items.PreviewKeyDown -= ItemKeyDown;
        }
        base.OnApplyTemplate();
        SearchBox = (TextBox)GetTemplateChild("PART_SearchBox");
        Items = (ListBox)GetTemplateChild("PART_Items");
        CountLabel = (TextBlock)GetTemplateChild("PART_CountLabel");
        EmptyLabel = (TextBlock)GetTemplateChild("PART_EmptyLabel");
        FeatureStatus = (TextBlock)GetTemplateChild("PART_FeatureStatus");
        Enable3D = (CheckBox)GetTemplateChild("PART_Enable3D");
        _header = (Thumb)GetTemplateChild("PART_HeaderDrag");
        SearchBox.TextChanged += SearchChanged;
        Items.PreviewMouseLeftButtonDown += ItemMouseDown;
        Items.PreviewMouseMove += ItemMouseMove;
        Items.PreviewMouseLeftButtonUp += ItemMouseUp;
        Items.PreviewKeyDown += ItemKeyDown;
        _header.DragDelta += HeaderDragDelta;
        Enable3D.IsChecked = Utils.Symbol3DFeature.IsEnabled;
        Enable3D.Click += Enable3DClicked;
        // 지금 상태 한 구절만 — "재시작 후 적용" 은 옆 "?"(Map.SymbolPalette.Symbol3D, help-callout H-4).
        FeatureStatus.Text = Utils.Symbol3DFeature.IsEnabled ? "현재 지도: 3D" : "현재 지도: 2D";
        HookButton("PART_CloseButton", CloseClicked);
        HookButton("PART_MilitaryButton", MilitaryClicked);
        HookButton("PART_BoundaryButton", BoundaryClicked);
        HookButton("PART_LineButton", LineClicked);
        HookButton("PART_PidsGroupButton", PidsGroupClicked);
        Filter();
    }

    private void HookButton(string name, RoutedEventHandler handler)
    {
        if (GetTemplateChild(name) is not Button button) return;
        button.Click += handler;
        _buttons.Add((button, handler));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DetachCanvas();
        for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ContentPresenter { Parent: Canvas canvas } host) continue;
            _host = host;
            _canvas = canvas;
            canvas.SizeChanged += CanvasSizeChanged;
            break;
        }
        ConstrainToCanvas();
    }

    private void DetachCanvas()
    {
        if (_canvas != null) _canvas.SizeChanged -= CanvasSizeChanged;
        _host = null;
        _canvas = null;
    }

    private void CanvasSizeChanged(object sender, SizeChangedEventArgs e) => ConstrainToCanvas();

    private void ConstrainToCanvas(double dx = 0, double dy = 0)
    {
        if (_host == null || _canvas == null || _canvas.ActualHeight <= 0 || _canvas.ActualWidth <= 0) return;
        SetCurrentValue(MaxHeightProperty, Math.Min(660, Math.Max(0, _canvas.ActualHeight - 16)));
        SetCurrentValue(MaxWidthProperty, Math.Min(384, Math.Max(0, _canvas.ActualWidth - 16)));
        double left = Canvas.GetLeft(_host), top = Canvas.GetTop(_host);
        if (!double.IsFinite(left)) left = 50;
        if (!double.IsFinite(top)) top = 60;
        double width = Math.Min(Width, MaxWidth), height = Math.Min(Height, MaxHeight);
        Canvas.SetLeft(_host, Math.Clamp(left + dx, 8, Math.Max(8, _canvas.ActualWidth - width - 8)));
        Canvas.SetTop(_host, Math.Clamp(top + dy, 8, Math.Max(8, _canvas.ActualHeight - height - 8)));
    }

    private void HeaderDragDelta(object sender, DragDeltaEventArgs e)
    {
        ConstrainToCanvas(e.HorizontalChange, e.VerticalChange);
        e.Handled = true;
    }

    private void Filter()
    {
        if (Items == null) return;
        string query = SearchBox.Text.Trim();
        var entries = SymbolPaletteItem.All.Where(x => x.Category == _category &&
            (x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Type.ToString()!.Contains(query, StringComparison.OrdinalIgnoreCase) || (x.ModelKey?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();
        Items.ItemsSource = entries; CountLabel.Text = _category is EnumMarkerCategory.PIDS_EQUIPMENT or EnumMarkerCategory.INFRASTRUCTURE
            ? $"{entries.Count}개 심볼 · 3D 미리보기" : $"{entries.Count}개 심볼";
        EmptyLabel.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => Filter();
    private void CategoryChanged(object sender, RoutedEventArgs e)
    { if (e.OriginalSource is RadioButton { Tag: string tag } && Enum.TryParse<EnumMarkerCategory>(tag, out var cat)) { _category = cat; Filter(); } }
    private void CloseClicked(object sender, RoutedEventArgs e) => ViewModel?.CloseSymbolPalette();
    private void ItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        var cell = ItemsControl.ContainerFromElement(Items, e.OriginalSource as DependencyObject) as ListBoxItem;
        _pressed = cell?.DataContext as SymbolPaletteItem; _down = _pressed == null ? null : e.GetPosition(this);
    }
    private void ItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_down is not Point start || _pressed is not { } item || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _down = null; _pressed = null;
        var vm = ViewModel;
        if (vm?.BeginPalettePlacement(item) != true || vm.MainMap == null) return;
        var map = vm.MainMap; bool couldDrag = map.CanDragMap;
        map.CanDragMap = false;
        try { DragDrop.DoDragDrop(this, new DataObject(typeof(SymbolPaletteDrag), new SymbolPaletteDrag(item, map)), DragDropEffects.Copy); }
        finally { map.CanDragMap = couldDrag; map.ClearSymbolDropPreview(); vm.CancelPalettePlacement(); }
        e.Handled = true;
    }
    private void ItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = _pressed; _pressed = null; _down = null;
        if (item != null) ViewModel?.BeginPalettePlacement(item);
    }
    private void ItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Items.SelectedItem is SymbolPaletteItem item) { ViewModel?.BeginPalettePlacement(item); e.Handled = true; }
        if (e.Key == Key.Escape) { ViewModel?.CancelPalettePlacement(); e.Handled = true; }
    }
    private async void Enable3DClicked(object sender, RoutedEventArgs e)
    {
        Enable3D.IsEnabled = false;
        try
        {
            await Helpers.MapSettingsHelper.SaveSymbol3DAsync(Enable3D.IsChecked == true);
            FeatureStatus.Text = "저장됨 — 다시 시작해야 지도에 반영됩니다";
        }
        catch (Exception ex) { Enable3D.IsChecked = Utils.Symbol3DFeature.IsEnabled; FeatureStatus.Text = $"설정 저장 실패: {ex.Message}"; }
        finally { Enable3D.IsEnabled = true; }
    }
    private void StartLegacy(EnumMarkerCategory category, object type)
    {
        if (ViewModel is not { } vm) return;
        vm.CancelPalettePlacement(); vm.CloseSymbolPalette();
        vm.SelectedMarkerCategory = category; vm.SelectedSymbolType = type;
        if (vm.AddSelectedSymbolCommand?.CanExecute(type) == true) vm.AddSelectedSymbolCommand.Execute(type);
    }
    private void MilitaryClicked(object sender, RoutedEventArgs e) => StartLegacy(EnumMarkerCategory.MILITARY_SYMBOLS, "Register");
    private void BoundaryClicked(object sender, RoutedEventArgs e) => StartLegacy(EnumMarkerCategory.AREA_BOUNDARY, "Area");
    private void LineClicked(object sender, RoutedEventArgs e) => StartLegacy(EnumMarkerCategory.AREA_BOUNDARY, "Line");
    private void PidsGroupClicked(object sender, RoutedEventArgs e) => StartLegacy(EnumMarkerCategory.PIDS_EQUIPMENT, EnumDeviceType.Fence_Group);
}

internal sealed record SymbolPaletteDrag(SymbolPaletteItem Item, GMapCustomControl Map);
