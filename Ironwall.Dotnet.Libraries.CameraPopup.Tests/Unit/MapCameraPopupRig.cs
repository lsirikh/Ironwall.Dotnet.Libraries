using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using Xunit;
using PopupSettings = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupSettings;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 지도 쪽 카메라 팝업 헤드리스 시나리오 시험 공용 도구(MAP-183~187)
                  - 창을 띄우지 않는다: STA 스레드 · 부모 없는 Measure/Arrange 까지만(지도 컨트롤은 코어만 시작)
                  - 팝업 호스트 프로세스 · LibVLC · 카메라 · RTSP · 지도 DB · 서버 · NATS 를 쓰지 않는다(전부 가짜)
                  - MapViewModel 은 생성자 의존이 많아 생성자 없이 만들고 시험에 필요한 필드만 꽂는다(지도 시나리오 시험 선례)
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>지도 STA 시험은 하나씩 — Caliburn IoC · 지도 관리자(GMaps.Instance)가 정적이다.</summary>
[CollectionDefinition(MapPopupRig.Collection, DisableParallelization = true)]
public sealed class MapCameraPopupStaCollection
{
}

internal static class MapPopupRig
{
    public const string Collection = "MapCameraPopupSta";
    public const double Lat = 37.5, Lng = 127.0;
    public const int CameraId = 7;

    /// <summary>지도 가운데에서 조금 떨어진 카메라 심볼 자리(1200×800 · 줌 16 에서 화면 안).</summary>
    public static readonly PointLatLng CameraPosition = new(37.5004, 127.0006);

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    // ───────── STA 실행 ─────────

