using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Linq;
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
            AvailableBuildingTypes = Enum.GetValues<EnumBuildingType>();

            // 건물 용도 목록 초기화
            AvailableBuildingUsages = Enum.GetValues<EnumBuildingUsage>();
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
        /// 층수 개념이 있는 건물 종류인가 — 지상/지하 슬라이더 활성 게이트(D-6).
        /// 게이트·발전기·물탱크·헬리패드·교량은 층수가 형상에 반영되지 않으므로 입력을 막아 무음 실패를 없앤다.
        /// </summary>
        public bool SupportsFloors
        {
            get { return (bool)GetValue(SupportsFloorsProperty); }
            private set { SetValue(SupportsFloorsProperty, value); }
        }

        public static readonly DependencyProperty SupportsFloorsProperty =
            DependencyProperty.Register("SupportsFloors", typeof(bool),
                typeof(GMapPropertyInfraControl),
                new PropertyMetadata(true));

        public static bool BuildingTypeSupportsFloors(EnumBuildingType type) => type is not
            (EnumBuildingType.Gate or EnumBuildingType.Generator or EnumBuildingType.WaterTank or EnumBuildingType.Helipad or EnumBuildingType.Bridge);

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
                new PropertyMetadata(1, OnFloorCountChanged, Helpers.Numerics.MapNumericFields.FloorCount.Coerce));

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
                new PropertyMetadata(0, OnBasementFloorCountChanged, Helpers.Numerics.MapNumericFields.BasementFloorCount.Coerce));

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
                new PropertyMetadata(100.0, OnBuildingAreaChanged, Helpers.Numerics.MapNumericFields.BuildingArea.Coerce));

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
                SetBinding(FloorCountProperty, CreateTwoWayBinding(nameof(infraMarker.FloorCount), Helpers.Numerics.MapNumericFields.FloorCount));
                SetBinding(BasementFloorCountProperty, CreateTwoWayBinding(nameof(infraMarker.BasementFloorCount), Helpers.Numerics.MapNumericFields.BasementFloorCount));
                SetBinding(BuildingAreaProperty, CreateTwoWayBinding(nameof(infraMarker.BuildingArea), Helpers.Numerics.MapNumericFields.BuildingArea));
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

            //System.Diagnostics.Debug.WriteLine($"인프라 마커 속성 로드:");
            //System.Diagnostics.Debug.WriteLine($"  BuildingType: {infraMarker.BuildingType}");
            //System.Diagnostics.Debug.WriteLine($"  FloorCount: B{infraMarker.BasementFloorCount}/F{infraMarker.FloorCount}");
            //System.Diagnostics.Debug.WriteLine($"  BuildingArea: {infraMarker.BuildingArea}㎡");
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

        /// <summary>그룹 Pending — 건물 칸도 "전원 동일=값 / 다름=빈칸(—)"(같은 종류 다중 선택 = 단일 선택 칸 전부, 2026-10-19).
        /// 층수(int)는 NaN 이 없어 대표값을 두고 <see cref="IsFloorCountMixed"/> 로 글만 "—" 로 바꾼다.</summary>
        protected override void ApplyGroupPendingSpecific()
        {
            base.ApplyGroupPendingSpecific();
            var g = GroupMarkers!.OfType<IInfraEditableMarker>().ToList();
            if (g.Count < 2) return;
            bool AllEq<T>(Func<IInfraEditableMarker, T> sel) => g.All(m => System.Collections.Generic.EqualityComparer<T>.Default.Equals(sel(m), sel(g[0])));
            BuildingType = AllEq(m => m.BuildingType) ? g[0].BuildingType : (EnumBuildingType)(-1);
            BuildingUsage = AllEq(m => m.BuildingUsage) ? g[0].BuildingUsage : (EnumBuildingUsage)(-1);
            SupportsFloors = g.All(m => BuildingTypeSupportsFloors(m.BuildingType));   // 층수 칸은 전원이 층을 가질 때만 켠다
            IsFloorCountMixed = !AllEq(m => m.FloorCount);
            IsBasementFloorCountMixed = !AllEq(m => m.BasementFloorCount);
            BuildingArea = AllEq(m => m.BuildingArea) ? g[0].BuildingArea : MIXED_DOUBLE;
        }

        /// <summary>그룹 편집에서 지상층 값이 서로 다름(글 "—").</summary>
        public bool IsFloorCountMixed { get => (bool)GetValue(IsFloorCountMixedProperty); set => SetValue(IsFloorCountMixedProperty, value); }
        public static readonly DependencyProperty IsFloorCountMixedProperty = DependencyProperty.Register(nameof(IsFloorCountMixed), typeof(bool), typeof(GMapPropertyInfraControl), new PropertyMetadata(false));
        /// <summary>그룹 편집에서 지하층 값이 서로 다름(글 "—").</summary>
        public bool IsBasementFloorCountMixed { get => (bool)GetValue(IsBasementFloorCountMixedProperty); set => SetValue(IsBasementFloorCountMixedProperty, value); }
        public static readonly DependencyProperty IsBasementFloorCountMixedProperty = DependencyProperty.Register(nameof(IsBasementFloorCountMixed), typeof(bool), typeof(GMapPropertyInfraControl), new PropertyMetadata(false));

        #endregion

        #region - Property Changed Callbacks -

        private static void OnBuildingTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyInfraControl gate) gate.SupportsFloors = BuildingTypeSupportsFloors((EnumBuildingType)e.NewValue);
            if (d is GMapPropertyInfraControl control && control.SelectedMarker is IInfraEditableMarker infraMarker)
            {
                if (!control._isInitializing && !control._isClearingBindings)
                {
                    if (e.NewValue is EnumBuildingType bt && !Enum.IsDefined(bt)) return;   // 그룹 Pending sentinel — 미전파
                    if (!control.IsGroupMode) infraMarker.BuildingType = (EnumBuildingType)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
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
                    if (e.NewValue is EnumBuildingUsage bu && !Enum.IsDefined(bu)) return;
                    if (!control.IsGroupMode) infraMarker.BuildingUsage = (EnumBuildingUsage)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
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
                    if (!control.IsGroupMode) infraMarker.FloorCount = (int)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                    control.IsFloorCountMixed = false;
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
                    if (!control.IsGroupMode) infraMarker.BasementFloorCount = (int)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                    control.IsBasementFloorCountMixed = false;
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
                    if (double.IsNaN((double)e.NewValue)) return;
                    if (!control.IsGroupMode) infraMarker.BuildingArea = (double)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                    control.OnMarkerPropertyChanged("BuildingArea", e.OldValue, e.NewValue);
                }
            }
        }

        #endregion
    }
}
