using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapMilitary;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/8/2025 8:43:24 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 군사 심볼 전용 마커 컨트롤
/// - GMapMarkerBaseControl<GMapMilitarySymbolMarker>를 상속받아 군사 심볼 특화 기능 제공
/// - NATO APP-6D 표준 기반 군사 심볼 렌더링
/// </summary>
public class GMapMilitarySymbolMarkerControl : GMapMarkerBaseControl<GMapMilitarySymbolMarker>
{
    #region Static Constructor
    static GMapMilitarySymbolMarkerControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMilitarySymbolMarkerControl),
            new FrameworkPropertyMetadata(typeof(GMapMilitarySymbolMarkerControl)));
    }
    #endregion

    #region Additional Dependency Properties

    /// <summary>
    /// 소속 구분
    /// </summary>
    public EnumMilitaryAffiliation Affiliation
    {
        get { return (EnumMilitaryAffiliation)GetValue(AffiliationProperty); }
        set { SetValue(AffiliationProperty, value); }
    }

    public static readonly DependencyProperty AffiliationProperty =
        DependencyProperty.Register("Affiliation", typeof(EnumMilitaryAffiliation), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(EnumMilitaryAffiliation.Friend, OnAffiliationChanged));

    /// <summary>
    /// 전투 차원
    /// </summary>
    public EnumMilitaryBattleDimension BattleDimension
    {
        get { return (EnumMilitaryBattleDimension)GetValue(BattleDimensionProperty); }
        set { SetValue(BattleDimensionProperty, value); }
    }

    public static readonly DependencyProperty BattleDimensionProperty =
        DependencyProperty.Register("BattleDimension", typeof(EnumMilitaryBattleDimension), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(EnumMilitaryBattleDimension.Land, OnBattleDimensionChanged));

    /// <summary>
    /// 표준 정체성
    /// </summary>
    public EnumMilitaryStandardIdentity StandardIdentity
    {
        get { return (EnumMilitaryStandardIdentity)GetValue(StandardIdentityProperty); }
        set { SetValue(StandardIdentityProperty, value); }
    }

    public static readonly DependencyProperty StandardIdentityProperty =
        DependencyProperty.Register("StandardIdentity", typeof(EnumMilitaryStandardIdentity), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(EnumMilitaryStandardIdentity.Present, OnStandardIdentityChanged));

    /// <summary>
    /// 부대 종류
    /// </summary>
    public EnumMilitaryUnitType UnitType
    {
        get { return (EnumMilitaryUnitType)GetValue(UnitTypeProperty); }
        set { SetValue(UnitTypeProperty, value); }
    }

    public static readonly DependencyProperty UnitTypeProperty =
        DependencyProperty.Register("UnitType", typeof(EnumMilitaryUnitType), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(EnumMilitaryUnitType.Infantry, OnUnitTypeChanged));

    /// <summary>
    /// 부대 규모
    /// </summary>
    public EnumMilitaryUnitSize UnitSize
    {
        get { return (EnumMilitaryUnitSize)GetValue(UnitSizeProperty); }
        set { SetValue(UnitSizeProperty, value); }
    }

    public static readonly DependencyProperty UnitSizeProperty =
        DependencyProperty.Register("UnitSize", typeof(EnumMilitaryUnitSize), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(EnumMilitaryUnitSize.Company, OnUnitSizeChanged));

    /// <summary>
    /// 부대 지시자
    /// </summary>
    public string UnitDesignator
    {
        get { return (string)GetValue(UnitDesignatorProperty); }
        set { SetValue(UnitDesignatorProperty, value); }
    }

    public static readonly DependencyProperty UnitDesignatorProperty =
        DependencyProperty.Register("UnitDesignator", typeof(string), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 상급 부대명
    /// </summary>
    public string HigherFormation
    {
        get { return (string)GetValue(HigherFormationProperty); }
        set { SetValue(HigherFormationProperty, value); }
    }

    public static readonly DependencyProperty HigherFormationProperty =
        DependencyProperty.Register("HigherFormation", typeof(string), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 콜사인
    /// </summary>
    public string CallSign
    {
        get { return (string)GetValue(CallSignProperty); }
        set { SetValue(CallSignProperty, value); }
    }

    public static readonly DependencyProperty CallSignProperty =
        DependencyProperty.Register("CallSign", typeof(string), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 국가 코드
    /// </summary>
    public string CountryCode
    {
        get { return (string)GetValue(CountryCodeProperty); }
        set { SetValue(CountryCodeProperty, value); }
    }

    public static readonly DependencyProperty CountryCodeProperty =
        DependencyProperty.Register("CountryCode", typeof(string), typeof(GMapMilitarySymbolMarkerControl),
            new PropertyMetadata(string.Empty));


    public bool IsPreviewMode
    {
        get { return (bool)GetValue(IsPreviewModeProperty); }
        set { SetValue(IsPreviewModeProperty, value); }
    }

    public static readonly DependencyProperty IsPreviewModeProperty =
    DependencyProperty.Register("IsPreviewMode", typeof(bool), typeof(GMapMilitarySymbolMarkerControl), 
        new PropertyMetadata(false));
    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMilitarySymbolMarkerControl()
    {
        //System.Diagnostics.Debug.WriteLine("GMapMilitarySymbolMarkerControl() 생성자 호출됨");

        // 미리보기용 기본값 설정
        Affiliation = EnumMilitaryAffiliation.Friend;
        BattleDimension = EnumMilitaryBattleDimension.Land;
        UnitType = EnumMilitaryUnitType.Artillery;
        UnitSize = EnumMilitaryUnitSize.Company;
        ShowShape = true;

        //System.Diagnostics.Debug.WriteLine($"기본값 설정 완료: {Affiliation}, {BattleDimension}, {UnitType}");

        // Loaded 이벤트에서 추가 초기화
        this.Loaded += OnControlLoaded;
    }

    /// <summary>
    /// GMapMilitarySymbolMarker 함께 생성하는 생성자
    /// </summary>
    /// <param name="militaryMarker">연결할 군사 심볼 마커</param>
    public GMapMilitarySymbolMarkerControl(GMapMilitarySymbolMarker militaryMarker) : base(militaryMarker)
    {
        IsPreviewMode = false;
        //System.Diagnostics.Debug.WriteLine("GMapMilitarySymbolMarkerControl() 생성자 호출됨");
        //System.Diagnostics.Debug.WriteLine($"기본값 설정 완료: {Affiliation}, {BattleDimension}, {UnitType}");

        // Loaded 이벤트에서 추가 초기화
        this.Loaded += OnControlLoaded;
    }

    /// <summary>
    /// 컨트롤이 완전히 로드된 후 호출
    /// </summary>
    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine("OnControlLoaded 호출됨");

        // Loaded 이벤트는 한 번만 처리
        this.Loaded -= OnControlLoaded;

        // 미리보기 모드에서 최종 업데이트
        if (IsPreviewMode)
        {
            //System.Diagnostics.Debug.WriteLine("미리보기 모드 최종 업데이트 실행");
            UpdateMilitarySymbolAppearance();
            InvalidateVisual();
        }
    }

    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// GMapMilitarySymbolMarker 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // GMapMilitarySymbolMarker 전용 속성 동기화 (타입 안전)
        Affiliation = Marker.Affiliation;
        BattleDimension = Marker.BattleDimension;
        StandardIdentity = Marker.StandardIdentity;
        UnitType = Marker.UnitType;
        UnitSize = Marker.UnitSize;
        UnitDesignator = Marker.UnitDesignator ?? string.Empty;
        HigherFormation = Marker.HigherFormation ?? string.Empty;
        CallSign = Marker.CallSign ?? string.Empty;
        CountryCode = Marker.CountryCode ?? string.Empty;

        // 군사 심볼 전용 모양 업데이트
        UpdateMilitarySymbolAppearance();
    }

    /// <summary>
    /// GMapMilitarySymbolMarker 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // GMapMilitarySymbolMarker 전용 바인딩 (타입 안전)
        SetupPropertyBinding(AffiliationProperty, nameof(Marker.Affiliation));
        SetupPropertyBinding(BattleDimensionProperty, nameof(Marker.BattleDimension));
        SetupPropertyBinding(StandardIdentityProperty, nameof(Marker.StandardIdentity));
        SetupPropertyBinding(UnitTypeProperty, nameof(Marker.UnitType));
        SetupPropertyBinding(UnitSizeProperty, nameof(Marker.UnitSize));
        SetupPropertyBinding(UnitDesignatorProperty, nameof(Marker.UnitDesignator));
        SetupPropertyBinding(HigherFormationProperty, nameof(Marker.HigherFormation));
        SetupPropertyBinding(CallSignProperty, nameof(Marker.CallSign));
        SetupPropertyBinding(CountryCodeProperty, nameof(Marker.CountryCode));
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 오버라이드
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();
        //System.Diagnostics.Debug.WriteLine("GMapMilitarySymbolMarkerControl 초기화 완료");
        // 미리보기 모드일 때 기본값으로 초기화
        if (Marker == null)
        {
            ShowShape = true;
            ShowTitle = false;
            Width = 60;
            Height = 60;
        }
    }

    /// <summary>
    /// 마커 모양 업데이트 오버라이드 (군사 심볼 고려)
    /// </summary>
    protected override void UpdateMarkerAppearance()
    {
        // 기본 색상 설정 먼저
        base.UpdateMarkerAppearance();

        // 군사 심볼 전용 모양 업데이트
        UpdateMilitarySymbolAppearance();
    }

    /// <summary>
    /// 단일 클릭 처리 오버라이드 (군사 심볼 전용 로직)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 오버라이드 (정보 표시 등)
    /// </summary>
    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);
    }

    /// <summary>
    /// 클릭으로 선택과 비선택에 따른 이벤트 콜백
    /// </summary>
    /// <param name="isSelected"></param>
    protected override void OnSelectionChanged(bool isSelected)
    {
        base.OnSelectionChanged(isSelected);
    }

    #endregion

    #region Public Methods
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        //System.Diagnostics.Debug.WriteLine("GMapMilitarySymbolMarkerControl OnApplyTemplate 호출됨");

        // 템플릿 요소들 확인
        var mainContainer = GetTemplateChild("PART_MainContainer");
        var symbolContainer = GetTemplateChild("PART_SymbolContainer");
        var groundFrame = GetTemplateChild("PART_GroundFrame");

        //System.Diagnostics.Debug.WriteLine($"템플릿 요소들: MainContainer={mainContainer != null}, SymbolContainer={symbolContainer != null}, GroundFrame={groundFrame != null}");

        // 템플릿 적용 후 지연된 업데이트 실행
        if (IsPreviewMode)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                //System.Diagnostics.Debug.WriteLine("지연된 미리보기 업데이트 실행");
                MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                UpdateMilitarySymbolAppearance();
                InvalidateVisual(); // 강제 화면 갱신
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
    #endregion

    #region Military Symbol Specific Methods

    /// <summary>
    /// 군사 심볼 컨트롤 초기화 완료 후 호출 (가상 메서드)
    /// </summary>
    protected virtual void OnMilitarySymbolControlInitialized()
    {
        // 상속 클래스에서 구현 가능
    }

    /// <summary>
    /// 군사 심볼 전용 모양 업데이트
    /// </summary>
    protected virtual void UpdateMilitarySymbolAppearance()
    {
        //System.Diagnostics.Debug.WriteLine($"UpdateMilitarySymbolAppearance 호출 - UnitType: {UnitType}, IsPreviewMode: {IsPreviewMode}");

        if (Marker == null || _isUpdatingFromMarker) return;

      
        //System.Diagnostics.Debug.WriteLine("UpdateMilitarySymbolAppearance 완료");

    }

    
    #endregion

    #region Static Property Changed Callbacks
    /// <summary>
    /// Affiliation 변경 시 호출
    /// </summary>
    protected static void OnAffiliationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine($"OnAffiliationChanged 호출: {e.OldValue} → {e.NewValue}");

        if (d is GMapMilitarySymbolMarkerControl control)
        {
            // 마커가 있을 때만 동기화
            if (control.Marker != null)
            {
                if (control.Marker.Affiliation != (EnumMilitaryAffiliation)e.NewValue)
                    control.Marker.Affiliation = (EnumMilitaryAffiliation)e.NewValue;
            }

            // 미리보기 모드든 아니든 UI 업데이트는 항상 실행
            //System.Diagnostics.Debug.WriteLine($"Affiliation UI 업데이트 실행됨");
        }
    }


    /// <summary>
    /// BattleDimension 변경 시 호출
    /// </summary>
    protected static void OnBattleDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine($"OnBattleDimensionChanged 호출: {e.OldValue} → {e.NewValue}");

        if (d is GMapMilitarySymbolMarkerControl control)
        {
            if (control.Marker != null)
            {
                if (control.Marker.BattleDimension != (EnumMilitaryBattleDimension)e.NewValue)
                    control.Marker.BattleDimension = (EnumMilitaryBattleDimension)e.NewValue;
            }
        }
    }


    /// <summary>
    /// StandardIdentity 변경 시 호출
    /// </summary>
    protected static void OnStandardIdentityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMilitarySymbolMarkerControl control && control.Marker != null)
        {
            if (control.Marker.StandardIdentity != (EnumMilitaryStandardIdentity)e.NewValue)
                control.Marker.StandardIdentity = (EnumMilitaryStandardIdentity)e.NewValue;
        }
    }


    /// <summary>
    /// UnitType 변경 시 호출
    /// </summary>
    protected static void OnUnitTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine($"OnUnitTypeChanged 호출: {e.OldValue} → {e.NewValue}");

        if (d is GMapMilitarySymbolMarkerControl control)
        {
            // 마커가 있을 때만 동기화
            if (control.Marker != null)
            {
                if (control.Marker.UnitType != (EnumMilitaryUnitType)e.NewValue)
                    control.Marker.UnitType = (EnumMilitaryUnitType)e.NewValue;
            }

            // UI 업데이트 강제 실행 (미리보기 모드에서도)
            control.UpdateMilitarySymbolAppearance();
            //System.Diagnostics.Debug.WriteLine($"UnitType UI 업데이트 실행됨: {e.NewValue}");
        }
    }

    /// <summary>
    /// UnitSize 변경 시 호출
    /// </summary>
    protected static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine($"OnUnitSizeChanged 호출: {e.OldValue} → {e.NewValue}");

        if (d is GMapMilitarySymbolMarkerControl control && control.Marker != null)
        {
            if (control.Marker.UnitSize != (EnumMilitaryUnitSize)e.NewValue)
                control.Marker.UnitSize = (EnumMilitaryUnitSize)e.NewValue;

            // UI 업데이트 강제 실행
            control.UpdateMilitarySymbolAppearance();
            //System.Diagnostics.Debug.WriteLine($"UnitSize UI 업데이트 실행됨: {e.NewValue}");
        }
    }

    #endregion

}