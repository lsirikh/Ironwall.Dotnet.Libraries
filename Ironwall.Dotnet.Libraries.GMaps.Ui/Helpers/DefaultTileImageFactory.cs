using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      : MBTiles 베이스맵 커버리지 밖(빈 타일) 영역에 표시할
                  "깔끔/모던" 기본 타일(no-data tile) PNG 바이트를 1회 생성·캐시한다.
   Created By   : GHLee
   Created On   : 6/22/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 256×256 no-data 타일을 PNG <see cref="byte"/>[] 로 생성한다.
/// <para>
/// 배경 + 모서리(우/하) 1px 라인으로 화면 전체에 이음매 없는 연속 격자가 형성되고,
/// 각 타일 정중앙(가로·세로 가운데)에 센서웨이 로고를 배치한다. 타일이 반복되므로
/// 빈 영역 전반에 로고가 규칙적으로 나타난다(맵과 함께 이동).
/// </para>
/// <para>
/// 결과는 <see cref="GMap.NET.MapProviders.MBTilesMapProvider.DefaultTileBytes"/> 에 주입한다.
/// Core(GMap.NET)는 WPF 비의존이므로 비트맵 생성은 이 WPF 레이어에서만 수행하고 byte[]만 넘긴다.
/// </para>
/// </summary>
public static class DefaultTileImageFactory
{
    private const int TILE_SIZE = 256;   // GMap.NET 타일 표준 크기
    private const double DPI = 96.0;      // 96 고정 → 인코딩 PNG가 정확히 256×256 device px

    // ── A안: 라이트 뉴트럴 (기본) ──────────────────────────────────────
    // 대안(검토 시 교체): B 다크모던 bg #23272E / line #2C313A,  C 라이트 도트그리드 bg #F4F6F8 / dot #D5DAE0
    private const string BACKGROUND_HEX = "#EEF1F5";   // Figma 캔버스풍 밝은 뉴트럴
    private const string GRID_LINE_HEX  = "#DFE3E8";   // 우·하 hairline → 연속 격자

    // 각 타일 정중앙 로고(센서웨이) — pack URI 임베드 리소스
    private const string LOGO_PACK_URI =
        "pack://application:,,,/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Resources/Images/sensorway.png";
    private const double LOGO_OPACITY = 1.0;   // 0.0~1.0 (낮추면 워터마크 느낌)
    private const double LOGO_MARGIN  = 16.0;  // 타일보다 클 때 유지할 최소 여백(px)

    private static byte[]? _cached;
    private static readonly object _gate = new();

    private static BitmapSource? _logo;
    private static bool _logoTried;

    /// <summary>
    /// 기본 타일 PNG 바이트를 반환한다(최초 1회 생성 후 캐시 재사용).
    /// WPF의 <see cref="RenderTargetBitmap"/> 은 STA/Dispatcher 스레드를 요구하므로,
    /// UI 스레드가 아니면 <see cref="Application.Current"/> Dispatcher로 마샬링한다.
    /// </summary>
    public static byte[] GetBytes()
    {
        if (_cached is not null) return _cached;

        lock (_gate)
        {
            if (_cached is not null) return _cached;

            var dispatcher = Application.Current?.Dispatcher;
            _cached = (dispatcher is not null && !dispatcher.CheckAccess())
                ? dispatcher.Invoke(Build)
                : Build();
            return _cached;
        }
    }

    /// <summary>실제 256×256 no-data 타일을 그려 PNG 바이트로 인코딩한다(UI 스레드 전제).</summary>
    private static byte[] Build()
    {
        var background = ToFrozenBrush(BACKGROUND_HEX);
        var gridLine = ToFrozenBrush(GRID_LINE_HEX);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 배경
            dc.DrawRectangle(background, null, new Rect(0, 0, TILE_SIZE, TILE_SIZE));
            // 우변 / 하변 1px 라인(펜 대신 채움 사각으로 픽셀 정렬 → 흐림 방지)
            dc.DrawRectangle(gridLine, null, new Rect(TILE_SIZE - 1, 0, 1, TILE_SIZE));
            dc.DrawRectangle(gridLine, null, new Rect(0, TILE_SIZE - 1, TILE_SIZE, 1));

            // 정중앙 로고(가로·세로 가운데 정렬). 로드 실패 시 격자만 표시(graceful).
            var logo = TryLoadLogo();
            if (logo != null)
            {
                double w = logo.PixelWidth, h = logo.PixelHeight;   // 96DPI → device px 1:1
                double max = TILE_SIZE - LOGO_MARGIN * 2;
                if (w > max || h > max)                              // 타일보다 크면 비율 유지 축소
                {
                    double scale = Math.Min(max / w, max / h);
                    w *= scale; h *= scale;
                }
                var rect = new Rect((TILE_SIZE - w) / 2, (TILE_SIZE - h) / 2, w, h);
                dc.PushOpacity(LOGO_OPACITY);
                dc.DrawImage(logo, rect);
                dc.Pop();
            }
        }

        var rtb = new RenderTargetBitmap(TILE_SIZE, TILE_SIZE, DPI, DPI, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>임베드된 센서웨이 로고를 1회 로드(Freeze). 실패 시 null(격자만 표시).</summary>
    private static BitmapSource? TryLoadLogo()
    {
        if (_logoTried) return _logo;
        _logoTried = true;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(LOGO_PACK_URI, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;   // 즉시 디코딩 → 스트림 의존 제거
            img.EndInit();
            img.Freeze();
            _logo = img;
        }
        catch
        {
            _logo = null;
        }
        return _logo;
    }

    private static SolidColorBrush ToFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
