using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 경광등 색상 5색
    /// GIS.md v1.5.2 §2.4 — 와이어 값은 멤버명과 동일(Red/Orange/Green/Blue/White)이나,
    /// 스펙 문자열 고정을 위해 EnumMember로 명시한다 (DTO 프로퍼티에 StringEnumConverter 지정).
    /// </summary>
    public enum EnumLampColor
    {
        [EnumMember(Value = "Red")]
        Red,
        [EnumMember(Value = "Orange")]
        Orange,
        [EnumMember(Value = "Green")]
        Green,
        [EnumMember(Value = "Blue")]
        Blue,
        [EnumMember(Value = "White")]
        White
    }
}
