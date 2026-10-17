using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/20/2025 10:32:59 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MarkerFactory : IMarkerFactory
{
    private readonly ILogService? _log;
    private readonly DeviceProvider? _deviceProvider;

    public MarkerFactory(ILogService log, DeviceProvider deviceProvider)
    {
        _log = log;
        _deviceProvider = deviceProvider;
    }

    public IEditableMarker CreateMarker(ISymbolModel symbolModel)
    {
        if (symbolModel == null)
            throw new ArgumentNullException(nameof(symbolModel));

        try
        {
            // 타입 체크를 더 명확하게
            return symbolModel switch
            {
                IGeometricSymbolModel geometricSymbol => CreateGeometricMarker(geometricSymbol),
                IPidsSymbolModel pidsSymbol => CreatePidsMarker(pidsSymbol),
                IMilitarySymbolModel militarySymbol => CreateMilitaryMarker(militarySymbol),
                IInfraSymbolModel infraSymbol => CreateInfraMarker(infraSymbol),
                IPidsGroupSymbolModel pidsGroupSymbol => CreatePidsGroupMarker(pidsGroupSymbol),
                ILineSymbolModel lineSymbol => CreateLineMarker(lineSymbol),
                _ => CreateCustomMarker(symbolModel)
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 생성 실패: {ex.Message}");
            // 폴백으로 기본 마커 생성
            return new GMapCustomMarker(_log!, symbolModel);
        }
    }

    private GMapPidsGroupMarker CreatePidsGroupMarker(IPidsGroupSymbolModel pidsGroupSymbol)
    {
        //_log?.Info($"GMapPidsGroupMarker 생성: {pidsGroupSymbol.Title}");
        return new GMapPidsGroupMarker(_log!, pidsGroupSymbol);
    }

    private GMapInfraMarker CreateInfraMarker(IInfraSymbolModel symbol)
    {
        //_log?.Info($"GMapInfraMarker 생성: {symbol.Title}");
        return new GMapInfraMarker(_log!, symbol);
    }

    private GMapLineMarker CreateLineMarker(ILineSymbolModel symbol)
    {
        //_log?.Info($"GMapLineMarker 생성: {symbol.Title}");
        return new GMapLineMarker(_log!, symbol);
    }

    private GMapMilitarySymbolMarker CreateMilitaryMarker(IMilitarySymbolModel symbol)
    {
        //_log?.Info($"GMapMilitarySymbolMarker 생성: {symbol.Title}, UnitType: {symbol.UnitType}");
        return new GMapMilitarySymbolMarker(_log!, symbol);
    }

    private GMapPidsMarker CreatePidsMarker(IPidsSymbolModel symbol)
    {
        // DB 로드 시 LinkedDeviceId만 존재하고 LinkedDevice는 null인 경우 바인딩
        if (symbol.LinkedDevice == null && symbol.LinkedDeviceId > 0 && _deviceProvider != null)
        {
            var allDevices = _deviceProvider.ToList();

            // 중요: DeviceType에 따라 필터링된 목록에서만 검색
            // LinkedDeviceId=1이 Controller와 Fence 모두에 존재할 수 있으므로
            // 해당 심볼의 DeviceType과 일치하는 디바이스만 검색해야 함
            var filteredDevices = FilterDevicesByType(allDevices, symbol.DeviceType).ToList();

            //_log?.Info($"GMapPidsMarker - DeviceProvider 전체: {allDevices.Count}개, 필터링(DeviceType={symbol.DeviceType}): {filteredDevices.Count}개, 검색 대상 LinkedDeviceId={symbol.LinkedDeviceId}");

            symbol.BindToDeviceList(filteredDevices);

            if (symbol.LinkedDevice == null)
            {
                _log?.Warning($"GMapPidsMarker - LinkedDevice 바인딩 실패! DeviceId={symbol.LinkedDeviceId}가 필터링된 목록에 없음");
            }
            else
            {
                //_log?.Info($"GMapPidsMarker - LinkedDevice 바인딩 성공: DeviceId={symbol.LinkedDeviceId}, Device={symbol.LinkedDevice.DeviceName}");
            }
        }

        //_log?.Info($"GMapPidsMarker 생성: {symbol.Title}, DeviceType: {symbol.DeviceType}, LinkedDeviceId: {symbol.LinkedDeviceId}");
        return new GMapPidsMarker(_log!, symbol);
    }

    private IEnumerable<IBaseDeviceModel> FilterDevicesByType(
        IEnumerable<IBaseDeviceModel> devices,
        EnumDeviceType targetType)
    {
        return DeviceFilterHelper.FilterDevicesByType(devices, targetType);
    }

    private GMapGeometricMarker CreateGeometricMarker(IGeometricSymbolModel symbol)
    {
        //_log?.Info($"GMapGeometricMarker 생성: {symbol.Title}, ShapeType: {symbol.ShapeType}");
        return new GMapGeometricMarker(_log!, symbol);
    }

    private GMapCustomMarker CreateCustomMarker(ISymbolModel symbol)
    {
        //_log?.Info($"GMapCustomMarker 생성: {symbol.Title}");
        return new GMapCustomMarker(_log!, symbol);
    }

    /// <summary>
    /// ImageModel로부터 GMapImageMarker를 생성합니다.
    /// </summary>
    /// <param name="imageModel">이미지 모델</param>
    /// <returns>GMapImageMarker 인스턴스</returns>
    public GMapImageMarker CreateImageMarker(IImageModel imageModel)
    {
        if (imageModel == null)
            throw new ArgumentNullException(nameof(imageModel));

        //_log?.Info($"GMapImageMarker 생성: {imageModel.Title}");
        return new GMapImageMarker(_log!, imageModel);
    }
}
