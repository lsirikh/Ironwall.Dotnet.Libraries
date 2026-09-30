using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 이벤트 창 타일 격자 (camera-popup-modes PRD FR-10)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 격자 한 모양. 표기는 <b>가로×세로</b>(열×행) — <c>3×2</c> 는 가로 3칸, 세로 2줄.
/// 저장 키는 ASCII <c>3x2</c>(설정 파일 가독성 · 대소문자 무관 파싱).
/// </summary>
public readonly record struct CameraPopupGridLayout(int Columns, int Rows)
{
    /// <summary>칸 수(= 가로 × 세로).</summary>
    public int Capacity => Columns * Rows;

    /// <summary>설정 파일에 적는 값 — <c>3x2</c>.</summary>
    public string Key => string.Create(CultureInfo.InvariantCulture, $"{Columns}x{Rows}");

    /// <summary>화면 글자 — <c>3×2</c>.</summary>
    public string Display => string.Create(CultureInfo.InvariantCulture, $"{Columns}×{Rows}");

    public override string ToString() => Key;

    /// <summary><c>3x2</c> · <c>3X2</c> · <c>3×2</c> 를 받는다. 양수 두 개가 아니면 실패.</summary>
    public static bool TryParse(string? text, out CameraPopupGridLayout layout)
    {
        layout = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(new[] { 'x', 'X', '×' }, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows)) return false;
        if (columns <= 0 || rows <= 0) return false;

        layout = new CameraPopupGridLayout(columns, rows);
        return true;
    }
}

/// <summary>
/// 창당 카메라 수에 맞는 격자 규칙 — 순수 함수(헤드리스 시험 · 설정 화면 · 이벤트 창이 같은 표를 쓴다).
/// </summary>
/// <remarks>
/// 표(PRD FR-10 · SB §2 ⑥): 1 <c>1×1</c> · 2 <c>2×1/1×2</c> · 3 <c>3×1/1×3</c> · 4 <c>2×2/4×1/1×4</c> ·
/// 5 <c>3×2/2×3</c>(한 칸 비움) · 6 <c>3×2/2×3/6×1/1×6</c>. 첫 항목이 기본값이다.
/// </remarks>
public static class CameraPopupGridLayouts
{
    /// <summary>창당 카메라 최소 · 최대(PRD FR-10).</summary>
    public const int MinCameras = 1;
    public const int MaxCameras = 6;

    private static readonly CameraPopupGridLayout[][] Table =
    {
        new[] { new CameraPopupGridLayout(1, 1) },
        new[] { new CameraPopupGridLayout(2, 1), new CameraPopupGridLayout(1, 2) },
        new[] { new CameraPopupGridLayout(3, 1), new CameraPopupGridLayout(1, 3) },
        new[] { new CameraPopupGridLayout(2, 2), new CameraPopupGridLayout(4, 1), new CameraPopupGridLayout(1, 4) },
        new[] { new CameraPopupGridLayout(3, 2), new CameraPopupGridLayout(2, 3) },
        new[] { new CameraPopupGridLayout(3, 2), new CameraPopupGridLayout(2, 3), new CameraPopupGridLayout(6, 1), new CameraPopupGridLayout(1, 6) },
    };

    /// <summary>1~6 밖의 수를 안쪽으로 당긴다.</summary>
    public static int ClampCount(int cameraCount) => Math.Clamp(cameraCount, MinCameras, MaxCameras);

    /// <summary>그 수에서 고를 수 있는 격자(첫 항목 = 기본). 범위 밖 수는 먼저 당긴다.</summary>
    public static IReadOnlyList<CameraPopupGridLayout> Allowed(int cameraCount) => Table[ClampCount(cameraCount) - 1];

    /// <summary>그 수의 기본 격자.</summary>
    public static CameraPopupGridLayout Default(int cameraCount) => Allowed(cameraCount)[0];

    /// <summary>그 수에서 고를 수 있는 격자인가.</summary>
    public static bool IsAllowed(int cameraCount, CameraPopupGridLayout layout) => Allowed(cameraCount).Contains(layout);

