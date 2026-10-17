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
                  "3D 만들기 — ○ 위성 타일만 ○ 오버레이 이미지 포함(그림 합성) ○ 오버레이 이미지 포함 + 건물 추출" + [만들기] [취소]
                  (MapView.xaml "GMaps.Map3D.BakeOptions"). 기본은 마지막 고른 값(appsettings Map3D.OverlayMode).
   Note         : ★ 영역과 겹치는(회전 포함) 보이는 오버레이 이미지가 하나도 없으면 묻지 않고 tiles 로 굽는다.
                  ★ overlays = 이미지 마커(GMapImageMarker, FilePath → 절대 경로) + 지도 CustomImages(TIF 등, 원본 파일이 있을 때만).
                    서버가 그대로 받지 못하는 파일(TIF · 50 MB 초과)은 PNG/JPG 캐시 파일로 바꿔 보낸다(Map3DImageCache.GetOrEncodeFile).
                  ★ id 는 3D 스냅샷과 같은 값(이미지 마커 = 모델 Id, CustomImages = 다리의 음수 id) — 3D 화면이 overlays_baked 와 맞춰 다시 그리지 않는다.
                  판정 · 본문 · 기본값은 순수 함수 Map3DBake(헤드리스 시험).
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private double[]? _map3DBakeOptionsBbox;
    private List<Map3DBakeOverlay> _map3DBakeOptionsOverlays = new();
    private string _map3DBakeMode = Map3DBake.ModeTiles;

    private bool _isMap3DBakeOptionsOpen;
    /// <summary>굽기 방식 선택 패널이 열려 있는가.</summary>
    public bool IsMap3DBakeOptionsOpen { get => _isMap3DBakeOptionsOpen; private set { _isMap3DBakeOptionsOpen = value; NotifyOfPropertyChange(); } }

    /// <summary>패널 상태 한 줄 — 영역 안 오버레이 이미지 수.</summary>
    public string Map3DBakeOptionsSummary => $"영역 안 오버레이 이미지 {_map3DBakeOptionsOverlays.Count}개";

    public bool IsMap3DBakeTiles { get => _map3DBakeMode == Map3DBake.ModeTiles; set { if (value) SetMap3DBakeMode(Map3DBake.ModeTiles); } }
    public bool IsMap3DBakeComposite { get => _map3DBakeMode == Map3DBake.ModeComposite; set { if (value) SetMap3DBakeMode(Map3DBake.ModeComposite); } }
    public bool IsMap3DBakeExtract { get => _map3DBakeMode == Map3DBake.ModeExtract; set { if (value) SetMap3DBakeMode(Map3DBake.ModeExtract); } }

    private RelayCommand? _confirmMap3DBakeOptionsCommand;
    /// <summary>[만들기] — 고른 방식을 기억하고 굽는다.</summary>
    public RelayCommand ConfirmMap3DBakeOptionsCommand => _confirmMap3DBakeOptionsCommand ??= new RelayCommand(_ => ConfirmMap3DBakeOptions());

    private RelayCommand? _cancelMap3DBakeOptionsCommand;
    /// <summary>[취소] — 굽지 않고 닫는다.</summary>
    public RelayCommand CancelMap3DBakeOptionsCommand => _cancelMap3DBakeOptionsCommand ??= new RelayCommand(_ =>
    {
        _log?.Info("[Map3D] 굽기 방식 고르기 취소");
        CloseMap3DBakeOptions();
    });

    private void SetMap3DBakeMode(string mode)
    {
        _map3DBakeMode = Map3DBake.NormalizeMode(mode);
        NotifyOfPropertyChange(nameof(IsMap3DBakeTiles));
        NotifyOfPropertyChange(nameof(IsMap3DBakeComposite));
        NotifyOfPropertyChange(nameof(IsMap3DBakeExtract));
    }

    /// <summary>영역이 정해졌다 — 겹치는 오버레이 이미지가 있으면 방식을 묻고, 없으면 바로 tiles 로 굽는다.</summary>
    private void RequestMap3DBake(double[] bbox)
    {
        List<Map3DBakeOverlay> overlays;
        try { overlays = CollectMap3DBakeOverlays(bbox); }
        catch (Exception ex)
        {
            _log?.Warning($"[Map3D] 영역 안 오버레이 이미지 찾기 실패(타일만으로 굽는다): {ex.Message}");
            overlays = new();
        }
        if (!Map3DBake.ShouldAsk(overlays.Count))
        {
            _ = BakeMap3DAsync(bbox);
            return;
        }
        _map3DBakeOptionsBbox = bbox;
        _map3DBakeOptionsOverlays = overlays;
        SetMap3DBakeMode(Map3DBake.DefaultMode(_map3DSettings.OverlayMode, overlays.Count));
        NotifyOfPropertyChange(nameof(Map3DBakeOptionsSummary));
        IsMap3DBakeOptionsOpen = true;
        _log?.Info($"[Map3D] 굽기 방식 고르기 — 영역 안 오버레이 이미지 {overlays.Count}개, 기본={_map3DBakeMode}");
    }

    private void ConfirmMap3DBakeOptions()
    {
        if (_map3DBakeOptionsBbox is not { } bbox) { CloseMap3DBakeOptions(); return; }
        var overlays = _map3DBakeOptionsOverlays;
        string mode = _map3DBakeMode;
        CloseMap3DBakeOptions();
        if (_isMap3DBaking) { SetAimStatus("이미 3D 를 만드는 중입니다", autoHide: true); return; }
        if (_map3DSettings.OverlayMode != mode)
        {
            _map3DSettings.OverlayMode = mode;   // 다음 패널의 기본값(마지막 고른 값)
            _ = SaveMap3DOverlayModeAsync();
        }
        _ = BakeMap3DAsync(bbox, mode, mode == Map3DBake.ModeTiles ? null : overlays);
    }

    private async System.Threading.Tasks.Task SaveMap3DOverlayModeAsync()
    {
        try { await MapSettingsHelper.SaveMap3DAsync(_map3DSettings, _log); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 굽기 방식 저장 실패(이번 실행 동안만 기억): {ex.Message}"); }
    }

    private void CloseMap3DBakeOptions()
    {
        _map3DBakeOptionsBbox = null;
        _map3DBakeOptionsOverlays = new();
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
