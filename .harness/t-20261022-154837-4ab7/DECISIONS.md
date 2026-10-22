# 결정 기록 — t-20261022-154837-4ab7 (3D 에서 지도 심볼 고르기 · 고치기)

## 1. 도형 심볼(geometric)을 「점 심볼」로 볼지 「선 · 구역」으로 볼지
- options: (a) 점 심볼 — 옮기기 · 돌리기 손잡이 / (b) 선 · 구역 — 꼭짓점 · 통째 옮기기
- chosen: (a)
- why: 도형 심볼(GMapGeometricMarker)은 점이 아니라 위치 · 크기 · 방향 하나만 가진 마커이고, 3D 다리(Map3DOverlayBridge)도 점 아이콘으로 보낸다. 꼭짓점이 없으니 꼭짓점 손잡이를 줄 수 없다. 2D 어도너도 돌리기 손잡이를 그린다(빌보드 아님). (pattern: follow-recommended)
- effect: 선 · 구역 = `Map3DOverlay.IsLineKind`(pidsGroup · line · polygon)만. 바꾸려면 `MapViewModel.Overlay3D.Overlay3DHandleTarget` 의 rotatable 판정과 `Map3DEdit.ParseOverlayCommit` 의 IsLineKind 갈래.

## 2. 오버레이 이미지 마커(kind "image")에 3D 손잡이를 줄지
- options: (a) 고름 표시만 / (b) 점처럼 옮기기 손잡이
- chosen: (a)
- why: 이미지는 2D 에서도 모서리 · 자르기 등 전용 편집(이미지 편집 경로)이 따로 있고 목표는 「심볼」만이다. 2D 러버밴드도 이미지를 뺀다. (pattern: follow-recommended)
- effect: overlay.select 에는 들어가고 edit.start 는 없다. 3D 상자 고르기(overlay.box)도 이미지는 뺀다.

## 3. 2D 고름이 바뀔 때 overlay.select 를 바로 보낼지 모아서 보낼지
- options: (a) 바뀔 때마다 즉시 / (b) Dispatcher 한 박자(Normal) 모아 한 번
- chosen: (b)
- why: 클릭 한 번이 「모두 풀기(ClearAllSelections) → 새로 고르기」 두 단계라 즉시 보내면 빈 고름 · edit.stop · edit.start 가 섞여 깜빡인다. Background 우선순위는 바쁠 때 굶는다는 기존 교훈(camhost)이 있어 Normal. (pattern: speed)
- effect: `QueueMap3DSelectionSync`. 3D 진입 · 새 페이지(ready)는 바로 보낸다(`ResyncMap3DSelection`).

## 4. 3D 편집 모드에서 빈 곳 클릭 때 고름을 풀지
- options: (a) 예전처럼 강조 해제 · 카드 닫기만 / (b) 2D 빈 곳 클릭(OnMapClicked)처럼 고름도 풀기
- chosen: (b)
- why: 목표의 「2D 에서 고름이 바뀔 때(… 빈 곳 …)」와 같은 방식. 3D 에 고름 표시가 생겼으니 3D 안에서 풀 길이 필요하다. Ctrl 을 누른 빈 곳 클릭은 풀지 않는다. (pattern: broad)
- effect: `OnMap3DPick` 빈 곳 갈래의 `ClearAllSelections()` 한 줄.

## 5. overlay.box 의 add 값 해석
- options: (a) add 무시 · 늘 2D 러버밴드 규칙 / (b) add=true(또는 없음) = 2D 러버밴드 규칙(더하기 · 겹치면 빼기), add=false = 상자 안 것으로 바꾸기
- chosen: (b)
- why: 계약에 add 가 있으니 뜻을 살리고, 값이 없으면(옛 엔진) 2D 와 같게 둔다. 바꾸기는 2D 의 같은 처리기(ApplyGroupSelectionByIds)를 쓴다. (pattern: follow-recommended)
- effect: `OnMap3DOverlayBox`. 바꾸려면 add=false 갈래만.

## 6. 3D 에서 Esc 로 심볼 손잡이가 끝났을 때(edit.end)
- options: (a) 손잡이 기록만 지움 / (b) 2D Esc 처럼 고름도 풀기
- chosen: (b)
- why: 꾸미기 · 직접 그린 건물의 edit.end 도 판의 고름을 푼다(기존 규칙). edit.stop 은 되보내지 않는다(3D 가 이미 내렸다). (pattern: reuse)
- effect: `OnMap3DEditMessage` 의 edit.end 갈래 앞부분.

