namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// NATS 서브시스템 식별자 (Envelope from 필드)
    /// NATS v1.2 §2.3
    /// </summary>
    public enum EnumSubsystem
    {
        Central,
        GIS,
        DBApi,
        PidsProxy,
        NVRManager,
        VMS,
        BroadcastingManager,
        AiAnalysis
    }
}
