namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IEnclosureDeviceModel : IBaseDeviceModel
{
    string DoorStatus { get; set; }
    IEnclosureThresholdConfigModel? ThresholdConfig { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }

    /// <summary>접속 IP — 서버 7.0+ <c>connection.ip_address</c>(D-21). 함체도 IP_DIRECT 접속을 가질 수 있다.</summary>
    string? IpAddress { get; set; }

    /// <summary>접속 포트 — 서버 7.0+ <c>connection.ip_port</c>(D-21).</summary>
    int? IpPort { get; set; }

    /// <summary>
    /// 히터 부품의 서버 선언 key(예: <c>heater_1</c>) — <b>읽기 전용 캐시</b>. 직전 GET/목록 응답의
    /// <c>hardware_spec.components</c> 에서 <c>DtoToModelHelper.ToEnclosureDeviceModel</c> 이 채운다.
    /// 선언이 없으면(부품 미선언 함체) <c>null</c>.
    /// </summary>
    /// <remarks>
    /// (D-31 후속) 패널 편집 경로는 <c>hardware_spec</c> 을 쓰기 채널에 절대 싣지 않는다 — 이 값은
    /// <c>DtoToModelHelper.ToEnclosureDeviceDto</c> 가 <c>EnclosureDeviceDto.HeaterComponentKeyHint</c>
    /// (조회 전용, <c>[JsonIgnore]</c>)로만 옮긴다. UI 는 이 값의 유무로 히터 토글 가용성을 판정한다
    /// (<c>EnclosureDeviceViewModel.IsHeaterToggleEnabled</c>).
    /// </remarks>
    string? HeaterComponentKey { get; set; }

    /// <summary>팬 부품의 서버 선언 key — <see cref="HeaterComponentKey"/> 와 같은 계약.</summary>
    string? FanComponentKey { get; set; }
}
