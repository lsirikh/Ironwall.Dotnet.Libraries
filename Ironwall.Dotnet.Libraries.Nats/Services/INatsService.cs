namespace Ironwall.Dotnet.Libraries.Nats.Services;

/// <summary>
/// NATS 서비스 인터페이스
/// </summary>
public interface INatsService : IMessageService<INatsService>
{
    /// <summary>
    /// 현재 구독 중인 기본 Subject
    /// </summary>
    string Subject { get; }
}
