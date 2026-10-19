# DECISIONS — 직접 그린 건물

## 1. REST 주소를 영역 키가 아니라 장면(job) id 로
- options: (a) /api/areas/{영역 키}/user-buildings 새 주소 체계 (b) 기존 「건물 높이」처럼 /api/jobs/{id}/user-buildings — 저장은 그 장면 bbox 해시 파일
- chosen: (b)
- why: GIS 는 영역을 job id 로만 안다(Map3DAreaItem.Id). 저장 파일은 기존 data/overrides 와 같은 bbox 해시 이름(data/user-buildings/area-<key>.json)이라 요청 값이 경로가 되지 않고, 읽을 때는 장면 범위(회전 영역은 회전 사각형)에 중심이 든 항목을 모든 파일에서 모아 다시 만들기(새 job id)에도 유지된다.
- effect: 다시 만들기 · 다른 방식 굽기에서 유지. 영역 키 주소가 필요해지면 userbuildings.items_in(scene) 을 그대로 쓰는 라우트를 더하면 된다.

## 2. 겹친 자동 건물 빼는 기준
- options: IoU 만 / 덮인 넓이 비만 / 둘 중 하나
- chosen: 둘 중 하나 — 자동 건물 넓이 중 직접 그린 건물에 덮인 몫 ≥ 0.5 또는 IoU ≥ 0.3 (userbuildings.OVERLAP_FRAC · OVERLAP_IOU)
- why: 작은 자동 건물이 큰 그린 건물 안에 들면(덮인 몫) 빼야 하고, 조금 크게 딴 같은 건물(IoU)도 빼야 한다. 4 배 큰 자동 건물이 작은 그린 건물을 감싸는 경우(둘 다 0.25)는 다른 건물로 보고 남긴다.
- effect: 상수 두 개로 조정.

## 3. 3D 에서 끌어 올리기 = 기존 「건물 높이」 판 재사용
- options: (a) 3D 에 새 판 (b) 다시 만든 직접 그린 건물(scene.json user_uid)을 기존 판으로 고르고, 서버 heights.set_height 가 그 건물이면 높이 직접 입력 파일 대신 user-buildings 파일을 고친다
- chosen: (b)
- why: GIS 변경 없이 손잡이 끌기 · 층수 · 키 · 되돌리기가 그대로 된다. 직접 그린 건물은 자동 높이가 없어 「자동으로 되돌리기」는 아무것도 하지 않는다(판에 단추가 꺼진다 — Manual=false). 출처 글은 "직접 그린 건물".
- effect: 다시 굽기 전(상자 미리보기)에는 3D 에서 고를 수 없고 2D 판으로 높이를 바꾼다.

## 4. 3D 상자 미리보기 유지 방식
- options: GIS 가 보낸 메시지로만 / 뷰어가 장면을 불러올 때 서버 목록(baked_id 없는 것)으로도
- chosen: 둘 다 — 저장 즉시 userbuilding.preview(그 장면을 띄워 둔 3D 에만), 장면을 불러올 때 GET …/user-buildings 의 아직 안 구운 건물을 상자로
- why: 그리기는 2D 에서 하고 3D 는 그 뒤에 열린다 — 메시지만으로는 다시 불러오면 상자가 사라진다.
- effect: 다시 만들면 baked_id 가 생겨 상자 대신 꾸며진 건물이 보인다.

## 5. 되돌리기(Undo)
- options: (a) 판 안 전용 칸(건물 높이 판처럼) (b) GIS 편집 기록(IUndoService)에 UserBuildingCommand
- chosen: (b) + 그리기 중 Ctrl+Z 는 윤곽 편집 칸 먼저, 그다음은 편집 기록 맨 위가 "직접 그린 건물…" 일 때만 그것
- why: 목표의 "GIS 편집 기록 관용구". 다른 기능 기록을 그리기 중 Ctrl+Z 가 되돌리지 않게 설명 앞머리로 가린다. 편집 모드의 GIS 되돌리기 · 다시 하기로도 된다.
- effect: 설명 앞머리 상수 Map3DUserBuilding.UndoPrefix.

