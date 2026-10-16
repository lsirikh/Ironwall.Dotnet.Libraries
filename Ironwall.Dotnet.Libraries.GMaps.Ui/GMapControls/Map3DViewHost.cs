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
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class Map3DViewHost : Grid
{
    private IWebView2? _web;
    private FrameworkElement? _el;

    /// <summary>처음 WebView2 를 만들 때 쓸 방식 — true 면 WebView2CompositionControl(공역 없음). 만든 뒤 바꾸면 다음 실행부터.</summary>
    public bool UseComposition { get; set; } = true;
    private bool _coreHooked;
    private string? _job;
    private bool _ready;

    /// <summary>3D → GIS 메시지(ready · camera · pick · overlay.resync). ready 는 이 호스트가 먼저 기록한 뒤 올린다.</summary>
    public event Action<Map3DMessage>? MessageReceived;

    /// <summary>WebView2 초기화 또는 탐색 실패(사유 문구).</summary>
    public event Action<string>? LoadFailed;

    /// <summary>지금 열린 장면이 <paramref name="job"/> 이고 ready 를 받았는가 — 다시 탐색하지 않고 카메라만 보내면 된다.</summary>
    public bool IsReadyFor(string job) => _ready && _job == job;

    /// <summary>펼쳐져 있는가.</summary>
    public bool IsRevealed { get; private set; }

    /// <summary>장면 주소를 연다(1×1 대기 상태로). ready 가 오면 MessageReceived("ready").</summary>
    public async Task LoadAsync(string job, string url)
    {
        EnsureWebView();
        Park();
        _job = job;
        _ready = false;
        try
        {
            await _web!.EnsureCoreWebView2Async();
            if (!_coreHooked)
            {
                _web.CoreWebView2.WebMessageReceived += (_, e) => OnWebMessage(e.WebMessageAsJson);
                _web.CoreWebView2.NavigationCompleted += (_, e) =>
                {
                    if (!e.IsSuccess) LoadFailed?.Invoke($"3D 화면을 열지 못했습니다({e.WebErrorStatus})");
                };
                _coreHooked = true;
            }
        }
        catch (Exception ex)
        {
            LoadFailed?.Invoke($"3D 화면(WebView2)을 준비하지 못했습니다: {ex.Message}");
            return;
        }
        if (_job != job) return;   // 기다리는 사이 다른 장면 요청이 왔다
        _web.CoreWebView2.Navigate(url);
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
        if (UseComposition)
        {
            var c = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Black };
            _web = c; _el = c;
        }
        else
        {
            var w = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Black };
            _web = w; _el = w;
        }
        System.Windows.Automation.AutomationProperties.SetAutomationId(_el, "GMaps.Map3D.View");
        Children.Add(_el);
    }

    private void OnWebMessage(string json)
    {
        var msg = Map3DMath.ParseMessage(json);
        if (msg == null) return;
        if (msg.Type == "ready") _ready = true;
        MessageReceived?.Invoke(msg);
    }
}
