# PLAN — UI 결함 식별 · 수정 (t-20261015-145412-f975)

## 목표
1. 조치보고 창 라디오: 선택/마우스 올림/포커스 표시를 형태로 구분
2. 권한 설정 표 '카메라' 줄 칸 빠짐 원인 확정 → 고침
3. 부대 관계도: 전체 보기 배율 · 레일 선택 표시 · 빈 띠 · 설명 알약 · L1 노드 그림
4. 같은 유형 결함 다른 창에서 찾아 고침(목록 아래)

## 진행
- [x] 1. 라디오 — `Theme/Themes/Styles.Console.xaml` Console.RadioButton: 마우스 올림 = 옅은 바탕(SurfaceHover)만(청록 테두리 제거) ·
      선택 = 채운 원 + 점(마우스 올림보다 뒤에 둬 선택이 이김) · 키보드 포커스 = 바깥 점선 고리(Ellipse, FocusInner 제거). 라이트/다크 같은 토큰.
- [~] 2. 권한 표 — 헤드리스로 재현 안 됨(아래 '발견'). 회귀 시험 `tests/Accounts.Ui.ViewTests/PermissionMatrixRowCellsViewTests.cs`(픽셀 검사) 추가.
- [x] 3. 부대 관계도
  - 첫 화면: 내 부대 50% 가운데 대신, 편제 전체가 카드(L2 ≥ 0.80)로 한 화면에 들면 전체 보기(`IUnitMapSurface.CenterOnUnlessSmall` 기본 구현 = CenterOn, 캔버스가 구현).
  - 전체 보기 배율 상한 1.0(`UnitMapCanvas.FIT_MAX_SCALE`) — 노드 2개에서 1.6 까지 치솟던 과대 확대 막음.
  - 레일: `Utils/Themes/Generic.xaml` ConsoleRailItem — 포커스 고리를 IsKeyboardFocused 트리거(마우스 클릭에도 뜸, 굵은 2px 실선이 선택 막대를 덮음) →
    키보드로 왔을 때만 뜨는 FocusVisualStyle 점선으로. 모든 콘솔 레일 공통.
  - 빈 띠: `Units.Map.Status` 띠를 문구가 비면 접음(DataTrigger).
  - 알약: 평상시 문구(`UnitMapText.LayoutStatusQuiet` = 공유 · 아직 아무도 안 옮김 · 편집 가능)는 캔버스가 접음, 뜻은 관계도 "?"(Devices.Units.Map)에 한 줄 추가.
    VM 문구 자체는 그대로(기존 시험이 문구를 단언).
  - L1 노드 그림: 바꾸지 않음(결정 참조).
- [x] 4. 같은 유형 결함(아래 목록) 고침
- [ ] dotnet test sln + sln 밖 csproj 통과 확인

## 결정
- L1 노드(군대 부호 틀 + 제대 표지)는 이미 토큰(SurfaceAlt · TintInfo · TextPrimary)으로 그리고, 96/144dpi 픽셀 대조 시험(UnitMapNodeRenderCostTests)이
  잠그고 있다. 사용자가 본 '흰 사각 + 가는 선'은 노드 2개를 50%(L1)로 연 탓이 컸으므로, 첫 화면을 카드(L2)로 여는 쪽으로 해결하고 부호 그림은 두었다.
- 부대 트리 행(Unit.TreeRow)의 x:Name 목록(Bd · Bar · Focus)은 시험이 잠근다 → 이름은 두고 Focus 를 굵은 실선 Border → 점선 Rectangle 로.
- DataGrid 행(Console.DataGrid.Row)의 두 겹 포커스 고리(K-10)는 의도된 설계 + 셀이 포커스를 받는 구조라 이번엔 두었다.

## 발견 — 같은 유형 결함 목록(고친 것)
1. `Events.Ui/Views/Dialogs/DetectionHistoryDialogView.xaml` PeriodSegButton — 마우스 올림 트리거가 선택보다 뒤라 고른 칸 위에 마우스를 올리면
   바탕이 옅어지고 흰 글자가 사라졌다 → 트리거 순서 바꿈.
2. `Events.Ui/Consoles/Settings/CameraPopup/CameraPopupSettingsView.xaml` 격자 칩 — 포커스 고리(실선 2px 청록)가 선택 고리(실선 2px 같은 청록)와 같은 모양 →
   포커스를 점선 Rectangle 로.
3. 커널 레일(ConsoleRailItem) 포커스 사각형 — 위 3 참조(모든 콘솔).
4. 부대 트리 행 포커스 — 굵은 실선 → 점선.

## 발견 — 권한 표 '카메라' 줄(미확정)
- 카탈로그(PermissionCatalog) · 행 생성(BuildMatrixRows) · 행 VM · 템플릿 모두 카메라만 다르게 다루는 곳이 없다(cameras = 4동작 · 제어 서버집행 아님).
- 화면 밖 실제 뷰(AccountConsolePanelView)에 6개 그룹을 띄워 그룹 바꾸기 · 구성원 보기 왕복 · 끝까지 굴렸다 오기 · 칩 줄 펼침 × 라이트/다크로
  시각 트리(IsVisible · 크기 · 템플릿)와 픽셀(테두리가 실제로 그려졌는가)을 봤다 — 모든 줄 정상. (처음 PNG 에서 빈 것처럼 보인 건 축소 표시 탓, 2배 확대로 확인.)
- 그래서 원인을 코드에서 확정하지 못했다. 실창(대화형 세션)에서 재현 조건(어느 서버 · 어떤 조작 뒤)을 받아야 한다. 회귀 시험은 남겨 둔다.
- (2회차) 캡처의 그룹 이름 `LRT-UI-PGNEW-…R` 은 메인 솔루션 실창 시험 `ConsoleGapAccountsShellTests.should_create_edit_and_delete_permission_group_when_matrix_actions_are_used`
  (SC-ACC-035~042)이 만든 그룹이다 — 장비·조회만 켜진 것도 SC-ACC-039(첫 칸 UIA Toggle → [저장]) 직후 상태와 맞는다.
  같은 순서(UIA Toggle → ApplyAsync(다시 읽기 · 줄 재생성) → Toggle · Revert → Toggle · 칩 전환 시도 · Revert)를 화면 밖 실제 뷰로 재연하는
  시험 `should_paint_every_check_cell_after_the_live_save_revert_sequence` 를 더했다 — 통과(재현 안 됨).
- 같은 칸 안의 글자(TextBlock '카메라')는 그려지고 같은 DataTemplate 안 단추([전체]) · 체크칸(템플릿 컨트롤)만 빠진 모양이라,
  데이터 · 템플릿 문제가 아니라 그리기 단계(화면 합성 · 시험 도중 순간 캡처) 쪽이 유력하다. RenderTargetBitmap 은 시각 트리를 직접 그려 이 단계를 못 본다.
  추측으로 코드를 바꾸지 않았다(규칙: 증거 없이 고치지 않음). 실창 시험 스크린샷 폴더(%TEMP%) 확인은 이 세션 권한으로 막혀 못 했다.

## 남은 일
- 실창 재현 조건 확보 후 권한 표 원인 재조사.
