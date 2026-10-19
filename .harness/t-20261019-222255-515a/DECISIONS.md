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

## 7. 저장 뒤 그리기 모드 유지
- options: 저장하면 끝냄 / 빈 윤곽으로 이어서 그리기
- chosen: 이어서 그리기(Esc 두 번 또는 ✕ 로 끝)
- why: 여러 채를 연달아 그리는 일이 많다.
- effect: ApplyUserBuildingAsync 끝의 ResetUserBuildingDraft(null) 을 ExitUserBuildingDraw 로 바꾸면 끝내기.
