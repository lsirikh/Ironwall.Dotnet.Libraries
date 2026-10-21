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
