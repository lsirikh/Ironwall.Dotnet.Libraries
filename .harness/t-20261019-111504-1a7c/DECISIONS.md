# DECISIONS — t-20261019-111504-1a7c

## 1. 덮어쓰기를 어디에 묶을까(장면 id · 영역 키 · 자리)
- options: 장면 id 별 파일 / 영역(bbox) 키 파일만 보고 같은 bbox 일 때만 적용 / 영역 키 파일에 저장하되 다시 굽기 때는 장면 범위에 중심이 든 모든 파일의 항목을 자리(IoU)로 붙임
- chosen: 마지막 — `data/overrides/area-<bbox 소수 5자리 sha1 16자>.json` 에 저장, 굽기 때는 장면 bbox 안 항목 전부를 IoU ≥ 0.4 로 붙인다
- why: 다시 굽기 · 다른 방식 굽기에서 영역이 조금 달라도(다시 그린 영역) 직접 입력이 사라지지 않게. 파일 이름은 해시라 요청 값이 경로가 되지 않는다
- effect: 임계는 `heights.MATCH_IOU`, 파일 규칙은 `heights.area_file` 에서 바꾼다

## 2. 되돌리기(Undo) — 3D 중 Ctrl+Z 가 막혀 있다
- options: GIS 편집 기록(EditRecorder/UndoService)에 새 명령 넣기 / 판 안 「되돌리기」 단추(판 전용 기록)
- chosen: 판 안 「되돌리기」(AutomationId `Map.BuildingHeight.Undo`) + 판에 포커스가 있을 때 Ctrl+Z 도 같은 동작
- why: GIS 의 Undo 는 편집 모드 ∧ 지도 편집 권한에서만 켜지고(CanUndo), 3D 는 편집 모드에서 열리지 않는다(Map3DMath.IsBlockedByInputMode) — 지시의 대체안
- effect: 기록은 3D 장면마다(다른 장면 · 3D 나가기에서 비움). 공용 Undo 로 옮기려면 IUndoApplyContext 명령으로 감싸면 된다

## 3. 지금 장면 반영 방식(다시 굽기 없이)
- options: 그 건물만 다시 세우기(build_building 재호출 — 사진 텍스처 · 지붕 재료 재구성 필요) / GLB 정점만 세로로 고치기
- chosen: GLB 정점만 — 옛 벽 꼭대기 위(지붕 · 난간 · 옥탑)는 통째로 올리고 벽은 바닥 기준으로 늘인다(사진 텍스처는 늘어남). 뷰어 미리보기와 같은 식
- why: 몇 초 안(실제 수십 ms), 지붕 · 난간 · 옥탑 모양이 깨지지 않는다. 다시 굽기에서는 새 높이로 제대로 다시 세운다
- effect: `heights.stretch_y` · `buildingedit.stretchY` 두 곳이 같은 식(시험이 같은 값 확인). 블렌더 디테일 GLB 도 형식이 맞으면 같이 고친다

## 4. 「모두 자동으로」의 범위
- options: 그 장면에 붙은 직접 입력만 / 장면 범위의 못 붙인 직접 입력까지
- chosen: 장면 범위의 못 붙인 것까지 지운다(확인 팝업 뒤)
- why: 사용자가 "모두" 를 눌렀을 때 다음 굽기에서 옛 값이 되살아나지 않게
- effect: `heights.reset_all` 의 `_drop_items` 조건

## 5. 층고 입력 범위 · 끌기 감도
- options: 정하지 않음(지시 없음)
- chosen: 층고 2.0~10.0 m(소수 1자리, 기본 3.0) · 판 안 손잡이 세로 끌기 10 px = 1 m(위로 = 높게), 0.1 m 단위로 맞춤, 4 px 데드존
- why: 보통 건물 층고 2.5~6 m 를 넉넉히 담고, 끌기는 판 크기(손잡이 칸 높이)로 수십 m 를 오갈 수 있게
- effect: `Map3DBuilding` 상수에서 바꾼다

## 6. 판이 열려 있을 때 Esc
- options: 3D 나가기(기존 그대로) / 판만 취소(끄는 중이면 끌기만)
- chosen: 판만 취소 — 판이 닫혀 있으면 예전처럼 3D 를 나간다
- why: 지시의 "ESC = 취소". 판을 쓰다 Esc 로 3D 전체가 닫히면 미리보기 · 자리를 잃는다
- effect: `MapViewModel.TryHandleMap3DKey` 의 `TryCancelBuildingHeightByEscape` 한 줄

## 7. 높이 출처 "llm"(외부 AI 어림)의 쉬운 말
- options: 모름(기본값) / 사진에서 잼
- chosen: 사진에서 잼(정사영상 조각을 보고 층수를 어림한 것)
- why: 지시의 여섯 가지 말 안에서 가장 가까운 것
- effect: `Map3DBuilding.SourceText` 한 줄

## 8. 원격(네이티브 창) 3D 에서의 판
- options: 원격일 때 판을 열기 전에 2D 로 나가기 / 그대로 띄우기
- chosen: 그대로(WPF 안 3D 기준) — 네이티브 창 3D 는 WPF 판을 덮을 수 있다(실기 미확인)
- why: 판은 3D 미리보기를 보며 쓰는 것이라 2D 로 나가면 쓸모가 없다
- effect: 필요하면 `OpenBuildingHeightPanel` 에서 `Map3DGuide.ShouldLeave3DForWindow` 로 막는다
