using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapMilitary{
    /****************************************************************************
       Purpose      : 군사 심볼 등록/생성을 위한 컨트롤                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapMilitarySymbolRegisterControl : Control
    {
        #region Static Constructor
        static GMapMilitarySymbolRegisterControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMilitarySymbolRegisterControl),
                new FrameworkPropertyMetadata(typeof(GMapMilitarySymbolRegisterControl)));
        }
        #endregion

        #region Dependency Properties

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
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(EnumMilitaryAffiliation.Friend, OnAffiliationChangedRegister));

        /// <summary>
        /// 전투 차원 (공중성)
        /// </summary>
        public EnumMilitaryBattleDimension BattleDimension
        {
            get { return (EnumMilitaryBattleDimension)GetValue(BattleDimensionProperty); }
            set { SetValue(BattleDimensionProperty, value); }
        }

        public static readonly DependencyProperty BattleDimensionProperty =
            DependencyProperty.Register("BattleDimension", typeof(EnumMilitaryBattleDimension),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(EnumMilitaryBattleDimension.Land, OnBattleDimensionChangedRegister));

       

        /// <summary>
        /// 표준 정체성 (계획 속성)
        /// </summary>
        public EnumMilitaryStandardIdentity StandardIdentity
        {
            get { return (EnumMilitaryStandardIdentity)GetValue(StandardIdentityProperty); }
            set { SetValue(StandardIdentityProperty, value); }
        }

        public static readonly DependencyProperty StandardIdentityProperty =
            DependencyProperty.Register("StandardIdentity", typeof(EnumMilitaryStandardIdentity),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(EnumMilitaryStandardIdentity.Present));

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
                typeof(GMapMilitarySymbolRegisterControl),
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
                typeof(GMapMilitarySymbolRegisterControl),
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
                typeof(GMapMilitarySymbolRegisterControl),
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
            DependencyProperty.Register("HigherFormation", typeof(string),
                typeof(GMapMilitarySymbolRegisterControl),
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
            DependencyProperty.Register("CallSign", typeof(string),
                typeof(GMapMilitarySymbolRegisterControl),
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
            DependencyProperty.Register("CountryCode", typeof(string),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 사용 가능한 부대 종류 목록
        /// </summary>
        public IEnumerable<EnumMilitaryUnitType> AvailableUnitTypes
        {
            get { return (IEnumerable<EnumMilitaryUnitType>)GetValue(AvailableUnitTypesProperty); }
            set { SetValue(AvailableUnitTypesProperty, value); }
        }

        public static readonly DependencyProperty AvailableUnitTypesProperty =
            DependencyProperty.Register("AvailableUnitTypes", typeof(IEnumerable<EnumMilitaryUnitType>),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 사용 가능한 부대 규모 목록
        /// </summary>
        public IEnumerable<EnumMilitaryUnitSize> AvailableUnitSizes
        {
            get { return (IEnumerable<EnumMilitaryUnitSize>)GetValue(AvailableUnitSizesProperty); }
            set { SetValue(AvailableUnitSizesProperty, value); }
        }

        public static readonly DependencyProperty AvailableUnitSizesProperty =
            DependencyProperty.Register("AvailableUnitSizes", typeof(IEnumerable<EnumMilitaryUnitSize>),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 사용 가능한 표준 정체성 목록
        /// </summary>
        public IEnumerable<EnumMilitaryStandardIdentity> AvailableStandardIdentities
        {
            get { return (IEnumerable<EnumMilitaryStandardIdentity>)GetValue(AvailableStandardIdentitiesProperty); }
            set { SetValue(AvailableStandardIdentitiesProperty, value); }
        }

        public static readonly DependencyProperty AvailableStandardIdentitiesProperty =
            DependencyProperty.Register("AvailableStandardIdentities", typeof(IEnumerable<EnumMilitaryStandardIdentity>),
                typeof(GMapMilitarySymbolRegisterControl),
                new PropertyMetadata(null));

        #endregion

        #region Commands

        /// <summary>
        /// 취소 명령
        /// </summary>
        public ICommand CancelCommand
        {
            get { return (ICommand)GetValue(CancelCommandProperty); }
            set { SetValue(CancelCommandProperty, value); }
        }

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register("CancelCommand", typeof(ICommand),
                typeof(GMapMilitarySymbolRegisterControl));

        /// <summary>
        /// 등록 명령
        /// </summary>
        public ICommand RegisterCommand
        {
            get { return (ICommand)GetValue(RegisterCommandProperty); }
            set { SetValue(RegisterCommandProperty, value); }
        }

        public static readonly DependencyProperty RegisterCommandProperty =
            DependencyProperty.Register("RegisterCommand", typeof(ICommand),
                typeof(GMapMilitarySymbolRegisterControl));

        #endregion

        #region Events

        /// <summary>
        /// 군사 심볼 등록 요청 이벤트
        /// </summary>
        public event EventHandler<MilitarySymbolRegisterEventArgs>? MilitarySymbolRegisterRequested;

        /// <summary>
        /// 취소 요청 이벤트
        /// </summary>
        public event EventHandler? CancelRequested;

        #endregion

        #region Constructor

        public GMapMilitarySymbolRegisterControl()
        {
            InitializeAvailableValues();
            InitializeCommands();
            InitializeDragSupport();

        }

       

        #endregion

        #region Initialization Methods

        private void InitializeAvailableValues()
        {
            // 사용 가능한 부대 종류 (15개 NATO 표준 항목들)
            AvailableUnitTypes = new[]
            {
                EnumMilitaryUnitType.Ammunition,                // 탄약
                EnumMilitaryUnitType.Armour,                   // 기갑
                EnumMilitaryUnitType.Artillery,                // 포병
                EnumMilitaryUnitType.RotaryWingAviation,       // 회전익항공
                EnumMilitaryUnitType.FixedWingAviation,        // 고정익항공
                EnumMilitaryUnitType.Bridging,                 // 교량
                EnumMilitaryUnitType.CombatServiceSupport,     // 전투근무지원
                EnumMilitaryUnitType.CombinedManoeuvreArms,    // 연합기동부대
                EnumMilitaryUnitType.Engineer,                 // 공병
                EnumMilitaryUnitType.ElectronicWarfare,        // 전자전
                EnumMilitaryUnitType.ExplosiveOrdnanceDisposal,// 폭발물처리
                EnumMilitaryUnitType.FuelPOL,                  // 연료보급
                //EnumMilitaryUnitType.Maintenance,              // 정비
                //EnumMilitaryUnitType.Meteorological            // 기상
            };

            // 사용 가능한 부대 규모
            AvailableUnitSizes = Enum.GetValues<EnumMilitaryUnitSize>();

            // 사용 가능한 표준 정체성
            AvailableStandardIdentities = Enum.GetValues<EnumMilitaryStandardIdentity>();
        }

        private void InitializeCommands()
        {
            CancelCommand = new RelayCommand(_ => OnCancelRequested());
            RegisterCommand = new RelayCommand(_ => OnRegisterRequested(), _ => CanRegister());
        }


        
        #endregion

        #region Command Methods

        private bool CanRegister()
        {
            // 기본적인 유효성 검사
            return !string.IsNullOrWhiteSpace(UnitDesignator);
        }

        private void OnCancelRequested()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnRegisterRequested()
        {
            var militaryModel = CreateMilitarySymbolModel();
            var args = new MilitarySymbolRegisterEventArgs(militaryModel);
            MilitarySymbolRegisterRequested?.Invoke(this, args);
        }

        #endregion
        #region Drag Support Methods

        private void InitializeDragSupport()
        {
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(this);

            // 헤더 영역만 드래그 가능 (상단 45px)
            if (position.Y > 45) return;

            // Canvas 찾기
            var contentPresenter = FindParentOfType<ContentPresenter>(this);
            var canvas = contentPresenter?.Parent as Canvas;

            if (canvas == null) return;

            _isDragging = true;
            _lastMousePosition = e.GetPosition(canvas);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var contentPresenter = FindParentOfType<ContentPresenter>(this);
            var canvas = contentPresenter?.Parent as Canvas;

            if (canvas == null) return;

            var currentPosition = e.GetPosition(canvas);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            var currentLeft = Canvas.GetLeft(contentPresenter);
            var currentTop = Canvas.GetTop(contentPresenter);

            if (double.IsNaN(currentLeft)) currentLeft = 0;
            if (double.IsNaN(currentTop)) currentTop = 0;

            Canvas.SetLeft(contentPresenter, currentLeft + deltaX);
            Canvas.SetTop(contentPresenter, currentTop + deltaY);

            _lastMousePosition = currentPosition;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();
        }

        private T FindParentOfType<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as T;
        }

        #endregion
        #region Public Methods

        /// <summary>
        /// 현재 설정으로 군사 심볼 모델 생성
        /// </summary>
        public MilitarySymbolModel CreateMilitarySymbolModel()
        {
            return new MilitarySymbolModel
            {
                Affiliation = this.Affiliation,
                BattleDimension = this.BattleDimension,
                StandardIdentity = this.StandardIdentity,
                UnitType = this.UnitType,
                UnitSize = this.UnitSize,
                UnitDesignator = this.UnitDesignator,
                HigherFormation = this.HigherFormation,
                CallSign = this.CallSign,
                CountryCode = this.CountryCode,
                Title = !string.IsNullOrEmpty(UnitDesignator) ? UnitDesignator : "새 군사 심볼"
            };
        }

        /// <summary>
        /// 기본값으로 초기화
        /// </summary>
        public void ResetToDefaults()
        {
            Affiliation = EnumMilitaryAffiliation.Friend;
            BattleDimension = EnumMilitaryBattleDimension.Land;
            StandardIdentity = EnumMilitaryStandardIdentity.Present;
            UnitType = EnumMilitaryUnitType.Infantry;
            UnitSize = EnumMilitaryUnitSize.Company;
            UnitDesignator = string.Empty;
            HigherFormation = string.Empty;
            CallSign = string.Empty;
            CountryCode = string.Empty;
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnAffiliationChangedRegister(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMilitarySymbolRegisterControl control)
            {
                // UnitType 변경 시 미리보기 업데이트 등 추가 로직
                System.Diagnostics.Debug.WriteLine($"[Register] Affiliation 변경: {e.OldValue} → {e.NewValue}");
                // 템플릿에서 미리보기 컨트롤 직접 찾아서 업데이트
                if (control.Template != null)
                {
                    var preview = control.Template.FindName("PART_MilitarySymbolPreview", control) as GMapMilitarySymbolMarkerControl;
                    if (preview != null)
                    {
                        System.Diagnostics.Debug.WriteLine("미리보기 컨트롤 직접 업데이트 시도");
                        preview.Affiliation = (EnumMilitaryAffiliation)e.NewValue;
                    }
                }
            }
            
        }

        private static void OnBattleDimensionChangedRegister(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMilitarySymbolRegisterControl control)
            {
                // BattleDimension 변경 시 미리보기 업데이트 등 추가 로직
                System.Diagnostics.Debug.WriteLine($"[Register] BattleDimension 변경: {e.OldValue} → {e.NewValue}");
                // 템플릿에서 미리보기 컨트롤 직접 찾아서 업데이트
                if (control.Template != null)
                {
                    var preview = control.Template.FindName("PART_MilitarySymbolPreview", control) as GMapMilitarySymbolMarkerControl;
                    if (preview != null)
                    {
                        System.Diagnostics.Debug.WriteLine("미리보기 컨트롤 직접 업데이트 시도");
                        preview.BattleDimension = (EnumMilitaryBattleDimension)e.NewValue;
                    }
                }
            }
        }

        private static void OnUnitTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMilitarySymbolRegisterControl control)
            {
                // UnitType 변경 시 미리보기 업데이트 등 추가 로직
                System.Diagnostics.Debug.WriteLine($"[Register] UnitType 변경: {e.OldValue} → {e.NewValue}");
                // 템플릿에서 미리보기 컨트롤 직접 찾아서 업데이트
                if (control.Template != null)
                {
                    var preview = control.Template.FindName("PART_MilitarySymbolPreview", control) as GMapMilitarySymbolMarkerControl;
                    if (preview != null)
                    {
                        System.Diagnostics.Debug.WriteLine("미리보기 컨트롤 직접 업데이트 시도");
                        preview.UnitType = (EnumMilitaryUnitType)e.NewValue;

                        switch (preview.UnitType)
                        {
                            case EnumMilitaryUnitType.Infantry:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.AirDefence:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Ammunition:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.AntiTank:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Armour:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Artillery:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Black);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Bridging:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.CombatServiceSupport:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.CombinedManoeuvreArms:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Engineer:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.ElectronicRanging:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.ElectronicWarfare:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.ExplosiveOrdnanceDisposal:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.FuelPOL:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Hospital:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.HQUnit:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Maintenance:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Medical:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.Meteorological:
                            case EnumMilitaryUnitType.Missile:
                            case EnumMilitaryUnitType.Mortar:
                            case EnumMilitaryUnitType.MilitaryPolice:
                            case EnumMilitaryUnitType.CBRNDefence:
                            case EnumMilitaryUnitType.Ordnance:
                            case EnumMilitaryUnitType.PsychologicalOperations:
                            case EnumMilitaryUnitType.ReconnaissanceCavalry:
                            case EnumMilitaryUnitType.Signals:
                            case EnumMilitaryUnitType.SpecialForces:
                            case EnumMilitaryUnitType.SpecialOperationsForces:
                            case EnumMilitaryUnitType.Supply:
                            case EnumMilitaryUnitType.Topographical:
                            case EnumMilitaryUnitType.Transportation:
                            case EnumMilitaryUnitType.RotaryWingAviation:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Black);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.FixedWingAviation:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Black);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            case EnumMilitaryUnitType.UnmannedAirVehicle:
                            case EnumMilitaryUnitType.Radar:
                            case EnumMilitaryUnitType.Navy:
                                preview.MarkerFill = ColorHelper.ToBrush(EnumColorType.Transparent);
                                preview.MarkerStroke = ColorHelper.ToBrush(EnumColorType.Black);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMilitarySymbolRegisterControl control)
            {
                // UnitSize 변경 시 미리보기 업데이트 등 추가 로직
                System.Diagnostics.Debug.WriteLine($"UnitSize 변경: {e.OldValue} → {e.NewValue}");
                // 템플릿에서 미리보기 컨트롤 직접 찾아서 업데이트
                if (control.Template != null)
                {
                    var preview = control.Template.FindName("PART_MilitarySymbolPreview", control) as GMapMilitarySymbolMarkerControl;
                    if (preview != null)
                    {
                        System.Diagnostics.Debug.WriteLine("미리보기 컨트롤 직접 업데이트 시도");
                        preview.UnitSize = (EnumMilitaryUnitSize)e.NewValue;
                    }
                }
            }
        }

        #endregion

        #region Fields
        private bool _isDragging;
        private Point _lastMousePosition;
        #endregion
    }


}