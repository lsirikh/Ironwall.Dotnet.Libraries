namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// NATS 메시지 타입 (Envelope m_type 필드)
    /// NATS v1.2 §3 Envelope 구조
    /// </summary>
    public enum EnumNatsMessageType
    {
        PUB,
        REQ,
        RSP
    }
}
