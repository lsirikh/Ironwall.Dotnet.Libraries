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
