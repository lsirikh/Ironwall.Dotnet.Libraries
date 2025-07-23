using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:44:12 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ConnectionEventModel: ExEventModel, IConnectionEventModel
{
    #region - Ctors -
    public ConnectionEventModel()
    {
        
    }

    public ConnectionEventModel(IExEventModel model) : base(model) 
    { 


    }
    public ConnectionEventModel(IConnectionEventModel model): base(model)
    {
        
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    #endregion
}