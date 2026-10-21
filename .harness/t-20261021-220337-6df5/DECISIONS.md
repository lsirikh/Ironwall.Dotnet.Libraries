# 결정 기록 — 편집 모드에서 3D 유지(t-20261021-220337-6df5)

## 1. 아직 3D 호스트를 만들지 않은 상태("none")에서 편집 모드가 3D 를 막을지
- options: (a) `Map3DGuide.ShouldLeave3DForWindow` 그대로(composition 만 머묾 → "none" 도 막음) · (b) "hwnd" 일 때만 막음(새 `Map3DGuide.EditModeLeaves3D`)
- chosen: (b)
- why: (a) 면 앱을 켜고 처음 3D 에 들어가기 전에는 호스트가 "none" 이라 편집 모드 중 3D 진입이 영영 안 된다(요청 1 "편집 모드 중에도 3D 에 들어갈 수 있게"와 충돌). 기본 방식이 composition(`UseComposition = true`)이고, 열다가 네이티브 창으로 바뀌면 기존 `HostFallback` 처리기가 3D 를 접은 뒤 새 판정이 다시 막는다. Edit3D 의 `ShouldEditIn3D`(`HostMode != "hwnd"`)와 같은 갈래라 규칙이 하나로 모인다(pattern: reuse · follow-recommended).
- effect: 편집 모드는 HostMode "hwnd" 일 때만 3D 를 접는다/막는다. 바꾸려면 `Map3DGuide.EditModeLeaves3D` 한 줄.

## 2. 묶음 회전(「회전」 단추 · ] [ 키)을 3D 위에서 시작하면
- options: (a) 막고 안내만 · (b) 2D 로 보낸다(다른 2D 도구와 같게) · (c) 그대로 둔다(보이지 않는 2D 지도에서 회전)
- chosen: (b)
- why: 요청이 "그룹 회전"을 (c) 2D 전용 도구로 들고 "시작할 때만 2D 로" 라고 했다. 회전 세션 열림을 `IsMap3DBlockedByInputMode` 에 더하고(`IsGroupRotationPanelOpen`), 세션이 열리고 닫히는 순간에만 `SyncMap3DWithInputModes` 를 부른다 — 측정 · 심볼 배치와 똑같은 기존 경로(pattern: reuse).
- effect: 회전을 시작하면 3D 를 접고(진입 보류), 끝나면 기존 규칙(수동 = 단추로만, 자동 = 줌 · 영역을 새로 만족할 때). 되돌리려면 `MapViewModel.GroupRotation.cs` 의 `_groupRotationWasOpen` 블록과 Map3D.cs 의 `|| IsGroupRotationPanelOpen` 을 빼면 된다.

## 3. 3D 에 머무는 동안 방향키 · Ctrl+V(2D 지도 기준 동작)
- options: (a) 그대로(보이지 않는 2D 지도에서 1 px 이동 · 2D 커서 자리에 붙여넣기) · (b) 3D 를 나가며 실행 · (c) 3D 동안은 받지 않는다(방향키는 3D 방향 링으로)
- chosen: (c)
- why: 방향키는 3D 방향 링 키이기도 하다(기존 시험 `should_leave_group_rotation_keys_alone_and_let_ring_arrows_through_window_hook_in_3d`). 2D 화소 이동 · 2D 커서 붙여넣기는 3D 화면에서 뜻이 없고, 키 하나로 3D 를 떠나는 건 "편집 모드가 3D 를 떠날 필요 없다"는 요청과 어긋난다. Delete · Ctrl+C · Ctrl+A · Ctrl+Z/Y 는 선택 · 기록 기준이라 그대로 둔다.
- effect: 3D 중에는 방향키 · Ctrl+V 를 창 후킹이 먹지 않는다. 바꾸려면 `MapViewModel.cs` `OnMapPreviewKeyDownForGroup` 의 `!IsMap3DShown` 두 곳.

## 4. 옛 동작("편집 모드면 3D 를 접는다")을 단정하던 기존 시험 12건을 어떻게 고칠지
- options: (a) 지운다 · (b) 새 동작(머문다)으로 뒤집는다 · (c) 네이티브 창 3D 갈래(여전히 접힌다)로 옮긴다
- chosen: 편집 모드 켜기 자체를 보던 1건(`should_refuse_group_rotation_in_3d_and_stay_in_3d_when_edit_mode_turned_on`)은 (b), 편집 모드를 "입력 모드의 하나"로 써서 진입 보류 · 돌아갈 자리 · 미리 불러오기 · 판 닫기 · 이동 멈춤을 보던 11건은 (c)
- why: 그 11건이 지키는 결함(D-1 돌아갈 자리, 진입 보류 등)은 네이티브 창 3D 에서 여전히 일어나는 길이라 시험 가치가 남는다. 이름 · 그 줄만 바꾸고 이유 주석을 달았다(요청 5).
- effect: 이름에 `_on_native_window` · `EditModeOnNativeWindow` 가 붙었다. 시험 증거 수집(TRX) 이름이 바뀐 것에 유의.

