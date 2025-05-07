using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Communications.Accounts;
using Ironwall.Dotnet.Framework.Models.Communications.Devices;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Communications.Settings;
using Ironwall.Dotnet.Framework.Models.Communications.Symbols;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public static class ResponseFactory
    {
        #region - From Base Response Param to Base Response Model  -
        /// <summary>
        /// Create ResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg) where T : new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg });
            return instance;
        }

        #endregion

        #region -  From Request Model to Event Response Model-
        /// <summary>
        /// Create Event ConnectionResponseModel Instance From EventRequest
        /// </summary>
        /// <typeparam name="T">ConnectionResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="model">IConnectionRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IConnectionRequestModel model = null) where T : ConnectionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, model });
            return instance;
        }

        /// <summary>
        /// Create Event DetectionResponseModel Instance From EventRequest
        /// </summary>
        /// <typeparam name="T">DetectionResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="model">IDetectionRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IDetectionRequestModel model = null) where T : DetectionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, model });
            return instance;
        }

        /// <summary>
        /// Create Event MalfunctionResponseModel Instance From EventRequest
        /// </summary>
        /// <typeparam name="T">MalfunctionResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="model">IMalfunctionRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IMalfunctionRequestModel model = null) where T : MalfunctionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, model });
            return instance;
        }

        

        /// <summary>
        /// Create Event ActionResponseModel Instance From EventRequest
        /// </summary>
        /// <typeparam name="T">ActionResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="model">IActionRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IActionRequestModel model = null) where T : ActionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, model });
            return instance;
        }

        /// <summary>
        /// Create Event ModeWindyResponseModel Instance From EventRequest
        /// </summary>
        /// <typeparam name="T">ModeWindyResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="model">IModeWindyRequestModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IModeWindyRequestModel model) where T : ModeWindyResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, model });
            return instance;
        }


        public static T Build<T>(bool success, string msg, List<DetectionRequestModel> events) where T : SearchDetectionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, events });
            return instance;
        }

        public static T Build<T>(bool success, string msg, List<MalfunctionRequestModel> events) where T : SearchMalfunctionResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, events });
            return instance;
        }

        //public static T Build<T>(bool success, string msg
        //     , List<DetectionRequestModel> detectionEvents
        //    , List<MalfunctionRequestModel> malfunctionEvents
        //    , List<ActionRequestModel> actionEvents) where T : SearchActionResponseModel, new()
        //{
        //    var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, detectionEvents, malfunctionEvents, actionEvents });
        //    return instance;
        //}
        #endregion

        #region -  From Device Data to Device Response Model-
        /// <summary>
        /// Create DeviceDetailResponseBuild Instance From Data
        /// </summary>
        /// <typeparam name="T">DeviceDetailResponse</typeparam>
        /// <param name="controller">int</param>
        /// <param name="sensor">int</param>
        /// <param name="camera">int</param>
        /// <param name="updateTime">Datetime</param>
        /// <returns></returns>
        public static T Build<T>(
            int controller
            , int sensor
            , int camera
            , DateTime updateTime) where T : DeviceDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { controller, sensor, camera, updateTime});
            return instance;
        }

        /// <summary>
        /// Create DeviceDetailResponse Instance From DeviceDetailResponse
        /// </summary>
        /// <typeparam name="T">DeviceDetailResponse</typeparam>
        /// <param name="model">IDeviceDetailResponse</param>
        /// <returns></returns>
        public static T Build<T>(IDeviceDetailModel model) where T : DeviceDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

       

        /// <summary>
        /// Create DeviceDataResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">DeviceDataResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="content">string</param>
        /// <param name="controllers">Body<ControllerDeviceModel></param>
        /// <param name="sensors">Body<SensorDeviceModel></param>
        /// <param name="cameras">Body<CameraDeviceModel></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string content
            , List<ControllerDeviceModel> controllers
            , List<SensorDeviceModel> sensors
            , List<CameraDeviceModel> cameras) where T : DeviceDataResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, controllers, sensors, cameras });
            return instance;
        }

        /// <summary>
        /// Create ControllerDataResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">ControllerDataResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="content">string</param>
        /// <param name="devices">Body<ControllerDeviceModel></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string content
            , List<ControllerDeviceModel> devices) where T : ControllerDataResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, devices });
            return instance;
        }

        /// <summary>
        /// Create SensorDataResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">SensorDataResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="content">string</param>
        /// <param name="devices">Body<SensorDeviceModel></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string content
            , List<SensorDeviceModel> devices) where T : SensorDataResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, devices });
            return instance;
        }

        public static T Build<T>(bool success, string content
            , List<CameraPresetModel> list) where T : CameraPresetResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, list });
            return instance;
        }

        /// <summary>
        /// Create CameraDataResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">CameraDataResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="content">string</param>
        /// <param name="devices">Body<CameraDeviceModel></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string content
            , List<CameraDeviceModel> devices) where T : CameraDataResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, devices });
            return instance;
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="success"></param>
        /// <param name="content"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string content
            , List<CameraMappingModel> list) where T : CameraMappingResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, list });
            return instance;
        }

        #endregion

        #region - From Request Model to Symbol Response Model -
        public static T Build<T>(ISymbolDetailModel model) where T : SymbolDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        

        public static T Build<T>(
            int map
            , int symbol
            , int shapeSymbol
            , int objectShape
            , DateTime updateTime) where T : SymbolDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { map, symbol, shapeSymbol, objectShape, updateTime });
            return instance;
        }

        public static T Build<T>(ISymbolMoreDetailModel model) where T : SymbolMoreDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        public static T Build<T>(
            int map
            , int symbol
            , int point
            , int shapeSymbol
            , int objectShape
            , DateTime updateTime) where T : SymbolMoreDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { map, point, symbol, shapeSymbol, objectShape, updateTime });
            return instance;
        }

        public static T Build<T>(bool success, string content, ISymbolDetailModel detail) where T : SymbolInfoResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, detail });
            return instance;
        }

        public static T Build<T>(
            bool success
            , string content
            , List<MapModel> maps
            , List<PointClass> points
            , List<SymbolModel> symbols
            , List<ShapeSymbolModel> shapes
            , List<ObjectShapeModel> objects
            ) where T : SymbolDataLoadResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, maps, points, symbols, shapes, objects });
            return instance;
        }

        public static T Build<T>(bool success, string content, ISymbolMoreDetailModel detail) where T : SymbolDataSaveResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, detail });
            return instance;
        }

        public static T Build<T>(bool success, string content, List<MapModel> maps) where T : MapFileLoadResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, maps });
            return instance;
        }

        public static T Build<T>(bool success, string content, MapDetailModel detail) where T : MapFileSaveResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, content, detail });
            return instance;
        }

       
        #endregion

        #region -  From Request Model to Account Response Model-
        /// <summary>
        /// Create AccountDetailModel Instance From IUserModel
        /// </summary>
        /// <typeparam name="T">AccountDetailModel</typeparam>
        /// <param name="model">IUserModel</param>
        /// <returns></returns>
        public static T Build<T>(IUserModel model) where T : AccountDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] 
            {
                model.Id,
                model.IdUser,
                model.Password,
                model.Name,
                model.EmployeeNumber,
                model.Birth,
                model.Phone,
                model.Address,
                model.EMail,
                model.Image,
                model.Position,
                model.Department,
                model.Company,
                model.Level,
                model.Used
        });
            return instance;
        }

        /// <summary>
        /// Create AccountDetailModel Instance From IUserModel
        /// </summary>
        /// <typeparam name="T">AccountDetailModel</typeparam>
        /// <param name="id">Account Id</param>
        /// <param name="userId">Account MappingGroup</param>
        /// <param name="password">Account Password</param>
        /// <param name="name">Account User MappingGroup</param>
        /// <param name="empnum">Account Employee Number</param>
        /// <param name="birth">Account User Birthday</param>
        /// <param name="phone">Account User Phone Number</param>
        /// <param name="address">Account User Address</param>
        /// <param name="email">Account User Email</param>
        /// <param name="image">Account User Profile Image</param>
        /// <param name="position">Account User Position</param>
        /// <param name="department">Account User Department</param>
        /// <param name="company">Account User Company</param>
        /// <param name="level">Account Level</param>
        /// <param name="used">Account Status</param>
        /// <returns></returns>
        public static T Build<T>(
            int id,
            string userId,
            string password,
            string name,
            string empnum,
            string birth,
            string phone,
            string address,
            string email,
            string image,
            string position,
            string department,
            string company,
            int level,
            bool used) where T : AccountDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] 
            {
                id,
                userId,
                password,
                name,
                empnum,
                birth,
                phone,
                address,
                email,
                image,
                position,
                department,
                company,
                level,
                used,
            });
            return instance;
        }

        /// <summary>
        /// Create AccountDetailModel Instance From IUserModel and Data related with Session
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="userId">Account User Id</param>
        /// <param name="token">A Token designated for this Account</param>
        /// <param name="clientId">Client Number</param>
        /// <param name="userLevel">Account Level</param>
        /// <param name="sessionTimeOut">Account Session Datetime Span</param>
        /// <param name="details">IUserModel</param>
        /// <param name="createdTime">Session Created Datetime</param>
        /// <param name="expiredTime">Expected Session Expiration Datetime</param>
        /// <returns></returns>
        public static T Build<T>(
            string userId,
            string token,
            int clientId,
            int userLevel,
            int sessionTimeOut,
            IUserModel details,
            string createdTime,
            string expiredTime) where T : LoginResultModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[]
            {
                userId,
                token,
                clientId,
                userLevel,
                sessionTimeOut,
                details,
                createdTime,
                expiredTime
        });
            return instance;
        }

        /// <summary>
        /// Create LoginResponseModel Instance From LoginResultModel
        /// </summary>
        /// <typeparam name="T">LoginResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="result">LoginResultModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, 
            LoginResultModel result) where T : LoginResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, result });
            return instance;
        }

        

        /// <summary>
        /// Create KeepAliveResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">KeepAliveResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="expiredTime">string for Expiration Datetime</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, string expiredTime) where T : KeepAliveResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, expiredTime });
            return instance;
        }

        /// <summary>
        /// Create AccountRegisterResponseModel, AccountEditResponseModel ,or AccountInfoResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T">AccountRegisterResponseModel</typeparam>
        /// <param name="success">bool</param>
        /// <param name="msg">string</param>
        /// <param name="details">IUserModel</param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, IUserModel details) where T : new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, details });
            return instance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="success"></param>
        /// <param name="msg"></param>
        /// <param name="details"></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg
            , List<AccountDetailModel> details) where T : AccountAllResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, details });
            return instance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="success"></param>
        /// <param name="msg"></param>
        /// <param name="deletedAccounts"></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, List<string> deletedAccounts) where T : AccountDeleteAllResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, deletedAccounts });
            return instance;
        }

        /// <summary>
        /// Create HeartBeatResponseModel Instance From Data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="success"></param>
        /// <param name="msg"></param>
        /// <param name="currentTime"></param>
        /// <param name="expiredTime"></param>
        /// <returns></returns>
        public static T Build<T>(bool success, string msg, string currentTime, string expiredTime) where T : HeartBeatResponseModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { success, msg, currentTime, expiredTime });
            return instance;
        }
        #endregion



    }
}
