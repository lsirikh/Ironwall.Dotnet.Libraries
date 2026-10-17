# PLAN — 3D 영역마다 진입 줌 + 세팅 「지도」 절 "3D 최소 줌"

## 목표
1. 3D 만들기 패널(GMaps.Map3D.BakeOptions)에서 "3D 로 바뀌는 줌"(15~19, 0.5 단위, 기본 = 세팅 EnterZoom)을 고른다. 오버레이 이미지가 없어도 패널을 띄운다.
   굽기 본문에 "enter_zoom" · 장면 목록의 enter_zoom 을 읽어 영역별 게이트(이탈 = 영역 진입 − (세팅 진입 − 세팅 이탈), 미리 준비 = min(세팅 미리 준비, 영역 진입 − 1)).
   배지 툴팁 · 목록 · 칩에 영역 줌. 배지 우클릭 「3D 줌 바꾸기…」 → PATCH /api/jobs/{id}, 404/405 면 %LocalAppData%\Ironwall\Gis\map3d-areas.json 에 저장(우선).
2. 메인 앱 세팅 「지도」 절에 "3D 최소 줌"(Map3D.EnterZoom), 「3D 지도」 절에서는 그 칸을 뺀다. 줌 순서 검증 유지. "?" 도움말.

## 진행
- [x] 라이브러리 순수 함수: Map3DMath.AreaGate · ResolveAreaEnterZoom(+ Map3DZoomGate), Map3DGuide.EnterZoomChoices · EnterZoomChoiceFor · AreaToolTip(줌) · AreaMenuHeader,
      Map3DBake.BuildBody(enter_zoom), Map3DClient(StartBakeAsync enterZoom · 장면 EnterZoom 해석 · UpdateSceneEnterZoomAsync PATCH), Services/Map3D/Map3DAreaZoomStore(로컬 파일)
- [x] 라이브러리 VM: 만들기 패널 늘 띄움 + 줌 고르기 + 「3D 줌 바꾸기」 모드(같은 패널), 게이트 · 미리 불러오기 · 2D 복귀 · 로그인 안내 줌을 영역별로, 칩 · 목록 · 툴팁에 영역 줌, 지우면 로컬 줌도 지움
- [x] MapView.xaml: 패널 제목/확인 글 바인딩 · 방식 묶음 Visibility · 줌 콤보(GMaps.Map3D.BakeOptions.EnterZoom) · "?"(Map.Map3D.EnterZoom) · 목록 머리글 MenuHeader
- [x] 시험: tests/GMaps.Ui.Tests/Map3DAreaZoomTests.cs — GMaps.Ui.Tests 1147 통과
- [x] 메인 앱(복사본에서 빌드 · 시험): SettingsCatalog.MapSection 칸 · Map3DSection 칸 빼기 · 검증 문구 · HostHelp · Map3DSettingsSectionTests → `.harness/t-20261018-002351-16a4/host.patch`
      메인 앱 시험(Window/Startup/Headed 제외) 817 통과 · 0 실패. 원본 사본에 `git apply` → 작업본과 바이트 동일 확인.
- [x] 추가 요구(실기: 오버레이로 만들면 건물이 안 섬): 만들기 방식 넷 — tiles · photo(새 기본, composite + mask=false) · mask(composite + mask=true) · extract.
      Map3DBake.NormalizeMode(옛 composite → mask) · ServerOverlayMode · MaskFor · BuildBody, Map3DModel 기본 photo, 패널 라디오 넷 + "?" 셋, 메인 앱 「기본 만들기 방식」 선택지 넷(host.patch 갱신).
      GMaps.Ui.Tests 1158 통과 · 메인 앱 시험 817 통과 · host.patch 원본 적용 검증.
- [x] 정리: 복사본 · 정션(claude-skill-harness-h3d) 지움(두 번 — 추가 요구 때 다시 만들었다가 지움)
- [x] 검증: dotnet test Ironwall.Dotnet.Libraries.sln — 시험 어셈블리 10개 모두 통과(실패 0)

## 발견
- 하네스 첫 검사에서 CameraPopup.Tests `EventWindowTileStreamTests.should_close_with_timer_reason_when_timer_elapses_in_host`(900~3000 ms 예산, 실측 4283 ms)가 실패 — 이번 변경과 무관한 시간 예산 시험(기계가 바쁠 때). 바로 앞 실행에서는 통과했다.
- 복사본에서 메인 앱 시험을 돌리려면 Dotnet.Monitoring.Solution.sln + Installer\ 도 있어야 한다(InstallerSource.FindRepoRoot) — 없으면 설치 · 옛 서버 시험 22개가 실패한다.
- worktree 안 하위 폴더에서 `git apply` 하면 저장소 기준 경로라 조용히 아무것도 안 한다 → GIT_CEILING_DIRECTORIES 로 저장소 밖처럼 돌려 확인했다.
- map-3d 가 enter_zoom 을 아직 기록하지 않으면(별도 작업) 새로 만든 영역도 목록을 다시 받은 뒤엔 세팅값을 쓴다 — 목표대로 GIS 는 보내기만 한다.

## 남은 일(코디네이터)
- 메인 앱 저장소 루트에서 `git apply <이 worktree>/.harness/t-20261018-002351-16a4/host.patch` → 빌드 · 시험.
- map-3d: POST /api/jobs 의 enter_zoom 을 scene.json 에 기록 · GET /api/jobs scenes 에 돌려주기 · PATCH /api/jobs/{id} {"enter_zoom"} (이 저장소 밖).
