using System;
using System.Collections.Generic;
using System.Linq;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 선택 묶음 회전 1회(드래그 또는 키보드) — 시작 시 원본(위치·방향·꼭짓점) 스냅샷 →
                  Preview(각)=원본에서 매번 다시 계산(누적 오차 없음) → Commit(변경 목록) / Cancel(원위치).
   Note         : 잠금·dispose 멤버는 제외(그룹 이동 FR-MS-08 과 같은 규칙). 선/구역(ILineEditableMarker)=꼭짓점 전부 +
                  Position 회전(방향 각 불변), 그 외(심볼·장비·이미지)=Position 회전 + Bearing 에 같은 각 더함.
                  장비(IPidsEditableMarker)는 감시 방향(DetectionBearing — 시야 부채꼴 · 3D 카메라 머리, 지도 기준 각)도
                  같은 각 더함(2026-10-19 — 빠뜨리면 아이콘만 돌고 시야는 옛 방향에 남는다). BaseBearing 은 Bearing 기준 상대각이라 그대로.
                  중심 = 원본 위치 + 선 꼭짓점의 메르카토르 경계 상자 중심(GroupRotationMath.BoundsCenter).
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class GroupRotationSession
{
    /// <summary>멤버 1개의 회전 전 상태.</summary>
    public sealed class Original
    {
        public Original(IEditableMarker marker, PointLatLng position, double bearing, List<PointLatLng>? linePoints, double? detectionBearing = null)
        { Marker = marker; Position = position; Bearing = bearing; LinePoints = linePoints; DetectionBearing = detectionBearing; }
        public IEditableMarker Marker { get; }
        public PointLatLng Position { get; }
        public double Bearing { get; }
        /// <summary>선/구역이면 회전 전 꼭짓점, 그 외 null.</summary>
        public List<PointLatLng>? LinePoints { get; }
        /// <summary>장비(점 마커)면 회전 전 감시 방향, 그 외 null.</summary>
        public double? DetectionBearing { get; }
    }

    private readonly List<Original> _originals;

    public GroupRotationSession(IEnumerable<IEditableMarker> markers)
    {
        _originals = (markers ?? Enumerable.Empty<IEditableMarker>())
            .Where(m => m != null && !m.IsDisposed && !m.IsLocked)
            .Select(m => new Original(m, m.Position, m.Bearing, (m as ILineEditableMarker)?.RuntimePoints,
                m is IPidsEditableMarker p && m is not ILineEditableMarker ? p.DetectionBearing : null))
            .ToList();

        var anchors = _originals.SelectMany(o => o.LinePoints is { Count: > 0 } pts ? pts : new List<PointLatLng> { o.Position })
            .Select(p => (p.Lat, p.Lng));
        var c = GroupRotationMath.BoundsCenter(anchors);
        DefaultPivot = c.HasValue ? new PointLatLng(c.Value.lat, c.Value.lng) : PointLatLng.Empty;
        Pivot = DefaultPivot;
    }

    /// <summary>회전 대상(잠금 제외) 원본 목록.</summary>
    public IReadOnlyList<Original> Originals => _originals;
    /// <summary>회전 중심(위경도) — 기본은 <see cref="DefaultPivot"/>, 컨트롤러 판 · 중심 십자 끌기로 바꾼다.</summary>
    public PointLatLng Pivot { get; private set; }
    /// <summary>기본 회전 중심 = 원본의 경계 상자 중심(「가운데로」가 돌아가는 자리).</summary>
    public PointLatLng DefaultPivot { get; }

    /// <summary>중심을 바꾸고 지금 각도로 원본에서 다시 돌린다(미리보기 — 저장 없음).</summary>
    public void SetPivot(PointLatLng pivot)
    {
        Pivot = pivot;
        Preview(Angle);
    }
    /// <summary>현재 미리보기 회전량(도, 시계 방향 +, (-180,180]).</summary>
    public double Angle { get; private set; }
    public bool IsEmpty => _originals.Count == 0;

    /// <summary>원본에서 <paramref name="degrees"/> 만큼 돌린 모습으로 멤버를 옮긴다(미리보기 — 저장 없음).</summary>
    public void Preview(double degrees)
    {
        Angle = GroupRotationMath.WrapSigned(degrees);
        foreach (var o in _originals)
            if (Angle == 0) Restore(o);   // 0° = 원본 그대로(메르카토르 왕복 오차로 저장 없이 미세하게 어긋나지 않게)
            else ApplyTo(o, Angle);
    }

    /// <summary>원위치(회전 0) — ESC 취소.</summary>
    public void Cancel()
    {
        Angle = 0;
        foreach (var o in _originals) Restore(o);
    }

    /// <summary>확정 — 실제로 바뀐 멤버의 원본 목록(회전 0 이면 빈 목록). 저장 · Undo 는 받는 쪽.</summary>
    public IReadOnlyList<Original> Commit()
        => Angle == 0 ? Array.Empty<Original>() : _originals.Where(o => !o.Marker.IsDisposed).ToList();

    private void ApplyTo(Original o, double degrees)
    {
        var m = o.Marker;
        if (m.IsDisposed) return;
        var pos = Rotate(o.Position, degrees);
        if (o.LinePoints != null && m is ILineEditableMarker line)
            line.ApplyGeometry(o.LinePoints.Select(p => Rotate(p, degrees)).ToList(), pos);
        else
        {
            m.UpdateLocation(pos);
            m.UpdateRotation(GroupRotationMath.AddBearing(o.Bearing, degrees));
            if (o.DetectionBearing is double db && m is IPidsEditableMarker p) p.DetectionBearing = GroupRotationMath.AddBearing(db, degrees);
        }
    }

    /// <summary>멤버 1개를 회전 전 상태로 되돌림(취소 · 권한 없음 원위치 공용).</summary>
    public static void Restore(Original o)
    {
        var m = o.Marker;
        if (m.IsDisposed) return;
        if (o.LinePoints != null && m is ILineEditableMarker line)
            line.ApplyGeometry(o.LinePoints, o.Position);
        else
        {
            m.UpdateLocation(o.Position);
            m.UpdateRotation(o.Bearing);
            if (o.DetectionBearing is double db && m is IPidsEditableMarker p) p.DetectionBearing = db;
        }
    }

    private PointLatLng Rotate(PointLatLng p, double degrees)
    {
        var (lat, lng) = GroupRotationMath.RotateAround((p.Lat, p.Lng), (Pivot.Lat, Pivot.Lng), degrees);
        return new PointLatLng(lat, lng);
    }
}
