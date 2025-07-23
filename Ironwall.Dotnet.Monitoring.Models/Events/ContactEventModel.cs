using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 7:37:12 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ContactEventModel : ExEventModel, IContactEventModel
{
    #region - Ctors -
    public ContactEventModel()
    {
    }

    public ContactEventModel(IExEventModel model):base(model)
    {
        
    }

    public ContactEventModel(IContactEventModel model, IBaseDeviceModel device) : base(model, device)
    {
        ReadWrite = model.ReadWrite;
        ContactNumber = model.ContactNumber;
        ContactSignal = model.ContactSignal;
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
    [JsonProperty("read_write", Order = 6)]
    public int ReadWrite { get; set; }
    [JsonProperty("contact_number", Order = 7)]
    public int ContactNumber { get; set; }
    [JsonProperty("contact_signal", Order = 8)]
    public int ContactSignal { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}