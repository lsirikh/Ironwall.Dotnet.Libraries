# RPT 남은 줄 다시 보기 — 결과(2026-10-11)

앞 작업(t-20261011-073820-4f76)이 헤디드로 남긴 RPT-009 · 010 · 017 · 019 · 020 · 024 · 028 과 범위 밖으로 둔 RPT-025 를 하나씩 다시 보고, 창 없이 단언할 수 있는 부분을 시험으로 만들었다.
창은 띄우지 않았다(Window.Show · ShowDialog · 화면 밖 창 없음). WPF 요소가 필요한 시험은 STA 스레드에서 요소만 만들고(기존 ReportStatusChipStyleTests 와 같은 방식), 입력 원천이 필요한 "?" 시험은 HWND 없는 가짜 `PresentationSource`(시험 안 `HeadlessSource`)를 썼다. 운영 서버 · 실제 NATS 없음 — 가짜 API 만.

약호: RLS = `Ironwall.Dotnet.Libraries.Reports.Ui/Tests/ReportLeftoverScenarioTests`, PTS = `Ironwall.Dotnet.Libraries.Events.Ui/Tests/ReportPhraseTrayScenarioTests`.

## 1. 항목별 결과

| id | 새 상태 | 시험 메서드 또는 남긴 이유 한 줄 |
|---|---|---|
| RPT-009 | 부분 통과(새) | RLS.should_ask_for_the_large_window_with_the_html_and_say_the_preview_is_shown_there_until_it_closes — 열기 요청이 HTML · 제목과 함께 뷰로 가고, 상세 칸은 「큰 창에서 보고 있습니다」 · 「그 창을 닫으면 미리보기가 이 자리로 돌아옵니다」, [크게 보기] 꺼짐, 닫으면 되돌아옴. **남김**: 별도 최상위 창(`Reports.LargePreview.Window`) 자체는 `ReportConsoleView.xaml.cs` 가 `new ReportLargePreviewWindow(...)` 로 만든다 — 실창 [WIN] |
| RPT-010 | 헤디드 남김 | 큰 창 줌(− + 100%)은 `ReportLargePreviewWindow.xaml.cs:73-86` 코드비하인드가 `WebView2.ZoomFactor` 를 직접 바꾼다 — 뷰모델 · 순수 함수가 없어 창 + WebView2 실체 없이는 단언할 것이 없다 |
| RPT-017 | 부분 통과(새) | RLS.should_reorder_template_components_by_mouse_exactly_like_alt_arrow(3사례: b↑ · a↓ · c↑) — 진짜 `ReorderKeyboardBehavior`(Alt+↑/↓) 결과 = 마우스 놓기(커널과 같은 `DragMath.InsertionIndex` 삽입 자리 + 같은 담당 `Drop`) 결과, 미적용 표시 · [적용] PATCH 구성 순서까지 같음; RLS.should_keep_the_component_order_and_stay_clean_when_the_drag_is_released_outside_or_back_in_place — 다른 드롭존 · 순서 드롭 아님 · 제자리 놓기 → 순서 · 미적용 그대로, PATCH 없음. **남김**: 실마우스 좌표 끌기 [COORD] · Esc 키 취소(커널 `CaptureDragBehavior.OnRootPreviewKeyDown` — 창 필요) |
| RPT-019 | 부분 통과(새) | RLS.should_hang_a_registered_question_mark_on_each_reports_area(4: 콘솔 · 심각도 · 구성 요소 · 생성 취소/삭제 — 뷰 XAML 의 `ConsoleShell`/`ConsoleSection` HelpKey + 설명 목록 등록 · 제목 · 내용); RLS.should_open_one_reports_callout_at_a_time_and_close_only_the_open_one_with_escape(말풍선 한 번에 하나); RLS.should_open_the_nearest_reports_question_mark_when_help_is_asked_from_inside_it(F1 명령 → 가장 가까운 범위의 "?", 절 밖이면 콘솔 "?"). **남김**: 실제 뷰 트리에서 셸 · 절 템플릿이 "?" 에 범위를 거는 배선, 말풍선 그림(Popup), 실제 F1 키 제스처 |
| RPT-020 | 헤디드 남김(정적 부분 시험 · 표지 없음) | RLS.should_draw_reports_views_with_theme_brushes_that_exist_in_both_themes_and_follow_a_theme_switch — 보고서 뷰 7개 + 코드 셀 템플릿의 색 토큰이 모두 DynamicResource 이고 라이트 · 다크 토큰 양쪽에 있음, 고정 색은 미리보기 종이 흰색(R20, 의도) 둘뿐. **남김**: 기대 「레이아웃 위반 없음」(LayoutAudit)은 실렌더라 이 시험이 단언하지 않는다 → 표지를 달지 않았다 |
| RPT-024 | 부분 통과(새) | RLS.should_reorder_phrases_by_mouse_exactly_like_alt_arrow_and_the_arrow_buttons(3사례) — Alt+화살표(진짜 behavior) = 마우스 놓기 = ▲▼(`MoveSelected`): 화면 순서 · 즉시 저장 reorder 본문(단일 호출) · 되돌리기 켜짐까지 같음; RLS.should_keep_the_phrase_order_and_send_nothing_when_the_drag_is_released_outside_or_back_in_place — 바깥 · 제자리 → 순서 그대로, reorder 호출 없음, 되돌리기 꺼짐. **남김**: 실마우스 좌표 [COORD] · Esc 키 취소(커널, 창 필요) |
| RPT-025 | 새 시험 작성 — **Events.Ui 빌드 · 실행 필요(이 작업에서 미실행)** | PTS.should_show_the_newly_registered_phrase_in_the_event_tray_when_the_template_change_arrives(등록 → 알림 전엔 그대로 → `ActionReportTemplatesChangedMessage("CREATED")` → 트레이 문구 끝에 새 문구, '기타' 마지막); PTS.should_show_the_phrase_registered_while_the_event_console_was_closed_when_it_opens_again; PTS.should_bind_the_tray_phrase_box_to_the_phrase_options(`Console.Events.Tray.Phrase` = `Tray.PhraseOptions`). 진짜 `ActionReportPhraseSource` 를 상태 가진 가짜 문구 서버 위에 세움. 보고서 쪽 몫은 RLS.should_store_a_registered_phrase_at_the_end_of_the_server_order_that_the_event_tray_reads(표지 없음 — 등록 POST display_order = 끝 + 서버 순서). SYNC_ACTION_REPORT_TEMPLATE → 메시지 변환은 호스트 라우터(라이브러리 밖) |
| RPT-028 | 부분 통과(새) | RLS.should_hang_the_registered_phrase_question_mark_on_the_phrase_console_header(문구 창 머리 `ConsoleShell HelpKey="Reports.Phrase"` + 「순서 바꾸기」 · 「다른 곳에서 바뀌었을 때」 묶음); RLS.should_open_one_reports_callout_at_a_time_and_close_only_the_open_one_with_escape(열린 말풍선만 Esc 로 닫고 먹음, 닫혀 있으면 Esc 통과). **남김**: 말풍선 그림 · 실창 Esc 키 |

