namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 통문(Gate) 장비 — 서버 v6.3 신설 타입. 함체(<see cref="IEnclosureDeviceModel"/>)와 달리
/// 임계 설정·히터·팬이 없고, 개폐 상태와 결선 정보만 갖는다.
/// </summary>
public interface IGateDeviceModel : IBaseDeviceModel
{
    /// <summary>통문 개폐 상태 — "CLOSED" / "OPEN". 개폐 <b>명령</b>으로는 바뀌지 않는다(매니저 보고로만 전이).</summary>
    string GateStatus { get; set; }

    /// <summary>이미지·통합관리 링크 원본 JSON 문자열(서버 `urls`). 파싱은 소비처 책임.</summary>
    string? UrlsJson { get; set; }

    /// <summary>결선 방식 정보 원본 JSON 문자열(서버 `link_info`) — 구동 주체 판단 근거.</summary>
    string? LinkInfoJson { get; set; }
}
