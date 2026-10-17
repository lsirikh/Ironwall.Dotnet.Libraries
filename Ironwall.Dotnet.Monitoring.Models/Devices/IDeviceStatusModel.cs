using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 관측 축(<c>device_status</c>) — 부품 key 별 관측 상태. 문 위치·부품 건강의 유일한 자리.
/// <b>읽기 전용</b>: 매니저가 보고한 값이고 클라가 쓰면 422 <c>OBSERVED_FIELD</c> 다.
/// </summary>
public interface IDeviceStatusModel
{
    int? Schema { get; set; }

    /// <summary>부품 key → 관측 상태. 선언은 있는데 항목이 없으면 "아직 관측된 적 없다".</summary>
    IDictionary<string, ComponentStatusModel> Components { get; }
}
