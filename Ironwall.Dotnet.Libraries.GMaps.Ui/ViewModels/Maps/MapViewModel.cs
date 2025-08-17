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
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using CoordinateSharp;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Org.BouncyCastle.Crypto.Macs;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : GIS 지도 제어 및 편집 기능을 제공하는 주요 ViewModel 
   Created By   : GHLee                                                
   Created On   : 7/22/2025 2:59:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MapViewModel : BasePanelViewModel
{
    #region - 상수 정의 -
    public const int ZOOM_MAX = 19;
    public const int ZOOM_MIN = 6;
    public const double DEFAULT_ZOOM = 15d;
    public const int SENSOR_COVERAGE = 200;
    public const int MaxTimeDifference = 60 * 5;
    public ICoordinateModel DEFAULT_LOCATION = new CoordinateModel(37.648425, 126.904284);
    #endregion

    #region - 생성자 -
    /// <summary>
    /// MapViewModel 생성자 - 의존성 주입을 통한 초기화
    /// </summary>
    public MapViewModel(ILogService log
                        , IEventAggregator eventAggregator
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

    #region - 라이프사이클 오버라이드 -
    /// <summary>
    /// 뷰가 연결될 때 MainMap 컨트롤 설정 및 회전 속성 동기화
    /// </summary>
    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);
        if (view is MapView mapView)
        {
            MainMap = mapView.MainMap;
            // Adorner 시스템 통합
            SetupAdornerIntegration();

            // 회전 속성 동기화
            SyncRotationProperties();

            _log?.Info("MapViewModel과 뷰 연결 완료");
        }
    }

    /// <summary>
    /// ViewModel 활성화 시 비동기 초기화 작업 수행
    /// </summary>
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

    /// <summary>
    /// ViewModel 비활성화 시 리소스 정리
    /// </summary>
    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        try
        {
            // Adorner 시스템 정리
            CleanupAdornerIntegration();

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
    #region - Adorner 시스템 통합 -
    /// <summary>
    /// Adorner 시스템 통합 설정
    /// </summary>
    private void SetupAdornerIntegration()
    {
        if (MainMap?.AdornerManager == null)
        {
            _log?.Error("MainMap 또는 AdornerManager가 null입니다!");
            return;
        }


        try
        {
            _log?.Info("Adorner 시스템 통합 시작");

            // GMapCustomControl 이벤트 구독
            MainMap.OnMarkerClicked += OnMapMarkerClicked;
            MainMap.OnImageClicked += OnMapImageClicked;
            MainMap.OnMapClicked += OnMapClicked;
            _log?.Info("GMapCustomControl 이벤트 구독 완료");

            // AdornerManager 이벤트 구독
            MainMap.MarkerEditStarted += OnMarkerEditStarted;
            MainMap.MarkerEditCompleted += OnMarkerEditCompleted;
            MainMap.MarkerEditCancelled += OnMarkerEditCancelled;
            MainMap.AdornerCreated += OnAdornerCreated;
            MainMap.AdornerRemoved += OnAdornerRemoved;
            _log?.Info("AdornerManager 이벤트 구독 완료");

            // 다중 선택 모드 설정 (기본값: 단일 선택)
            MainMap.SetMultiSelectMode(false);

            _log?.Info("Adorner 시스템 통합 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 시스템 통합 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner 시스템 정리
    /// </summary>
    private void CleanupAdornerIntegration()
    {
        if (MainMap == null) return;

        try
        {
            // 이벤트 구독 해제
            MainMap.OnMarkerClicked -= OnMapMarkerClicked;
            MainMap.OnImageClicked -= OnMapImageClicked;
            MainMap.OnMapClicked -= OnMapClicked;
            MainMap.MarkerEditStarted -= OnMarkerEditStarted;
            MainMap.MarkerEditCompleted -= OnMarkerEditCompleted;
            MainMap.MarkerEditCancelled -= OnMarkerEditCancelled;
            MainMap.AdornerCreated -= OnAdornerCreated;
            MainMap.AdornerRemoved -= OnAdornerRemoved;

            // 모든 선택 해제
            MainMap?.DeselectAllMarkers();

            _log?.Info("Adorner 시스템 정리 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 시스템 정리 실패: {ex.Message}");
        }
    }
    #endregion
    #region - GMapCustomControl 이벤트 핸들러 -
    /// <summary>
    /// 지도 마커 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapMarkerClicked(GMapCustomMarker marker)
    {
        try
        {
            _log?.Info($"*** OnMapMarkerClicked 호출됨 *** : {marker.Title}, 편집모드: {IsEditModeEnabled}");

            if (IsEditModeEnabled)
            {
                _log?.Info($"편집 모드에서 마커 선택 시도: {marker.Title}");
                SelectMarkerForEditing(marker);
            }
            else
            {
                _log?.Info("일반 모드에서 마커 선택");
                UpdateSelectedMarker(marker);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 이미지 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapImageClicked(GMapCustomImage image)
    {
        try
        {
            _log?.Info($"이미지 클릭됨: {image.Title}");

            if (IsEditModeEnabled)
            {
                // 편집 모드에서는 이미지를 선택
                SelectImageForEditing(image);
            }
            else
            {
                // 일반 모드에서는 단순 선택만
                UpdateSelectedImage(image);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 빈 공간 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapClicked(PointLatLng geoPos, Point screenPos)
    {
        try
        {
            ClickedCurrentPosition = geoPos;
            _log?.Info($"지도 클릭: ({geoPos.Lat:F6}, {geoPos.Lng:F6})");

            // 편집 모드에서 빈 공간 클릭 시 모든 선택 해제
            if (IsEditModeEnabled)
            {
                ClearAllSelections();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 클릭 처리 실패: {ex.Message}");
        }
    }
    #endregion

    #region - Adorner 이벤트 핸들러 -
    /// <summary>
    /// 마커 편집 시작 이벤트 핸들러
    /// </summary>
    private void OnMarkerEditStarted(object sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"마커 편집 시작: {e.Marker.Title}, 핸들: {e.Handle}");

        // UI 상태 업데이트
        IsMarkerEditing = true;
        NotifyOfPropertyChange(nameof(SelectedMarkerInfo));
    }

    /// <summary>
    /// 마커 편집 완료 이벤트 핸들러
    /// </summary>
    private void OnMarkerEditCompleted(object sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"마커 편집 완료: {e.Marker.Title}");
        _log?.Info($"변경사항: {e.GetChangesSummary()}");

        // UI 상태 업데이트
        IsMarkerEditing = false;
        NotifyOfPropertyChange(nameof(SelectedMarkerInfo));

        // 선택된 마커 속성들 갱신
        if (SelectedMarker?.Id == e.Marker.Id)
        {
            NotifyOfPropertyChange(nameof(SelectedMarkerBearing));
            NotifyOfPropertyChange(nameof(SelectedMarkerWidth));
            NotifyOfPropertyChange(nameof(SelectedMarkerHeight));
        }
    }

    /// <summary>
    /// 마커 편집 취소 이벤트 핸들러
    /// </summary>
    private void OnMarkerEditCancelled(object sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"마커 편집 취소: {e.Marker.Title}, 이유: {e.Reason}");

        // UI 상태 복원
        IsMarkerEditing = false;
        NotifyOfPropertyChange(nameof(SelectedMarkerInfo));
    }

    /// <summary>
    /// Adorner 생성 이벤트 핸들러
    /// </summary>
    private void OnAdornerCreated(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 생성됨: {e.Marker.Title}");
        AdornerCount++;
    }

    /// <summary>
    /// Adorner 제거 이벤트 핸들러
    /// </summary>
    private void OnAdornerRemoved(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 제거됨: {e.Marker.Title}");
        AdornerCount = Math.Max(0, AdornerCount - 1);
    }
    #endregion
    #region - 선택 관리 메서드 -
    /// <summary>
    /// 편집을 위한 마커 선택
    /// </summary>
    private void SelectMarkerForEditing(GMapCustomMarker marker)
    {
        try
        {
            _log?.Info($"편집을 위한 마커 선택 시작: {marker.Title}");

            // 이전 선택 해제
            ClearAllSelections();
            _log?.Info("이전 선택 해제 완료");

            // 새 마커 선택 및 Adorner 생성
            _log?.Info($"MainMap.SelectMarker 호출 중...");
            bool success = MainMap.SelectMarker(marker);
            _log?.Info($"MainMap.SelectMarker 결과: {success}");

            if (success)
            {
                UpdateSelectedMarker(marker);
                _log?.Info($"마커 편집 모드 활성화 완료: {marker.Title}");
            }
            else
            {
                _log?.Warning($"마커 선택 실패: {marker.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 편집 선택 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 편집을 위한 이미지 선택
    /// </summary>
    private void SelectImageForEditing(GMapCustomImage image)
    {
        try
        {
            // 이전 선택 해제
            ClearAllSelections();

            // 새 이미지 선택
            UpdateSelectedImage(image);
            image.IsSelected = true;

            _log?.Info($"이미지 편집 모드 활성화: {image.Title}");
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 편집 선택 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 선택된 마커 업데이트
    /// </summary>
    private void UpdateSelectedMarker(GMapCustomMarker marker)
    {
        SelectedMarker = marker;
        SelectedImage = null; // 이미지 선택 해제

        // 마커 관련 속성들 갱신
        NotifyOfPropertyChange(nameof(SelectedMarkerBearing));
        NotifyOfPropertyChange(nameof(SelectedMarkerWidth));
        NotifyOfPropertyChange(nameof(SelectedMarkerHeight));
        NotifyOfPropertyChange(nameof(SelectedMarkerInfo));
        NotifyOfPropertyChange(nameof(CanEditMarker));
    }

    /// <summary>
    /// 선택된 이미지 업데이트
    /// </summary>
    private void UpdateSelectedImage(GMapCustomImage image)
    {
        SelectedImage = image;
        SelectedMarker = null; // 마커 선택 해제
    }

    /// <summary>
    /// 모든 선택 해제
    /// </summary>
    private void ClearAllSelections()
    {
        try
        {
            // Adorner 모든 해제
            MainMap?.DeselectAllMarkers();

            // 이미지 선택 해제
            if (MainMap?.CustomImages != null)
            {
                foreach (var img in MainMap.CustomImages)
                {
                    img.IsSelected = false;
                }
            }
           


            // ViewModel 속성 초기화
            SelectedMarker = null;
            SelectedImage = null;

            _log?.Info("모든 선택 해제 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 해제 실패: {ex.Message}");
        }
    }
    #endregion
    #region - 명령어 초기화 -
    /// <summary>
    /// 모든 RelayCommand 초기화
    /// </summary>
    private void InitializeCommands()
    {
        InitializeFileCommands();
        InitializeMapCommands();
        InitializeNavigationCommands();
        InitializeEditCommands();
        InitializeRotationCommands();
        InitializeMarkerEditCommands();
        InitializeAdornerCommands(); // 새로 추가
    }

    /// <summary>
    /// 파일 관련 명령어 초기화
    /// </summary>
    private void InitializeFileCommands()
    {
        LoadMapImageCommand = new RelayCommand(ExecuteLoadMapImage, CanExecuteLoadImageMap);
        CreateCustomMapCommand = new RelayCommand(ExecuteCreateCustomMap, CanExecuteCreateCustomMap);
        SetMapTileFolderCommand = new RelayCommand(ExecuteSetMapTileFolder, CanExecuteSetMapTileFolder);
        ExitApplicationCommand = new RelayCommand(ExecuteExitApplication, CanExecuteExitApplication);
    }

    /// <summary>
    /// 지도 표시 관련 명령어 초기화
    /// </summary>
    private void InitializeMapCommands()
    {
        ToggleWGS84Command = new RelayCommand(ExecuteToggleWGS84Command, CanExecuteToggleWGS84Command);
        ToggleMGRSCommand = new RelayCommand(ExecuteToggleMGRSCommand, CanExecuteToggleMGRSCommand);
        ToggleUTMCommand = new RelayCommand(ExecuteToggleUTMCommand, CanExecuteToggleUTMCommand);
    }

    /// <summary>
    /// 네비게이션 관련 명령어 초기화
    /// </summary>
    private void InitializeNavigationCommands()
    {
        MoveHomeLocationCommand = new RelayCommand(ExecuteMoveHomeLocation, CanExecuteMoveHomeLocation);
        SetHomeLocationCommand = new RelayCommand(ExecuteSetHomeLocation, CanExecuteSetHomeLocation);
    }

    /// <summary>
    /// 편집 관련 명령어 초기화
    /// </summary>
    private void InitializeEditCommands()
    {
        ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, CanExecuteClearSelection);
        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected, CanExecuteDeleteSelected);
        ToggleEditModeCommand = new RelayCommand(ExecuteToggleEditMode, CanExecuteToggleEditMode); // 새로 추가

    }

    /// <summary>
    /// 회전 관련 명령어 초기화
    /// </summary>
    private void InitializeRotationCommands()
    {
        RotateCommand = new RelayCommand(ExecuteRotate, CanExecuteRotate);
        FineRotateCommand = new RelayCommand(ExecuteFineRotate, CanExecuteFineRotate);
        ResetRotationCommand = new RelayCommand(ExecuteResetRotation, CanExecuteResetRotation);
    }

    /// <summary>
    /// 마커 편집 관련 명령어 초기화
    /// </summary>
    private void InitializeMarkerEditCommands()
    {
        AddMarkerCommand = new RelayCommand(ExecuteAddMarker, CanExecuteAddMarker);
        DuplicateMarkerCommand = new RelayCommand(ExecuteDuplicateMarker, CanExecuteDuplicateMarker);
        SnapMarkerToGridCommand = new RelayCommand(ExecuteSnapMarkerToGrid, CanExecuteSnapMarkerToGrid);
        ResetMarkerRotationCommand = new RelayCommand(ExecuteResetMarkerRotation, CanExecuteResetMarkerRotation);
        ResetMarkerSizeCommand = new RelayCommand(ExecuteResetMarkerSize, CanExecuteResetMarkerSize);
    }

    /// <summary>
    /// Adorner 관련 명령어 초기화
    /// </summary>
    private void InitializeAdornerCommands()
    {
        ToggleMultiSelectCommand = new RelayCommand(ExecuteToggleMultiSelect, CanExecuteToggleMultiSelect);
        CancelAllEditingCommand = new RelayCommand(ExecuteCancelAllEditing, CanExecuteCancelAllEditing);
        LogAdornerStatsCommand = new RelayCommand(ExecuteLogAdornerStats, CanExecuteLogAdornerStats);
    }
    #endregion

    #region - 파일 명령어 구현 -
    /// <summary>
    /// 이미지 맵 로드 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteLoadImageMap(object arg) => true;

    /// <summary>
    /// 이미지 맵 로드 실행 - TIF/일반 이미지를 오버레이로 추가
    /// </summary>
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
                        filePath, currentPosition, MainMap, mapName);
                }
                else
                {
                    image = await _imageOverlayService.CreateImageOverlayAsync(
                        filePath, currentPosition, MainMap, mapName);
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
        }
    }

    /// <summary>
    /// 커스텀 맵 생성 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteCreateCustomMap(object arg) => SelectedImage != null;

    /// <summary>
    /// 커스텀 맵 생성 실행 - 선택된 이미지를 타일 맵으로 변환
    /// </summary>
    private async void ExecuteCreateCustomMap(object obj)
    {
        try
        {
            _log?.Info("커스텀 맵 생성하기 시작");

            // 1단계: 선택된 이미지 확인
            if (SelectedImage == null)
            {
                _log?.Warning("커스텀 지도로 변환할 이미지가 선택되지 않았습니다.");
                return;
            }

            _log?.Info($"선택된 이미지: {SelectedImage.Title}");

            // 2단계: 이미지 파일 경로 확인
            if (SelectedImage.Img == null)
            {
                _log?.Error("선택된 이미지의 소스 파일을 찾을 수 없습니다.");
                return;
            }

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
        }
    }

    /// <summary>
    /// 맵 타일 폴더 설정 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteSetMapTileFolder(object arg) => true;

    /// <summary>
    /// 맵 타일 폴더 설정 실행
    /// </summary>
    private void ExecuteSetMapTileFolder(object obj)
    {
        SelectTileDirectory();
    }

    /// <summary>
    /// 애플리케이션 종료 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteExitApplication(object arg) => true;

    /// <summary>
    /// 애플리케이션 종료 실행
    /// </summary>
    private void ExecuteExitApplication(object obj)
    {
        // TODO: 애플리케이션 종료 로직 구현 필요
    }
    #endregion

    #region - 지도 표시 명령어 구현 -
    /// <summary>
    /// WGS84 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleWGS84Command(object arg) => true;

    /// <summary>
    /// WGS84 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleWGS84Command(object obj)
    {
        IsShowWSG84 = !IsShowWSG84; // 개선: 직접 토글로 변경
    }

    /// <summary>
    /// MGRS 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleMGRSCommand(object arg) => true;

    /// <summary>
    /// MGRS 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleMGRSCommand(object obj)
    {
        IsShowMGRSGrid = IsShowMGRS;
    }

    /// <summary>
    /// UTM 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleUTMCommand(object arg) => true;

    /// <summary>
    /// UTM 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleUTMCommand(object obj)
    {
    }
    #endregion

    #region - 네비게이션 명령어 구현 -
    /// <summary>
    /// 홈 위치로 이동 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteMoveHomeLocation(object arg) => HomePosition != null;

    /// <summary>
    /// 홈 위치로 이동 실행
    /// </summary>
    private void ExecuteMoveHomeLocation(object obj)
    {
        GoToHomePosition();
    }

    /// <summary>
    /// 홈 위치 설정 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteSetHomeLocation(object arg) => true;

    /// <summary>
    /// 홈 위치 설정 실행
    /// </summary>
    private void ExecuteSetHomeLocation(object obj)
    {
        SetHomePosition();
    }
    #endregion

    #region - 편집 명령어 구현 -

    /// <summary>
    /// 선택 해제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteClearSelection(object arg) => HasSelectedItem;

    /// <summary>
    /// 선택 해제 실행
    /// </summary>
    private void ExecuteClearSelection(object obj)
    {
        ClearAllSelections();
    }

    /// <summary>
    /// 선택된 항목 삭제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteDeleteSelected(object arg) => HasSelectedItem && IsEditModeEnabled;

    /// <summary>
    /// 선택된 항목 삭제 실행
    /// </summary>
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
                // Adorner 먼저 제거
                MainMap.DeselectMarker(SelectedMarker);

                // GMap.NET 마커 컬렉션에서 제거
                MainMap.Markers.Remove(SelectedMarker);

                // 마커 리소스 정리
                SelectedMarker.Dispose();
                SelectedMarker = null;

                _log?.Info("선택된 마커 삭제 완료");
            }

            // 화면 갱신
            MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 항목 삭제 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 편집 모드 토글 명령어
    /// </summary>
    private bool CanExecuteToggleEditMode(object arg) => true;
    private void ExecuteToggleEditMode(object obj)
    {
        IsEditModeEnabled = !IsEditModeEnabled;
    }

    /// <summary>
    /// 다중 선택 모드 토글 명령어
    /// </summary>
    private bool CanExecuteToggleMultiSelect(object arg) => IsEditModeEnabled;
    private void ExecuteToggleMultiSelect(object obj)
    {
        try
        {
            IsMultiSelectEnabled = !IsMultiSelectEnabled;
            MainMap?.SetMultiSelectMode(IsMultiSelectEnabled);
            _log?.Info($"다중 선택 모드: {(IsMultiSelectEnabled ? "활성화" : "비활성화")}");
        }
        catch (Exception ex)
        {
            _log?.Error($"다중 선택 모드 토글 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 모든 편집 취소 명령어
    /// </summary>
    private bool CanExecuteCancelAllEditing(object arg) => IsEditModeEnabled && AdornerCount > 0;
    private void ExecuteCancelAllEditing(object obj)
    {
        try
        {
            MainMap?.AdornerManager?.CancelAllEditing(MainMap);
            _log?.Info("모든 편집 취소 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"모든 편집 취소 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner 통계 로그 출력 명령어
    /// </summary>
    private bool CanExecuteLogAdornerStats(object arg) => true;
    private void ExecuteLogAdornerStats(object obj)
    {
        try
        {
            MainMap?.LogAdornerStatistics();
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 통계 출력 실패: {ex.Message}");
        }
    }
    #endregion

    #region - 회전 명령어 구현 -
    /// <summary>
    /// 회전 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteRotate(object arg) => true;

    /// <summary>
    /// 회전 실행 - 절대각도로 회전
    /// </summary>
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

    /// <summary>
    /// 미세 회전 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteFineRotate(object arg) => true;

    /// <summary>
    /// 미세 회전 실행 - 상대각도로 회전
    /// </summary>
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

    /// <summary>
    /// 회전 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetRotation(object arg) => true;

    /// <summary>
    /// 회전 초기화 실행
    /// </summary>
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
    #endregion

    #region - 마커 편집 명령어 구현 -
    /// <summary>
    /// 마커 추가 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteAddMarker(object arg) => true;

    /// <summary>
    /// 마커 추가 실행
    /// </summary>
    private void ExecuteAddMarker(object obj)
    {
        try
        {
            var position = ClickedCurrentPosition.IsEmpty ? MainMap.CenterPosition : ClickedCurrentPosition;
            AddTestMarker(position, "Test");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 추가 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 복제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteDuplicateMarker(object arg) => SelectedMarker != null;

    /// <summary>
    /// 마커 복제 실행
    /// </summary>
    private void ExecuteDuplicateMarker(object obj)
    {
        try
        {
            DuplicateSelectedMarker();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 복제 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 격자 스냅 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteSnapMarkerToGrid(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 격자 스냅 실행
    /// </summary>
    private void ExecuteSnapMarkerToGrid(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                SnapMarkerToGrid(SelectedMarker);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 스냅 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetMarkerRotation(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 회전 초기화 실행
    /// </summary>
    private void ExecuteResetMarkerRotation(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                UpdateMarkerRotation(SelectedMarker, 0);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 초기화 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetMarkerSize(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 크기 초기화 실행
    /// </summary>
    private void ExecuteResetMarkerSize(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                UpdateMarkerSize(SelectedMarker, 32, 32); // 기본 크기로 초기화
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 초기화 실행 실패: {ex.Message}");
        }
    }
    #endregion

    #region - 지도 구성 및 설정 -
    /// <summary>
    /// 비동기 지도 설정 - 선택된 맵 타입에 따라 구성
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
    /// 기존 제공자 지도 설정 (Google, Bing, OpenStreetMap 등)
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
    /// 커스텀 지도 설정 (사용자 정의 타일 맵)
    /// </summary>
    private Task ConfigureCustomMapAsync(CustomMapModel customMap)
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
        return Task.CompletedTask;
    }

    /// <summary>
    /// 공통 지도 설정 - 위치, 줌, 이벤트 핸들러 등
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

    /// <summary>
    /// Google 지도 스타일별 Provider 설정을 별도 메서드로 분리
    /// </summary>
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

    /// <summary>
    /// Bing  지도 스타일별 Provider 설정
    /// </summary>
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
    #endregion

    #region - 지도 전환 및 관리 -
    /// <summary>
    /// 지도 변경 (런타임에서) - 다른 맵으로 동적 전환
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
    #endregion

    #region - 홈 위치 관리 -
    /// <summary>
    /// 초기 홈 위치 설정
    /// </summary>
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

    /// <summary>
    /// 홈 위치 설정 - 현재 클릭된 위치를 홈으로 저장
    /// </summary>
    public async void SetHomePosition()
    {
        if (HomePosition == null) return;

        HomePosition.Position = new CoordinateModel(latitude: ClickedCurrentPosition.Lat, longitude: ClickedCurrentPosition.Lng, altitude: 0);
        HomePosition.Zoom = Zoom;
        HomePosition.IsAvailable = true;
        _log?.Info($"The home position is set to (Position: ({HomePosition.Position.Latitude}, {HomePosition.Position.Longitude}), Zoom: {HomePosition.Zoom}).");
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);
    }

    /// <summary>
    /// 홈 위치로 이동
    /// </summary>
    public void GoToHomePosition()
    {
        if (HomePosition == null || HomePosition.Position == null) return;

        MainMap.Position = new PointLatLng(HomePosition.Position.Latitude, HomePosition.Position.Longitude);
        MainMap.Zoom = HomePosition.Zoom;
        _log?.Info($"Moved to home position.");
    }

    /// <summary>
    /// 홈 위치 해제
    /// </summary>
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
    #endregion

    #region - 마커 관리 및 편집 -
    /// <summary>
    /// 마커 추가 (테스트용) - 지정된 위치에 새 마커 생성
    /// </summary>
    public void AddTestMarker(PointLatLng position, string title = "Test Marker")
    {
        try
        {
            // SymbolModel 생성 (실제 구현에 맞게 조정 필요)
            var symbolModel = new SymbolModel
            {
                Id = GenerateNewMarkerId(),
                Title = title,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Width = 50,
                Height = 50,
                Bearing = 0,
                Category = EnumMarkerCategory.PIDS_EQUIPMENT, // 적절한 카테고리로 수정
                Visibility = true,
                OperationState = EnumOperationState.ACTIVE
            };

            var customMarker = new GMapCustomMarker(_log!, symbolModel);

            // 마커에 UI Shape 설정 확인
            if (customMarker.Shape == null)
            {
                _log?.Error($"마커 '{title}'의 Shape가 여전히 null입니다!");
                return; // 마커 추가 중단
            }
            else
            {
                _log?.Info($"마커 '{title}' Shape 확인됨: {customMarker.Shape.GetType().Name}");
            }

            // GMap에 마커 추가
            MainMap.Markers.Add(customMarker);

            // CustomMarkers 동기화 확인
            _log?.Info($"GMap.Markers 총 개수: {MainMap.Markers.Count}");
            _log?.Info($"CustomMarkers 총 개수: {MainMap.CustomMarkers?.Count ?? 0}");

            // 강제 동기화 (필요한 경우)
            if (MainMap.CustomMarkers != null && !MainMap.CustomMarkers.Contains(customMarker))
            {
                _log?.Warning("CustomMarkers에 마커가 없어서 강제 추가 중...");
                MainMap.CustomMarkers.Add(customMarker);
            }

            // 강제 새로고침
            MainMap.InvalidateVisual();

            _log?.Info($"마커 추가 완료: {title} at ({position.Lat:F6}, {position.Lng:F6})");
            _log?.Info($"현재 총 마커 수: {MainMap.Markers.Count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"테스트 마커 추가 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 새 마커 ID 생성 - 기존 마커들의 최대 ID + 1
    /// </summary>
    private int GenerateNewMarkerId()
    {
        return MainMap.CustomMarkers.Any() ?
               MainMap.CustomMarkers.Max(m => m.Id) + 1 :
               1;
    }

    /// <summary>
    /// 마커 위치 업데이트
    /// </summary>
    public void UpdateMarkerPosition(GMapCustomMarker marker, PointLatLng newPosition)
    {
        try
        {
            if (marker == null) return;

            marker.UpdateLocation(newPosition);
            _log?.Info($"마커 '{marker.Title}' 위치 업데이트: ({newPosition.Lat:F6}, {newPosition.Lng:F6})");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 위치 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 업데이트
    /// </summary>
    public void UpdateMarkerRotation(GMapCustomMarker marker, double bearing)
    {
        try
        {
            if (marker == null) return;

            marker.Bearing = bearing;
            _log?.Info($"마커 '{marker.Title}' 회전 업데이트: {bearing:F1}도");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 업데이트
    /// </summary>
    public void UpdateMarkerSize(GMapCustomMarker marker, double width, double height)
    {
        try
        {
            if (marker == null) return;

            marker.Width = Math.Max(10, width);   // 최소 크기 보장
            marker.Height = Math.Max(10, height); // 최소 크기 보장

            _log?.Info($"마커 '{marker.Title}' 크기 업데이트: {width:F1}x{height:F1}");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 선택된 마커 복제
    /// </summary>
    public void DuplicateSelectedMarker()
    {
        try
        {
            if (SelectedMarker == null)
            {
                _log?.Warning("복제할 마커가 선택되지 않았습니다.");
                return;
            }

            var originalPos = SelectedMarker.Position;
            var newPos = new PointLatLng(originalPos.Lat + 0.0001, originalPos.Lng + 0.0001); // 약간 이동된 위치

            AddTestMarker(newPos, $"{SelectedMarker.Title}_Copy");

            _log?.Info($"마커 복제 완료: {SelectedMarker.Title}");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 복제 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 우클릭 메뉴 생성 (향후 확장용)
    /// </summary>
    public void ShowMarkerContextMenu(GMapCustomMarker marker, Point screenPosition)
    {
        try
        {
            if (marker == null) return;

            _log?.Info($"마커 컨텍스트 메뉴 표시: {marker.Title}");

            // TODO: 실제 컨텍스트 메뉴 구현
            // - 속성 편집
            // - 복제
            // - 삭제
            // - 회전 초기화
            // - 크기 초기화
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 컨텍스트 메뉴 표시 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 스냅 기능 (격자에 맞춤)
    /// </summary>
    public void SnapMarkerToGrid(GMapCustomMarker marker, double gridSize = 0.0001)
    {
        try
        {
            if (marker == null) return;

            var currentPos = marker.Position;
            var snappedLat = Math.Round(currentPos.Lat / gridSize) * gridSize;
            var snappedLng = Math.Round(currentPos.Lng / gridSize) * gridSize;

            var snappedPos = new PointLatLng(snappedLat, snappedLng);
            marker.UpdateLocation(snappedPos);

            _log?.Info($"마커 '{marker.Title}' 격자 스냅 완료: ({snappedLat:F6}, {snappedLng:F6})");

            MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 격자 스냅 실패: {ex.Message}");
        }
    }
    #endregion

  
    #region - 회전 속성 동기화 -
    /// <summary>
    /// 회전 관련 속성들을 MainMap과 동기화하는 메서드
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
    #endregion

    #region - 이벤트 핸들러 -
    /// <summary>
    /// 지도 위치 변경 이벤트 핸들러
    /// </summary>
    private void MainMap_OnCurrentPositionChanged(PointLatLng point)
    {
        MainMap.Position = point;
    }

    /// <summary>
    /// 마우스 이동 이벤트 핸들러 - 좌표 표시 업데이트
    /// </summary>
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

    /// <summary>
    /// 마우스 클릭 이벤트 핸들러 - 클릭 위치 저장 및 편집 모드 처리
    /// </summary>
    private void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(MainMap);
        ClickedCurrentPosition = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);

        // 디버깅 로그 추가
        _log?.Info($"마우스 클릭: 화면좌표({p.X:F2}, {p.Y:F2}) -> 지리좌표({ClickedCurrentPosition.Lat:F6}, {ClickedCurrentPosition.Lng:F6})");

        // 이제 객체 선택은 GMapCustomControl의 이벤트로 처리됨
        // - OnMarkerClicked 이벤트 → OnMapMarkerClicked() 핸들러
        // - OnImageClicked 이벤트 → OnMapImageClicked() 핸들러  
        // - OnMapClicked 이벤트 → OnMapClicked() 핸들러

        //List<GMapCustomImage> clickedImages = MainMap.GetImageOverlaysAt(ClickedCurrentPosition);
        //List<GMapCustomMarker> clickedMarkers = MainMap.CustomMarkers
        //    .Where(m => IsNearPosition(m.Position, ClickedCurrentPosition, 0.0001))
        //    .ToList();

        //_log?.Info($"찾은 이미지 수: {clickedImages.Count}, 찾은 마커 수: {clickedMarkers.Count}");

        //// 편집 모드일 때 자동으로 객체 선택
        //if (IsEditModeEnabled)
        //{
        //    _log?.Info($"편집 모드 활성화됨. 이미지 개수: {MainMap.CustomImages.Count}");

        //    // 이미지 우선 선택
        //    if (clickedImages.Any())
        //    {
        //        var selectedImage = clickedImages.First();
        //        _log?.Info($"이미지 선택: {selectedImage.Title}");
        //        SelectAndEditImage(selectedImage);
        //    }
        //    // 마커 선택
        //    else if (clickedMarkers.Any())
        //    {
        //        var selectedMarker = clickedMarkers.First();
        //        _log?.Info($"마커 선택: {selectedMarker.Title}");
        //        SelectAndEditMarker(selectedMarker);
        //    }
        //    else
        //    {
        //        _log?.Info("선택 위치에 편집 가능한 객체가 없습니다.");
        //    }
        //}
        //else
        //{
        //    _log?.Info("편집 모드가 비활성화 상태입니다.");
        //}
    }

    /// <summary>
    /// 줌 변경 이벤트 핸들러 - 스케일바 업데이트
    /// </summary>
    private void MainMap_OnMapZoomChanged()
    {
        CreateScaleBar();
    }
    #endregion

    #region - UI 업데이트 및 유틸리티 -
    /// <summary>
    /// 줌 버튼 클릭 핸들러 - 줌 인
    /// </summary>
    public void OnClickZoomUp(object sender, EventArgs args)
    {
        if (ZoomMax > MainMap.Zoom)
            MainMap.Zoom++;
    }

    /// <summary>
    /// 줌 버튼 클릭 핸들러 - 줌 아웃
    /// </summary>
    public void OnClickZoomDown(object sender, EventArgs args)
    {
        if (ZoomMin < MainMap.Zoom)
            MainMap.Zoom--;
    }

    /// <summary>
    /// 스케일바 생성
    /// </summary>
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

    /// <summary>
    /// 객체 위치 검색 - 첫 번째 마커 위치로 이동
    /// </summary>
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
    #endregion

    #region - 데이터 로딩 및 저장 -
    /// <summary>
    /// 캐시된 지도 데이터 비동기 로드
    /// </summary>
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
        }
    }
    #endregion

    #region - 헬퍼 메서드 -
    /// <summary>
    /// 두 위치가 허용 범위 내에 있는지 확인
    /// </summary>
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
    /// 생성된 커스텀 지도 적용 (삭제필요 - 사용되지 않는 메서드)
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
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 지도 적용 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 원본 이미지 제거 확인 (삭제필요 - 사용되지 않는 메서드)
    /// </summary>
    private async Task<bool> AskRemoveOriginalImageAsync()
    {
        // TODO: 실제 UI 확인 대화상자 구현
        await Task.Delay(100);
        return true; // 기본적으로 제거
    }
    #endregion

    #region - 속성 (Properties) -

    #region - 줌 관련 속성 -
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    public double Zoom
    {
        get { return MainMap.Zoom; }
        set
        {
            MainMap.Zoom = value;
            NotifyOfPropertyChange(nameof(Zoom));
        }
    }

    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    public int ZoomMax
    {
        get { return MainMap.MaxZoom; }
        set
        {
            MainMap.MaxZoom = value;
            NotifyOfPropertyChange(nameof(ZoomMax));
        }
    }

    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    public int ZoomMin
    {
        get { return MainMap.MinZoom; }
        set
        {
            MainMap.MinZoom = value;
            NotifyOfPropertyChange(nameof(ZoomMin));
        }
    }
    #endregion

    #region - 좌표 및 위치 관련 속성 -
    /// <summary>
    /// 현재 마우스 커서 위치의 좌표
    /// </summary>
    public ICoordinateModel CurrentCoordinatePosition
    {
        get { return _currentPosition; }
        set
        {
            _currentPosition = value;
            NotifyOfPropertyChange(nameof(CurrentCoordinatePosition));
        }
    }

    /// <summary>
    /// 현재 위치의 MGRS 좌표
    /// </summary>
    public string CurrentMGRS
    {
        get { return _currentMGRS; }
        set { _currentMGRS = value; NotifyOfPropertyChange(nameof(CurrentMGRS)); }
    }

    /// <summary>
    /// 현재 위치의 UTM 좌표
    /// </summary>
    public string CurrentUTM
    {
        get { return _currentUTM; }
        set { _currentUTM = value; NotifyOfPropertyChange(nameof(CurrentUTM)); }
    }

    /// <summary>
    /// 현재 좌표를 PointLatLng로 반환
    /// </summary>
    public PointLatLng CurrentPointPosition => new PointLatLng(_currentPosition.Latitude, _currentPosition.Longitude);

    /// <summary>
    /// 마지막으로 클릭된 위치
    /// </summary>
    public PointLatLng ClickedCurrentPosition { get; set; }

    /// <summary>
    /// 홈 위치 정보
    /// </summary>
    public HomePositionModel? HomePosition { get; set; }
    #endregion

    #region - 표시 옵션 관련 속성 -
    /// <summary>
    /// WGS84 좌표계 표시 여부
    /// </summary>
    public bool IsShowWSG84
    {
        get { return _isShowWSG84; }
        set { _isShowWSG84 = value; NotifyOfPropertyChange(nameof(IsShowWSG84)); }
    }

    /// <summary>
    /// MGRS 좌표계 표시 여부
    /// </summary>
    public bool IsShowMGRS
    {
        get { return _isShowMGRS; }
        set { _isShowMGRS = value; 
            NotifyOfPropertyChange(nameof(IsShowMGRS)); }
    }

    /// <summary>
    /// MGRS 그리드 표시 여부
    /// </summary>
    public bool IsShowMGRSGrid
    {
        get { return _isShowMGRSGrid; }
        set { _isShowMGRSGrid = value; NotifyOfPropertyChange(nameof(IsShowMGRSGrid)); }
    }

    /// <summary>
    /// UTM 좌표계 표시 여부
    /// </summary>
    public bool IsShowUTM
    {
        get { return _isShowUTM; }
        set { _isShowUTM = value; NotifyOfPropertyChange(nameof(IsShowUTM)); }
    }

    /// <summary>
    /// 스케일바 텍스트
    /// </summary>
    public string? Scale
    {
        get { return _scale; }
        set
        {
            _scale = value;
            NotifyOfPropertyChange(nameof(Scale));
        }
    }

    /// <summary>
    /// 스케일바 그리기 점들
    /// </summary>
    public PointCollection? ScalePoints { get; set; }
    #endregion

    #region - 편집 모드 관련 속성 -
    /// <summary>
    /// 편집 모드 활성화 여부
    /// </summary>
    public bool IsEditModeEnabled
    {
        get => _isEditModeEnabled;
        set
        {
            if (_isEditModeEnabled != value)
            {
                _isEditModeEnabled = value;
                MainMap.SetEditMode(value);

                // 편집 모드 해제 시 모든 선택 해제
                if (!value)
                {
                    ClearAllSelections();
                }

                NotifyOfPropertyChange(nameof(IsEditModeEnabled));
                _log?.Info($"편집 모드: {(value ? "활성화" : "비활성화")}");
            }
        }
    }

    /// <summary>
    /// 다중 선택 모드 활성화 여부
    /// </summary>
    public bool IsMultiSelectEnabled
    {
        get => _isMultiSelectEnabled;
        set
        {
            _isMultiSelectEnabled = value;
            NotifyOfPropertyChange(nameof(IsMultiSelectEnabled));
        }
    }

    /// <summary>
    /// 마커 편집 중인지 여부
    /// </summary>
    public bool IsMarkerEditing
    {
        get => _isMarkerEditing;
        set
        {
            _isMarkerEditing = value;
            NotifyOfPropertyChange(nameof(IsMarkerEditing));
        }
    }

    /// <summary>
    /// 현재 활성 Adorner 개수
    /// </summary>
    public int AdornerCount
    {
        get => _adornerCount;
        set
        {
            _adornerCount = value;
            NotifyOfPropertyChange(nameof(AdornerCount));
            NotifyOfPropertyChange(nameof(HasActiveAdorners));
        }
    }

    /// <summary>
    /// 활성 Adorner가 있는지 여부
    /// </summary>
    public bool HasActiveAdorners => AdornerCount > 0;

    /// <summary>
    /// Adorner 통계 정보
    /// </summary>
    public string AdornerStatistics => MainMap?.LogAdornerStatistics() ?? "통계 없음";

    /// <summary>
    /// 선택된 이미지
    /// </summary>
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

    /// <summary>
    /// 선택된 마커
    /// </summary>
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


    /// <summary>
    /// 선택된 항목이 있는지 여부
    /// </summary>
    public bool HasSelectedItem => SelectedImage != null || SelectedMarker != null;
    #endregion

    #region - 회전 관련 속성 -
    /// <summary>
    /// 현재 회전 각도
    /// </summary>
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

    /// <summary>
    /// 지도 회전 각도
    /// </summary>
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

    /// <summary>
    /// 회전 스냅 각도
    /// </summary>
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

    /// <summary>
    /// 회전 컨트롤 표시 여부
    /// </summary>
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

    /// <summary>
    /// 회전 상태 여부
    /// </summary>
    public bool IsRotated => Math.Abs(CurrentRotation) > 0.1;
    #endregion

    #region - 선택된 마커 편집 속성 -
    /// <summary>
    /// 선택된 마커의 방향각
    /// </summary>
    public double SelectedMarkerBearing
    {
        get => SelectedMarker?.Bearing ?? 0;
        set
        {
            if (SelectedMarker != null && Math.Abs(SelectedMarker.Bearing - value) > 0.1)
            {
                UpdateMarkerRotation(SelectedMarker, value);
                NotifyOfPropertyChange(nameof(SelectedMarkerBearing));
            }
        }
    }

    /// <summary>
    /// 선택된 마커의 너비
    /// </summary>
    public double SelectedMarkerWidth
    {
        get => SelectedMarker?.Width ?? 0;
        set
        {
            if (SelectedMarker != null && Math.Abs(SelectedMarker.Width - value) > 0.1)
            {
                UpdateMarkerSize(SelectedMarker, value, SelectedMarker.Height);
                NotifyOfPropertyChange(nameof(SelectedMarkerWidth));
            }
        }
    }

    /// <summary>
    /// 선택된 마커의 높이
    /// </summary>
    public double SelectedMarkerHeight
    {
        get => SelectedMarker?.Height ?? 0;
        set
        {
            if (SelectedMarker != null && Math.Abs(SelectedMarker.Height - value) > 0.1)
            {
                UpdateMarkerSize(SelectedMarker, SelectedMarker.Width, value);
                NotifyOfPropertyChange(nameof(SelectedMarkerHeight));
            }
        }
    }

    /// <summary>
    /// 선택된 마커 정보 문자열
    /// </summary>
    public string SelectedMarkerInfo
    {
        get
        {
            if (SelectedMarker == null) return "마커가 선택되지 않음";

            return $"마커: {SelectedMarker.Title}\n" +
                   $"위치: ({SelectedMarker.Position.Lat:F6}, {SelectedMarker.Position.Lng:F6})\n" +
                   $"크기: {SelectedMarker.Width:F0}x{SelectedMarker.Height:F0}\n" +
                   $"회전: {SelectedMarker.Bearing:F1}°\n" +
                   $"상태: {SelectedMarker.OperationState}";
        }
    }

    /// <summary>
    /// 마커 편집 가능 여부
    /// </summary>
    public bool CanEditMarker => SelectedMarker != null && IsEditModeEnabled;
    #endregion

    #region - 컨트롤 및 서비스 참조 -
    /// <summary>
    /// 메인 지도 컨트롤
    /// </summary>
    public GMapCustomControl MainMap { get; private set; } = new();

    /// <summary>
    /// 현재 선택된 지도 모델
    /// </summary>
    public IMapModel SelectedMap { get; private set; }

    /// <summary>
    /// 현재 활성화된 커스텀 맵 Provider
    /// </summary>
    public FileBasedCustomMapProvider? CurrentCustomMapProvider { get; private set; }
    #endregion

    #region - 명령어 속성 -
    // 파일 관련 명령어
    public RelayCommand LoadMapImageCommand { get; private set; }
    public RelayCommand CreateCustomMapCommand { get; private set; }
    public RelayCommand SetMapTileFolderCommand { get; private set; }
    public RelayCommand ExitApplicationCommand { get; private set; }

    // 지도 표시 관련 명령어
    public RelayCommand ToggleWGS84Command { get; private set; }
    public RelayCommand ToggleMGRSCommand { get; private set; }
    public RelayCommand ToggleUTMCommand { get; private set; }

    // 네비게이션 관련 명령어
    public RelayCommand MoveHomeLocationCommand { get; private set; }
    public RelayCommand SetHomeLocationCommand { get; private set; }

    // 편집 관련 명령어
    public RelayCommand ClearSelectionCommand { get; private set; }
    public RelayCommand DeleteSelectedCommand { get; private set; }

    // 회전 관련 명령어
    public RelayCommand RotateCommand { get; private set; }
    public RelayCommand FineRotateCommand { get; private set; }
    public RelayCommand ResetRotationCommand { get; private set; }
    public RelayCommand AlignToMGRSCommand { get; private set; } // TODO: 구현 필요

    // 마커 편집 관련 명령어
    public RelayCommand AddMarkerCommand { get; private set; }
    public RelayCommand DuplicateMarkerCommand { get; private set; }
    public RelayCommand SnapMarkerToGridCommand { get; private set; }
    public RelayCommand ResetMarkerRotationCommand { get; private set; }
    public RelayCommand ResetMarkerSizeCommand { get; private set; }

    public RelayCommand ToggleEditModeCommand { get; private set; }
    public RelayCommand ToggleMultiSelectCommand { get; private set; }
    public RelayCommand CancelAllEditingCommand { get; private set; }
    public RelayCommand LogAdornerStatsCommand { get; private set; }
    #endregion

    #endregion

    #region - 필드 (Private Fields) -
    // 서비스 및 의존성
    private CancellationTokenSource _cts;
    private MapProvider _mapProvider;
    private DefinedMapProvider _definedMapProvider;
    private Providers.CustomMapProvider _customMapProvider;
    private GMapSetupModel _setupModel;
    private CustomMapService _customMapService;
    private ImageOverlayService _imageOverlayService;

    // UI 상태 필드
    private string? _scale;
    private ICoordinateModel _currentPosition = new CoordinateModel(37.648425, 126.904284);
    private string? _currentMGRS;
    private string? _currentUTM;
    private bool _isEditModeEnabled;

    // 표시 옵션 필드
    private bool _isShowWSG84 = true;
    private bool _isShowMGRS;
    private bool _isShowMGRSGrid;
    private bool _isShowUTM;

    // 회전 관련 필드
    private double _currentRotation;
    private double _mapRotation;
    private double _rotationSnapAngle;
    private bool _showRotationControl = false;

    // Adorner관련 필드
    private bool _isMultiSelectEnabled = false;
    private bool _isMarkerEditing = false;
    private int _adornerCount = 0;

    // 선택 상태 필드
    private GMapCustomImage? _selectedImage;
    private GMapCustomMarker? _selectedMarker;
    #endregion
}