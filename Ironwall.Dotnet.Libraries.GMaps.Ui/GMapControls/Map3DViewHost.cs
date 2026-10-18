using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Microsoft.Web.WebView2.Wpf;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 지도 3D 화면(map-3d `/view?embed=1`) WebView2 호스트 — 지도 컨트롤과 같은 칸에 올라간다(MapView.xaml).
   Note         : ★ WebView2 는 처음 쓸 때 1회만 만들고 이후 재사용한다(매번 만들지 않음). 사용자 데이터 폴더는 기본값
                    (실행 파일 옆 `{exe}.WebView2`, Reports.Ui 와 같은 관례 — EnsureCoreWebView2Async() 인자 없음).
                  ★ 대기(Park) = 왼쪽 위 1×1. Visibility 를 숨기면 WebView2 가 페이지를 "안 보임"으로 바꿔 그리기(rAF)가 멈추고
                    map-3d 의 ready 가 늦거나 안 올 수 있어, ready 전에는 1×1 로 "보이되 안 보이게" 둔다. ready 뒤 Reveal 로 펼친다.
                  ★ 기본은 WebView2CompositionControl(WPF 안에서 그림) — 네이티브 창 WebView2 는 펼친 동안 같은 자리의 WPF
                    (상세 창 · 카메라 상자 · 조립 카드 · 상태 배너)를 덮었다(공역, 2026-10-17). UseComposition=false 면 네이티브 창.
                  ★ 전환 시간(2026-10-17 실측 첫 진입 5.7 s 중 WebView2 기동 4.36 s): 지도가 뜬 뒤 PrewarmAsync 로 기동만 미리 해 두고,
                    줌 15 이상 영역 안이면 VM 이 장면을 1×1 로 미리 연다. ready · rendered 시각을 남겨 전환 시간 로그에 쓴다.
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class Map3DViewHost : Grid
{
    private IWebView2? _web;
    private FrameworkElement? _el;

    /// <summary>처음 WebView2 를 만들 때 쓸 방식 — true 면 WebView2CompositionControl(공역 없음). 만든 뒤 바꾸면 다음 실행부터.</summary>
    public bool UseComposition { get; set; } = true;
    private bool _coreHooked;
    private Task? _coreTask;
    private string? _job;
    private string? _url;
    private bool _ready;
    private bool _rendered;
    private bool _failed;
    private ulong _navigationId;
    private bool _guarding;

    /// <summary>지금 열었거나 여는 장면(실패했어도 남는다 — 같은 장면을 다시 미리 열지 않게). 없으면 null.</summary>
    public string? Job => _job;

    /// <summary>장면을 불러오는 중인가(ready 전 · 실패 아님).</summary>
    public bool IsLoading => _job != null && !_ready && !_failed;

    /// <summary>WebView2 기동(EnsureCoreWebView2) 끝 시각(UTC) — 미리 데우기 판정 · 전환 시간 로그. 아직이면 null.</summary>
    public DateTime? CoreReadyAt { get; private set; }

    /// <summary>지금 장면의 ready · rendered 받은 시각(UTC). 아직이면 null.</summary>
    public DateTime? ReadyAt { get; private set; }
    public DateTime? RenderedAt { get; private set; }

    /// <summary>지금 장면이 <paramref name="job"/> 이고 rendered(오버레이 적용 + 한 프레임)까지 받았는가.</summary>
    public bool IsRenderedFor(string job) => _rendered && IsReadyFor(job);

    /// <summary>지금 쓰는 방식 — "composition"(WPF 안, 오버레이가 3D 위에 보임) · "hwnd"(네이티브 창) · "none"(아직 안 만듦).</summary>
    public string HostMode => _el is WebView2CompositionControl ? "composition" : _el != null ? "hwnd" : "none";

    /// <summary>CompositionControl 을 쓸 수 없어 네이티브 창으로 바꿨다(사유) — 로그용.</summary>
    public event Action<string>? HostFallback;

    /// <summary>3D → GIS 메시지(ready · rendered · camera · pick · overlay.resync). ready · rendered 는 이 호스트가 먼저 기록한 뒤 올린다.</summary>
    public event Action<Map3DMessage>? MessageReceived;

    /// <summary>WebView2 초기화 또는 탐색 실패(사유 문구).</summary>
    public event Action<string>? LoadFailed;

    /// <summary>지금 열린 장면이 <paramref name="job"/> 이고 ready 를 받았는가 — 다시 탐색하지 않고 카메라만 보내면 된다.</summary>
    public bool IsReadyFor(string job) => _ready && _job == job;

    /// <summary>펼쳐져 있는가.</summary>
    public bool IsRevealed { get; private set; }

    private Canvas? _overlayLayer;
    /// <summary>
    /// 3D 화면 위 층(조립 카드 등 3D 마커 화면 자리에 붙는 창) — 좌표 = 이 호스트 좌표 = 3D 화면 CSS px × <see cref="ZoomFactor"/>.
    /// 배경이 없어 창 밖 입력은 3D 로 간다. WPF 안 3D(composition)에서만 보인다(네이티브 창 3D 는 덮는다).
    /// </summary>
    public Canvas OverlayLayer
    {
        get
        {
            if (_overlayLayer != null) return _overlayLayer;
            _overlayLayer = new Canvas { ClipToBounds = true };
            System.Windows.Automation.AutomationProperties.SetAutomationId(_overlayLayer, "GMaps.Map3D.OverlayLayer");
            Children.Add(_overlayLayer);   // WebView2 는 늘 맨 아래에 끼운다(EnsureWebView)
            return _overlayLayer;
        }
    }

    /// <summary>WebView2 확대 배율(CSS px → DIP). 아직 만들지 않았으면 1.</summary>
    public double ZoomFactor => _el switch
    {
        WebView2CompositionControl c => c.ZoomFactor,
        WebView2 w => w.ZoomFactor,
        _ => 1d,
    };

    /// <summary>장면 주소를 연다(1×1 대기 상태로). ready 가 오면 MessageReceived("ready").</summary>
    public async Task LoadAsync(string job, string url)
    {
        EnsureWebView();
        Park();
        _job = job;
        _url = url;
        _ready = _rendered = _failed = false;
        ReadyAt = RenderedAt = null;
        try
        {
            await EnsureCoreAsync();
        }
        catch (Exception ex)
        {
            if (_job == job) _failed = true;
            LoadFailed?.Invoke($"3D 화면(WebView2)을 준비하지 못했습니다: {ex.Message}");
            return;
        }
        if (_job != job || _web?.CoreWebView2 == null) return;   // 기다리는 사이 다른 장면 요청이 왔다
        _web.CoreWebView2.Navigate(url);
    }

    /// <summary>
    /// 미리 데우기 — WebView2 를 만들고 EnsureCoreWebView2 까지만 해 둔다(1×1 대기, 탐색 없음). 첫 3D 진입의 WebView2 기동 시간을 없앤다.
    /// 실패는 LoadFailed 로 알리고 삼킨다(진입 때 다시 시도).
    /// </summary>
    public async Task PrewarmAsync()
    {
        EnsureWebView();
        try { await EnsureCoreAsync(); }
        catch (Exception ex) { LoadFailed?.Invoke($"3D 화면(WebView2)을 미리 준비하지 못했습니다: {ex.Message}"); }
    }

    /// <summary>WebView2 기동은 한 번만(미리 데우기 · 장면 열기가 같은 Task 를 기다린다). 끝나면 메시지 · 탐색 이벤트를 1회 붙인다.</summary>
    private async Task EnsureCoreAsync()
    {
        var web = _web!;
        try { await (_coreTask ??= web.EnsureCoreWebView2Async()); }
        catch { if (ReferenceEquals(web, _web)) _coreTask = null; throw; }
        if (!ReferenceEquals(web, _web)) return;   // 기다리는 사이 네이티브 창 방식으로 바뀌었다
        CoreReadyAt ??= DateTime.UtcNow;
        if (_coreHooked) return;
        web.CoreWebView2.WebMessageReceived += (_, e) => OnWebMessage(e.WebMessageAsJson);
        web.CoreWebView2.NavigationStarting += (_, e) => _navigationId = e.NavigationId;
        web.CoreWebView2.NavigationCompleted += (_, e) =>
        {
            // 새 장면으로 바꿔 탐색하면 앞 탐색이 취소로 끝난다 — 지금 탐색의 실패만 알린다
            if (e.IsSuccess || e.NavigationId != _navigationId) return;
            _failed = true;
            LoadFailed?.Invoke($"3D 화면을 열지 못했습니다({e.WebErrorStatus})");
        };
        // 페이지(렌더 프로세스)가 죽으면 3D 가 멈춘 채 "3D"(「나가기」)로 남는다 — 실패로 알려 2D 로 돌아가게 하고, 다음 진입은 다시 연다
        web.CoreWebView2.ProcessFailed += (_, e) =>
        {
            if (e.ProcessFailedKind != Microsoft.Web.WebView2.Core.CoreWebView2ProcessFailedKind.RenderProcessExited) return;
            _ready = _rendered = false;
            _failed = true;
            LoadFailed?.Invoke($"3D 화면이 멈췄습니다({e.ProcessFailedKind} · {e.Reason})");
        };
        _coreHooked = true;
    }

    /// <summary>지도 칸 전체로 펼치고 포커스를 준다.</summary>
    public void Reveal()
    {
        if (_el == null) return;
        _el.HorizontalAlignment = HorizontalAlignment.Stretch;
        _el.VerticalAlignment = VerticalAlignment.Stretch;
        _el.Width = double.NaN;
        _el.Height = double.NaN;
        IsRevealed = true;
        _el.Focus();
    }

    /// <summary>1×1 대기로 접는다(숨김 — 다음에 재사용).</summary>
    public void Park()
    {
        if (_el == null) return;
        _el.HorizontalAlignment = HorizontalAlignment.Left;
        _el.VerticalAlignment = VerticalAlignment.Top;
        _el.Width = 1;
        _el.Height = 1;
        IsRevealed = false;
    }

    /// <summary>카메라 메시지 보내기(ready 전이면 무시).</summary>
    public void PostCamera(Map3DCamera cam, bool animate)
    {
        if (!_ready || _web?.CoreWebView2 == null) return;
        _web.CoreWebView2.PostWebMessageAsJson(Map3DMath.CameraMessageJson(cam, animate));
    }

    /// <summary>JSON 메시지 보내기(오버레이 등 — ready 전이면 무시).</summary>
    public void PostJson(string json)
    {
        if (!_ready || _web?.CoreWebView2 == null) return;
        _web.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void EnsureWebView()
    {
        if (_web != null) return;
        if (UseComposition && !CompositionSupported())
        {
            UseComposition = false;
            HostFallback?.Invoke("Windows SDK 투영(Microsoft.Windows.SDK.NET)이 없다 — 앱 대상이 net8.0-windows10.0.17763.0 이상이어야 한다");
        }
        if (UseComposition)
        {
            var c = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Black };
            _web = c; _el = c;
            if (!_guarding) { Dispatcher.UnhandledException += OnDispatcherUnhandled; _guarding = true; }
        }
        else
        {
            var w = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Black };
            _web = w; _el = w;
        }
        System.Windows.Automation.AutomationProperties.SetAutomationId(_el, "GMaps.Map3D.View");
        Children.Insert(0, _el);   // 3D 위 층(OverlayLayer)보다 아래
        Park();   // 만들자마자 1×1 대기(미리 데우기 중 지도를 덮지 않게)
    }

    /// <summary>CompositionControl 실행에 필요한 Windows SDK 투영이 있는가 — 없으면 Loaded 순간 처리되지 않은 예외로 앱이 죽는다(2026-10-17 실기).</summary>
    private static bool CompositionSupported()
    {
        try { System.Reflection.Assembly.Load("Microsoft.Windows.SDK.NET"); return true; }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// ★ GIS 는 3D 때문에 죽으면 안 된다 — CompositionControl 이 붙는 순간(Loaded 등 디스패처 안) 던진 예외만 잡아 삼키고
    /// 네이티브 창 방식으로 바꿔 같은 주소를 다시 연다. 다른 예외는 건드리지 않는다.
    /// </summary>
    private void OnDispatcherUnhandled(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (_el is not WebView2CompositionControl) return;
        if (!(e.Exception.ToString().Contains(nameof(WebView2CompositionControl), StringComparison.Ordinal))) return;
        e.Handled = true;
        SwitchToHwnd($"CompositionControl 실패 → 네이티브 창으로: {e.Exception.GetType().Name}: {e.Exception.Message}");
    }

    private void SwitchToHwnd(string reason)
    {
        var old = _el;
        Children.Remove(old);
        (old as IDisposable)?.Dispose();
        _web = null; _el = null; _coreHooked = false; _coreTask = null; _ready = false; IsRevealed = false;
        UseComposition = false;
        HostFallback?.Invoke(reason);
        if (_job is { } job && _url is { } url) _ = LoadAsync(job, url);
    }

    private void OnWebMessage(string json)
    {
        var msg = Map3DMath.ParseMessage(json);
        if (msg == null) return;
        if (msg.Type == "ready") { _ready = true; ReadyAt = DateTime.UtcNow; }
        if (msg.Type == "rendered") { _rendered = true; RenderedAt = DateTime.UtcNow; }
        MessageReceived?.Invoke(msg);
    }
}
