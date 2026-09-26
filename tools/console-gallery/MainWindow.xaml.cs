using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ConsoleGallery;

/// <summary>갤러리의 가짜 장비 한 줄.</summary>
public sealed class FakeDevice : PropertyChangedBase
{
    private string _unit = string.Empty, _group = string.Empty, _server = string.Empty, _name = string.Empty, _number = string.Empty;

    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = "정상";
    public string Kind { get; init; } = string.Empty;
    public string Number { get => _number; set { _number = value; NotifyOfPropertyChange(); } }
    public string Name { get => _name; set { _name = value; NotifyOfPropertyChange(); } }
    public string Unit { get => _unit; set { _unit = value; NotifyOfPropertyChange(); } }
    public string Group { get => _group; set { _group = value; NotifyOfPropertyChange(); } }
    public string Server { get => _server; set { _server = value; NotifyOfPropertyChange(); } }
    public override string ToString() => Name;
}

/// <summary>놓을 곳 칩.</summary>
public sealed record FakeZone(string ZoneKey, string Label, string Value, string[]? AllowedCategories = null);

/// <summary>
/// 콘솔 커널 갤러리 — 세 배치 · 상세 여섯 상태 · 드래그 3종(다중 선택 → 드롭존 → Draft, 순서 드래그, 키보드 폴백)을
/// 가짜 데이터로 눈으로 확인한다. 서버 호출 0.
/// </summary>
public partial class MainWindow : Window, IDragDropHandler
{
    private readonly List<FakeDevice> _all = new();
    private bool _isDark;
    private bool _loadingForm;
    private string _search = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        Seed();
        DataContext = this;
        Detail.TypeName = ActiveRail!.Label;
        WidthSlider.ValueChanged += (_, _) => ApplyStageWidth();
        Shell.SizeChanged += (_, _) => ApplyStageWidth();
        Loaded += (_, _) => { ApplyStageWidth(); FilterRows(); };
    }

    #region - Bound state -
    public ObservableCollection<ConsoleRailEntry> Rail { get; } = new();
    public ObservableCollection<FakeDevice> Rows { get; } = new();
    public ObservableCollection<FakeZone> Zones { get; } = new();
    public ObservableCollection<string> Parts { get; } = new() { "door  DOOR_SENSOR  ch1", "heater  HEATER  ch2", "fan  FAN  ch3", "ups  UPS  —", "nic  NETWORK_INTERFACE  —" };
    public ConsoleDetailPresenter Detail { get; } = new();
    public DraftTrayViewModel Tray { get; } = new();

    public static readonly DependencyProperty ActiveRailProperty = DependencyProperty.Register(nameof(ActiveRail), typeof(ConsoleRailEntry), typeof(MainWindow));
    public ConsoleRailEntry? ActiveRail { get => (ConsoleRailEntry?)GetValue(ActiveRailProperty); set => SetValue(ActiveRailProperty, value); }

    public static readonly DependencyProperty CanEditProperty = DependencyProperty.Register(nameof(CanEdit), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
    public bool CanEdit { get => (bool)GetValue(CanEditProperty); set => SetValue(CanEditProperty, value); }

    public static readonly DependencyProperty CanDeleteProperty = DependencyProperty.Register(nameof(CanDelete), typeof(bool), typeof(MainWindow));
    public bool CanDelete { get => (bool)GetValue(CanDeleteProperty); set => SetValue(CanDeleteProperty, value); }

    public static readonly DependencyProperty DeleteReasonProperty = DependencyProperty.Register(nameof(DeleteReason), typeof(string), typeof(MainWindow), new PropertyMetadata("지울 행을 먼저 고르세요."));
    public string DeleteReason { get => (string)GetValue(DeleteReasonProperty); set => SetValue(DeleteReasonProperty, value); }

    public static readonly DependencyProperty IsFormEnabledProperty = DependencyProperty.Register(nameof(IsFormEnabled), typeof(bool), typeof(MainWindow));
    public bool IsFormEnabled { get => (bool)GetValue(IsFormEnabledProperty); set => SetValue(IsFormEnabledProperty, value); }

    public string Search { get => _search; set { _search = value ?? string.Empty; FilterRows(); } }
    public int TotalCount => _all.Count;
    public int BadCount => _all.Count(d => d.Status == "장애");
    #endregion

    #region - Seed -
    private void Seed()
    {
        (string Key, string Label, string Glyph)[] types =
        {
            ("group", "그룹", "M2,4 h14 v3 h-14z M2,9 h14 v3 h-14z M2,14 h14 v2 h-14z"),
            ("controller", "제어기", "M2,3 h14 v12 h-14z M5,6 h3 v3 h-3z M10,6 h3 v3 h-3z"),
            ("sensor", "센서", "M9,2 a7,7 0 1 0 0.01,0z M9,6 a3,3 0 1 0 0.01,0z"),
            ("camera", "카메라", "M2,5 h10 v8 h-10z M12,8 l4,-3 v8 l-4,-3z"),
            ("speaker", "스피커", "M3,7 h3 l5,-4 v12 l-5,-4 h-3z"),
            ("enclosure", "함체", "M3,2 h12 v14 h-12z M6,5 h6 v2 h-6z"),
            ("lamp", "경광등", "M9,2 a5,5 0 0 1 5,5 v5 h-10 v-5 a5,5 0 0 1 5,-5z M4,14 h10 v2 h-10z"),
            ("gate", "통문", "M2,15 v-11 h2 v11z M4,6 h12 v2 h-12z"),
        };
        var random = new Random(7);
        string[] units = { "1소대", "2소대", "3소대" };
        string[] groups = { "정문 권역", "북측 울타리", "탄약고" };

        foreach (var (key, label, glyph) in types)
        {
            var icon = new System.Windows.Shapes.Path { Data = Geometry.Parse(glyph), Stretch = Stretch.Uniform };
            icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "TextMutedBrush");     // 토큰은 참조로 — 테마를 바꿔도 따라온다
            var entry = new ConsoleRailEntry(key, label, icon) { HasSeparatorAbove = key == "controller" };
            Rail.Add(entry);
            if (key == "group") { entry.Count = groups.Length; continue; }

            var n = 5 + random.Next(9);
            for (var i = 1; i <= n; i++)
                _all.Add(new FakeDevice
                {
                    Category = key,
                    Number = $"{key[..1].ToUpperInvariant()}-{i:000}",
                    Name = $"{label} {i}",
                    Kind = key == "camera" ? "PTZ" : key == "sensor" ? "Fence" : "Standard",
                    Status = random.Next(9) == 0 ? "장애" : "정상",
                    Unit = units[random.Next(units.Length)],
                    Group = groups[random.Next(groups.Length)],
                });
            entry.Count = n;
            entry.BadCount = _all.Count(d => d.Category == key && d.Status == "장애");
        }
        ActiveRail = Rail[3];

        foreach (var u in units) Zones.Add(new FakeZone("unit", $"부대 · {u}", u));
        foreach (var g in groups) Zones.Add(new FakeZone("group", $"그룹 · {g}", g));
        Zones.Add(new FakeZone("server", "서버 · CAM-API-1", "CAM-API-1", new[] { "camera" }));
        Zones.Add(new FakeZone("server", "서버 · ENC-API-1", "ENC-API-1", new[] { "enclosure", "gate" }));
    }

    private void FilterRows()
    {
        var key = ActiveRail?.Key;
        var q = _search.Trim();
        Rows.Clear();
        foreach (var d in _all.Where(d => d.Category == key && (q.Length == 0 || (d.Number + d.Name).Contains(q, StringComparison.OrdinalIgnoreCase))))
            Rows.Add(d);
    }
    #endregion

    #region - Stage -
    private void ApplyStageWidth()
    {
        Stage.Width = WidthSlider.Value + 2;      // 테두리 1px × 2 — 콘솔 자체가 슬라이더 값이 되게
        WidthText.Text = $"{WidthSlider.Value:0}px · {Shell.LayoutMode} · 상세 {Shell.DetailWidth:0}";
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        var merged = Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(_isDark ? "Dark" : "Light")}.xaml") };
        merged.Add(next);                 // 먼저 더하고
        merged.RemoveAt(merged.Count - 2); // 옛 것을 뺀다 — 중간에 토큰이 비는 순간이 없다
    }

    private void OnReadOnlyChanged(object sender, RoutedEventArgs e)
    {
        CanEdit = ReadOnlyCheck.IsChecked != true;
        Detail.IsReadOnly = !CanEdit;
        SyncSelection();
    }
    #endregion

    #region - Navigation (미적용 이동 차단) -
    private void OnGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 핸들은 끌기 전용이다 — 막지 않는다(끌기 자체가 선택을 바꾸지 않는다).
        if (e.OriginalSource is DependencyObject d && FindAncestor<DragHandle>(d) != null) return;
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) == null) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) e.Handled = true;
    }

    private ConsoleRailEntry? _railBeforeChange;
    private void OnRailChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || ReferenceEquals(e.AddedItems[0], _railBeforeChange)) return;
        if (_railBeforeChange != null && !Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail))
        {
            ActiveRail = _railBeforeChange;       // 되돌린다
            return;
        }
        _railBeforeChange = ActiveRail;
        Detail.TypeName = ActiveRail?.Label ?? string.Empty;
        Detail.IsCreating = false;
        FilterRows();
        SyncSelection();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SyncSelection();

    private void SyncSelection()
    {
        var selected = DeviceGrid.SelectedItems.Cast<FakeDevice>().ToList();
        if (selected.Count > 0) Detail.IsCreating = false;
        Detail.SelectedCount = selected.Count;
        Detail.SingleTitle = selected.Count == 1 ? selected[0].Name : string.Empty;
        Detail.SingleNumber = selected.Count == 1 ? selected[0].Number : string.Empty;
        CanDelete = CanEdit && selected.Count > 0;
        DeleteReason = CanEdit ? "지울 행을 먼저 고르세요." : "권한이 없습니다.";
        IsFormEnabled = CanEdit && (selected.Count > 0 || Detail.IsCreating);
        LoadForm(selected);
    }

    private void LoadForm(IReadOnlyList<FakeDevice> selected)
    {
        _loadingForm = true;
        NumberBox.Text = MixedValue<string>.Of(selected.Select(d => d.Number)).Display();
        NameBox.Text = MixedValue<string>.Of(selected.Select(d => d.Name)).Display();
        UnitBox.Text = MixedValue<string>.Of(selected.Select(d => d.Unit)).Display();
        NumberBox.IsEnabled = selected.Count <= 1;          // 여러 개면 식별 칸은 잠근다
        NumberField.IsLocked = selected.Count > 1;
        _loadingForm = false;
        Detail.Tracker.Clear();
        foreach (var f in new[] { NumberField, NameField, UnitField }) f.IsTouched = false;
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingForm || sender is not TextBox box || box.Tag is not string key) return;
        var selected = DeviceGrid.SelectedItems.Cast<FakeDevice>().ToList();
        var mixed = key switch
        {
            "number_device" => MixedValue<string>.Of(selected.Select(d => d.Number)),
            "name_device" => MixedValue<string>.Of(selected.Select(d => d.Name)),
            _ => MixedValue<string>.Of(selected.Select(d => d.Unit)),
        };
        Detail.LastMessage = null;
        Detail.Tracker.Touch(key, mixed.Value ?? string.Empty, box.Text, hasOriginal: !mixed.IsMixed && !Detail.IsCreating);
        var field = key switch { "number_device" => NumberField, "name_device" => NameField, _ => UnitField };
        field.IsTouched = Detail.Tracker.IsTouched(key);
    }
    #endregion

    #region - Toolbar · detail actions -
    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;
        DeviceGrid.UnselectAll();
        Detail.IsCreating = true;
        SyncSelection();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        foreach (var d in DeviceGrid.SelectedItems.Cast<FakeDevice>().ToList()) { _all.Remove(d); Rows.Remove(d); }
        Detail.LastMessage = "예시에서 지웠습니다";
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) FilterRows();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        var selected = DeviceGrid.SelectedItems.Cast<FakeDevice>().ToList();
        var changes = Detail.Tracker.ChangesFor(Detail.IsCreating ? 1 : selected.Count);

        if (Detail.IsCreating)
        {
            var created = new FakeDevice { Category = ActiveRail!.Key, Kind = "Standard", Number = NumberBox.Text, Name = NameBox.Text, Unit = UnitBox.Text };
            _all.Add(created);
            Rows.Add(created);
            Detail.IsCreating = false;
            Detail.Settle("등록했습니다");
            DeviceGrid.SelectedItem = created;
            return;
        }

        foreach (var device in selected)
            foreach (var (key, value) in changes)
            {
                var text = value?.ToString() ?? string.Empty;
                if (key == "number_device") device.Number = text;
                else if (key == "name_device") device.Name = text;
                else device.Unit = text;
            }
        Detail.Settle(ConsoleDetailStateMachine.AppliedMessage(selected.Count, changes.Count));
        foreach (var f in new[] { NumberField, NameField, UnitField }) f.IsTouched = false;
    }

    private void OnRevert(object sender, RoutedEventArgs e)
    {
        var wasCreating = Detail.IsCreating;
        Detail.IsCreating = false;
        Detail.Settle(wasCreating ? "등록을 취소했습니다" : "되돌렸습니다");
        SyncSelection();
    }

    private void OnDraftRevert(object sender, RoutedEventArgs e) => Tray.Revert();

    private async void OnDraftApply(object sender, RoutedEventArgs e)
    {
        try { await Tray.ApplyAsync(); }
        catch (InvalidOperationException) { /* 이미 적용 중 — 버튼이 꺼져 있어 오지 않는다 */ }
    }
    #endregion

    #region - State buttons -
    private void OnStateNone(object sender, RoutedEventArgs e) { Detail.Settle(string.Empty); Detail.IsCreating = false; DeviceGrid.UnselectAll(); SyncSelection(); }
    private void OnStateSingle(object sender, RoutedEventArgs e) { OnStateNone(sender, e); if (Rows.Count > 0) DeviceGrid.SelectedItem = Rows[0]; }
    private void OnStateMultiple(object sender, RoutedEventArgs e) { OnStateNone(sender, e); foreach (var r in Rows.Take(3)) DeviceGrid.SelectedItems.Add(r); }
    private void OnStateCreate(object sender, RoutedEventArgs e) { OnStateNone(sender, e); OnAdd(sender, e); }
    private void OnStateDirty(object sender, RoutedEventArgs e) { OnStateSingle(sender, e); NameBox.Text += " (수정)"; }
    #endregion

    #region - IDragDropHandler -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (target.ZoneKey == "parts") return ReferenceEquals(payload.Source, PartsList);
        if (ReferenceEquals(payload.Source, PartsList)) return false;            // 부품은 부품 목록 안에서만
        if (target.ZoneData is not FakeZone zone) return false;
        // 서버는 허용 유형만 — 아니면 드롭 불가(사선 해치).
        return zone.AllowedCategories == null || payload.Items.OfType<FakeDevice>().All(d => zone.AllowedCategories.Contains(d.Category));
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (target.IsReorder)
        {
            // 순서만 바뀐다 — 호출 0, Draft 없이 곧바로.
            DragMath.MoveMany(Parts, payload.IndexesIn(Parts), target.InsertionIndex);
            return;
        }

        var zone = (FakeZone)target.ZoneData!;
        var call = zone.ZoneKey switch { "unit" => "PATCH unit_id", "group" => "POST group assign", _ => "PATCH server_id" };
        // N회 호출로 번진다 → 곧바로 보내지 않고 Draft 에 쌓는다.
        foreach (var device in payload.Items.OfType<FakeDevice>())
            Tray.Add(new DraftEntry($"{device.Category}:{device.Number}", call, $"{device.Name} → {zone.Value}", async token =>
            {
                await Task.Delay(180, token);
                if (zone.ZoneKey == "unit") { if (device.Unit == zone.Value) return DraftOutcome.Skipped; device.Unit = zone.Value; }
                else if (zone.ZoneKey == "group") { if (device.Group == zone.Value) return DraftOutcome.Skipped; device.Group = zone.Value; }
                else device.Server = zone.Value;
                return DraftOutcome.Applied;
            }));
    }
    #endregion

    #region - Snapshot mode -
    /// <summary>
    /// <c>--snapshot &lt;폴더&gt;</c> — 입력 없이 여러 상태를 PNG 로 떠 놓고 끝낸다(눈 검증 · 테스트 결과 첨부용).
    /// 드래그 중 모습(고스트 · 삽입선 · 드롭존)은 입력이 있어야 생기므로 여기서는 못 뜬다.
    /// </summary>
    public async Task RunSnapshotsAsync(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        async Task Shot(string name, double width, System.Action arrange)
        {
            WidthSlider.Value = width;
            arrange();
            await Task.Delay(350);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            SaveVisual(System.IO.Path.Combine(directory, name + ".png"), Stage, Background);
            PreviewTools.Shared.ClipAudit.Frame(directory, name, Stage);
        }

        await Shot("01-docked-none-light", 1280, () => OnStateNone(this, new RoutedEventArgs()));
        await Shot("02-docked-single-light", 1280, () => OnStateSingle(this, new RoutedEventArgs()));
        await Shot("03-docked-multiple-light", 1280, () => OnStateMultiple(this, new RoutedEventArgs()));
        await Shot("04-docked-create-light", 1280, () => OnStateCreate(this, new RoutedEventArgs()));
        await Shot("05-docked-dirty-light", 1280, () => OnStateDirty(this, new RoutedEventArgs()));
        await Shot("06-docked-dirty-blocked-light", 1280, () => Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow));
        await Shot("07-docked-readonly-light", 1280, () => { OnStateSingle(this, new RoutedEventArgs()); ReadOnlyCheck.IsChecked = true; OnReadOnlyChanged(this, new RoutedEventArgs()); });
        await Shot("08-drawer-open-light", 1100, () => { ReadOnlyCheck.IsChecked = false; OnReadOnlyChanged(this, new RoutedEventArgs()); OnStateSingle(this, new RoutedEventArgs()); });
        await Shot("09-drawer-closed-light", 1100, () => OnStateNone(this, new RoutedEventArgs()));
        await Shot("10-compact-light", 900, () => OnStateNone(this, new RoutedEventArgs()));
        await SimulateDragsAsync(directory, Shot);
        OnToggleTheme(this, new RoutedEventArgs());
        await Shot("11-docked-dirty-dark", 1280, () => OnStateDirty(this, new RoutedEventArgs()));
        await Shot("12-docked-multiple-dark", 1280, () => OnStateMultiple(this, new RoutedEventArgs()));
        await Shot("13-compact-dark", 900, () => OnStateNone(this, new RoutedEventArgs()));
    }

    /// <summary>
    /// 어떤 요소든, 제 부모 안에서의 배치 위치(오프셋)에 상관없이 정확히 찍는다.
    /// <c>Stage</c> 는 <c>DockPanel</c> 안에서 <c>Margin="16"</c> + 가운데 정렬이라 부모 안에서의 위치가 (0,0) 이
    /// 아니다 — <see cref="RenderTargetBitmap.Render(Visual)"/> 는 요소를 새 루트인 것처럼 그리지만, 그 배치
    /// 오프셋은 요소 자신의 시각에 그대로 실려 있어서, 곧이곧대로 Render 하면(요소의 ActualWidth/Height 로 잰
    /// 캔버스에) 그 오프셋만큼 내용이 밀려 오른쪽·아래가 잘린다(실측). VisualBrush 로 우회하면 오프셋 문제는
    /// 없앨 수 있지만 D-05 가 device/accounts/reports 콘솔에서 잡은 "창을 막 띄운 첫 캡처에서 내용이 통째로
    /// 빠지는" 결함이 있다. 그래서 여기서는 요소의 부모 기준 오프셋만큼 캔버스를 <b>더 크게</b> 잡아 직접
    /// Render 한 뒤, 요소 자신의 사각만 오려낸다(VisualBrush 없이, 오프셋 문제도 없이).
    /// </summary>
    private static void SaveVisual(string path, FrameworkElement element, Brush background)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var parent = VisualTreeHelper.GetParent(element) as Visual;
        var offset = parent != null ? element.TransformToAncestor(parent).Transform(new Point(0, 0)) : new Point(0, 0);

        var dpi = VisualTreeHelper.GetDpi(element);
        var canvasWidth = offset.X + width;
        var canvasHeight = offset.Y + height;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(canvasWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(canvasHeight * dpi.DpiScaleY));

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen())
            dc.DrawRectangle(background, null, new Rect(0, 0, canvasWidth, canvasHeight));
        bitmap.Render(backdrop);
        bitmap.Render(element);

        var cropX = Math.Max(0, (int)Math.Round(offset.X * dpi.DpiScaleX));
        var cropY = Math.Max(0, (int)Math.Round(offset.Y * dpi.DpiScaleY));
        var cropW = Math.Max(1, Math.Min((int)Math.Ceiling(width * dpi.DpiScaleX), pixelWidth - cropX));
        var cropH = Math.Max(1, Math.Min((int)Math.Ceiling(height * dpi.DpiScaleY), pixelHeight - cropY));

        BitmapSource final = cropX == 0 && cropY == 0 && cropW == pixelWidth && cropH == pixelHeight
            ? bitmap
            : new CroppedBitmap(bitmap, new Int32Rect(cropX, cropY, cropW, cropH));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
    }
    #endregion

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        for (; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }
}
