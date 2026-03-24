using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/10/2025 10:50:02 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraEventModel : BaseModel, ICameraEventModel
{
    #region - Ctors -
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
    
    public string EventName { get; set; } = string.Empty;
    // <summary>
    /// 대상 카메라 GUID 목록 (DB 조회용) 주 방식
    /// </summary>
    public List<string> CameraGuids { get; set; } = new List<string>();
    /// <summary>
    /// 상세 설명
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// 이벤트 활성화 여부 (true: 활성, false: 비활성)
    /// </summary>
    public bool IsEnable { get; set; } = true;
   
    #endregion
    #region - Attributes -
    #endregion
}