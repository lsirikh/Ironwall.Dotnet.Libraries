using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Reports.Api.Modules;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Modules;

/// <summary>
/// Reports.Ui DI 등록 — ReportApiModule(Bearer 파이프라인+서비스) + 보고서 콘솔/탭/미리보기 VM.
/// 외부 Monitoring 솔루션 Bootstrapper 가 이 모듈 1줄 + OpenReportConsoleMessageModel 핸들러를 추가한다.
/// </summary>
public class ReportUiModule : Module
{
    #region - Ctors -
    /// <param name="apiSetup">GOP 서버 설정(URL) — ReportApiModule 주입(필수).</param>
    public ReportUiModule(IApiSetupModel apiSetup, ILogService? log = default, int count = 100, string name = "ReportApi")
    {
        _apiSetup = apiSetup;
        _log = log;
        _count = count;
        _name = name;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // apiSetup(IApiSetupModel) → 구체 ApiSetupModel 로 복제(EventUiModule 패턴).
            // 앱은 SetupModel(IApiSetupModel 구현, ApiSetupModel 아님)을 넘기므로 하드캐스트 금지 — 복사 생성자로 수용.
            var setup = new ApiSetupModel(_apiSetup);

            // 1) 보고서 API(Bearer 파이프라인 + ReportApiService)
            builder.RegisterModule(new ReportApiModule(_log, setup, name: _name, count: _count));

            // 2) 콘솔/탭/미리보기 VM
            builder.RegisterType<ReportListViewModel>().SingleInstance();
            builder.RegisterType<ReportCreateViewModel>().SingleInstance();
            builder.RegisterType<ReportTemplateViewModel>().SingleInstance();
            builder.RegisterType<ReportTemplateEditViewModel>().SingleInstance();
            builder.RegisterType<ReportPreviewViewModel>().SingleInstance();
            builder.RegisterType<ReportConsoleViewModel>().SingleInstance();

            _log?.Info($"[{nameof(ReportUiModule)}] Module loaded successfully.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReportUiModule)}] Failed to load: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly IApiSetupModel _apiSetup;
    private readonly ILogService? _log;
    private readonly int _count;
    private readonly string _name;
    #endregion
}