## 7. 키보드 폴백(방향키 · [ ])을 GIS 쪽에 더 만들지
- options: (a) 엔진 키보드(3D 화면을 누른 뒤 방향키 · [ ] · Enter → edit.commit) 그대로 / (b) WPF 단추 · 키를 새로 만들어 edit.key 로 넘기기
- chosen: (a)
- why: 목표가 「기존 edit.key 그대로 쓴다」. 심볼에는 꾸미기 판 같은 3D 위 판이 없어 단추를 만들면 새 UI 가 된다(범위 밖). 3D 에서 심볼을 누르면 키보드 포커스가 3D 화면에 있어 엔진이 키를 받는다.
- effect: GIS 코드 변경 없음(도움말에만 적음). 판 단추가 필요해지면 `Edit3DKeyCommand` 의 대상 판정에 `_overlayEdit3D` 를 더하면 된다.

## 8. edit.start target 의 rotatable 을 늘 넣을지
- options: (a) 돌릴 수 있을 때만 rotatable:true / (b) 늘 true/false 로
- chosen: (b)
- why: 엔진이 없는 필드와 false 를 같게 보면 되고, GIS 가 판정한 값을 명시하는 편이 시험 · 로그에서 읽기 쉽다. 선 · 구역은 false.
- effect: `Map3DEdit.OverlayEditStartJson`.

## 10. 3D 를 펼칠 때 고른 것이 없어도 빈 overlay.select 를 보낼지
- options: (a) 펼칠 때마다 지금 고름을 무조건 보냄 / (b) 새 페이지는 고름 없음으로 보고, 접어 둔 페이지는 마지막으로 보낸 것과 다를 때만 보냄
- chosen: (b)
- why: (a) 로 했더니 기존 건물 시험 3개(「고르기만으로는 3D 를 바꾸지 않는다」 = 보낸 메시지 없음)가 깨졌다 — 쓸데없는 메시지다. 접어 둔 페이지(1×1)는 고름 표시를 기억하므로 나가 있는 동안 2D 에서 바뀐 것만 보내면 된다. 손잡이는 2D 에서 모양이 바뀌었을 수 있어 펼칠 때 다시 세운다. (pattern: reuse — 기존 규칙 유지)
- effect: `ResyncMap3DSelection(newPage)` — ready = true(기록 비움), Reveal3D = false.

## 9. 저장 뒤 손잡이를 다시 세울지(edit.start 다시 보내기)
- options: (a) 다시 보내지 않음 — 엔진이 끈 손잡이를 그대로 / (b) 저장 · 되돌리기마다 edit.start 다시
- chosen: (a)
- why: 심볼 위치가 바뀌면 오버레이 다리가 patch 를 보내고(50 ms 모음), edit.start 를 먼저 보내면 엔진이 옛 자리로 손잡이를 세울 수 있다. 계약에 다시 세우기 규칙이 없다.
- effect: 되돌리기(Ctrl+Z) 뒤 엔진 손잡이 자리는 엔진이 patch 를 받아 맞춰야 한다 — 엔진 세션에 확인할 일(PLAN 남은 일).

## 11. 도형 투명도(opacity)를 아이콘 PNG 에 굽는지, 숫자로 따로 보내는지 (추가 지시 7)
- options: (a) 지금처럼 PNG 에 굽고 opacity 는 안 보냄 / (b) PNG 에도 굽고 opacity 도 보냄 / (c) PNG 는 바깥 투명도 없이 굽고 opacity 숫자로 보냄
- chosen: (c)
- why: 실측(`Map3DOverlayPixelTests.should_bake_root_opacity_by_default_but_not_when_opacity_goes_separately`) — VisualBrush 는 루트 Opacity 를 굽는다(0.5 → 알파 128). 엔진이 opacity 를 반영한다고 알려 왔으니 (b) 는 두 번 옅어진다. (c) 는 투명도만 바뀌면 아이콘을 다시 그리지 않아도 된다(IconSignature 에서 opacity 제외). (pattern: verify)
- effect: `RenderIconPng(..., withoutRootOpacity: marker is GMapGeometricMarker)` — 그리는 동안만 SetCurrentValue(1). 도형 심볼만 `opacity` 를 싣는다(다른 점 심볼은 투명도 값이 없다).

## 12. 새 이름표 · 모양 필드를 값이 기본일 때도 늘 실을지
- options: (a) 기본값이면 빼기 / (b) 늘 싣기(showShape true · italic false · font null · maxWidth · offset 0,0 포함)
- chosen: (b)
- why: 다리의 Diff 는 사라진 키를 보내지 못한다 — 빼는 방식이면 끔 → 켬 같은 변경이 patch 로 가지 않는다. 옛 엔진은 모르는 키를 무시한다. 단, 선 · 이미지의 label 은 DTO 에 Label 이 있을 때만(옛 호출 · 지도 CustomImages 는 예전 모양 그대로).
- effect: `Map3DOverlay.LabelJson`, 점 `showShape`. 도형 `opacity` 는 도형만이라 종류별로 늘 같다.

