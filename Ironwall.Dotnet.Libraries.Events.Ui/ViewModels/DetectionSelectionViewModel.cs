using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 8:33:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionSelectionViewModel : BasePanelViewModel
{
    #region - Ctors -
    public DetectionSelectionViewModel(IList<DetectionEventViewModel> selection)
    {
        PanelViewModel = IoC.Get<DetectionEventPanelViewModel>();
        DeviceProvider = IoC.Get<DeviceProvider>();
        _selection = selection;
        RefreshAll();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void ApplyButton()
    {
        foreach (var item in _selection)
        {
            item.MessageType = MessageType ?? item.MessageType;
            // Device는 읽기 전용(이벤트가 가리키는 장비는 변경 불가) — 적용에서 제외
            item.Result = Result ?? item.Result;
            item.Status = Status ?? item.Status;
            item.DateTime = DateTime ?? item.DateTime;
        }
    }

    /* 공통값 계산 헬퍼 */
    //int 형 및 Enum 타입의 형식 비교
    private static T? CommonOrNullValue<T>(IEnumerable<DetectionEventViewModel> list, Func<IDetectionEventModel, T> selector) where T : struct
    {
        try
        {
            if (list == null || !list.Any()) return null;

            var firstModel = list.FirstOrDefault()?.Model as IDetectionEventModel;
            if (firstModel == null) return null;

            T firstValue = selector(firstModel);

            bool allSame = list
                .Select(vm => vm.Model as IDetectionEventModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

            return allSame ? firstValue : (T?)null;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private static T? CommonOrNullString<T>(IEnumerable<DetectionEventViewModel> list, Func<IDetectionEventModel, T> selector) where T : class?
    {
        try
        {
            if (!list.Any()) return null;

            var models = list.Select(x => x.Model as IDetectionEventModel).ToList();
            var firstModel = list.FirstOrDefault()?.Model as IDetectionEventModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);

            return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static IBaseDeviceModel? CommonOrNullReference(IEnumerable<DetectionEventViewModel> list, DeviceProvider devices, ILogService? log)
    {
        if (!list.Any()) return null;

        var first = list.First()?.Device;
        if (first == null) return null;

        var ret = list
            .Where(m => m?.Device != null)
            .All(m => ReferenceEquals(m!.Device, first))
        ? first
        : null;

        if (ret == null)
            return null;
        else
            return devices.Where(entity => entity.Id == ret.Id)
                .Where(entity => entity.DeviceName == ret.DeviceName).FirstOrDefault();
    }

    public void RefreshAll()
    {
        MessageType = CommonOrNullValue(_selection, m => m.MessageType);
        Device = CommonOrNullReference(_selection, DeviceProvider, _log);
        Result = CommonOrNullValue(_selection, m => m.Result);
        Status = CommonOrNullValue(_selection, m => m.Status);
        DateTime = CommonOrNullValue(_selection, m => m.DateTime);

        // Device 읽기전용 표시값 갱신
        NotifyOfPropertyChange(nameof(DeviceNameText));
        NotifyOfPropertyChange(nameof(DeviceTypeText));
        NotifyOfPropertyChange(nameof(DeviceNumberText));
        NotifyOfPropertyChange(nameof(DeviceZoneText));
    }


    #region helpers
    private IBaseDeviceModel? ResolveDevice(IBaseDeviceModel? dev)
        => dev == null
           ? null
           : DeviceProvider.FirstOrDefault(d => d.Id == dev.Id);
    #endregion
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public EnumEventType? MessageType { get; set; }
    public IBaseDeviceModel? Device { get; set; }
    public EnumTrueFalse? Status { get; set; }
    public EnumDetectionType? Result { get; set; }
    public DateTime? DateTime { get; set; }
    public DetectionEventPanelViewModel PanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; }

    /// <summary>편집 가능 여부. 대시보드 편집기=true(기본), 조치보고 다이얼로그=false(읽기 전용).
    /// 뷰의 편집 컨트롤 IsEnabled에만 바인딩 — ScrollViewer는 항상 활성이라 읽기 모드에서도 스크롤 가능.</summary>
    private bool _isEditable = true;
    public bool IsEditable { get => _isEditable; set { _isEditable = value; NotifyOfPropertyChange(() => IsEditable); } }

    // ── 장비(Device) 필수 정보 — 읽기 전용. 이벤트가 가리키는 장비는 변경 불가(무결성) → ComboBox 대신 텍스트로만 노출 ──
    public string DeviceNameText   => Device?.DeviceName is string n && n.Length > 0 ? n : "—";
    public string DeviceTypeText   => Device != null ? Device.DeviceType.ToString() : "—";
    public string DeviceNumberText => Device != null ? Device.DeviceNumber.ToString() : "—";

    /// <summary>장비 소속 구역(그룹) 이름 — DeviceGroups(Id 목록)를 DeviceGroupProvider로 이름 변환(BaseDeviceViewModel 패턴).</summary>
    public string DeviceZoneText
    {
        get
        {
            var groups = Device?.DeviceGroups;
            if (groups == null || groups.Count == 0) return "—";
            try
            {
                var provider = IoC.Get<DeviceGroupProvider>();
                return string.Join(", ", groups.Select(id =>
                    provider.OfType<DeviceGroupModel>().FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
            }
            catch { return string.Join(", ", groups); }
        }
    }

    // ── 탐지 상세(detail) 읽기 전용 표시 (Detection_Signal_History) ──
    // 계측값이라 편집 대상 아님. detail은 이벤트별 값이라 단일 선택일 때만 표시 —
    // 멀티셀렉트에서 첫 항목 값을 대표값처럼 보여 오인시키지 않도록(편집필드 CommonOrNull 규칙과 일관).
    private bool IsSingle => _selection is { Count: 1 };
    private IDetectionEventModel? FirstModel => IsSingle ? _selection[0]?.Model as IDetectionEventModel : null;

    private const string MULTI = "(다중 선택)";

    /// <summary>신호 크기(detail.signal) — null/0(AI)은 "—", 멀티셀렉트는 "(다중 선택)".</summary>
    public string SignalText => !IsSingle ? MULTI : (FirstModel?.Signal is int s and > 0 ? s.ToString("N0") : "—");

    /// <summary>AI 추론 요약 — "yolov8n · 45ms".</summary>
    public string AiSummaryText
    {
        get
        {
            if (!IsSingle) return MULTI;
            var m = FirstModel;
            if (m == null || (string.IsNullOrEmpty(m.AiModel) && m.InferenceMs is null)) return "—";
            var inference = m.InferenceMs is int ms ? $"{ms}ms" : "-";
            return $"{(string.IsNullOrEmpty(m.AiModel) ? "-" : m.AiModel)} · {inference}";
        }
    }

    /// <summary>AI 탐지 객체 요약 — "person 95% [100,200,50,100]".</summary>
    public string ObjectsText
        => !IsSingle ? MULTI
            : FirstModel?.Objects is { Count: > 0 } objs
                ? string.Join(", ", objs.Select(o =>
                    $"{o.Label} {o.Confidence:P0}" + (o.Bbox is { Count: > 0 } b ? $" [{string.Join(",", b)}]" : string.Empty)))
                : "—";

    /// <summary>썸네일 URL(detail.thumbnail).</summary>
    public string ThumbnailText => !IsSingle ? MULTI : (string.IsNullOrEmpty(FirstModel?.Thumbnail) ? "—" : FirstModel!.Thumbnail!);

    /// <summary>썸네일 절대 URI — 상대경로(/api/thumbnails/…)면 API base host를 결합. 없거나 조합 실패면 null(→ default 이미지).
    /// 단일 선택일 때만. 실제 이미지 로드 실패(자체서명 인증서 등)는 View의 ImageFailed가 default로 폴백.</summary>
    public Uri? ThumbnailUri
    {
        get
        {
            if (!IsSingle) return null;
            var raw = FirstModel?.Thumbnail;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // 절대 URL이면 그대로
            if (Uri.TryCreate(raw, UriKind.Absolute, out var abs)) return abs;

            // 상대경로면 API base host와 결합 (base 미조달 시 null → default)
            var baseUrl = ResolveApiBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;
            return Uri.TryCreate(new Uri(baseUrl!), raw, out var combined) ? combined : null;
        }
    }

    /// <summary>썸네일 이미지 후보가 있는지(단일 선택 + URL 존재). false면 View가 default 이미지를 표시.</summary>
    public bool HasThumbnail => ThumbnailUri != null;

    /// <summary>API base host 추출 — appsettings Url(예: https://host:8136/api)에서 scheme+host+port. IoC 미등록 시 null.</summary>
    private static string? ResolveApiBaseUrl()
    {
        try
        {
            var setup = IoC.Get<Ironwall.Dotnet.Libraries.Api.Models.IApiSetupModel>();
            if (setup?.Url is not string url || string.IsNullOrWhiteSpace(url)) return null;
            return Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Authority}" : null;
        }
        catch { return null; }   // IoC 미등록/해석 실패 — 상대 썸네일은 default로 폴백
    }
    #endregion
    #region - Attributes -
    private IList<DetectionEventViewModel> _selection;
    #endregion
}