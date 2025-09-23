using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;


namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/23/2025 10:07:33 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPidsGroupMarker: GMapBaseMarker<IPidsGroupSymbolModel>, IPidsGroupEditableMarker
    {
        #region - Ctors -
        public GMapPidsGroupMarker(ILogService log, IPidsGroupSymbolModel symbolModel)
            : base(log, symbolModel)
        {
            _log?.Info($"[GMapPidsGroupMarker 생성자] 시작 - Title: {symbolModel.Title}");

            // 모델의 GeoPoint를 런타임 PointLatLng로 변환
            _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
            _log?.Info($"[GMapPidsGroupMarker 생성자] 초기 포인트 변환 완료 - 개수: {_runtimePoints.Count}");

            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);

            // Position은 symbolModel의 Latitude/Longitude 사용 (중심점)
            Position = new PointLatLng(symbolModel.Latitude, symbolModel.Longitude);
            _log?.Info($"[GMapPidsGroupMarker 생성자] 중심 위치 유지: ({Position.Lat}, {Position.Lng})");

            // 카테고리 설정 (라인은 기본적으로 AREA_BOUNDARY 카테고리)
            Category = EnumMarkerCategory.AREA_BOUNDARY;

            _isDrawing = false;

            _log?.Info($"[GMapPidsGroupMarker 생성자] 완료 - Category: {Category}");
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        /// <summary>
        /// 위치 업데이트 오버라이드 - 라인의 모든 포인트도 함께 이동
        /// </summary>
        public override void UpdateLocation(PointLatLng newPosition)
        {
            // 이동 거리 계산
            var deltaLat = newPosition.Lat - Position.Lat;
            var deltaLng = newPosition.Lng - Position.Lng;

            _log?.Info($"[UpdateLocation] 이동 거리: deltaLat={deltaLat}, deltaLng={deltaLng}");

            // 기본 위치 업데이트
            base.UpdateLocation(newPosition);

            // 모든 포인트를 동일한 거리만큼 이동
            for (int i = 0; i < _runtimePoints.Count; i++)
            {
                var oldPoint = _runtimePoints[i];
                _runtimePoints[i] = new PointLatLng(
                    oldPoint.Lat + deltaLat,
                    oldPoint.Lng + deltaLng
                );
            }

            // 모델 동기화
            SyncModelPoints();

            // UI 업데이트 알림
            NotifyPointsChanged();

            // Shape가 GMapMarkerLineControl이면 강제 업데이트
            if (Shape is GMapMarkerLineControl lineControl)
            {
                // UpdateLineGeometry를 다시 호출하도록 트리거
                lineControl.InvalidateVisual();
            }

            _log?.Info($"[UpdateLocation] 라인 포인트 {_runtimePoints.Count}개 이동 완료");
        }


        protected override UIElement CreateMarkerControl()
        {
            _log?.Info($"[CreateMarkerControl] GMapPidsGroupMarker 생성 시작");
            var control = new GMapMarkerPidsGroupControl(this);
            _log?.Info($"[CreateMarkerControl] GMapPidsGroupMarker 생성 완료 - Type: {control.GetType().Name}");
            return control;
        }

        /// <summary>
        /// 마커 초기화 오버라이드
        /// </summary>
        protected override void InitializeMarker()
        {
            base.InitializeMarker();

            // 라인은 기본적으로 편집 모드 활성화
            EnableShapeAnimation = false;

            _log?.Info($"라인 마커 초기화: {_model.Title}, {_model.LinePoints.Count}개 포인트");
        }

        /// <summary>
        /// 명령어 초기화 오버라이드
        /// </summary>
        protected override void InitializeCommands()
        {
            _log?.Info($"[InitializeMarker] 시작 - Title: {_model.Title}");
            base.InitializeMarker();

            EnableShapeAnimation = false;
            _log?.Info($"[InitializeMarker] 완료 - EnableShapeAnimation: {EnableShapeAnimation}");
            _log?.Info($"[InitializeMarker] 초기 LinePoints 개수: {_model.LinePoints.Count}");

            // 라인 전용 명령어 추가 가능
            // AddPointCommand = new RelayCommand<PointLatLng>(ExecuteAddPoint);
            // RemovePointCommand = new RelayCommand<int>(ExecuteRemovePoint);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        /// <summary>
        /// 포인트 추가 (편집 모드에서 사용)
        /// </summary>
        public void AddPoint(PointLatLng point)
        {
            _log?.Info($"[AddPoint] 포인트 추가: ({point.Lat}, {point.Lng})");

            _runtimePoints.Add(point);

            // 첫 번째 점이면 마커 위치 설정
            if (_runtimePoints.Count == 1)
            {
                Position = point;
                _model.Latitude = point.Lat;
                _model.Longitude = point.Lng;
            }

            // 모델 동기화
            SyncModelPoints();

            // 속성 변경 알림
            NotifyPointsChanged();
        }

        /// <summary>
        /// 포인트 업데이트 (편집 모드에서 사용)
        /// </summary>
        public void UpdatePoint(int index, PointLatLng newPoint)
        {
            if (index < 0 || index >= _runtimePoints.Count)
            {
                _log?.Warning($"[UpdatePoint] 잘못된 인덱스: {index}");
                return;
            }

            _log?.Info($"[UpdatePoint] 인덱스 {index} 포인트 업데이트");

            _runtimePoints[index] = newPoint;

            // 첫 번째 점 업데이트시 마커 위치도 업데이트
            if (index == 0)
            {
                Position = newPoint;
                _model.Latitude = newPoint.Lat;
                _model.Longitude = newPoint.Lng;
            }

            // 모델 동기화
            SyncModelPoints();

            // 속성 변경 알림
            NotifyPointsChanged();
        }

        /// <summary>
        /// 마지막 포인트 제거 (편집 모드에서 사용)
        /// </summary>
        public void RemoveLastPoint()
        {
            if (_runtimePoints.Count > 0)
            {
                var removed = _runtimePoints.Last();
                _runtimePoints.RemoveAt(_runtimePoints.Count - 1);

                _log?.Info($"[RemoveLastPoint] 포인트 제거: ({removed.Lat}, {removed.Lng})");

                // 모델 동기화
                SyncModelPoints();

                // 속성 변경 알림
                NotifyPointsChanged();
            }
        }

        /// <summary>
        /// 드로잉 시작 (Adorner 방식에서는 사용 안 함)
        /// </summary>
        public void StartDrawing()
        {
            // Adorner 방식에서는 이 메서드를 사용하지 않음
            _log?.Info("[StartDrawing] Adorner 방식에서는 사용하지 않음");
        }

        /// <summary>
        /// 드로잉 완료 (Adorner 방식에서는 사용 안 함)
        /// </summary>
        public void FinishDrawing()
        {
            // Adorner 방식에서는 이 메서드를 사용하지 않음
            _log?.Info("[FinishDrawing] Adorner 방식에서는 사용하지 않음");
        }

        /// <summary>
        /// 드로잉 취소 (Adorner 방식에서는 사용 안 함)
        /// </summary>
        public void CancelDrawing()
        {
            // Adorner 방식에서는 이 메서드를 사용하지 않음
            _log?.Info("[CancelDrawing] Adorner 방식에서는 사용하지 않음");
        }

        /// <summary>
        /// 라인 검증 (최소 2개 포인트 필요)
        /// </summary>
        public bool IsValid()
        {
            return _runtimePoints.Count >= 2;
        }

        /// <summary>
        /// 모델 포인트 동기화
        /// </summary>
        private void SyncModelPoints()
        {
            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
        }

        /// <summary>
        /// 포인트 변경 알림
        /// </summary>
        private void NotifyPointsChanged()
        {
            OnPropertyChanged(nameof(RuntimePoints));
            OnPropertyChanged(nameof(LinePoints));
            OnPropertyChanged(nameof(TotalDistance));
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public List<PointLatLng> RuntimePoints => _runtimePoints.ToList();

        public List<GeoPoint> LinePoints
        {
            get { return _model.LinePoints.ToList(); }
            set
            {
                _model.LinePoints = value;
                _runtimePoints = GeoPointConverter.ToPointLatLngList(value);
                OnPropertyChanged(nameof(LinePoints));
                OnPropertyChanged(nameof(RuntimePoints));
            }
        }


        public bool IsDrawing
        {
            get => _isDrawing;
            set
            {
                if (_isDrawing != value)
                {
                    _isDrawing = value;
                    OnPropertyChanged(nameof(IsDrawing));
                }
            }
        }

        public bool IsClosedPath
        {
            get => _model.IsClosedPath;
            set
            {
                _model.IsClosedPath = value;
                OnPropertyChanged(nameof(IsClosedPath));
            }
        }

        public EnumLinePattern LinePattern
        {
            get => _model.LinePattern;
            set
            {
                _model.LinePattern = value;
                OnPropertyChanged(nameof(LinePattern));
            }
        }

        public double LineOpacity
        {
            get => _model.LineOpacity;
            set
            {
                _model.LineOpacity = value;
                OnPropertyChanged(nameof(LineOpacity));
            }
        }

        public bool ShowArrowHead
        {
            get => _model.ShowArrowHead;
            set
            {
                _model.ShowArrowHead = value;
                OnPropertyChanged(nameof(ShowArrowHead));
            }
        }

        /// <summary>
        /// 연결된 장치 ID
        /// </summary>
        public int LinkedDeviceGroup
        {
            get => _model.LinkedDeviceGroup;
            set
            {
                _model.LinkedDeviceGroup = value;
                OnPropertyChanged(nameof(LinkedDeviceGroup));
            }
        }

        /// <summary>
        /// 이벤트 상태 (애니메이션 트리거)
        /// </summary>
        public EnumEventStatus EventStatus
        {
            get => _model.EventStatus;
            set
            {
                _model.EventStatus = value;
                OnPropertyChanged(nameof(EventStatus));
            }
        }


        public double TotalDistance => GeoPointConverter.CalculateTotalDistance(_model.LinePoints);

        private bool _isDrawing = false;

        private List<PointLatLng> _runtimePoints = new List<PointLatLng>();
        #endregion
        #region - Attributes -
        #endregion
    }
}