using Ironwall.Dotnet.Libraries.Enums;
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
        #region Private Helpers (Phase 16)

        /// <summary>
        /// Device에서 Controller DeviceNumber 추출
        /// - IControllerDeviceModel: 직접 DeviceNumber 반환
        /// - ISensorDeviceModel: Controller.DeviceNumber 반환
        /// - ICameraDeviceModel: -1 반환 (Controller 속성 없음)
        /// - null/기타: -1 반환
        /// </summary>
        public static int GetControllerNumber(IBaseDeviceModel? device)
        {
            return device switch
            {
                IControllerDeviceModel ctrl => ctrl.DeviceNumber,
                ISensorDeviceModel sensor => sensor.Controller?.DeviceNumber ?? -1,
                _ => -1
            };
        }

        /// <summary>
        /// 이벤트의 Device가 특정 Device와 매칭되는지 확인
        /// 1. ID 직접 매칭
        /// 2. DeviceNumber + DeviceType 매칭
        /// 3. Sensor/Camera → Controller 매핑 (Controller 기준 조회 시)
        /// </summary>
        public static bool IsDeviceMatch(IBaseDeviceModel? eventDevice, IBaseDeviceModel targetDevice)
        {
            if (eventDevice == null) return false;

            // 1. 직접 ID 매칭
            if (eventDevice.Id == targetDevice.Id) return true;

            // 2. DeviceNumber + DeviceType 매칭
            if (eventDevice.DeviceNumber == targetDevice.DeviceNumber &&
                eventDevice.DeviceType == targetDevice.DeviceType) return true;

            // 3. Sensor/Camera → Controller 매핑 (Controller 기준 조회 시)
            if (targetDevice is IControllerDeviceModel ctrl)
            {
                var controllerNumber = GetControllerNumber(eventDevice);
                return controllerNumber == ctrl.DeviceNumber;
            }

            return false;
        }

        #endregion

        #region Phase 16 New Methods

        /// <summary>
        /// Detection 이벤트를 Device별로 카운트
        /// - 지원: ISensorDeviceModel, ICameraDeviceModel
        /// - Controller 기준 조회 시 하위 Sensor/Camera도 집계됨
        /// </summary>
        public static List<double> GetDetectionCountsByDevice(
            DateTime startDate, DateTime endDate,
            IEnumerable<IBaseDeviceModel> devices,
            IEnumerable<IDetectionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .ToList();

            return devices
                .OrderBy(d => d.DeviceNumber)
                .Select(device => (double)evInRange.Count(ev => IsDeviceMatch(ev.Device, device)))
                .ToList();
        }

        /// <summary>
        /// Malfunction 이벤트를 Device별로 카운트
        /// - 지원: IControllerDeviceModel, ISensorDeviceModel
        /// </summary>
        public static List<double> GetMalfunctionCountsByDevice(
            DateTime startDate, DateTime endDate,
            IEnumerable<IBaseDeviceModel> devices,
            IEnumerable<IMalfunctionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .ToList();

            return devices
                .OrderBy(d => d.DeviceNumber)
                .Select(device => (double)evInRange.Count(ev => IsDeviceMatch(ev.Device, device)))
                .ToList();
        }

        /// <summary>
        /// Connection 이벤트를 Device별로 카운트
        /// - 지원: IControllerDeviceModel, ISensorDeviceModel
        /// </summary>
        public static List<double> GetConnectionCountsByDevice(
            DateTime startDate, DateTime endDate,
            IEnumerable<IBaseDeviceModel> devices,
            IEnumerable<IConnectionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .ToList();

            return devices
                .OrderBy(d => d.DeviceNumber)
                .Select(device => (double)evInRange.Count(ev => IsDeviceMatch(ev.Device, device)))
                .ToList();
        }

        /// <summary>
        /// Action 이벤트를 Device별로 카운트
        /// - 지원: IControllerDeviceModel, ISensorDeviceModel, ICameraDeviceModel
        /// - OriginEvent의 Device 기준으로 카운트
        /// </summary>
        public static List<double> GetActionCountsByDevice(
            DateTime startDate, DateTime endDate,
            IEnumerable<IBaseDeviceModel> devices,
            IEnumerable<IActionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .ToList();

            return devices
                .OrderBy(d => d.DeviceNumber)
                .Select(device =>
                {
                    var count = evInRange.Count(ev =>
                    {
                        var originDevice = ev.OriginEvent?.Device;
                        return IsDeviceMatch(originDevice, device);
                    });
                    return (double)count;
                })
                .ToList();
        }

        /// <summary>
        /// 센서 Detection 이벤트만 Device별로 카운트 (IpCamera 제외)
        /// </summary>
        public static List<double> GetSensorDetectionCountsByDevice(
            DateTime startDate, DateTime endDate,
            IEnumerable<IBaseDeviceModel> devices,
            IEnumerable<IDetectionEventModel> allEvents)
        {
            var evInRange = allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .Where(ev => ev.Device?.DeviceType != EnumDeviceType.IpCamera)
                .ToList();

            return devices
                .OrderBy(d => d.DeviceNumber)
                .Select(device => (double)evInRange.Count(ev => IsDeviceMatch(ev.Device, device)))
                .ToList();
        }

        /// <summary>
        /// 카메라 Detection 이벤트 총 건수 (IpCamera만)
        /// </summary>
        public static int GetCameraDetectionCount(
            DateTime startDate, DateTime endDate,
            IEnumerable<IDetectionEventModel> allEvents)
        {
            return allEvents
                .Where(ev => ev.DateTime >= startDate && ev.DateTime < endDate)
                .Count(ev => ev.Device?.DeviceType == EnumDeviceType.IpCamera);
        }

        #endregion

        #region Legacy Methods (하위 호환성)

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

            //return controllers
            //    .OrderBy(c => c.DeviceNumber)
            //    .Select(c => (double)evInRange.Count(ev =>
            //        ((ISensorDeviceModel)ev.OriginEvent!.Device!).Controller!.DeviceNumber == c.DeviceNumber))
            //    .ToList();
            return controllers
                .OrderBy(c => c.DeviceNumber)
                .Select(c => (double)evInRange.Count(ev =>
                    ((ISensorDeviceModel)ev.OriginEvent!.Device!).Controller!.DeviceNumber == c.DeviceNumber))
                .ToList();
        }

        #endregion
    }
}