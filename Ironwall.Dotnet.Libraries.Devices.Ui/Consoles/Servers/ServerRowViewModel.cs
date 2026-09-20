using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;

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
/// <para>"마지막 변화" 는 <b>적재 시점</b>에 한 번 계산한다. 초 단위로 갱신하는 타이머를 두지 않는다 —
/// 그 타이머는 뷰보다 오래 살아 싱글턴 뷰모델에 붙박이고, 무엇보다 <b>경과 시간은 생존 판정이 아니다</b>.</para>
/// </remarks>
public sealed class ServerRowViewModel : PropertyChangedBase, IServerRailItem, IServerAssignTarget
{
    public ServerRowViewModel(ServerListEntry entry, IClock clock)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        if (clock is null) throw new ArgumentNullException(nameof(clock));

        Dto = entry.Dto;
        Type = entry.Type;
        RawTypeServer = entry.RawTypeServer;

        Id = entry.Dto.Id;
        Name = string.IsNullOrWhiteSpace(entry.Dto.Name) ? $"(이름 없음 · {Id})" : entry.Dto.Name;
        TypeText = ServerTypeCatalog.TypeLabel(entry.Type, entry.RawTypeServer);
        RailKey = ServerTypeCatalog.RailKeyOf(entry.Type);
        AddressText = entry.Dto.Port > 0 ? $"{entry.Dto.IpAddress}:{entry.Dto.Port}" : entry.Dto.IpAddress;
        if (string.IsNullOrWhiteSpace(AddressText)) AddressText = "—";

        LastChangeAt = ServerStatusRules.LastChangeOf(entry.Dto.UpdatedAt, entry.Dto.CreatedAt);
        Status = ServerStatusRules.Classify(entry.Dto.Status, LastChangeAt);
        StatusText = ServerStatusRules.StatusText(Status);
        LastChangeText = ServerStatusRules.LastChangeText(LastChangeAt, clock);
        UnitText = entry.UnitName ?? (entry.Dto.UnitId is { } unitId ? $"#{unitId}" : "—");
    }

    public ServerDto Dto { get; }
    public int Id { get; }
    public string Name { get; }
    public EnumServerType? Type { get; }
    public string RawTypeServer { get; }

    public string TypeText { get; }
    public string AddressText { get; }
    public string StatusText { get; }

    /// <summary>"마지막 변화" — 마지막 <b>전이</b>이지 마지막 수신이 아니다.</summary>
    public string LastChangeText { get; }
    public DateTimeOffset? LastChangeAt { get; }
    public string UnitText { get; }

    /// <summary>선택 열 — 서버가 주지 않으면 "—".</summary>
    public string HostnameText => string.IsNullOrWhiteSpace(Dto.Hostname) ? "—" : Dto.Hostname!;

    public string RailKey { get; }
    public ServerStatusKind Status { get; }

    /// <summary>목록의 상태 칸이 <c>▲</c> 를 붙이는가(색이 아니라 형태).</summary>
    public bool IsFault => ServerStatusRules.IsFault(Status);

    /// <summary>한 번도 보고가 없는가 — 정상·고장과 구분되는 셋째 값.</summary>
    public bool IsNotReported => Status == ServerStatusKind.NotReported;

    /// <summary>이 서버가 장비를 받는가 — 끌기 전에도 행이 그 사실을 보인다.</summary>
    public bool AcceptsDevices => ServerDropRules.Accepts(Type, EnumDeviceCategory.Speaker);

    public IServerModel AsModel() => new ServerModel
    {
        Id = Id,
        CategoryId = Dto.CategoryId,
        Name = Dto.Name,
        Status = Dto.Status,
        IpAddress = Dto.IpAddress,
        Port = Dto.Port,
        Hostname = Dto.Hostname,
        // 계정 · 비밀번호는 옮기지 않는다 — 목록 행이 쥘 값이 아니다.
    };

    public override string ToString() => Name;
}
