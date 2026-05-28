using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using GMap.NET.MapProviders.Custom;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// CustomMap 1개의 오버레이 렌더링 상태
/// CustomMapOverlayService가 관리하며, 전용 Canvas에 타일 Image를 배치한다.
/// </summary>
public class OverlayMapState
{
    /// <summary>DB 모델</summary>
    public CustomMapModel CustomMap { get; set; } = null!;

    /// <summary>타일 제공자 (폴더에서 PNG 로드)</summary>
    public FileBasedCustomMapProvider Provider { get; set; } = null!;

    /// <summary>이 CustomMap 전용 Canvas (Opacity/Visibility 개별 제어)</summary>
    public Canvas Canvas { get; set; } = null!;

    /// <summary>표시 여부</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>투명도 (0.0~1.0)</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>오버레이가 표시되는 최소 줌 레벨</summary>
    public int MinZoom { get; set; }

    /// <summary>오버레이가 표시되는 최대 줌 레벨</summary>
    public int MaxZoom { get; set; }

    /// <summary>렌더링 순서 (낮을수록 아래)</summary>
    public int ZOrder { get; set; }

    /// <summary>현재 Canvas에 배치된 타일 Image 목록</summary>
    public Dictionary<TileKey, Image> VisibleTiles { get; } = new();

    /// <summary>CustomMap의 지리 범위</summary>
    public RectLatLng GeoBounds { get; set; }

    /// <summary>진행 중인 타일 로드 취소 토큰 (줌 변경 시 교체)</summary>
    public CancellationTokenSource? ActiveLoadCts { get; set; }

    /// <summary>현재 렌더링 대상 줌 레벨</summary>
    public int CurrentRenderZoom { get; set; }

    /// <summary>현재 줌이 표시 범위 내인지</summary>
    public bool IsZoomInRange(int zoom) => zoom >= MinZoom && zoom <= MaxZoom;
}
