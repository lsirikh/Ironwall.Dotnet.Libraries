using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/10/2025 4:45:33 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyMilitaryControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyMilitaryControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyMilitaryControl),
               new FrameworkPropertyMetadata(typeof(GMapPropertyMilitaryControl)));
        }

        public GMapPropertyMilitaryControl()
        {
            
        }
        #endregion

        #region - Implementation of Interface -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== MilitaryControl ClearSpecificBindings 시작 ===");

            BindingOperations.ClearBinding(this, AffiliationProperty);
            BindingOperations.ClearBinding(this, BattleDimensionProperty);
            BindingOperations.ClearBinding(this, StandardIdentityProperty);
            BindingOperations.ClearBinding(this, UnitTypeProperty);
            BindingOperations.ClearBinding(this, UnitSizeProperty);
            BindingOperations.ClearBinding(this, UnitDesignatorProperty);
            BindingOperations.ClearBinding(this, HigherFormationProperty);
            BindingOperations.ClearBinding(this, CallSignProperty);
            BindingOperations.ClearBinding(this, CountryCodeProperty);

            //System.Diagnostics.Debug.WriteLine("=== MilitaryControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== MilitaryControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IMilitarySymbolEditableMarker militaryMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {militaryMarker.GetType().Name}");

                // 군사 심볼 핵심 속성 바인딩
                var affiliationBinding = CreateTwoWayBinding(nameof(militaryMarker.Affiliation));
                SetBinding(AffiliationProperty, affiliationBinding);

                var battleDimensionBinding = CreateTwoWayBinding(nameof(militaryMarker.BattleDimension));
                SetBinding(BattleDimensionProperty, battleDimensionBinding);

                var standardIdentityBinding = CreateTwoWayBinding(nameof(militaryMarker.StandardIdentity));
                SetBinding(StandardIdentityProperty, standardIdentityBinding);

                var unitTypeBinding = CreateTwoWayBinding(nameof(militaryMarker.UnitType));
                SetBinding(UnitTypeProperty, unitTypeBinding);

                var unitSizeBinding = CreateTwoWayBinding(nameof(militaryMarker.UnitSize));
                SetBinding(UnitSizeProperty, unitSizeBinding);

                // 텍스트 속성 바인딩
                var unitDesignatorBinding = CreateTwoWayBinding(nameof(militaryMarker.UnitDesignator));
                SetBinding(UnitDesignatorProperty, unitDesignatorBinding);

                var higherFormationBinding = CreateTwoWayBinding(nameof(militaryMarker.HigherFormation));
                SetBinding(HigherFormationProperty, higherFormationBinding);

                var callSignBinding = CreateTwoWayBinding(nameof(militaryMarker.CallSign));
                SetBinding(CallSignProperty, callSignBinding);

                var countryCodeBinding = CreateTwoWayBinding(nameof(militaryMarker.CountryCode));
                SetBinding(CountryCodeProperty, countryCodeBinding);
            }

            //System.Diagnostics.Debug.WriteLine("=== MilitaryControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IMilitarySymbolEditableMarker militaryMarker)) return;

            this.Affiliation = militaryMarker.Affiliation;
            this.BattleDimension = militaryMarker.BattleDimension;
            this.StandardIdentity = militaryMarker.StandardIdentity;
            this.UnitType = militaryMarker.UnitType;
            this.UnitSize = militaryMarker.UnitSize;
            this.UnitDesignator = militaryMarker.UnitDesignator ?? string.Empty;
            this.HigherFormation = militaryMarker.HigherFormation ?? string.Empty;
            this.CallSign = militaryMarker.CallSign ?? string.Empty;
            this.CountryCode = militaryMarker.CountryCode ?? string.Empty;

            //System.Diagnostics.Debug.WriteLine($"군사 심볼 마커에서 속성 로드 완료");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IMilitarySymbolEditableMarker militaryMarker)
            {
                militaryMarker.Affiliation = this.Affiliation;
                militaryMarker.BattleDimension = this.BattleDimension;
                militaryMarker.StandardIdentity = this.StandardIdentity;
                militaryMarker.UnitType = this.UnitType;
                militaryMarker.UnitSize = this.UnitSize;
                militaryMarker.UnitDesignator = this.UnitDesignator;
                militaryMarker.HigherFormation = this.HigherFormation;
                militaryMarker.CallSign = this.CallSign;
                militaryMarker.CountryCode = this.CountryCode;
            }
        }

        /// <summary>그룹 Pending — 이 종류의 칸도 "전원 동일=값 / 다름=빈칸(—)"(같은 종류 다중 선택 = 단일 선택 칸 전부, 2026-10-19).</summary>
        protected override void ApplyGroupPendingSpecific()
        {
            base.ApplyGroupPendingSpecific();
            var g = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<IMilitarySymbolEditableMarker>(GroupMarkers!));
            if (g.Count < 2) return;
            bool AllEq<T>(Func<IMilitarySymbolEditableMarker, T> sel) => System.Linq.Enumerable.All(g, m => System.Collections.Generic.EqualityComparer<T>.Default.Equals(sel(m), sel(g[0])));
            Affiliation = AllEq(m => m.Affiliation) ? g[0].Affiliation : (EnumMilitaryAffiliation)(-1);
            BattleDimension = AllEq(m => m.BattleDimension) ? g[0].BattleDimension : (EnumMilitaryBattleDimension)(-1);
            StandardIdentity = AllEq(m => m.StandardIdentity) ? g[0].StandardIdentity : (EnumMilitaryStandardIdentity)(-1);
            UnitType = AllEq(m => m.UnitType) ? g[0].UnitType : (EnumMilitaryUnitType)(-1);
            UnitSize = AllEq(m => m.UnitSize) ? g[0].UnitSize : (EnumMilitaryUnitSize)(-1);
            HigherFormation = AllEq(m => m.HigherFormation) ? g[0].HigherFormation ?? string.Empty : string.Empty;
            CountryCode = AllEq(m => m.CountryCode) ? g[0].CountryCode ?? string.Empty : string.Empty;
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyMilitaryControl);
        }

        #endregion

        #region - Overrides -
        #endregion

        #region - Binding Methods -
        #endregion

        #region - Processes -
        #endregion

        #region - IHandles -
        #endregion

        #region - Dependency Properties -

        /// <summary>
        /// 소속 구분
        /// </summary>
        public EnumMilitaryAffiliation Affiliation
        {
            get { return (EnumMilitaryAffiliation)GetValue(AffiliationProperty); }
            set { SetValue(AffiliationProperty, value); }
        }

        public static readonly DependencyProperty AffiliationProperty =
            DependencyProperty.Register("Affiliation", typeof(EnumMilitaryAffiliation),
                typeof(GMapPropertyMilitaryControl),
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
            DependencyProperty.Register("BattleDimension", typeof(EnumMilitaryBattleDimension),
                typeof(GMapPropertyMilitaryControl),
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
            DependencyProperty.Register("StandardIdentity", typeof(EnumMilitaryStandardIdentity),
                typeof(GMapPropertyMilitaryControl),
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
            DependencyProperty.Register("UnitType", typeof(EnumMilitaryUnitType),
                typeof(GMapPropertyMilitaryControl),
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
            DependencyProperty.Register("UnitSize", typeof(EnumMilitaryUnitSize),
                typeof(GMapPropertyMilitaryControl),
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
            DependencyProperty.Register("UnitDesignator", typeof(string),
                typeof(GMapPropertyMilitaryControl),
                new PropertyMetadata(string.Empty, OnUnitDesignatorChanged));

        /// <summary>
        /// 상급 부대명
        /// </summary>
        public string HigherFormation
        {
            get { return (string)GetValue(HigherFormationProperty); }
            set { SetValue(HigherFormationProperty, value); }
        }

        public static readonly DependencyProperty HigherFormationProperty =
            DependencyProperty.Register("HigherFormation", typeof(string),
                typeof(GMapPropertyMilitaryControl),
                new PropertyMetadata(string.Empty, OnHigherFormationChanged));

        /// <summary>
        /// 콜사인
        /// </summary>
        public string CallSign
        {
            get { return (string)GetValue(CallSignProperty); }
            set { SetValue(CallSignProperty, value); }
        }

        public static readonly DependencyProperty CallSignProperty =
            DependencyProperty.Register("CallSign", typeof(string),
                typeof(GMapPropertyMilitaryControl),
                new PropertyMetadata(string.Empty, OnCallSignChanged));

        /// <summary>
        /// 국가 코드
        /// </summary>
        public string CountryCode
        {
            get { return (string)GetValue(CountryCodeProperty); }
            set { SetValue(CountryCodeProperty, value); }
        }

        public static readonly DependencyProperty CountryCodeProperty =
            DependencyProperty.Register("CountryCode", typeof(string),
                typeof(GMapPropertyMilitaryControl),
                new PropertyMetadata(string.Empty, OnCountryCodeChanged));

        #endregion

        #region - Property Changed Methods -

        private static void OnAffiliationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnAffiliationChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.Affiliation = (EnumMilitaryAffiliation)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("Affiliation", e.OldValue, e.NewValue);
            }
        }

        private static void OnBattleDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnBattleDimensionChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.BattleDimension = (EnumMilitaryBattleDimension)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("BattleDimension", e.OldValue, e.NewValue);
            }
        }

        private static void OnStandardIdentityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnStandardIdentityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.StandardIdentity = (EnumMilitaryStandardIdentity)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("StandardIdentity", e.OldValue, e.NewValue);
            }
        }

        private static void OnUnitTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnUnitTypeChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.UnitType = (EnumMilitaryUnitType)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("UnitType", e.OldValue, e.NewValue);
            }
        }

        private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnUnitSizeChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.UnitSize = (EnumMilitaryUnitSize)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("UnitSize", e.OldValue, e.NewValue);
            }
        }

        private static void OnUnitDesignatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnUnitDesignatorChanged: '{e.OldValue}' → '{e.NewValue}'");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.UnitDesignator = (string)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("UnitDesignator", e.OldValue, e.NewValue);
            }
        }

        private static void OnHigherFormationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnHigherFormationChanged: '{e.OldValue}' → '{e.NewValue}'");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.HigherFormation = (string)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("HigherFormation", e.OldValue, e.NewValue);
            }
        }

        private static void OnCallSignChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnCallSignChanged: '{e.OldValue}' → '{e.NewValue}'");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.CallSign = (string)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("CallSign", e.OldValue, e.NewValue);
            }
        }

        private static void OnCountryCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnCountryCodeChanged: '{e.OldValue}' → '{e.NewValue}'");

            if (d is GMapPropertyMilitaryControl control &&
                control.SelectedMarker is IMilitarySymbolEditableMarker militaryMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) militaryMarker.CountryCode = (string)e.NewValue;   // 그룹=VM 이 전원 일괄 적용
                control.OnMarkerPropertyChanged("CountryCode", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #region - Attributes -
        #endregion
    }
