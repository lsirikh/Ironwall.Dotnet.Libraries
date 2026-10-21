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

    /// <summary>
    /// NATS 연결이 끊겼다가 다시 열렸을 때 1회 난다(첫 연결은 알리지 않는다, G53).
    /// 재연결 사이 놓친 알림을 메우는 재적재(호스트 H1)가 이 사건을 받는다.
    /// 기본 구현은 아무것도 하지 않는다 — 시험용 가짜 구현이 따로 고칠 필요가 없게.
    /// </summary>
    event EventHandler? Reconnected { add { } remove { } }
}
