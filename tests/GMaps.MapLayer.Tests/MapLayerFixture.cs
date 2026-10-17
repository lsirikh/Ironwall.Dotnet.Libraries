using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;

namespace GMaps.MapLayer.Tests;

/// <summary>로그 줄을 모은다(판정 근거를 로그로 보는 시나리오용).</summary>
internal sealed class FakeLog : ILogService
{
    public readonly List<string> Lines = new();
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Add("E " + msg);
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Add("I " + msg);
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Add("W " + msg);
#pragma warning disable CS0067
    public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
}

/// <summary>키 이벤트 인자를 만들려면 입력 원본이 있어야 한다 — 창 없이 쓰는 빈 원본.</summary>
internal sealed class FakeSource : PresentationSource
{
    private Visual? _root;
    public override Visual? RootVisual { get => _root; set => _root = value; }
    public override bool IsDisposed => false;
    protected override CompositionTarget? GetCompositionTargetCore() => null;
    public static KeyEventArgs Key(Key key, RoutedEvent? routed = null)
        => new(Keyboard.PrimaryDevice, new FakeSource(), Environment.TickCount, key) { RoutedEvent = routed ?? Keyboard.PreviewKeyDownEvent };
}

/// <summary>
/// 생성자 없이 만든 MapViewModel + 가짜 지도 DB · 심볼 DB · 이벤트 집계기 · 권한 · 지도 컨트롤(창 없음).
/// 필드 초기화식이 돌지 않으므로(GetUninitializedObject) 시험 경로가 읽는 컬렉션 필드는 여기서 직접 만든다.
/// </summary>
internal sealed class MapVm
{
    public MapViewModel Vm { get; } = Scenario.Uninit<MapViewModel>();
    public Mock<IGMapDbService> Db { get; } = new();
    public Mock<IGMapDbSymbolService> SymDb { get; } = new();
    public Mock<IPermissionService> Perm { get; } = new();
    public List<object> Published { get; } = new();
    public FakeLog Log { get; } = new();
    public GMapCustomControl Map { get; }
    public List<IMapLayerModel> Layers { get; } = new();

    public MapVm(bool canEdit = true)
    {
        Map = NewMap();
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => Published.Add(m))
          .Returns(Task.CompletedTask);
        Perm.Setup(p => p.CanEdit("map")).Returns(canEdit);
        Db.Setup(d => d.FetchMapLayersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Layers.ToList());
        Db.Setup(d => d.SeedDefaultSymbolLayersAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Db.Setup(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Db.Setup(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SymDb.Setup(d => d.UpdateSymbolLockAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SymDb.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>())).ReturnsAsync((IImageModel m, CancellationToken _) => m);

        Scenario.SetField(Vm, "<MainMap>k__BackingField", Map);
        Scenario.SetField(Vm, "_gMapDbService", Db.Object);
        Scenario.SetField(Vm, "_gMapDbSymbolService", SymDb.Object);
        Scenario.SetField(Vm, "_eventAggregator", ea.Object);
        Scenario.SetField(Vm, "_log", Log);
        Scenario.SetField(Vm, "_permissionService", Perm.Object);
        Scenario.SetField(Vm, "_permissionResolved", true);
        Scenario.SetField(Vm, "_roiItems", new System.Collections.ObjectModel.ObservableCollection<IMapRoiModel>());
        Scenario.SetField(Vm, "_layerTreeNodes", new System.Collections.ObjectModel.ObservableCollection<Ironwall.Dotnet.Libraries.GMaps.Ui.Models.LayerTreeNode>());
        Scenario.SetField(Vm, "_locateHighlight", new MapLocateHighlight());
        Scenario.SetField(Vm, "_locateRings", new List<LocateRingMarker>());
        Scenario.SetField(Vm, "_flyWatch", new System.Diagnostics.Stopwatch());   // 지도 이동 공용 경로 FlyTo(63ecd628)가 읽는다
    }

    public T Get<T>(string field) => Scenario.GetField<T>(Vm, field);
    public object? Call(string method, params object?[] args) => Scenario.Call(Vm, method, args);
    public Task CallAsync(string method, params object?[] args) => (Task)Scenario.Call(Vm, method, args)!;
    public IEnumerable<T> Of<T>() => Published.OfType<T>();

    /// <summary>
    /// 지도 컨트롤(창 없음). 벤더 GMapControl 은 판 · 항목 템플릿 · 항목 스타일을 정적 필드로 공유한다 — 다른 STA 스레드의
    /// 선행 시험이 만든 것을 쓰면 "다른 스레드가 소유" 로 실패하므로 이 스레드에서 같은 모양을 다시 만든다(MapLocateFitTests 선례).
    /// </summary>
    public static GMapCustomControl NewMap()
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 21, Position = new PointLatLng(37.39, 126.96) };
        map.Zoom = 15;
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);
        var itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        itemPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Shape"));
        map.ItemTemplate = new DataTemplate(typeof(GMapMarker)) { VisualTree = itemPresenter };
        var itemStyle = new Style();
        itemStyle.Setters.Add(new Setter(Canvas.LeftProperty, new System.Windows.Data.Binding("LocalPositionX")));
        itemStyle.Setters.Add(new Setter(Canvas.TopProperty, new System.Windows.Data.Binding("LocalPositionY")));
        itemStyle.Setters.Add(new Setter(Panel.ZIndexProperty, new System.Windows.Data.Binding("ZIndex")));
        map.ItemContainerStyle = itemStyle;
        return map;
    }

    public static GMapPidsMarker Pids(int id, int deviceId, double lat, double lng, string title = "카메라")
    {
        var model = new PidsSymbolModel { Id = id, LinkedDeviceId = deviceId, Latitude = lat, Longitude = lng, Title = title, Visible = true };
        return new GMapPidsMarker(new FakeLog(), model) { IsLayerEnabled = true };
    }

    public static (GMapImageMarker Marker, MapLayerModel Layer) Image(int imageId, int layerId, string title, string path, int zOrder = 0)
    {
        var image = new ImageModel { Id = imageId, Title = title, FilePath = path, Left = 126.90, Right = 126.92, Top = 37.40, Bottom = 37.38, Latitude = 37.39, Longitude = 126.91, Opacity = 1.0, Visibility = true, Width = 200, Height = 150 };
        var marker = new GMapImageMarker(new FakeLog(), image) { IsLayerEnabled = true };
        var layer = new MapLayerModel { Id = layerId, Name = title, LayerType = "OverlayImage", Category = "OverlayImage", FilePath = path, IsVisible = true, Opacity = 1.0, ZOrder = zOrder };
        return (marker, layer);
    }
}
