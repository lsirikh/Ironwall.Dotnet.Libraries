# DECISIONS

## 1. 오프셋 방위 · 줌을 어디서 가져올까
- options: (a) 3D 뷰어가 overlay.screen 에 그 프레임 view(줌 · 방위)를 실음 · (b) GIS 만 고침 — camera 메시지 사이 방위를 보간 · (c) 움직이는 동안 오프셋 회전을 고정했다가 멈추면 맞춤 · (d) camera 메시지 스로틀을 없앰
- chosen: (a)
- why: 마커 자리와 오프셋이 정확히 같은 프레임 값이 되어 지연 0 프레임 · 왕복 0(하네스). (b)는 추정이라 감속 끝에서 넘치고, (c)는 멈출 때 한 번 크게 튀며, (d)는 camera 마다 2D 지도를 다시 맞추는 비용(Apply3DCameraTo2D)이 60 Hz 로 늘어남.
- effect: map-3d 에 map3d.patch 를 적용해야 효과. 옛 뷰어(view 없음)면 GIS 가 예전 방식(마지막 camera)으로 돌아감. 되돌리려면 `Map3DScreen.OffsetView3D` 에서 frame 을 무시하면 된다.

## 2. 3D 중 2D 통지(줌 · 위치 · 회전)로도 상자를 다시 놓을까
- options: 계속 놓음(값은 거의 같음) · view 를 받는 동안은 overlay.screen 만 놓음
- chosen: view 를 받는 동안은 overlay.screen 만(`Map3DScreen.PlaceOn2DChange`)
- why: 한 프레임 한 번 · 한 출처. 2D 통지는 camera 메시지마다 3번씩 와서 같은 프레임에 여러 번 자리를 정했다. 지켜볼 목록을 보내면 뷰어가 다시 그려 곧 overlay.screen 이 온다.
- effect: 3D 중 지도 크기만 바뀌고 3D 가 다시 안 그리면 상자가 다음 프레임까지 옛 자리 — 뷰어가 크기 변경 때 다시 그리므로 실사용 영향 없음. 되돌리려면 PlaceOn2DChange 가 항상 true.

## 3. 2D 정수 좌표 반올림 잔떨림(±1 px) 처리
- options: 상자마다 오프셋을 한 번만 재 둠(줌 0 · 방위 0 기준) · 위경도에서 직접 메르카토르 실수 오프셋을 셈
- chosen: 한 번만 재 둠(`BaseOffset` + 캐시, 앵커 · 마커 위경도가 바뀌면 다시)
- why: 기존 규칙(2D 화면 오프셋 → 3D 줌 · 방위)과 수식상 같아 회전 방향 · 디지털 줌 규약을 새로 가정하지 않음. 실수 메르카토르는 GMap 회전 · 디지털 줌 규약을 다시 맞춰야 함.
- effect: 3D 에 들어온 뒤 첫 배치 때의 2D 반올림 오차(≤ 약 1 px)가 3D 동안 고정. 2D 로 돌아가면 기존 2D 규칙이 그대로 놓음.

## 4. 끄는 중(드래그) 3D 관성이 이어질 때
- options: 끄는 중엔 따라가기를 멈춤 · 끄는 동안도 마커를 따라가며 끈 거리만 더함
- chosen: 끄는 중엔 그 상자만 따라가기를 멈춤(`IsWindowDragging`), 놓으면 기존 DragCompleted 가 3D 오프셋을 2D 로 되돌려 새 앵커 저장
- why: 마우스 아래 상자가 그대로 따라와야 자연스럽고, 기존 2D 끌기와 같은 느낌. 놓은 뒤부터는 새 앵커 관계로 마커를 따라감(하네스: 튐 ≤ 2.5 px).
- effect: VM 에 공개 속성 하나 추가(set 은 internal). 되돌리려면 TryPlaceCameraPopupsIn3D 의 건너뛰기 한 줄 삭제.

## 5. map-3d 복사본 위치
- options: 이 worktree 안에 clone · %TEMP% 에 clone
- chosen: `%TEMP%\map3d-t-20261019-162523-2e52` 에 git clone, 고친 파일은 worktree 안 임시 폴더에서 편집해 복사(편집 도구가 worktree 밖에 못 씀), 끝나고 임시 폴더는 지움
- why: worktree 안 clone 은 하네스 커밋에 중첩 저장소로 섞임. 원본 map-3d 는 손대지 않음.
- effect: %TEMP% 복사본은 남아 있음(바깥 삭제 금지). 패치는 `.harness/t-20261019-162523-2e52/map3d.patch`.
