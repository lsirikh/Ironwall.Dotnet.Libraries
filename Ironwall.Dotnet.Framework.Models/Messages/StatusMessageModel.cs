namespace Ironwall.Dotnet.Framework.Models.Messages
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/1/2023 10:55:37 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class StatusMessageModel
    {
        public StatusMessageModel()
        {
        }

        public StatusMessageModel(string log)
        {
            Log = log;
        }

        public string Log { get; set; }
    }
}
