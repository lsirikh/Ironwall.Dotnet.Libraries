using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 지도 3D 굽기 방식 고르기(오버레이 이미지 포함 여부) — 영역 그리기 · 배지 「다시 만들기」 직후, 굽기 시작 전.
                  "3D 만들기 — ○ 오버레이 영역은 이미지에서 건물 찾기(기본) ○ 오버레이를 바닥 사진으로만(지도 건물 유지)
                  ○ 오버레이로 가리기(보안 · 건물 없음) ○ 위성 타일만(오버레이 안 넣음)"
                  + [만들기] [취소](2026-10-18 방식 넷 — 요청 본문 · mask 는 Map3DBake.ServerOverlayMode · MaskFor)
                  (MapView.xaml "GMaps.Map3D.BakeOptions"). 기본은 마지막 고른 값(appsettings Map3D.OverlayMode).
   Note         : ★ 영역과 겹치는(회전 포함) 보이는 오버레이 이미지가 하나도 없으면 방식은 묻지 않고(라디오 숨김) tiles 로 굽는다.
                  ★ 영역마다 3D 줌(2026-10-18): 패널은 늘 띄워 "3D 로 바뀌는 줌"(15~19, 0.5 단위, 기본 = 세팅의 3D 최소 줌)을 고른다 —
                    고른 줌은 굽기 요청 enter_zoom 으로 간다. 배지 「3D 줌 바꾸기…」도 같은 패널(줌만, [바꾸기])을 쓴다.
                  ★ overlays = 이미지 마커(GMapImageMarker, FilePath → 절대 경로) + 지도 CustomImages(TIF 등, 원본 파일이 있을 때만).
                    서버가 그대로 받지 못하는 파일(TIF · 50 MB 초과)은 PNG/JPG 캐시 파일로 바꿔 보낸다(Map3DImageCache.GetOrEncodeFile).
                  ★ id 는 3D 스냅샷과 같은 값(이미지 마커 = 모델 Id, CustomImages = 다리의 음수 id) — 3D 화면이 overlays_baked 와 맞춰 다시 그리지 않는다.
                  판정 · 본문 · 기본값은 순수 함수 Map3DBake(헤드리스 시험).
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private Map3DArea? _map3DBakeOptionsArea;
    private List<Map3DBakeOverlay> _map3DBakeOptionsOverlays = new();
    private string _map3DBakeMode = Map3DBake.ModeTiles;
    private double _map3DBakeEnterZoom = Map3DMath.EnterZoom;
    /// <summary>「3D 줌 바꾸기…」로 연 패널이면 그 영역(줌만 고른다), 만들기면 null.</summary>
    private Map3DAreaItem? _map3DZoomEditArea;

    private bool _isMap3DBakeOptionsOpen;
    /// <summary>굽기 방식 선택 패널이 열려 있는가.</summary>
    public bool IsMap3DBakeOptionsOpen { get => _isMap3DBakeOptionsOpen; private set { _isMap3DBakeOptionsOpen = value; NotifyOfPropertyChange(); } }

    /// <summary>패널 상태 한 줄 — 영역 안 오버레이 이미지 수(줌 바꾸기면 영역 이름).</summary>
    public string Map3DBakeOptionsSummary => _map3DZoomEditArea is { } area ? $"영역: {area.Title}" : $"영역 안 오버레이 이미지 {_map3DBakeOptionsOverlays.Count}개";

    /// <summary>패널 제목 — 「3D 만들기」 또는 「3D 줌 바꾸기」.</summary>
    public string Map3DBakeOptionsTitle => _map3DZoomEditArea == null ? "3D 만들기" : "3D 줌 바꾸기";

    /// <summary>확인 단추 글 — 만들기 · 바꾸기.</summary>
    public string Map3DBakeOptionsConfirmText => _map3DZoomEditArea == null ? "만들기" : "바꾸기";

    /// <summary>만들기 방식(라디오)을 보일까 — 만들기이고 영역 안에 오버레이 이미지가 있을 때만.</summary>
    public bool IsMap3DBakeModeVisible => _map3DZoomEditArea == null && Map3DBake.ShouldAsk(_map3DBakeOptionsOverlays.Count);

    /// <summary>"3D 로 바뀌는 줌" 선택지(15 · 15.5 · … · 19).</summary>
    public IReadOnlyList<double> Map3DEnterZoomChoices => Map3DGuide.EnterZoomChoices;

    /// <summary>고른 "3D 로 바뀌는 줌".</summary>
    public double Map3DBakeEnterZoom { get => _map3DBakeEnterZoom; set { _map3DBakeEnterZoom = Map3DGuide.EnterZoomChoiceFor(value); NotifyOfPropertyChange(); } }

    public bool IsMap3DBakeTiles { get => _map3DBakeMode == Map3DBake.ModeTiles; set { if (value) SetMap3DBakeMode(Map3DBake.ModeTiles); } }
    /// <summary>오버레이를 사진으로(건물 유지).</summary>
    public bool IsMap3DBakePhoto { get => _map3DBakeMode == Map3DBake.ModePhoto; set { if (value) SetMap3DBakeMode(Map3DBake.ModePhoto); } }
    /// <summary>오버레이로 가리기(보안).</summary>
    public bool IsMap3DBakeMask { get => _map3DBakeMode == Map3DBake.ModeMask; set { if (value) SetMap3DBakeMode(Map3DBake.ModeMask); } }
    /// <summary>옛 이름 — 예전 "그림 합성"은 늘 가렸으므로 <see cref="IsMap3DBakeMask"/> 와 같다.</summary>
    public bool IsMap3DBakeComposite { get => IsMap3DBakeMask; set => IsMap3DBakeMask = value; }
    public bool IsMap3DBakeExtract { get => _map3DBakeMode == Map3DBake.ModeExtract; set { if (value) SetMap3DBakeMode(Map3DBake.ModeExtract); } }

    private RelayCommand? _confirmMap3DBakeOptionsCommand;
    /// <summary>[만들기] — 고른 방식을 기억하고 굽는다.</summary>
    public RelayCommand ConfirmMap3DBakeOptionsCommand => _confirmMap3DBakeOptionsCommand ??= new RelayCommand(_ => ConfirmMap3DBakeOptions());

    private RelayCommand? _cancelMap3DBakeOptionsCommand;
    /// <summary>[취소] — 굽지 않고 닫는다.</summary>
    public RelayCommand CancelMap3DBakeOptionsCommand => _cancelMap3DBakeOptionsCommand ??= new RelayCommand(_ =>
    {
        _log?.Info("[Map3D] 굽기 방식 고르기 취소");
        _map3DBakeReplaces = null; _map3DBakeOptionsSourceId = null;   // 「다시 만들기」 표지도 버린다(다음 굽기로 새지 않게)
        CloseMap3DBakeOptions();
    });

    private void SetMap3DBakeMode(string mode)
    {
        _map3DBakeMode = Map3DBake.NormalizeMode(mode);
        NotifyOfPropertyChange(nameof(IsMap3DBakeTiles));
        NotifyOfPropertyChange(nameof(IsMap3DBakePhoto));
        NotifyOfPropertyChange(nameof(IsMap3DBakeMask));
        NotifyOfPropertyChange(nameof(IsMap3DBakeComposite));
        NotifyOfPropertyChange(nameof(IsMap3DBakeExtract));
    }

    /// <summary>영역이 정해졌다 — 만들기 패널을 띄운다(줌 고르기 · 겹치는 오버레이 이미지가 있으면 방식도).</summary>
    /// <param name="enterZoom">처음 골라 둘 줌 — 비우면 세팅의 3D 최소 줌(「다시 만들기」는 그 영역의 지금 줌).</param>
    /// <param name="area">영역 — 회전 영역이면 겹치는 이미지는 감싸는 bbox 로 고르고(map-3d 가 회전 사각형 밖 이미지는 굽지 않는다), 굽기 요청에 rect 를 함께 보낸다.</param>
    private void RequestMap3DBake(Map3DArea area, double? enterZoom = null)
    {
        List<Map3DBakeOverlay> overlays;
        try { overlays = CollectMap3DBakeOverlays(area.Bbox); }
        catch (Exception ex)
        {
            _log?.Warning($"[Map3D] 영역 안 오버레이 이미지 찾기 실패(타일만으로 굽는다): {ex.Message}");
            overlays = new();
        }
        _map3DZoomEditArea = null;
        _map3DBakeOptionsSourceId = null;   // 「다시 만들기」면 부른 쪽(RebakeMap3DArea)이 다시 정한다
        _map3DBakeOptionsArea = area;
        _map3DBakeOptionsOverlays = overlays;
        SetMap3DBakeMode(Map3DBake.DefaultMode(_map3DSettings.OverlayMode, overlays.Count));
        OpenMap3DBakeOptions(enterZoom ?? _map3DSettings.EnterZoom);
        _log?.Info($"[Map3D] 만들기 패널 — 영역 안 오버레이 이미지 {overlays.Count}개, 기본={_map3DBakeMode}, 줌={_map3DBakeEnterZoom}");
    }

    /// <summary>배지 · 목록 「3D 줌 바꾸기…」 — 같은 패널을 줌만 고르게 연다(지금 그 영역의 줌을 골라 둔다).</summary>
    private void OpenMap3DAreaZoomEditor(Map3DAreaItem item)
    {
        CloseMap3DBakeOptions();
        _map3DZoomEditArea = item;
        OpenMap3DBakeOptions(item.EnterZoom);
        _log?.Info($"[Map3D] 3D 줌 바꾸기 패널 id={item.Id} 지금={item.EnterZoom}");
    }

    private void OpenMap3DBakeOptions(double enterZoom)
    {
        Map3DBakeEnterZoom = enterZoom;
        NotifyOfPropertyChange(nameof(Map3DBakeOptionsSummary));
        NotifyOfPropertyChange(nameof(Map3DBakeOptionsTitle));
        NotifyOfPropertyChange(nameof(Map3DBakeOptionsConfirmText));
        NotifyOfPropertyChange(nameof(IsMap3DBakeModeVisible));
        IsMap3DBakeOptionsOpen = true;
    }

    private void ConfirmMap3DBakeOptions()
    {
        double enterZoom = _map3DBakeEnterZoom;
        if (_map3DZoomEditArea is { } area)
        {
            CloseMap3DBakeOptions();
            _ = ChangeMap3DAreaZoomAsync(area, enterZoom);
            return;
        }
        if (_map3DBakeOptionsArea is not { } bakeArea) { CloseMap3DBakeOptions(); return; }
        var overlays = _map3DBakeOptionsOverlays;
        string mode = _map3DBakeMode;
        CloseMap3DBakeOptions();
        if (_isMap3DBaking) { SetAimStatus("이미 3D 를 만드는 중입니다", autoHide: true); return; }
        if (_map3DSettings.OverlayMode != mode)
        {
            _map3DSettings.OverlayMode = mode;   // 다음 패널의 기본값(마지막 고른 값)
            _ = SaveMap3DOverlayModeAsync();
        }
        _ = BakeMap3DAsync(bakeArea, mode, mode == Map3DBake.ModeTiles ? null : overlays, enterZoom);
    }

    private async System.Threading.Tasks.Task SaveMap3DOverlayModeAsync()
    {
        try { await MapSettingsHelper.SaveMap3DAsync(_map3DSettings, _log); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 굽기 방식 저장 실패(이번 실행 동안만 기억): {ex.Message}"); }
    }

    private void CloseMap3DBakeOptions()
    {
        _map3DBakeOptionsArea = null;
        _map3DBakeOptionsOverlays = new();
        _map3DZoomEditArea = null;
        if (IsMap3DBakeOptionsOpen) IsMap3DBakeOptionsOpen = false;
    }

    /// <summary>영역과 겹치는 보이는 오버레이 이미지(이미지 마커 + 지도 CustomImages) — 보낼 파일이 없는 이미지는 뺀다.</summary>
    private List<Map3DBakeOverlay> CollectMap3DBakeOverlays(double[] bbox)
    {
        var list = new List<Map3DBakeOverlay>();
        if (MainMap == null) return list;
        foreach (var im in MainMap.Markers.OfType<GMapImageMarker>())
        {
            if (im.IsDisposed || !im.Visible || !im.IsLayerEnabled
                || !Map3DBake.Intersects(bbox, im.Left, im.Top, im.Right, im.Bottom, im.Bearing)) continue;
            if (Map3DBakeImagePath(im.FilePath, im.ImageSource) is not { } path) { _log?.Info($"[Map3D] 굽기에서 뺌(파일 없음) image id={im.Id}"); continue; }
            list.Add(new Map3DBakeOverlay(im.Id, im.Title ?? "", path, im.Left, im.Top, im.Right, im.Bottom, im.Bearing, im.Opacity, Mask: true));
        }
        foreach (var ci in MainMap.CustomImages)
        {
            var b = ci.ImageBounds;
            if (!ci.Visibility || !Map3DBake.Intersects(bbox, b.Left, b.Top, b.Right, b.Bottom, ci.UserRotation)) continue;
            if (Map3DBakeImagePath(ci.FilePath, ci.Img) is not { } path) { _log?.Info($"[Map3D] 굽기에서 뺌(원본 파일 없음) custom image '{ci.Title}'"); continue; }
            list.Add(new Map3DBakeOverlay(Map3DOverlay.IdOf(ci), ci.Title ?? "", path, b.Left, b.Top, b.Right, b.Bottom, ci.UserRotation, ci.Opacity, Mask: true));
        }
        return list;
    }

    /// <summary>
    /// 서버에 보낼 이미지 절대 경로 — 상대 경로는 실행 폴더 기준(GMapImageMarker 와 같다). 원본 파일이 없으면 null.
    /// PNG/JPG(50 MB 이하)는 원본 그대로, 그 밖은 PNG/JPG 캐시 파일.
    /// </summary>
    private string? Map3DBakeImagePath(string? filePath, ImageSource? source)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        var file = new FileInfo(Path.IsPathRooted(filePath) ? filePath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath));
        if (!file.Exists) return null;
        if (!Map3DBake.NeedsConversion(file.FullName, file.Length)) return file.FullName;
        return source is BitmapSource bitmap ? Map3DImageCache.GetOrEncodeFile(bitmap, Map3DImageCache.DefaultDirectory, _log) : null;
    }
}
