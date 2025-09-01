using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// GMapCustomMarker 전용 마커 컨트롤 (기존 호환성 유지)
/// - GMapMarkerBaseControl<T>를 상속받아 타입 안전성 확보
/// - 기존 코드와 완전 호환
/// </summary>
public class GMapMarkerCustomControl : GMapMarkerBaseControl<GMapCustomMarker>
{
    #region Static Constructor
    static GMapMarkerCustomControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerCustomControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerCustomControl)));
    }
    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerCustomControl()
    {
        // 기본 클래스에서 InitializeControl() 호출됨
    }

    /// <summary>
    /// 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="marker">연결할 마커</param>
    public GMapMarkerCustomControl(GMapCustomMarker marker) : base(marker)
    {
        // 기본 클래스에서 UpdateFromMarker(), SetupDataBindings() 호출됨
    }

    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// GMapCustomMarker 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        // GMapCustomMarker는 기본 속성만 있으므로 추가 처리 없음
        // 모든 속성은 이미 기본 클래스에서 처리됨

        // 필요시 GMapCustomMarker 전용 로직 추가
        if (Marker == null) return;

        // 예: 특별한 상태 처리
        // if (Marker.SomeSpecialProperty != null) { ... }
    }

    /// <summary>
    /// GMapCustomMarker 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        // GMapCustomMarker는 기본 바인딩만 있으므로 추가 바인딩 없음
        // 모든 바인딩은 이미 기본 클래스에서 처리됨

        // 필요시 GMapCustomMarker 전용 바인딩 추가
        if (Marker == null) return;

        // 예: 특별한 속성 바인딩
        // SetupPropertyBinding(SomeProperty, nameof(Marker.SomeProperty));
    }

    #endregion

    #region Override Methods (기존 동작 유지)

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 (필요시 오버라이드)
    /// </summary>
    protected override void OnControlInitialized()
    {
        // GMapCustomMarker 전용 초기화 로직
        base.OnControlInitialized();
        System.Diagnostics.Debug.WriteLine("GMapMarkerCustomControl 초기화 완료");
    }

    /// <summary>
    /// 단일 클릭 처리 (기존 동작 유지)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);

        // 기하 심볼 전용 클릭 효과 (예: 깜빡임)
        if (EnableShapeAnimation)
        {
            TriggerClickAnimation();
        }
    }

    /// <summary>
    /// 더블클릭 처리 (기존 동작 유지)
    /// </summary>
    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);
    }



    #endregion
    #region Public Methods
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

            // 기존 Transform 구조 분석
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
                transformGroup.Children.Add(existingRotate); // 기존 회전 보존
                scaleTransform = null;
            }
            else
            {
                // Transform이 없는 경우
                transformGroup = new TransformGroup();
                scaleTransform = null;
            }

            // ScaleTransform 추가/수정
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                transformGroup.Children.Add(scaleTransform);
            }

            // TransformGroup 적용 (기존 회전 유지됨)
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
}