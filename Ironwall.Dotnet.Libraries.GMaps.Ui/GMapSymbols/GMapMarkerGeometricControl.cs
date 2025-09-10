using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// 기하학적 심볼 전용 마커 컨트롤
/// - GMapMarkerBaseControl<GMapGeometryMarker>를 상속받아 기하 심볼 특화 기능 제공
/// - ShapeType에 따른 동적 Shape 렌더링
/// - MarkerOpacity 및 기하 심볼 전용 속성 지원
/// </summary>
public class GMapGeometricMarkerControl : GMapMarkerBaseControl<GMapGeometricMarker>
{
    #region Static Constructor
    static GMapGeometricMarkerControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapGeometricMarkerControl),
            new FrameworkPropertyMetadata(typeof(GMapGeometricMarkerControl)));
    }
    #endregion

    #region Additional Dependency Properties

    /// <summary>
    /// 기하학적 모양 타입
    /// </summary>
    public EnumShapeType ShapeType
    {
        get { return (EnumShapeType)GetValue(ShapeTypeProperty); }
        set { SetValue(ShapeTypeProperty, value); }
    }

    public static readonly DependencyProperty ShapeTypeProperty =
        DependencyProperty.Register("ShapeType", typeof(EnumShapeType), typeof(GMapGeometricMarkerControl),
            new PropertyMetadata(EnumShapeType.Circle, OnShapeTypeChanged));

    /// <summary>
    /// 투명도 (0.0 ~ 1.0) - Control.Opacity를 오버라이드
    /// </summary>
    public double MarkerOpacity
    {
        get { return (double)GetValue(MarkerOpacityProperty); }
        set { SetValue(MarkerOpacityProperty, value); }
    }

    public static readonly DependencyProperty MarkerOpacityProperty =
        DependencyProperty.Register("MarkerOpacity", typeof(double), typeof(GMapGeometricMarkerControl),
            new PropertyMetadata(1.0, OnMarkerOpacityChanged));

    

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapGeometricMarkerControl()
    {
        // 기본 클래스에서 InitializeControl() 호출됨
    }

    /// <summary>
    /// GMapGeometricMarker 함께 생성하는 생성자
    /// </summary>
    /// <param name="GMapGeometricMarker">연결할 Geometry 마커</param>
    public GMapGeometricMarkerControl(GMapGeometricMarker geometryMarker) : base(geometryMarker)
    {
        // 기본 클래스에서 UpdateFromMarker(), SetupDataBindings() 호출됨
    }


    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// GMapGeometryMarker 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // GMapGeometryMarker 전용 속성 동기화 (타입 안전)
        ShapeType = Marker.ShapeType;
        MarkerOpacity = Marker.Opacity;

        // 기하 심볼 전용 모양 업데이트
        //UpdateGeometricAppearance();
    }

    /// <summary>
    /// GMapGeometryMarker 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // GMapGeometryMarker 전용 바인딩 (타입 안전)
        SetupPropertyBinding(ShapeTypeProperty, nameof(Marker.ShapeType));
        SetupPropertyBinding(MarkerOpacityProperty, nameof(Marker.Opacity));
    }

    #endregion

    #region Override Methods
    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 오버라이드
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();
        System.Diagnostics.Debug.WriteLine("GMapGeometricMarkerControl 초기화 완료");
    }

    /// <summary>
    /// 마커 모양 업데이트 오버라이드 (ShapeType 고려)
    /// </summary>
    protected override void UpdateMarkerAppearance()
    {
        // 기본 색상 설정 먼저
        base.UpdateMarkerAppearance();

        // 기하 심볼 전용 모양 업데이트
        UpdateGeometricAppearance();
    }

    /// <summary>
    /// 단일 클릭 처리 오버라이드 (기하 심볼 전용 로직)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 오버라이드 (형태 변경 등)
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

    #region Geometric Control Specific Methods


    /// <summary>
    /// 기하 컨트롤 초기화 완료 후 호출 (가상 메서드)
    /// </summary>
    protected virtual void OnGeometricControlInitialized()
    {
        // 상속 클래스에서 구현 가능
    }

    /// <summary>
    /// 기하 심볼 전용 모양 업데이트
    /// </summary>
    protected virtual void UpdateGeometricAppearance()
    {
        if (Marker == null || _isUpdatingFromMarker) return;

        //// ShapeType에 따른 색상 조정 (선택사항)
        //switch (ShapeType)
        //{
        //    case EnumShapeType.Circle:
        //        // 원형은 기본 색상 유지
        //        break;
        //    case EnumShapeType.Square:
        //        // 사각형은 약간 다른 색조
        //        var squareBrush = MarkerFill.Clone();
        //        if (squareBrush is SolidColorBrush solidBrush)
        //        {
        //            var color = solidBrush.Color;
        //            color.B = Math.Min((byte)(color.B + 20), (byte)255);
        //            MarkerFill = new SolidColorBrush(color);
        //        }
        //        break;
        //    case EnumShapeType.Triangle:
        //        // 삼각형은 또 다른 색조
        //        var triangleBrush = MarkerFill.Clone();
        //        if (triangleBrush is SolidColorBrush triangleSolid)
        //        {
        //            var color = triangleSolid.Color;
        //            color.R = Math.Min((byte)(color.R + 20), (byte)255);
        //            MarkerFill = new SolidColorBrush(color);
        //        }
        //        break;
        //}

    }
  
    /// <summary>
    /// 투명도 적용
    /// </summary>
    private void ApplyOpacity()
    {
        base.Opacity = MarkerOpacity; // 기본 Control의 Opacity에 적용
    }

    #endregion

    #region Static Property Changed Callbacks

    /// <summary>
    /// ShapeType 변경 시 호출
    /// </summary>
    protected static void OnShapeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapGeometricMarkerControl control && control.Marker != null)
        {
            // UI 업데이트
            //control.UpdateGeometricAppearance();

            // 마커 데이터와 동기화 (타입 안전)
            if(control.Marker.ShapeType != (EnumShapeType)e.NewValue)
                control.Marker.ShapeType = (EnumShapeType)e.NewValue;
        }
    }

    /// <summary>
    /// 투명도 변경 시 호출
    /// </summary>
    protected static void OnMarkerOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapGeometricMarkerControl control && control.Marker != null)
        {
            // UI 투명도 적용
            //control.ApplyOpacity();

            // 마커 데이터와 동기화 (타입 안전)
            if(control.Marker.Opacity != (double)e.NewValue) 
                control.Marker.Opacity = (double)e.NewValue;
        }
    }

    #endregion

    //#region Public Methods

    ///// <summary>
    ///// 기하 심볼 상태 초기화
    ///// </summary>
    //public void ResetGeometryState()
    //{
    //    ShapeType = EnumShapeType.Circle;
    //    MarkerOpacity = 1.0;
    //    EnableShapeAnimation = true;
    //}

    //#endregion

    private bool _isUpdatingFromMarker = false;  // 순환 방지 플래그

}