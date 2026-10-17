namespace Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;

/// <summary>
/// NATS Publish 메시지 (단방향 발행, 응답 불필요)
/// <para>type_message = "PUB" 고정</para>
/// </summary>
/// <typeparam name="TBody">Data 타입</typeparam>
public class BrokerPublish<TBody> : BaseMessage<TBody> where TBody : class
{
    public BrokerPublish()
    {
        TypeMessage = "PUB";
    }
}
