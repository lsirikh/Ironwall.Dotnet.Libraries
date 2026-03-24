using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/19/2025 3:46:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapGeometricMarker : GMapBaseMarker<IGeometricSymbolModel>, IGeoEditableMarker
{
    #region - Ctors -
    /// <summary>
    /// 기하 심볼 마커 생성자
    /// </summary>
    public GMapGeometricMarker(ILogService log, IGeometricSymbolModel geometryModel)
        : base(log, geometryModel)
    {
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    /// <summary>
    /// 기하 심볼 전용 컨트롤 생성
    /// </summary>
    protected override UIElement CreateMarkerControl()
    {
        var markerControl = new GMapGeometricMarkerControl(this);
        _log?.Info($"=====================  GMapGeometricMarkerControl 생성 호출됨   ===========================");
        return markerControl;
    }


    protected override void ConfigureMarkerControl(UIElement markerControl)
    {
        base.ConfigureMarkerControl(markerControl);
    }

    protected override void UpdateShapeSize(double width, double height)
    {
        base.UpdateShapeSize(width, height);

        // GeometrySymbol 전용 크기 업데이트 로직
        //if (Shape is GMapGeometricMarkerControl geometricControl)
        //{
        //    // 추가 설정...
        //}
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -/// <summary>
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 기하학적 모양 타입
    /// </summary>
    public EnumShapeType ShapeType
    {
        get => _model.ShapeType;
        set
        {
            _model.ShapeType = value;
            OnPropertyChanged(nameof(ShapeType));
            //CreateMarkerShape(); // Shape 재생성
        }
    }

    /// <summary>
    /// 투명도
    /// </summary>
    public double Opacity
    {
        get => _model.Opacity;
        set
        {
            _model.Opacity = value;
            OnPropertyChanged(nameof(Opacity));
            //UpdateOpacity();
        }
    }
    #endregion
    #region - Attributes -
    #endregion
}