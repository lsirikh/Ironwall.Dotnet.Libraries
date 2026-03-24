using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 3:35:15 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    #region Event Args

    /// <summary>
    /// 군사 심볼 등록 이벤트 인자
    /// </summary>
    public class MilitarySymbolRegisterEventArgs : EventArgs
    {
        public MilitarySymbolModel MilitarySymbolModel { get; }

        public MilitarySymbolRegisterEventArgs(MilitarySymbolModel militarySymbolModel)
        {
            MilitarySymbolModel = militarySymbolModel;
        }
    }

    #endregion
}