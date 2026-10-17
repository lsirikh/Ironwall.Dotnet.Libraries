// 계정 창(N-06) 라이브 왕복 — 창 뷰모델을 그대로 몰아 GOP 서버와 주고받는다(LRT_ONLY=accounts-vm).
//
// 규칙
//   - 행위(act)는 전부 제품 경로다: AccountSetupPanelViewModel.ClickSave · AccountConsolePanelViewModel.ApplyAsync ·
//     EditorDialogViewModel.UploadPictureAsync · RegisterDialogViewModel.SetPictureAsync/ClickOk ·
//     AccountConsolePanelViewModel.LastLoginText. Raw 는 준비(arrange)와 서버 상태 재확인(assert)에만 쓴다.
//   - 만든 것은 접두 lrt_acc… / LRT-ACC-… 로만 만들고 finally 에서 지운다.
//   - 하네스 관리자(admin) 계정의 비밀번호 · 설정 · 세션은 건드리지 않는다. 폐기용 계정 로그인은
//     관리자 세션을 쫓아내지 않았는지(GET /users/me 200) 바로 확인한다.
//   - 서버 전역 세션 설정은 스냅샷 → finally 에서 복원 → 복원됐는지 단언한다.
using System.IO;
using System.Net.Http;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

/// <summary>팝업 · 재조회 통지를 모으는 이벤트 애그리게이터(창을 띄우지 않는다).</summary>
internal sealed class AccVmEvents : IEventAggregator
{
    public List<object> Published { get; } = new();
    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
    public void Unsubscribe(object subscriber) { }
    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
    public string LastInfo() => Published.OfType<OpenInfoPopupMessageModel>().LastOrDefault()?.Explain ?? "(no popup)";
}

/// <summary>편집 다이얼로그 생성자에 필요한 자리 채움 — 비밀번호 초기화 경로는 이 하네스가 부르지 않는다.</summary>
internal sealed class AccVmSessionConfig : ISessionConfigService
{
    public bool IsSession => true;
    public int SessionExpiration => 60;
    public string AdminResetPassword => "not-used-by-harness";
    public void ApplySession(bool isSession, int expirationMinutes) { }
}

public static partial class Steps
{
    const string ACC_TAG = "accounts-vm";
    const string ACC_USER_PREFIX = "lrt_acc";
    const string ACC_GROUP_PREFIX = "LRT-ACC-";
    const string ACC_PW = "LrtAccVm1!pw";

    // 1×1 PNG (70 bytes) — 서버는 Pillow 로 실제 바이트를 검증한다.
    static readonly byte[] AccPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    static readonly string[] ACC_SESSION_KEYS =
    {
        "session_timeout_hours", "refresh_expiration_days", "lockout_threshold", "lockout_duration_minutes",
        "session_enabled", "session_concurrency_policy", "max_concurrent_sessions",
        "session_history_retention_days", "login_anomaly_event_enabled", "session_self_replace_enabled",
    };

