using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/19/2025 8:16:41 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 인프라/건물 심볼 전용 마커 컨트롤
    /// - GMapMarkerBaseControl<GMapInfraMarker>를 상속받아 인프라 심볼 특화 기능 제공
    /// - BuildingType, BuildingUsage, FloorCount 시각화
    /// </summary>
    public class GMapMarkerInfraControl : GMapMarkerBaseControl<GMapInfraMarker>
    {
        #region Static Constructor
        static GMapMarkerInfraControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerInfraControl),
                new FrameworkPropertyMetadata(typeof(GMapMarkerInfraControl)));
        }
        #endregion

        #region Additional Dependency Properties

        /// <summary>
        /// 건물 종류
        /// </summary>
        public EnumBuildingType BuildingType
        {
            get { return (EnumBuildingType)GetValue(BuildingTypeProperty); }
            set { SetValue(BuildingTypeProperty, value); }
        }

        public static readonly DependencyProperty BuildingTypeProperty =
            DependencyProperty.Register("BuildingType", typeof(EnumBuildingType),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(EnumBuildingType.Factory, OnBuildingTypeChanged));

        /// <summary>
        /// 건물 용도
        /// </summary>
        public EnumBuildingUsage BuildingUsage
        {
            get { return (EnumBuildingUsage)GetValue(BuildingUsageProperty); }
            set { SetValue(BuildingUsageProperty, value); }
        }

        public static readonly DependencyProperty BuildingUsageProperty =
            DependencyProperty.Register("BuildingUsage", typeof(EnumBuildingUsage),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(EnumBuildingUsage.Office, OnBuildingUsageChanged));

        /// <summary>
        /// 지상 층수
        /// </summary>
        public int FloorCount
        {
            get { return (int)GetValue(FloorCountProperty); }
            set { SetValue(FloorCountProperty, value); }
        }

        public static readonly DependencyProperty FloorCountProperty =
            DependencyProperty.Register("FloorCount", typeof(int),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(1, OnFloorCountChanged));

        /// <summary>
        /// 지하 층수
        /// </summary>
        public int BasementFloorCount
        {
            get { return (int)GetValue(BasementFloorCountProperty); }
            set { SetValue(BasementFloorCountProperty, value); }
        }

        public static readonly DependencyProperty BasementFloorCountProperty =
            DependencyProperty.Register("BasementFloorCount", typeof(int),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(0, OnBasementFloorCountChanged));

        /// <summary>
        /// 건물 면적
        /// </summary>
        public double BuildingArea
        {
            get { return (double)GetValue(BuildingAreaProperty); }
            set { SetValue(BuildingAreaProperty, value); }
        }

        public static readonly DependencyProperty BuildingAreaProperty =
            DependencyProperty.Register("BuildingArea", typeof(double),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(0.0, OnBuildingAreaChanged));

        /// <summary>
        /// 층수 표시 여부
        /// </summary>
        public bool ShowFloorCount
        {
            get { return (bool)GetValue(ShowFloorCountProperty); }
            set { SetValue(ShowFloorCountProperty, value); }
        }

        public static readonly DependencyProperty ShowFloorCountProperty =
            DependencyProperty.Register("ShowFloorCount", typeof(bool),
                typeof(GMapMarkerInfraControl),
                new PropertyMetadata(false));

        #endregion

        #region Constructors

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public GMapMarkerInfraControl()
        {
        }

        /// <summary>
        /// GMapInfraMarker와 함께 생성하는 생성자
        /// </summary>
        public GMapMarkerInfraControl(GMapInfraMarker infraMarker) : base(infraMarker)
        {
        }

        #endregion

        #region Abstract Methods Implementation

        /// <summary>
        /// GMapInfraMarker 전용 UI 업데이트 구현
        /// </summary>
        protected override void UpdateFromSpecificMarker()
        {
            if (Marker == null) return;

            BuildingType = Marker.BuildingType;
            BuildingUsage = Marker.BuildingUsage;
            FloorCount = Marker.FloorCount;
            BasementFloorCount = Marker.BasementFloorCount;
            BuildingArea = Marker.BuildingArea;

            // 2층 이상이거나 지하층이 있으면 층수 표시
            ShowFloorCount = (FloorCount > 1 || BasementFloorCount > 0);
        }

        /// <summary>
        /// GMapInfraMarker 전용 바인딩 설정 구현
        /// </summary>
        protected override void SetupSpecificBindings()
        {
            if (Marker == null) return;

            SetupPropertyBinding(BuildingTypeProperty, nameof(Marker.BuildingType));
            SetupPropertyBinding(BuildingUsageProperty, nameof(Marker.BuildingUsage));
            SetupPropertyBinding(FloorCountProperty, nameof(Marker.FloorCount));
            SetupPropertyBinding(BasementFloorCountProperty, nameof(Marker.BasementFloorCount));
            SetupPropertyBinding(BuildingAreaProperty, nameof(Marker.BuildingArea));
        }

        #endregion

        #region Override Methods

        protected override void OnControlInitialized()
        {
            base.OnControlInitialized();
            //System.Diagnostics.Debug.WriteLine("GMapMarkerInfraControl 초기화 완료");
        }

        protected override void UpdateMarkerAppearance()
        {
            base.UpdateMarkerAppearance();
            UpdateInfraAppearance();
        }

        protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
        {
            base.OnMarkerSingleClicked(e);

            // 클릭 시 층수 정보 토글
            if (FloorCount > 1 || BasementFloorCount > 0)
            {
                ShowFloorCount = !ShowFloorCount;
            }
        }

        protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
        {
            base.OnMarkerDoubleClicked(e);
        }

        protected override void OnSelectionChanged(bool isSelected)
        {
            base.OnSelectionChanged(isSelected);

            // 선택 시 층수 표시
            if (isSelected && (FloorCount > 1 || BasementFloorCount > 0))
            {
                ShowFloorCount = true;
            }
        }

        #endregion

        #region Private Methods

        private void UpdateInfraAppearance()
        {
            if (Marker == null || _isUpdatingFromMarker) return;

            // 건물 타입별 색상
            MarkerFill = BuildingType switch
            {
                EnumBuildingType.Factory => ColorHelper.ToBrush(EnumColorType.Gray),
                _ => ColorHelper.ToBrush(EnumColorType.Brown)
            };

            // 용도별 테두리
            MarkerStroke = BuildingUsage switch
            {
                EnumBuildingUsage.Office => ColorHelper.ToBrush(EnumColorType.Gold),
                _ => ColorHelper.ToBrush(EnumColorType.White)
            };

        }

        /// <summary>
        /// 층수 표시 텍스트 생성
        /// </summary>
        public string GetFloorDisplayText()
        {
            var text = "";

            if (BasementFloorCount > 0)
                text = $"B{BasementFloorCount}";

            if (FloorCount > 0)
            {
                if (!string.IsNullOrEmpty(text))
                    text += "/";
                text += $"F{FloorCount}";
            }

            return text;
        }

        #endregion

        #region Static Property Changed Callbacks

        private static void OnBuildingTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerInfraControl control)
            {
                control.UpdateInfraAppearance();

                if (control.Marker != null && control.Marker.BuildingType != (EnumBuildingType)e.NewValue)
                    control.Marker.BuildingType = (EnumBuildingType)e.NewValue;
            }
        }

        private static void OnBuildingUsageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerInfraControl control)
            {
                control.UpdateInfraAppearance();

                if (control.Marker != null && control.Marker.BuildingUsage != (EnumBuildingUsage)e.NewValue)
                    control.Marker.BuildingUsage = (EnumBuildingUsage)e.NewValue;
            }
        }

        private static void OnFloorCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerInfraControl control)
            {
                control.ShowFloorCount = control.FloorCount > 1 || control.BasementFloorCount > 0;

                if (control.Marker != null && control.Marker.FloorCount != (int)e.NewValue)
                    control.Marker.FloorCount = (int)e.NewValue;
            }
        }

        private static void OnBasementFloorCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerInfraControl control)
            {
                control.ShowFloorCount = control.FloorCount > 1 || control.BasementFloorCount > 0;

                if (control.Marker != null && control.Marker.BasementFloorCount != (int)e.NewValue)
                    control.Marker.BasementFloorCount = (int)e.NewValue;
            }
        }

        private static void OnBuildingAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerInfraControl control && control.Marker != null)
            {
                if (Math.Abs(control.Marker.BuildingArea - (double)e.NewValue) > 0.01)
                    control.Marker.BuildingArea = (double)e.NewValue;
            }
        }

        #endregion
    }
}