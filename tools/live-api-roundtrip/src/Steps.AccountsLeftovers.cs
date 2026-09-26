// 계정 창 잔여 결함 라이브 왕복(LRT_ONLY=accounts-left) — 1f1daa03 이 남긴 세 가지 + 마이페이지 점검.
//
//   AL1 편집 다이얼로그 [확인](EditorDialogViewModel.HandleAsync(CallEditAccountAdminProcess…)) — 계정 관리 패널의
//       OnClickAccountDetail 로 여는 제품 경로 그대로. 비-ADMIN 편집자(users:view+edit)와 관리자 두 사람으로 본다.
//   AL2 세션 정책 화면(AccountSetupPanelViewModel.ClickSave) — 잠금 임계 1~2 를 서버가 422 로 거절하는 것을 VM 이 먼저 막는가.
//   AL3 등록 다이얼로그(RegisterDialogViewModel.ClickOk) — 서버 모드에서 비어 있는 로컬 목록으로 "첫 계정 = ADMIN" 을 추론하는가.
//   AL4 마이페이지(MyPagePanelViewModel.HandleAsync(CallEditProcess…) → PUT /users/me) — 허용 키만 · 비우기 · 사원번호.
//
// 규칙
//   - 행위(act)는 전부 제품 VM 경로. Raw 는 준비(arrange)와 서버 상태 재확인(assert)에만 쓴다.
//   - 만드는 것은 접두 lrt_al… / LRT-AL-… 로만 만들고 finally 에서 지운다(다른 작업자의 접두는 건드리지 않는다).
//   - 하네스 관리자(admin)의 비밀번호 · 설정 · 세션은 건드리지 않는다. 폐기용 계정 로그인 뒤마다 관리자 세션 생존을 확인한다.
//   - 서버 전역 세션 설정은 스냅샷 → finally 에서 복원 → 복원됐는지 단언한다.
using System.IO;
using System.Net.Http;
using System.Windows;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string AL_TAG = "accounts-left";
    const string AL_USER_PREFIX = "lrt_al";
    const string AL_GROUP_PREFIX = "LRT-AL-";

    // =====================================================================================
    // AL1 — 편집 다이얼로그 [확인] (AccountManagerPanelViewModel.OnClickAccountDetail → EditorDialogViewModel)
    // =====================================================================================
    [ExtraStep(AL_TAG, 710)]
    static async Task AccLeft_AL1_EditorDialogSave(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        await AlSweep(raw).ConfigureAwait(false);
        int groupId = 0, editorId = 0, targetId = 0;
        Bootstrap? second = null;
        try
        {
            groupId = await AlCreateGroup(raw, rec, AL_GROUP_PREFIX + "EDITORS", view: true, edit: true).ConfigureAwait(false);
            editorId = await AccCreateUser(raw, rec, "lrt_al_ed", "LRT AL editor", new { group_id = groupId }).ConfigureAwait(false);
            targetId = await AccCreateUser(raw, rec, "lrt_al_tg", "LRT AL target", new
            {
                department = "LRT-before",
                email = "lrt_al_tg@example.com",
                phone = "010-0000-0710",
            }).ConfigureAwait(false);
            if (groupId == 0 || editorId == 0 || targetId == 0)
            {
                rec.Add("al.1.0", AL_TAG, "arrange group/users", Verdict.BLOCKED, $"group={groupId} editor={editorId} target={targetId}", blocked: "fixture");
                return;
            }

            // ---- (a) 비-ADMIN 편집자(users:view+edit)가 부서만 바꾼다 ----
            second = await AlSecondLogin(rec, "lrt_al_ed", "AL1").ConfigureAwait(false);
            if (second is null) return;
            if (!await AccAdminSessionAlive(boot, rec, "AL1").ConfigureAwait(false)) return;

            var (secondPerm, secondUser) = await AlPermissionFor(second).ConfigureAwait(false);
            var gwEditor = new ApiAccountGateway(second.AccountApi, second.Tokens, secondPerm, null, second.Log);
            var (managerE, editorE, eventsE) = await AlOpenEditor(second, gwEditor, targetId).ConfigureAwait(false);
            if (editorE is null)
            {
                rec.Add("al.1a.0", AL_TAG, "editor dialog opened on the target row (non-ADMIN)", Verdict.BLOCKED,
                    $"target row not in the manager list; popup='{eventsE.LastInfo()}'; editor={secondUser}", blocked: "fixture");
                return;
            }

            second.Wire.CurrentTag = AL_TAG + "/AL1a";
            var before = rec.LastSeq();
            editorE.ViewModel.Department = "LRT-after";                  // 사용자가 부서 칸만 고친다(바인딩 경로)
            await editorE.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
            var putA = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, ta) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var deptA = (string?)Obj(ta["data"])?["department"];
            var bodyA = AccParse(putA?.RequestBody);
            var roleOnWire = bodyA?.ContainsKey("role") == true;
            var okA = putA?.Status == 200 && deptA == "LRT-after" && !roleOnWire;
            rec.Add("al.1a", AL_TAG, "editor dialog [확인] by non-ADMIN editor (users:edit), 부서만 변경 -> 200, role not on the wire, server department changed",
                okA ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {putA?.Status}; role on wire={roleOnWire}; server department='{deptA}'; body={Recorder.Trunc(putA?.RequestBodyRedacted ?? "(none)", 400)}; " +
                $"response={Recorder.Trunc(putA?.ResponseBodyRedacted ?? "(none)", 250)}; popup='{eventsE.LastInfo()}'",
                defectAt: okA ? "" : "Accounts.Ui/ViewModels/Dialogs/EditorDialogViewModel.cs HandleAsync(CallEditAccountAdminProcess) -> gateway.UpdateAccountAsync (whole model + role)",
                seqs: AccSeqs(rec, before));

            // ---- (b) 관리자가 이메일 · 전화를 비우고 상태를 미사용으로 ----
            var adminPerm = new PermissionService();
            adminPerm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gwAdmin = new ApiAccountGateway(boot.AccountApi, boot.Tokens, adminPerm, null, boot.Log);
            var (_, editorB, eventsB) = await AlOpenEditor(boot, gwAdmin, targetId).ConfigureAwait(false);
            if (editorB is null) { rec.Add("al.1b.0", AL_TAG, "editor dialog opened on the target row (admin)", Verdict.BLOCKED, "row not listed", blocked: "fixture"); return; }

            boot.Wire.CurrentTag = AL_TAG + "/AL1b";
            before = rec.LastSeq();
            editorB.ViewModel.EMail = "";                                  // WPF TextBox 를 비우면 "" 가 온다
            editorB.ViewModel.Phone = "";
            editorB.ViewModel.Used = EnumUsedType.NOT_USED;
            await editorB.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
            var putB = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains($"users/{targetId}"));
            var (_, tb) = await raw.Get($"users/{targetId}").ConfigureAwait(false);
            var dB = Obj(tb["data"]);
            var active = (bool?)dB?["is_active"];
            var clearOk = AccIsBlank(dB?["email"]) && AccIsBlank(dB?["phone"]);
            rec.Add("al.1b", AL_TAG, "editor dialog [확인] (admin): 이메일 · 전화 비우기 + 상태=미사용 -> server email/phone blank, is_active=false",
                clearOk && active == false ? Verdict.PASS : Verdict.FAIL,
                $"server email={J(dB?["email"])} phone={J(dB?["phone"])} is_active={active}; HTTP {putB?.Status}; " +
                $"body={Recorder.Trunc(putB?.RequestBodyRedacted ?? "(none)", 400)}; popup='{eventsB.LastInfo()}'",
                defectAt: clearOk && active == false ? "" : "Accounts.Ui/ViewModels/Dialogs/EditorDialogViewModel.cs (full save: is_active never sent) · Accounts.Api/Helpers/AccountDtoMapper.cs ToUserUpdateDto(IAccountModel)",
                seqs: AccSeqs(rec, before));

            var bodyB = AccParse(putB?.RequestBody);
            var extra = bodyB?.Properties().Select(p => p.Name).Where(k => k is not ("email" or "phone" or "is_active")).ToList() ?? new List<string> { "(no body)" };
            rec.Add("al.1b-wire", AL_TAG, "editor dialog [확인] sends only the changed keys (clears as explicit null)",
                bodyB is not null && extra.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                $"keys beyond email/phone/is_active = [{string.Join(",", extra)}]; body={Recorder.Trunc(putB?.RequestBodyRedacted ?? "(none)", 400)}",
                defectAt: extra.Count == 0 ? "" : "Accounts.Ui/ViewModels/Dialogs/EditorDialogViewModel.cs (whole model sent)");

            // ---- (c) 아무것도 안 바꾸고 [확인] → 서버 쓰기 없이 끝난다 ----
            var (_, editorC, eventsC) = await AlOpenEditor(boot, gwAdmin, targetId).ConfigureAwait(false);
            if (editorC is not null)
            {
                boot.Wire.CurrentTag = AL_TAG + "/AL1c";
                before = rec.LastSeq();
                await editorC.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
                var putC = AccSince(rec, before).Where(w => w.Method == "PUT").ToList();
                var bodyC = putC.Count == 0 ? null : AccParse(putC[0].RequestBody);
                var noOp = putC.Count == 0 || (bodyC is not null && !bodyC.Properties().Any());
                rec.Add("al.1c", AL_TAG, "editor dialog [확인] with nothing changed -> no field is written",
                    noOp ? Verdict.PASS : Verdict.FAIL,
                    $"PUT count={putC.Count}; body={Recorder.Trunc(putC.FirstOrDefault()?.RequestBodyRedacted ?? "(none)", 300)}; popup='{eventsC.LastInfo()}'",
                    defectAt: noOp ? "" : "Accounts.Ui/ViewModels/Dialogs/EditorDialogViewModel.cs (unchanged fields re-sent)");
            }
        }
        catch (Exception ex)
        {
            rec.Add("al.1!", AL_TAG, "editor dialog save", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (second is not null) { try { await second.AccountApi.LogoutAsync().ConfigureAwait(false); } catch { /* 계정 삭제가 세션을 정리한다 */ } }
            await AccDeleteUser(raw, rec, targetId).ConfigureAwait(false);
            await AccDeleteUser(raw, rec, editorId).ConfigureAwait(false);
            await AlDeleteGroup(raw, rec, groupId).ConfigureAwait(false);
        }
    }

    // =====================================================================================
    // AL2 — 세션 정책 화면 잠금 임계(AccountSetupPanelViewModel.ClickSave → PUT /settings/session)
    // =====================================================================================
    [ExtraStep(AL_TAG, 720)]
    static async Task AccLeft_AL2_LockoutThreshold(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        var (snapStatus, snapJo) = await raw.Get("settings/session").ConfigureAwait(false);
        var snapshot = Obj(snapJo["data"]);
        if (snapStatus != 200 || snapshot is null)
        {
            rec.Add("al.2.0", AL_TAG, "snapshot settings/session", Verdict.BLOCKED, $"HTTP {snapStatus} {Short(snapJo)}",
                blocked: "could not snapshot the shared server setting - refusing to write it");
            return;
        }

        var oldGet = IoC.GetInstance;
        try
        {
            // 서버 규칙 자체(조건 증명) — 거절되는 값이라 서버 상태는 바뀌지 않는다.
            boot.Wire.CurrentTag = AL_TAG + "/AL2-rule";
            var (ruleStatus, ruleJo) = await raw.Send(HttpMethod.Put, "settings/session", new JObject { ["lockout_threshold"] = 2 }).ConfigureAwait(false);
            rec.Add("al.2.rule", AL_TAG, "server rule: PUT settings/session {lockout_threshold:2} -> 422 (0 or 3~20 only)",
                ruleStatus == 422 ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {ruleStatus} {Recorder.Trunc(Short(ruleJo), 300)}",
                defectAt: ruleStatus == 422 ? "" : "(server rule differs from app/schemas/settings.py - re-check the contract)");

            IoC.GetInstance = (type, key) => type == typeof(IAccountApiService) ? boot.AccountApi : oldGet?.Invoke(type, key);

            foreach (var bad in new[] { 1, 2 })
            {
                var events = new AccVmEvents();
                var vm = new AccountSetupPanelViewModel(events, boot.Log);
                await vm.ClickReload().ConfigureAwait(false);
                if (!vm.ServerSettingsAvailable)
                {
                    rec.Add($"al.2.{bad}.0", AL_TAG, "panel loads server settings", Verdict.BLOCKED, vm.ServerStatus, blocked: "panel could not load");
                    return;
                }
                vm.LockoutThreshold = bad;                                  // 화면 입력 칸(바인딩)
                boot.Wire.CurrentTag = AL_TAG + $"/AL2-{bad}";
                var before = rec.LastSeq();
                await vm.ClickSave().ConfigureAwait(false);
                var put = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains("settings/session"));
                var popup = events.LastInfo();
                var blocked = put is null && popup.Contains("3") && popup.Contains("20") && popup.Contains("잠금");
                rec.Add($"al.2.{bad}", AL_TAG, $"session policy [저장] with lockout threshold {bad} -> blocked in the VM (no PUT) with a Korean message naming 0 / 3~20",
                    blocked ? Verdict.PASS : Verdict.FAIL,
                    $"PUT sent={(put is not null)} (HTTP {put?.Status}; body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 250)}; " +
                    $"response={Recorder.Trunc(put?.ResponseBodyRedacted ?? "(none)", 250)}); popup='{popup}'",
                    defectAt: blocked ? "" : "Accounts.Ui/ViewModels/Panels/AccountSetupPanelViewModel.cs ClickSave (LockoutThreshold is < 0 or > 20 lets 1~2 through)",
                    seqs: AccSeqs(rec, before));
            }

            // 막는 규칙이 멀쩡한 현재값까지 막지 않는지 — 불러온 그대로 [저장].
            {
                var events = new AccVmEvents();
                var vm = new AccountSetupPanelViewModel(events, boot.Log);
                await vm.ClickReload().ConfigureAwait(false);
                boot.Wire.CurrentTag = AL_TAG + "/AL2-unchanged";
                var before = rec.LastSeq();
                await vm.ClickSave().ConfigureAwait(false);
                var put = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.Contains("settings/session"));
                rec.Add("al.2.ok", AL_TAG, "session policy [저장] with the loaded (valid) values still goes through -> 200",
                    put?.Status == 200 ? Verdict.PASS : Verdict.FAIL,
                    $"loaded lockout_threshold={vm.LockoutThreshold}; HTTP {put?.Status}; popup='{events.LastInfo()}'",
                    defectAt: put?.Status == 200 ? "" : "Accounts.Ui/ViewModels/Panels/AccountSetupPanelViewModel.cs ClickSave (over-blocking)",
                    seqs: AccSeqs(rec, before));
            }
        }
        catch (Exception ex)
        {
            rec.Add("al.2!", AL_TAG, "lockout threshold", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            IoC.GetInstance = oldGet;
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
            rec.Add("al.2.restore", AL_TAG, "shared settings/session restored to the snapshot",
                remaining.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                (drift.Count == 0 ? "nothing drifted" : "drifted [" + string.Join(", ", drift) + "] -> restored") +
                (remaining.Count == 0 ? "" : "; STILL DIFFERENT: " + string.Join(", ", remaining)),
                defectAt: remaining.Count == 0 ? "" : "harness restore");
        }
    }

    // =====================================================================================
    // AL3 — 등록 다이얼로그의 "첫 계정 = ADMIN" 추론(RegisterDialogViewModel.ClickOk → POST /users)
    // =====================================================================================
    [ExtraStep(AL_TAG, 730)]
    static async Task AccLeft_AL3_RegisterRole(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int groupId = 0, creatorId = 0;
        Bootstrap? second = null;
        var tmpDir = Path.Combine(Path.GetTempPath(), "lrt_al_vm");
        try
        {
            Directory.CreateDirectory(tmpDir);

            // ---- (a) 실제 경로로 빈 목록이 생기는 경우: users:edit 만 있고 users:view 는 없는 운영자 ----
            groupId = await AlCreateGroup(raw, rec, AL_GROUP_PREFIX + "CREATORS", view: false, edit: true).ConfigureAwait(false);
            creatorId = await AccCreateUser(raw, rec, "lrt_al_cr", "LRT AL creator", new { group_id = groupId }).ConfigureAwait(false);
            if (groupId == 0 || creatorId == 0) { rec.Add("al.3.0", AL_TAG, "arrange creator", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            second = await AlSecondLogin(rec, "lrt_al_cr", "AL3").ConfigureAwait(false);
            if (second is null) return;
            if (!await AccAdminSessionAlive(boot, rec, "AL3").ConfigureAwait(false)) return;

            var (perm, _) = await AlPermissionFor(second).ConfigureAwait(false);
            var gw = new ApiAccountGateway(second.AccountApi, second.Tokens, perm, null, second.Log);
            var provider = new AccountProvider();                      // 앱 시작 직후의 싱글톤 — 비어 있다
            var events = new AccVmEvents();
            var editor = new EditorDialogViewModel(events, second.Log, new AccountViewModel(events, second.Log, new AccountModel()),
                gw, new AccVmSessionConfig(), new ProfileImageService(second.Log, tmpDir), gw);
            var manager = new AccountManagerPanelViewModel(events, second.Log, editor, provider,
                new LoginViewModel(events, second.Log, new AccountModel()), gw);
            second.Wire.CurrentTag = AL_TAG + "/AL3a-list";
            await ScreenExtensions.TryActivateAsync(manager).ConfigureAwait(false);   // GET /users → 403 → 목록이 비어 남는다
            rec.Add("al.3a.pre", AL_TAG, "precondition: account list could not load for a users:edit-only operator -> local provider stays empty",
                Verdict.INFO, $"provider count={provider.Count()}; popup='{events.LastInfo()}'");

            var (st, cj) = await AlRegister(second, rec, provider, gw, tmpDir, "lrt_al_new1", "LRT AL new1", AL_TAG + "/AL3a").ConfigureAwait(false);
            var storedA = await AlStoredRole(raw, "lrt_al_new1").ConfigureAwait(false);
            var okA = st.Post?.Status is 200 or 201 && storedA == "USER" && st.SentRole != "ADMIN";
            rec.Add("al.3a", AL_TAG, "register dialog (server mode, list not loaded) by a users:edit operator -> account created as USER, not ADMIN/403",
                okA ? Verdict.PASS : Verdict.FAIL,
                $"sent role={st.SentRole ?? "(absent)"}; HTTP {st.Post?.Status}; stored role={storedA ?? "(not created)"}; " +
                $"response={Recorder.Trunc(st.Post?.ResponseBodyRedacted ?? "(none)", 250)}; popup='{cj}'",
                defectAt: okA ? "" : "Accounts.Ui/ViewModels/Dialogs/RegisterDialogViewModel.cs ClickOk (AccountProvider.Count()==0 -> Level=ADMIN)",
                seqs: st.Seqs);

            // ---- (b) 관리자가 목록이 아직 안 찬 상태에서 등록 ----
            var adminPerm = new PermissionService();
            adminPerm.Apply(new AuthUserDto { Id = 1, LoginId = Bootstrap.LOGIN_ID, Name = "admin", Role = "ADMIN", IsActive = true });
            var gwAdmin = new ApiAccountGateway(boot.AccountApi, boot.Tokens, adminPerm, null, boot.Log);
            var (sb, pb) = await AlRegister(boot, rec, new AccountProvider(), gwAdmin, tmpDir, "lrt_al_new2", "LRT AL new2", AL_TAG + "/AL3b").ConfigureAwait(false);
            var storedB = await AlStoredRole(raw, "lrt_al_new2").ConfigureAwait(false);
            var okB = sb.Post?.Status is 200 or 201 && storedB == "USER";
            rec.Add("al.3b", AL_TAG, "register dialog (server mode, local list empty) by ADMIN -> a normal registration is stored as USER (never silently ADMIN)",
                okB ? Verdict.PASS : Verdict.FAIL,
                $"sent role={sb.SentRole ?? "(absent)"}; HTTP {sb.Post?.Status}; stored role={storedB ?? "(not created)"}; popup='{pb}'",
                defectAt: okB ? "" : "Accounts.Ui/ViewModels/Dialogs/RegisterDialogViewModel.cs ClickOk (first-account=ADMIN inferred from an empty local list)",
                seqs: sb.Seqs);
        }
        catch (Exception ex)
        {
            rec.Add("al.3!", AL_TAG, "register role", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (second is not null) { try { await second.AccountApi.LogoutAsync().ConfigureAwait(false); } catch { } }
            foreach (var login in new[] { "lrt_al_new1", "lrt_al_new2" })
                await AccDeleteUser(raw, rec, await AccFindUserId(raw, login).ConfigureAwait(false)).ConfigureAwait(false);
            await AccDeleteUser(raw, rec, creatorId).ConfigureAwait(false);
            await AlDeleteGroup(raw, rec, groupId).ConfigureAwait(false);
        }
    }

    // =====================================================================================
    // AL4 — 마이페이지 자기 수정(MyPagePanelViewModel → PUT /users/me)
    // =====================================================================================
    [ExtraStep(AL_TAG, 740)]
    static async Task AccLeft_AL4_MyPage(ExtraContext ctx)
    {
        var (boot, rec, raw) = (ctx.Boot, ctx.Rec, ctx.Raw);
        int meId = 0;
        Bootstrap? second = null;
        var tmpDir = Path.Combine(Path.GetTempPath(), "lrt_al_vm");
        try
        {
            Directory.CreateDirectory(tmpDir);
            meId = await AccCreateUser(raw, rec, "lrt_al_me", "LRT AL me", new
            {
                email = "lrt_al_me@example.com",
                phone = "010-0000-0740",
                department = "LRT-me-dept",
                employee_number = "LRT-EMP-1",
            }).ConfigureAwait(false);
            if (meId == 0) { rec.Add("al.4.0", AL_TAG, "arrange user", Verdict.BLOCKED, "create failed", blocked: "fixture"); return; }

            second = await AlSecondLogin(rec, "lrt_al_me", "AL4").ConfigureAwait(false);
            if (second is null) return;
            if (!await AccAdminSessionAlive(boot, rec, "AL4").ConfigureAwait(false)) return;

            var (perm, _) = await AlPermissionFor(second).ConfigureAwait(false);
            var gw = new ApiAccountGateway(second.AccountApi, second.Tokens, perm, null, second.Log);
            var events = new AccVmEvents();
            var login = new LoginViewModel(events, second.Log, new AccountModel { Id = meId });
            var page = new MyPagePanelViewModel(events, second.Log, login, gw, new ProfileImageService(second.Log, tmpDir));
            await ScreenExtensions.TryActivateAsync(page).ConfigureAwait(false);   // GET /users/me → 화면에 채운다
            if (page.ViewModel.Username != "lrt_al_me") { rec.Add("al.4.0", AL_TAG, "my page loaded", Verdict.BLOCKED, $"username='{page.ViewModel.Username}'", blocked: "fixture"); return; }

            second.Wire.CurrentTag = AL_TAG + "/AL4";
            var before = rec.LastSeq();
            page.ViewModel.EMail = "";                                  // WPF 로 비우면 ""
            page.ViewModel.Phone = "";
            page.ViewModel.Department = "LRT-me-dept2";
            page.ViewModel.EmployeeNumber = "LRT-EMP-2";                 // 화면에서 편집 가능한 칸
            await page.HandleAsync(new CallEditProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
            var put = AccSince(rec, before).FirstOrDefault(w => w.Method == "PUT" && w.Uri.EndsWith("users/me", StringComparison.Ordinal));
            var body = AccParse(put?.RequestBody);
            var allowed = new[] { "name", "email", "department", "position", "phone", "photo_url" };
            var foreign = body?.Properties().Select(p => p.Name).Where(k => !allowed.Contains(k)).ToList() ?? new List<string> { "(no body)" };
            var popup = events.LastInfo();
            rec.Add("al.4-wire", AL_TAG, "my page [적용] -> PUT /users/me carries only the self-editable keys, HTTP 200",
                put?.Status == 200 && foreign.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {put?.Status}; foreign keys=[{string.Join(",", foreign)}]; body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 400)}",
                defectAt: foreign.Count == 0 ? "" : "Messages/Dto/Accounts/UserSelfUpdateDto.cs · Accounts.Api/Helpers/AccountDtoMapper.cs ToUserSelfUpdateDto",
                seqs: AccSeqs(rec, before));

            var (_, mj) = await raw.Get($"users/{meId}").ConfigureAwait(false);
            var d = Obj(mj["data"]);
            var cleared = AccIsBlank(d?["email"]) && AccIsBlank(d?["phone"]) && (string?)d?["department"] == "LRT-me-dept2";
            rec.Add("al.4-clear", AL_TAG, "my page: 이메일 · 전화 비우기 + 부서 변경 -> server email/phone blank, department changed",
                cleared ? Verdict.PASS : Verdict.FAIL,
                $"server email={J(d?["email"])} phone={J(d?["phone"])} department={J(d?["department"])}; popup='{popup}'",
                defectAt: cleared ? "" : "Accounts.Api/Helpers/AccountDtoMapper.cs ToUserSelfUpdateDto (null ignored -> clear impossible)");

            // 사원번호: 서버는 본인 경로로 바꾸게 두지 않는다(AccountUserSelfUpdate 에 키가 없다) — 화면이 "완료" 라고만 하면 거짓이다.
            var emp = (string?)d?["employee_number"];
            var empSaved = emp == "LRT-EMP-2";
            var empTold = popup.Contains("사원번호");
            rec.Add("al.4-emp", AL_TAG, "my page: 사원번호 edit is either saved or the user is told it is not saved (no false '완료')",
                empSaved || empTold ? Verdict.PASS : Verdict.FAIL,
                $"server employee_number='{emp}'; popup='{popup}'",
                defectAt: empSaved || empTold ? "" : "Accounts.Ui/ViewModels/Panels/MyPagePanelViewModel.cs HandleAsync(CallEditProcess) · Views/Panels/MyPagePanelView.xaml (사원번호 editable, never sent)");

            rec.Add("al.4-photo", AL_TAG, "my page save writes back photo_url (info)", Verdict.INFO,
                $"sent photo_url={J(body?["photo_url"])}; stored photo_url={J(d?["photo_url"])}");
        }
        catch (Exception ex)
        {
            rec.Add("al.4!", AL_TAG, "my page", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (second is not null) { try { await second.AccountApi.LogoutAsync().ConfigureAwait(false); } catch { } }
            await AccDeleteUser(raw, rec, meId).ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ helpers

    sealed record AlRegisterWire(WireExchange? Post, string? SentRole, int[] Seqs);

    /// <summary>등록 다이얼로그를 제품 경로로 한 번 돌린다(OnActivate → 입력 → ClickOk).</summary>
    static async Task<(AlRegisterWire Wire, string Popup)> AlRegister(Bootstrap who, Recorder rec, AccountProvider provider,
        ApiAccountGateway gw, string tmpDir, string loginId, string name, string tag)
    {
        var events = new AccVmEvents();
        var reg = new RegisterDialogViewModel(events, who.Log, new RegisterViewModel(events, who.Log, new AccountModel()),
            provider, gw, new ProfileImageService(who.Log, tmpDir));
        await ScreenExtensions.TryActivateAsync(reg).ConfigureAwait(false);
        reg.Username = loginId;
        reg.Name = name;
        reg.RegisterPass = ACC_PW;
        reg.PasswordConfirm = ACC_PW;
        who.Wire.CurrentTag = tag;
        var before = rec.LastSeq();
        await reg.ClickOk().ConfigureAwait(false);
        var post = AccSince(rec, before).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/users", StringComparison.Ordinal));
        var role = (string?)AccParse(post?.RequestBody)?["role"];
        var id = (int?)AccParse(post?.ResponseBody)?["data"]?["id"] ?? 0;
        if (id > 0) rec.Created("user", id);
        return (new AlRegisterWire(post, role, AccSeqs(rec, before)), events.LastInfo());
    }

    static async Task<string?> AlStoredRole(Raw raw, string loginId)
    {
        var id = await AccFindUserId(raw, loginId).ConfigureAwait(false);
        if (id <= 0) return null;
        var (_, j) = await raw.Get($"users/{id}").ConfigureAwait(false);
        return (string?)Obj(j["data"])?["role"];
    }

    /// <summary>계정 관리 패널을 세우고(목록 로드) 대상 행을 골라 편집 다이얼로그를 여는 제품 경로.</summary>
    static async Task<(AccountManagerPanelViewModel Manager, EditorDialogViewModel? Editor, AccVmEvents Events)> AlOpenEditor(
        Bootstrap who, ApiAccountGateway gw, int targetId)
    {
        var events = new AccVmEvents();
        var tmp = Path.Combine(Path.GetTempPath(), "lrt_al_vm");
        var editor = new EditorDialogViewModel(events, who.Log, new AccountViewModel(events, who.Log, new AccountModel()),
            gw, new AccVmSessionConfig(), new ProfileImageService(who.Log, tmp), gw);
        var manager = new AccountManagerPanelViewModel(events, who.Log, editor, new AccountProvider(),
            new LoginViewModel(events, who.Log, new AccountModel()), gw);
        await ScreenExtensions.TryActivateAsync(manager).ConfigureAwait(false);
        var row = manager.ViewModelProvider.FirstOrDefault(r => r.Id == targetId);
        if (row is null) return (manager, null, events);
        manager.SelectedItem = row;
        manager.OnClickAccountDetail(manager, new RoutedEventArgs());       // 행 더블클릭 = 편집 다이얼로그 열기
        await ScreenExtensions.TryActivateAsync(editor).ConfigureAwait(false);   // 호스트 ConductorControlViewModel 이 하는 활성화
        return (manager, editor, events);
    }

    static async Task<(PermissionService Perm, string Who)> AlPermissionFor(Bootstrap second)
    {
        var perm = new PermissionService();
        var me = await second.AccountApi.GetMeAsync().ConfigureAwait(false);
        if (me is not null) perm.Apply(me);
        return (perm, me is null ? "(unknown)" : $"{me.LoginId}/{me.Role}");
    }

    static async Task<Bootstrap?> AlSecondLogin(Recorder rec, string loginId, string where)
    {
        var second = new Bootstrap(rec);
        await second.InitAsync().ConfigureAwait(false);
        second.Wire.CurrentTag = AL_TAG + $"/{where}-login";
        var login = await second.AccountApi.LoginAsync(loginId, ACC_PW).ConfigureAwait(false);
        if (!login.Success || login.Data?.User is null)
        {
            rec.Add($"al.{where}.login", AL_TAG, "throwaway login", Verdict.BLOCKED, $"{login.Error?.Code} {login.Message}", blocked: "fixture");
            return null;
        }
        second.Tokens.SetTokens(login.Data.AccessToken, login.Data.RefreshToken, login.Data.SessionId);
        return second;
    }

    static async Task<int> AlCreateGroup(Raw raw, Recorder rec, string name, bool view, bool edit)
    {
        var (gs, gj) = await raw.Post("user-groups", new { name, description = "live roundtrip accounts-left" }).ConfigureAwait(false);
        if (gs != 200 && gs != 201) { Console.WriteLine($"   arrange group {name}: HTTP {gs} {Short(gj)}"); return 0; }
        var id = (int)gj["data"]!["id"]!;
        rec.Created("user-group", id);
        var mods = new JObject();
        foreach (var m in ALL_MODULES)
            mods[m] = new JObject { ["view"] = m == "users" && view, ["edit"] = m == "users" && edit, ["delete"] = false, ["control"] = false };
        var (ps, pj) = await raw.Post($"user-groups/{id}/permissions", new JObject { ["modules"] = mods }).ConfigureAwait(false);
        if (ps != 200 && ps != 201) Console.WriteLine($"   arrange group permissions {name}: HTTP {ps} {Short(pj)}");
        return id;
    }

    static async Task AlDeleteGroup(Raw raw, Recorder rec, int groupId)
    {
        if (groupId <= 0) return;
        var (s, _) = await raw.Delete($"user-groups/{groupId}").ConfigureAwait(false);
        if (s == 200 || s == 204) rec.Deleted("user-group", groupId); else rec.Leftover("user-group", groupId, $"DELETE {s}");
    }

    /// <summary>지난 회차가 남긴 내 접두(lrt_al / LRT-AL-) 잔여물만 치운다.</summary>
    static async Task AlSweep(Raw raw)
    {
        for (var page = 1; page <= 20; page++)
        {
            var (s, j) = await raw.Get($"users?page={page}&limit=100").ConfigureAwait(false);
            if (s != 200) break;
            foreach (var u in (j["data"] as JArray) ?? new JArray())
            {
                var login = (string?)u["login_id"] ?? "";
                if (!login.StartsWith(AL_USER_PREFIX, StringComparison.Ordinal)) continue;
                await raw.Delete($"users/{(int)u["id"]!}").ConfigureAwait(false);
                Console.WriteLine($"   pre-sweep removed stale user {login}");
            }
            var pages = (int?)(j["pagination"] as JObject)?["total_pages"] ?? 1;
            if (page >= pages) break;
        }
        var (gs, gj) = await raw.Get("user-groups?page=1&limit=100").ConfigureAwait(false);
        if (gs == 200)
            foreach (var g in (gj["data"] as JArray) ?? new JArray())
                if (((string?)g["name"] ?? "").StartsWith(AL_GROUP_PREFIX, StringComparison.Ordinal))
                    await raw.Delete($"user-groups/{(int)g["id"]!}").ConfigureAwait(false);
    }
}
