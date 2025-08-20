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
/// - Opacity 및 기하 심볼 전용 속성 지원
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
    public new double Opacity
    {
        get { return (double)GetValue(OpacityProperty); }
        set { SetValue(OpacityProperty, value); }
    }

    public static readonly DependencyProperty OpacityProperty =
        DependencyProperty.Register("Opacity", typeof(double), typeof(GMapGeometricMarkerControl),
            new PropertyMetadata(1.0, OnOpacityChanged));

    /// <summary>
    /// 형태 변경 애니메이션 활성화 여부
    /// </summary>
    public bool EnableShapeAnimation
    {
        get { return (bool)GetValue(EnableShapeAnimationProperty); }
        set { SetValue(EnableShapeAnimationProperty, value); }
    }

    public static readonly DependencyProperty EnableShapeAnimationProperty =
        DependencyProperty.Register("EnableShapeAnimation", typeof(bool), typeof(GMapGeometricMarkerControl),
            new PropertyMetadata(true));

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
        Opacity = Marker.Opacity;

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
        SetupPropertyBinding(OpacityProperty, nameof(Marker.Opacity));
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
        //UpdateGeometricAppearance();
    }

    /// <summary>
    /// 단일 클릭 처리 오버라이드 (기하 심볼 전용 로직)
    /// </summary>
    protected override void HandleSingleClick(MouseButtonEventArgs e)
    {
        base.HandleSingleClick(e);

        // 기하 심볼 전용 클릭 처리
        OnGeometryMarkerClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 오버라이드 (형태 변경 등)
    /// </summary>
    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);

        // 더블클릭 시 형태 순환 변경 (타입 안전)
        if (Marker != null)
        {
            CycleShapeType();
        }
    }

    #endregion

    #region Geometric Control Specific Methods

    /// <summary>
    /// 기하 컨트롤 전용 초기화
    /// </summary>
    private void InitializeGeometricControl()
    {
        // 기하 심볼 기본값
        ShapeType = EnumShapeType.Circle;
        Opacity = 1.0;
        EnableShapeAnimation = true;

        OnGeometricControlInitialized();
    }

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

        // ShapeType에 따른 색상 조정 (선택사항)
        switch (ShapeType)
        {
            case EnumShapeType.Circle:
                // 원형은 기본 색상 유지
                break;
            case EnumShapeType.Square:
                // 사각형은 약간 다른 색조
                var squareBrush = MarkerFill.Clone();
                if (squareBrush is SolidColorBrush solidBrush)
                {
                    var color = solidBrush.Color;
                    color.B = Math.Min((byte)(color.B + 20), (byte)255);
                    MarkerFill = new SolidColorBrush(color);
                }
                break;
            case EnumShapeType.Triangle:
                // 삼각형은 또 다른 색조
                var triangleBrush = MarkerFill.Clone();
                if (triangleBrush is SolidColorBrush triangleSolid)
                {
                    var color = triangleSolid.Color;
                    color.R = Math.Min((byte)(color.R + 20), (byte)255);
                    MarkerFill = new SolidColorBrush(color);
                }
                break;
        }

    }


    /// <summary>
    /// 기하 심볼 클릭 처리 (가상 메서드)
    /// </summary>
    protected virtual void OnGeometryMarkerClicked(MouseButtonEventArgs e)
    {
        // 기하 심볼 전용 클릭 효과 (예: 깜빡임)
        if (EnableShapeAnimation)
        {
            TriggerClickAnimation();
        }
    }

    /// <summary>
    /// 형태 순환 변경
    /// </summary>
    protected virtual void CycleShapeType()
    {
        var currentType = ShapeType;
        var nextType = currentType switch
        {
            EnumShapeType.Circle => EnumShapeType.Square,
            EnumShapeType.Square => EnumShapeType.Triangle,
            EnumShapeType.Triangle => EnumShapeType.Circle,
            _ => EnumShapeType.Circle
        };

        // 타입 안전한 메서드 호출
        if (Marker != null)
        {
            Marker.ChangeShapeType(nextType);
        }
    }

    /// <summary>
    /// 투명도 적용
    /// </summary>
    private void ApplyOpacity()
    {
        base.Opacity = Opacity; // 기본 Control의 Opacity에 적용
    }

    /// <summary>
    /// 클릭 애니메이션 트리거
    /// </summary>
    private void TriggerClickAnimation()
    {
        if (!EnableShapeAnimation) return;

        try
        {
            TransformGroup transformGroup;
            ScaleTransform scaleTransform;
            RotateTransform existingRotate = null;

            // ✅ 기존 Transform 구조 분석
            if (RenderTransform is TransformGroup existingGroup)
            {
                // 기존 TransformGroup 사용
                transformGroup = existingGroup;
                existingRotate = transformGroup.Children.OfType<RotateTransform>().FirstOrDefault();
                scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            }
            else if (RenderTransform is RotateTransform rotateOnly)
            {
                // 기존 RotateTransform만 있는 경우
                existingRotate = rotateOnly;
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(existingRotate); // ✅ 기존 회전 보존
                scaleTransform = null;
            }
            else
            {
                // Transform이 없는 경우
                transformGroup = new TransformGroup();
                scaleTransform = null;
            }

            // ✅ ScaleTransform 추가/수정
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                transformGroup.Children.Add(scaleTransform);
            }

            // ✅ TransformGroup 적용 (기존 회전 유지됨)
            RenderTransform = transformGroup;
            RenderTransformOrigin = new Point(0.5, 0.5);

            // 애니메이션 실행
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            System.Diagnostics.Debug.WriteLine($"애니메이션 실행 - 기존 회전 보존: {existingRotate?.Angle ?? 0:F1}°");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"클릭 애니메이션 실행 실패: {ex.Message}");
        }
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
    protected static void OnOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    #region Public Methods

    /// <summary>
    /// 형태 변경 (애니메이션 포함)
    /// </summary>
    /// <param name="newShapeType">새로운 형태</param>
    /// <param name="animate">애니메이션 여부</param>
    public void ChangeShapeType(EnumShapeType newShapeType, bool animate = true)
    {
        if (ShapeType == newShapeType) return;

        if (animate && EnableShapeAnimation)
        {
            // 형태 변경 애니메이션 실행
            TriggerShapeChangeAnimation(() => ShapeType = newShapeType);
        }
        else
        {
            ShapeType = newShapeType;
        }
    }

    /// <summary>
    /// 투명도 변경 (애니메이션 포함)
    /// </summary>
    /// <param name="newOpacity">새로운 투명도</param>
    /// <param name="animate">애니메이션 여부</param>
    public void ChangeOpacity(double newOpacity, bool animate = true)
    {
        newOpacity = Math.Clamp(newOpacity, 0.0, 1.0);

        if (Math.Abs(Opacity - newOpacity) < 0.01) return;

        if (animate && EnableShapeAnimation)
        {
            // 투명도 변경 애니메이션
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = Opacity,
                To = newOpacity,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            BeginAnimation(OpacityProperty, animation);
        }
        else
        {
            Opacity = newOpacity;
        }
    }

    /// <summary>
    /// 형태 변경 애니메이션 트리거
    /// </summary>
    private void TriggerShapeChangeAnimation(Action changeAction)
    {
        try
        {
            // 페이드 아웃 → 형태 변경 → 페이드 인
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(150)
            };

            fadeOut.Completed += (s, e) =>
            {
                changeAction?.Invoke();

                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.0,
                    To = Opacity,
                    Duration = TimeSpan.FromMilliseconds(150)
                };

                BeginAnimation(OpacityProperty, fadeIn);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"형태 변경 애니메이션 실행 실패: {ex.Message}");
            // 애니메이션 실패 시 바로 변경
            changeAction?.Invoke();
        }
    }

    /// <summary>
    /// 기하 심볼 상태 초기화
    /// </summary>
    public void ResetGeometryState()
    {
        ShapeType = EnumShapeType.Circle;
        Opacity = 1.0;
        EnableShapeAnimation = true;
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    #endregion

    private bool _isUpdatingFromMarker = false;  // 순환 방지 플래그

}