using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;

/****************************************************************************
   Purpose   : GrantManagementPanelViewModel 권한 부여 기능 시뮬레이션(110+ 시나리오, 2회).
   대상      : Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.GrantManagementPanelViewModel
   서버계약  : FakeGopServer(api-test-server grants.py 규칙 충실 모사).
   보증방식  : 각 시나리오마다 신선한 서버/VM(상호격리) → 단계별 PASS/FAIL 로그 → 2회 실행 결정론 비교.
****************************************************************************/
internal static class GrantScenarios
{
    // ── 팝업 조회 헬퍼 ──
    private static bool HasInfo(RecordingEventAggregator ea, string contains)
        => ea.OfType<OpenInfoPopupMessageModel>().Any(m => (m.Explain ?? string.Empty).Contains(contains));
    private static int N<T>(RecordingEventAggregator ea) => ea.OfType<T>().Count();

    public static async Task RunAllAsync(SimLog log)
    {
        var ct = CancellationToken.None;

        // ══════════════ A. 초기 로드(OnActivate) ══════════════
        log.Section("A. 초기 로드 (OnActivate → 계정/그룹/전체부여)");
        {
            var s = GrantFixtures.NewServer();
            var g1 = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-2));    // ACTIVE(상시)
            var g2 = s.Seed(3, 11, GrantFixtures.T0.AddHours(1), GrantFixtures.T0.AddHours(2), GrantFixtures.T0.AddHours(-1)); // PENDING
            var (vm, ea, _) = GrantFixtures.NewVm(s);
            await vm.ActivateForTestAsync();

