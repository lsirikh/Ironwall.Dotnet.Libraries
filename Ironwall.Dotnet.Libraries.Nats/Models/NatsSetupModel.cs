namespace Ironwall.Dotnet.Libraries.Nats.Models;

/****************************************************************************
    Purpose      : NATS 연결 설정 모델
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// NATS 연결 설정 구현 클래스
/// </summary>
public class NatsSetupModel : INatsSetupModel
{
    public NatsSetupModel()
    {
    }

    public NatsSetupModel(INatsSetupModel model)
    {
        IpAddressNats = model.IpAddressNats;
        PortNats = model.PortNats;
        DefaultSubjectNats = model.DefaultSubjectNats;
        DomainNats = model.DomainNats;
        GroupNats = model.GroupNats;
        SubsystemNats = model.SubsystemNats;
        UsernameNats = model.UsernameNats;
        PasswordNats = model.PasswordNats;
        ConnectionTimeoutNats = model.ConnectionTimeoutNats;
    }

    public string IpAddressNats { get; set; } = "localhost";
    public int PortNats { get; set; } = 4222;
    public string? DefaultSubjectNats { get; set; }
    public string? DomainNats { get; set; }
    public string? GroupNats { get; set; }
    public string? SubsystemNats { get; set; }
    public string? UsernameNats { get; set; }
    public string? PasswordNats { get; set; }
    public int ConnectionTimeoutNats { get; set; } = 5000;

    public string EffectiveSubject =>
        !string.IsNullOrEmpty(DomainNats) &&
        !string.IsNullOrEmpty(GroupNats) &&
        !string.IsNullOrEmpty(SubsystemNats)
            ? $"{DomainNats}.{GroupNats}.{SubsystemNats}.>"
            : DefaultSubjectNats ?? "default.>";
}
