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

## 5. 직접 그린 건물 판에서 값을 바꾸는 즉시(저장 전) 3D 상자에 미리 보일지 — ★추가 지시로 뒤집음
- options: (a) 넣는다(값 미리보기 userbuilding.preview · 취소 때 되돌림) · (b) 넣지 않는다 — [저장] 뒤 미리보기만
- chosen: 처음엔 (b), 추가 지시("6층으로 했는데 그대로 3층" · "바꾸면 3D 미리보기 상자에 바로")로 (a)
- why: 추가 지시가 앞선 범위 판단보다 먼저다.
- effect: `MapViewModel.UserBuilding.cs` PreviewUserBuildingDraft(RefreshUserBuildingPanel · 지붕 · 외관 세터 끝) · RevertUserBuildingPreview(ResetUserBuildingDraft · ExitUserBuildingDraw · [2D 에서 윤곽 고치기]). 판 값 그대로의 상자 JSON 이 마지막에 보낸 것과 같으면 다시 보내지 않는다.

## 6. 바로 미리보기를 2D 그리기에서도 보낼지
- options: (a) 3D 위 판(_ubIn3D)에서만 · (b) 2D 그리기에서도(접혀 있는 3D 에)
- chosen: (a)
- why: 2D 그리기는 3D 를 접어 둬 아무도 보지 못한다. 2D 기존 시험(저장 뒤 미리보기 한 번)을 그대로 둘 수 있다. pattern: speed.
- effect: 2D 에서는 예전처럼 [저장] 뒤에만 상자. 바꾸려면 PreviewUserBuildingDraft 첫 줄의 `!_ubIn3D` 조건.

## 7. 새 윤곽(아직 저장 안 함)의 미리보기 uid
- options: (a) 윤곽을 닫을 때 uid 를 미리 만들어 미리보기 · 저장에 같은 uid · (b) 저장된 건물만 미리보기
- chosen: (a)
- why: 3D 에서 새로 그린 건물도 층수를 바꾸며 바로 봐야 한다(사용자 결함과 같은 흐름). 미리 그린 상자가 그대로 그 건물이 된다.
- effect: `_ubNewUid`(UserBuildingDraftUid). [취소] · 끝내기 = 그 상자 지움(height null).

## 8. 기울기(taper_deg) 400 거절 때 무엇을 되돌리나
- options: (a) 기울기만 저장된 값으로 · (b) 판 값을 모두 저장된 값으로
- chosen: (a)
- why: 지시 "판에 그 문구를 보이고 값은 되돌린다" 의 값 = 기울기. 함께 바꾼 층수 · 색은 다시 [저장] 할 수 있게 남긴다.
- effect: 거절이 400 이고 기울기를 바꿨을 때만 되돌린다(3D 상자도). 다른 400(색 · 모양)은 값 그대로.

## 9. 지도 건물 「지우기」의 실체 · 단추 자리
- options: (a) 숨김(looks hidden) 그대로 이름만 [이 건물 지우기] · 외관 줄에서 새 「지우기 · 다시 만들기」 칸으로 옮김 · (b) 이름만 바꾸고 자리 그대로
- chosen: (a)
- why: 지시대로 실제 삭제가 아닌 숨김(되살리기 가능). 지우기 · 지우고 새로 그리기 · 여러 개 · 영역 모두가 한 칸에 모여야 찾는다. 「건물 높이」 판은 3D 에서만 열린다(2D 에는 지도 건물 판이 없다) — 2D 판은 만들지 않았다.
- effect: 「숨긴 건물 n개 · 되살리기 · 모두 되살리기」 는 그대로. 결함 조사: 단추 · 확인 · PUT · building.look 경로는 끝까지 동작(시험) — 로그에 누른 기록 자체가 없어 "숨기기" 라는 이름 · 외관 줄 안 자리가 찾지 못한 이유로 본다(추정).

## 10. 「지우고 이 자리에 새로 그리기」 의 바닥 윤곽 출처
- options: (a) 장면 파일 GET …/files/scene.json 의 buildings.footprint(뷰어가 장면을 불러올 때 받는 그 파일, 기존 API) · (b) 엔진에 고름 메시지 footprint 키를 요청하고 그때까지 빈 그리기
- chosen: (a) + 못 받으면 빈 그리기, 엔진 후보로 (b)
- why: 계약 안(기존 엔드포인트)에서 바로 된다. 확인 창 없이 바로(판 안 되돌리기 1칸으로 되돌릴 수 있음).
- effect: Map3DClient.GetSceneBuildingsAsync · Map3DBuilding.ParseSceneBuildings. 높이 = 그 건물 높이, 층수는 층고로 계산.

## 11. 여러 개 고르기 방식 · 사각형 끌기
- options: (a) 판 [여러 개 고르기] 켜고 3D 에서 차례로 누르기 + Ctrl+클릭 · (b) + 2D 지도 사각형 끌기(3D 를 접고 2D 에 장면 건물 윤곽을 그려 고름) · (c) + 3D 사각형 끌기(엔진 메시지 필요)
- chosen: (a), (b) · (c) 는 보고에 후보
- why: 3D 사각형 끌기는 엔진에 메시지가 없다(뷰어 pointerup 은 한 건물만 고른다). 2D 사각형은 지도 건물 윤곽을 2D 에 새로 그리는 표면이 필요한 큰 일이고, 넓게 지울 때는 「이 영역 자동 건물 모두 지우기」 가 같은 목적을 한 번에 한다. Ctrl 은 시험에서 사용자 키가 끼어들지 않게 `_map3DModifiers` 로 바꿀 수 있게 했다. pattern: follow-recommended.
- effect: 3D 는 마지막에 누른 건물만 강조(building.select 는 하나) — 고른 수는 판 글. 엔진 후보: `building.select {ids:[…]}` · 사각형 고르기 메시지.

## 12. 고른 n개 지우기 · 영역 모두 지우기 확인
- options: (a) 둘 다 확인 창 한 번 · (b) 고른 n개는 확인 없이(되돌리기로)
- chosen: (a)
- why: 하나 지우기도 확인 창이 있다 — 같게. 지시는 영역 모두에 확인을 요구했다.
- effect: CallHideMap3DBuildingsProcessMessageModel. 되돌리기 1칸(BuildingHeightUndoStep.Group) = 지우기 전 모양(색 · 무늬 포함)으로 되살림. [모두 되살리기] 뒤에는 그 칸을 비운다.
