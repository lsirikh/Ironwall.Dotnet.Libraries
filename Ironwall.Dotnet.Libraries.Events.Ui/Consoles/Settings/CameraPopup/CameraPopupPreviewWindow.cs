namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 모니터 미리보기 속 창 하나(첫 창 · 계단 고스트) (PRD FR-12)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>미리보기 캔버스 안 창 자리(DIU). 첫 창은 끌 수 있고, 뒤 창(고스트)은 쌓일 자리만 보인다.</summary>
public sealed record CameraPopupPreviewWindow(int Number, double Left, double Top, double Width, double Height,
                                              int Columns, int Rows, bool IsFirst)
{
    /// <summary>미니 타일 수(격자 칸).</summary>
    public IReadOnlyList<int> Tiles => Enumerable.Range(0, Math.Max(1, Columns * Rows)).ToList();
}

/// <summary>창 크기 고르기 한 줄.</summary>
public sealed record CameraPopupSizeChoice(int Width, int Height)
{
    public string Label => $"{Width} × {Height}";
    public override string ToString() => Label;
}

/// <summary>숫자 고르기 한 줄(동시 창 수 · 브로커 모니터 · 칸).</summary>
public sealed record CameraPopupNumberChoice(int Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>모니터 고르기 한 줄.</summary>
public sealed record CameraPopupMonitorChoice(Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.DisplayMonitorInfo Monitor, string Label)
{
    public string Id => Monitor.Id;
    public override string ToString() => Label;
}

/// <summary>[저장] 결과 — 호스트가 설정 막대의 칸 결과로 옮긴다.</summary>
/// <param name="Applied">저장했고 다시 읽어 같은 값을 확인했다.</param>
/// <param name="Reason">운영자 문장(실패 시).</param>
/// <param name="Detail">원문(로그 전용).</param>
public sealed record CameraPopupApplyResult(bool Applied, bool Skipped, string Reason = "", string Detail = "");
