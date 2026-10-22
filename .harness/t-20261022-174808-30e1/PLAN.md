# PLAN — t-20261022-174808-30e1 건물 설정 판 높이 줄이기 · 스크롤

## 목표
(사용자 원문, 2026-10-22) "너무 건물 설정 오버레이 윈도우 길어 좀 높이 줄이고 스크롤 넣어라"
대상: 「건물 높이」(BuildingHeightPanelStyle.xaml) · 「건물 고치기 · 만들기」(UserBuildingPanelStyle.xaml) · 「꾸미기」(DecorationPanelStyle.xaml).
머리줄(제목 · ? · ✕)과 행동 줄([✓ 적용] · [취소] · 되돌리기 등)은 늘 보이게 고정, 그 사이 내용만 세로 스크롤. 판 위 휠은 판 스크롤만, Tab 포커스 칸은 보이게.
디자인 · 토큰 · AutomationId · 폭(320) 그대로. 창 없는 측정 시험으로 고치기 전 실패 → 고친 뒤 통과 증명.

## 진행
- [x] 고치기 전 실패 확인(새 시험 `tests/GMaps.MapEdit.Tests/BuildingPanelScrollScenarioTests.cs`) — 11/11 실패.
      실측 판 높이: 「건물 높이」 848 · 「건물 고치기」 1227 · 「꾸미기」 1191 px(상한 420 · 452 · 637 에 대해)
- [x] HeightPanelControlBase — 높이 상한(MeasureOverride + FitHeight, Canvas SizeChanged 로 다시 재기) · OnMouseWheel Handled · GotKeyboardFocus → BringIntoView
- [x] OverlayHeaderDrag — 끌어 옮기면 `_panel.InvalidateMeasure()`
- [x] 세 판 XAML — 행 38 / * / Auto, 가운데 ScrollViewer(`Map.<판>.Scroll`, 기본 스크롤바 스타일), 아래 행동 줄(Grid.Row=2)
- [x] 고친 뒤 11/11 통과. 처리기별 증명: OnMouseWheel 의 Handled 만 빼면 휠 시험 3건 실패(행동 줄 위 휠이 지도 Canvas 까지 올라감),
      GotKeyboardFocus 처리기만 빼면 3건 실패(예: 「건물 고치기」 To2D 가 y 932, 보이는 칸 0~209 밖) → 되살리면 통과.
- [x] 스크롤바가 생겨도 글자 잘림 없음 시험 추가(should_not_clip_texts_when_vertical_scrollbar_narrows_content, 세 판 × 지도 600 — 세로 스크롤바 보임을 먼저 단정)
- [x] 시험 통과 · MapEdit 전체 · GMaps.Ui.Tests · MapAux · 솔루션 시험
- [x] CHANGELOG

## 검증(이 세션에서 돌린 명령 · 결과)
- `dotnet test tests\GMaps.MapEdit.Tests\GMaps.MapEdit.Tests.csproj --filter "FullyQualifiedName~BuildingPanelScrollScenarioTests"`
  - 고치기 전: 실패 11 / 통과 0(판 높이 848 · 1227 · 1191 > 상한)
  - 고친 뒤: 통과 11 → 글자 잘림 시험 3 더해 통과 14 / 실패 0
  - 처리기 하나씩 끈 탐침: 휠 Handled 끔 → 3 실패, 포커스 BringIntoView 끔 → 3 실패, 되살림 → 통과(탐침 코드는 남기지 않음)
- `dotnet test tests\GMaps.MapEdit.Tests\GMaps.MapEdit.Tests.csproj` → 통과 951 / 실패 0 (글자 잘림 시험 3건 추가 전 빌드)
- `dotnet test tests\GMaps.Ui.Tests\GMaps.Ui.Tests.csproj` → 통과 1875 / 실패 0
- `dotnet test tests\GMaps.MapAux.Tests\GMaps.MapAux.Tests.csproj` → 통과 130 / 실패 0
- `dotnet test Ironwall.Dotnet.Libraries.sln --filter "FullyQualifiedName!~Sounds&FullyQualifiedName!~Libraries.Db"` → 시험 묶음 11개 모두 0 실패
  (Sounds = 실제 소리, Libraries.Db = 다른 PC DB 에 씀 — 사용자 방침으로 뺐다)

## 남은 일
- 헤디드(실앱) 확인은 하지 않았다 — 창 띄우는 시험 금지 지시. 실앱에서 1920×1032 · 1366×768 로 판을 열어 행동 줄이 보이는지 보면 된다.
- 앞선 작업 브랜치 `harness/t-20261022-164413-a54e` 를 나중에 받으면 세 판 XAML 이 충돌한다 — DECISIONS 1 참고.

## 결정
DECISIONS.md 참고(1 앞선 작업 재사용 방식, 2 상한 = min(지도×0.7, 지도−Top−8) · 최소 200, 3 행동 줄 내용, 4 휠, 5 포커스).

## 새로 온 사람을 위한 맥락
- 판은 `Views/Maps/MapView.xaml` 의 `PropertyPanelCanvas`(지도 · 3D 호스트와 형제, ZIndex 200) 안 ContentPresenter(Left 620 · Top 90)에 놓인다.
  Canvas 는 자식을 끝없는 높이로 재므로 판이 화면 밖까지 길어졌다 → 판 바탕 `GMapControls/HeightPanelControlBase.cs` 가 MeasureOverride 에서 높이를 자른다.
- 세 판 컨트롤(BuildingHeightPanelControl · UserBuildingPanelControl · DecorationPanelControl)이 모두 HeightPanelControlBase 를 쓴다.
- 앞선 작업 t-20261022-164413-a54e(브랜치 `harness/t-20261022-164413-a54e`)에 비슷한 스크롤이 있으나 v2.6 미병합 — DECISIONS 1.
- tests/GMaps.* 는 .sln 밖 → 직접 `dotnet test tests\GMaps.MapEdit.Tests\GMaps.MapEdit.Tests.csproj`.
