namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 악천후 모드 7종
    /// NATS v1.2 §6 NVR 제어
    /// </summary>
    public enum EnumWeatherMode
    {
        NORMAL,
        FOG,
        SEA_FOG,
        YELLOW_DUST,
        RAIN,
        SNOW,
        HEAT_HAZE
    }
}
