using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 커맨드 공통 베이스 — 컨텍스트 참조 + IEditableMarker 속성 적용 헬퍼.
   Note         : IEditableMarker가 대부분 편집속성을 setter로 노출 → 값 커맨드는 스냅샷 없이 setter로 적용.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public abstract class UndoableCommandBase : IUndoableCommand
{
    protected readonly IUndoApplyContext Ctx;
    protected UndoableCommandBase(IUndoApplyContext ctx) => Ctx = ctx;

    public abstract string Description { get; }
    public int ScopeMapId { get; set; }
    public abstract Task ExecuteAsync(CancellationToken ct = default);
    public abstract Task UndoAsync(CancellationToken ct = default);

    /// <summary>IEditableMarker의 편집 속성을 이름으로 세팅(값 커맨드 공용). 미지원 속성은 무시.
    /// public — 멀티셀렉트 그룹 속성 일괄반영(MapViewModel)에서도 재사용(단일 출처).</summary>
    public static void ApplyProperty(IEditableMarker m, string prop, object? v)
    {
        switch (prop)
        {
            case "Title": m.Title = v as string ?? string.Empty; break;
            case "TitleSize": m.TitleSize = ToD(v); break;
            case "Bearing": m.UpdateRotation(ToD(v)); break;
            case "Width": m.UpdateSize(ToD(v), m.Height); break;
            case "Height": m.UpdateSize(m.Width, ToD(v)); break;
            case "Zoom": m.Zoom = ToD(v); break;
            case "StrokeThickness": m.StrokeThickness = ToD(v); break;
            case "LabelOffsetX": m.LabelOffsetX = ToD(v); break;
            case "LabelOffsetY": m.LabelOffsetY = ToD(v); break;
            case "ZOrder": m.ZOrder = ToI(v); break;
            case "Opacity": if (m is GMapImageMarker imOp) imOp.Opacity = ToD(v); break;   // 투명도 undo(D2) — 이미지 전용 UI속성
            case "ShowShape": m.ShowShape = ToB(v); break;
            case "ShowTitle": m.ShowTitle = ToB(v); break;
            case "IsLocked": m.IsLocked = ToB(v); break;
            case "FillColor": m.FillColor = ToEnum<EnumColorType>(v); break;
            case "StrokeColor": m.StrokeColor = ToEnum<EnumColorType>(v); break;
            case "OperationState": m.OperationState = ToEnum<EnumOperationState>(v); break;
        }
    }

    /// <summary>ApplyProperty가 실제로 복원 가능한 속성명인지 — 미지원명은 죽은 undo 엔트리이므로
    /// EditRecorder에서 기록 억제(CMD-02). 이 목록은 위 switch의 단일 출처.</summary>
    public static bool IsReplayableProperty(string? prop) => prop switch
    {
        "Title" or "TitleSize" or "Bearing" or "Width" or "Height" or "Zoom"
        or "StrokeThickness" or "LabelOffsetX" or "LabelOffsetY" or "ZOrder"
        or "Opacity" or "ShowShape" or "ShowTitle" or "IsLocked" or "FillColor"
        or "StrokeColor" or "OperationState" => true,
        _ => false,
    };

    private static double ToD(object? v) => v == null ? 0d : Convert.ToDouble(v);
    private static int ToI(object? v) => v == null ? 0 : Convert.ToInt32(v);
    private static bool ToB(object? v) => v != null && Convert.ToBoolean(v);
    private static T ToEnum<T>(object? v) where T : struct, Enum
        => v is T t ? t : (v != null && Enum.TryParse<T>(v.ToString(), out var r) ? r : default);
}
