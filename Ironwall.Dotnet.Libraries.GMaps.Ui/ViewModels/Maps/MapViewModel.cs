using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using GMap.NET;
using GMap.NET.MapProviders;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using System.IO;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 2:59:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MapViewModel : BasePanelViewModel
{
    #region - Ctors - 
    public MapViewModel(ILogService log,
                        IEventAggregator eventAggregator
                        //CustomPathProvider pathProvider,
                        //SoundPlayerService soundPlayer,
                        //EventProvider eventProvider,
                        //MissionInfoSetupModel missionSetup
                        ) : base(eventAggregator, log)
    {
        _cts = new CancellationTokenSource();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);
        if(view is MapView mapView) 
        {
            MainMap = mapView.MainMap;
        }
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {

        await base.OnActivateAsync(cancellationToken);
        GoogleMapProvider.Instance.ApiKey = "AIzaSyCXJrDpszuNQfMEXKIifx5zYzhSq3Irpyg";

        MainMap.Manager.Mode = AccessMode.ServerAndCache;

        // config map
        MapConfigure();

        if (MainMap.Manager.Mode == AccessMode.CacheOnly)
            await GetMapData();

        //_eventCount = 0;

        //SetInitialHomePosition();

        //SetInitialMissionInfo();
    }

    #endregion
    #region - Binding Methods -
    public void OnClickZoomUp(object sender, EventArgs args)
    {
        if (ZoomMax > MainMap.Zoom)
            MainMap.Zoom++;
    }
    public void OnClickZoomDown(object sender, EventArgs args)
    {
        if (ZoomMin < MainMap.Zoom)
            MainMap.Zoom--;
    }
    #endregion
    #region - Processes -
    public void MapConfigure()
    {
        try
        {
            //MainMap.MapProvider = GMapProviders.GoogleHybridMap;
            MainMap.MapProvider = GMapProviders.GoogleSatelliteMap;
            //MainMap.MapProvider = GMapProviders.BingHybridMap;
            MainMap.Position = new PointLatLng(37.648425, 126.904284);
            MainMap.MinZoom = ZOOM_MIN;
            MainMap.MaxZoom = ZOOM_MAX;
            MainMap.Zoom = DEFAULT_ZOOM;

            MainMap.ShowCenter = false;

            //MainMap.TouchEnabled = false;
            MainMap.MultiTouchEnabled = false;

            //MainMap.MouseDoubleClick += new MouseButtonEventHandler(MainMap_MouseDoubleClick);
            MainMap.OnPositionChanged += MainMap_OnCurrentPositionChanged;
            //MainMap.OnTileLoadComplete += MainMap_OnTileLoadComplete;
            //MainMap.OnTileLoadStart += MainMap_OnTileLoadStart;
            //MainMap.OnMapTypeChanged += MainMap_OnMapTypeChanged;
            MainMap.MouseMove += MainMap_MouseMove;
            //MainMap.MouseEnter += MainMap_MouseEnter;
            MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
            MainMap.OnMapZoomChanged += MainMap_OnMapZoomChanged;

            MainMap.ShowCenter = true;
            MainMap_OnMapZoomChanged();

            SetInitialHomePosition();

        }
        catch (Exception)
        {

            throw;
        }
    }

    private void SetInitialHomePosition()
    {
        HomePosition = new HomePositionModel();
        HomePosition.Position = DEFAULT_LOCATION;
        HomePosition.Zoom = DEFAULT_ZOOM;
        HomePosition.IsAvailable = false;
        _log.Info($"HomePosition정보가 없어서 Default Position으로 설정되었습니다.");
    }

    public Task<bool> GetMapData()
    {
        return Task.Run(() =>
        {

            try
            {
                Thread.Sleep(3000);
                DirectoryInfo di = new DirectoryInfo(System.Environment.CurrentDirectory);
                var dirs = di.GetDirectories();
                foreach (var folder in dirs)
                {
                    var folderName = "maps";
                    if (folder.Name.ToLower() == folderName)
                    {
                        _log?.Info($"Find the folder({folderName}) successfully!");
                        var fileName = "map.gmdb";
                        var file = folder.GetFiles().Where(t => t.Name == fileName).FirstOrDefault();
                        bool ret = false;
                        if (file != null)
                        {
                            _log?.Info($"Find the file({fileName}) successfully!");
                            ret = GMap.NET.GMaps.Instance.ImportFromGMDB(file.FullName);
                        }

                        if (ret)
                        {
                            _log?.Info($"Reload Map from cashed data : {file.Name}");
                            MainMap.ReloadMap();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"Rasied Exception in {nameof(GetMapData)} :  {ex.Message}");
                return false;
            }
        });
    }


    private void MainMap_OnCurrentPositionChanged(PointLatLng point)
    {
        MainMap.Position = point;
    }

    private void MainMap_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(MainMap);

        CurrentPosition = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);

    }


    private void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(MainMap);
        ClickedCurrentPosition = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);

       
    }


    private void MainMap_OnMapZoomChanged()
    {
        CreateScaleBar();
    }

    private void CreateScaleBar()
    {
        double scaleX = 0.0;
        var scale = "";

        /* Scale
         * Zoom : 17, Scale : 50m, Length : 1.5 cm
         * Zoom : 16, Scale : 100m, Length : 1.5 cm
         * Zoom : 15, Scale : 300m, Length : 2.5 cm
         * Zoom : 14, Scale : 500m, Length : 2 cm 
         * Zoom : 13, Scale : 1000m, Length : 2 cm
         * 
         * Zoom : 12, Scale : 3000m, Length : 2.2 cm
         * Zoom : 11, Scale : 5000m, Length : 2.2 cm
         * Zoom : 10, Scale : 10Km, Length : 2.2 cm
         * Zoom : 9, Scale : 20Km, Length : 2 cm
         * Zoom : 8, Scale : 30Km, Length : 1.7 cm
         * Zoom : 7, Scale : 50Km, Length : 1.4 cm
         * Zoom : 6, Scale : 100Km, Length : 1.4 cm
         * 
         */

        switch (Zoom)
        {
            case 6:
                scaleX = 52.9;
                scale = "100Km";
                break;
            case 7:
                scaleX = 52.9;
                scale = "50Km";
                break;
            case 8:
                scaleX = 64.3;
                scale = "30Km";
                break;
            case 9:
                scaleX = 75.6;
                scale = "20Km";
                break;
            case 10:
                scaleX = 83.1;
                scale = "10Km";
                break;
            case 11:
                scaleX = 83.1;
                scale = "5Km";
                break;
            case 12:
                scaleX = 83.1;
                scale = "3Km";
                break;
            case 13:
                scaleX = 75.59;
                scale = "1Km";
                break;
            case 14:
                scaleX = 75.59;
                scale = "500m";
                break;
            case 15:
                scaleX = 94.5;
                scale = "300m";
                break;
            case 16:
                scaleX = 56.7;
                scale = "100m";
                break;
            case 17:
                scaleX = 56.7;
                scale = "50m";
                break;
            case 18:
                scaleX = 56.7;
                scale = "30m";
                break;
            case 19:
                scaleX = 56.7;
                scale = "15m";
                break;

            default:
                break;
        }

        Scale = scale;
        ScalePoints = new PointCollection()
            {
                new Point(0.0, 0.0),
                new Point(0.0, 5.0),
                new Point(scaleX, 5.0),
                new Point(scaleX, 0.0),

            };
        NotifyOfPropertyChange(() => ScalePoints);
    }

    public void SearchObjectPosition()
    {
        try
        {
            if (MainMap.Markers == null || !(MainMap.Markers.Count() > 0))
                return;

            MainMap.Position = (MainMap.Markers.FirstOrDefault() ?? throw new NullReferenceException($"GMap의 Markers Collection에 인스턴스가 하나도 없습니다.")).Position;
        }
        catch (NullReferenceException ex)
        {
            _log?.Error(ex.Message); 
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }

    public void SetHomePosition()
    {
        if (HomePosition == null) return;

        HomePosition.Position = MainMap.Position;
        HomePosition.Zoom = Zoom;
        HomePosition.IsAvailable = true;
        _log?.Info($"The home position is set to (Position: {HomePosition.Position}, Zoom: {HomePosition.Zoom}).");
        //await _dbService.AddHomePositionAsync(HomePosition);
    }

    public void GoToHomePosition()
    {
        if (HomePosition == null) return;

        MainMap.Position = HomePosition.Position;
        MainMap.Zoom = HomePosition.Zoom;
        _log?.Info($"Moved to home position.");
    }

    public void ClearHomePosition()
    {
        if (HomePosition == null) return;
        HomePosition.Position = DEFAULT_LOCATION;
        HomePosition.Zoom = DEFAULT_ZOOM;
        HomePosition.IsAvailable = false;
        _log?.Info($"Home position has been released..");
        //await _dbService.DeleteHomePositionAsync(1);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public double Zoom
    {
        get { return MainMap.Zoom; }
        set
        {
            MainMap.Zoom = value;
            NotifyOfPropertyChange(nameof(Zoom));
        }
    }

    public int ZoomMax
    {
        get { return MainMap.MaxZoom; }
        set
        {
            MainMap.MaxZoom = value;
            NotifyOfPropertyChange(nameof(ZoomMax));
        }
    }

    public int ZoomMin
    {
        get { return MainMap.MinZoom; }
        set
        {
            MainMap.MinZoom = value;
            NotifyOfPropertyChange(nameof(ZoomMin));
        }
    }

    public PointLatLng CurrentPosition
    {
        get { return _currentPosition; }
        set
        {
            _currentPosition = value;
            NotifyOfPropertyChange(nameof(CurrentPosition));
        }
    }

    public string? Scale
    {
        get { return _scale; }
        set
        {
            _scale = value;
            NotifyOfPropertyChange(nameof(Scale));
        }
    }

    public PointLatLng ClickedCurrentPosition { get; set; }

    public HomePositionModel? HomePosition { get; set; }
    public PointCollection? ScalePoints { get; set; }
    public GMapCustomControl MainMap { get; private set; } = new ();
    #endregion
    #region - Attributes -
    private string? _scale;
    private PointLatLng _currentPosition;
    //private PointLatLng start;
    //private PointLatLng end;


    private CancellationTokenSource _cts;
    public const int ZOOM_MAX = 19;
    public const int ZOOM_MIN = 6;
    public const double DEFAULT_ZOOM = 15d;
    public const int SENSOR_COVERAGE = 200;
    public const int MaxTimeDifference = 60 * 5;
    public PointLatLng DEFAULT_LOCATION = new(37.648425, 126.904284);
    #endregion
}