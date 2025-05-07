using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Communications.Accounts;
using Ironwall.Dotnet.Framework.Models.Communications.Devices;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Communications.Settings;
using Ironwall.Dotnet.Framework.Models.Communications.Symbols;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Redis.Message.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public static class RequestFactory
    {
        public static T? Build<T>(ILoginSessionModel model) where T : class, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        #region -  From Event Model to Event Request Model-
        /// <summary>
        /// Create ConnectionRequestModel Instance from Event Model
        /// </summary>
        /// <typeparam name="T">ConnectionRequestModel</typeparam>
        /// <param name="model">IConnectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IConnectionEventModel model) where T : ConnectionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create DetectionRequestModel Instance from IDetectionEventModel Model
        /// </summary>
        /// <typeparam name="T">DetectionRequestModel</typeparam>
        /// <param name="model">IDetectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IDetectionEventModel model) where T : DetectionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create MalfunctionRequestModel Instance from IMalfunctionEventModel Model
        /// </summary>
        /// <typeparam name="T">MalfunctionRequestModel</typeparam>
        /// <param name="model">IMalfunctionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IMalfunctionEventModel model) where T : MalfunctionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }


        /// <summary>
        /// Create ActionRequestModel Instance from IDetectionEventModel Model
        /// </summary>
        /// <typeparam name="T">ActionRequestModel</typeparam>
        /// <param name="content">Content for the action report</param>
        /// <param name="user">User who reuqested this action event</param>
        /// <param name="model">IDetectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(string content, string user, IDetectionEventModel model) where T : ActionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { content, user, model });
            return instance;
        }

        /// <summary>
        /// Create ActionRequestModel Instance from IMalfunctionEventModel Model
        /// </summary>
        /// <typeparam name="T">IMalfunctionEventModel</typeparam>
        /// <param name="content">Content for the action report</param>
        /// <param name="user">User who reuqested this action event</param>
        /// <param name="model">IDetectionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(string content, string user, IMalfunctionEventModel model) where T : ActionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { content, user, model });
            return instance;
        }

        /// <summary>
        /// Create ActionRequestModel Instance from IActionEventModel Model
        /// </summary>
        /// <typeparam name="T">ActionRequestModel</typeparam>
        /// <param name="model">IActionEventModel</param>
        /// <returns></returns>
        public static T Build<T>(IActionEventModel model) where T : ActionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        public static T Build<T>(string startDate, string endDate, ILoginSessionModel model) where T : class, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { startDate, endDate, model });
            return instance;
        }

        #endregion

        #region - From Event Brk To Event Request Model -
        /// <summary>
        /// Create ContactRequestModel Instance from IContactEventModel Model
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        /// <returns></returns>
        public static T Build<T>(int result) where T : DetectionDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { result });
            return instance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reason"></param>
        /// <param name="fStart"></param>
        /// <param name="fEnd"></param>
        /// <param name="SStart"></param>
        /// <param name="SEnd"></param>
        /// <returns></returns>
        public static T Build<T>(EnumFaultType reason, int fStart, int fEnd, int SStart, int SEnd) where T : MalfunctionDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { reason, fStart, fEnd, SStart, SEnd });
            return instance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="readWrite"></param>
        /// <param name="contactNumber"></param>
        /// <param name="contactSignal"></param>
        /// <returns></returns>
        public static T Build<T>(int readWrite, int contactNumber, int contactSignal) where T : ContactDetailModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { readWrite, contactNumber, contactSignal });
            return instance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <returns></returns>
        public static T Build<T>(BrkConnection model) where T : ConnectionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <returns></returns>
        public static T Build<T>(BrkDectection model) where T : DetectionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <returns></returns>
        public static T Build<T>(BrkMalfunction model) where T : MalfunctionRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }
       
        #endregion

        #region -  From Device Model to Device Request Model -

        public static T Build<T>(ILoginSessionModel model, List<CameraMappingModel> mappings) where T : CameraMappingSaveRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, mappings });
            return instance;
        }

        public static T Build<T>(ILoginSessionModel model, List<CameraDeviceModel> cameras) where T : CameraDataSaveRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, cameras });
            return instance;
        }

        public static T Build<T>(ILoginSessionModel model, List<CameraPresetModel> presets) where T : CameraPresetSaveRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, presets });
            return instance;
        }
        #endregion

        #region - From User Model to Symbol Request Model -
        public static T Build<T>(
            ILoginSessionModel model
            //, Body<MapModel> maps
            , List<PointClass> points
            , List<SymbolModel> symbols
            , List<ShapeSymbolModel> shapes
            , List<ObjectShapeModel> objects)
            where T : SymbolDataSaveRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] {
                model
                //, maps
                , points
                , symbols
                , shapes
                , objects });
            return instance;
        }

        //public static T Build<T>(ILoginSessionModel model) where T : MapFileLoadRequestModel, new()
        //{
        //    var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
        //    return instance;
        //}

        public static T Build<T>(ILoginSessionModel model, List<MapModel> maps) where T : MapFileSaveRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, maps });
            return instance;
        }
        #endregion

        #region -  From (Account)User Model to Account Request Model -
        /// <summary>
        /// Create Request Message Instance For Login
        /// </summary>
        /// <typeparam name="T">LoginRequestModel</typeparam>
        /// <param name="model">IUserModel</param>
        /// <param name="isForceLogin">bool</param>
        /// <returns></returns>
        public static T Build<T>(IUserModel model, bool isForceLogin) where T : LoginRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model, isForceLogin });
            return instance;
        }

        /// <summary>
        /// Create Request Message Instance For Login
        /// </summary>
        /// <typeparam name="T">LoginRequestModel</typeparam>
        /// <param name="userId">string</param>
        /// <param name="userPass">string</param>
        /// <param name="isForceLogin">bool</param>
        /// <returns></returns>
        public static T Build<T>(string userId, string userPass, bool isForceLogin) where T : LoginRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { userId, userPass, isForceLogin });
            return instance;
        }

        /// <summary>
        /// Create Request Message Instance For Logout
        /// </summary>
        /// <typeparam name="T">LogoutRequestModel</typeparam>
        /// <param name="cmd">int</param>
        /// <param name="userId">string</param>
        /// <param name="token">string</param>
        /// <returns></returns>
        public static T Build<T>(int cmd, string userId, string token) where T : LogoutRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { cmd, userId, token });
            return instance;
        }

        /// <summary>
        /// Create Request Model Instance For Account Process
        /// </summary>
        /// <typeparam name="T">KeepAliveRequestModel, AccountIdCheckRequest</typeparam>
        /// <param name="param">string</param>
        /// <returns></returns>
        public static T Build<T>(string param) where T : class, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { param });
            return instance;
        }

        /// <summary>
        /// Create Request Model Instance For Account Process
        /// </summary>
        /// <typeparam name="T">AccountInfoRequestModel, LogoutRequestModel, AccountAllRequestModel</typeparam>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <returns></returns>
        public static T Build<T>(string param1, string param2) where T : class, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { param1, param2 });
            return instance;
        }


        /// <summary>
        /// Create Request AccountRegisterRequestModel Instance For Register Account
        /// </summary>
        /// <typeparam name="T">AccountRegisterRequestModel</typeparam>
        /// <param name="model">IUserModel</param>
        /// <returns></returns>
        public static T Build<T>(IUserModel model) where T : AccountRegisterRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { model });
            return instance;
        }

        /// <summary>
        /// Create Request AccountRegisterRequestModel Instance For Register Account
        /// </summary>
        /// <typeparam name="T">AccountRegisterRequestModel</typeparam>
        /// <param name="id"></param>
        /// <param name="userId"></param>
        /// <param name="password"></param>
        /// <param name="name"></param>
        /// <param name="empnum"></param>
        /// <param name="birth"></param>
        /// <param name="phone"></param>
        /// <param name="address"></param>
        /// <param name="email"></param>
        /// <param name="image"></param>
        /// <param name="position"></param>
        /// <param name="department"></param>
        /// <param name="company"></param>
        /// <param name="level"></param>
        /// <param name="used"></param>
        /// <returns></returns>
        public static T Build<T>(int id,
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
            bool used) where T : AccountRegisterRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] {
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
                used
            });
            return instance;
        }

        /// <summary>
        /// Create Request AccountDeleteRequestModel Instance For Delete Account
        /// </summary>
        /// <typeparam name="T">AccountDeleteRequestModel</typeparam>
        /// <param name="userId"></param>
        /// <param name="userPass"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static T Build<T>(string userId, string userPass, string token) where T : AccountDeleteRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { userId, userPass, token });
            return instance;
        }

        /// <summary>
        /// Create Request AccountEditRequestModel Instance For Edit Account
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="userId"></param>
        /// <param name="userPass"></param>
        /// <param name="details"></param>
        /// <returns></returns>
        public static T Build<T>(string userId, string userPass, IUserModel details) where T : AccountEditRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { userId, userPass, details });
            return instance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="userId"></param>
        /// <param name="userPass"></param>
        /// <param name="token"></param>
        /// <param name="details"></param>
        /// <returns></returns>
        public static T Build<T>(string userId, string userPass, string token, List<AccountDetailModel> details) where T : AccountDeleteAllRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { userId, userPass, token, details });
            return instance;
        }
        #endregion

        #region -  From Data to Settings Request Model -
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T Build<T>(string ipAddress, int port) where T : HeartBeatRequestModel, new()
        {
            var instance = (T)Activator.CreateInstance(typeof(T), new object[] { ipAddress, port});
            return instance;
        }


        #endregion

    }
}