    /// <summary>
    /// 카메라 수를 바꿨을 때 격자를 맞춘다 — 지금 격자가 새 수에서도 고를 수 있으면 그대로, 아니면 새 수의 기본.
    /// (예: 6대 · 2×3 → 5대면 2×3 유지, 6대 · 6×1 → 5대면 3×2.)
    /// </summary>
    public static CameraPopupGridLayout Snap(int cameraCount, CameraPopupGridLayout? current)
        => current is { } layout && IsAllowed(cameraCount, layout) ? layout : Default(cameraCount);

    /// <summary>타일 영상 가로세로비(16:9) — 격자 고르기의 기준.</summary>
    public const double TileAspect = 16d / 9d;

    /// <summary>
    /// 이벤트 창에 실제로 띄울 격자(<b>정하는 곳은 여기 하나</b> — GIS 창 관리자가 부르고, 호스트는 받은 열 · 행을 그대로 쓴다).
    /// <list type="bullet">
    /// <item>실제 카메라 수 = 설정의 창당 카메라 수 → <b>설정 격자 그대로</b>.</item>
    /// <item>그보다 적으면(매핑 카메라가 모자람 · 일부가 빠짐) → 그 수를 담는 격자 가운데 <b>이 창 크기에서 16:9 타일이 가장 큰 것</b>
    /// (<see cref="BestFit"/>). 예: 960×600 창에 3대 → 3×1(320×180 타일 · 위아래 큰 검은 띠) 대신 2×2 한 칸 비움(480×270).</item>
    /// </list>
    /// 창 크기는 창 전체(머리 · 꼬리 포함) 기준이다 — 머리 · 꼬리는 얇아 순위를 바꾸지 않는다.
    /// </summary>
    public static CameraPopupGridLayout ForWindow(int actualCameras, int configuredCameras, CameraPopupGridLayout configured,
                                                  double windowWidth, double windowHeight)
    {
        int count = ClampCount(actualCameras);
        if (count == ClampCount(configuredCameras) && IsAllowed(count, configured)) return configured;
        return BestFit(count, windowWidth, windowHeight);
    }

    /// <summary>
    /// 그 수를 담는 격자(표의 모든 모양 중 칸 수 ≥ 카메라 수) 가운데 16:9 타일 면적이 가장 큰 것.
    /// 같으면 빈 칸이 적은 쪽, 그래도 같으면 표 순서(가로 우선). 창 크기를 모르면(0 이하) 그 수의 기본 격자.
    /// </summary>
    public static CameraPopupGridLayout BestFit(int cameraCount, double windowWidth, double windowHeight)
    {
        int count = ClampCount(cameraCount);
        if (!(windowWidth > 0) || !(windowHeight > 0) || !double.IsFinite(windowWidth) || !double.IsFinite(windowHeight))
            return Default(count);

        CameraPopupGridLayout best = Default(count);
        double bestArea = -1;
        int bestEmpty = int.MaxValue;
        var seen = new HashSet<CameraPopupGridLayout>();
        foreach (var row in Table)
        {
            foreach (var layout in row)
            {
                if (layout.Capacity < count || !seen.Add(layout)) continue;
                double area = TileArea(layout, windowWidth, windowHeight);
                int empty = layout.Capacity - count;
                // 면적은 소수 오차를 흡수해 비교(같은 값이 계산 순서로 갈리지 않게).
                bool larger = area > bestArea + 1e-6;
                bool same = Math.Abs(area - bestArea) <= 1e-6;
                if (larger || (same && empty < bestEmpty))
                {
                    best = layout;
                    bestArea = area;
                    bestEmpty = empty;
                }
            }
        }
        return best;
    }

    /// <summary>그 격자 한 칸 안에 들어가는 16:9 타일의 면적(창 크기 단위²). 순수.</summary>
    public static double TileArea(CameraPopupGridLayout layout, double windowWidth, double windowHeight)
    {
        if (layout.Columns <= 0 || layout.Rows <= 0) return 0;
        double cellWidth = windowWidth / layout.Columns;
        double cellHeight = windowHeight / layout.Rows;
        double tileWidth = Math.Min(cellWidth, cellHeight * TileAspect);
        return tileWidth * (tileWidth / TileAspect);
    }
}
