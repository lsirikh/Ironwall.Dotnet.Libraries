using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 맵 앵커(사이트 고정) — BoundsOfMap 패닝 구역 + MinZoom 하한 배선.
                  MapViewModel partial 분리(다중세션 협의: S-SHORT와 본체 미충돌).
                  PRD GMap_Zoom_Anchor_Home 증분2 (FR-B1/B2/B4).
   Created By   : GHLee
   Created On   : 7/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>
    /// 로드된 설정(_setupModel.MapAnchor)의 앵커를 지도에 적용.
    /// 활성+유효면 BoundsOfMap(패닝 구역) + MinZoom(축소 하한) 설정, 아니면 해제.
    /// ConfigureCommonMapSettings(부팅)·설정 저장 시 호출.
    /// </summary>
    private void ApplyMapAnchor()
    {
        if (MainMap == null) return;

        var anchor = _setupModel?.MapAnchor;
        if (anchor == null || !anchor.IsAvailable)
        {
            // 앵커 비활성/무효 → 패닝 구역 해제 + 앵커가 올렸던 최소줌을 지도 기본값으로 복원(줌아웃 복구, 사용자 지적)
            MainMap.BoundsOfMap = null;
            RestoreBaseMinZoom();
            return;
        }

        var rect = BuildAnchorRect(anchor);
        if (rect == null)
        {
            MainMap.BoundsOfMap = null;
            RestoreBaseMinZoom();
            return;
        }

        // FR-B2: 최소 줌 하한 — 맵 min과 앵커 floor 중 큰 값(축소 차단). ZoomMin 래퍼 → 슬라이더 통지.
        if (anchor.MinZoomFloor > 0)
            ZoomMin = Math.Max(ZoomMin, anchor.MinZoomFloor);

        // FR-B1/B4: 패닝 구역 — 엄격모드면 반-뷰포트 inset(화면밖 완전차단), 아니면 중심 클램프.
        MainMap.BoundsOfMap = anchor.StrictContainment ? InsetByHalfViewport(rect.Value) : rect;

        // FR-H1: 앵커 활성인데 홈이 미설정이면 홈=앵커 중심 자동 세팅(최초 적용).
        if (HomePosition == null || !HomePosition.IsAvailable)
            SetHomeToAnchorCenter();

        _log?.Info($"[MapAnchor] 적용: strict={anchor.StrictContainment}, minZoom={ZoomMin}, bounds={MainMap.BoundsOfMap}");
    }

    /// <summary>앵커가 올렸던 MinZoom을 지도 기본(SelectedMap.MinZoomLevel)으로 복원. 앵커 해제 시 줌아웃 복구(사용자 지적).</summary>
    private void RestoreBaseMinZoom()
    {
        if (SelectedMap != null && SelectedMap.MinZoomLevel >= 0)
            ZoomMin = SelectedMap.MinZoomLevel;
    }

    /// <summary>NW/SE 좌표 → GMap RectLatLng. Contains(west≤lng&lt;east, south&lt;lat≤north) 규약에 맞춤.</summary>
    private static RectLatLng? BuildAnchorRect(IMapAnchorModel a)
    {
        if (a.NorthWest == null || a.SouthEast == null) return null;
        double north = Math.Max(a.NorthWest.Latitude, a.SouthEast.Latitude);
        double south = Math.Min(a.NorthWest.Latitude, a.SouthEast.Latitude);
        double west = Math.Min(a.NorthWest.Longitude, a.SouthEast.Longitude);
        double east = Math.Max(a.NorthWest.Longitude, a.SouthEast.Longitude);
        if (east <= west || north <= south) return null;
        // FromLTRB(leftLng, topLat, rightLng, bottomLat)
        return RectLatLng.FromLTRB(west, north, east, south);
    }

    /// <summary>
    /// 현재 뷰포트(ViewArea) 절반만큼 구역을 안쪽으로 축소 → 뷰포트가 구역 밖을 안 보이게(엄격모드, best-effort).
    /// 줌 변경 시 뷰포트가 커지면 정밀도가 떨어지므로 후속(줌-change 재계산) 여지 있음. 구역이 뷰포트보다 작으면 원본 유지.
    /// </summary>
    private RectLatLng InsetByHalfViewport(RectLatLng rect)
    {
        var view = MainMap.ViewArea;   // 현재 화면 영역(RectLatLng)
        double halfLat = view.HeightLat / 2.0;
        double halfLng = view.WidthLng / 2.0;

        double west = rect.Lng + halfLng;
        double east = (rect.Lng + rect.WidthLng) - halfLng;
        double north = rect.Lat - halfLat;                     // top(north)을 아래로
        double south = (rect.Lat - rect.HeightLat) + halfLat;  // bottom(south)을 위로

        if (east <= west || north <= south)
            return rect;   // 구역이 뷰포트보다 작아 inset 불가 → 중심 클램프로 폴백

        return RectLatLng.FromLTRB(west, north, east, south);
    }

    // ─────────── 증분6: 앵커 설정 UI 바인딩 (FR-B3) ───────────

    private bool _isMapAnchorPanelVisible;
    /// <summary>앵커 설정 패널 표시 여부(툴바 토글).</summary>
    public bool IsMapAnchorPanelVisible
    {
        get => _isMapAnchorPanelVisible;
        set { _isMapAnchorPanelVisible = value; NotifyOfPropertyChange(nameof(IsMapAnchorPanelVisible)); }
    }

    private MapAnchorPanelControl? _mapAnchorPanel;
    /// <summary>사이트 고정 오버레이 패널(OverlayWindow 패턴 Control). DataContext=this로 폼 필드 바인딩, 헤더 X=토글.</summary>
    public MapAnchorPanelControl MapAnchorPanel => _mapAnchorPanel ??= new MapAnchorPanelControl
    {
        DataContext = this,
        PanelTitle = "사이트 고정 (Map Anchor)",
        CloseCommand = ShowMapAnchorPanelCommand,
    };

    private bool _anchorEnabled;
    public bool AnchorEnabled { get => _anchorEnabled; set { _anchorEnabled = value; NotifyOfPropertyChange(nameof(AnchorEnabled)); } }

    private int _anchorMinZoom;
    public int AnchorMinZoom { get => _anchorMinZoom; set { _anchorMinZoom = value; NotifyOfPropertyChange(nameof(AnchorMinZoom)); } }

    private bool _anchorStrict;
    public bool AnchorStrict { get => _anchorStrict; set { _anchorStrict = value; NotifyOfPropertyChange(nameof(AnchorStrict)); } }

    private double _anchorNwLat, _anchorNwLng, _anchorSeLat, _anchorSeLng;
    public double AnchorNwLat { get => _anchorNwLat; set { _anchorNwLat = value; NotifyOfPropertyChange(nameof(AnchorNwLat)); } }
    public double AnchorNwLng { get => _anchorNwLng; set { _anchorNwLng = value; NotifyOfPropertyChange(nameof(AnchorNwLng)); } }
    public double AnchorSeLat { get => _anchorSeLat; set { _anchorSeLat = value; NotifyOfPropertyChange(nameof(AnchorSeLat)); } }
    public double AnchorSeLng { get => _anchorSeLng; set { _anchorSeLng = value; NotifyOfPropertyChange(nameof(AnchorSeLng)); } }

    private RelayCommand? _showMapAnchorPanelCommand;
    /// <summary>앵커 설정 패널 토글(열 때 현재 설정 로드).</summary>
    public RelayCommand ShowMapAnchorPanelCommand => _showMapAnchorPanelCommand ??= new RelayCommand(_ => ToggleMapAnchorPanel());

    private RelayCommand? _setAnchorFromViewCommand;
    /// <summary>현재 화면(ViewArea)을 앵커 구역으로 지정.</summary>
    public RelayCommand SetAnchorFromViewCommand => _setAnchorFromViewCommand ??= new RelayCommand(_ => SetAnchorFromCurrentView(), _ => MainMap != null);

    private RelayCommand? _saveMapAnchorCommand;
    /// <summary>UI 값 → MapAnchorModel 저장·적용(+홈 자동세팅).</summary>
    public RelayCommand SaveMapAnchorCommand => _saveMapAnchorCommand ??= new RelayCommand(_ => SaveMapAnchorFromUi());

    private void ToggleMapAnchorPanel()
    {
        if (!IsMapAnchorPanelVisible) LoadAnchorToUi();
        IsMapAnchorPanelVisible = !IsMapAnchorPanelVisible;
    }

    /// <summary>_setupModel.MapAnchor → UI 프로퍼티 로드(패널 열 때).</summary>
    private void LoadAnchorToUi()
    {
        var a = _setupModel?.MapAnchor;
        AnchorEnabled = a?.IsEnabled ?? false;
        AnchorMinZoom = a?.MinZoomFloor > 0 ? a.MinZoomFloor : (int)ZoomMin;
        AnchorStrict = a?.StrictContainment ?? false;
        AnchorNwLat = a?.NorthWest?.Latitude ?? 0;
        AnchorNwLng = a?.NorthWest?.Longitude ?? 0;
        AnchorSeLat = a?.SouthEast?.Latitude ?? 0;
        AnchorSeLng = a?.SouthEast?.Longitude ?? 0;

        // "앵커 구역 자동으로 안 잡힌다" 대응 — 구역 미설정(모두 0)이면 패널 열 때
        //   현재 화면(ViewArea)을 기본 구역으로 즉시 자동 캡처. 사용자는 바로 조정/저장만.
        if (AnchorNwLat == 0 && AnchorNwLng == 0 && AnchorSeLat == 0 && AnchorSeLng == 0)
            SetAnchorFromCurrentView();
    }

    /// <summary>현재 화면(ViewArea)의 NW/SE를 구역 입력으로 채운다.</summary>
    private void SetAnchorFromCurrentView()
    {
        if (MainMap == null) return;
        // ViewArea는 코어(정수줌) 영역이라 디지털 줌(17+, RenderTransform 1.25×)을 반영 못 함 →
        //   실제 "보이는 영역"은 중심 기준 1/digScale 로 축소. 화면과 정확히 일치하도록 보정. (사용자 지적)
        var v = MainMap.ViewArea;                          // RectLatLng: Lat=north, Lng=west
        double s = MainMap.DigitalZoomScale;               // 1.0 / 1.25(17+) / …
        double centerLat = v.Lat - v.HeightLat / 2.0;      // 화면 중심(위경도)
        double centerLng = v.Lng + v.WidthLng / 2.0;
        double halfLat = (v.HeightLat / 2.0) / s;          // 디지털 줌 시 보이는 반-높이
        double halfLng = (v.WidthLng / 2.0) / s;
        AnchorNwLat = centerLat + halfLat;                 // north(위)
        AnchorNwLng = centerLng - halfLng;                 // west(좌)
        AnchorSeLat = centerLat - halfLat;                 // south(아래)
        AnchorSeLng = centerLng + halfLng;                 // east(우)
        AnchorMinZoom = (int)Math.Round(Zoom);             // ★ 최소 줌 = 현재 줌 반영 — 이 줌 이하로 축소 차단
        _log?.Info($"[MapAnchor] 현재 화면(보이는 영역, digScale={s:F2})을 구역으로: 중심({centerLat:F6},{centerLng:F6}) NW({AnchorNwLat:F6},{AnchorNwLng:F6}) SE({AnchorSeLat:F6},{AnchorSeLng:F6}) minZoom={AnchorMinZoom}");
    }

    /// <summary>UI 값으로 MapAnchorModel 구성 → 저장·즉시 적용(+활성 시 홈=중심).</summary>
    private async void SaveMapAnchorFromUi()
    {
        var anchor = new MapAnchorModel
        {
            IsEnabled = AnchorEnabled,
            NorthWest = new CoordinateModel(AnchorNwLat, AnchorNwLng, 0.0),
            SouthEast = new CoordinateModel(AnchorSeLat, AnchorSeLng, 0.0),
            MinZoomFloor = AnchorMinZoom,
            StrictContainment = AnchorStrict,
        };
        if (_setupModel != null) _setupModel.MapAnchor = anchor;
        await MapSettingsHelper.SaveMapAnchorAsync(anchor, _log);

        ApplyMapAnchor();                              // 즉시 적용(BoundsOfMap/MinZoom 또는 해제)
        if (anchor.IsAvailable) SetHomeToAnchorCenter();  // FR-H1: 홈=앵커 중심

        IsMapAnchorPanelVisible = false;
        _log?.Info($"[MapAnchor] 저장·적용: {anchor}");
    }
}
