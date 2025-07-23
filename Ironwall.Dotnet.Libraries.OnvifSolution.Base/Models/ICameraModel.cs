using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
public interface ICameraModel : IConnectionModel
{
    // ───────────────────────── Identification ─────────────────────────
    /// <summary>카메라(사이트) 별칭 - 예: “FrontGate”, “LobbyCam-01”</summary>
    string? Name { get; set; }

    /// <summary>설치 위치(건물·층·지점) - UI 표시용 메타데이터</summary>
    string? Location { get; set; }

    /// <summary>제조사 - <c>Dahua</c>, <c>Axis</c>, <c>Hanwha Vision</c> …</summary>
    string? Manufacturer { get; set; }

    /// <summary>모델명 / Part Number - 예: <c>QNP-6320</c></summary>
    string? DeviceModel { get; set; }

    /// <summary>하드웨어 리비전·보드 버전 등 (장치 제공 시)</summary>
    string? Hardware { get; set; }

    /// <summary>펌웨어 버전 - ex) <c>2.41.16 build 220110</c></summary>
    string? FirmwareVersion { get; set; }

    /// <summary>시리얼 넘버 또는 Display Serial</summary>
    string? SerialNumber { get; set; }

    /// <summary>ONVIF <c>GetDeviceInformation</c> 의 <em>HardwareId</em> 필드</summary>
    string? HardwareId { get; set; }

    /// <summary>MAC Address (가능하면 ‘콜론(:) 기호’ 포함 12-byte)</summary>
    string? MacAddress { get; set; }

    /// <summary>장치가 보고한 ONVIF 규격 버전 - 예: <c>16.12</c> (<c>major.minor</c>)</summary>
    string? OnvifVersion { get; set; }

    /// <summary>
    /// Device Service URI – 일반적으로  
    /// <c>http://&lt;ip&gt;:&lt;port&gt;/onvif/device_service</c>
    /// </summary>
    string? ServiceUri { get; set; }

    // ───────────────────────── Runtime Status ─────────────────────────
    /// <summary>카메라 타입 : 고정/PTZ/미정</summary>
    EnumCameraType Type { get; set; }

    /// <summary>현재 ONVIF 접속 가능 여부</summary>
    EnumCameraStatus CameraStatus { get; set; }

    /// <summary>
    /// Media Profile·스트림 URI·PTZ Preset 등
    /// 카메라 Media/PTZ 서비스에서 수집한 복합 데이터
    /// </summary>
    CameraMediaModel? CameraMedia { get; set; }

    /// <summary>마지막으로 정보를 갱신한 시각(UTC 권장)</summary>
    DateTime UpdateTime { get; set; }

    // ───────────────────────── Operations ─────────────────────────
    /// <summary>
    /// <para>
    /// 전달받은 <paramref name="src"/> 의 모든 속성 값을 현재 인스턴스에 복사합니다.
    /// 필요 시 <see cref="IConnectionModel"/> 부분도 함께 동기화합니다.
    /// </para>
    /// <para>⚠ 참고 : 얕은 복사이므로 <see cref="CameraMediaModel"/> 같이
    /// 참조형 하위 객체는 별도 <c>Clone()</c> 로 깊은 복사를 수행하거나
    /// 불변 객체(Immutable) 설계를 추천합니다.</para>
    /// </summary>
    void Update(ICameraModel src);
}