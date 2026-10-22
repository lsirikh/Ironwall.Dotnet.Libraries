# DECISIONS — t-20261022-093356-2dae

## 1. 3D 위 「건물 그리기」 판이 열린 채 같은 자리의 구운 건물 고름이 오면 무엇을 우선하나
- options: (a) 나중 고름이 이긴다(구운 건물 판으로 바꾼다) · (b) 같은 자리(구운 건물 가운데가 아직 안 구운 직접 그린 건물 윤곽 안)면 직접 그린 건물이 우선 · (c) 직접 그린 건물 고름 뒤 몇 초 안의 구운 건물 고름만 무시
- chosen: (b)
- why: 사용자가 고치던 것은 직접 그린 굴뚝이고, 다시 만들면 겹친 자동 건물은 빠진다(userbuildings OVERLAP 규칙). 엔진 조사로 클릭 한 번에 메시지는 하나뿐이고 1.4초 뒤 고름은 두 번째 클릭(엔진 굴뚝 29.6 m 가 직접 그린 18 m 보다 높아 윗부분이 맞음)이라 (c) 의 시간 창은 근거가 없다. 목표 문구 "편집 중인 것 · 직접 그린 것 우선". pattern: follow-recommended.
- effect: 그 자리 구운 건물 고름은 구운 건물 강조를 지우고(building.select null) 직접 그린 건물 강조를 되돌린다(userbuilding.select uid). 판이 열려 있지 않을 때는 예전처럼 「건물 높이」 판. 바꾸려면 `MapViewModel.BuildingHeight.cs` `OnMap3DBuildingPickedOverEdit3D` 의 ② 조건.

## 2. 떨어진 구운 건물을 고르면 열린 직접 그린 건물 · 꾸미기 판을 어떻게 하나
- options: (a) 판을 닫는다(그리기 · 꾸미기 끝 — 고름 · 손잡이 · 3D 그리기 모두 내림) · (b) 고름만 풀고 판은 남긴다
- chosen: (a)
- why: 꾸미기 고름 · 직접 그린 건물 고름이 이미 서로 상대 판을 닫는다(StartDecoration · StartUserBuildingDraw → CancelConflictingModesForMeasure). 같은 규칙으로 맞추면 3D 위에 판이 하나만 뜬다. pattern: reuse.
- effect: ExitUserBuildingDraw("building-pick") · ExitDecoration("building-pick"). 저장 안 한 값은 버린다(편집 모드 끄기와 같은 규칙).

## 3. 3D 그리기(draw.start) 중 구운 건물 고름이 오면
- options: (a) 무시하고 엔진이 그린 강조만 지운다 · (b) 그리기를 끝내고 「건물 높이」 판
- chosen: (a)
- why: 그리기 중 클릭은 점 찍기 몫("편집 중인 것 우선"). 뷰어는 그리기 중 고르기를 보내지 않지만(view.js:465 `if (drawSt) { onDrawUp(ev); return; }`), GIS 가 도구를 눌러 draw.start 를 보낸 직후 뷰어가 받기 전의 클릭 고름은 올 수 있어 그 틈을 막는다.
- effect: 그리기 모드 · 판 그대로, building.select null 하나만 보낸다.

## 4. 뷰어의 building 메시지에 외관 키가 없다 — 다시 고를 때 저장된 외관을 어디서
- options: (a) GIS 가 판을 열 때 받는 GET …/looks 목록의 그 건물 줄을 쓴다 · (b) 엔진에 building 메시지 외관 키 추가를 요청하고 GIS 는 그대로
- chosen: (a) + 보고에 엔진 후보로 (b) 를 적는다
- why: 엔진은 이번에 건드리지 않는다(목표 3). (a) 는 기존 계약 안이고, 이게 없으면 지붕 색만 바꿔 [외관 적용] 할 때 저장된 외벽 무늬 · 색이 자동으로 지워진다(데이터 손실). pattern: autonomy.
- effect: `MapViewModel.BuildingLook.cs` `WithSavedLook` — 목록을 받으면(또는 이미 받은 이 장면 목록이면 고를 때 바로) 판의 "저장된 값"을 맞춘다. 판에서 이미 고친 값(_bhLookEdit)은 덮지 않는다.

## 5. 직접 그린 건물 판에서 콤보를 바꾸는 즉시(저장 전) 3D 상자에 미리 보일지
- options: (a) 이번에 넣는다(값 미리보기 userbuilding.preview · 취소 때 되돌림) · (b) 넣지 않는다 — 계약대로 [저장] 뒤 미리보기만, 빠진 키(use)만 고친다
- chosen: (b)
- why: 목표 2(b) 는 "저장 뒤 userbuilding.preview(새 값 포함)" 를 확인 · 수정 범위로 정했다. 즉시 미리보기는 새 동작이라 범위 밖. pattern: follow-recommended.
- effect: 보고에 후보로 적었다. 넣으려면 UserBuildingFacade · WallColor · RoofColor · Roof · Use 세터에서 `_ubEditing` 이 있을 때 판 값으로 PreviewMessageJson 을 보내고, ResetUserBuildingDraft · ExitUserBuildingDraw 에서 저장된 값으로 되돌린다(뷰어는 이미 "값 미리보기" 상자를 받는다 — view.js onUserBuildingMessage).