    /// <summary>본문을 새 STA 스레드의 디스패처 위에서 끝까지 돌린다(await 뒤 이어짐도 같은 스레드). Application 은 만들지 않는다.</summary>
    public static void Sta(Func<Task> body)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            var previous = Caliburn.Micro.IoC.GetInstance;
            var events = new Caliburn.Micro.EventAggregator();   // 지도 컨트롤 생성자가 꺼낸다(GMapCustomControl) — 메모리 안 발행만
            Caliburn.Micro.IoC.GetInstance = (type, _) => type == typeof(ILogService) ? new TestLog()
                : type == typeof(Caliburn.Micro.IEventAggregator) ? events
                : throw new InvalidOperationException("시험 컨테이너에 없음: " + type.Name);
            try
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
                {
                    try { await body(); }
                    catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
                    finally { frame.Continue = false; }
                });
                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex) { failure ??= ExceptionDispatchInfo.Capture(ex); }
            finally
            {
                Caliburn.Micro.IoC.GetInstance = previous;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA 시험이 60초 안에 끝나지 않았다");
        failure?.Throw();
    }

    /// <summary>조건이 참이 될 때까지 디스패처를 돌리며 기다린다. 끝내 거짓이면 false.</summary>
    public static async Task<bool> Until(Func<bool> condition, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

    // ───────── 반사 ─────────

    private static FieldInfo FieldOf(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        throw new MissingFieldException(type.Name, name);
    }

    public static void SetField(object target, string name, object? value) => FieldOf(target.GetType(), name).SetValue(target, value);

    public static T GetField<T>(object target, string name) => (T)FieldOf(target.GetType(), name).GetValue(target)!;

    public static object? Call(object target, string method, params object?[] args)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethods(Any | BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().Name, method);
    }

    // ───────── 지도 · 지도 뷰모델 ─────────

    /// <summary>
    /// 코어가 시작된 지도(창 없음). 벤더 정적 템플릿 3종은 이 STA 스레드에서 다시 만든다. 타일 캐시(로컬 DB)는 만들지 않는다
    /// (ServerOnly + 빈 공급자 — 네트워크도 없다).
    /// </summary>
    public static GMapCustomControl NewMap(double w = 1200, double h = 800, int zoom = 16)
    {
        var map = new GMapCustomControl { MinZoom = 5, MaxZoom = 19, Position = new PointLatLng(Lat, Lng) };
        map.Manager.Mode = AccessMode.ServerOnly;
        map.Zoom = zoom;
        map.SoftwareTierOverride = false;
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);
        var itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        itemPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Shape"));
        map.ItemTemplate = new DataTemplate(typeof(GMapMarker)) { VisualTree = itemPresenter };
        var itemStyle = new Style();
        itemStyle.Setters.Add(new Setter(Canvas.LeftProperty, new System.Windows.Data.Binding("LocalPositionX")));
        itemStyle.Setters.Add(new Setter(Canvas.TopProperty, new System.Windows.Data.Binding("LocalPositionY")));
        map.ItemContainerStyle = itemStyle;
        map.Measure(new Size(w, h));
        map.Arrange(new Rect(0, 0, w, h));
        map.UpdateLayout();
        map.InitializeForBackgroundRendering((int)w, (int)h);
        Pump();
        return map;
    }

    /// <summary>위경도 → 팝업이 사는 캔버스(outer) 화면 좌표 — 지도 뷰모델과 같은 길(FromLatLngToLocal → InnerToOuter).</summary>
    public static Point ScreenOf(GMapCustomControl map, PointLatLng geo)
    {
        var g = map.FromLatLngToLocal(geo);
        return map.InnerToOuter(new Point(g.X, g.Y));
    }

    /// <summary>
    /// 생성자 없이 만든 지도 뷰모델 — 팝업 경로가 읽는 필드만 꽂는다(필드 초기화식이 돌지 않으므로 사전 · 표도 넣는다).
    /// 위치 저장소는 실제 구현(<see cref="CameraPopupPositionStore"/>) + 가짜 지도 DB(DB 실패 시 인메모리 유지 경로).
    /// </summary>
    public static MapViewModel NewVm(GMapCustomControl? map, ICameraPopupHost? host, ICameraPopupOverlaySettings? settings,
                                     IGMapDbService? db = null, bool canControlCamera = true, ICameraPopupBrokerService? broker = null)
    {
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        vm.IsNotifying = true;   // PropertyChangedBase 생성자가 켜는 값 — 생성자를 건너뛰었으니 직접 켠다(안 켜면 화면 바인딩이 값 변화를 못 받는다)
        var log = new TestLog();
        SetField(vm, "<MainMap>k__BackingField", map);
        SetField(vm, "_log", log);
        SetField(vm, "_popupAutoCloseTimers", new Dictionary<CameraStreamPopupViewModel, DispatcherTimer>());
        SetField(vm, "_ptzGestureCts", new ConcurrentDictionary<int, CancellationTokenSource>());
        SetField(vm, "_cameraPopupHost", host);
        SetField(vm, "_cameraPopupHostResolved", true);
        SetField(vm, "_overlaySettings", settings);
        SetField(vm, "_overlaySettingsResolved", true);
        SetField(vm, "_cameraPopupBroker", broker);
        SetField(vm, "_cameraPopupBrokerResolved", true);
        SetField(vm, "_cameraPopupToastMessage", string.Empty);
        SetField(vm, "_cameraPopupHostNoticeMessage", string.Empty);
        var permission = new Mock<IPermissionService>();
        permission.Setup(p => p.CanControl("cameras")).Returns(canControlCamera);
        SetField(vm, "_permissionService", permission.Object);
        SetField(vm, "_permissionResolved", true);
        SetField(vm, "_cameraPopupPositionStore", new CameraPopupPositionStore(db ?? new Mock<IGMapDbService>().Object, log));
        return vm;
    }

    /// <summary>지도 위 IpCamera 심볼(연결 장비 = RTSP 서브 주소가 있는 카메라).</summary>
    public static IPidsEditableMarker CameraMarker(int id = CameraId, string title = "정문 PTZ", PointLatLng? position = null)
    {
        var urls = new Mock<ICameraUrlsModel>();
        urls.SetupGet(u => u.RtspSub).Returns("rtsp://10.0.0.5/sub");
        urls.SetupGet(u => u.RtspMain).Returns("rtsp://10.0.0.5/main");
        var camera = new Mock<ICameraDeviceModel>();
        camera.SetupGet(c => c.Id).Returns(id);
        camera.SetupGet(c => c.DeviceName).Returns("카메라-" + id);
        camera.SetupGet(c => c.IpAddress).Returns("10.0.0.5");
        camera.SetupGet(c => c.Urls).Returns(urls.Object);
        var marker = new Mock<IPidsEditableMarker>();
        marker.SetupGet(m => m.DeviceType).Returns(EnumDeviceType.IpCamera);
        marker.SetupGet(m => m.Title).Returns(title);
        marker.SetupGet(m => m.LinkedDeviceId).Returns(id);
        marker.SetupGet(m => m.LinkedDevice).Returns(camera.Object);
        marker.SetupGet(m => m.Position).Returns(position ?? CameraPosition);
        return marker.Object;
    }

    /// <summary>지도 더블클릭 처리기(OnMapMarkerDoubleClicked) — 실제 지도 뷰가 부르는 그 처리기.</summary>
    public static void DoubleClick(MapViewModel vm, IEditableMarker marker) => Call(vm, "OnMapMarkerDoubleClicked", marker);

    /// <summary>자체 모드 · RTSP 주소 제공자 · 자동 닫기 끔(시험 중 타이머가 팝업을 닫지 않게).</summary>
    public static FakeOverlaySettings SelfSettings() => new()
    {
        Value = new PopupSettings
        {
            Mode = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupMode.Self,
            Provider = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.VideoProviderKind.RtspUrl,
            DoubleClickAutoClose = false,
        },
    };

    /// <summary>더블클릭 → 팝업 하나가 열리고 호스트에 영상 열기 · PTZ 준비가 끝날 때까지.</summary>
    public static async Task<CameraStreamPopupViewModel> OpenPopupAsync(MapViewModel vm, MapFakeHost host, IEditableMarker marker, int expectedOverlays = 1)
    {
        DoubleClick(vm, marker);
        Assert.True(await Until(() => vm.CameraPopups.Count == 1), "더블클릭 뒤 지도 팝업이 열리지 않았다");
        var popup = vm.CameraPopups[0];
        Assert.True(await Until(() => host.Opened.Count >= expectedOverlays), "팝업 호스트에 영상 열기(OpenOverlay)가 가지 않았다");
        Assert.True(await Until(() => !popup.IsPtzLoading && (popup.IsPtzCapable || popup.PtzUnavailableReason != null)),
                    "PTZ 준비(PreparePtz) 결과가 팝업에 반영되지 않았다");
        return popup;
    }

    /// <summary>점이 팝업 사각형의 테두리 위인가(지시선 끝점2).</summary>
    public static bool OnPopupEdge(double x, double y, CameraStreamPopupViewModel p, double eps = 1e-6)
    {
        double l = p.CanvasLeft, t = p.CanvasTop, r = l + p.PopupWidth, b = t + p.PopupHeight;
        bool onVertical = (Math.Abs(x - l) < eps || Math.Abs(x - r) < eps) && y >= t - eps && y <= b + eps;
        bool onHorizontal = (Math.Abs(y - t) < eps || Math.Abs(y - b) < eps) && x >= l - eps && x <= r + eps;
        return onVertical || onHorizontal;
    }

    // ───────── 저장소 파일(XAML 계약) ─────────

    public static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, relative);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(relative);
    }

    /// <summary>AutomationId 로 XAML 요소를 찾는다(MapView · 팝업 스타일).</summary>
    public static XElement ElementByAutomationId(string relativeXaml, string automationId)
        => XDocument.Load(RepoFile(relativeXaml)).Descendants()
                    .Single(e => (string?)e.Attribute("AutomationProperties.AutomationId") == automationId);

    public const string MapViewXaml = @"Ironwall.Dotnet.Libraries.GMaps.Ui\Views\Maps\MapView.xaml";
    public const string PopupStyleXaml = @"Ironwall.Dotnet.Libraries.GMaps.Ui\Themes\CameraStreamPopupStyle.xaml";
}

