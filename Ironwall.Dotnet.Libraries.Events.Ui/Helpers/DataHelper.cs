using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/28/2025 5:06:33 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public static class DataHelper
    {
        public static List<double> GetDetectionCountsByController(
                DateTime _startDate, DateTime _endDate,
                IEnumerable<IControllerDeviceModel> controllers,
                IEnumerable<IDetectionEventModel> allEvents)
        {
            // 1. 기간 + 컨트롤러 필터
            var evInRange = allEvents.OfType<IDetectionEventModel>()
                .Where(ev =>
                    ev.DateTime >= _startDate &&
                    ev.DateTime < _endDate &&
                    (ev.Device as ISensorDeviceModel)?.Controller != null);

            // 2. 컨트롤러(DeviceNumber)별 카운트
            return controllers
               .OrderBy(c => c.DeviceNumber)
               .Select(c => (double)evInRange.Count(ev =>
                   ((ISensorDeviceModel)ev.Device!).Controller!.DeviceNumber == c.DeviceNumber))
               .ToList();
        }

        public static List<double> GetMalfunctionCountsByController(
        DateTime startDate, DateTime endDate,
        IEnumerable<IControllerDeviceModel> controllers,
        IEnumerable<IMalfunctionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev =>
                    ev.DateTime >= startDate &&
                    ev.DateTime < endDate &&
                    ev.Device is ISensorDeviceModel sensor &&
                    sensor.Controller != null);

            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c => (double)evInRange.Count(ev =>
                    ((ISensorDeviceModel)ev.Device!).Controller!.DeviceNumber == c.DeviceNumber))
                .ToList();
        }

        public static List<double> GetConnectionCountsByController(
       DateTime startDate, DateTime endDate,
       IEnumerable<IControllerDeviceModel> controllers,
       IEnumerable<IConnectionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev =>
                    ev.DateTime >= startDate &&
                    ev.DateTime < endDate &&
                    ev.Device is ISensorDeviceModel sensor &&
                    sensor.Controller != null);

            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c => (double)evInRange.Count(ev =>
                    ((ISensorDeviceModel)ev.Device!).Controller!.DeviceNumber == c.DeviceNumber))
                .ToList();
        }

        public static List<double> GetActionCountsByController(
        DateTime startDate, DateTime endDate,
        IEnumerable<IControllerDeviceModel> controllers,
        IEnumerable<IActionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev =>
                    ev.DateTime >= startDate &&
                    ev.DateTime < endDate &&
                    ev.OriginEvent?.Device is ISensorDeviceModel sensor &&
                    sensor.Controller != null);

            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c => (double)evInRange.Count(ev =>
                    ((ISensorDeviceModel)ev.OriginEvent!.Device!).Controller!.DeviceNumber == c.DeviceNumber))
                .ToList();
        }
    }
}