            log.Check("A1", "계정 콤보 3건 로드", vm.Accounts.Count == 3, $"count={vm.Accounts.Count}");
            log.Check("A2", "그룹 콤보 3건 로드", vm.Groups.Count == 3, $"count={vm.Groups.Count}");
            log.Check("A3", "전체 부여 2건 로드", vm.Grants.Count == 2, $"count={vm.Grants.Count}");
            log.Check("A4", "정상 로드시 안내팝업 없음", N<OpenInfoPopupMessageModel>(ea) == 0);
            log.Check("A5", "초기 선택계정 null", vm.SelectedAccount is null);
            log.Check("A6", "초기 부여버튼 비활성(CanCreateGrant=false)", vm.CanCreateGrant == false);
            log.Check("A7", "created_at desc 정렬(최신 g2 선두)", vm.Grants[0].Id == g2.Id, $"head={vm.Grants[0].Id}");
            log.Check("A8", "계정 목록에 op1 포함", vm.Accounts.Any(a => a.LoginId == "op1"));
            log.Check("A9", "그룹 목록에 '야간조' 포함", vm.Groups.Any(gp => gp.Name == "야간조"));
            log.Check("A10", "전체부여 단일 호출(N-순회 아님)", s.ListAllCallCount == 1, $"calls={s.ListAllCallCount}");
            log.Check("A11", "부여행 계정 비정규화(UserLogin) 표시", vm.Grants.Any(x => x.Id == g1.Id && x.UserLogin == "op1"));
            log.Check("A12", "부여행 그룹명 비정규화 표시", vm.Grants.Any(x => x.Id == g2.Id && x.GroupName == "주간조"));
        }

        // ══════════════ B. CanCreateGrant 게이팅 ══════════════
        log.Section("B. 부여버튼 활성 게이팅(CanCreateGrant)");
        {
            var s = GrantFixtures.NewServer();
            var (vm, _, _) = GrantFixtures.NewVm(s);
            await vm.OnClickReloadButton();
            var acc = vm.Accounts.First(a => a.LoginId == "op1");
            var grp = vm.Groups.First(g => g.Name == "야간조");

            vm.SelectedAccount = null; vm.SelectedGroup = null;
            log.Check("B1", "계정·그룹 모두 null → 비활성", vm.CanCreateGrant == false);
            vm.SelectedAccount = acc; vm.SelectedGroup = null;
            log.Check("B2", "계정만 선택 → 비활성", vm.CanCreateGrant == false);
            vm.SelectedAccount = null; vm.SelectedGroup = grp;
            log.Check("B3", "그룹만 선택 → 비활성", vm.CanCreateGrant == false);
            vm.SelectedAccount = acc; vm.SelectedGroup = grp;
            log.Check("B4", "둘 다 선택 → 활성", vm.CanCreateGrant == true);
            vm.SelectedGroup = null;
            log.Check("B5", "그룹 해제 → 다시 비활성", vm.CanCreateGrant == false);
        }

        // ══════════════ C. 생성 클라 1차 경계검증(until<=from → POST 미도달) ══════════════
        log.Section("C. 생성 클라 사전검증(종료<=시작 이면 서버 미호출)");
        {
            // C1: until < from
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0.AddHours(2);
                vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("C1a", "종료<시작: 경계 안내팝업", HasInfo(ea, "종료 일시는 시작 일시보다 뒤여야"));
                log.Check("C1b", "종료<시작: 서버 CreateGrant 미호출", s.CreateCallCount == 0, $"calls={s.CreateCallCount}");
            }
            // C2: until == from
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0.AddHours(3);
                vm.ValidUntil = GrantFixtures.T0.AddHours(3);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("C2a", "종료==시작: 경계 안내팝업", HasInfo(ea, "종료 일시는 시작 일시보다 뒤여야"));
                log.Check("C2b", "종료==시작: 서버 미호출", s.CreateCallCount == 0);
            }
            // C3: until = from + 1min → 서버 도달·성공
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0;
                vm.ValidUntil = GrantFixtures.T0.AddMinutes(1);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("C3a", "종료>시작: 서버 CreateGrant 도달", s.CreateCallCount == 1, $"calls={s.CreateCallCount}");
                log.Check("C3b", "종료>시작: 부여 성공 안내", HasInfo(ea, "부여 완료"));
            }
            // C4: until null(상시) → 서버 도달
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0;
                vm.ValidUntil = null;
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("C4a", "상시(종료 생략): 서버 도달", s.CreateCallCount == 1);
                log.Check("C4b", "상시: 부여 성공 안내", HasInfo(ea, "부여 완료"));
            }
        }

        // ══════════════ D. 생성 서버측 결과 ══════════════
        log.Section("D. 생성 서버측 결과(성공/422/404/403 + 폼 리셋)");
        {
            // D1: 정상 미래구간 → 성공·ACTIVE·목록반영
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D1a", "성공 안내팝업 '부여 완료'", HasInfo(ea, "부여 완료"));
                log.Check("D1b", "서버 부여행 1건 생성", s.GrantRows.Count == 1, $"rows={s.GrantRows.Count}");
                log.Check("D1c", "목록 재조회 반영(1건)", vm.Grants.Count == 1);
                log.Check("D1d", "상태 ACTIVE", vm.Grants.Count == 1 && vm.Grants[0].Status == "ACTIVE", vm.Grants.FirstOrDefault()?.Status ?? "-");
                log.Check("D1e", "부여행 계정 op1", vm.Grants.FirstOrDefault()?.UserLogin == "op1");
                log.Check("D1f", "부여행 그룹 야간조", vm.Grants.FirstOrDefault()?.GroupName == "야간조");
            }
            // D2: 과거 종료(until>from 이나 until<=now) → 422
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0.AddHours(-2); vm.ValidUntil = GrantFixtures.T0.AddHours(-1);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D2a", "과거종료: 서버 도달(클라 미차단)", s.CreateCallCount == 1);
                log.Check("D2b", "과거종료: 422 실패 안내(미래여야)", HasInfo(ea, "valid_until must be in the future"));
                log.Check("D2c", "과거종료: 서버 부여행 미생성", s.GrantRows.Count == 0);
            }
            // D3: 과거 시작 + 상시 → 성공(ACTIVE) — 서버는 valid_from 과거를 허용
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0.AddHours(-1); vm.ValidUntil = null;
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D3a", "과거시작+상시: 성공", HasInfo(ea, "부여 완료") && s.GrantRows.Count == 1);
                log.Check("D3b", "과거시작+상시: ACTIVE", vm.Grants.FirstOrDefault()?.Status == "ACTIVE");
            }
            // D4: 미래 시작 + 미래 종료 → 성공(PENDING)
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0.AddHours(1); vm.ValidUntil = GrantFixtures.T0.AddHours(2);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D4a", "미래구간: 성공", s.GrantRows.Count == 1);
                log.Check("D4b", "미래구간: PENDING", vm.Grants.FirstOrDefault()?.Status == "PENDING", vm.Grants.FirstOrDefault()?.Status ?? "-");
            }
            // D5: 대상 계정 없음 → 404
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                s.Users.RemoveAll(u => u.Id == 2);   // 선택 후 서버에서 계정 소멸
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D5a", "계정없음: 404 실패 안내", HasInfo(ea, "User not found"));
                log.Check("D5b", "계정없음: 부여행 미생성", s.GrantRows.Count == 0);
            }
            // D6: 대상 그룹 없음 → 404
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                s.Groups.RemoveAll(g => g.Id == 10);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D6a", "그룹없음: 404 실패 안내", HasInfo(ea, "User group not found"));
                log.Check("D6b", "그룹없음: 부여행 미생성", s.GrantRows.Count == 0);
            }
            // D7: 비ADMIN 부여 → 403
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                s.ActorRole = "OPERATOR";
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("D7a", "비ADMIN: 403 실패 안내(role)", HasInfo(ea, "Insufficient role"));
                log.Check("D7b", "비ADMIN: 부여행 미생성", s.GrantRows.Count == 0);
            }
            // D8: 성공 후 폼 리셋
            {
                var s = GrantFixtures.NewServer(); var (vm, _, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                await vm.ClickCreateGrant();
                log.Check("D8a", "성공 후 선택계정 리셋(null)", vm.SelectedAccount is null);
                log.Check("D8b", "성공 후 선택그룹 리셋(null)", vm.SelectedGroup is null);
                log.Check("D8c", "성공 후 종료 리셋(null)", vm.ValidUntil is null);
                log.Check("D8d", "성공 후 부여버튼 비활성", vm.CanCreateGrant == false);
            }
        }

        // ══════════════ E. 생성 예외(transport) ══════════════
        log.Section("E. 생성 예외 경로(트랜스포트 실패 → 로깅·무크래시)");
        {
            var s = GrantFixtures.NewServer(); var (vm, ea, lg) = GrantFixtures.NewVm(s);
            await vm.OnClickReloadButton();
            vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
            vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
            vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
            s.ThrowOnCreate = true;
            ea.ClearPublished();
            await vm.ClickCreateGrant();   // 예외를 VM이 흡수해야 함
            log.Check("E1", "예외를 흡수(무크래시)", true);
            log.Check("E2", "에러 로그 '[GrantMgmt] 부여 실패'", lg.Errors.Any(e => e.Contains("부여 실패")));
            log.Check("E3", "예외시 성공팝업 없음", N<OpenInfoPopupMessageModel>(ea) == 0);
        }

        // ══════════════ F. 회수 확인 플로우 ══════════════
        log.Section("F. 회수 확인 플로우(클릭→확인→Yes/No→DELETE, 멱등·404·403·예외)");
        {
            // F1~F7: 정상 회수(Confirm→Yes)
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1)); // ACTIVE
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                ea.ClearPublished();
                await vm.OnClickRevoke(row);
                log.Check("F1", "클릭시 확인팝업 1건", N<OpenConfirmPopupMessageModel>(ea) == 1);
                log.Check("F2a", "클릭시 즉시삭제 안 함(DELETE 0)", s.DeleteCallCount == 0);
                var confirm = ea.Last<OpenConfirmPopupMessageModel>();
                var call = confirm?.MessageModel as CallRevokeGrantMessageModel;
                log.Check("F2b", "확인팝업 payload=CallRevokeGrant", call is not null);
                log.Check("F3a", "payload.Grant.Id 일치", call?.Grant?.Id == seeded.Id);
                log.Check("F3b", "확인문구에 그룹명 포함", (confirm?.Explain ?? "").Contains("야간조"));
                log.Check("F3c", "확인문구에 부여#id 포함", (confirm?.Explain ?? "").Contains($"#{seeded.Id}"));
                log.Check("F4", "확인 전 서버행 여전히 active", s.GrantRows.First(g => g.Id == seeded.Id).RevokedAt is null);

                ea.ClearPublished();
                await vm.HandleAsync(call!, ct);   // 사용자 Yes
                log.Check("F5a", "Yes 후 DELETE 1회", s.DeleteCallCount == 1);
                log.Check("F5b", "서버행 soft-revoke(RevokedAt 기록)", s.GrantRows.First(g => g.Id == seeded.Id).RevokedAt != null);
                log.Check("F5c", "서버행 is_active=false", s.GrantRows.First(g => g.Id == seeded.Id).IsActive == false);
                log.Check("F6", "Yes 후 ClosePopup 발행", N<ClosePopupMessageModel>(ea) == 1);
                log.Check("F7", "재조회시 상태 REVOKED", vm.Grants.First(g => g.Id == seeded.Id).Status == "REVOKED");
            }
            // F8: No(확인 취소) → 삭제 안 함
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                ea.ClearPublished();
                await vm.OnClickRevoke(vm.Grants.First(g => g.Id == seeded.Id));   // 확인팝업만, HandleAsync 미호출
                log.Check("F8a", "No: DELETE 미호출", s.DeleteCallCount == 0);
                log.Check("F8b", "No: 서버행 active 유지", s.GrantRows.First(g => g.Id == seeded.Id).RevokedAt is null);
            }
            // F9: 멱등 회수(이미 회수된 부여 재회수)
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1), revoked: true);
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                log.Check("F9a", "멱등회수: 성공 처리(실패팝업 없음)", !HasInfo(ea, "회수 실패"));
                log.Check("F9b", "멱등회수: ClosePopup 발행", N<ClosePopupMessageModel>(ea) == 1);
            }
            // F10: 없는 부여 회수 → 404
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                s.GrantRows.RemoveAll(g => g.Id == seeded.Id);   // 타 세션이 먼저 삭제
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                log.Check("F10a", "404: 회수 실패 안내", HasInfo(ea, "회수 실패"));
                log.Check("F10b", "404: ClosePopup 여전히 발행", N<ClosePopupMessageModel>(ea) == 1);
            }
            // F11: 비ADMIN 회수 → 403
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                s.ActorRole = "OPERATOR";
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                log.Check("F11a", "403: 회수 실패 안내(role)", HasInfo(ea, "회수 실패"));
                log.Check("F11b", "403: 서버행 미변경(active)", s.GrantRows.First(g => g.Id == seeded.Id).RevokedAt is null);
                log.Check("F11c", "403: ClosePopup 발행", N<ClosePopupMessageModel>(ea) == 1);
            }
            // F12: 회수 예외 → 로깅 + finally ClosePopup
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, lg) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                s.ThrowOnDelete = true;
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                log.Check("F12a", "예외: 에러 로그 '[GrantMgmt] 회수 실패'", lg.Errors.Any(e => e.Contains("회수 실패")));
                log.Check("F12b", "예외: finally ClosePopup 발행", N<ClosePopupMessageModel>(ea) == 1);
                log.Check("F12c", "예외: 무크래시", true);
            }
            // F13: HandleAsync(null grant) 가드
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = null! }, ct);
                log.Check("F13a", "null grant: DELETE 미호출", s.DeleteCallCount == 0);
                log.Check("F13b", "null grant: 발행 없음", ea.Published.Count == 0);
            }
            // F14: OnClickRevoke(null) 가드
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                ea.ClearPublished();
                await vm.OnClickRevoke(null!);
                log.Check("F14", "OnClickRevoke(null): 확인팝업 없음", N<OpenConfirmPopupMessageModel>(ea) == 0);
            }
        }

        // ══════════════ G. 파생상태(가짜시계) ══════════════
        log.Section("G. 파생 상태(ACTIVE/PENDING/EXPIRED/REVOKED + 시간경과 + 우선순위)");
        {
            async Task<GrantDto> LoadOne(FakeGopServer s)
            {
                var (vm, _, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                return vm.Grants.Single();
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), GrantFixtures.T0.AddHours(1));
                log.Check("G1", "구간내 → ACTIVE", (await LoadOne(s)).Status == "ACTIVE");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null);
                log.Check("G2", "상시(종료 null) → ACTIVE", (await LoadOne(s)).Status == "ACTIVE");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(1), GrantFixtures.T0.AddHours(2));
                log.Check("G3", "시작 미도래 → PENDING", (await LoadOne(s)).Status == "PENDING");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-2), GrantFixtures.T0.AddHours(-1));
                log.Check("G4", "종료 경과 → EXPIRED", (await LoadOne(s)).Status == "EXPIRED");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, revoked: true);
                log.Check("G5", "회수됨 → REVOKED", (await LoadOne(s)).Status == "REVOKED");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(1), GrantFixtures.T0.AddHours(2), revoked: true);
                log.Check("G6", "우선순위 REVOKED>PENDING", (await LoadOne(s)).Status == "REVOKED");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-2), GrantFixtures.T0.AddHours(-1), revoked: true);
                log.Check("G7", "우선순위 REVOKED>EXPIRED", (await LoadOne(s)).Status == "REVOKED");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), GrantFixtures.T0.AddHours(1));
                log.Check("G8", "ACTIVE 부여 is_active=true", (await LoadOne(s)).IsActive == true);
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, revoked: true);
                log.Check("G9", "회수 부여 is_active=false", (await LoadOne(s)).IsActive == false);
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-2), GrantFixtures.T0.AddHours(-1));
                var dto = await LoadOne(s);
                log.Check("G10", "만료(미sweep) EXPIRED이나 is_active=true(회수버튼 활성 근거)", dto.Status == "EXPIRED" && dto.IsActive == true);
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(1), GrantFixtures.T0.AddHours(3));
                var (vm, _, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("G11a", "초기 PENDING", vm.Grants.Single().Status == "PENDING");
                s.Now = GrantFixtures.T0.AddHours(2);            // 시간경과: 구간 진입
                await vm.OnClickReloadButton();
                log.Check("G11b", "시간경과 후 ACTIVE(라이브 재계산)", vm.Grants.Single().Status == "ACTIVE");
            }
            {
                var s = GrantFixtures.NewServer(); s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), GrantFixtures.T0.AddHours(1));
                var (vm, _, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("G12a", "초기 ACTIVE", vm.Grants.Single().Status == "ACTIVE");
                s.Now = GrantFixtures.T0.AddHours(2);            // 시간경과: 종료 초과
                await vm.OnClickReloadButton();
                log.Check("G12b", "시간경과 후 EXPIRED", vm.Grants.Single().Status == "EXPIRED");
            }
        }

        // ══════════════ H. 갱신 버튼(Reload) ══════════════
        log.Section("H. 갱신 버튼(신규 그룹/계정/부여 반영 + 단일 재조회)");
        {
            var s = GrantFixtures.NewServer(); var (vm, _, _) = GrantFixtures.NewVm(s);
            await vm.OnClickReloadButton();
            var before = s.ListAllCallCount;
            s.Groups.Add(new UserGroupDto { Id = 13, Name = "임시조", IsActive = true });
            s.Users.Add(new AuthUserDto { Id = 4, LoginId = "op2", Name = "운영자이", Role = "OPERATOR", IsActive = true });
            s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null);
            await vm.OnClickReloadButton();
            log.Check("H1", "신규 그룹 '임시조' 콤보 반영", vm.Groups.Any(g => g.Name == "임시조"));
            log.Check("H2", "신규 계정 'op2' 콤보 반영", vm.Accounts.Any(a => a.LoginId == "op2"));
            log.Check("H3", "신규 부여 목록 반영", vm.Grants.Count == 1);
            log.Check("H4", "재조회시 GetAllGrants 재호출(+1)", s.ListAllCallCount == before + 1, $"before={before},after={s.ListAllCallCount}");
        }

        // ══════════════ I. 로드 실패 경로 ══════════════
        log.Section("I. 로드 실패(계정/그룹/부여 500·예외 → graceful)");
        {
            {
                var s = GrantFixtures.NewServer(); s.FailUsersLoad = true;
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("I1a", "계정 로드 실패 안내", HasInfo(ea, "계정 목록 불러오기 실패"));
                log.Check("I1b", "계정 목록 비어있음", vm.Accounts.Count == 0);
            }
            {
                var s = GrantFixtures.NewServer(); s.FailGroupsLoad = true;
                var (vm, _, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("I2", "그룹 로드 실패시 그룹 비고 무크래시", vm.Groups.Count == 0);
            }
            {
                var s = GrantFixtures.NewServer(); s.ThrowOnListAll = true;
                var (vm, _, lg) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("I3a", "부여 로드 예외 흡수(무크래시)", true);
                log.Check("I3b", "에러 로그 '[GrantMgmt] 전체 부여 로드 실패'", lg.Errors.Any(e => e.Contains("전체 부여 로드 실패")));
                log.Check("I3c", "부여 목록 비어있음", vm.Grants.Count == 0);
            }
            {
                var s = GrantFixtures.NewServer(); s.FailListAll = true;
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("I4a", "부여 목록 500 실패 안내", HasInfo(ea, "부여 목록 불러오기 실패"));
                log.Check("I4b", "부여 목록 비어있음", vm.Grants.Count == 0);
            }
        }

        // ══════════════ J. 페이지네이션(무한 스크롤) — 절단 경고 팝업 제거 후 append 검증 ══════════════
        //   변경: 100건 초과 시 절단 경고 팝업(구 J1a/J2a) 제거 → 무한 스크롤이 대체.
        //   첫 페이지 100건 표시 + HasMorePages=true, 스크롤(LoadNextGrantsPageAsync)로 나머지 append.
        log.Section("J. 페이지네이션(무한 스크롤): 첫 100건 + append, 절단경고 없음");
        {
            static void Seed150(FakeGopServer s)
            {
                for (int i = 0; i < 150; i++)
                    s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddSeconds(i));
            }
            static void Seed100(FakeGopServer s)
            {
                for (int i = 0; i < 100; i++)
                    s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddSeconds(i));
            }
            {
                var s = GrantFixtures.NewServer(); Seed150(s); s.EmitPaginationMeta = false;   // 실서버 재현(total top-level)
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("J1a", "절단 경고 팝업 제거(무한 스크롤 대체)", !HasInfo(ea, "페이지네이션 필요"));
                log.Check("J1b", "첫 페이지 100건 표시", vm.Grants.Count == 100, $"count={vm.Grants.Count}");
                log.Check("J1c", "더 있음(HasMorePages=true)", vm.HasMorePages);
                await vm.LoadNextGrantsPageAsync();   // 스크롤 → 나머지 50건 append
                log.Check("J1d", "append 후 150건 전체 표시", vm.Grants.Count == 150, $"count={vm.Grants.Count}");
                log.Check("J1e", "마지막 페이지 후 더 없음", !vm.HasMorePages);
            }
            {
                var s = GrantFixtures.NewServer(); Seed150(s); s.EmitPaginationMeta = true;    // pagination 객체 제공 경로
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("J2a", "pagination 제공시에도 절단경고 없음", !HasInfo(ea, "페이지네이션 필요"));
                log.Check("J2b", "첫 페이지 100건", vm.Grants.Count == 100);
                log.Check("J2c", "더 있음(HasMorePages=true)", vm.HasMorePages);
                await vm.LoadNextGrantsPageAsync();
                log.Check("J2d", "append 후 150건", vm.Grants.Count == 150, $"count={vm.Grants.Count}");
            }
            {
                var s = GrantFixtures.NewServer(); Seed100(s); s.EmitPaginationMeta = true;
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("J3", "정확히 100건: 경고 없음·더 없음", !HasInfo(ea, "건 중") && vm.Grants.Count == 100 && !vm.HasMorePages);
            }
            {
                var s = GrantFixtures.NewServer(); Seed100(s); s.EmitPaginationMeta = false;
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                log.Check("J4", "정확히 100건(실서버): 경고 없음·더 없음", !HasInfo(ea, "건 중") && vm.Grants.Count == 100 && !vm.HasMorePages);
            }
        }

        // ══════════════ K. 봉투 시퀀스 정합 ══════════════
        log.Section("K. 이벤트 봉투 시퀀스(성공/확인/회수)");
        {
            // K1: 생성 성공 → [OpenInfo] 단독
            {
                var s = GrantFixtures.NewServer(); var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
                vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
                vm.ValidFrom = GrantFixtures.T0; vm.ValidUntil = GrantFixtures.T0.AddHours(1);
                ea.ClearPublished();
                await vm.ClickCreateGrant();
                log.Check("K1a", "생성성공: OpenInfo 1건", N<OpenInfoPopupMessageModel>(ea) == 1);
                log.Check("K1b", "생성성공: ClosePopup 없음", N<ClosePopupMessageModel>(ea) == 0);
                log.Check("K1c", "생성성공: ConfirmPopup 없음", N<OpenConfirmPopupMessageModel>(ea) == 0);
            }
            // K2: 회수 클릭 → [OpenConfirm] 단독
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                ea.ClearPublished();
                await vm.OnClickRevoke(vm.Grants.First(g => g.Id == seeded.Id));
                log.Check("K2a", "회수클릭: OpenConfirm 1건", N<OpenConfirmPopupMessageModel>(ea) == 1);
                log.Check("K2b", "회수클릭: Info/Close 없음", N<OpenInfoPopupMessageModel>(ea) == 0 && N<ClosePopupMessageModel>(ea) == 0);
            }
            // K3: 회수 Yes 성공 → [ClosePopup] 단독
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                log.Check("K3a", "회수성공: ClosePopup 1건", N<ClosePopupMessageModel>(ea) == 1);
                log.Check("K3b", "회수성공: Info 없음", N<OpenInfoPopupMessageModel>(ea) == 0);
            }
            // K4: 회수 Yes 실패(404) → [OpenInfo, ClosePopup] 순서
            {
                var s = GrantFixtures.NewServer();
                var seeded = s.Seed(2, 10, GrantFixtures.T0.AddHours(-1), null, GrantFixtures.T0.AddHours(-1));
                var (vm, ea, _) = GrantFixtures.NewVm(s);
                await vm.OnClickReloadButton();
                var row = vm.Grants.First(g => g.Id == seeded.Id);
                s.GrantRows.RemoveAll(g => g.Id == seeded.Id);
                ea.ClearPublished();
                await vm.HandleAsync(new CallRevokeGrantMessageModel { Grant = row }, ct);
                var infoIdx = ea.Published.FindIndex(m => m is OpenInfoPopupMessageModel);
                var closeIdx = ea.Published.FindIndex(m => m is ClosePopupMessageModel);
                log.Check("K4a", "회수실패: Info + Close 발행", infoIdx >= 0 && closeIdx >= 0);
                log.Check("K4b", "회수실패: Info가 Close보다 먼저", infoIdx >= 0 && closeIdx >= 0 && infoIdx < closeIdx, $"info={infoIdx},close={closeIdx}");
            }
        }
    }
}

