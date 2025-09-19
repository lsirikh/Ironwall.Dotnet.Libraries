using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/19/2025 8:06:49 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapInfraMarker : GMapBaseMarker<IInfraSymbolModel>, IInfraEditableMarker
    {
        #region - Ctors -
        /// <summary>
        /// 인프라 심볼 마커 생성자
        /// </summary>
        public GMapInfraMarker(ILogService log, IInfraSymbolModel infraModel)
            : base(log, infraModel)
        {
            _log?.Info($"GMapInfraMarker 생성: {infraModel.Title}, Type: {infraModel.BuildingType}");

            // 초기 설정
            ApplyBuildingTypeDefaults();
        }
        #endregion

        #region - Implementation of Interface -
        // IInfraEditableMarker는 Properties 섹션에서 구현
        #endregion

        #region - Overrides -
        /// <summary>
        /// 인프라 심볼 전용 컨트롤 생성
        /// </summary>
        protected override UIElement CreateMarkerControl()
        {
            var markerControl = new GMapMarkerInfraControl(this);
            _log?.Info($"=====================  GMapMarkerInfraControl 생성 호출됨   ===========================");
            return markerControl;
        }

        /// <summary>
        /// 마커 컨트롤 구성
        /// </summary>
        protected override void ConfigureMarkerControl(UIElement markerControl)
        {
            base.ConfigureMarkerControl(markerControl);

            // 인프라 마커 전용 설정
            if (markerControl is GMapMarkerInfraControl infraControl)
            {
                // 건물 타입에 따른 초기 설정
                ApplyBuildingTypeDefaults();

                // 층수에 따른 크기 조정
                if (FloorCount > 1)
                {
                    AdjustSizeByFloorCount();
                }
            }
        }

        /// <summary>
        /// Shape 크기 업데이트
        /// </summary>
        protected override void UpdateShapeSize(double width, double height)
        {
            base.UpdateShapeSize(width, height);

            // 층수에 비례한 높이 조정
            if (Shape is GMapMarkerInfraControl infraControl && FloorCount > 1)
            {
                var adjustedHeight = CalculateHeightByFloorCount(height);
                infraControl.Height = adjustedHeight;

                // 지하층이 있으면 추가 표시
                if (BasementFloorCount > 0)
                {
                    infraControl.Margin = new Thickness(0, 0, 0, -5); // 지하 표시를 위한 여백
                }
            }
        }
        #endregion

        #region - Binding Methods -
        // 필요시 추가 바인딩 메서드
        #endregion

        #region - Processes -
        /// <summary>
        /// 건물 타입별 기본값 적용
        /// </summary>
        private void ApplyBuildingTypeDefaults()
        {
            switch (BuildingType)
            {
                case EnumBuildingType.Factory:
                    // 공장 건물 기본 설정
                    if (Width == 0 || Height == 0)
                    {
                        Width = 60;
                        Height = 40;
                    }
                    if (FillColor == EnumColorType.Blue)
                    {
                        FillColor = EnumColorType.Gray;
                    }
                    break;

                default:
                    // 일반 건물 기본 설정
                    if (Width == 0 || Height == 0)
                    {
                        Width = 35;
                        Height = 35;
                    }
                    if (FillColor == EnumColorType.Blue)
                    {
                        FillColor = EnumColorType.Beige;
                    }
                    break;
            }
        }

        /// <summary>
        /// 층수에 따른 크기 조정
        /// </summary>
        private void AdjustSizeByFloorCount()
        {
            if (FloorCount <= 1) return;

            var baseHeight = Height;
            var adjustedHeight = CalculateHeightByFloorCount(baseHeight);

            if (Math.Abs(Height - adjustedHeight) > 0.01)
            {
                Height = adjustedHeight;
                UpdateShapeSize(Width, Height);
            }
        }

        /// <summary>
        /// 층수에 따른 높이 계산
        /// </summary>
        private double CalculateHeightByFloorCount(double baseHeight)
        {
            // 층당 10% 높이 증가, 최대 2배
            var multiplier = 1 + (FloorCount - 1) * 0.1;
            return Math.Min(baseHeight * multiplier, baseHeight * 2);
        }

        /// <summary>
        /// 건물 면적에 따른 크기 계산
        /// </summary>
        private void AdjustSizeByBuildingArea()
        {
            if (BuildingArea <= 0) return;

            // 면적에 따른 크기 조정 (√면적 비례)
            var scale = Math.Sqrt(BuildingArea / 100); // 100㎡ 기준
            var newWidth = Math.Max(20, Math.Min(100, 35 * scale));
            var newHeight = Math.Max(20, Math.Min(100, 35 * scale));

            if (Math.Abs(Width - newWidth) > 0.01 || Math.Abs(Height - newHeight) > 0.01)
            {
                Width = newWidth;
                Height = newHeight;
                UpdateShapeSize(Width, Height);
            }
        }
        #endregion

        #region - IHandles -
        // 이벤트 핸들러 필요시 추가
        #endregion

        #region - Properties -
        /// <summary>
        /// 건물 종류
        /// </summary>
        public EnumBuildingType BuildingType
        {
            get => _model.BuildingType;
            set
            {
                if (_model.BuildingType == value) return;

                _model.BuildingType = value;
                OnPropertyChanged(nameof(BuildingType));
                ApplyBuildingTypeDefaults();

                // UI 업데이트
                if (Shape is GMapMarkerInfraControl infraControl)
                {
                    infraControl.BuildingType = value;
                }
            }
        }

        /// <summary>
        /// 건물 용도
        /// </summary>
        public EnumBuildingUsage BuildingUsage
        {
            get => _model.BuildingUsage;
            set
            {
                if (_model.BuildingUsage == value) return;

                _model.BuildingUsage = value;
                OnPropertyChanged(nameof(BuildingUsage));

                // UI 업데이트
                if (Shape is GMapMarkerInfraControl infraControl)
                {
                    infraControl.BuildingUsage = value;
                }
            }
        }

        /// <summary>
        /// 지상 층수
        /// </summary>
        public int FloorCount
        {
            get => _model.FloorCount;
            set
            {
                if (_model.FloorCount == value) return;

                _model.FloorCount = value;
                OnPropertyChanged(nameof(FloorCount));
                AdjustSizeByFloorCount();

                // UI 업데이트
                if (Shape is GMapMarkerInfraControl infraControl)
                {
                    infraControl.FloorCount = value;
                }
            }
        }

        /// <summary>
        /// 지하 층수
        /// </summary>
        public int BasementFloorCount
        {
            get => _model.BasementFloorCount;
            set
            {
                if (_model.BasementFloorCount == value) return;

                _model.BasementFloorCount = value;
                OnPropertyChanged(nameof(BasementFloorCount));

                // UI 업데이트
                if (Shape is GMapMarkerInfraControl infraControl)
                {
                    infraControl.BasementFloorCount = value;
                }
            }
        }

        /// <summary>
        /// 건물 면적 (㎡)
        /// </summary>
        public double BuildingArea
        {
            get => _model.BuildingArea;
            set
            {
                if (Math.Abs(_model.BuildingArea - value) < 0.01) return;

                _model.BuildingArea = value;
                OnPropertyChanged(nameof(BuildingArea));
                AdjustSizeByBuildingArea();

                // UI 업데이트
                if (Shape is GMapMarkerInfraControl infraControl)
                {
                    infraControl.BuildingArea = value;
                }
            }
        }

        /// <summary>
        /// 총 층수 (지상 + 지하)
        /// </summary>
        public int TotalFloorCount => FloorCount + BasementFloorCount;

        /// <summary>
        /// 건물 정보 요약
        /// </summary>
        public string BuildingInfo
        {
            get
            {
                var info = $"{Title} ({BuildingType})";
                if (FloorCount > 1 || BasementFloorCount > 0)
                {
                    info += $" - ";
                    if (BasementFloorCount > 0)
                    {
                        info += $"B{BasementFloorCount}/";
                    }
                    info += $"F{FloorCount}";
                }
                if (BuildingArea > 0)
                {
                    info += $" ({BuildingArea:N0}㎡)";
                }
                return info;
            }
        }
        #endregion

        #region - Attributes -
        // 필요시 추가 필드
        #endregion

        #region - Public Methods -
        /// <summary>
        /// 건물 정보 업데이트
        /// </summary>
        public void UpdateBuildingInfo(EnumBuildingType type, EnumBuildingUsage usage,
            int floors, int basementFloors = 0, double area = 0)
        {
            BuildingType = type;
            BuildingUsage = usage;
            FloorCount = floors;
            BasementFloorCount = basementFloors;
            BuildingArea = area;

            _log?.Info($"건물 정보 업데이트: {BuildingInfo}");
        }

        /// <summary>
        /// 건물 상태 확인
        /// </summary>
        public string GetBuildingStatus()
        {
            return $"건물: {Title}, 타입: {BuildingType}, 용도: {BuildingUsage}, " +
                   $"층수: {TotalFloorCount}층, 면적: {BuildingArea:N0}㎡";
        }
        #endregion
    }
}