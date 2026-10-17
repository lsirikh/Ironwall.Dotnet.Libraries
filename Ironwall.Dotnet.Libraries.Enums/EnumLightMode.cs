using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 경광등 점등 모드
    /// GIS.md v1.5.2 §2.4 — 와이어 값은 소문자("steady"/"blinking").
    /// 직렬화 시 StringEnumConverter가 EnumMember 값을 사용한다 (DTO 프로퍼티에 컨버터 지정).
    /// </summary>
    public enum EnumLightMode
    {
        [EnumMember(Value = "steady")]
        Steady,
        [EnumMember(Value = "blinking")]
        Blinking
    }
}
