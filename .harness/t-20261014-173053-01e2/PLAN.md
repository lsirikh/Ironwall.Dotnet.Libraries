# PLAN — 로그아웃 창: SSO 세션이면 [취소] · [이 앱만 로그아웃] · [전체 로그아웃]

## 목표
로그아웃 단추를 누르면 SSO 로 로그인한 세션에서는 3개 단추(취소 · 이 앱만 로그아웃 · 전체 로그아웃),
SSO 가 아니면 지금처럼 [취소] · [로그아웃] 만 보인다.
참고: `docs/coordinations/2026-10-14-gis-로그아웃-때-logoutasync-app-호출-에이전트에-반영되지-않음.md` (이전 작업 t-20261014-171531-3c30 이 "app" 경로를 이미 구현)

## 진행
- [x] `ISsoAgentGateway.LogoutAllAsync` + `SsoAgentGateway` 에서 SDK `LogoutAsync("all")`
- [x] `SsoSessionCoordinator.SignOutAllAsync()` — `SignOutAppAsync` 와 같은 조건 · 훅 떼기 · 5초 제한 · 실패 삼킴(공용 `SignOutAsync(bool all)`)
- [x] `LogoutPanelViewModel` — `IsSsoSession`(=`_sso?.IsEnabled`) · `OkText` · `ClickLogoutAll`, 활성화 때마다 갱신
- [x] `LogoutPanelView.xaml` — `ClickOk` 글자를 `OkText` 바인딩, `ClickLogoutAll` 단추(SSO 일 때만 보임)
- [x] 시험 2건 추가(`SsoAppLogoutTests`), `FakeAgent.LogoutAllAsync`
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` — 실패 0 (Sso 103 통과)
- [x] `Accounts.Ui.Tests` 의 뷰 계약 · 모듈 해석 시험 38 통과

## 결정
- 기존 `ClickOk`(x:Name 계약 · UI 시험 앵커)를 유지하고, SSO 세션이면 글자만 「이 앱만 로그아웃」 으로 바꾼다 — 새 단추는 [전체 로그아웃] 하나.
- SSO 여부 판정은 기존 `IsEnabled`(훅이 꽂혀 있음) — 비밀번호 로그인 세션은 단추 2개.
- 실패해도 GIS 로그아웃은 진행(기존 정책 그대로).

## 남은 일
- 실기 확인(에이전트 실행 필요): [전체 로그아웃] 뒤 에이전트에서 SSO 세션 · 다른 앱이 로그아웃되는지.
