using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      : 등록 센서 정보 오버레이 행 모델 (pidsgroup-rightclick FR-05)
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 등록 센서 정보 오버레이의 테이블 1행 — 열람 시점 스냅샷(불변).
/// 상태는 <see cref="EnumCompositeEventStatus"/>(EQM GetDeviceState 재계산값).
/// </summary>
public class SensorInfoRowModel
{
    public int DeviceId { get; init; }
    public int DeviceNumber { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    /// <summary>제어기 이름 — 갱신 경로에서 Controller null 가능(V-04) → 조립부가 "—" 폴백.</summary>
    public string ControllerName { get; init; } = "—";
    public string Location { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public EnumCompositeEventStatus State { get; init; }

    public string StateText => State switch
    {
        EnumCompositeEventStatus.Detecting        => "탐지",
        EnumCompositeEventStatus.FaultedDetecting => "탐지+장애",
        EnumCompositeEventStatus.Faulted          => "장애",
        EnumCompositeEventStatus.Connection       => "연결",
        EnumCompositeEventStatus.Blackout         => "통신두절",
        _                                         => "정상",
    };
}