## 6. 그리는 동안 지도 끌기(팬)
- options: 왼쪽 끌기 = 팬 유지 / 측정 · 선 그리기처럼 그리기가 왼쪽 입력을 모두 받음
- chosen: 그리기 표면(어도너)이 왼쪽 입력을 모두 받고 휠 줌은 지도로 넘김
- why: 꼭짓점 끌기가 팬 · 마커 고르기보다 먼저여야 하고, 측정 도구와 같은 규칙(그리는 동안 왼쪽 끌기 팬 없음).
- effect: 그리는 중 화면 이동은 휠 줌 · 그리기 끝낸 뒤. 바꾸려면 UserBuildingAdorner 에서 빈 곳 끌기를 지도로 넘긴다.

## 8. (추가 지시) 판 공용부 — 중복 대신 공용 바탕 클래스
- options: 두 판 컨트롤에 같은 손잡이 · 키 · 닫기 코드 / 공용 바탕 HeightPanelControlBase 로 옮김
- chosen: 공용 바탕(「건물 높이」 판도 그것을 상속 — 공개 멤버 Host · CloseCommand · CloseCommandProperty · CancelHandleDrag · IsDraggingHandle 은 상속으로 그대로 보인다)
- why: 품질 검사가 지적한 중복. 판마다 다른 것은 손잡이 AutomationId 와 도구 단축키(HandleExtraKey)뿐이다.
- effect: 「건물 높이」 시험 그대로 통과. ↑/↓ 는 AutomationId 가 ".Name" 으로 끝나는 입력칸(이름)에서는 판 높이를 바꾸지 않는다(건물 높이 판에는 그런 칸이 없어 영향 없음).

## 9. (추가 지시) 그리던 윤곽이 있을 때 도구를 바꾸면
- options: 바꾸지 못하게 막음 / 그리던 윤곽을 버리고 새 도구로 새로
- chosen: 버리고 새로(빈 윤곽이면 그냥 바꿈)
- why: 아이콘을 누르는 것이 곧 "이 모양으로 새로 그리겠다"는 뜻. 저장하지 않은 윤곽만 버리고, 저장된 건물은 그대로다.
- effect: MapViewModel.SelectUserBuildingToolCommand 의 ResetUserBuildingDraft 를 빼면 막기가 된다.

## 10. (추가 지시) 원뿔 · 돔 지붕은 둥근 윤곽에서만
- options: 어떤 윤곽이든 원뿔 · 돔 / map-3d 의 탱크 규칙(buildings.is_round — 17각 이상 · 원형도 0.9 이상)에서만 둥근 지붕, 아니면 평지붕
- chosen: 탱크 규칙 재사용(목표의 "원형+돔 = tank 모양 규칙 재사용")
- why: 둥근 지붕 메시(round_roof)는 원기둥 벽(탱크 외벽)과 짝이다. 사각 윤곽에 돔을 고르면 평지붕으로 굽는다(도움말에 적음). 원 도구로 만들면 지붕을 고른 적이 없을 때 돔을 제안한다.
- effect: 타원은 원형도가 낮아 돔이 평지붕이 된다 — 바꾸려면 buildings.build_building 의 둥근 지붕 판정을 넓힌다.

## 11. (추가 지시) 지도의 기존 선 · 도형 마커를 골라 윤곽으로 쓰기("있으면 좋음")
- options: 이번에 함께 / 미룸
- chosen: 미룸
- why: 마커 종류마다 위경도 꼭짓점을 꺼내는 경로가 달라(선 · 도형 · 이미지) 별도 작업이 크다. 형상 도구 4종 · 편집 · 지붕 · 외벽이 먼저.
- effect: 후속 작업으로 Map3DUserBuildingDraft.Load(윤곽) 에 그 마커의 꼭짓점을 넘기면 된다(고치기와 같은 입구).

## 7. 저장 뒤 그리기 모드 유지
- options: 저장하면 끝냄 / 빈 윤곽으로 이어서 그리기
- chosen: 이어서 그리기(Esc 두 번 또는 ✕ 로 끝)
- why: 여러 채를 연달아 그리는 일이 많다.
- effect: ApplyUserBuildingAsync 끝의 ResetUserBuildingDraft(null) 을 ExitUserBuildingDraw 로 바꾸면 끝내기.
