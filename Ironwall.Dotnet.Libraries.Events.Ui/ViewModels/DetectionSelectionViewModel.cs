using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Windows.Media;

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
    /// <summary>
    /// 편집 버퍼(<see cref="Edit"/>)를 선택된 행 ViewModel에 적용한다 = DataGrid 항목이 곧바로 갱신되고,
    /// 값이 실제로 바뀐 행만 <c>IsEdited</c>(dirty)가 켜져 저장(PUT) 대상에 포함된다.
    /// <para>적용 경로는 <see cref="DetectionEditModel.ApplyTo"/> <b>하나</b>다 —
    /// 종전처럼 모델에 직접 대입하면 dirty가 켜지지 않아
    /// 저장 루프 <c>Where(vm =&gt; vm.IsEdited &amp;&amp; Id&gt;0)</c> 에서 행이 조용히 탈락한다
    /// (PRD Event_Edit_Save_Pipeline FR-01/02).</para>
    /// <para>상세(detail)는 이벤트마다 다른 계측값이라 <b>단일 선택에서만</b> 적용한다.
    /// 썸네일·objects 등 미편집 키는 모델에 그대로 남아, 저장 시
    /// <c>DtoToModelHelper.BuildDetectionDetail</c> 이 detail 전체를 재구성해 함께 보존한다
    /// (서버 PUT은 detail 통째 교체).</para>
    /// </summary>
    public void ApplyButton()
    {
        // Device는 읽기 전용(이벤트가 가리키는 장비는 변경 불가) — 편집 모델에 포함하지 않는다.
        foreach (var row in _selection)
            Edit.ApplyTo(row, includeDetail: IsSingle);

        NotifyDetailTexts();
        PanelViewModel?.RefreshSignalScale();   // 신호 편집이 목록 최대값을 바꿨을 수 있음(미니바 기준)
    }

    /* 공통값 계산 헬퍼 — 편집 필드는 DetectionEditModel.LoadFrom 이 담당하고,
       여기 남은 것은 편집 대상이 아닌 Device(참조 비교) 전용이다. */
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

    /// <summary>편집 버퍼를 현재 선택의 공통값으로 다시 시딩하고, 뷰 바인딩을 전부 갱신한다.</summary>
    public void RefreshAll()
    {
        Edit.LoadFrom(_selection as IReadOnlyList<DetectionEventViewModel> ?? _selection?.ToList());
        Device = CommonOrNullReference(_selection, DeviceProvider, _log);

        NotifyEditFields();

        // Device 읽기전용 표시값 갱신
        NotifyOfPropertyChange(nameof(DeviceNameText));
        NotifyOfPropertyChange(nameof(DeviceTypeText));
        NotifyOfPropertyChange(nameof(DeviceNumberText));
        NotifyOfPropertyChange(nameof(DeviceZoneText));
        NotifyOfPropertyChange(nameof(IsDetailEditable));
    }

    /// <summary>편집 모델을 감싸는 뷰 바인딩 속성 전체 갱신(뷰는 이 래퍼 이름으로 바인딩되어 있다).</summary>
    private void NotifyEditFields()
    {
        NotifyOfPropertyChange(nameof(MessageType));
        NotifyOfPropertyChange(nameof(Status));
        NotifyOfPropertyChange(nameof(Result));
        NotifyOfPropertyChange(nameof(DateTime));
        NotifyOfPropertyChange(nameof(SignalEdit));
        NotifyOfPropertyChange(nameof(AiModelEdit));
        NotifyOfPropertyChange(nameof(InferenceMsEdit));
        NotifyOfPropertyChange(nameof(FrameWidthEdit));
        NotifyOfPropertyChange(nameof(FrameHeightEdit));
    }

    /// <summary>편집 반영 후 읽기전용 표시 텍스트 재계산.</summary>
    private void NotifyDetailTexts()
    {
        NotifyOfPropertyChange(nameof(SignalText));
        NotifyOfPropertyChange(nameof(AiSummaryText));
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
    /// <summary>
    /// 이 속성창의 <b>편집 상태 정본</b>. 모든 입력 필드는 이 모델 하나에 모이며,
    /// 규칙도 하나다 — <c>null</c> = "변경 없음", 값 있음 = "이 값으로 변경".
    /// <para>아래 <c>MessageType</c>/<c>Result</c>/<c>SignalEdit</c>… 은 기존 뷰 바인딩 이름을 유지하기 위한
    /// <b>얇은 위임 래퍼</b>일 뿐이며, 상태는 전부 여기에 있다.</para>
    /// </summary>
    public DetectionEditModel Edit { get; } = new();

    // ── 편집 필드(뷰 바인딩 유지용 위임 래퍼) ─────────────────────────────
    public EnumEventType? MessageType
    {
        get => Edit.MessageType;
        set { Edit.MessageType = value; NotifyOfPropertyChange(); }
    }
    public EnumTrueFalse? Status
    {
        get => Edit.Status;
        set { Edit.Status = value; NotifyOfPropertyChange(); }
    }
    public EnumDetectionType? Result
    {
        get => Edit.Result;
        set { Edit.Result = value; NotifyOfPropertyChange(); }
    }
    public DateTime? DateTime
    {
        get => Edit.DateTime;
        set { Edit.DateTime = value; NotifyOfPropertyChange(); }
    }

    /// <summary>이벤트가 가리키는 장비 — 읽기 전용(무결성상 변경 불가)이라 편집 모델에 넣지 않는다.</summary>
    public IBaseDeviceModel? Device { get; set; }
    public DetectionEventPanelViewModel PanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; }

    /// <summary>편집 가능 여부. 대시보드 편집기=true(기본), 조치보고 다이얼로그=false(읽기 전용).
    /// 뷰의 편집 컨트롤 IsEnabled에만 바인딩 — ScrollViewer는 항상 활성이라 읽기 모드에서도 스크롤 가능.</summary>
    private bool _isEditable = true;
    public bool IsEditable { get => _isEditable; set { _isEditable = value; NotifyOfPropertyChange(() => IsEditable); } }

    // ── 장비(Device) 필수 정보 — 읽기 전용. 이벤트가 가리키는 장비는 변경 불가(무결성) → ComboBox 대신 텍스트로만 노출 ──
    public string DeviceNameText   => Device?.DeviceName is string n && n.Length > 0 ? n : "—";
    public string DeviceTypeText   => Device != null ? EnumKoreanMap.To(Device.DeviceType) : "—";
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

    // ── 탐지 상세(detail) 편집 필드(뷰 바인딩 유지용 위임 래퍼) ───────────
    // 단일 선택에서만 편집. 값을 비우면(null) 해당 키는 **변경하지 않는다**(기존 값 유지).
    // 썸네일·objects 는 편집 대상이 아니며 모델 값이 그대로 남아 저장 시 함께 보존된다.

    /// <summary>상세 편집 가능 여부 — 단일 선택에서만(멀티셀렉트는 대상 모호).</summary>
    public bool IsDetailEditable => IsSingle;

    /// <summary>신호 크기(detail.signal) 편집값.</summary>
    public int? SignalEdit { get => Edit.Signal; set { Edit.Signal = value; NotifyOfPropertyChange(); } }

    /// <summary>AI 모델명(detail.model) 편집값 — 공백만 입력하면 "변경 없음"으로 정규화된다.</summary>
    public string? AiModelEdit { get => Edit.AiModel; set { Edit.AiModel = value; NotifyOfPropertyChange(); } }

    /// <summary>추론 시간 ms(detail.inference_ms) 편집값.</summary>
    public int? InferenceMsEdit { get => Edit.InferenceMs; set { Edit.InferenceMs = value; NotifyOfPropertyChange(); } }

    /// <summary>프레임 가로 px(detail.frame_width) 편집값 — objects[].bbox 좌표 해석 기준.</summary>
    public int? FrameWidthEdit { get => Edit.FrameWidth; set { Edit.FrameWidth = value; NotifyOfPropertyChange(); } }

    /// <summary>프레임 세로 px(detail.frame_height) 편집값.</summary>
    public int? FrameHeightEdit { get => Edit.FrameHeight; set { Edit.FrameHeight = value; NotifyOfPropertyChange(); } }

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
    /// <summary>썸네일 절대 URI(detail.thumbnail) — 단일 선택일 때만. 상대경로는 API base 결합(공용 ThumbnailUriResolver).
    /// 없거나 조합 실패면 null → default. 실제 이미지 로드 실패(자체서명 인증서 등)는 View의 겹침 default가 폴백.</summary>
    public Uri? ThumbnailUri => IsSingle ? ThumbnailUriResolver.Resolve(FirstModel?.Thumbnail) : null;

    /// <summary>썸네일 이미지 후보가 있는지(단일 선택 + URL 존재). false면 View가 default 이미지를 표시.</summary>
    public bool HasThumbnail => ThumbnailUri != null;

    /// <summary>썸네일 이미지(단일 선택) — 원격 URL 1회 다운로드→OnLoad 캐시(<see cref="ThumbnailImageLoader"/>).
    /// 재열기/탭 전환에도 유지. 로드 전/부재/실패면 null → View의 겹침 default.</summary>
    public ImageSource? Thumbnail
    {
        get
        {
            var uri = ThumbnailUri;
            return uri is null ? null : ThumbnailImageLoader.GetOrLoad(uri, () => NotifyOfPropertyChange(nameof(Thumbnail)));
        }
    }
    #endregion
    #region - Attributes -
    private IList<DetectionEventViewModel> _selection;
    #endregion
}