using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Enums;
using System.Net.NetworkInformation;
using System.Buffers;
using Autofac.Core;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 3:54:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapCustomMarker : GMapBaseMarker<ISymbolModel>, IEditableMarker
{
    #region - Ctors -
    /// <summary>
    /// 생성자
    /// </summary>
    public GMapCustomMarker(ILogService log, ISymbolModel symbolModel)
        : base(log, symbolModel)
    {
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override UIElement CreateMarkerControl()
    {
        // 기본 마커 UI 컨트롤 생성
        var markerControl = new GMapMarkerCustomControl(this);
        _log?.Info($"=====================  GMapMarkerCustomControl 생성 호출됨   ===========================");
        return markerControl;
    }

    /// <summary>
    /// 마커 초기화 (선택적 오버라이드)
    /// </summary>
    protected override void InitializeMarker()
    {
        base.InitializeMarker();

        // GMapCustomMarker 전용 초기화 로직 (필요시)
        // 예: 특별한 타이머 설정, 이벤트 구독 등
    }

    /// <summary>
    /// 상태 변경 처리 (선택적 오버라이드)
    /// </summary>
    /// <param name="status">새로운 상태</param>
    protected override void OnStatusChanged(EnumOperationState status)
    {
        base.OnStatusChanged(status);

        // GMapCustomMarker 전용 상태 변경 처리 (필요시)
        // 예: 특별한 알림, 로깅 등
    }

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - 이벤트 및 명령 -
    #endregion
    #region - Attributes -
    #endregion
}