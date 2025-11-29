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
            /*──────────────────────────────────────────────────────────────
             *  1. 날짜 범위 + 유효한 Device 필터링
             *──────────────────────────────────────────────────────────────*/
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .Where(ev => ev.Device is IControllerDeviceModel
                          || (ev.Device is ISensorDeviceModel sensor && sensor.Controller != null))
                .ToList();

            /*──────────────────────────────────────────────────────────────
             *  2. 컨트롤러별 카운팅
             *──────────────────────────────────────────────────────────────*/
            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c =>
                {
                    var count = evInRange.Count(ev =>
                    {
                        var deviceNumber = ev.Device switch
                        {
                            IControllerDeviceModel ctrl => ctrl.DeviceNumber,
                            ISensorDeviceModel sensor => sensor.Controller?.DeviceNumber ?? -1,
                            _ => -1
                        };

                        return deviceNumber == c.DeviceNumber;
                    });

                    return (double)count;
                })
                .ToList();
        }

        public static List<double> GetConnectionCountsByController(
    DateTime startDate, DateTime endDate,
    IEnumerable<IControllerDeviceModel> controllers,
    IEnumerable<IConnectionEventModel> allEvents)
        {
            /*──────────────────────────────────────────────────────────────
             *  1. 날짜 범위 필터링
             *──────────────────────────────────────────────────────────────*/
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .ToList();

            /*──────────────────────────────────────────────────────────────
             *  2. Device가 유효한 Controller 정보를 가진 이벤트만 필터링
             *──────────────────────────────────────────────────────────────*/
            var evWithController = evInRange
                .Where(ev => ev.Device is IControllerDeviceModel
                          || (ev.Device is ISensorDeviceModel sensor && sensor.Controller != null))
                .ToList();

            /*──────────────────────────────────────────────────────────────
             *  3. 컨트롤러별 카운팅
             *     
             *     Device 타입에 따른 DeviceNumber 추출:
             *     ┌─────────────────────────┬─────────────────────────────┐
             *     │ IControllerDeviceModel  │ ev.Device.DeviceNumber      │
             *     │ ISensorDeviceModel      │ ev.Device.Controller.DeviceNumber │
             *     └─────────────────────────┴─────────────────────────────┘
             *──────────────────────────────────────────────────────────────*/
            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c =>
                {
                    var count = evWithController.Count(ev =>
                    {
                        // Device 타입에 따라 DeviceNumber 추출
                        var deviceNumber = ev.Device switch
                        {
                            IControllerDeviceModel ctrl => ctrl.DeviceNumber,
                            ISensorDeviceModel sensor => sensor.Controller?.DeviceNumber ?? -1,
                            _ => -1
                        };

                        return deviceNumber == c.DeviceNumber;
                    });

                    return (double)count;
                })
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