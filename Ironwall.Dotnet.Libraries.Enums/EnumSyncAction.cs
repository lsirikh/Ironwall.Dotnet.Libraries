namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// NATS 동기화 액션 (SYNC body.action 필드)
    /// NATS v1.2 §8
    /// </summary>
    public enum EnumSyncAction
    {
        CREATED,
        UPDATED,
        DELETED
    }
}