### 기존 시험 판정(표지 추가 안 함)
- `ActionReportTemplateDropTests` · `TemplateComponentDropTests`: 담당의 받기/거절 · 이동 판정만 본다 — 기대 「Alt+화살표와 같음 · Esc 취소」를 단언하지 않으므로 RPT-017/024 표지를 달지 않았다(비교 단언은 RLS 새 시험).
- `ReportConsoleTests.should_drop_the_preview_while_the_large_window_shows_the_same_report`: 열기 요청 · 자리표시자 전환만 보고 「큰 창에서 보고 있습니다」 글은 단언하지 않는다 — 표지 없이 두고 RLS 새 시험이 그 글까지 본다.
- 커널 쪽 Esc(끌기 취소 · 말풍선 닫기) · F1 의 실창 경로는 `Ironwall.Dotnet.Libraries.Utils.Tests` 의 `CaptureDragLifecycleTests` · `HelpTipTests` 가 화면 밖 창으로 다룬다 — 창을 띄우므로 이 작업에서 돌리지 않았다.

## 2. 찾은 결함
- 없음. 새 시험 18사례(Reports.Ui)가 모두 첫 실행에 통과했다 — 빨강→초록 기록 없음, 제품 코드 무변경.
- 관찰(결함 단정 아님): 큰 창 줌 범위는 0.5~3.0(코드비하인드), 상세 칸 미리보기 줌은 `ReportPreviewFit` 0.30~3.0 — 두 곳 하한이 다르다. 사양 확인이 필요하면 헤디드 RPT-010 회차에서 같이 본다.