/// <summary>더블클릭 팝업 설정 창구 가짜.</summary>
internal sealed class FakeOverlaySettings : ICameraPopupOverlaySettings
{
    public PopupSettings Value { get; set; } = new();
    public bool IsCameraPopupUsed => Value.Mode != Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupMode.None;
    public PopupSettings Settings => Value;
    public string ClientId => "gis-7f3a9c2e41b6";
}

/// <summary>공유 메모리 프레임 가짜 — 호스트가 알리는 스트림 상태를 손으로 넣는다.</summary>
internal sealed class MapFakeFrameSource : IFrameSource
{
    public MapFakeFrameSource(OverlayStreamRequest request) => Request = request;
    public OverlayStreamRequest Request { get; }
    public string StreamId => Request.StreamId ?? string.Empty;
    public int Width => Request.Width;
    public int Height => Request.Height;
    public int Stride => Width * 4;
    public long PublishedSequence => 0;
    public StreamState State { get; private set; } = StreamState.Opening;
    public string? StateDetail { get; private set; }
    public bool Disposed { get; private set; }
    public event EventHandler? StateChanged;

    public void Set(StreamState state, string? detail = null)
    {
        State = state;
        StateDetail = detail;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence)
    {
        sequence = 0;
        return false;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>
/// 팝업 호스트 감시자 가짜(프로세스 · LibVLC · 카메라 없음) — 오버레이 열기 · 보낸 명령(PTZ · 요청)을 적고,
/// PTZ 준비 요청(PreparePtz)에는 <see cref="PtzCapable"/> 로 곧바로 답한다(더미 PTZ). 상태 변화는 손으로 일으킨다.
/// </summary>
internal sealed class MapFakeHost : ICameraPopupHost
{
    private EventHandler<CameraPopupStatusEventArgs>? _status;

    public CameraPopupHostState State { get; private set; } = CameraPopupHostState.Running;
    public string? StateReason => null;
    public bool PtzCapable { get; set; } = true;
    public List<MapFakeFrameSource> Opened { get; } = new();
    public ConcurrentQueue<(IIpcMessage Message, string? Key)> Sent { get; } = new();
    public int Restarts { get; private set; }

    public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;

    public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived
    {
        add => _status += value;
        remove => _status -= value;
    }

    public IEnumerable<PtzCommand> PtzCommands => Sent.Select(s => s.Message).OfType<PtzCommand>();

    public void ChangeState(CameraPopupHostState state)
    {
        var old = State;
        State = state;
        StateChanged?.Invoke(this, new CameraPopupHostStateChangedEventArgs(old, state, "test", null, null));
    }

    public bool Send(IIpcMessage message, string? coalesceKey = null)
    {
        if (State != CameraPopupHostState.Running) return false;
        Sent.Enqueue((message, coalesceKey));
        if (message is CameraRequest { Kind: CameraRequestKind.PreparePtz } prepare)
        {
            _status?.Invoke(this, new CameraPopupStatusEventArgs(new CameraResponse
            {
                RequestId = prepare.RequestId,
                Kind = prepare.Kind,
                CameraId = prepare.CameraId,
                Success = true,
                PtzCapable = PtzCapable,
                ImagingCapable = false,
            }));
        }
        return true;
    }

    public IFrameSource? OpenOverlay(OverlayStreamRequest request)
    {
        var source = new MapFakeFrameSource(request);
        Opened.Add(source);
        return source;
    }

    public void Restart() => Restarts++;
    public void Start() { }
    public void CloseOverlay(string streamId) { }
    public void OpenEventWindow(OpenEventWindow request) { }
    public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
    public void BringEventWindowToFront(string eventKey) { }
    public void SetTheme(string theme) { }
    public void SendPtz(PtzCommand command) => Send(command, "ptz:" + command.CameraId);
    public void Dispose() { }
}