## 5. (추가 지시) 편집 모드가 꺼졌을 때 3D 건물 · 직접 그린 건물 · 꾸미기 고르기를 어떻게 막을지
- options: (a) 엔진에 고르기를 끄라고 알린다 · (b) GIS 가 메시지를 무시하고, 엔진이 그렸을 수 있는 강조만 `*.select(null)` 로 지운다
- chosen: (b)
- why: 지금 GIS→3D 메시지 계약(Map3DBuilding · Map3DUserBuilding · Map3DDecoration · Map3DEdit 의 type 31종)에 "고르기 끄기"가 없다. 엔진은 이번에 고치지 않는다(요청 "지금 메시지 계약 안에서 되는 쪽"). `building.select` · `userbuilding.select` · `decoration.select` 의 null 은 이미 있는 메시지다(pattern: reuse).
- effect: 보기 전용에서 3D 를 눌러도 판 · 목록 요청 · edit.start 없음, 엔진 강조는 곧바로 지워진다. 엔진에 고르기 끄기 메시지가 생기면 `RequireMapEditMode(notify: false)` 세 곳에서 그 메시지로 바꾸면 된다.

## 6. 보기 전용에서 3D 고르기 · 고치기 입구를 눌렀을 때 안내
- options: (a) 3D 클릭마다 안내 · (b) 3D 클릭은 조용히, 고치기 입구(툴바 · 영역 배지 메뉴)만 한 줄 안내 · (c) 모두 조용히
- chosen: (b)
- why: 관제 중에는 3D 를 자주 누른다 — 매번 띠가 뜨면 방해된다("꺼짐 = 보기 전용"). 입구는 사용자가 고치려고 누른 것이라 "편집 모드에서 쓸 수 있습니다 — 툴바의 「편집」을 켜세요"(쉬운 말)를 띄운다. 툴바 단추는 편집 스트립 안이라 원래 편집 모드에서만 보이고, CanExecute 도 편집 모드로 꺼 둔다. "?" 말풍선(Map.Toolbar.EditMode)에 규칙 한 줄을 넣었다(pattern: follow-recommended).
- effect: 문구는 `MapViewModel.EditModeRequiredText`.

## 7. 편집 모드를 끌 때 저장 안 한 값이 있는 판(건물 높이 · 건물 그리기 · 꾸미기)
- options: (a) 저장 확인을 묻는다 · (b) 기존 2D 편집 모드 끄기 규칙대로 묻지 않고 버리고 닫는다
- chosen: (b)
- why: 기존 세터(IsEditModeEnabled=false)는 이미지 자르기 판 = 취소, 선 그리기 = 취소, 배치 = 취소로 묻지 않는다(요청 "기존 규칙을 찾아 따른다"). 3D 그리기는 draw.cancel, 손잡이는 edit.stop(Exit* → EndEdit3D), 「건물 높이」 미리보기는 원래 높이로(빈 땅 클릭과 같은 닫기).
- effect: `CloseMap3DEditPanelsForViewMode`(MapViewModel.Edit3D). 묻게 하려면 이 함수 안에서 확인 팝업으로 바꾸면 된다.

## 8. 영역 배지 메뉴 중 어떤 항목을 편집 모드로 막을지
- options: (a) 건물 고치기 항목만(「건물 그리기」 · 「건물 높이 모두 자동으로」 · 「직접 그린 건물 모두 지우기」) · (b) 영역 관리(다시 만들기 · 지우기 · 3D 줌 바꾸기)까지
- chosen: (a)
- why: 요청 범위가 "건물 클릭 · 건물 그리기 · 외관 · 꾸미기". 영역 만들기 · 다시 만들기 · 지우기는 3D 영역 관리이고 기존 권한 규칙이 따로 있다(pattern: 범위를 지어내지 않음).
- effect: 막힌 항목은 메뉴에 그대로 보이고 누르면 안내 한 줄. (b) 로 넓히려면 `ApplyMap3DAreas` 의 해당 action 람다에 `RequireMapEditMode(true)` 를 더한다.

## 9. 고치기 시험 장치(UserBuilding · TwinContract · BuildingHeight(Edge))의 시작 상태
- options: (a) 시험마다 편집 모드를 켠다 · (b) 장치를 편집 모드로 시작(보기 전용 시험만 끈다)
- chosen: (b) — 권한 없는 계정(canEdit:false) 시험도 켠 채(편집 모드 중 권한을 잃은 경우의 CanEditMap 막힘을 그대로 본다)
- why: 실제 앱에서 그 입구(편집 스트립)는 편집 모드에서만 보인다 — 시험 장치가 실제와 같아진다. 시험 본문은 바꾸지 않는다.
- effect: 3D 진입 시험 장치(Map3DEntryScenarioTests.Rig)는 보기 전용 그대로 두고, 건물을 고르는 도우미에서만 `UseEditMode` 를 부른다.

## 10. 편집 모드에서 건물 높이 판을 연 채 마커를 3D 에서 누르면
- options: (a) 예전처럼 카드를 열려고 2D 로 · (b) 편집 모드 마커 클릭 규칙대로 편집 선택(카드 없음), 3D · 판 그대로
- chosen: (b)
- why: 판은 이제 편집 모드에서만 열리고, 편집 모드의 마커 클릭은 원래 조립 카드가 아니라 편집 선택(OnMapMarkerClicked)이다. 옛 조합 시험(`should_leave_3d_revert_preview_and_open_card_…`)은 일어날 수 없는 조합이 되어 새 동작으로 바꿨다.