## 13. 선 · 구역 이름표의 지리 자리(label.lon/lat) 계산
- options: (a) 엔진이 anchor 로 계산 / (b) GIS 가 2D 규칙으로 계산해 함께 보냄
- chosen: (b) — 추가 지시대로. 기준점 = 마커 Position(2D LabelAdorner.IconCenter), 반폭 · 반높이 = 점 상자 절반(경도 · 위도), 자리 = (U · 반폭, 아래로 (1+V) · 반높이)
- why: 2D LabelAdorner.ComputeLabelCenterRel 과 같은 식. 2D 의 아이콘 간격 8 px 은 줌마다 거리가 달라 지리 좌표로 고정할 수 없어 뺐다(이름표 높이 정도 차이). 좁은 구역에서 경위도를 선형으로 본다(메르카토르 차이 무시).
- effect: `Map3DOverlay.LineLabelAt`. anchor{u,v} 도 함께 보내므로 엔진이 원하면 자기 방식으로 다시 셀 수 있다.

## 14. label.maxWidth 값
- options: (a) 저장값 그대로(0 = 미설정) / (b) 2D 가 실제로 쓰는 유효값(0 이하 → 200, 40~800 로 자름)
- chosen: (b)
- why: 3D 가 2D 와 같은 폭에서 말줄임해야 한다 — 2D 규칙(`LabelAdorner.EffectiveMaxWidth`)을 그대로 재사용. (pattern: reuse)
- effect: `Map3DOverlayBridge.StyleOfLabel`.

## 15. 오버레이 이미지 이름표 자리
- options: (a) label 만 / (b) label + anchor{u,v}(2D LabelOffsetU/V) / (c) + lon/lat 도
- chosen: (b)
- why: 지시는 label 싣기. 2D 이미지 이름표도 U/V 비율이라 같은 꼴로 실어 두면 엔진이 쓸 수 있다. 이미지는 회전 · 경계로 자리가 정해져 지리 좌표 계산은 범위 밖으로 남겼다. 지도 CustomImages(TIF 등)는 이름표가 없어 label 을 싣지 않는다.
- effect: `Map3DOverlayBridge.Build` 의 GMapImageMarker 갈래.

## 16. Shift+끌기 한 번에 building.box 와 overlay.box 가 함께 올 때(심볼 · 건물 고름이 섞일 때)
- options: (a) 각자 자기 고름에만 반영(서로 풀지 않음) / (b) 하나가 오면 다른 쪽 고름을 푼다
- chosen: (a)
- why: 2D 에서도 심볼 고름과 건물 판(「건물 높이」 · 여러 개 고르기)은 따로 산다. 한 번 끌어 둘 다 고르는 것이 사용자의 뜻이다. overlay.box 는 이 작업(심볼 고름), building.box 는 건물 작업(t-20261022-154533-1fef)의 처리기가 받는다. (pattern: follow-recommended)
- effect: `OnMap3DOverlayBox` 는 심볼 고름만 바꾼다. 건물 고름은 건드리지 않는다.

## 17. 엔진 알림의 building.select ids(Ctrl+클릭 확정) · building.none(하늘 클릭)
- options: (a) 이 작업에서 고침 / (b) 건물 작업 몫으로 둠
- chosen: (b)
- why: 목표가 이 작업은 심볼 쪽만이고 건물 작업의 파일(MapViewModel.BuildingHeight.cs · BuildingErase.cs)은 고치지 않는다고 정했다. 이 작업의 빈 곳 처리(pick 빈 곳 → 심볼 고름 풀기)는 building.none 과 무관하다.
- effect: 건물 작업이 엔진 알림대로 고름이 바뀔 때마다 building.select ids 를 돌려줘야 한다(PLAN 남은 일).

## 18. 3D 손잡이 저장 뒤 보내는 overlay.patch 의 내용
- options: (a) 바뀐 필드만(직전 기록과 비교) / (b) 지금 값 전체
- chosen: (b)
- why: 엔진 계약이 「저장 후 반드시 보낸다」 — 3D 가 끈 값과 같으면 비교 방식은 아무것도 보내지 않는다. 전체를 보내도 엔진은 합친다. 보낸 뒤 기록을 갱신해 곧 올 PropertyChanged 의 patch 는 중복되지 않는다.
- effect: `Map3DOverlayBridge.PatchNow` ← `MapViewModel.CommitOverlayEdit3D`.

## 19. 2D 에서 선 꼭짓점을 고쳤을 때 3D 에 다시 가는지 (추가 지시 8)
- options: (a) 고침 / (b) 이미 됨 — 시험만 더함
- chosen: (b)
- why: `UpdatePoint` → `NotifyPointsChanged`(PropertyChanged) → 다리 MarkDirty → Flush → pts patch. 고치기 전 코드로도 새 시험(`should_send_new_points_to_3d_when_line_vertex_is_edited_in_2d`)이 통과했다.
- effect: 코드 변경 없음.
