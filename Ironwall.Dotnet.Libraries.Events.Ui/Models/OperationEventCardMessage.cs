namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : NATS OPERATION_EVENT 한 건 → 이벤트 패널 운영 카드 (operation-event-card PRD §7)
                  OperationEventNatsSyncService 가 발행하고 EventCardListPanelViewModel 이 받아 카드로 올린다.
   Created By   : Claude
   Created On   : 2026-10-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OperationEventCardMessage
{
    public OperationEventModel Model { get; }

    public OperationEventCardMessage(OperationEventModel model)
    {
        Model = model;
    }
}
