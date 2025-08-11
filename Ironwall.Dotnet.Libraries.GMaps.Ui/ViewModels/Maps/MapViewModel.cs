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
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Libraries.Enums;
using GMap.NET.MapProviders.Custom;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System.Windows.Controls;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using CoordinateSharp;

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
    public MapViewModel(ILogService log
                        , IEventAggregator eventAggregator
                        //CustomPathProvider pathProvider,
                        //SoundPlayerService soundPlayer,
                        //EventProvider eventProvider,
                        , GMapSetupModel setupModel
                        , MapProvider mapProvider
                        , DefinedMapProvider definedMapProvider
                        , Providers.CustomMapProvider customMapProvider
                        , CustomMapService customMapService
                        , ImageOverlayService imageOverlayService
                        ) : base(eventAggregator, log)
    {
        _cts = new CancellationTokenSource();
        _mapProvider = mapProvider;
        _definedMapProvider = definedMapProvider;
        _customMapProvider = customMapProvider;
        _setupModel = setupModel;
        _customMapService = customMapService;
        _imageOverlayService = imageOverlayService;

        InitializeCommands();

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
            SyncRotationProperties(); // 회전 속성 동기화
        }
    }

    /// <summary>
    /// 회전 관련 속성들을 MainMap과 동기화하는 메서드 수정
    /// DependencyProperty는 자동으로 바인딩되므로 PropertyChanged 이벤트 불필요
    /// </summary>
    private void SyncRotationProperties()
    {
        if (MainMap != null)
        {
            // ✅ 단방향 초기값 설정만 수행
            UpdateRotationPropertiesFromMainMap();

            _log?.Info("회전 속성 초기화 완료");
        }
    }

    /// <summary>
    /// MainMap에서 현재 회전 상태를 읽어와서 ViewModel 속성 초기화
    /// </summary>
    private void UpdateRotationPropertiesFromMainMap()
    {
        if (MainMap == null) return;

        // DependencyProperty 값을 직접 읽어서 ViewModel 초기화
        _currentRotation = MainMap.MapRotation;
        _mapRotation = MainMap.MapRotation;
        _rotationSnapAngle = MainMap.RotationSnapAngle;
        _showRotationControl = MainMap.ShowRotationControl;

        // UI 업데이트 알림
        NotifyOfPropertyChange(nameof(CurrentRotation));
        NotifyOfPropertyChange(nameof(MapRotation));
        NotifyOfPropertyChange(nameof(RotationSnapAngle));
        NotifyOfPropertyChange(nameof(ShowRotationControl));
        NotifyOfPropertyChange(nameof(IsRotated));
    }


    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {

        try
        {
            await base.OnActivateAsync(cancellationToken);

            // 1. 저장된 커스텀 맵들 로드
            await _customMapService.LoadCustomMapsAsync();

            // 2. 지도 설정
            await MapConfigureAsync();
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
        
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        try
        {
            // 모든 커스텀 맵 비활성화
            _customMapService.DeactivateAllCustomMaps();

            await base.OnDeactivateAsync(close, cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error($"MapViewModel 비활성화 실패: {ex.Message}");
        }
    }
    #endregion
    #region - Binding Methods -
    private void InitializeCommands()
    {
        // File 메뉴 Commands
        // Custom Map으로 활용할 Tif 이미지 불러오기
        LoadMapImageCommand = new RelayCommand(ExecuteLoadMapImage, CanExecuteLoadImageMap);
        // Custom Map으로 선택 이미지 Tile 생성 Commands
        CreateCustomMapCommand = new RelayCommand(ExecuteCreateCustomMap, CanExecuteCreateCustomMap);
        // Custom Map의 Tile Folder 설정
        SetMapTileFolderCommand = new RelayCommand(ExecuteSetMapTileFolder, CanExecuteSetMapTileFolder);
        // 프로그램 종료 메뉴
        ExitApplicationCommand = new RelayCommand(ExecuteExitApplication, CanExecuteExitApplication);
        // Map 메뉴 Commands
        // WGS-84 좌표계 
        ToggleWGS84Command = new RelayCommand(ExecuteToggleWGS84Command, CanExecuteToggleWGS84Command);
        // MGRS 좌표계 
        ToggleMGRSCommand = new RelayCommand(ExecuteToggleMGRSCommand, CanExecuteToggleMGRSCommand);
        // UTM 좌표계 
        ToggleUTMCommand = new RelayCommand(ExecuteToggleUTMCommand, CanExecuteToggleUTMCommand);

        
        
        
        
        
        //아이콘 버튼 커멘드
        // 홈 위치 이동 버튼
        MoveHomeLocationCommand = new RelayCommand(ExecuteMoveHomeLocation, CanExecuteMoveHomeLocation);
        // 홈 위치 설정 버튼
        SetHomeLocationCommand = new RelayCommand(ExecuteSetHomeLocation, CanExecuteSetHomeLocation);

        EditSelectedItemsCommand = new RelayCommand(ExecuteEditSelectedItems, CanExecuteEditSelectedItems);

        ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, CanExecuteClearSelection);

        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected, CanExecuteDeleteSelected);

        // 명령 초기화
        RotateCommand = new RelayCommand(ExecuteRotate, CanExecuteRotate);

        FineRotateCommand = new RelayCommand(ExecuteFineRotate, CanExecuteFineRotate);

        ResetRotationCommand = new RelayCommand(ExecuteResetRotation, CanExecuteResetRotation);
    }

    private bool CanExecuteResetRotation(object arg) => true;
    private void ExecuteResetRotation(object obj)
    {
        try
        {
            MainMap.ResetRotation();
            _log?.Info("지도 회전 초기화");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 초기화 실패: {ex.Message}");
        }
    }

    private bool CanExecuteFineRotate(object arg) => true;
    private void ExecuteFineRotate(object obj)
    {
        try
        {
            if (obj is string deltaStr && double.TryParse(deltaStr, out double delta))
            {
                MainMap.RotateMap(delta);
                _log?.Info($"지도 미세 회전: {delta:+0.0;-0.0}도");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 미세 회전 실패: {ex.Message}");
        }
    }

    private bool CanExecuteRotate(object arg) => true;
    private void ExecuteRotate(object obj)
    {
        try
        {
            if (obj is string angleStr && double.TryParse(angleStr, out double angle))
            {
                MainMap.SetMapRotation(angle);
                _log?.Info($"지도 회전: {angle}도");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 실패: {ex.Message}");
        }
    }

    private bool CanExecuteLoadImageMap(object arg) => true;
    private async void ExecuteLoadMapImage(object obj)
    {
        try
        {
            _log?.Info("커스텀 맵 불러오기 시작");

            // 파일 다이얼로그 열기
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "TIF 파일 선택",
                Filter = "TIF Files (*.tif;*.tiff)|*.tif;*.tiff|All Files (*.*)|*.*",
                DefaultExt = ".tif",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                var mapName = System.IO.Path.GetFileNameWithoutExtension(filePath);

                // 파일 확장자에 따라 적절한 메서드 호출
                GMapCustomImage image = null;
                var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                var currentPosition = ClickedCurrentPosition.IsEmpty ? MainMap.CenterPosition : ClickedCurrentPosition;
                if (extension == ".tif" || extension == ".tiff")
                {
                    image = await _imageOverlayService.CreateTifOverlayAsync(
                        filePath,
                        currentPosition,
                        MainMap,
                        mapName);
                }
                else
                {
                    image = await _imageOverlayService.CreateImageOverlayAsync(
                        filePath,
                        currentPosition,
                        MainMap,
                        mapName);
                }


                if (image != null)
                {
                    // 이미지가 표시되도록 Visibility 확인
                    image.Visibility = true;

                    // GMapCustomControl에 이미지 추가
                    MainMap.AddImageOverlay(image);

                    // 뷰 갱신 강제
                    MainMap.InvalidateVisual();

                    _log?.Info($"이미지 오버레이 추가 완료: {mapName}");
                }

            }
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 불러오기 실패: {ex.Message}");
            //await ShowErrorMessageAsync("커스텀 맵 불러오기 실패", ex.Message);
        }
    }

    private bool CanExecuteCreateCustomMap(object arg) => SelectedImage != null;
    private async void ExecuteCreateCustomMap(object obj)
    {
        try
        {
            _log?.Info("커스텀 맵 생성하기 시작");

            // 1단계: 선택된 이미지 확인
            if (SelectedImage == null)
            {
                _log?.Warning("커스텀 지도로 변환할 이미지가 선택되지 않았습니다.");
                // TODO: 사용자에게 알림 표시
                return;
            }

            _log?.Info($"선택된 이미지: {SelectedImage.Title}");

            // 2단계: 이미지 파일 경로 확인
            if (SelectedImage.Img == null)
            {
                _log?.Error("선택된 이미지의 소스 파일을 찾을 수 없습니다.");
                return;
            }

            // 이미지 파일 경로 추출 (실제 구현에 따라 조정 필요)
            var imageFilePath = SelectedImage.FilePath;
            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
            {
                _log?.Error($"이미지 파일을 찾을 수 없습니다: {imageFilePath}");
                return;
            }

            // 3단계: 현재 이미지 경계에서 GIS 좌표 추출
            var imageBounds = SelectedImage.ImageBounds;
            var geoOptions = CreateGeoOptionsFromImageBounds(imageBounds, SelectedImage.Title);

            _log?.Info($"지리참조 좌표:");
            _log?.Info($"  - 좌상단: ({geoOptions.ManualMinLongitude:F6}, {geoOptions.ManualMaxLatitude:F6})");
            _log?.Info($"  - 우하단: ({geoOptions.ManualMaxLongitude:F6}, {geoOptions.ManualMinLatitude:F6})");
            geoOptions.MaxZoom = 19;
            // 4단계: 사용자 확인
            var userConfirmed = await ShowCustomMapConfirmationAsync(SelectedImage, geoOptions);
            if (!userConfirmed)
            {
                _log?.Info("사용자가 커스텀 지도 생성을 취소했습니다.");
                return;
            }

            // 5단계: 진행률 모니터링 설정
            var progress = CreateProgressReporter();

            // 6단계: 실제 커스텀 지도 변환 실행
            _log?.Info("이미지를 커스텀 지도로 변환 중...");
            var startTime = DateTime.Now;

            var customMap = await _customMapService.ProcessTifFileAsync(
                imageFilePath,
                $"{SelectedImage.Title}_CustomMap",
                geoOptions,
                progress);

            var elapsedTime = DateTime.Now - startTime;

            // 7단계: 변환 완료 후 처리
            _log?.Info($"커스텀 지도 생성 완료!");
            _log?.Info($"소요 시간: {elapsedTime.TotalMinutes:F1}분");
            _log?.Info($"생성된 타일: {customMap.TotalTileCount:N0}개");
            _log?.Info($"타일 크기: {customMap.TilesDirectorySize / (1024 * 1024):N0} MB");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 생성 실패: {ex.Message}");
            // TODO: 사용자에게 에러 알림 표시
        }
    }

    private bool CanExecuteSetMapTileFolder(object arg) => true;
    private void ExecuteSetMapTileFolder(object obj)
    {
        SelectTileDirectory();
    }

    private bool CanExecuteExitApplication(object arg) => true;
    private void ExecuteExitApplication(object obj)
    {
    }

    private bool CanExecuteToggleWGS84Command(object arg) => true;
    private void ExecuteToggleWGS84Command(object obj)
    {
        IsShowWSG84 = IsShowWSG84;
    }

    private bool CanExecuteToggleMGRSCommand(object arg) => true;
    private void ExecuteToggleMGRSCommand(object obj)
    {
        IsShowMGRS = IsShowMGRS;
        IsShowMGRSGrid = IsShowMGRS;
    }

    private bool CanExecuteToggleUTMCommand(object arg) => true;

    private void ExecuteToggleUTMCommand(object obj)
    {
        IsShowUTM = IsShowUTM;
    }

    private bool CanExecuteMoveHomeLocation(object arg) => HomePosition != null;
    private void ExecuteMoveHomeLocation(object obj)
    {
        GoToHomePosition();
    }

    private bool CanExecuteSetHomeLocation(object arg) => true;
    private void ExecuteSetHomeLocation(object obj)
    {
        SetHomePosition();
    }

    private bool CanExecuteEditSelectedItems(object arg) => IsEditModeEnabled && (MainMap.CustomImages.Any() || MainMap.CustomMarkers.Any());
    private void ExecuteEditSelectedItems(object obj)
    {
        try
        {
            // 현재 마우스 위치의 객체 찾기
            var currentPosition = ClickedCurrentPosition.IsEmpty ? MainMap.CenterPosition : ClickedCurrentPosition;

            _log?.Info($"객체 검색 시작: 좌표({currentPosition.Lat:F6}, {currentPosition.Lng:F6})");
            _log?.Info($"검색 대상 이미지 수: {MainMap.CustomImages.Count}");
            _log?.Info($"검색 대상 마커 수: {MainMap.CustomMarkers.Count}");

            var clickedImages = MainMap.GetImageOverlaysAt(currentPosition);
            var clickedMarkers = MainMap.CustomMarkers
                .Where(m => IsNearPosition(m.Position, ClickedCurrentPosition, 0.0001))
                .ToList();

            _log?.Info($"찾은 이미지 수: {clickedImages.Count}, 찾은 마커 수: {clickedMarkers.Count}");

            // 이미지 우선 선택
            if (clickedImages.Any())
            {
                if (!(obj is GMapCustomImage image)) return;
                
                var selectedImage = clickedImages.Where(entity => entity.Id == image.Id).FirstOrDefault();
                if (selectedImage == null) return;
                _log?.Info($"이미지 선택: {selectedImage.Title}");
                SelectAndEditImage(selectedImage);
            }
            // 마커 선택
            else if (clickedMarkers.Any())
            {
                var selectedMarker = clickedMarkers.First();
                _log?.Info($"마커 선택: {selectedMarker.Title}");
                SelectAndEditMarker(selectedMarker);
            }
            else
            {
                _log?.Info("선택 위치에 편집 가능한 객체가 없습니다.");
            }

            // ViewArea 정보 추가
            var viewArea = MainMap.ViewArea;
            _log?.Info($"현재 ViewArea: ({viewArea.Left:F6}, {viewArea.Bottom:F6}) to ({viewArea.Right:F6}, {viewArea.Top:F6})");


            // 각 이미지의 경계 정보 출력
            //foreach (var img in MainMap.CustomImages)
            //{
            //    _log?.Info($"이미지 '{img.Title}': Visibility={img.Visibility}");
            //    _log?.Info($"  - 표시용 ImageBounds: ({img.ImageBounds.Left:F6}, {img.ImageBounds.Bottom:F6}) to ({img.ImageBounds.Right:F6}, {img.ImageBounds.Top:F6})");

            //    // 내부 _imageBounds 직접 확인 (리플렉션 사용)
            //    var field = img.GetType().GetField("_imageBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            //    if (field != null)
            //    {
            //        var internalBounds = (RectLatLng)field.GetValue(img);
            //        _log?.Info($"  - 내부 _imageBounds: ({internalBounds.Left:F6}, {internalBounds.Bottom:F6}) to ({internalBounds.Right:F6}, {internalBounds.Top:F6})");
            //    }

            //    var contains = img.Contains(currentPosition);
            //    _log?.Info($"  - Contains 결과: {contains}");
            //}
        }
        catch (Exception ex)
        {
            _log?.Error($"편집 모드 활성화 실패: {ex.Message}");
        }
    }

    private void SelectAndEditImage(GMapCustomImage image)
    {
        try
        {
            // 이전 선택 해제
            ClearCurrentSelection();

            // 새 이미지 선택
            SelectedImage = image;
            image.IsSelected = true;
            // Adorner 모드 활성화
            var adornerWrapper = MainMap.EnableAdornerMode(image);

            if (adornerWrapper != null)
            {
                adornerWrapper.IsSelected = true;
                _log?.Info($"이미지 편집 모드 활성화: {image.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 선택 실패: {ex.Message}");
        }
    }

    private void SelectAndEditMarker(GMapCustomMarker marker)
    {
        try
        {
            // 이전 선택 해제
            ClearCurrentSelection();

            // 새 마커 선택
            SelectedMarker = marker;
            marker.IsSelected = true;

            // Adorner 모드 활성화
            var adornerWrapper = MainMap.EnableAdornerMode(marker);

            if (adornerWrapper != null)
            {
                adornerWrapper.IsSelected = true;
                _log?.Info($"마커 편집 모드 활성화: {marker.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 선택 실패: {ex.Message}");
        }
    }

    private bool CanExecuteClearSelection(object arg) => HasSelectedItem;
    private void ExecuteClearSelection(object obj)
    {
        ClearCurrentSelection();
    }

    private void ClearCurrentSelection()
    {
        try
        {
            // 모든 이미지 선택 해제
            foreach (var img in MainMap.CustomImages)
            {
                img.IsSelected = false;
            }

            // 모든 마커 선택 해제  
            foreach (var marker in MainMap.CustomMarkers)
            {
                marker.IsSelected = false;
            }

            // 경계선 표시 해제
            MainMap.ShowImageBounds = false;
            MainMap.InvalidateVisual();

            // 기존 SelectedImage, SelectedMarker 초기화
            SelectedImage = null;
            SelectedMarker = null;

            _log?.Info("모든 선택 해제 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 해제 실패: {ex.Message}");
        }
    }

    private bool CanExecuteDeleteSelected(object arg) => HasSelectedItem && IsEditModeEnabled;
    private void ExecuteDeleteSelected(object obj)
    {
        try
        {
            if (SelectedImage != null)
            {
                MainMap.RemoveImageOverlay(SelectedImage);
                SelectedImage = null;
                _log?.Info("선택된 이미지 삭제 완료");
            }

            if (SelectedMarker != null)
            {
                MainMap.Markers.Remove(SelectedMarker);
                SelectedMarker = null;
                _log?.Info("선택된 마커 삭제 완료");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 항목 삭제 실패: {ex.Message}");
        }
    }



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
    /// <summary>
    /// 비동기 지도 설정
    /// </summary>
    private async Task MapConfigureAsync()
    {
        try
        {
            if (_mapProvider.Any())
            {
                var mapName = _setupModel.MapName ?? throw new NullReferenceException("MapName was not found.");

                SelectedMap = _mapProvider.Where(entity => entity.Name == mapName).FirstOrDefault();
            }

            if (SelectedMap == null) return;

            if (SelectedMap is DefinedMapModel definedMap)
            {
                await ConfigureDefinedMapAsync(definedMap);
            }
            else if (SelectedMap is CustomMapModel customMap)
            {
                await ConfigureCustomMapAsync(customMap);
            }

            // 공통 지도 설정
            ConfigureCommonMapSettings();


            _log?.Info($"지도 설정 완료: {SelectedMap.Name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 설정 실패: {ex.Message}");
            throw;
        }
    }

    
    /// <summary>
    /// 기존 제공자 지도 설정
    /// </summary>
    private async Task ConfigureDefinedMapAsync(DefinedMapModel definedMap)
    {
        try
        {
            switch (definedMap.Vendor)
            {
                case EnumMapVendor.Google:
                    GoogleMapProvider.Instance.ApiKey = definedMap.ApiKey;
                    ConfigureGoogleMap(definedMap.Style);
                    break;
                case EnumMapVendor.Microsoft:
                    BingMapProvider.Instance.ClientKey = definedMap.ApiKey;
                    ConfigureBingMap(definedMap.Style);
                    break;
                case EnumMapVendor.OpenStreetMap:
                    OpenStreetMapProvider.Instance.YoursClientName = definedMap.ApiKey;
                    MainMap.MapProvider = GMapProviders.OpenStreetMap;
                    break;
                default:
                    MainMap.MapProvider = GMapProviders.OpenStreetMap;
                    break;
            }

            // 온라인 지도 모드 설정
            if (MainMap.Manager.Mode == AccessMode.CacheOnly)
                await GetMapDataAsync();
            else
                MainMap.Manager.Mode = AccessMode.ServerAndCache;
            
            _log?.Info($"기존 제공자 지도 설정 완료: {definedMap.Vendor} {definedMap.Style}");
        }
        catch (Exception ex)
        {
            _log?.Error($"기존 제공자 지도 설정 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 지도 설정
    /// </summary>
    private async Task ConfigureCustomMapAsync(CustomMapModel customMap)
    {
        try
        {
            _log?.Info($"커스텀 지도 설정 시작: {customMap.Name}");

            // 1. 커스텀 맵 활성화
            var customProvider = _customMapService.ActivateCustomMap(customMap);

            // 2. GMap에 Provider 설정
            MainMap.MapProvider = customProvider;

            // 3. 서버 전용 모드로 설정
            MainMap.Manager.Mode = AccessMode.ServerOnly;

            // 4. 경계 영역이 있으면 해당 영역으로 이동
            if (customProvider.GeographicBounds.HasValue)
            {
                var bounds = customProvider.GeographicBounds.Value;
                var centerLat = bounds.Lat - bounds.HeightLat / 2;
                var centerLng = bounds.Lng + bounds.WidthLng / 2;
                MainMap.Position = new PointLatLng(centerLat, centerLng);

                _log?.Info($"커스텀 지도 중심점 설정: {centerLat:F6}, {centerLng:F6}");
            }

            CurrentCustomMapProvider = customProvider;
            _log?.Info($"커스텀 지도 설정 완료: {customMap.Name}, 타일 수: {customMap.TotalTileCount}");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 지도 설정 실패: {customMap.Name}, 오류: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 공통 지도 설정
    /// </summary>
    private void ConfigureCommonMapSettings()
    {
        MainMap.Position = _setupModel.HomePosition?.PointLatLng ?? new PointLatLng(37.648425, 126.904284);
        MainMap.MinZoom = SelectedMap.MinZoomLevel;
        MainMap.MaxZoom = SelectedMap.MaxZoomLevel;
        MainMap.Zoom = _setupModel.HomePosition?.Zoom ?? DEFAULT_ZOOM;

        MainMap.ShowCenter = false;
        MainMap.MultiTouchEnabled = false;

        // 이벤트 핸들러 등록
        MainMap.OnPositionChanged += MainMap_OnCurrentPositionChanged;
        MainMap.MouseMove += MainMap_MouseMove;
        MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
        MainMap.OnMapZoomChanged += MainMap_OnMapZoomChanged;

        MainMap.ShowCenter = true;
        MainMap_OnMapZoomChanged();

        SetInitialHomePosition();
    }

    private void ConfigureGoogleMap(EnumMapStyle style)
    {
        MainMap.MapProvider = style switch
        {
            EnumMapStyle.Normal => GMapProviders.GoogleMap,
            EnumMapStyle.Satellite => GMapProviders.GoogleSatelliteMap,
            EnumMapStyle.Hybrid => GMapProviders.GoogleHybridMap,
            EnumMapStyle.Terrain => GMapProviders.GoogleTerrainMap,
            _ => GMapProviders.GoogleMap
        };
    }

    private void ConfigureBingMap(EnumMapStyle style)
    {
        MainMap.MapProvider = style switch
        {
            EnumMapStyle.Normal => GMapProviders.BingMap,
            EnumMapStyle.Satellite => GMapProviders.BingSatelliteMap,
            EnumMapStyle.Hybrid => GMapProviders.BingHybridMap,
            _ => GMapProviders.BingMap
        };
    }


    /// <summary>
    /// 지도 변경 (런타임에서)
    /// </summary>
    public async Task SwitchToMapAsync(IMapModel targetMap)
    {
        try
        {
            if (targetMap == null || targetMap.Id == SelectedMap?.Id)
                return;

            _log?.Info($"지도 변경: {SelectedMap?.Name} -> {targetMap.Name}");

            // 이전 커스텀 맵 비활성화
            if (SelectedMap is CustomMapModel && CurrentCustomMapProvider != null)
            {
                _customMapService.DeactivateCustomMap(SelectedMap.Id);
                CurrentCustomMapProvider = null;
            }

            SelectedMap = targetMap;
            // 이름 기반 검색
            SelectedMap = _mapProvider.Where(entity => entity.Name == targetMap.Name)
                                    .Where(entity => entity.Id == targetMap.Id).FirstOrDefault() ?? throw new NullReferenceException("There is no map you choose.");

            _setupModel.MapName = SelectedMap.Name;
            _setupModel.MapType = SelectedMap.ProviderType.ToString();
            switch (SelectedMap.ProviderType)
            {
                case EnumMapProvider.Defined:
                    _setupModel.MapMode = "ServerAndCache";
                    break;
                case EnumMapProvider.Custom:
                    _setupModel.MapMode = "ServerAndCache";
                    break;
                default:
                    break;
            }

            await MapConfigureAsync();

            _log?.Info($"지도 변경 완료: {targetMap.Name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 변경 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 맵으로 직접 변경
    /// </summary>
    public async Task SwitchToCustomMapAsync(CustomMapModel customMap)
    {
        try
        {
            await SwitchToMapAsync(customMap);
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 변경 실패: {ex.Message}");
            throw;
        }
    }

    private void SetInitialHomePosition()
    {
        HomePosition = new HomePositionModel();
        if (_setupModel.HomePosition == null || _setupModel.HomePosition.Position == null) return;
        var position = _setupModel.HomePosition.Position;
        HomePosition.Position = position;
        HomePosition.Zoom = _setupModel.HomePosition.Zoom;
        HomePosition.IsAvailable = true;
        ClickedCurrentPosition = new PointLatLng(position.Latitude, position.Longitude);
        

        _log?.Info($"HomePosition정보가 (Lat:{HomePosition.Position.Latitude}, Lng:{HomePosition.Position.Longitude}, Alt:{HomePosition.Position.Altitude}, Zoom:{HomePosition.Zoom})으로 설정되었습니다.");
    }

    public Task<bool> GetMapDataAsync()
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
                _log?.Error($"Rasied Exception in {nameof(GetMapDataAsync)} :  {ex.Message}");
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

        var current = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);
        CurrentCoordinatePosition = new CoordinateModel(current.Lat, current.Lng, 0.0);
        // 위경도 → MGRS 변환
        var coordinate = new Coordinate(current.Lat, current.Lng);
        CurrentMGRS = coordinate.MGRS.ToString(); // "52S CG 13084 42135"
        CurrentUTM = coordinate.UTM.ToString();
    }


    private void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(MainMap);
        ClickedCurrentPosition = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);

        // 디버깅 로그 추가
        _log?.Info($"마우스 클릭: 화면좌표({p.X:F2}, {p.Y:F2}) -> 지리좌표({ClickedCurrentPosition.Lat:F6}, {ClickedCurrentPosition.Lng:F6})");

        List<GMapCustomImage> clickedImages = MainMap.GetImageOverlaysAt(ClickedCurrentPosition);
        List<GMapCustomMarker> clickedMarkers = MainMap.CustomMarkers
            .Where(m => IsNearPosition(m.Position, ClickedCurrentPosition, 0.0001))
            .ToList();

        _log?.Info($"찾은 이미지 수: {clickedImages.Count}, 찾은 마커 수: {clickedMarkers.Count}");

        // 편집 모드일 때 자동으로 객체 선택
        if (IsEditModeEnabled)
        {
            _log?.Info($"편집 모드 활성화됨. 이미지 개수: {MainMap.CustomImages.Count}");

            // 이미지 우선 선택
            if (clickedImages.Any())
            {
                var selectedImage = clickedImages.First();
                _log?.Info($"이미지 선택: {selectedImage.Title}");
                SelectAndEditImage(selectedImage);
            }
            // 마커 선택
            else if (clickedMarkers.Any())
            {
                var selectedMarker = clickedMarkers.First();
                _log?.Info($"마커 선택: {selectedMarker.Title}");
                SelectAndEditMarker(selectedMarker);
            }
            else
            {
                _log?.Info("선택 위치에 편집 가능한 객체가 없습니다.");
            }
        }
        else
        {
            _log?.Info("편집 모드가 비활성화 상태입니다.");
        }
    }


    private void MainMap_OnMapZoomChanged()
    {
        CreateScaleBar();
    }

    private void CreateScaleBar()
    {
        (var scaleX, var scale) = ScaleHelper.RelativeCreateScalebar(Zoom);
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

    public async void SetHomePosition()
    {
        if (HomePosition == null) return;

        HomePosition.Position = new CoordinateModel(latitude: ClickedCurrentPosition.Lat, longitude: ClickedCurrentPosition.Lng, altitude:0);
        HomePosition.Zoom = Zoom;
        HomePosition.IsAvailable = true;
        _log?.Info($"The home position is set to (Position: ({HomePosition.Position.Latitude}, {HomePosition.Position.Longitude}), Zoom: {HomePosition.Zoom}).");
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);

    }

    public void GoToHomePosition()
    {
        if (HomePosition == null || HomePosition.Position == null) return;

        MainMap.Position = new PointLatLng(HomePosition.Position.Latitude, HomePosition.Position.Longitude);
        MainMap.Zoom = HomePosition.Zoom;
        _log?.Info($"Moved to home position.");

        
    }

    

    public async void ClearHomePosition()
    {
        if (HomePosition == null || HomePosition.Position == null) return;

        MainMap.Position = new PointLatLng(HomePosition.Position.Latitude, HomePosition.Position.Longitude);
        HomePosition.Zoom = DEFAULT_ZOOM;
        HomePosition.IsAvailable = false;
        _log?.Info($"Home position has been released..");

        // JSON에 저장
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition);

        
    }

    /// <summary>
    /// 현재 지도 설정을 JSON에 저장
    /// </summary>
    public async Task SaveCurrentMapSettingsAsync()
    {
        try
        {
            await MapSettingsHelper.SaveMapSettingsAsync(_setupModel);

            _log?.Info("현재 지도 설정 저장 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 설정 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 타일 저장 폴더 선택 및 설정
    /// </summary>
    public async void SelectTileDirectory()
    {
        try
        {
            // 폴더 선택 대화상자
            var folderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "타일 저장 폴더 선택",
                InitialDirectory = _setupModel.TileDirectory ?? "C:\\Tiles"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var selectedPath = folderDialog.FolderName;

                // JSON에 저장
                await MapSettingsHelper.SaveTileDirectoryAsync(selectedPath, _log);
            }
            else
            {
                _log?.Info("타일 폴더 선택이 취소되었습니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"타일 폴더 선택 실패: {ex.Message}");
            // MessageBox.Show($"폴더 선택 중 오류가 발생했습니다.\n{ex.Message}", "오류");
        }
    }
    #endregion
    #region - Helper Methods -

    private bool IsNearPosition(PointLatLng pos1, PointLatLng pos2, double tolerance)
    {
        var latDiff = Math.Abs(pos1.Lat - pos2.Lat);
        var lngDiff = Math.Abs(pos1.Lng - pos2.Lng);
        return latDiff <= tolerance && lngDiff <= tolerance;
    }

    /// <summary>
    /// 이미지 경계에서 지리참조 옵션 생성
    /// </summary>
    private TifProcessingOptions CreateGeoOptionsFromImageBounds(RectLatLng bounds, string mapName)
    {
        return new TifProcessingOptions
        {
            UseManualCoordinates = true,

            // 📌 이미지 경계의 4개 모서리 좌표 사용
            ManualMinLatitude = bounds.LocationRightBottom.Lat,  // 남쪽 (하단)
            ManualMaxLatitude = bounds.LocationTopLeft.Lat,      // 북쪽 (상단)
            ManualMinLongitude = bounds.LocationTopLeft.Lng,     // 서쪽 (좌측)
            ManualMaxLongitude = bounds.LocationRightBottom.Lng, // 동쪽 (우측)

            MinZoom = 10,  // 적절한 최소 줌 레벨
            MaxZoom = 19,  // 적절한 최대 줌 레벨
            TileSize = 256 // 표준 타일 크기
        };
    }

   
    /// <summary>
    /// 커스텀 지도 생성 확인 대화상자
    /// </summary>
    private async Task<bool> ShowCustomMapConfirmationAsync(GMapCustomImage image, TifProcessingOptions options)
    {
        // TODO: 실제 UI 확인 대화상자 구현
        // 예시 정보를 로그로 표시
        _log?.Info("=== 커스텀 지도 생성 정보 ===");
        _log?.Info($"이미지: {image.Title}");
        _log?.Info($"좌표 범위:");
        _log?.Info($"  위도: {options.ManualMinLatitude:F6} ~ {options.ManualMaxLatitude:F6}");
        _log?.Info($"  경도: {options.ManualMinLongitude:F6} ~ {options.ManualMaxLongitude:F6}");
        _log?.Info($"줌 레벨: {options.MinZoom} ~ {options.MaxZoom}");

        // TODO: 실제 UI 구현 시 사용자 확인 받기
        // 지금은 자동으로 true 반환
        await Task.Delay(100); // UI 대화상자 시뮬레이션
        return true;

        // 실제 구현 예시:
        // var result = MessageBox.Show(
        //     $"'{image.Title}'를 커스텀 지도로 변환하시겠습니까?\n\n" +
        //     $"좌표 범위: ({options.ManualMinLatitude:F6}, {options.ManualMinLongitude:F6}) ~ " +
        //     $"({options.ManualMaxLatitude:F6}, {options.ManualMaxLongitude:F6})",
        //     "커스텀 지도 생성 확인",
        //     MessageBoxButton.YesNo,
        //     MessageBoxImage.Question);
        // return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// 진행률 리포터 생성
    /// </summary>
    private IProgress<TileConversionProgress> CreateProgressReporter()
    {
        return new Progress<TileConversionProgress>(progress =>
        {
            // 10% 단위로 로그 출력
            if (progress.ProgressPercentage % 10 < 0.1)
            {
                _log?.Info($"타일 생성 진행률: {progress.ProgressPercentage:F1}% " +
                          $"({progress.ProcessedTiles:N0}/{progress.TotalTiles:N0}) - {progress.Status}");
            }

            // TODO: UI 진행률 표시 (ProgressBar 등)
            // Application.Current?.Dispatcher?.Invoke(() => {
            //     // progressBar.Value = progress.ProgressPercentage;
            //     // statusText.Text = progress.Status;
            // });
        });
    }

    /// <summary>
    /// 생성된 커스텀 지도 적용
    /// </summary>
    private async Task ApplyGeneratedCustomMap(CustomMapModel customMap, GMapCustomImage originalImage)
    {
        try
        {
            _log?.Info("생성된 커스텀 지도를 지도에 적용 중...");

            // 1. 커스텀 맵 활성화
            var provider = _customMapService.ActivateCustomMap(customMap);

            // 2. 지도 전환
            await SwitchToCustomMapAsync(customMap);

            // 3. 지도 중심을 원본 이미지 위치로 이동
            var bounds = originalImage.ImageBounds;
            var centerLat = (bounds.LocationTopLeft.Lat + bounds.LocationRightBottom.Lat) / 2;
            var centerLng = (bounds.LocationTopLeft.Lng + bounds.LocationRightBottom.Lng) / 2;

            MainMap.Position = new PointLatLng(centerLat, centerLng);
            MainMap.Zoom = 15; // 적절한 줌 레벨

            _log?.Info(" 커스텀 지도 적용 완료!");

            // TODO: 사용자에게 성공 알림
            // ShowSuccessMessage($"'{customMap.Name}' 커스텀 지도가 생성되어 적용되었습니다!");

        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 지도 적용 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 원본 이미지 제거 확인
    /// </summary>
    private async Task<bool> AskRemoveOriginalImageAsync()
    {
        // TODO: 실제 UI 확인 대화상자 구현
        await Task.Delay(100);
        return true; // 기본적으로 제거

        // 실제 구현:
        // var result = MessageBox.Show(
        //     "커스텀 지도가 생성되었습니다.\n원본 이미지 오버레이를 제거하시겠습니까?",
        //     "원본 이미지 제거",
        //     MessageBoxButton.YesNo,
        //     MessageBoxImage.Question);
        // return result == MessageBoxResult.Yes;
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

    public ICoordinateModel CurrentCoordinatePosition
    {
        get { return _currentPosition; }
        set
        {
            _currentPosition = value;
            NotifyOfPropertyChange(nameof(CurrentCoordinatePosition));
        }
    }

    public string CurrentMGRS
    {
        get { return _currentMGRS; }
        set { _currentMGRS = value; NotifyOfPropertyChange(nameof(CurrentMGRS)); }
    }

    public string CurrentUTM
    {
        get { return _currentUTM; }
        set { _currentUTM = value; NotifyOfPropertyChange(nameof(CurrentUTM)); }
    }


    public PointLatLng CurrentPointPosition => new PointLatLng(_currentPosition.Latitude, _currentPosition.Longitude);

    public string? Scale
    {
        get { return _scale; }
        set
        {
            _scale = value;
            NotifyOfPropertyChange(nameof(Scale));
        }
    }

    public bool IsEditModeEnabled
    {
        get => _isEditModeEnabled;
        set
        {
            if (_isEditModeEnabled != value)
            {
                _isEditModeEnabled = value;
                MainMap.SetEditMode(value);
                NotifyOfPropertyChange(nameof(IsEditModeEnabled));
               
                _log?.Info($"편집 모드: {(value ? "활성화" : "비활성화")}");
            }
        }
    }

    public bool IsShowWSG84
    {
        get { return _isShowWSG84; }
        set { _isShowWSG84 = value; NotifyOfPropertyChange(nameof(IsShowWSG84)); }
    }

    public bool IsShowMGRS
    {
        get { return _isShowMGRS; }
        set { _isShowMGRS = value; NotifyOfPropertyChange(nameof(IsShowMGRS)); }
    }

    public bool IsShowMGRSGrid
    {
        get { return _isShowMGRSGrid; }
        set { _isShowMGRSGrid = value; NotifyOfPropertyChange(nameof(IsShowMGRSGrid)); }
    }

    public bool IsShowUTM
    {
        get { return _isShowUTM; }
        set { _isShowUTM = value; NotifyOfPropertyChange(nameof(IsShowUTM)); }
    }


    private GMapCustomImage? _selectedImage;
    public GMapCustomImage? SelectedImage
    {
        get => _selectedImage;
        set
        {
            _selectedImage = value;
            NotifyOfPropertyChange(nameof(SelectedImage));
            NotifyOfPropertyChange(nameof(HasSelectedItem));
        }
    }

    private GMapCustomMarker? _selectedMarker;
    public GMapCustomMarker? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            _selectedMarker = value;
            NotifyOfPropertyChange(nameof(SelectedMarker));
            NotifyOfPropertyChange(nameof(HasSelectedItem));
        }
    }

    public double CurrentRotation
    {
        get => _currentRotation;
        set
        {
            _currentRotation = value;
            NotifyOfPropertyChange(nameof(CurrentRotation));
            NotifyOfPropertyChange(nameof(IsRotated));
        }
    }

    public double MapRotation
    {
        get => _mapRotation;
        set
        {
            if (Math.Abs(_mapRotation - value) > 0.01) // 미세한 변화 무시
            {
                _mapRotation = value;

                // MainMap에 적용
                if (MainMap != null)
                {
                    MainMap.MapRotation = value;
                }

                CurrentRotation = value;
                NotifyOfPropertyChange(nameof(MapRotation));
            }
        }
    }

    public double RotationSnapAngle
    {
        get => _rotationSnapAngle;
        set
        {
            if (Math.Abs(_rotationSnapAngle - value) > 0.01)
            {
                _rotationSnapAngle = value;

                if (MainMap != null)
                {
                    MainMap.RotationSnapAngle = value;
                }

                NotifyOfPropertyChange(nameof(RotationSnapAngle));
            }
        }
    }

    public bool ShowRotationControl
    {
        get => _showRotationControl;
        set
        {
            if (_showRotationControl != value)
            {
                _showRotationControl = value;

                if (MainMap != null)
                {
                    MainMap.ShowRotationControl = value;
                }

                NotifyOfPropertyChange(nameof(ShowRotationControl));
            }
        }
    }

    public bool IsRotated => Math.Abs(CurrentRotation) > 0.1;

    public bool HasSelectedItem => SelectedImage != null || SelectedMarker != null;


    public PointLatLng ClickedCurrentPosition { get; set; }
    public HomePositionModel? HomePosition { get; set; }
    public PointCollection? ScalePoints { get; set; }
    public GMapCustomControl MainMap { get; private set; } = new ();
    public IMapModel SelectedMap { get; private set; }
    public FileBasedCustomMapProvider? CurrentCustomMapProvider { get; private set; }
    public RelayCommand LoadMapImageCommand { get; private set; }
    public RelayCommand CreateCustomMapCommand { get; private set; }
    public RelayCommand SetMapTileFolderCommand { get; private set; }
    public RelayCommand ExitApplicationCommand { get; private set; }
    public RelayCommand ToggleWGS84Command { get; private set; }
    public RelayCommand ToggleMGRSCommand { get; private set; }
    public RelayCommand ToggleUTMCommand { get; private set; }
    public RelayCommand MoveHomeLocationCommand { get; private set; }
    public RelayCommand SetHomeLocationCommand { get; private set; }
    public RelayCommand EditSelectedItemsCommand { get; private set; }
    public RelayCommand ClearSelectionCommand { get; private set; }
    public RelayCommand DeleteSelectedCommand { get; private set; }
    public RelayCommand RotateCommand { get; private set; }
    public RelayCommand FineRotateCommand { get; private set; }
    public RelayCommand ResetRotationCommand { get; private set; }
    public RelayCommand AlignToMGRSCommand { get; private set; }

    #endregion
    #region - Attributes -
    private string? _scale;
    private ICoordinateModel _currentPosition;
    private string _currentMGRS;
    private string _currentUTM;
    private bool _isEditModeEnabled;

    private bool _isShowWSG84 = true;
    private bool _isShowMGRS;
    private bool _isShowMGRSGrid;
    private bool _isShowUTM;


    private double _currentRotation;
    private double _mapRotation;
    private double _rotationSnapAngle;
    private bool _showRotationControl = false;

    private CancellationTokenSource _cts;
    private MapProvider _mapProvider;
    private DefinedMapProvider _definedMapProvider;
    private Providers.CustomMapProvider _customMapProvider;
    private GMapSetupModel _setupModel;
    private CustomMapService _customMapService;
    private ImageOverlayService _imageOverlayService;
    public const int ZOOM_MAX = 19;
    public const int ZOOM_MIN = 6;
    public const double DEFAULT_ZOOM = 15d;
    public const int SENSOR_COVERAGE = 200;
    public const int MaxTimeDifference = 60 * 5;
    public ICoordinateModel DEFAULT_LOCATION = new CoordinateModel(37.648425, 126.904284);
    #endregion
}