/// <summary>
/// 권한 부여 기능 시뮬레이션 — 110+ 시나리오를 2회 실행하여 전건 통과 + 결정론(재현성)을 보증한다.
/// 단계 로그는 GRANT_SIM_LOG(env) 또는 실행 디렉터리에 기록된다.
/// </summary>
public class GrantManagementScenarioTests
{
    private readonly ITestOutputHelper _out;
    public GrantManagementScenarioTests(ITestOutputHelper output) => _out = output;

    private static string ResolveLogPath()
    {
        var env = Environment.GetEnvironmentVariable("GRANT_SIM_LOG");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        return Path.Combine(AppContext.BaseDirectory, "grant-sim-run.log");
    }

    [Fact]
    public async Task should_pass_all_grant_scenarios_twice_deterministically()
    {
        var run1 = new SimLog();
        await GrantScenarios.RunAllAsync(run1);
        var run2 = new SimLog();
        await GrantScenarios.RunAllAsync(run2);

        // ── 로그 아티팩트 작성(각 과정 단계 기록) ──
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║   권한 부여(GrantManagementPanel) 기능 시뮬레이션 — 2회 실행 로그    ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
        sb.AppendLine($"대상 VM   : Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.GrantManagementPanelViewModel");
        sb.AppendLine($"서버계약  : FakeGopServer (api-test-server/app/routers/grants.py 충실 모사)");
        sb.AppendLine($"RUN #1    : PASS={run1.Pass}  FAIL={run1.Fail}  TOTAL={run1.Total}");
        sb.AppendLine($"RUN #2    : PASS={run2.Pass}  FAIL={run2.Fail}  TOTAL={run2.Total}");
        bool deterministic = run1.Outcomes.SequenceEqual(run2.Outcomes);
        sb.AppendLine($"결정론    : RUN#1 ≡ RUN#2 → {(deterministic ? "일치(재현성 보증)" : "불일치!")}");
        sb.AppendLine($"판정      : {((run1.Fail == 0 && run2.Fail == 0 && deterministic) ? "✅ 전건 통과·재현성 확인" : "❌ 실패 존재")}");
        sb.AppendLine();
        sb.AppendLine("──────────────────────────── RUN #1 상세 ────────────────────────────");
        foreach (var line in run1.Lines) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("──────────────────────────── RUN #2 상세 ────────────────────────────");
        foreach (var line in run2.Lines) sb.AppendLine(line);
        if (run1.Failures.Count > 0 || run2.Failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("──────────── 실패 목록 ────────────");
            foreach (var f in run1.Failures) sb.AppendLine($"  RUN#1 {f}");
            foreach (var f in run2.Failures) sb.AppendLine($"  RUN#2 {f}");
        }

        var text = sb.ToString();
        try
        {
            var path = ResolveLogPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(true));
            _out.WriteLine($"[로그 기록] {path}");
        }
        catch (Exception ex) { _out.WriteLine($"[로그 기록 실패] {ex.Message}"); }
        _out.WriteLine(text);

        // ── 보증 판정 ──
        Assert.True(run1.Total >= 100, $"시나리오 100+ 요구: 실제 {run1.Total}");
        Assert.True(run1.Fail == 0, $"RUN#1 실패: {string.Join(" / ", run1.Failures)}");
        Assert.True(run2.Fail == 0, $"RUN#2 실패: {string.Join(" / ", run2.Failures)}");
        Assert.True(deterministic, "2회 실행 결과가 불일치(비결정론)");
    }
}
