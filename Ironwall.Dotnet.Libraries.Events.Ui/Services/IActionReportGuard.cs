namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 조치보고 멱등 가드 — 동일 EventId에 대한 ActionEvent 중복 생성 차단.
                  자동/자동복구/배치(EventCardListPanel) + 수동(ReportDialog→EventCard.SendAction)
                  경로가 같은 싱글톤 인스턴스를 공유해 in-flight 충돌을 막는다.
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public interface IActionReportGuard
{
    /// <summary>
    /// EventId 점유 시도. eventId<=0 이면 가드 대상 아님(항상 true).
    /// </summary>
    /// <returns>점유 성공(=진행 가능) true, 이미 진행 중이면 false(=중복 스킵).</returns>
    bool TryEnter(int eventId);

    /// <summary>점유 해제. eventId<=0 이면 무시.</summary>
    void Exit(int eventId);
}
