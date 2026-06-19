using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /****************************************************************************
       Purpose      : Image Overlay 마커 전용 속성 편집 컨트롤 (Phase 26)
       Created By   : Claude Code
       Created On   : 2025-12-03
       PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
       Department   : SW Team
       Company      : Sensorway Co., Ltd.
    ****************************************************************************/

    /// <summary>
    /// GMapImageMarker 전용 속성 편집 컨트롤
    /// </summary>
    /// <remarks>
    /// Image Overlay 마커의 고유 속성:
    /// - 파일 경로 (FilePath)
    /// - 투명도 (ImageOpacity)
    /// - 회전 각도 (ImageRotation)
    /// - 가시성 (ImageVisibility)
    /// - 경계 좌표 정보 (Left, Top, Right, Bottom - 읽기 전용)
    /// </remarks>
    public class GMapPropertyImageControl : GMapPropertyBaseControl
    {
        #region Static Constructor

        static GMapPropertyImageControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyImageControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyImageControl)));
        }

        #endregion

        #region Additional Dependency Properties

        /// <summary>
        /// 이미지 파일 경로
        /// </summary>
        public string ImageFilePath
        {
            get => (string)GetValue(ImageFilePathProperty);
            set => SetValue(ImageFilePathProperty, value);
        }

        public static readonly DependencyProperty ImageFilePathProperty =
            DependencyProperty.Register("ImageFilePath", typeof(string),
                typeof(GMapPropertyImageControl),
                new PropertyMetadata(string.Empty, OnImageFilePathChanged));

        /// <summary>
        /// 이미지 투명도 (0.0 ~ 1.0)
        /// </summary>
        public double ImageOpacity
        {
            get => (double)GetValue(ImageOpacityProperty);
            set => SetValue(ImageOpacityProperty, value);
        }

        public static readonly DependencyProperty ImageOpacityProperty =
            DependencyProperty.Register("ImageOpacity", typeof(double),
                typeof(GMapPropertyImageControl),
                new PropertyMetadata(0.8, OnImageOpacityChanged, CoerceOpacityValue));

        /// <summary>
        /// 이미지 가시성
        /// </summary>
        public bool ImageVisibility
        {
            get => (bool)GetValue(ImageVisibilityProperty);
            set => SetValue(ImageVisibilityProperty, value);
        }

        public static readonly DependencyProperty ImageVisibilityProperty =
            DependencyProperty.Register("ImageVisibility", typeof(bool),
                typeof(GMapPropertyImageControl),
                new PropertyMetadata(true, OnImageVisibilityChanged));

        /// <summary>
        /// 가로세로비 유지 여부
        /// </summary>
        public bool MaintainAspectRatio
        {
            get => (bool)GetValue(MaintainAspectRatioProperty);
            set => SetValue(MaintainAspectRatioProperty, value);
        }

        public static readonly DependencyProperty MaintainAspectRatioProperty =
            DependencyProperty.Register("MaintainAspectRatio", typeof(bool),
                typeof(GMapPropertyImageControl),
                new PropertyMetadata(true, OnMaintainAspectRatioChanged));

        #region Read-Only Boundary Properties

        /// <summary>
        /// 좌측 경도 (읽기 전용)
        /// </summary>
        public double BoundLeft
        {
            get => (double)GetValue(BoundLeftProperty);
            private set => SetValue(BoundLeftPropertyKey, value);
        }

        private static readonly DependencyPropertyKey BoundLeftPropertyKey =
            DependencyProperty.RegisterReadOnly("BoundLeft", typeof(double),
                typeof(GMapPropertyImageControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty BoundLeftProperty =
            BoundLeftPropertyKey.DependencyProperty;

        /// <summary>
        /// 우측 경도 (읽기 전용)
        /// </summary>
        public double BoundRight
        {
            get => (double)GetValue(BoundRightProperty);
            private set => SetValue(BoundRightPropertyKey, value);
        }

        private static readonly DependencyPropertyKey BoundRightPropertyKey =
            DependencyProperty.RegisterReadOnly("BoundRight", typeof(double),
                typeof(GMapPropertyImageControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty BoundRightProperty =
            BoundRightPropertyKey.DependencyProperty;

        /// <summary>
        /// 상단 위도 (읽기 전용)
        /// </summary>
        public double BoundTop
        {
            get => (double)GetValue(BoundTopProperty);
            private set => SetValue(BoundTopPropertyKey, value);
        }

        private static readonly DependencyPropertyKey BoundTopPropertyKey =
            DependencyProperty.RegisterReadOnly("BoundTop", typeof(double),
                typeof(GMapPropertyImageControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty BoundTopProperty =
            BoundTopPropertyKey.DependencyProperty;

        /// <summary>
        /// 하단 위도 (읽기 전용)
        /// </summary>
        public double BoundBottom
        {
            get => (double)GetValue(BoundBottomProperty);
            private set => SetValue(BoundBottomPropertyKey, value);
        }

        private static readonly DependencyPropertyKey BoundBottomPropertyKey =
            DependencyProperty.RegisterReadOnly("BoundBottom", typeof(double),
                typeof(GMapPropertyImageControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty BoundBottomProperty =
            BoundBottomPropertyKey.DependencyProperty;

        /// <summary>
        /// 경계 정보 요약 문자열 (읽기 전용)
        /// </summary>
        public string BoundsInfo
        {
            get => (string)GetValue(BoundsInfoProperty);
            private set => SetValue(BoundsInfoPropertyKey, value);
        }

        private static readonly DependencyPropertyKey BoundsInfoPropertyKey =
            DependencyProperty.RegisterReadOnly("BoundsInfo", typeof(string),
                typeof(GMapPropertyImageControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BoundsInfoProperty =
            BoundsInfoPropertyKey.DependencyProperty;

        #endregion

        #endregion

        #region Constructor

        public GMapPropertyImageControl()
        {
            // 기본 클래스 초기화는 자동으로 수행됨
            HasSpecificProperties = true;
            SpecificPropertiesTitle = "이미지 속성";
            MarkerSizeEnabled = true; // 픽셀 크기 조정 활성화
        }

        #endregion

        #region Override Methods

        protected override void SetupSpecificBindings()
        {
            if (SelectedMarker is IImageEditableMarker imageMarker)
            {
                SetBinding(ImageFilePathProperty, CreateTwoWayBinding(nameof(imageMarker.FilePath)));
                SetBinding(ImageOpacityProperty, CreateTwoWayBinding(nameof(imageMarker.Opacity)));
                SetBinding(ImageVisibilityProperty, CreateTwoWayBinding(nameof(imageMarker.IsVisible)));
                SetBinding(MaintainAspectRatioProperty, CreateTwoWayBinding(nameof(imageMarker.MaintainAspectRatio)));

                //System.Diagnostics.Debug.WriteLine("GMapPropertyImageControl.SetupSpecificBindings 완료");
            }
        }

        protected override void ClearSpecificBindings()
        {
            BindingOperations.ClearBinding(this, ImageFilePathProperty);
            BindingOperations.ClearBinding(this, ImageOpacityProperty);
            BindingOperations.ClearBinding(this, ImageVisibilityProperty);
            BindingOperations.ClearBinding(this, MaintainAspectRatioProperty);

            //System.Diagnostics.Debug.WriteLine("GMapPropertyImageControl.ClearSpecificBindings 완료");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (marker is IImageEditableMarker imageMarker)
            {
                ImageFilePath = imageMarker.FilePath ?? string.Empty;
                ImageOpacity = imageMarker.Opacity;
                ImageVisibility = imageMarker.IsVisible;
                MaintainAspectRatio = imageMarker.MaintainAspectRatio;

                // 읽기 전용 경계 정보 업데이트
                UpdateBoundsInfo(imageMarker);

                //System.Diagnostics.Debug.WriteLine($"GMapPropertyImageControl 특화 속성 설정: FilePath={ImageFilePath}, Opacity={ImageOpacity}, Visibility={ImageVisibility}");
            }
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IImageEditableMarker imageMarker)
            {
                UpdateBoundsInfo(imageMarker);
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapImageMarker);
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnImageFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyImageControl control &&
                control.SelectedMarker is IImageEditableMarker imageMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                imageMarker.FilePath = (string)e.NewValue;
                control.OnMarkerPropertyChanged("FilePath", e.OldValue, e.NewValue);
            }
        }

        private static void OnImageOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyImageControl control &&
                control.SelectedMarker is IImageEditableMarker imageMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                imageMarker.Opacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("Opacity", e.OldValue, e.NewValue);
            }
        }

        private static void OnImageVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyImageControl control &&
                control.SelectedMarker is IImageEditableMarker imageMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                imageMarker.IsVisible = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("Visibility", e.OldValue, e.NewValue);
            }
        }

        private static void OnMaintainAspectRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyImageControl control &&
                control.SelectedMarker is IImageEditableMarker imageMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                imageMarker.MaintainAspectRatio = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("MaintainAspectRatio", e.OldValue, e.NewValue);
            }
        }

        private static object CoerceOpacityValue(DependencyObject d, object value)
        {
            var opacity = (double)value;
            return Math.Clamp(opacity, 0.0, 1.0);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 경계 정보 업데이트
        /// </summary>
        private void UpdateBoundsInfo(IImageEditableMarker imageMarker)
        {
            BoundLeft = imageMarker.Left;
            BoundRight = imageMarker.Right;
            BoundTop = imageMarker.Top;
            BoundBottom = imageMarker.Bottom;

            BoundsInfo = $"경도: {imageMarker.Left:F4}° ~ {imageMarker.Right:F4}°\n" +
                        $"위도: {imageMarker.Bottom:F4}° ~ {imageMarker.Top:F4}°";
        }

        #endregion
    }
}
