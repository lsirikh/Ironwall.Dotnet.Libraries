# PLAN — GIS 로그아웃 때 `LogoutAsync("app")` 호출

## 목표
`docs/coordinations/2026-10-14-gis-로그아웃-때-logoutasync-app-호출-에이전트에-반영되지-않음.md` — 사람이 GIS 안에서 로그아웃하면
SSO 에이전트에 "이 앱만 로그아웃"(SDK `LogoutAsync("app")`)을 알려 「내 프로그램」 줄이 「로그아웃」 으로 바뀌게 한다.

## 진행
- [x] `ISsoAgentGateway.LogoutAppAsync` 추가, `SsoAgentGateway` 에서 SDK `_client.LogoutAsync("app", ct)` 호출(`"all"` 아님)
- [x] `SsoSessionCoordinator.SignOutAppAsync()` — SSO 세션(훅 꽂힘)일 때만, 훅 먼저 떼고 · 복귀 기다림 끔 · 5초 제한 · 실패는 기록만
- [x] `LogoutPanelViewModel.ClickOk` — GOP 로그아웃(`_gateway.LogoutAsync`) **전에** `_sso.SignOutAppAsync()` (선택 주입, DB 모드는 null)
- [x] 시험 `Ironwall.Dotnet.Libraries.Sso/Tests/SsoAppLogoutTests.cs` 4건 + `FakeAgent.LogoutAppAsync`
- [x] `dotnet test Ironwall.Dotnet.Libraries.Sso` 101 통과
- [x] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` — 실패 0

## 결정
- 비밀번호로 들어온 세션은 에이전트와 무관하므로 알리지 않는다(`IsEnabled` 로 판정). GOP 로그아웃 뒤엔 판정이 불가하므로 그 전에 부른다.
- 훅을 먼저 떼어, 에이전트가 답으로 `signed-out` 사건을 보내도 `SignOutFromAgentAsync` 가 무시하게 했다(사건 경로의 안내 문구 · 복귀 기다림이 사람의 로그아웃을 덮지 않게).
- 에이전트가 멈춰도 로그아웃이 막히지 않게 5초 제한(`LogoutAppTimeout`).
- 요청 2(「SSO 로 로그인」 단추 = `SignInInteractiveAsync`)는 이미 구현돼 있어 변경 없음.
- 요청 3(반영 판 · 실기 결과 회신)은 실기(에이전트 실행) 필요 — 이 작업 범위 밖, 남은 일.

## 남은 일
- 실기 확인: GIS 로그아웃 → 에이전트 줄이 몇 초 안에 「로그아웃」 으로 바뀌는지, SSO 팀에 회신.
