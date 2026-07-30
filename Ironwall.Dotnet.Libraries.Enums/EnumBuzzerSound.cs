using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 경광등 부저 소리 5종
    /// GIS.md v1.5.2 §2.5 — 와이어 값에 공백/하이픈이 포함되어 멤버명 직렬화 불가.
    /// ("Fire A-WANG" / "Emergency" / "Ambulance" / "PI-PI-PI" / "PI_continue")
    /// 직렬화 시 StringEnumConverter가 EnumMember 값을 사용한다 (DTO 프로퍼티에 컨버터 지정).
    /// </summary>
    public enum EnumBuzzerSound
    {
        [EnumMember(Value = "Fire A-WANG")]
        FireAWang,
        [EnumMember(Value = "Emergency")]
        Emergency,
        [EnumMember(Value = "Ambulance")]
        Ambulance,
        [EnumMember(Value = "PI-PI-PI")]
        PiPiPi,
        [EnumMember(Value = "PI_continue")]
        PiContinue
    }
}
