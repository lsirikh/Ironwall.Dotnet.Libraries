using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;

namespace Ironwall.Dotnet.Libraries.Nats.Services;

/// <summary>
/// NATS 메시지 서비스 Generic 인터페이스
/// </summary>
/// <typeparam name="T">구체적인 서비스 타입</typeparam>
public interface IMessageService<T> : IService
{
    /// <summary>
    /// NATS 서버에 동기 연결
    /// </summary>
    T? Connect(INatsSetupModel setupModel);

    /// <summary>
    /// NATS 서버에 비동기 연결
    /// </summary>
    Task<T?> ConnectAsync(INatsSetupModel setupModel);

    /// <summary>
    /// Subject로 메시지 발행
    /// </summary>
    Task PublishAsync(string subject, string data);

    /// <summary>
    /// 동기식 메시지 수신 이벤트
    /// </summary>
    event EventHandler<MessageArgsModel> NatsSubscribeEvent;

    /// <summary>
    /// 비동기식 메시지 수신 이벤트
    /// </summary>
    event Func<MessageArgsModel, Task> NatsSubscribeEventAsync;
}
