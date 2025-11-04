using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      : 군사 심볼 마커 클래스                                                       
   Created By   : GHLee                                                
   Created On   : 9/8/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapMilitarySymbolMarker : GMapBaseMarker<IMilitarySymbolModel>, IMilitarySymbolEditableMarker
{
    #region - Ctors -
    /// <summary>
    /// 군사 심볼 마커 생성자
    /// </summary>
    public GMapMilitarySymbolMarker(ILogService log, IMilitarySymbolModel militaryModel)
        : base(log, militaryModel)
    {
        Width = 60;
        Height = 60;
    }
    #endregion

    #region - Implementation of Interface -
    #endregion

    #region - Overrides -
    /// <summary>
    /// 군사 심볼 전용 컨트롤 생성
    /// </summary>
    protected override UIElement CreateMarkerControl()
    {
        var markerControl = new GMapMilitarySymbolMarkerControl(this);
        _log?.Info($"GMapMilitarySymbolMarkerControl 생성: {_model.Title}");
        return markerControl;
    }

    protected override void ConfigureMarkerControl(UIElement markerControl)
    {
        base.ConfigureMarkerControl(markerControl);
    }

    protected override void UpdateShapeSize(double width, double height)
    {
        base.UpdateShapeSize(width, height);

        // 군사 심볼은 정사각형 유지가 일반적
        var size = Math.Max(width, height);
        Width = size;
        Height = size;
    }
    #endregion

    #region - Binding Methods -
    #endregion

    #region - Processes -
    #endregion

    #region - IHanldes -
    #endregion

    #region - Properties -
    /// <summary>
    /// 소속 구분
    /// </summary>
    public EnumMilitaryAffiliation Affiliation
    {
        get => _model.Affiliation;
        set
        {
            _model.Affiliation = value;
            OnPropertyChanged(nameof(Affiliation));
        }
    }

    /// <summary>
    /// 전투 차원
    /// </summary>
    public EnumMilitaryBattleDimension BattleDimension
    {
        get => _model.BattleDimension;
        set
        {
            _model.BattleDimension = value;
            OnPropertyChanged(nameof(BattleDimension));
        }
    }

    /// <summary>
    /// 표준 정체성
    /// </summary>
    public EnumMilitaryStandardIdentity StandardIdentity
    {
        get => _model.StandardIdentity;
        set
        {
            _model.StandardIdentity = value;
            OnPropertyChanged(nameof(StandardIdentity));
        }
    }

    /// <summary>
    /// 부대 종류
    /// </summary>
    public EnumMilitaryUnitType UnitType
    {
        get => _model.UnitType;
        set
        {
            _model.UnitType = value;
            OnPropertyChanged(nameof(UnitType));
        }
    }

    /// <summary>
    /// 부대 규모
    /// </summary>
    public EnumMilitaryUnitSize UnitSize
    {
        get => _model.UnitSize;
        set
        {
            _model.UnitSize = value;
            OnPropertyChanged(nameof(UnitSize));
        }
    }

    /// <summary>
    /// 부대 지시자
    /// </summary>
    public string? UnitDesignator
    {
        get => _model.UnitDesignator;
        set
        {
            _model.UnitDesignator = value;
            OnPropertyChanged(nameof(UnitDesignator));
        }
    }

    /// <summary>
    /// 상급 부대명
    /// </summary>
    public string? HigherFormation
    {
        get => _model.HigherFormation;
        set
        {
            _model.HigherFormation = value;
            OnPropertyChanged(nameof(HigherFormation));
        }
    }

    /// <summary>
    /// 콜사인
    /// </summary>
    public string? CallSign
    {
        get => _model.CallSign;
        set
        {
            _model.CallSign = value;
            OnPropertyChanged(nameof(CallSign));
        }
    }

    /// <summary>
    /// 국가 코드
    /// </summary>
    public string? CountryCode
    {
        get => _model.CountryCode;
        set
        {
            _model.CountryCode = value;
            OnPropertyChanged(nameof(CountryCode));
        }
    }
    #endregion

    #region - Attributes -
    #endregion
}