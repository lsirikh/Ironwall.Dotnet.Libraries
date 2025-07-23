using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using System;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.Painting;
using System.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using LiveChartsCore.Measure;
using LiveChartsCore.Drawing;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using System.Xml.Linq;
using Org.BouncyCastle.Security;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using MahApps.Metro.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2025 10:13:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventInfoViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public EventInfoViewModel(DeviceProvider deviceProvider
                                , EventProvider eventProvider
                                , IEventDbService eventDbService)
        {

            _deviceProvider = deviceProvider;
            _eventProvider = eventProvider;
            LSeries = new ObservableCollection<ISeries>();
            DSeries = new ObservableCollection<ISeries>();
            _dbService = eventDbService;

            _names = new[] { "DET", "MAL", "CON", "ACT" };
            RefreshActiveness(); 
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            //await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            LSeries.Clear();
            DSeries.Clear();
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        //  실제 토글 로직 - 인덱스 경계 확인만 해주면 OK
        private void ToggleVisibility(int index, bool isEnable)
        {
            if (index < 0 || index >= LSeries.Count) return;

            var sL = LSeries[index];
            sL.IsVisible = isEnable;

            var sD = DSeries[index];
            sD.IsVisible = isEnable;

            // 시리즈 객체 내부에서 INotifyPropertyChanged 를 구현하므로
            // 별도 NotifyOfPropertyChange 호출 없이도 차트가 즉시 재렌더링됩니다.
        }


        public void SetData(DateTime startDate, DateTime endDate, string[] names)
        {
            SetDate(startDate, endDate);
            SetNames(names);
        }

        private void SetDate(DateTime startDate, DateTime endDate)
        {
            _startDate = startDate;
            _endDate = endDate;
        }

        private void SetNames(string[] names)
        {
            _names = names;
            RefreshActiveness();
        }

        private void RefreshActiveness()
        {
            SetFlag(ref _isDetectionActive, _names.Contains("DET"), nameof(IsDetectionActive));
            SetFlag(ref _isMalfunctionActive, _names.Contains("MAL"), nameof(IsMalfunctionActive));
            SetFlag(ref _isConnectionActive, _names.Contains("CON"), nameof(IsConnectionActive));
            SetFlag(ref _isActionActive, _names.Contains("ACT"), nameof(IsActionActive));
        }

        private void SetFlag(ref bool field, bool value, string propertyName)
        {
            if (field == value) return;
            field = value;
            NotifyOfPropertyChange(propertyName);
        }

        private void OnPointMeasured(ChartPoint<float, RoundedRectangleGeometry, LabelGeometry> point)
        {
            var perPointDelay = 100; // in milliseconds
            var delay = point.Context.Entity.MetaData!.EntityIndex * perPointDelay;
            var speed = (float)point.Context.Chart.AnimationsSpeed.TotalMilliseconds + delay;

            // the animation takes a function, that represents the progress of the animation
            // the parameter is the progress of the animation, it goes from 0 to 1
            // the function must return a value from 0 to 1, where 0 is the initial state
            // and 1 is the end state

            point.Visual?.SetTransition(
                new Animation(progress =>
                {
                    var d = delay / speed;

                    return progress <= d
                        ? 0
                        : EasingFunctions.BuildCustomElasticOut(1.5f, 0.60f)((progress - d) / (1 - d));
                },
                TimeSpan.FromMilliseconds(speed)));
        }



        ////
        //public List<double> CreateDetectionCount()
        //{
        //    List<double> dCount = DataHelper.GetDetectionCountsByController(_startDate, _endDate,
        //                DeviceProvider.OfType<IControllerDeviceModel>(),
        //                _eventProvider.OfType<IDetectionEventModel>());

        //    return dCount;
        //}

        //public ISeries GetDetectBarDataSet(int index, List<double> dCount)
        //{
        //    return ChartHelper.MakeBar(_names[index++], dCount, new SKColor(255, 205, 0), new SKColor(255, 255, 255));
        //}

        //public ISeries GetDetectDognutDataSet(int index, List<double> dCount)
        //{
        //    return ChartHelper.MakeBar(_names[index++], dCount, new SKColor(255, 205, 0), new SKColor(255, 255, 255));
        //}


        public Task DataInitialize(CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    // 이벤트 인스턴스 가져오기
                    await _dbService.FetchInstanceAsync(_startDate, _endDate, cancellationToken);

                    // 컨트롤러(Device) 번호 → 문자열 레이블
                    var devices = _deviceProvider.OfType<IControllerDeviceModel>()
                                                .OrderBy(d => d.DeviceNumber);          // 보기 좋게 정렬
                                               

                    //List<double> mCount = DataHelper.GetMalfunctionCountsByController(_startDate, _endDate,
                    //    DeviceProvider.OfType<IControllerDeviceModel>(),
                    //    _eventProvider.OfType<IMalfunctionEventModel>());

                    //List<double> cCount = DataHelper.GetConnectionCountsByController(_startDate, _endDate,
                    //    DeviceProvider.OfType<IControllerDeviceModel>(),
                    //    _eventProvider.OfType<IConnectionEventModel>());

                    //List<double> aCount = DataHelper.GetActionCountsByController(_startDate, _endDate,
                    //    DeviceProvider.OfType<IControllerDeviceModel>(),
                    //    _eventProvider.OfType<IActionEventModel>());

                    //int index = 0;
                    //var totalBarSeries = new[]
                    //{
                    //    //ChartHelper.MakeBar(_names[index++], dCount, new SKColor(255, 205, 0), new SKColor(255,255,255)),
                    //    ChartHelper.MakeBar(_names[index++], mCount, new SKColor( 30,144,255), new SKColor(255,255,255)),
                    //    ChartHelper.MakeBar(_names[index++], cCount, new SKColor(155, 89,182), new SKColor(255,255,255)),
                    //    ChartHelper.MakeBar(_names[index++], aCount, new SKColor( 50,205, 50), new SKColor(255,255,255)),
                    //};

                    //index = 0;
                    //var totalDognutSeries = new[]
                    //{
                    //    //ChartHelper.MakePie(_names[index++], dCount.Sum(), new SKColor(255, 205, 0), new SKColor(255,255,255)),
                    //    ChartHelper.MakePie(_names[index++], mCount.Sum(), new SKColor( 30,144,255), new SKColor(255,255,255)),
                    //    ChartHelper.MakePie(_names[index++], cCount.Sum(), new SKColor(155, 89,182), new SKColor(255,255,255)),
                    //    ChartHelper.MakePie(_names[index++], aCount.Sum(), new SKColor( 50,205, 50), new SKColor(255,255,255)),
                    //};
                    // ─── X축: “카테고리” 4개 ────────────────────────────────



                    var xLabel = new Axis
                    {
                        Labels = devices.Select(d => d.DeviceNumber.ToString()).ToArray(),
                        Name = "controller",
                        Position = AxisPosition.Start,
                        NameTextSize = 15,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        UnitWidth = 1,
                        // (선택) 줄눈 제거
                        NamePadding = new Padding(0, -10, 0, 5),   // L,T,R,B
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        ShowSeparatorLines = false
                    };

                    var yLabels = new Axis
                    {
                        Name = "events",
                        Position = AxisPosition.Start,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        NameTextSize = 15,
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 0),
                        NamePadding = new Padding(0, 5, 0, -10),   // L,T,R,B
                        MinLimit = 0
                    };


                    //DispatcherService.Invoke(async () =>
                    //{
                    //    LSeries.Clear();
                    //    DSeries.Clear();
                    //    XAxes.Clear();
                    //    XAxes.Add(xLabel);      // 컬렉션 변경 → 차트가 즉시 갱신
                    //    YAxes.Clear();
                    //    YAxes.Add(yLabels);

                    //    int delay = 100;

                    //    foreach (var item in totalBarSeries)
                    //    {
                    //        LSeries.Add(item);
                    //        //await Task.Delay(delay);
                    //    }

                    //    foreach (var item in totalDognutSeries)
                    //    {
                    //        DSeries.Add(item);
                    //        //await Task.Delay(delay);
                    //    }

                    //});


                    DispatcherService.Invoke(() => {
                        LSeries.Clear();
                        DSeries.Clear();
                        XAxes.Clear();
                        XAxes.Add(xLabel);
                        YAxes.Clear();
                        YAxes.Add(yLabels);

                        foreach (var code in _names)
                        {
                            if (!_meta.TryGetValue(code, out var m)) continue;


                            /* 1) 데이터 계산 */
                            var counts = m.Counter(_startDate, _endDate, devices, _eventProvider);

                            /* 2) Bar + Pie 시리즈 */
                            var bar = ChartHelper.MakeBar(
                                m.DisplayName, counts, m.BarColor, SKColors.White);

                            var pie = ChartHelper.MakePie(
                                m.DisplayName, counts.Sum(), m.PieColor, SKColors.White);

                            /* 3) 추가 */
                            LSeries.Add(bar);
                            DSeries.Add(pie);
                        }
                    });

                }
                catch (TaskCanceledException ex)
                {
                    _log?.Warning($"Raised {nameof(TaskCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
                }
                finally
                {
                }
            });
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public bool IsDetectionEnable
        {
            get { return _isDetectionEnable; }
            set 
            { _isDetectionEnable = value; ToggleVisibility(0, value); }
        }


        public bool IsMalfunctionEnable
        {
            get { return _isMalfunctionEnable; }
            set { _isMalfunctionEnable = value; ToggleVisibility(1, value); }
        }


        public bool IsConnectionEnable
        {
            get { return _isConnectionEnable; }
            set { _isConnectionEnable = value; ToggleVisibility(2, value); }
        }


        public bool IsActionEnable
        {
            get { return _isActionEnable; }
            set { _isActionEnable = value; ToggleVisibility(3, value); }
        }

        public SolidColorPaint LegendTextPaint { get; set; } =
        new SolidColorPaint
        {
            Color = new SKColor(50, 50, 50),
            SKTypeface = SKTypeface.FromFamilyName("Caliber")
        };

        public SolidColorPaint LedgendBackgroundPaint { get; set; } =
            new SolidColorPaint(new SKColor(240, 240, 240, 00));


        public ObservableCollection<ISeries> LSeries { get; private set; }
        public ObservableCollection<ISeries> DSeries { get; private set; }

        public ObservableCollection<Axis> XAxes { get; } = [];
        public ObservableCollection<Axis> YAxes { get; } = [];

        private readonly Dictionary<string, CategoryMeta> _meta = new()
        {
            ["DET"] = new(
                "Detection", 
                new SKColor(255, 205, 0),
                new SKColor(255, 205, 0), 
                (from, to, ctrls, evts) => 
                DataHelper.GetDetectionCountsByController(from, to, ctrls, evts.OfType<IDetectionEventModel>())),
            ["MAL"] = new(
                "Malfunction", 
                new SKColor(30, 144, 255), 
                new SKColor(30, 144, 255), 
                (from, to, ctrls, evts) =>
                DataHelper.GetMalfunctionCountsByController(from, to, ctrls, evts.OfType<IMalfunctionEventModel>())),
            ["CON"] = new(
                "Connection", 
                new SKColor(155, 89, 182), 
                new SKColor(155, 89, 182),
                (from, to, ctrls, evts) => 
                DataHelper.GetConnectionCountsByController(from, to, ctrls, evts.OfType<IConnectionEventModel>())),
            ["ACT"] = new(
                "Action", 
                new SKColor(50, 205, 50), 
                new SKColor(50, 205, 50),
                (from, to, ctrls, evts) => 
                DataHelper.GetActionCountsByController(from, to, ctrls, evts.OfType<IActionEventModel>()))
        };

        public bool IsDetectionActive 
        { 
            get => _isDetectionActive; 
            private set => SetFlag(ref _isDetectionActive, value, nameof(IsDetectionActive)); 
        }
        public bool IsMalfunctionActive 
        { 
            get => _isMalfunctionActive; 
            private set => SetFlag(ref _isMalfunctionActive, value, nameof(IsMalfunctionActive)); 
        }
        public bool IsConnectionActive 
        { 
            get => _isConnectionActive;
            private set => SetFlag(ref _isConnectionActive, value, nameof(IsConnectionActive)); 
        }
        public bool IsActionActive 
        {
            get => _isActionActive; 
            private set => SetFlag(ref _isActionActive, value, nameof(IsActionActive));
        }

        #endregion
        #region - Attributes -
        private IEventDbService _dbService;
        private DeviceProvider _deviceProvider;
        private EventProvider _eventProvider;
        private string[] _names;
        private DateTime _startDate;
        private DateTime _endDate;

        private bool _isDetectionActive;
        private bool _isMalfunctionActive;
        private bool _isConnectionActive;
        private bool _isActionActive;

        private bool _isDetectionEnable = true;
        private bool _isMalfunctionEnable = true;
        private bool _isConnectionEnable = true;
        private bool _isActionEnable = true;
        #endregion
    }

    // ─── 파일 최상위 (using 아래 아무 곳) ────────────────────────────
    readonly record struct CategoryMeta(
        string DisplayName,          // ← displayName 으로 선언
        SKColor BarColor,
        SKColor PieColor,
        CountsCounter Counter);

    // delegate 도 “파일 최상위” 에 둔다
    delegate List<double> CountsCounter(
        DateTime from, DateTime to,
        IEnumerable<IControllerDeviceModel> ctrls,
        IEnumerable<IBaseEventModel> evts);
}