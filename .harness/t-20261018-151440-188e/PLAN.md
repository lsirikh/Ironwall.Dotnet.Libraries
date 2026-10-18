# PLAN — 3D 만들기 기본 방식 = extract(오버레이 영역은 이미지대로, 나머지는 타일대로)

## 목표
- 기본 만들기 방식 extract, 옛 값 composite → extract, 패널 처음 값 extract(마지막 값이 있으면 그것)
- 패널 순서 · 이름: Extract · Photo · Mask · Tiles(AutomationId 그대로), "?" 도움말 새 뜻
- 메인 앱 세팅 「3D 지도」 선택지 · HostHelp · 시험을 같은 이름 · 순서로 → host.patch
- 순수 함수 시험

## 진행
- [x] Map3DModel.OverlayMode 기본 "extract", Normalize composite → extract
- [x] Map3DBake.NormalizeMode composite → extract, DefaultMode 빈 마지막 값 → extract
- [x] MapView.xaml 라디오 순서 · 글, MapsHelp.cs 도움말, VM 머리 주석
- [x] tests/GMaps.Ui.Tests 기대값 갱신 + 새 시험 4건(빈 마지막 값 3가지 → extract, 새 설정 → extract) — 1213 통과
- [x] 메인 앱 복사본(.hostcopy) 3파일 수정
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` — 2888 통과 · 실패 0
- [x] 메인 앱 복사본 시험(Window/Startup/Headed · Integration 제외) — 815 통과 · 8 건너뜀 · 실패 0, Map3DSettingsSectionTests 37 통과
- [x] host.patch 생성(메인 앱 v0.5 0c3faa1 에 `git apply --check` 통과) → .hostcopy(junction 은 rmdir 로 끊음) 삭제

## 남은 일
- 메인 앱에 host.patch 적용(`git apply .harness/t-20261018-151440-188e/host.patch`) — 사람이 할 일

## 바뀐 기존 기대값(의도된 변경)
- Map3DBakeTests: composite → photo 였던 곳 전부 → extract(NormalizeMode · Model.Normalize · DefaultMode · ServerOverlayMode/MaskFor = extract/true), 새 기본 photo → extract, BuildBody(composite) 의 overlay_mode · mask → extract · true
- Map3DAreaZoomTests: BuildBody(ModeComposite) overlay_mode "composite" → "extract"
- (메인 앱) Map3DSettingsSectionTests: 선택지 값 · 글 · 순서, 처음 선택 = extract 글, 저장 시험의 Options[3] = tiles

## 발견
- 메인 앱 경로에 대한 PowerShell 명령은 막혀 Bash(cd + tar)로 추적 파일만 복사했다.
- 메인 앱 csproj 는 `..\..\Ironwall.Dotnet.Libraries\…` 상대 참조 → `.hostcopy\Ironwall.Dotnet.Libraries` junction 으로 이 워크트리에 잇는다.
