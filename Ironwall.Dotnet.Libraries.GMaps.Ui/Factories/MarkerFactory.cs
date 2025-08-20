using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

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

    public MarkerFactory(ILogService log)
    {
        _log = log;
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

    private GMapGeometricMarker CreateGeometricMarker(IGeometricSymbolModel geometricSymbol)
    {
        _log?.Info($"GMapGeometricMarker 생성: {geometricSymbol.Title}, ShapeType: {geometricSymbol.ShapeType}");
        return new GMapGeometricMarker(_log!, geometricSymbol);
    }

    private GMapCustomMarker CreateCustomMarker(ISymbolModel symbolModel)
    {
        _log?.Info($"GMapCustomMarker 생성: {symbolModel.Title}");
        return new GMapCustomMarker(_log!, symbolModel);
    }
}
