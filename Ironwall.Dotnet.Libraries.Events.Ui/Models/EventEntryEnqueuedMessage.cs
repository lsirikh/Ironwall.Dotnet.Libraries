using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : EventQueue Enqueue 완료 시 발행하는 메시지 — entryId를 카드에 전달하기 위한 브릿지
   Created By   : GHLee
   Created On   : 2026-03-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventEntryEnqueuedMessage
{
    public string EntryId { get; }
    public int EventId { get; }
    public int DeviceId { get; }
    public EnumDeviceType DeviceType { get; }
    public EnumEventType EventType { get; }

    public EventEntryEnqueuedMessage(string entryId, int eventId, int deviceId, EnumDeviceType deviceType, EnumEventType eventType)
    {
        EntryId = entryId;
        EventId = eventId;
        DeviceId = deviceId;
        DeviceType = deviceType;
        EventType = eventType;
    }
}
