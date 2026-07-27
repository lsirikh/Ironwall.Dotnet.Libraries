using System;
using System.Windows;
using System.Windows.Documents;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>측정 리드아웃 스냅샷 — HUD 바인딩용(값/단위 분리). Measure_Tools FR-02.</summary>
public readonly record struct MeasureReadout(
    bool IsActive,
    MeasureKind Kind,
    int PointCount,
    string PrimaryLabel, string PrimaryValue, string PrimaryUnit,
    bool HasSecondary,
    string SecondaryLabel, string SecondaryValue, string SecondaryUnit,
    string Hint);

/****************************************************************************
   Purpose      : 측정 툴 상태·수명주기 제어 — MeasureAdorner(맵 AdornerLayer) 부착/해제,
                  점 추가·제거·완료·취소, 위경도(MeasureMath) 기반 리드아웃 계산 후 이벤트 통지.
                  Measure_Tools FR-03/04. DB 미저장(임시), 컨트롤(GMapCustomControl)이 소유.
   Note         : 완료(Finish) 후에는 결과를 화면에 고정 표시하고, 다음 점 추가 시 새 측정을 시작.
                  리드아웃은 라이브(미리보기 커서 포함)로 계산하되 완료 후에는 확정점만 사용.
   Created On   : 2026-07-24 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class MeasureController : IDisposable
{
    private readonly GMapControl _map;
    private readonly ILogService? _log;
    private MeasureAdorner? _adorner;
    private bool _finished;
    private bool _disposed;

    public bool IsActive { get; private set; }
    public MeasureKind Mode { get; private set; } = MeasureKind.Length;

    /// <summary>리드아웃 변경(점 추가/제거/미리보기/완료/취소 시). HUD 바인딩용.</summary>
    public event Action<MeasureReadout>? ReadoutChanged;

    public MeasureController(GMapControl map, ILogService? log = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
    }

    #region - 수명주기 -
    /// <summary>측정 모드 시작(또는 종류 전환). 기존 측정은 초기화.</summary>
    public void Start(MeasureKind kind)
    {
        if (_disposed) return;
        if (_adorner == null) Attach(kind);
        else _adorner.Clear();

        Mode = kind;
        if (_adorner != null) _adorner.Kind = kind;
        _finished = false;
        IsActive = true;
        RaiseReadout();
        _log?.Info($"[Measure] 시작 — {kind}");
    }

    /// <summary>측정 모드 종료 — 어도너 해제, 상태 초기화.</summary>
    public void Stop()
    {
        Detach();
        IsActive = false;
        _finished = false;
        RaiseReadout();   // IsActive=false → HUD 숨김
        _log?.Info("[Measure] 종료");
    }

    private void Attach(MeasureKind kind)
    {
        var layer = AdornerLayer.GetAdornerLayer(_map);
        if (layer == null) { _log?.Error("[Measure] AdornerLayer 없음 — 부착 실패"); return; }
        _adorner = new MeasureAdorner(_map, kind, _log);
        layer.Add(_adorner);
    }

    private void Detach()
    {
        if (_adorner == null) return;
        try { AdornerLayer.GetAdornerLayer(_map)?.Remove(_adorner); }
        catch (Exception ex) { _log?.Error($"[Measure] 어도너 제거 실패: {ex.Message}"); }
        _adorner.Dispose();
        _adorner = null;
    }
    #endregion

    #region - 인터랙션 -
    /// <summary>점 추가. 완료 상태였다면 새 측정으로 리셋 후 첫 점.</summary>
    public void AddPoint(PointLatLng geo)
    {
        if (!IsActive || _adorner == null) return;
        if (_finished) { _adorner.Clear(); _finished = false; }
        _adorner.AddPoint(geo);
        RaiseReadout();
    }

    /// <summary>미리보기 커서 갱신(라이브 리드아웃). 완료 상태에서는 무시.</summary>
    public void UpdateMouse(Point? screen)
    {
        if (!IsActive || _adorner == null || _finished) return;
        _adorner.UpdateMouse(screen);
        RaiseReadout();
    }

    /// <summary>마지막 점 제거(Backspace/Ctrl+Z).</summary>
    public void Undo()
    {
        if (!IsActive || _adorner == null) return;
        _finished = false;
        _adorner.RemoveLastPoint();
        RaiseReadout();
    }

    /// <summary>측정 완료(더블클릭/Enter) — 유효 시 미리보기 제거·결과 고정.</summary>
    public bool Finish()
    {
        if (!IsActive || _adorner == null || !_adorner.IsValid) return false;
        _adorner.Freeze();
        _finished = true;
        RaiseReadout();
        _log?.Info($"[Measure] 완료 — {Mode}, {_adorner.PointCount}점");
        return true;
    }
    #endregion

    #region - 리드아웃 -
    private void RaiseReadout() => ReadoutChanged?.Invoke(BuildReadout());

    private MeasureReadout BuildReadout()
    {
        if (_adorner == null || !IsActive)
            return new MeasureReadout(false, Mode, 0, "", "", "", false, "", "", "", "");

        int count = _adorner.PointCount;
        var pts = _finished ? _adorner.Points : _adorner.LivePoints();

        if (Mode == MeasureKind.Area)
        {
            var (av, au) = MeasureFormat.AreaParts(MeasureMath.AreaSquareMeters(pts));
            var (pv, pu) = MeasureFormat.DistanceParts(MeasureMath.PerimeterMeters(pts));
            return new MeasureReadout(true, Mode, count,
                "면적", av, au, true, "둘레", pv, pu, AreaHint(count));
        }
        else
        {
            var (dv, du) = MeasureFormat.DistanceParts(MeasureMath.TotalLengthMeters(pts));
            return new MeasureReadout(true, Mode, count,
                "거리", dv, du, false, "", "", "", LengthHint(count));
        }
    }

    private string LengthHint(int count)
        => _finished ? "완료 — 클릭하여 새 측정"
         : count == 0 ? "지도를 클릭해 시작"
         : "점 추가 · 더블클릭/Enter로 완료 · ESC 취소";

    private string AreaHint(int count)
        => _finished ? "완료 — 클릭하여 새 측정"
         : count < 3 ? "최소 3점을 찍으세요"
         : "점 추가 · 더블클릭/Enter로 완료 · ESC 취소";
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        IsActive = false;
    }
}
