namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 자동추적 상태
    /// NATS v1.2 §6 NVR 제어
    /// </summary>
    public enum EnumTrackingStatus
    {
        Active,
        Lost,
        Idle
    }
}
