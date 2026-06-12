using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows;
using System.Windows.Data;


namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/19/2025 9:22:16 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyInfraControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyInfraControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyInfraControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyInfraControl)));
        }

        public GMapPropertyInfraControl()
        {
            // 건물 타입 목록 초기화
            AvailableBuildingTypes = new[]
            {
                EnumBuildingType.Factory
            };

            // 건물 용도 목록 초기화
            AvailableBuildingUsages = new[]
            {
                EnumBuildingUsage.Office
            };
        }
        #endregion

        #region - Dependency Properties -

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
                typeof(GMapPropertyInfraControl),
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
                typeof(GMapPropertyInfraControl),
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
                typeof(GMapPropertyInfraControl),
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
                typeof(GMapPropertyInfraControl),
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
                typeof(GMapPropertyInfraControl),
                new PropertyMetadata(100.0, OnBuildingAreaChanged, CoerceDoubleValue));

        /// <summary>
        /// 사용 가능한 건물 타입 목록
        /// </summary>
        public EnumBuildingType[] AvailableBuildingTypes
        {
            get { return (EnumBuildingType[])GetValue(AvailableBuildingTypesProperty); }
            set { SetValue(AvailableBuildingTypesProperty, value); }
        }

        public static readonly DependencyProperty AvailableBuildingTypesProperty =
            DependencyProperty.Register("AvailableBuildingTypes", typeof(EnumBuildingType[]),
                typeof(GMapPropertyInfraControl));

        /// <summary>
        /// 사용 가능한 건물 용도 목록
        /// </summary>
        public EnumBuildingUsage[] AvailableBuildingUsages
        {
            get { return (EnumBuildingUsage[])GetValue(AvailableBuildingUsagesProperty); }
            set { SetValue(AvailableBuildingUsagesProperty, value); }
        }

        public static readonly DependencyProperty AvailableBuildingUsagesProperty =
            DependencyProperty.Register("AvailableBuildingUsages", typeof(EnumBuildingUsage[]),
                typeof(GMapPropertyInfraControl));

        #endregion

        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== Infra ClearSpecificBindings 시작 ===");
            BindingOperations.ClearBinding(this, BuildingTypeProperty);
            BindingOperations.ClearBinding(this, BuildingUsageProperty);
            BindingOperations.ClearBinding(this, FloorCountProperty);
            BindingOperations.ClearBinding(this, BasementFloorCountProperty);
            BindingOperations.ClearBinding(this, BuildingAreaProperty);
            //System.Diagnostics.Debug.WriteLine("=== Infra ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== Infra SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IInfraEditableMarker infraMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {infraMarker.GetType().Name}");

                SetBinding(BuildingTypeProperty, CreateTwoWayBinding(nameof(infraMarker.BuildingType)));
                SetBinding(BuildingUsageProperty, CreateTwoWayBinding(nameof(infraMarker.BuildingUsage)));
                SetBinding(FloorCountProperty, CreateTwoWayBinding(nameof(infraMarker.FloorCount)));
                SetBinding(BasementFloorCountProperty, CreateTwoWayBinding(nameof(infraMarker.BasementFloorCount)));
                SetBinding(BuildingAreaProperty, CreateTwoWayBinding(nameof(infraMarker.BuildingArea)));
            }

            //System.Diagnostics.Debug.WriteLine("=== Infra SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IInfraEditableMarker infraMarker)) return;

            this.BuildingType = infraMarker.BuildingType;
            this.BuildingUsage = infraMarker.BuildingUsage;
            this.FloorCount = infraMarker.FloorCount;
            this.BasementFloorCount = infraMarker.BasementFloorCount;
            this.BuildingArea = infraMarker.BuildingArea;

            System.Diagnostics.Debug.WriteLine($"인프라 마커 속성 로드:");
            System.Diagnostics.Debug.WriteLine($"  BuildingType: {infraMarker.BuildingType}");
            System.Diagnostics.Debug.WriteLine($"  FloorCount: B{infraMarker.BasementFloorCount}/F{infraMarker.FloorCount}");
            System.Diagnostics.Debug.WriteLine($"  BuildingArea: {infraMarker.BuildingArea}㎡");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IInfraEditableMarker infraMarker)
            {
                infraMarker.BuildingType = this.BuildingType;
                infraMarker.BuildingUsage = this.BuildingUsage;
                infraMarker.FloorCount = this.FloorCount;
                infraMarker.BasementFloorCount = this.BasementFloorCount;
                infraMarker.BuildingArea = this.BuildingArea;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapInfraMarker);
        }

        #endregion

        #region - Property Changed Callbacks -

        private static void OnBuildingTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    infraMarker.BuildingType = (EnumBuildingType)e.NewValue;
                    control.OnMarkerPropertyChanged("BuildingType", e.OldValue, e.NewValue);
                }
            }
        }

        private static void OnBuildingUsageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    infraMarker.BuildingUsage = (EnumBuildingUsage)e.NewValue;
                    control.OnMarkerPropertyChanged("BuildingUsage", e.OldValue, e.NewValue);
                }
            }
        }

        private static void OnFloorCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    infraMarker.FloorCount = (int)e.NewValue;
                    control.OnMarkerPropertyChanged("FloorCount", e.OldValue, e.NewValue);
                }
            }
        }

        private static void OnBasementFloorCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    infraMarker.BasementFloorCount = (int)e.NewValue;
                    control.OnMarkerPropertyChanged("BasementFloorCount", e.OldValue, e.NewValue);
                }
            }
        }

        private static void OnBuildingAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    infraMarker.BuildingArea = (double)e.NewValue;
                    control.OnMarkerPropertyChanged("BuildingArea", e.OldValue, e.NewValue);
                }
            }
        }

        #endregion
    }
}