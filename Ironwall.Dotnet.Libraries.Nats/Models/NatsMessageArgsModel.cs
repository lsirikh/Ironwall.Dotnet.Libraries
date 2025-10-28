namespace Ironwall.Dotnet.Libraries.Nats.Models;

/****************************************************************************
    Purpose      : NATS 메시지 이벤트 인자 모델
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// NATS 메시지 수신 시 발생하는 이벤트 인자
/// </summary>
public class NatsMessageArgsModel : EventArgs
{
    #region - Ctors -
    public NatsMessageArgsModel()
    {
    }

    public NatsMessageArgsModel(string? subject, string? subscriptionSubject, string? data)
    {
        Subject = subject;
        SubscriptionSubject = subscriptionSubject;
        Data = data;
    }
    #endregion

    #region - Properties -
    /// <summary>
    /// 실제 수신된 Subject
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// 구독한 Subject 패턴 (와일드카드 포함 가능)
    /// </summary>
    public string? SubscriptionSubject { get; set; }

    /// <summary>
    /// 메시지 데이터 (페이로드)
    /// </summary>
    public string? Data { get; set; }
    #endregion
}
