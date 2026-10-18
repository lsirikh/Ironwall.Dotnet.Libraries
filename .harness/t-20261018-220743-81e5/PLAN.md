# PLAN — 묶음 회전 컨트롤러 판

## 목표
선택 묶음(2개 이상) 회전이 열리면(손잡이 끌기 · `]`/`[` · 편집 도구줄 「회전」) 지도 위 판이 떠서
선택 요약 · 회전 중심(위경도 · MGRS/UTM) · 각도를 보이고 바꾸고(양방향), [확정](저장) · [취소](원위치)한다.
판의 값은 전부 `GroupRotationSession` 하나에서 파생.

## 구조
- `GMaps.Ui/Helpers/GroupRotationPanelRules.cs` — 순수 규칙(각도 · 위경도 입력 검증, ±한 칸, 입력칸 글, 종류별 요약, 확정 한 줄)
- `Services/GroupRotationSession.cs` — `Pivot` 바꾸기(`SetPivot`) + `DefaultPivot`(경계 상자 중심), 0° 미리보기 = 원본 그대로
- `Services/GroupSelectionService.cs` — `RotationChanged` 통지, `SetRotationPivot/ResetRotationPivot/SetPivotPicking`, 손잡이 놓음 = 멈춤(확정 아님), 열린 세션에서 이어 끌기, 중심 끌기 · 고르기 배선
- `Adorners/GroupSelectionAdorner.cs` — 세션 중 중심 손잡이 Thumb(`Map.GroupRotation.CenterHandle`, 데드존 8 · 단일 FinishPivotDrag · 캡처 잃음 = 되돌림), 「지도에서 고르기」 = 지도 전체 hit + PreviewMouseDown 소비
- `GMapControls/GroupRotationPanelControl.cs` + `Themes/GroupRotationPanelStyle.xaml`(Generic.xaml 병합) — OverlayWindow 패턴 판
- `ViewModels/Maps/MapViewModel.GroupRotation.cs` — 판 속성 · 명령(`GroupRotationPanel` · `IsGroupRotationPanelOpen` · 입력칸 · 막대 · 확정/취소/±/고르기/가운데로/「회전」)
- `Views/Maps/MapView.xaml` — PropertyPanelCanvas 에 판(ContentPresenter), 편집 도구줄 「선택」에 「회전」 단추
- 도움말 `Map.GroupRotation.Panel`(MapsHelp) + `Map.EditStrip.Selection` 한 줄

## 디자인 — 참조한 기존 창 · 스타일 키
- 참조: 사이트 고정 창(`MapAnchorPanelControl` + `Themes/MapAnchorPanelStyle.xaml` — OverlayWindow 패턴 · 머리 띠 끌기 Y≤38 · OverlayHeaderClamp),
  선 그리기 HUD(`Themes/LineDrawingHudStyle.xaml` — 진행 중 편집 HUD, Primary/Secondary 단추 두 칸),
  심볼 상세(`Themes/SymbolDetailStyle.xaml` — 머리 띠 "?" 받침 + 닫기), 3D 만들기 판(`MapView.xaml GMaps.Map3D.BakeOptions` — 요약 한 줄 · 확인/취소),
  좌표 칩(`Themes/MapCoordinateStyle.xaml` — Consolas 11 · 라벨 PrimaryBrush Bold · F8), 속성창 입력칸(`BasePropertyStyle.xaml CompactTextBox`)
- 그대로 재사용: `PanelCloseButtonStyle`(✕), `PanelHeaderHelpDisc`("?" 받침), `PanelPrimaryButtonStyle`(✓ 확정), `PanelSecondaryButtonStyle`(취소 · 고르기 · 가운데로, ± 단추는 BasedOn),
  `fb:FrameBorder` 카드(Surface · Border · 반지름 10 · 그림자 10/0.15/3 = 사이트 고정 창 값), 머리 띠 38 = PrimaryBrush + OnPrimaryFixedBrush 글/아이콘, `fb:HelpTip`, `BooleanToVisibilityConverter`(DesignTokens)
- 새로 만든 것(이유): `GroupRotationSectionLabel`(= MapAnchorSectionLabel 같은 값 — 그 키는 다른 사전이라 StaticResource 가 닿지 않음),
  `GroupRotationInputBox`(= CompactTextBox 값 + Consolas — BasePropertyStyle 은 이 사전에 병합돼 있지 않음),
  `GroupRotationStepButton`(PanelSecondaryButtonStyle BasedOn, 폭만), `GroupRotationCoordLabel/Value`(좌표 칩 글꼴 값 — 칩은 템플릿 안 리터럴이라 키가 없음)
- 색 · 글꼴은 전부 DynamicResource 토큰(하드코딩 색 0). 라이트/다크: 시험이 머리 띠 PrimaryBrush 를 Tokens.Light(#0C6B89) → Tokens.Dark(#22B8D9) 로 바꿔 따라 바뀜을 확인. 화면 그림은 실기 미확인

## 진행
- [x] 순수 규칙 + 시험(tests/GMaps.Ui.Tests/GroupRotationPanelRulesTests.cs)
- [x] 세션 중심 바꾸기 · 서비스 통지 · 어도너 중심 손잡이 · 고르기
- [x] 판 컨트롤 · 스타일 · VM · MapView 배선 · 「회전」 단추 · 도움말
- [x] 시나리오 시험(tests/GMaps.MapEdit.Tests/GroupRotationPanelScenarioTests.cs 10건, 창 없음) + 기존 손잡이 시험 1건을 새 규칙(놓음 ≠ 저장)에 맞춤
- [x] 판 포커스 중 키 가드(DECISIONS #7)
- [x] 머리 띠 끌기 중복 제거 — 공용 `Helpers/OverlayHeaderDrag.cs` 로 빼고 사이트 고정 창도 같은 것을 쓰게 함(DECISIONS #9)
- [x] 검증: dotnet test Ironwall.Dotnet.Libraries.sln 전부 통과 · GMaps.Ui.Tests 1373 · GMaps.MapEdit.Tests 75 · MapsHelpTests(키 · 문구) 30 통과

## 발견
- 창이 없으면 `VisualTreeHelper.HitTest` 가 돌지 않는다 → 어도너 `HitTestCore` 를 직접 불러 확인
- 생성자 없는 VM(Uninit)은 Caliburn `IsNotifying` 이 꺼져 있다 → 통지 시험은 켜고 본다
- 커스텀 Control 은 UIA peer 가 없어 `OnCreateAutomationPeer` 를 둬야 `Map.GroupRotation.Panel` 이 트리에 나온다

## 남은 것 / 실기 미확인
- 판 안 포커스 키 가드(DECISIONS #7)는 창 없는 시험으로 키보드 포커스를 만들 수 없어 시험 없음
- 실제 마우스로 손잡이 · 중심 십자 끌기, 판 머리 띠 끌기, 막대 조작, 라이트/다크 실제 그림, 지도 회전(뷰 회전) 상태의 중심 손잡이 자리 — 코디네이터 실기
