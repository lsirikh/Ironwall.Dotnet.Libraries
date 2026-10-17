using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 목록 한 줄 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>드롭존이 가리키는 것 — 처리기는 행 뷰모델의 전체 모양을 알 필요가 없다.</summary>
public interface IServerAssignTarget
{
    int Id { get; }
    string Name { get; }
    EnumServerType? Type { get; }

    /// <summary>로컬 반영용 최소 모델(스피커의 <c>Server</c> 참조에 꽂는다).</summary>
    IServerModel AsModel();
}

/// <summary>
/// 서버 목록 한 줄 — 열은 이름 · 유형 · 주소 · 상태 · <b>마지막 변화</b> · 부대(스토리보드 L1344).
/// </summary>
/// <remarks>
/// <para><b>비밀번호는 이 타입에 들어오지 않는다.</b> 행은 목록·자동화·미리보기 PNG 에 그대로 노출되는 표면이다
/// (규칙 <c>security.md</c>). 계정 이름조차 열로 내지 않는다 — 상세의 "접속" 절에서만 보인다.</para>
/// <para>시각 글자는 <b>적재 시점</b>에 한 번 계산한다. 초 단위로 갱신하는 타이머를 두지 않는다 —
/// 뷰보다 오래 살아 싱글턴에 붙박이고, 무엇보다 <b>경과 시간은 생존 판정이 아니다</b>.</para>
/// </remarks>
public sealed class ServerRowViewModel : PropertyChangedBase, IServerRailItem, IServerAssignTarget
{
    public ServerRowViewModel(ServerAxisView view, EnumServerContract contract, IClock clock, string? unitName = null)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        if (clock is null) throw new ArgumentNullException(nameof(clock));

        Contract = contract;
        Type = ServerTypeCatalog.ParseType(view.TypeServer);

        Id = view.Id;
        Name = string.IsNullOrWhiteSpace(view.Name) ? $"(이름 없음 · {Id})" : view.Name;
        TypeText = ServerTypeCatalog.TypeLabel(Type, view.TypeServer);
        RailKey = ServerTypeCatalog.RailKeyOf(Type);
        AddressText = view.Port > 0 ? $"{view.IpAddress}:{view.Port}" : view.IpAddress;
        if (string.IsNullOrWhiteSpace(AddressText)) AddressText = "—";

        Status = ServerStatusRules.Classify(view, contract);
        StatusText = ServerStatusRules.StatusText(Status);
        StatusGlyph = ServerStatusRules.StatusGlyph(Status);
        LastChangeText = ServerStatusRules.LastChangeText(view, contract, clock);
        LastEditText = ServerStatusRules.LastEditText(view, clock);
        // 이름을 모르는 부대는 "#7" 같은 기호 대신 말로 적는다(U-18 D-7 7.12).
        UnitText = unitName ?? (view.UnitId is { } unitId ? $"부대 {unitId}번" : "—");
    }

    public ServerAxisView View { get; }
    public EnumServerContract Contract { get; }

    public int Id { get; }
    public string Name { get; }
    public EnumServerType? Type { get; }

    public string TypeText { get; }
    public string AddressText { get; }
    public string StatusText { get; }

    /// <summary>상태 글리프 — 색이 아니라 형태로 넷을 가른다(▲ 장애 · ◆ 경고 · ● 정상 · ○ 보고 없음).</summary>
    public string StatusGlyph { get; }

    /// <summary>"마지막 변화" — 마지막 <b>상태 전이</b>다. 6.3 에는 그 시각이 없어 "—" 다.</summary>
    public string LastChangeText { get; }

    /// <summary>"마지막 수정"(<c>updated_at</c>) — 상태와 무관한 사실이라 이름을 달리해 보인다.</summary>
    public string LastEditText { get; }

    public string UnitText { get; }

    /// <summary>선택 열 — 서버가 주지 않으면 "—".</summary>
    public string HostnameText => string.IsNullOrWhiteSpace(View.Hostname) ? "—" : View.Hostname!;

    public string RailKey { get; }
    public ServerStatusKind Status { get; }

    public bool IsFault => ServerStatusRules.IsFault(Status);
    public bool IsWarning => Status == ServerStatusKind.Warning;

    /// <summary>한 번도 보고가 없는가 — 정상·고장과 구분되는 셋째 값.</summary>
    public bool IsNotReported => Status == ServerStatusKind.NotReported;

    /// <summary>이 서버가 장비를 받을 수 있는 유형인가(끌기 전에도 보인다).</summary>
    public bool AcceptsDevices => ServerDropRules.AllowedServerTypes.Values.Any(list => Type is not null && list.Contains(Type.Value));

    /// <summary>
    /// 끄는 동안 이 행이 <b>왜</b> 못 받는지 — 끌기 시작에 payload 로 채워진다(고정 문구가 아니다).
    /// </summary>
    public string? DropBlockReason
    {
        get => _dropBlockReason;
        set { if (_dropBlockReason == value) return; _dropBlockReason = value; NotifyOfPropertyChange(); }
    }

    public IServerModel AsModel() => new ServerModel
    {
        Id = Id,
        Name = View.Name,
        Status = View.Status ?? string.Empty,
        IpAddress = View.IpAddress,
        Port = View.Port,
        Hostname = View.Hostname,
        // 계정 · 비밀번호는 옮기지 않는다 — 목록 행이 쥘 값이 아니다.
    };

    public override string ToString() => Name;

    private string? _dropBlockReason;
}
