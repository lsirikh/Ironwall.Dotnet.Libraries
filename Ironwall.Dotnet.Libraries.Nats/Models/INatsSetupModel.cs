namespace Ironwall.Dotnet.Libraries.Nats.Models;

/// <summary>
/// NATS 연결 설정 인터페이스
/// </summary>
public interface INatsSetupModel
{
    /// <summary>
    /// NATS 서버 IP 주소
    /// </summary>
    string IpAddressNats { get; set; }

    /// <summary>
    /// NATS 서버 포트
    /// </summary>
    int PortNats { get; set; }

    /// <summary>
    /// 기본 구독 Subject (선택사항)
    /// </summary>
    string? DefaultSubjectNats { get; set; }

    /// <summary>
    /// 클라이언트 이름 (선택사항)
    /// </summary>
    string? ClientNameNats { get; set; }

    /// <summary>
    /// 인증 사용자명 (선택사항)
    /// </summary>
    string? UsernameNats { get; set; }

    /// <summary>
    /// 인증 비밀번호 (선택사항)
    /// </summary>
    string? PasswordNats { get; set; }

    /// <summary>
    /// 연결 타임아웃 (밀리초, 기본값: 5000)
    /// </summary>
    int ConnectionTimeoutNats { get; set; }
}
