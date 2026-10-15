using System;
using System.IO;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Helpers;
using Ironwall.Dotnet.Libraries.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

/****************************************************************************
   Purpose      : 제어 명령 공통 선택 칸(브로커 연동설계 v2.0.12 §7 머리 · 이슈 #7 G7-4)
                  requested_by = 운영자 계정(login_id), requested_client_id = 통제DB 세션 client_id(X-Client-Id).
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>제어 명령(클라 → 매니저)에 싣는 보낸 사람 · 단말. 값이 없으면 null(칸을 싣지 않는다).</summary>
public interface ICommandRequester
{
    /// <summary>명령을 낸 운영자 계정(<c>login_id</c>).</summary>
    string? RequestedBy { get; }

    /// <summary>명령을 낸 단말 — <c>X-Client-Id</c> 헤더와 같은 값(설치마다 다르고 업데이트에도 유지, SSO 로그인이어도 같다).</summary>
    string? RequestedClientId { get; }
}

/// <inheritdoc cref="ICommandRequester"/>
public class CommandRequester : ICommandRequester
{
    public CommandRequester(IPermissionService? permission = null, ITokenStorageService? tokenStorage = null, IApiSetupModel? apiSetup = null)
    {
        _permission = permission;
        _tokenStorage = tokenStorage;
        _apiSetup = apiSetup;
    }

    public string? RequestedBy => ResolveRequestedBy(_permission?.LoginId, _tokenStorage?.UserId, _permission?.Name);

    public string? RequestedClientId
    {
        get
        {
            // ApiService 가 X-Client-Id 헤더를 정하는 것과 같은 함수 · 같은 캐시 — 두 값이 어긋나지 않는다.
            try
            {
                var id = ClientIdResolver.Resolve(_apiSetup?.ClientId);
                return ClientIdResolver.IsWellFormed(id) ? id : null;
            }
            catch { return null; }
        }
    }

    /// <summary>운영자 계정 — login_id → JWT sub(= login_id) → 표시이름 순. 모두 비면 null.</summary>
    public static string? ResolveRequestedBy(string? loginId, string? tokenUserId, string? displayName)
    {
        foreach (var value in new[] { loginId, tokenUserId, displayName })
            if (!string.IsNullOrWhiteSpace(value)) return value!.Trim();
        return null;
    }

    /// <summary>
    /// 봉투 JSON 의 <c>body</c> 에 두 칸을 싣는다. 이미 값이 있는 칸은 두고, 넣을 값이 없으면 칸을 만들지 않는다.
    /// 날짜 문자열은 그대로 둔다(다시 쓰면서 형식이 바뀌지 않게).
    /// </summary>
    public static string Stamp(string envelopeJson, string? requestedBy, string? requestedClientId)
    {
        if (string.IsNullOrWhiteSpace(requestedBy) && string.IsNullOrWhiteSpace(requestedClientId)) return envelopeJson;
        JObject root;
        using (var reader = new JsonTextReader(new StringReader(envelopeJson)) { DateParseHandling = DateParseHandling.None })
            root = JObject.Load(reader);
        if (root["body"] is not JObject body) return envelopeJson;

        var changed = Put(body, RequestedByKey, requestedBy) | Put(body, RequestedClientIdKey, requestedClientId);
        return changed ? root.ToString(Formatting.None) : envelopeJson;

        static bool Put(JObject body, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (body[key] is { Type: JTokenType.String } existing && !string.IsNullOrWhiteSpace(existing.Value<string>())) return false;
            body[key] = value;
            return true;
        }
    }

    public const string RequestedByKey = "requested_by";
    public const string RequestedClientIdKey = "requested_client_id";

    private readonly IPermissionService? _permission;
    private readonly ITokenStorageService? _tokenStorage;
    private readonly IApiSetupModel? _apiSetup;
}
