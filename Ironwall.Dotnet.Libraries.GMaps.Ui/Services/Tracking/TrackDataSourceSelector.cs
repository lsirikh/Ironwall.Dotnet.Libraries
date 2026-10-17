using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : ITrackPointReader 데이터소스 토글 (로컬 DB / 서버 API)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="ITrackPointReader"/> 라이브 토글 — <c>FetchAsync</c>마다
/// <see cref="ITrackingSetupModel.DataSource"/>를 읽어 로컬(<see cref="TrackPointStore"/>) 또는
/// 서버 API(<see cref="TrackPointApiReader"/>)로 위임한다.
/// <para>설정 모델이 설정 UI와 동일 singleton이라 콤보 변경이 다음 Load에 즉시 반영(무재시작).
/// <see cref="_api"/>가 null(ApiSetup 미주입)이면 Local 폴백.</para>
/// </summary>
public sealed class TrackDataSourceSelector : ITrackPointReader
{
    #region - Ctors -
    public TrackDataSourceSelector(
        TrackPointStore local,
        TrackPointApiReader? api,
        ITrackingSetupModel model,
        ILogService? log = null)
    {
        _local = local;
        _api = api;
        _model = model;
        _log = log;
    }
    #endregion

    /// <inheritdoc/>
    public Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (_model.DataSource == EnumTrackDataSource.Api)
        {
            if (_api is not null)
                return _api.FetchAsync(cameraId, fromUtc, toUtc, ct);

            if (!_apiNullWarned)
            {
                _apiNullWarned = true;
                _log?.Warning("[TrackDataSourceSelector] DataSource=Api 이나 서버 ApiSetup 미주입 → 로컬 폴백. " +
                              "(Bootstrapper가 GMapUiModule에 trackingApiSetup 전달 필요)");
            }
        }
        return _local.FetchAsync(cameraId, fromUtc, toUtc, ct);
    }

    #region - Attributes -
    private readonly TrackPointStore _local;
    private readonly TrackPointApiReader? _api;
    private readonly ITrackingSetupModel _model;
    private readonly ILogService? _log;
    private bool _apiNullWarned;
    #endregion
}
