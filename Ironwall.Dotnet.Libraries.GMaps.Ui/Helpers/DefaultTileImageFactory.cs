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
/// 타일은 맵 격자로 <b>반복</b>되므로 중앙 텍스트/로고는 두지 않는다(256px마다 반복되어 지저분).
/// 배경 + 모서리(우/하) 1px 라인만으로 화면 전체에 이음매 없는 연속 격자가 형성된다.
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

    private static byte[]? _cached;
    private static readonly object _gate = new();

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

    private static SolidColorBrush ToFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
