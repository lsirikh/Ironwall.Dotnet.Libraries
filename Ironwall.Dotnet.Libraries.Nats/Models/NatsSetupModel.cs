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
        ClientNameNats = model.ClientNameNats;
        UsernameNats = model.UsernameNats;
        PasswordNats = model.PasswordNats;
        ConnectionTimeoutNats = model.ConnectionTimeoutNats;
    }

    public string IpAddressNats { get; set; } = "localhost";
    public int PortNats { get; set; } = 4222;
    public string? DefaultSubjectNats { get; set; }
    public string? ClientNameNats { get; set; }
    public string? UsernameNats { get; set; }
    public string? PasswordNats { get; set; }
    public int ConnectionTimeoutNats { get; set; } = 5000;
}