    // =====================================================================================
    // A1 — 세션 설정 저장(AccountSetupPanelViewModel.ClickSave → PUT /settings/session)
    // =====================================================================================
    [ExtraStep(ACC_TAG, 610)]
    static async Task AccVm_A1_SessionSetupSave(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        var (snapStatus, snapJo) = await raw.Get("settings/session").ConfigureAwait(false);
        var snapshot = Obj(snapJo["data"]);
        if (snapStatus != 200 || snapshot is null)
        {
            rec.Add("acc.A1.0", ACC_TAG, "snapshot settings/session", Verdict.BLOCKED, $"HTTP {snapStatus} {Short(snapJo)}",
                blocked: "could not snapshot the shared server setting - refusing to write it");
            return;
        }

        var oldGet = IoC.GetInstance;
        try
        {
            // 패널은 IAccountApiService 를 IoC 로 늦게 찾는다 — 호스트 DI 가 하던 일을 하네스가 대신 건다.
            IoC.GetInstance = (type, key) => type == typeof(IAccountApiService) ? boot.AccountApi : oldGet?.Invoke(type, key);

            var events = new AccVmEvents();
            var vm = new AccountSetupPanelViewModel(events, boot.Log);
            boot.Wire.CurrentTag = ACC_TAG + "/A1-load";
            await vm.ClickReload().ConfigureAwait(false);
            if (!vm.ServerSettingsAvailable)
            {
                rec.Add("acc.A1.0", ACC_TAG, "panel loads server settings", Verdict.BLOCKED, vm.ServerStatus, blocked: "panel could not load");
                return;
            }

            // 값은 바꾸지 않는다 — 불러온 그대로 [저장].
            boot.Wire.CurrentTag = ACC_TAG + "/A1-save";
            var before = rec.LastSeq();
            await vm.ClickSave().ConfigureAwait(false);
            var put = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains("settings/session"));
            var body = AccParse(put?.RequestBody);
            var sentReadOnly = body is not null && (body.ContainsKey("auth_mode") || body.ContainsKey("jwt_algorithm"));
            var ok = put is not null && put.Status == 200 && !sentReadOnly;
            rec.Add("acc.A1", ACC_TAG, "session setup [save] with unchanged values -> no read-only keys on the wire, HTTP 200",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {put?.Status}; auth_mode/jwt_algorithm on wire={sentReadOnly}; body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 400)}; " +
                $"response={Recorder.Trunc(put?.ResponseBodyRedacted ?? "(none)", 250)}; popup='{events.LastInfo()}'",
                defectAt: ok ? "" : "Messages/Dto/Accounts/SessionSettingsDto.cs (auth_mode/jwt_algorithm serialized as null) · Accounts.Ui/ViewModels/Panels/AccountSetupPanelViewModel.cs ClickSave",
                seqs: AccSeqs(rec, before));

            var (_, afterJo) = await raw.Get("settings/session").ConfigureAwait(false);
            var diff = AccSessionDiff(snapshot, Obj(afterJo["data"]));
            rec.Add("acc.A1b", ACC_TAG, "re-GET after an unchanged save equals the snapshot",
                diff.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                diff.Count == 0 ? "no editable key changed" : "changed: " + string.Join(", ", diff),
                defectAt: diff.Count == 0 ? "" : "Accounts.Ui/ViewModels/Panels/AccountSetupPanelViewModel.cs ClickSave");
        }
        catch (Exception ex)
        {
            rec.Add("acc.A1!", ACC_TAG, "session setup save", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            IoC.GetInstance = oldGet;
            // 전역 설정 복원 → 복원됐는지 단언.
            var (_, nowJo) = await raw.Get("settings/session").ConfigureAwait(false);
            var drift = AccSessionDiff(snapshot, Obj(nowJo["data"]));
            if (drift.Count > 0)
            {
                var restore = new JObject();
                foreach (var k in ACC_SESSION_KEYS) if (snapshot[k] is not null) restore[k] = snapshot[k];
                await raw.Send(HttpMethod.Put, "settings/session", restore).ConfigureAwait(false);
            }
            var (_, finalJo) = await raw.Get("settings/session").ConfigureAwait(false);
            var remaining = AccSessionDiff(snapshot, Obj(finalJo["data"]));
            rec.Add("acc.A1.restore", ACC_TAG, "shared settings/session restored to the snapshot",
                remaining.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                (drift.Count == 0 ? "nothing drifted" : "drifted [" + string.Join(", ", drift) + "] -> restored") +
                (remaining.Count == 0 ? "" : "; STILL DIFFERENT: " + string.Join(", ", remaining)),
                defectAt: remaining.Count == 0 ? "" : "harness restore");
        }
    }

    static List<string> AccSessionDiff(JObject? a, JObject? b)
    {
        var diff = new List<string>();
        if (a is null || b is null) { diff.Add("(unreadable)"); return diff; }
        foreach (var k in ACC_SESSION_KEYS)
            if (!JToken.DeepEquals(a[k], b[k])) diff.Add($"{k}: {J(a[k])} -> {J(b[k])}");
        return diff;
    }

    // =====================================================================================
    // A2 — 콘솔 [적용](AccountConsolePanelViewModel.ApplyAsync → PUT /users/{id})
    // =====================================================================================
    [ExtraStep(ACC_TAG, 620)]
    static async Task AccVm_A2_ConsoleApply(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        await AccSweep(raw, rec).ConfigureAwait(false);
        int targetId = 0;
        try
        {
            targetId = await AccCreateUser(raw, rec, "lrt_acc_a2", "LRT A2", new
            {
                email = "lrt_acc_a2@example.com",
                phone = "010-0000-0002",
                department = "LRT-dept",
            }).ConfigureAwait(false);
            if (targetId == 0) { rec.Add("acc.A2.0", ACC_TAG, "arrange user", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            var perm = new PermissionService();
            perm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gateway = new ApiAccountGateway(boot.AccountApi, boot.Tokens, perm, null, boot.Log);
            var (console, _) = await AccBuildConsole(boot, perm, boot.AccountApi, boot.Tokens, gateway).ConfigureAwait(false);

            var row = AccSelect(console, targetId);
            if (row is null) { rec.Add("acc.A2.0", ACC_TAG, "user row in console list", Verdict.BLOCKED, "row not loaded", blocked: "fixture"); return; }

            // ---- (a) 상태 → 미사용 ----
            boot.Wire.CurrentTag = ACC_TAG + "/A2a";
            var before = rec.LastSeq();
            AccField(console, "used").Text = "미사용";
            await console.ApplyAsync().ConfigureAwait(true);
            var putA = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, ua) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var activeAfter = (bool?)Obj(ua["data"])?["is_active"];
            var aOk = activeAfter == false;
            rec.Add("acc.A2a", ACC_TAG, "console: 상태=미사용 [적용] -> server is_active=false",
                aOk ? Verdict.PASS : Verdict.FAIL,
                $"server is_active={activeAfter}; HTTP {putA?.Status}; body={Recorder.Trunc(putA?.RequestBodyRedacted ?? "(none)", 300)}; " +
                $"console says '{console.DetailMessage}' / footer '{console.DetailFooter}'",
                defectAt: aOk ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToUserUpdateDto (is_active never sent)",
                seqs: AccSeqs(rec, before));

            // ---- (b) 이메일 · 전화 비우기 ----
            boot.Wire.CurrentTag = ACC_TAG + "/A2b";
            before = rec.LastSeq();
            AccField(console, "email").Text = "";
            AccField(console, "phone").Text = "";
            await console.ApplyAsync().ConfigureAwait(true);
            var putB = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, ub) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var email = Obj(ub["data"])?["email"];
            var phone = Obj(ub["data"])?["phone"];
            var bOk = AccIsBlank(email) && AccIsBlank(phone);
            rec.Add("acc.A2b", ACC_TAG, "console: 이메일 · 전화 비우고 [적용] -> server email/phone cleared",
                bOk ? Verdict.PASS : Verdict.FAIL,
                $"server email={J(email)} phone={J(phone)}; HTTP {putB?.Status}; body={Recorder.Trunc(putB?.RequestBodyRedacted ?? "(none)", 300)}; " +
                $"console says '{console.DetailMessage}'",
                defectAt: bOk ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToUserUpdateDto + Messages/Dto/Accounts/UserUpdateDto.cs (null ignored -> clear impossible)",
                seqs: AccSeqs(rec, before));

            // ---- (c-wire) 손대지 않은 칸은 보내지 않는다 ----
            var bodyB = AccParse(putB?.RequestBody);
            var untouched = new[] { "role", "name", "department", "is_active", "photo_url", "employee_number", "position" }
                .Where(k => bodyB?.ContainsKey(k) == true).ToList();
            rec.Add("acc.A2c-wire", ACC_TAG, "console [적용] sends only the touched fields (no role/name/... riding along)",
                bodyB is not null && untouched.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                $"untouched keys on the wire = [{string.Join(",", untouched)}]; body={Recorder.Trunc(putB?.RequestBodyRedacted ?? "(none)", 300)}",
                defectAt: untouched.Count == 0 ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToUserUpdateDto (whole model sent)");
        }
        catch (Exception ex)
        {
            rec.Add("acc.A2!", ACC_TAG, "console apply", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await AccDeleteUser(raw, rec, targetId).ConfigureAwait(false);
        }
    }

    // =====================================================================================
    // A2c — 비-ADMIN 편집자(users:view+edit)가 콘솔에서 부서만 바꾼다 → role 이 딸려 가면 403
    // =====================================================================================
    [ExtraStep(ACC_TAG, 621)]
    static async Task AccVm_A2c_NonAdminEditor(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int groupId = 0, editorId = 0, targetId = 0;
        Bootstrap? second = null;
        try
        {
            var (gs, gj) = await raw.Post("user-groups", new { name = ACC_GROUP_PREFIX + "EDITORS", description = "live roundtrip accounts-vm" }).ConfigureAwait(false);
            if (gs != 200 && gs != 201) { rec.Add("acc.A2c.0", ACC_TAG, "arrange group", Verdict.BLOCKED, $"HTTP {gs} {Short(gj)}", blocked: "fixture"); return; }
            groupId = (int)gj["data"]!["id"]!;
            rec.Created("user-group", groupId);
            var mods = new JObject();
            foreach (var m in ALL_MODULES)
                mods[m] = new JObject { ["view"] = m == "users", ["edit"] = m == "users", ["delete"] = false, ["control"] = false };
            var (ps, pj) = await raw.Post($"user-groups/{groupId}/permissions", new JObject { ["modules"] = mods }).ConfigureAwait(false);
            if (ps != 200 && ps != 201) { rec.Add("acc.A2c.0", ACC_TAG, "arrange group permissions", Verdict.BLOCKED, $"HTTP {ps} {Short(pj)}", blocked: "fixture"); return; }

            editorId = await AccCreateUser(raw, rec, "lrt_acc_ed", "LRT editor", new { group_id = groupId }).ConfigureAwait(false);
            targetId = await AccCreateUser(raw, rec, "lrt_acc_tg", "LRT target", new { department = "LRT-before" }).ConfigureAwait(false);
            if (editorId == 0 || targetId == 0) { rec.Add("acc.A2c.0", ACC_TAG, "arrange users", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            // 두 번째 클라이언트 — 폐기용 편집자로 로그인. 관리자 세션이 살아 있는지 곧바로 확인한다.
            second = new Bootstrap(rec);
            await second.InitAsync().ConfigureAwait(false);
            second.Wire.CurrentTag = ACC_TAG + "/A2c-login";
            var login = await second.AccountApi.LoginAsync("lrt_acc_ed", ACC_PW).ConfigureAwait(false);
            if (!login.Success || login.Data?.User is null)
            {
                rec.Add("acc.A2c.0", ACC_TAG, "throwaway editor login", Verdict.BLOCKED, $"{login.Error?.Code} {login.Message}", blocked: "fixture");
                return;
            }
            second.Tokens.SetTokens(login.Data.AccessToken, login.Data.RefreshToken, login.Data.SessionId);
            if (!await AccAdminSessionAlive(boot, rec, "A2c").ConfigureAwait(false)) return;

            var perm = new PermissionService();
            perm.Apply(login.Data.User);
            var gateway = new ApiAccountGateway(second.AccountApi, second.Tokens, perm, null, second.Log);
            var (console, _) = await AccBuildConsole(second, perm, second.AccountApi, second.Tokens, gateway).ConfigureAwait(false);
            var row = AccSelect(console, targetId);
            if (row is null) { rec.Add("acc.A2c.0", ACC_TAG, "target row visible to the editor", Verdict.BLOCKED, "row not loaded", blocked: "fixture"); return; }

            second.Wire.CurrentTag = ACC_TAG + "/A2c-apply";
            var before = rec.LastSeq();
            AccField(console, "department").Text = "LRT-after";
            await console.ApplyAsync().ConfigureAwait(true);
            var put = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, tj) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var dept = (string?)Obj(tj["data"])?["department"];
            var ok = put?.Status == 200 && dept == "LRT-after";
            rec.Add("acc.A2c", ACC_TAG, "non-ADMIN editor (users:edit) changes only 부서 through the console -> 200, not 403",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {put?.Status}; server department='{dept}'; editor CanEditUsers={console.CanEditUsers}; " +
                $"body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 300)}; response={Recorder.Trunc(put?.ResponseBodyRedacted ?? "(none)", 200)}; " +
                $"console says '{console.DetailMessage}'",
                defectAt: ok ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToUserUpdateDto (role always sent -> server 'Only ADMIN role can change role')",
                seqs: AccSeqs(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("acc.A2c!", ACC_TAG, "non-admin editor", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (second is not null)
            {
                try { await second.AccountApi.LogoutAsync().ConfigureAwait(false); } catch { /* 계정 삭제가 세션을 정리한다 */ }
            }
            await AccDeleteUser(raw, rec, targetId).ConfigureAwait(false);
            await AccDeleteUser(raw, rec, editorId).ConfigureAwait(false);
            if (groupId > 0)
            {
                var (s, _) = await raw.Delete($"user-groups/{groupId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("user-group", groupId); else rec.Leftover("user-group", groupId, $"DELETE {s}");
            }
        }
    }

    // =====================================================================================
    // A3 — 사진 표시(EditorDialogViewModel.UploadPictureAsync → POST /users/{id}/photo → 표시 경계)
    // =====================================================================================
    [ExtraStep(ACC_TAG, 630)]
    static async Task AccVm_A3_PhotoDisplay(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int targetId = 0;
        var tmpDir = Path.Combine(Path.GetTempPath(), "lrt_acc_vm");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var png = Path.Combine(tmpDir, "lrt_acc_1x1.png");
            File.WriteAllBytes(png, AccPng);

            targetId = await AccCreateUser(raw, rec, "lrt_acc_a3", "LRT A3", new { }).ConfigureAwait(false);
            if (targetId == 0) { rec.Add("acc.A3.0", ACC_TAG, "arrange user", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            var perm = new PermissionService();
            perm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gateway = new ApiAccountGateway(boot.AccountApi, boot.Tokens, perm, null, boot.Log);
            var model = (await gateway.GetAllAccountsAsync().ConfigureAwait(false))?.FirstOrDefault(a => a.Id == targetId);
            if (model is null) { rec.Add("acc.A3.0", ACC_TAG, "user from gateway", Verdict.BLOCKED, "not listed", blocked: "fixture"); return; }

            var events = new AccVmEvents();
            var row = new AccountViewModel(events, boot.Log, model);
            var editor = new EditorDialogViewModel(events, boot.Log, row, gateway, new AccVmSessionConfig(),
                new ProfileImageService(boot.Log, tmpDir), gateway);

            boot.Wire.CurrentTag = ACC_TAG + "/A3-upload";
            var before = rec.LastSeq();
            await editor.UploadPictureAsync(png).ConfigureAwait(false);
            var upload = AccSince(rec, before).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains($"users/{targetId}/photo"));
            var image = row.Image;
            var shown = AccRender(image);
            var (fetchStatus, fetchLen) = await AccFetch(shown).ConfigureAwait(false);
            var ok = upload?.Status == 200 && fetchStatus == 200 && fetchLen == AccPng.Length;
            rec.Add("acc.A3", ACC_TAG, "editor photo upload -> what ImageConverter renders is the uploaded image (200, same bytes)",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"upload HTTP {upload?.Status}; row.Image='{image}'; ImageConverter -> '{shown}'; GET -> {fetchStatus} ({fetchLen} bytes, expected {AccPng.Length}); popup='{events.LastInfo()}'",
                defectAt: ok ? "" : "Accounts.Api/Gateways/ApiAccountGateway.cs (relative /api/users/photo/... passed through) · Utils/ImageConverter.cs (http(s) only)",
                seqs: AccSeqs(rec, before));

            // 목록(재조회) 경로도 같은 그림을 보여야 한다.
            var listed = (await gateway.GetAllAccountsAsync().ConfigureAwait(false))?.FirstOrDefault(a => a.Id == targetId);
            var listedShown = AccRender(listed?.Image);
            var (ls, ll) = await AccFetch(listedShown).ConfigureAwait(false);
            var listOk = ls == 200 && ll == AccPng.Length;
            rec.Add("acc.A3b", ACC_TAG, "account list (GET /users) -> ImageConverter renders the uploaded photo",
                listOk ? Verdict.PASS : Verdict.FAIL,
                $"model.Image='{listed?.Image}'; ImageConverter -> '{listedShown}'; GET -> {ls} ({ll} bytes)",
                defectAt: listOk ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToAccountModel (Image = relative photo_url)");

            // 되쓰기 — 편집 다이얼로그 [확인](전체 모델 PUT)이 호스트가 박힌 절대 URL 을 서버에 심지 않는다.
            boot.Wire.CurrentTag = ACC_TAG + "/A3-writeback";
            before = rec.LastSeq();
            var saved = await gateway.UpdateAccountAsync(row.Model).ConfigureAwait(false);
            var wb = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, afterJo) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var storedUrl = (string?)Obj(afterJo["data"])?["photo_url"] ?? "";
            var wbOk = saved is not null && storedUrl.StartsWith("/api/users/photo/", StringComparison.Ordinal);
            rec.Add("acc.A3c", ACC_TAG, "editor full save keeps the server photo_url relative (no host baked into the DB)",
                wbOk ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {wb?.Status}; stored photo_url='{storedUrl}'; body={Recorder.Trunc(wb?.RequestBodyRedacted ?? "(none)", 300)}",
                defectAt: wbOk ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToServerPhotoUrl");
        }
        catch (Exception ex)
        {
            rec.Add("acc.A3!", ACC_TAG, "photo display", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (targetId > 0) await raw.Delete($"users/{targetId}/photo").ConfigureAwait(false);
            await AccDeleteUser(raw, rec, targetId).ConfigureAwait(false);
        }
    }

    // =====================================================================================
    // A4 — 등록 다이얼로그가 고른 사진(RegisterDialogViewModel.SetPictureAsync → ClickOk)
    // =====================================================================================
    [ExtraStep(ACC_TAG, 640)]
    static async Task AccVm_A4_RegisterWithPhoto(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int createdId = 0;
        var tmpDir = Path.Combine(Path.GetTempPath(), "lrt_acc_vm");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var png = Path.Combine(tmpDir, "lrt_acc_1x1_reg.png");
            File.WriteAllBytes(png, AccPng);

            var perm = new PermissionService();
            perm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gateway = new ApiAccountGateway(boot.AccountApi, boot.Tokens, perm, null, boot.Log);

            // 실제 앱에서는 목록이 이미 차 있다 — 비어 있으면 "첫 등록자=ADMIN" 분기가 탄다.
            var provider = new AccountProvider();
            foreach (var a in (await gateway.GetAllAccountsAsync().ConfigureAwait(false)) ?? new List<IAccountModel>()) provider.Add(a);

            var events = new AccVmEvents();
            var reg = new RegisterDialogViewModel(events, boot.Log, new RegisterViewModel(events, boot.Log, new AccountModel()),
                provider, gateway, new ProfileImageService(boot.Log, tmpDir));
            await ScreenExtensions.TryActivateAsync(reg).ConfigureAwait(false);
            reg.Username = "lrt_acc_a4";
            reg.Name = "LRT A4";
            reg.RegisterPass = ACC_PW;
            reg.PasswordConfirm = ACC_PW;
            await reg.SetPictureAsync(png).ConfigureAwait(false);

            boot.Wire.CurrentTag = ACC_TAG + "/A4-register";
            var before = rec.LastSeq();
            await reg.ClickOk().ConfigureAwait(false);
            var post = AccSince(rec, before).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/users", StringComparison.Ordinal));
            createdId = (int?)AccParse(post?.ResponseBody)?["data"]?["id"] ?? 0;
            if (createdId == 0) createdId = provider.FirstOrDefault(a => a.Username == "lrt_acc_a4")?.Id ?? 0;
            if (createdId > 0) rec.Created("user", createdId);

            var (_, uj) = await raw.Get($"users/{createdId}").ConfigureAwait(false);
            var stored = (string?)Obj(uj["data"])?["photo_url"] ?? "";
            var isDefault = stored.EndsWith("/default.png", StringComparison.OrdinalIgnoreCase) || stored.Length == 0;
            var (fs, fl) = isDefault ? (0, 0) : await AccFetch(AccAbsolute(stored)).ConfigureAwait(false);
            var ok = createdId > 0 && !isDefault && fs == 200 && fl == AccPng.Length;
            rec.Add("acc.A4", ACC_TAG, "register dialog with a chosen photo -> the created account carries that photo on the server",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"created id={createdId} (HTTP {post?.Status}); server photo_url='{stored}'; GET -> {fs} ({fl} bytes); " +
                $"register body={Recorder.Trunc(post?.RequestBodyRedacted ?? "(none)", 250)}; popup='{events.LastInfo()}'; " +
                $"photo calls={AccSince(rec, before).Count(w => w.Uri.Contains("/photo"))}",
                defectAt: ok ? "" : "Accounts.Ui/ViewModels/Dialogs/RegisterDialogViewModel.cs ClickOk (local file name never uploaded)",
                seqs: AccSeqs(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("acc.A4!", ACC_TAG, "register with photo", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (createdId == 0) createdId = await AccFindUserId(raw, "lrt_acc_a4").ConfigureAwait(false);
            if (createdId > 0) await raw.Delete($"users/{createdId}/photo").ConfigureAwait(false);
            await AccDeleteUser(raw, rec, createdId).ConfigureAwait(false);
        }
    }

    // =====================================================================================
    // A5 — 콘솔 "최근 로그인"(AccountConsolePanelViewModel.LastLoginText)
    // =====================================================================================
    [ExtraStep(ACC_TAG, 650)]
    static async Task AccVm_A5_LastLogin(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int targetId = 0;
        try
        {
            targetId = await AccCreateUser(raw, rec, "lrt_acc_a5", "LRT A5", new { }).ConfigureAwait(false);
            if (targetId == 0) { rec.Add("acc.A5.0", ACC_TAG, "arrange user", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            // 폐기용 계정으로 한 번 로그인 → 로그아웃(세션은 비활성, 서버 last_login_at 은 남는다).
            var other = new Raw();
            var (ls, lj) = await other.Post("auth/login", new { login_id = "lrt_acc_a5", password = ACC_PW }).ConfigureAwait(false);
            if (ls != 200) { rec.Add("acc.A5.0", ACC_TAG, "throwaway login", Verdict.BLOCKED, $"HTTP {ls} {Short(lj)}", blocked: "fixture"); return; }
            other.Token = (string?)lj["data"]?["access_token"] ?? "";
            if (!await AccAdminSessionAlive(boot, rec, "A5").ConfigureAwait(false)) return;
            await other.Post("auth/logout", new { }).ConfigureAwait(false);

            var (_, uj) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var serverLast = AccTime(Obj(uj["data"])?["last_login_at"]);

            var perm = new PermissionService();
            perm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gateway = new ApiAccountGateway(boot.AccountApi, boot.Tokens, perm, null, boot.Log);
            boot.Wire.CurrentTag = ACC_TAG + "/A5";
            var (console, _) = await AccBuildConsole(boot, perm, boot.AccountApi, boot.Tokens, gateway).ConfigureAwait(false);
            // 콘솔이 활성화 때 하는 두 조회 — 세션 목록(기본 활성만) · 그룹/소속.
            await console.UserSessionPanelViewModel.OnClickReloadButton().ConfigureAwait(false);
            await console.LoadGroupsAsync(CancellationToken.None).ConfigureAwait(false);
            var row = AccSelect(console, targetId);
            var text = console.LastLoginText;

            var expected = serverLast?.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var ok = row is not null && serverLast is not null && expected is not null && text.Contains(expected, StringComparison.Ordinal);
            rec.Add("acc.A5", ACC_TAG, "console 최근 로그인 shows the server last_login_at (not '기록 없음')",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"server last_login_at='{serverLast:o}'; console LastLoginText='{text}'; expected to contain '{expected}'; " +
                $"session rows loaded={console.UserSessionPanelViewModel.Items.Count}",
                defectAt: ok ? "" : "Accounts.Ui/ViewModels/Panels/AccountConsolePanelViewModel.cs LastLoginText (built from the active-session page)");
        }
        catch (Exception ex)
        {
            rec.Add("acc.A5!", ACC_TAG, "last login", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await AccDeleteUser(raw, rec, targetId).ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>진짜 패널 뷰모델로 콘솔 한 벌을 세운다(사용자 목록은 계정 관리 패널의 활성화 경로로 채운다).</summary>
    static async Task<(AccountConsolePanelViewModel Console, AccVmEvents Events)> AccBuildConsole(
        Bootstrap boot, IPermissionService perm, IAccountApiService api, ITokenStorageService tokens, ApiAccountGateway gateway)
    {
        var events = new AccVmEvents();
        var log = boot.Log;
        var editor = new EditorDialogViewModel(events, log, new AccountViewModel(events, log, new AccountModel()),
            gateway, new AccVmSessionConfig(), new ProfileImageService(log, Path.Combine(Path.GetTempPath(), "lrt_acc_vm")), gateway);
        var manager = new AccountManagerPanelViewModel(events, log, editor, new AccountProvider(),
            new LoginViewModel(events, log, new AccountModel()), gateway);
        var console = new AccountConsolePanelViewModel(events, log, perm, api, gateway, manager,
            new PermissionMatrixPanelViewModel(events, log, api),
            new UserSessionPanelViewModel(events, log, api, tokens),
            new AuditLogPanelViewModel(events, log, api),
            new AccountSetupPanelViewModel(events, log),
            new GrantManagementPanelViewModel(events, log, api));
        await ScreenExtensions.TryActivateAsync(manager).ConfigureAwait(false);
        await console.SelectRailAsync(AccountConsoleKeys.Users, force: true).ConfigureAwait(false);
        return (console, events);
    }

    static AccountViewModel? AccSelect(AccountConsolePanelViewModel console, int id)
    {
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Id == id);
        if (row is null) return null;
        console.OnUsersSelected(new List<AccountViewModel> { row });
        return row;
    }

    static Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms.AccountFieldViewModel AccField(AccountConsolePanelViewModel console, string key)
        => console.Form.Fields.Single(f => f.Key == key);

    static async Task<bool> AccAdminSessionAlive(Bootstrap boot, Recorder rec, string where)
    {
        var me = await boot.AccountApi.GetMeAsync().ConfigureAwait(false);
        var alive = me is not null && string.Equals(me.LoginId, Bootstrap.LOGIN_ID, StringComparison.OrdinalIgnoreCase);
        rec.Add($"acc.{where}.session", ACC_TAG, "throwaway login did not evict the harness admin session",
            alive ? Verdict.PASS : Verdict.BLOCKED,
            alive ? "GET /users/me as admin still answers" : "admin session no longer answers",
            blocked: alive ? "" : "admin session evicted - stopping this step");
        return alive;
    }

    static async Task<int> AccCreateUser(Raw raw, Recorder rec, string loginId, string name, object extra)
    {
        var body = JObject.FromObject(extra);
        body["login_id"] = loginId;
        body["password"] = ACC_PW;
        body["name"] = name;
        body["role"] = "USER";
        var (s, j) = await raw.Post("users", body).ConfigureAwait(false);
        if (s != 200 && s != 201) { Console.WriteLine($"   arrange {loginId}: HTTP {s} {Short(j)}"); return 0; }
        var id = (int)j["data"]!["id"]!;
        rec.Created("user", id);
        return id;
    }

    static async Task AccDeleteUser(Raw raw, Recorder rec, int id)
    {
        if (id <= 0) return;
        var (s, _) = await raw.Delete($"users/{id}").ConfigureAwait(false);
        if (s == 200 || s == 204) rec.Deleted("user", id); else rec.Leftover("user", id, $"DELETE {s}");
    }

    static async Task<int> AccFindUserId(Raw raw, string loginId)
    {
        for (var page = 1; page <= 20; page++)
        {
            var (s, j) = await raw.Get($"users?page={page}&limit=100").ConfigureAwait(false);
            if (s != 200) return 0;
            var hit = (j["data"] as JArray)?.FirstOrDefault(u => (string?)u["login_id"] == loginId);
            if (hit is not null) return (int)hit["id"]!;
            var pages = (int?)(j["pagination"] as JObject)?["total_pages"] ?? 1;
            if (page >= pages) return 0;
        }
        return 0;
    }

    /// <summary>지난 회차가 남긴 내 접두 잔여물만 치운다(다른 작업자의 접두는 건드리지 않는다).</summary>
    static async Task AccSweep(Raw raw, Recorder rec)
    {
        for (var page = 1; page <= 20; page++)
        {
            var (s, j) = await raw.Get($"users?page={page}&limit=100").ConfigureAwait(false);
            if (s != 200) break;
            foreach (var u in (j["data"] as JArray) ?? new JArray())
            {
                var login = (string?)u["login_id"] ?? "";
                if (!login.StartsWith(ACC_USER_PREFIX, StringComparison.Ordinal)) continue;
                var id = (int)u["id"]!;
                await raw.Delete($"users/{id}/photo").ConfigureAwait(false);
                await raw.Delete($"users/{id}").ConfigureAwait(false);
                Console.WriteLine($"   pre-sweep removed stale user {login} ({id})");
            }
            var pages = (int?)(j["pagination"] as JObject)?["total_pages"] ?? 1;
            if (page >= pages) break;
        }
        var (gs, gj) = await raw.Get("user-groups?page=1&limit=100").ConfigureAwait(false);
        if (gs == 200)
            foreach (var g in (gj["data"] as JArray) ?? new JArray())
                if (((string?)g["name"] ?? "").StartsWith(ACC_GROUP_PREFIX, StringComparison.Ordinal))
                    await raw.Delete($"user-groups/{(int)g["id"]!}").ConfigureAwait(false);
    }

    /// <summary>화면이 실제로 쓰는 변환기 — 절대 URL 이면 그대로, 아니면 로컬 기본 아바타 경로.</summary>
    static string AccRender(string? image)
        => new Ironwall.Dotnet.Libraries.Utils.ImageConverter()
            .Convert(image ?? "", typeof(object), "Full", System.Globalization.CultureInfo.InvariantCulture) as string ?? "";

    static string AccAbsolute(string relative)
        => new Uri(new Uri(Bootstrap.BASE_URL), relative).ToString();

    /// <summary>렌더러가 받을 주소를 실제로 내려받는다(루프백만).</summary>
    static async Task<(int Status, int Length)> AccFetch(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return (-1, 0);   // 로컬 기본 아바타 등 — 서버 이미지가 아니다
        LoopbackGuard.Assert(uri);
        using var h = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, c, ch, e) =>
            {
                if (req.RequestUri is not null) LoopbackGuard.Assert(req.RequestUri);
                return true;
            }
        };
        using var http = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(15) };
        using var res = await http.GetAsync(uri).ConfigureAwait(false);
        var bytes = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return ((int)res.StatusCode, bytes.Length);
    }

    /// <summary>
    /// before(= <c>rec.LastSeq()</c>) 이후의 교환들. 12e79bbe 이후 순번은 두 번째 Bootstrap 까지 실행 전체에서 이어지고
    /// LastSeq 는 최대 순번이므로 순번으로 자른다 — 위치(Skip)로 자르면 LastSeq 와 단위가 달라 전부 놓친다.
    /// </summary>
    static List<WireExchange> AccSince(Recorder rec, int before) => rec.Since(before).ToList();
    static int[] AccSeqs(Recorder rec, int before) => AccSince(rec, before).Select(w => w.Seq).ToArray();

    /// <summary>Raw 는 날짜를 DateTime 으로 바꿔 읽는다(문화권 문자열로 새면 오프셋이 사라진다) — 시각으로 되돌린다.</summary>
    static DateTimeOffset? AccTime(JToken? t)
    {
        if (t is null || t.Type == JTokenType.Null) return null;
        if (t.Type == JTokenType.Date)
        {
            var d = t.Value<DateTime>();
            return d.Kind == DateTimeKind.Unspecified ? new DateTimeOffset(d, TimeZoneInfo.Local.GetUtcOffset(d)) : new DateTimeOffset(d);
        }
        return DateTimeOffset.TryParse((string?)t, out var dto) ? dto : null;
    }

    static JObject? AccParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var r = new JsonTextReader(new StringReader(body)) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(r);
        }
        catch { return null; }
    }

    static bool AccIsBlank(JToken? t)
        => t is null || t.Type == JTokenType.Null || (t.Type == JTokenType.String && string.IsNullOrEmpty((string?)t));
}
