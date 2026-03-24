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
    /// 기본 구독 Subject 폴백 값 (DomainNats/GroupNats/SubsystemNats 미설정 시 사용)
    /// </summary>
    string? DefaultSubjectNats { get; set; }

    /// <summary>
    /// NATS Subject 도메인 접두사 (예: "sensorway")
    /// </summary>
    string? DomainNats { get; set; }

    /// <summary>
    /// NATS Subject 그룹(부대ID) (예: "unit001")
    /// </summary>
    string? GroupNats { get; set; }

    /// <summary>
    /// 서브시스템 이름 — NATS 연결 이름 및 Subject 구성에 사용 (예: "gis")
    /// </summary>
    string? SubsystemNats { get; set; }

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

    /// <summary>
    /// 실제 사용할 Subject 계산 속성.
    /// DomainNats, GroupNats, SubsystemNats 모두 설정 시 "{Domain}.{Group}.{Subsystem}.>" 반환.
    /// 하나라도 비어있으면 DefaultSubjectNats 또는 "default.>" 반환.
    /// </summary>
    string EffectiveSubject { get; }
}
