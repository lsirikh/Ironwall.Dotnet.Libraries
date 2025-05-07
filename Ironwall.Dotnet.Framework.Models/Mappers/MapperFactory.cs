using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Ironwall.Dotnet.Framework.Models.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public static class MapperFactory
    {
        #region - Static Base Procedures -
        public static T Build<T>() where T : class, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T));
            return instance;
        }
        #endregion

        #region - From EventModel To Mapper -
        /// <summary>
        /// Create ConnectionEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">ConnectionEventMapper</typeparam>
        /// <param name="model">IConnectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IConnectionEventModel model) where T : ConnectionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create DetectionEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">DetectionEventMapper</typeparam>
        /// <param name="model">IDetectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IDetectionEventModel model) where T : DetectionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create MalfunctionEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">MalfunctionEventMapper</typeparam>
        /// <param name="model">IMalfunctionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IMalfunctionEventModel model) where T : MalfunctionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create ContactEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">ContactEventMapper</typeparam>
        /// <param name="model">IContactEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IContactEventModel model) where T : ContactEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create ActionEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">ActionEventMapper</typeparam>
        /// <param name="model">IActionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IActionEventModel model) where T : ActionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create ModeWindyEventMapper Instance From Event Model
        /// </summary>
        /// <typeparam name="T">ModeWindyEventMapper</typeparam>
        /// <param name="model">IModeWindyEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IModeWindyEventModel model) where T : ModeWindyEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
        #endregion

        #region - From IRequestModel To EventMapper -
        public static T Build<T>(IDeviceDetailModel model) where T : DeviceInfoTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create ConnectionEventMapper Instance From Request Model
        /// </summary>
        /// <typeparam name="T">ConnectionEventMapper</typeparam>
        /// <param name="model">IConnectionRequestModel</param>
        /// <param name="deviceModel">IBaseDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(IConnectionRequestModel model, IBaseDeviceModel deviceModel) where T : ConnectionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, deviceModel });
            return instance;
        }

        /// <summary>
        /// Create DetectionEventMapper Instance From Request Model
        /// </summary>
        /// <typeparam name="T">DetectionEventMapper</typeparam>
        /// <param name="model">IDetectionRequestModel</param>
        /// <param name="deviceModel">IBaseDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(IDetectionRequestModel model, IBaseDeviceModel deviceModel) where T : DetectionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, deviceModel });
            return instance;
        }

        /// <summary>
        /// Create MalfunctionEventMapper Instance From Request Model
        /// </summary>
        /// <typeparam name="T">MalfunctionEventMapper</typeparam>
        /// <param name="model">IMalfunctionRequestModel</param>
        /// <param name="deviceModel">IBaseDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(IMalfunctionRequestModel model, IBaseDeviceModel deviceModel) where T : MalfunctionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, deviceModel });
            return instance;
        }

        /// <summary>
        /// Create ActionEventMapper Instance From Request Model
        /// </summary>
        /// <typeparam name="T">ActionEventMapper</typeparam>
        /// <param name="model">IActionRequestModel</param>
        /// <param name="eventModel">IMetaEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IActionRequestModel model, IMetaEventModel eventModel) where T : ActionEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, eventModel });
            return instance;
        }

        /// <summary>
        /// Create ModeWindyEventMapper Instance From Request Model
        /// </summary>
        /// <typeparam name="T">ModeWindyEventMapper</typeparam>
        /// <param name="model">IModeWindyRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(IModeWindyRequestModel model) where T : ModeWindyEventMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        #endregion

        #region - From DeviveModel To DeviceMapper -
        /// <summary>
        /// Create ControllerTableMapper Instance From Device Model
        /// </summary>
        /// <typeparam name="T">ControllerTableMapper</typeparam>
        /// <param name="model">IControllerDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(IControllerDeviceModel model) where T : ControllerTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create SensorTableMapper Instance From Device Model
        /// </summary>
        /// <typeparam name="T">SensorTableMapper</typeparam>
        /// <param name="model">ISensorDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(ISensorDeviceModel model) where T : SensorTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create CameraTableMapper Instance From Device Model
        /// </summary>
        /// <typeparam name="T">CameraTableMapper</typeparam>
        /// <param name="model">ICameraDeviceModel</param>
        /// <returns></returns>
        public static T Build<T>(ICameraDeviceModel model) where T : CameraTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        public static T Build<T>(ICameraPresetModel model) where T : PresetTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        public static T Build<T>(ICameraProfileModel model) where T : ProfileTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        public static T Build<T>(ICameraMappingModel model) where T : MappingTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
        #endregion

        #region - From SymbolModel To SymbolMapper -
        public static T Build<T>(ISymbolDetailModel model) where T : SymbolInfoTableMapper, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
        #endregion 
    }
}
