using Ironwall.Dotnet.Libraries.Api.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Servers;
/****************************************************************************
   Purpose      : 서버 판본별 읽기 투영 · 쓰기 본문 (N-12 · 계약 6.3 ↔ 7.0/8.0)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 판본과 무관한 <b>서버 한 대의 읽기 투영</b> — 6.3 의 평면 스칼라와 7.0+ 의 축을 한 모양으로 모은다.
/// </summary>
/// <remarks>
/// <para><b>왜 새 타입인가</b> — <c>Messages</c> 의 <c>ServerDto</c> 는 6.3 평면 모양이고(<c>category_id</c>·
/// <c>port</c>·<c>hostname</c>·<c>threshold_config</c>), 7.0 에서 그 키들은 <b>요청에 보내면 422</b> ·
/// 응답에서는 <c>connection</c>·<c>server_config</c> 축으로 옮겨졌다
/// (서버 <c>app/schemas/server.py:59-66</c> <c>SERVER_REMOVED_FIELDS</c> ·
/// <c>app/schemas/server.py:759-800</c> <c>ServerResponse</c>). <c>Messages</c> 는 이 노드 범위 밖이라
/// 여기(=API 계층)에 <b>새 타입</b>을 둔다.</para>
/// <para><b>키의 부재를 값과 구분한다.</b> <see cref="HasStatusKey"/>·<see cref="HasStatusObservedAtKey"/> 가 그것이다 —
/// <c>ServerDto.Status</c> 는 기본값이 <c>"NORMAL"</c> 이라 "키가 없었다" 를 영원히 알 수 없었고,
/// 그 때문에 한 번도 보고가 없는 서버가 <b>정상으로 보였다</b>.</para>
/// </remarks>
public sealed class ServerAxisView
{
    public int Id { get; init; }

    /// <summary>7.0+ 판별자 문자열(<c>category_server</c>). 6.3 에서는 분류 사전에서 해석해 채운다.</summary>
    public string TypeServer { get; init; } = string.Empty;

    /// <summary>6.3 의 분류 id. 7.0+ 응답에는 없다(0).</summary>
    public int CategoryId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>운용 의도. 6.3 응답에 없으면 <c>true</c>.</summary>
    public bool IsEnable { get; init; } = true;

    /// <summary>소속 부대 id — 8.0 이상에서만 온다.</summary>
    public int? UnitId { get; init; }

    /// <summary>관측 상태 문자열 <b>원문</b>. 키가 없었으면 <c>null</c>.</summary>
    public string? Status { get; init; }

    /// <summary>응답에 <c>status</c> 키가 <b>있었는가</b>.</summary>
    public bool HasStatusKey { get; init; }

    /// <summary>마지막 <b>상태 전이</b>의 관측 시각(7.0+ <c>status_observed_at</c>). 없으면 보고 없음.</summary>
    public string? StatusObservedAt { get; init; }

    /// <summary>응답에 <c>status_observed_at</c> 키가 있었는가(7.0+ 는 늘 있다 — 값이 null 일 뿐).</summary>
    public bool HasStatusObservedAtKey { get; init; }

    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    public string? Hostname { get; init; }
    public string? UserName { get; init; }

    /// <summary>접속 절을 <b>받았는가</b>. 목록 <c>basic</c> 은 자격증명만 빠지고 절 자체는 온다.</summary>
    public bool HasConnectionSection { get; init; }

    /// <summary>설정 절을 받았는가. <c>basic</c> 에서는 <c>include=server_config</c> 일 때만 온다.</summary>
    public bool HasConfigSection { get; init; }

    /// <summary>임계 — 6.3 <c>threshold_config</c> · 7.0+ <c>server_config.thresholds</c>.</summary>
    public JObject? Thresholds { get; init; }

    /// <summary>운용 모드 — 7.0+ <c>server_config.modes</c>(PROXY 전용). 6.3 에는 없다(프록시 설정 경로가 따로다).</summary>
    public JObject? Modes { get; init; }

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }

    /// <summary>원본 — 진단과 "받지 못한 절" 판정의 근거.</summary>
    public JObject Raw { get; init; } = new();
}

/// <summary>
/// 사람이 고친 것 — <c>null</c> 은 "손대지 않았다", <c>Clear*</c> 는 "비운다" 다.
/// </summary>
/// <remarks>
/// 손대지 않은 키는 본문에 <b>실리지 않는다</b>. <c>PATCH</c> 는 RFC 7396 이라 키가 <c>null</c> 이면
/// 서버가 <b>지운다</b>(<c>app/services/json_merge.py:28-40</c>) — 그래서 "비우기" 와 "안 건드림" 을
/// 타입에서 갈라 둔다.
/// </remarks>
public sealed class ServerWriteIntent
{
    /// <summary>등록 전용 — 7.0+ 판별자. 수정에는 싣지 않는다(유형 불변, <c>server.py:421-422</c>).</summary>
    public string? TypeServer { get; set; }

    /// <summary>등록 전용 — 6.3 분류 id.</summary>
    public int? CategoryId { get; set; }

    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }

    public string? Hostname { get; set; }
    public bool ClearHostname { get; set; }

    public string? UserName { get; set; }
    public bool ClearUserName { get; set; }

    /// <summary>새로 입력한 비밀번호. 손대지 않았으면 <c>null</c> — 화면의 마스킹 값을 되돌려 보내지 않는다.</summary>
    public string? NewPassword { get; set; }

    public double? CpuWarning { get; set; }
    public double? CpuCritical { get; set; }
    public double? RamWarning { get; set; }
    public double? RamCritical { get; set; }
    public double? DiskWarning { get; set; }
    public double? DiskCritical { get; set; }
    public double? NetworkWarningMbps { get; set; }
    public double? NetworkCriticalMbps { get; set; }

    /// <summary>운용 모드(7.0+ PROXY 전용).</summary>
    public string? OperationMode { get; set; }
    public string? WindyMode { get; set; }

    /// <summary>소속 부대 — 8.0 이상에서만 실린다.</summary>
    public int? UnitId { get; set; }

    public bool HasThresholdEdit =>
        CpuWarning is not null || CpuCritical is not null || RamWarning is not null || RamCritical is not null
        || DiskWarning is not null || DiskCritical is not null
        || NetworkWarningMbps is not null || NetworkCriticalMbps is not null;

    public bool HasModeEdit => OperationMode is not null || WindyMode is not null;

    public bool HasConnectionEdit =>
        IpAddress is not null || Port is not null || Hostname is not null || ClearHostname
        || UserName is not null || ClearUserName || NewPassword is not null;
}

/// <summary>
/// 응답 → <see cref="ServerAxisView"/>. <b>순수 함수</b>이고 던지지 않는다.
/// </summary>
public static class ServerAxisReader
{
    /// <summary>
    /// 한 행을 읽는다. <paramref name="contract"/> 가 7.0 이상이면 축 모양을, 아니면 평면 모양을 본다.
    /// 어느 쪽이든 <b>없는 키는 비운다</b>(기본값을 지어내지 않는다).
    /// </summary>
    /// <param name="typeOfCategory">6.3 전용 — <c>category_id</c> → 판별자 문자열 해석기.</param>
    public static ServerAxisView Parse(JObject row, EnumServerContract contract, Func<int, string?>? typeOfCategory = null)
    {
        if (row is null) throw new ArgumentNullException(nameof(row));

        var isAxis = contract >= EnumServerContract.V7_0;
        var connection = row["connection"] as JObject;
        var config = row["server_config"] as JObject;
        var credentials = connection?["credentials"] as JObject;

        var categoryId = Int(row["category_id"]) ?? 0;
        var typeServer = isAxis
            ? Str(row["category_server"]) ?? string.Empty
            : typeOfCategory?.Invoke(categoryId) ?? string.Empty;

        return new ServerAxisView
        {
            Id = Int(row["id"]) ?? 0,
            TypeServer = typeServer,
            CategoryId = categoryId,
            Name = Str(row["name"]) ?? string.Empty,
            IsEnable = Bool(row["is_enable"]) ?? true,
            UnitId = Int(row["unit_id"]),

            // 키의 부재를 값과 구분한다 — 이것이 "보고 없음" 판정의 근거다.
            Status = Str(row["status"]),
            HasStatusKey = row.ContainsKey("status"),
            StatusObservedAt = Str(row["status_observed_at"]),
            HasStatusObservedAtKey = row.ContainsKey("status_observed_at"),

            IpAddress = (isAxis ? Str(connection?["ip_address"]) : Str(row["ip_address"])) ?? string.Empty,
            Port = (isAxis ? Int(connection?["ip_port"]) : Int(row["port"])) ?? 0,
            Hostname = isAxis ? Str(connection?["hostname"]) : Str(row["hostname"]),
            UserName = isAxis ? Str(credentials?["user_name"]) : Str(row["user_name"]),

            HasConnectionSection = isAxis ? connection is not null : row.ContainsKey("ip_address"),
            HasConfigSection = isAxis ? config is not null : row.ContainsKey("threshold_config"),

            Thresholds = isAxis ? config?["thresholds"] as JObject : row["threshold_config"] as JObject,
            Modes = isAxis ? config?["modes"] as JObject : null,

            CreatedAt = Str(row["created_at"]),
            UpdatedAt = Str(row["updated_at"]),
            Raw = row,
        };
    }

    /// <summary>
    /// 문자열로 읽는다. <b>날짜는 왕복 형식으로 되돌린다</b> — <c>JObject.Parse</c> 가 ISO8601 문자열을
    /// <see cref="JTokenType.Date"/> 로 바꿔 버리면 <c>ToString()</c> 이 현지 표기("2026-10-27 오전 10:00:05")가 되어
    /// 오프셋이 사라진다(레포 교훈: <c>DateParseHandling</c>).
    /// </summary>
    private static string? Str(JToken? token)
    {
        if (token is null || token.Type is JTokenType.Null or JTokenType.Undefined) return null;
        if (token.Type == JTokenType.String) return token.Value<string>();
        if (token.Type == JTokenType.Date)
        {
            var value = token.Value<object>();
            return value switch
            {
                DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
                DateTime local => new DateTimeOffset(local).ToString("O", CultureInfo.InvariantCulture),
                _ => token.ToString(),
            };
        }
        return token.ToString();
    }

    private static int? Int(JToken? token)
        => token is null || token.Type is JTokenType.Null or JTokenType.Undefined ? null
           : token.Type is JTokenType.Integer or JTokenType.Float ? token.Value<int>()
           : int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool? Bool(JToken? token)
        => token is null || token.Type is JTokenType.Null or JTokenType.Undefined ? null
           : token.Type == JTokenType.Boolean ? token.Value<bool>()
           : bool.TryParse(token.ToString(), out var parsed) ? parsed : null;
}

/// <summary>
/// <see cref="ServerWriteIntent"/> → 판본별 요청 본문. <b>순수 함수</b>이고 네트워크를 모른다.
/// </summary>
/// <remarks>
/// <para><b>6.3</b>(운영) 은 평면 스칼라다 — <c>category_id</c>·<c>ip_address</c>·<c>port</c>·<c>hostname</c>·
/// <c>user_name</c>·<c>user_password</c>·<c>threshold_config</c>.</para>
/// <para><b>7.0+</b> 는 그 일곱 키를 <b>전부 거부한다</b>(<c>app/schemas/server.py:59-66</c> ·
/// <c>:334-338</c> <c>_ServerWriteBase._legacy_fields</c>). 축으로 보낸다 —
/// <c>category_server</c>·<c>name</c>·<c>is_enable</c>·<c>connection{…}</c>·<c>server_config{thresholds,modes}</c>.</para>
/// <para><b>어느 판본에서도 <c>status</c> 를 싣지 않는다</b> — 7.0 은 <c>422 OBSERVED_FIELD</c>
/// (<c>server.py:352-354</c>)이고 6.3 에서도 관측을 운영자가 덮어쓸 이유가 없다.</para>
/// <para><c>unit_id</c> 는 <b>8.0 이상에서만</b>(<c>server.py:336-339</c> 가 받지만 6.3·7.0 배포에는 키가 없다).</para>
/// </remarks>
public static class ServerAxisWriter
{
    /// <summary>등록 본문(POST /api/servers).</summary>
    public static JObject BuildCreate(ServerWriteIntent intent, EnumServerContract contract)
    {
        if (intent is null) throw new ArgumentNullException(nameof(intent));

        return contract >= EnumServerContract.V7_0
            ? AxisBody(intent, contract, isCreate: true)
            : FlatBody(intent, isCreate: true);
    }

    /// <summary>수정 본문(PATCH /api/servers/{id}) — <b>손댄 키만</b>.</summary>
    public static JObject BuildPatch(ServerWriteIntent intent, EnumServerContract contract)
    {
        if (intent is null) throw new ArgumentNullException(nameof(intent));

        return contract >= EnumServerContract.V7_0
            ? AxisBody(intent, contract, isCreate: false)
            : FlatBody(intent, isCreate: false);
    }

    /// <summary>이 판본에서 "비우기" 를 보낼 수 있는가 — 7.0+ 의 축 병합만 키 삭제를 받는다.</summary>
    public static bool SupportsClearing(EnumServerContract contract) => contract >= EnumServerContract.V7_0;

    /// <summary>장비 → 서버 배정 본문. 병합 패치라 <c>server_id</c> 하나(+8.0 의 부대)만 싣는다.</summary>
    /// <param name="serverId"><c>null</c> 이면 <b>해제</b>다 — 7.0+ 에서만 뜻이 있다(<c>device.py:568·683·740</c>).</param>
    public static JObject BuildDeviceAssign(int? serverId, int? unitId, EnumServerContract contract)
    {
        var body = new JObject { ["server_id"] = serverId is null ? JValue.CreateNull() : new JValue(serverId.Value) };
        if (contract >= EnumServerContract.V8_0 && unitId is > 0) body["unit_id"] = unitId.Value;
        return body;
    }

    #region - 6.3 (운영) -
    private static JObject FlatBody(ServerWriteIntent intent, bool isCreate)
    {
        var body = new JObject();
        if (isCreate && intent.CategoryId is > 0) body["category_id"] = intent.CategoryId.Value;
        if (intent.Name is not null) body["name"] = intent.Name;
        if (intent.IpAddress is not null) body["ip_address"] = intent.IpAddress;
        if (intent.Port is not null) body["port"] = intent.Port.Value;

        // 6.3 은 RFC 7396 병합 삭제를 쓰지 않는다 — 비우기는 호출부가 막고(UI), 여기서는 키를 싣지 않는다.
        if (intent.Hostname is not null) body["hostname"] = intent.Hostname;
        if (intent.UserName is not null) body["user_name"] = intent.UserName;
        if (intent.NewPassword is not null) body["user_password"] = intent.NewPassword;

        if (intent.HasThresholdEdit) body["threshold_config"] = Thresholds(intent);
        return body;
    }
    #endregion

    #region - 7.0 / 8.0 (축) -
    private static JObject AxisBody(ServerWriteIntent intent, EnumServerContract contract, bool isCreate)
    {
        var body = new JObject();

        // 유형은 등록에서만 싣는다 — 수정 본문의 category_server 는 현재 값과 같을 때만 허용이라 보낼 이유가 없다.
        if (isCreate && !string.IsNullOrWhiteSpace(intent.TypeServer)) body["category_server"] = intent.TypeServer;
        if (intent.Name is not null) body["name"] = intent.Name;

        if (intent.HasConnectionEdit)
        {
            var connection = new JObject();
            if (intent.IpAddress is not null) connection["ip_address"] = intent.IpAddress;
            if (intent.Port is not null) connection["ip_port"] = intent.Port.Value;
            if (intent.ClearHostname) connection["hostname"] = JValue.CreateNull();
            else if (intent.Hostname is not null) connection["hostname"] = intent.Hostname;

            if (intent.ClearUserName || intent.UserName is not null || intent.NewPassword is not null)
            {
                var credentials = new JObject();
                if (intent.ClearUserName) credentials["user_name"] = JValue.CreateNull();
                else if (intent.UserName is not null) credentials["user_name"] = intent.UserName;
                if (intent.NewPassword is not null) credentials["user_password"] = intent.NewPassword;
                connection["credentials"] = credentials;
            }
            body["connection"] = connection;
        }

        if (intent.HasThresholdEdit || intent.HasModeEdit)
        {
            var config = new JObject();
            if (intent.HasThresholdEdit) config["thresholds"] = Thresholds(intent);
            if (intent.HasModeEdit)
            {
                var modes = new JObject();
                if (intent.OperationMode is not null) modes["operation_mode"] = intent.OperationMode;
                if (intent.WindyMode is not null) modes["windy_mode"] = intent.WindyMode;
                config["modes"] = modes;
            }
            body["server_config"] = config;
        }

        if (contract >= EnumServerContract.V8_0 && intent.UnitId is > 0) body["unit_id"] = intent.UnitId.Value;
        return body;
    }
    #endregion

    /// <summary>임계 묶음 — 손댄 값만. 두 판본이 같은 모양을 쓴다(<c>cpu·ram·disk</c> % · <c>network</c> Mbps).</summary>
    private static JObject Thresholds(ServerWriteIntent intent)
    {
        var thresholds = new JObject();
        Put(thresholds, "cpu", "warning", intent.CpuWarning);
        Put(thresholds, "cpu", "critical", intent.CpuCritical);
        Put(thresholds, "ram", "warning", intent.RamWarning);
        Put(thresholds, "ram", "critical", intent.RamCritical);
        Put(thresholds, "disk", "warning", intent.DiskWarning);
        Put(thresholds, "disk", "critical", intent.DiskCritical);
        Put(thresholds, "network", "warning_mbps", intent.NetworkWarningMbps);
        Put(thresholds, "network", "critical_mbps", intent.NetworkCriticalMbps);
        return thresholds;
    }

    private static void Put(JObject root, string group, string key, double? value)
    {
        if (value is null) return;
        if (root[group] is not JObject section) root[group] = section = new JObject();
        section[key] = value.Value;
    }

    /// <summary>이 본문에 <b>절대</b> 실리지 않는 키 — 감사 테스트가 이 목록을 그대로 훑는다.</summary>
    public static readonly IReadOnlyList<string> NeverSentKeys = new[]
    {
        "status", "status_observed_at", "id", "created_at", "updated_at",
    };

    /// <summary>7.0+ 가 <b>거부하는</b> 평면 키 — 축 본문에 하나라도 있으면 422 다.</summary>
    public static readonly IReadOnlyList<string> LegacyRejectedOnAxis = new[]
    {
        "category_id", "ip_address", "port", "hostname", "user_name", "user_password", "threshold_config", "proxy_settings",
    };
}
