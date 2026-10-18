# PLAN — 선택 묶음 회전(2D 편집 모드)

## 목표
여러 개 선택(그룹 ≥2, Ctrl+A 포함)한 오버레이 항목을 묶음 가운데 기준으로 회전. 드래그 손잡이(데드존 8 · Shift 15° · ESC 취소) +
키보드 폴백(`]`/`[` ±1°, Shift ±0.1°, Enter 확정, Esc 취소) + Undo 1단위 + 기존 그룹 이동과 같은 저장(DbUpdateProcess).

## 구조
- `GMaps.Ui/Helpers/GroupRotationMath.cs` — 순수 수학(WPF·GMap 무의존): 메르카토르 왕복 · 경계 상자 중심 · 회전 · 각 정규화 · 드래그 각도 · Shift 스냅 · 표시 글
- `GMaps.Ui/Services/GroupRotationSession.cs` — 원본 스냅샷(잠금 제외) → Preview(각, 원본 기준 절대) → Commit/Cancel, `Restore`
- `GroupSelectionService` — 세션 소유(`BeginRotation/PreviewRotation/RotateBy/EndRotation`), `GroupRotateCompleted` 이벤트, Clear 때 열린 회전 확정
- `GroupSelectionAdorner` — 회전 손잡이 Thumb(AutomationId `Map.GroupSelection.RotateHandle`) · PreviewMouseDown 선점 캡처 · 데드존 · `FinishRotationDrag` · 중심 십자 · 각도 칩
- `MapViewModel` — `OnGroupRotateCompleted`(Undo 배치 → DbUpdateProcess) · `TryRouteGroupRotationKey` · `SelectAllForGroup`(Ctrl+A)
- 도움말 `Map.EditStrip.Selection` 에 Ctrl+A · 회전 손잡이 · ] [ 문구

## 진행
- [x] 순수 수학 + 시험(tests/GMaps.Ui.Tests/GroupRotationMathTests.cs, 41건)
- [x] 세션 · 서비스 · 어도너
- [x] VM 배선(키, 완료 처리, Ctrl+A)
- [x] VM 시나리오 시험(tests/GMaps.MapEdit.Tests/GroupRotationScenarioTests.cs, 10건 — 창 없음)
- [x] 도움말 "?" 문구
- [x] 툴바 Undo/Redo 단추 누를 때 열린 회전 먼저 확정(UndoContext 한 줄)
- [x] dotnet test sln(전 프로젝트 통과) + GMaps.Ui.Tests 1295 + GMaps.MapEdit.Tests 65 + MapsHelpTests 32 통과

## 발견
- 기존 Ctrl+A 는 AdornerManager 단일 경로 + MultiSelectEnabled=false 로 사실상 꺼져 있었음 → VM 창 키 처리기에서 그룹 선택으로 받음
- 선 컨트롤 템플릿(generic.xaml)의 고정 안 된 DropShadowEffect 는 시험 STA 스레드가 바뀌면 깨짐 → 시험에서 선 마커 Shape 를 Border 로 대체

## 남은 것 / 실기 미확인
- 실제 마우스 끌기 · Shift 상태 · 지도 회전(뷰 회전) 상태에서의 손잡이 각 · 라이트/다크 그림은 실기 미확인
