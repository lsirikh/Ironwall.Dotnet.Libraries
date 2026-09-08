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
            // 라벨 스타일 (Overlay_Title FR-09 v2.5) — 색=EnumColorType(FillColor 케이스 동형)
            case "TitleColor": m.TitleColor = ToEnum<EnumColorType>(v); break;
            case "TitleBackground": m.TitleBackground = ToEnum<EnumColorType>(v); break;
            case "TitleFontFamily": m.TitleFontFamily = v as string ?? string.Empty; break;
            case "TitleBold": m.TitleBold = ToB(v); break;
            case "TitleItalic": m.TitleItalic = ToB(v); break;
            case "TitleMaxWidth": m.TitleMaxWidth = ToD(v); break;
            case "ZOrder": m.ZOrder = ToI(v); break;
            case "Opacity": if (m is GMapImageMarker imOp) imOp.Opacity = ToD(v); break;   // 투명도 undo(D2) — 이미지 전용 UI속성
            case "ShowShape": m.ShowShape = ToB(v); break;
            case "ShowTitle": m.ShowTitle = ToB(v); break;
            case "IsLocked": m.IsLocked = ToB(v); break;
            case "FillColor": m.FillColor = ToEnum<EnumColorType>(v); break;
            case "StrokeColor": m.StrokeColor = ToEnum<EnumColorType>(v); break;
            case "OperationState": m.OperationState = ToEnum<EnumOperationState>(v); break;
            // PIDS 특화(그룹 일괄편집 + undo replay, 기능 ② 확장) — 비PIDS 마커엔 무시
            case "ShowFOV": if (m is IPidsEditableMarker pFov) pFov.ShowFOV = ToB(v); break;
            case "FOVColor": if (m is IPidsEditableMarker pFc) pFc.FOVColor = ToEnum<EnumColorType>(v); break;
            case "FOVOpacity": if (m is IPidsEditableMarker pFo) pFo.FOVOpacity = ToD(v); break;
            case "BaseBearing": if (m is IPidsEditableMarker pBb) pBb.BaseBearing = ToD(v); break;
            case "ModelVariant": if (m is GMapPidsMarker pVariant) pVariant.ModelVariant = v as string; break;
            // 통문·함체 개폐(FR-12) — DoorState 는 런타임 형태 축이라 undo 대상이 아니다
            case "GateWidthM": if (m is IPidsEditableMarker pGw) pGw.GateWidthM = v is null ? null : ToD(v); break;
            case "OpenOnContactOn": if (m is IPidsEditableMarker pOc) pOc.OpenOnContactOn = ToB(v); break;
            // PIDS 그룹 3D 철망(FR-03/04/06) + 기존 미등록 4속성(LinePattern/LineOpacity/IsClosedPath/LinkedDeviceGroup — 죽은 undo 엔트리였음)
            case "PostSpacingM": if (m is IPidsGroupEditableMarker gSp) gSp.PostSpacingM = v is null ? null : ToD(v); break;
            case "FenceHeightM": if (m is IPidsGroupEditableMarker gFh) gFh.FenceHeightM = v is null ? null : ToD(v); break;
            case "FenceMode": if (m is IPidsGroupEditableMarker gFm) gFm.FenceMode = ToEnum<EnumFenceMode>(v); break;
            case "Render3D": if (m is IPidsGroupEditableMarker gR3) gR3.Render3D = ToB(v); break;
            case "ReverseSensorOrder": if (m is IPidsGroupEditableMarker gRs) gRs.ReverseSensorOrder = ToB(v); break;
            case "LinkedDeviceGroup": if (m is IPidsGroupEditableMarker gDg) gDg.LinkedDeviceGroup = ToI(v); break;
            case "LinePattern": if (m is ILineEditableMarker lPat) lPat.LinePattern = ToEnum<EnumLinePattern>(v); break;
            case "LineOpacity": if (m is ILineEditableMarker lOp) lOp.LineOpacity = ToD(v); break;
            case "IsClosedPath": if (m is ILineEditableMarker lCp) lCp.IsClosedPath = ToB(v); break;
            case "Altitude":
                if (m is GMapPidsMarker elevatedDevice) elevatedDevice.Altitude = (float)ToD(v);
                else if (m is GMapInfraMarker elevatedBuilding) elevatedBuilding.Altitude = (float)ToD(v);
                break;
            case "BuildingType": if (m is IInfraEditableMarker building) building.BuildingType = ToEnum<EnumBuildingType>(v); break;
            case "BuildingUsage": if (m is IInfraEditableMarker usage) usage.BuildingUsage = ToEnum<EnumBuildingUsage>(v); break;
            case "FloorCount": if (m is IInfraEditableMarker floors) floors.FloorCount = ToI(v); break;
            case "BasementFloorCount": if (m is IInfraEditableMarker basement) basement.BasementFloorCount = ToI(v); break;
            case "BuildingArea": if (m is IInfraEditableMarker area) area.BuildingArea = ToD(v); break;
        }
    }

    /// <summary>IEditableMarker의 편집 속성을 이름으로 읽기 — <see cref="ApplyProperty"/>의 짝(단일 출처).
    /// 그룹 일괄편집의 per-마커 before 캡처·Pending(혼합값) 판정에 사용. 미지원 속성은 null.</summary>
    public static object? ReadProperty(IEditableMarker m, string prop) => prop switch
    {
        "Title" => m.Title,
        "TitleSize" => m.TitleSize,
        "Bearing" => m.Bearing,
        "Width" => m.Width,
        "Height" => m.Height,
        "Zoom" => m.Zoom,
        "StrokeThickness" => m.StrokeThickness,
        "LabelOffsetX" => m.LabelOffsetX,
        "LabelOffsetY" => m.LabelOffsetY,
        "TitleColor" => m.TitleColor,
        "TitleBackground" => m.TitleBackground,
        "TitleFontFamily" => m.TitleFontFamily,
        "TitleBold" => m.TitleBold,
        "TitleItalic" => m.TitleItalic,
        "TitleMaxWidth" => m.TitleMaxWidth,
        "ZOrder" => m.ZOrder,
        "Opacity" => m is GMapImageMarker imOp ? imOp.Opacity : null,
        "ShowShape" => m.ShowShape,
        "ShowTitle" => m.ShowTitle,
        "IsLocked" => m.IsLocked,
        "FillColor" => m.FillColor,
        "StrokeColor" => m.StrokeColor,
        "OperationState" => m.OperationState,
        "ShowFOV" => m is IPidsEditableMarker pFov ? pFov.ShowFOV : null,
        "FOVColor" => m is IPidsEditableMarker pFc ? pFc.FOVColor : null,
        "FOVOpacity" => m is IPidsEditableMarker pFo ? pFo.FOVOpacity : null,
        "BaseBearing" => m is IPidsEditableMarker pBb ? pBb.BaseBearing : null,
        "ModelVariant" => (m as GMapPidsMarker)?.ModelVariant,
        "GateWidthM" => (m as IPidsEditableMarker)?.GateWidthM,
        "OpenOnContactOn" => (m as IPidsEditableMarker)?.OpenOnContactOn,
        "PostSpacingM" => (m as IPidsGroupEditableMarker)?.PostSpacingM,
        "FenceHeightM" => (m as IPidsGroupEditableMarker)?.FenceHeightM,
        "FenceMode" => (m as IPidsGroupEditableMarker)?.FenceMode,
        "Render3D" => (m as IPidsGroupEditableMarker)?.Render3D,
        "ReverseSensorOrder" => (m as IPidsGroupEditableMarker)?.ReverseSensorOrder,
        "LinkedDeviceGroup" => (m as IPidsGroupEditableMarker)?.LinkedDeviceGroup,
        "LinePattern" => (m as ILineEditableMarker)?.LinePattern,
        "LineOpacity" => (m as ILineEditableMarker)?.LineOpacity,
        "IsClosedPath" => (m as ILineEditableMarker)?.IsClosedPath,
        "Altitude" => m switch { GMapPidsMarker device => device.Altitude, GMapInfraMarker building => building.Altitude, _ => (float?)null },
        "BuildingType" => (m as IInfraEditableMarker)?.BuildingType,
        "BuildingUsage" => (m as IInfraEditableMarker)?.BuildingUsage,
        "FloorCount" => (m as IInfraEditableMarker)?.FloorCount,
        "BasementFloorCount" => (m as IInfraEditableMarker)?.BasementFloorCount,
        "BuildingArea" => (m as IInfraEditableMarker)?.BuildingArea,
        _ => null,
    };

    /// <summary>ApplyProperty가 실제로 복원 가능한 속성명인지 — 미지원명은 죽은 undo 엔트리이므로
    /// EditRecorder에서 기록 억제(CMD-02). 이 목록은 위 switch의 단일 출처.</summary>
    public static bool IsReplayableProperty(string? prop) => prop switch
    {
        "Title" or "TitleSize" or "Bearing" or "Width" or "Height" or "Zoom"
        or "StrokeThickness" or "LabelOffsetX" or "LabelOffsetY" or "ZOrder"
        or "Opacity" or "ShowShape" or "ShowTitle" or "IsLocked" or "FillColor"
        or "StrokeColor" or "OperationState"
        or "TitleColor" or "TitleBackground" or "TitleFontFamily" or "TitleBold" or "TitleItalic" or "TitleMaxWidth"
        or "ShowFOV" or "FOVColor" or "FOVOpacity" or "BaseBearing" or "ModelVariant"
        or "GateWidthM" or "OpenOnContactOn"
        or "PostSpacingM" or "FenceHeightM" or "FenceMode" or "Render3D" or "ReverseSensorOrder"
        or "LinkedDeviceGroup" or "LinePattern" or "LineOpacity" or "IsClosedPath"
        or "Altitude" or "BuildingType" or "BuildingUsage" or "FloorCount" or "BasementFloorCount" or "BuildingArea" => true,
        _ => false,
    };

    private static double ToD(object? v) => v == null ? 0d : Convert.ToDouble(v);
    private static int ToI(object? v) => v == null ? 0 : Convert.ToInt32(v);
    private static bool ToB(object? v) => v != null && Convert.ToBoolean(v);
    private static T ToEnum<T>(object? v) where T : struct, Enum
        => v is T t ? t : (v != null && Enum.TryParse<T>(v.ToString(), out var r) ? r : default);
}
