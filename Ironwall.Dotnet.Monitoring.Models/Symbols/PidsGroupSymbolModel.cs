using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/23/2025 9:48:13 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class PidsGroupSymbolModel : LineSymbolModel, IPidsGroupSymbolModel
{
    #region - Ctors -
    public PidsGroupSymbolModel()
    {
        Width = 60;
        Height = 60;
        ShowShape = true;
        ShowTitle = false;
        StrokeColor = EnumColorType.Lime;
        FillColor = EnumColorType.Transparent;
        Category = EnumMarkerCategory.AREA_BOUNDARY;
        OperationState = EnumOperationState.NONE;
        LinePattern = EnumLinePattern.Solid;
        TitleSize = 10;
        Title = "New Group";
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
    [JsonProperty("device_group", Order = 20)]
    public int LinkedDeviceGroup { get; set; }

    [JsonProperty("event_status", Order = 21)]
    public EnumEventStatus EventStatus { get; set; } = EnumEventStatus.Normal;

    public event EventHandler Update;

    public void SetUpdate()
    {
        Update?.Invoke(this, EventArgs.Empty);
    }
    #endregion
    #region - Attributes -
    #endregion
}