## 3. 파일 · 실행 명령 · 수

### 새로 만든 파일(둘 다 UTF-8 BOM `ef bb bf` 확인)
- `Ironwall.Dotnet.Libraries.Reports.Ui/Tests/ReportLeftoverScenarioTests.cs` — 새 시험 18사례(표지 16 · 표지 없음 2: RPT-020 정적 · RPT-025 보고서 쪽)
- `Ironwall.Dotnet.Libraries.Events.Ui/Tests/ReportPhraseTrayScenarioTests.cs` — 새 시험 3사례(RPT-025). **Events.Ui 는 다른 에이전트가 동시에 빌드 · 편집 중이라 빌드 · 실행하지 않았다.**
- 고친 파일: 없음(기존 시험 · 제품 코드 무변경).

### 실행 명령
```
dotnet build Ironwall.Dotnet.Libraries.Reports.Ui/Ironwall.Dotnet.Libraries.Reports.Ui.csproj --no-dependencies -v q -nologo -clp:ErrorsOnly
dotnet test Ironwall.Dotnet.Libraries.Reports.Ui/Ironwall.Dotnet.Libraries.Reports.Ui.csproj --no-build --filter "FullyQualifiedName~ReportLeftoverScenarioTests"
dotnet test Ironwall.Dotnet.Libraries.Reports.Ui/Ironwall.Dotnet.Libraries.Reports.Ui.csproj --no-build --filter "Scenario~RPT"
dotnet test Ironwall.Dotnet.Libraries.Reports.Ui/Ironwall.Dotnet.Libraries.Reports.Ui.csproj --no-build
```
Events.Ui(남은 일 — 빌드 · 실행 필요):
```
dotnet build Ironwall.Dotnet.Libraries.Events.Ui/Ironwall.Dotnet.Libraries.Events.Ui.csproj --no-dependencies -v q -nologo -clp:ErrorsOnly
dotnet test Ironwall.Dotnet.Libraries.Events.Ui/Ironwall.Dotnet.Libraries.Events.Ui.csproj --no-build --filter "FullyQualifiedName~ReportPhraseTrayScenarioTests"
```

### 수
- Reports.Ui 빌드: 오류 0(경고 9 — 기존).
- 새 시험(RLS): 18 / 18 통과.
- `Scenario~RPT` 표지 시험: 66 / 66 통과(앞 작업 50 + 새 16).
- Reports.Ui 전체 회귀: **359 / 359 통과, 실패 0**(앞 작업 341 + 새 18). Reports.Ui 시험 중 `.Show()` · `ShowDialog` 쓰는 것은 없어 회귀에서 뺀 클래스 없음.
- Events.Ui PTS: 미실행(위 명령 필요).

## 4. 집계(RPT 28 기준, 이번 반영 후)
- 앞 작업 통과 20(변화 없음) + 이번 8줄: 부분 통과(새) 5(RPT-009 · 017 · 019 · 024 · 028) · 헤디드 남김 2(RPT-010 · RPT-020[정적 부분 시험 있음]) · 새 시험 미실행 1(RPT-025, Events.Ui 실행 대기) = 28.
- 끝까지 헤디드로만 볼 수 있는 것: 큰 창 실창(009) · 큰 창 WebView2 줌(010) · 실마우스 끌기 좌표와 Esc 키(017 · 024) · 실제 뷰 트리 "?" 배선 · 말풍선 그림 · 실제 F1/Esc 키(019 · 028) · 두 테마 실렌더 레이아웃(020).
