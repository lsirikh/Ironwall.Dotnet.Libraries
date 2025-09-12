using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/12/2025 11:22:03 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// GMap 라인 마커 구현
    /// </summary>
    public class GMapLineMarker : GMapBaseMarker<ILineSymbolModel>, ILineEditableMarker
    {
       

        public GMapLineMarker(ILogService log, LineSymbolModel symbolModel)
            : base(log, symbolModel)
        {
            // 모델의 GeoPoint를 런타임 PointLatLng로 변환
            _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);

            // 시작점이 있으면 마커 위치 설정
            if (_runtimePoints.Count > 0)
            {
                Position = _runtimePoints[0];
            }

            // 카테고리 설정 (라인은 기본적으로 AREA_BOUNDARY 카테고리)
            Category = EnumMarkerCategory.AREA_BOUNDARY;
        }

        #region IEditableLineMarker 구현

        

        public void AddPoint(PointLatLng point)
        {
            _runtimePoints.Add(point);

            // 첫 번째 점이면 마커 위치 설정
            if (_runtimePoints.Count == 1)
            {
                Position = point;
                _model.Latitude = point.Lat;
                _model.Longitude = point.Lng;
            }

            // 모델 동기화
            _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
            OnPropertyChanged(nameof(RuntimePoints));
            OnPropertyChanged(nameof(LinePoints));
            OnPropertyChanged(nameof(TotalDistance));
        }

        public void UpdatePoint(int index, PointLatLng newPoint)
        {
            if (index >= 0 && index < _runtimePoints.Count)
            {
                _runtimePoints[index] = newPoint;

                // 첫 번째 점 업데이트시 마커 위치도 업데이트
                if (index == 0)
                {
                    Position = newPoint;
                    _model.Latitude = newPoint.Lat;
                    _model.Longitude = newPoint.Lng;
                }

                // 모델 동기화
                _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
                _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
                OnPropertyChanged(nameof(RuntimePoints));
                OnPropertyChanged(nameof(LinePoints));
                OnPropertyChanged(nameof(TotalDistance));
            }
        }

        public void RemoveLastPoint()
        {
            if (_runtimePoints.Count > 0)
            {
                _runtimePoints.RemoveAt(_runtimePoints.Count - 1);
                _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
                _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
                OnPropertyChanged(nameof(RuntimePoints));
                OnPropertyChanged(nameof(LinePoints));
                OnPropertyChanged(nameof(TotalDistance));
            }
        }

        public void StartDrawing()
        {
            _isDrawing = true;
            _runtimePoints.Clear();
            _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
            OnPropertyChanged(nameof(IsDrawing));
            OnPropertyChanged(nameof(RuntimePoints));
        }

        public void FinishDrawing()
        {
            _isDrawing = false;
            OnPropertyChanged(nameof(IsDrawing));
            _log?.Info($"라인 그리기 완료: {_model.Title}, {_runtimePoints.Count}개 포인트, 총 거리: {TotalDistance:F1}m");
        }

        public void CancelDrawing()
        {
            _isDrawing = false;
            _runtimePoints.Clear();
            _runtimePoints = GeoPointConverter.ToPointLatLngList(_model.LinePoints);
            _model.LinePoints = GeoPointConverter.ToGeoPointList(_runtimePoints);
            OnPropertyChanged(nameof(IsDrawing));
            OnPropertyChanged(nameof(RuntimePoints));
            OnPropertyChanged(nameof(LinePoints));
            _log?.Info($"라인 그리기 취소됨: {_model.Title}");
        }

        #endregion

        #region 오버라이드 메서드

        protected override UIElement CreateMarkerControl()
        {
            return new GMapMarkerLineControl(this);
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
            base.InitializeCommands();

            // 라인 전용 명령어 추가 가능
            // AddPointCommand = new RelayCommand<PointLatLng>(ExecuteAddPoint);
            // RemovePointCommand = new RelayCommand<int>(ExecuteRemovePoint);
        }

        #endregion

        /// <summary>
        /// 라인 검증 (최소 2개 포인트 필요)
        /// </summary>
        public bool IsValid()
        {
            return _runtimePoints.Count >= 2;
        }

        /// <summary>
        /// 특정 포인트에서 가장 가까운 라인 세그먼트 찾기
        /// </summary>
        public int FindClosestSegment(PointLatLng point, out double distance)
        {
            distance = double.MaxValue;
            int closestSegment = -1;

            if (_runtimePoints.Count < 2)
                return closestSegment;

            for (int i = 0; i < _runtimePoints.Count - 1; i++)
            {
                var segmentDistance = CalculatePointToSegmentDistance(point, _runtimePoints[i], _runtimePoints[i + 1]);
                if (segmentDistance < distance)
                {
                    distance = segmentDistance;
                    closestSegment = i;
                }
            }

            return closestSegment;
        }

        /// <summary>
        /// 점과 선분 사이의 최단 거리 계산
        /// </summary>
        private double CalculatePointToSegmentDistance(PointLatLng point, PointLatLng segmentStart, PointLatLng segmentEnd)
        {
            // 간단한 유클리드 거리 계산 (실제로는 대원 거리 사용 권장)
            var dx = segmentEnd.Lng - segmentStart.Lng;
            var dy = segmentEnd.Lat - segmentStart.Lat;

            if (dx == 0 && dy == 0)
            {
                // 선분의 시작점과 끝점이 같음
                dx = point.Lng - segmentStart.Lng;
                dy = point.Lat - segmentStart.Lat;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            var t = ((point.Lng - segmentStart.Lng) * dx + (point.Lat - segmentStart.Lat) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            var projection = new PointLatLng(
                segmentStart.Lat + t * dy,
                segmentStart.Lng + t * dx
            );

            dx = point.Lng - projection.Lng;
            dy = point.Lat - projection.Lat;
            return Math.Sqrt(dx * dx + dy * dy);
        }


        public List<PointLatLng> RuntimePoints => _runtimePoints.ToList();

        public List<GeoPoint> LinePoints => _model.LinePoints.ToList();

        public bool IsDrawing => _isDrawing;

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

        public double TotalDistance => GeoPointConverter.CalculateTotalDistance(_model.LinePoints);



        private bool _isDrawing = false;
        private List<PointLatLng> _runtimePoints = new List<PointLatLng>();
    }
}