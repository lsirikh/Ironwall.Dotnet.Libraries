# PLAN — 끌기 PTZ 이동량 "가로 1/2 = 한 폭" · 타일 휠 줌 · PTZ 속도 · 이동 중 표시

## 목표(요약)
1. 끌기 이동량 기준 통일: m = (dx, dy) ÷ (영상 가로/2), |m| ≤ 1 로 자름. m = 1 → 수평 화각 1개(화면 한 폭) 회전. 세로도 같은 각/픽셀.
2. 이벤트 창 타일 휠 줌(지도 팝업과 같은 규칙).
2-1. PTZ 속도: 팝업 설정 속도 우선, 없으면 카메라 기본 속도(ONVIF), 카메라 범위로 자름.
2-2. 이동 중 표시(오른쪽 아래 작은 회전 아이콘 → '이동 완료' → 사라짐), GetStatus MoveStatus 로 완료 판정.
3. 헤드리스 시험(PTZ-DRAG-1nn).

## 진행
- [x] 1 수학: `PtzDragGesture.WidthVector` · `DragMagnitude` · `AmountText` · `TargetPoint`(Contracts) / `PtzDragMath`(Providers) 네 방식 모두 새 기준
- [x] 1 제어기: 화각 상대 공간 범위를 넘으면 같은 몫으로 나눠 보냄(`FovSteps` · `PtzDragPlan.Steps` · `FollowFovStepsAsync`)
- [x] 1 목표 표시: 과녁 = 중심 + m×가로(밖이면 그 방향 가장자리) + 끈 크기 글자("0.6폭" · "1폭 · 최대") — 두 화면
- [x] 2 타일 휠 줌(`TileViewModel.PtzWheelZoomAsync` · `TileView.OnTileMouseWheel`, 펄스 길이 `WheelZoomPulse` 시험에서 바꿈)
- [x] 2-1 속도(아래 결정)
- [x] 2-2 이동 중 표시: `PtzMotionTracker`(Contracts) · 타일 `PtzMotionChip` · 지도 팝업 `GMaps.CameraPopup.PtzMoving` · 요청 `GetPtzMoveStatus`
- [x] 더미 카메라: PtzSimulator 가 이미 GetStatus MoveStatus(PanTilt/Zoom MOVING·IDLE)를 주고 RelativeMove 도 --goto-seconds 동안 이동 → 변경 없음
- [x] 3 시험: CameraPopup.Tests `PtzDragWidthBasisTests`(101~112) · `PtzMotionTrackerTests`(120~123) · `PtzDragWidthProviderTests`(130~133),
      Host.Tests `TilePtzInteractionTests`(140~145), GMaps.Ui `CameraPopupPtzSpeedMotionTests`(150~152). 기존 기대값 갱신(아래)
- [x] sln 전체 시험(소리 · 원격 DB 시험 제외 필터) 0 실패 · GMaps.Ui 686/686 통과

## 결정
- **기준 단일 지점**: `PtzDragGesture.WidthVector`(Contracts) 하나가 m 을 만들고 Providers 수학 · 두 화면 과녁 · 글자가 모두 이것을 쓴다.
- **화각 상대(RelativeFov)**: ±1 = 반 화각 → 한 폭 = 2, 틸트 = −2·my·가로세로비. 범위(보통 ±1)를 넘으면 **같은 몫으로 나눠** 보낸다(최대 8번).
  근거: 한 번에 자르면 반 폭밖에 못 가고, 움직이는 중에 다음 RelativeMove 를 보내면 카메라가 앞 이동을 버리고 그 자리 기준으로 다시 잰다.
  그래서 2번째 몫부터는 앞 이동이 멈춘 뒤(GetStatus MoveStatus IDLE, 200 ms 간격 · 4초 상한, MoveStatus 없으면 900 ms 어림) 보낸다.
  나머지 몫은 배경에서 돈다(요청 제한 시간과 무관) — 새 드래그 · 정지 · 누름 이동 · 프리셋이 `CancelDrag` 로 끊는다(시험 PTZ-DRAG-131).
  다른 방식으로 넘어가지 않은 까닭: 화각 공간은 줌을 카메라가 스스로 반영하는 가장 정확한 방식이라 우선순위를 유지.
- **일반 상대 · 절대**: 도 = m × 지금 수평 화각(세로도 수평 화각 기준). 상대 범위 자르기 · 구동 범위 자르기는 그대로.
- **시간 제한 연속**: 속도 = 설정 속도(크기와 무관), 시간 = |m| × 700 ms(최소 120). 화각을 모르므로 "한 폭"은 어림 — 예전 '짧은 변 65%' 기준을 없앴다.
- **과녁 자르기**: 예전처럼 축마다 따로 자르지 않고 중심→목표 방향을 유지해 가장자리에 붙인다(방향을 알 수 있게). 크기는 글자로.
- **속도 우선순위(2-1)**: 팝업 PTZ 탭 슬라이더(사용자가 만졌을 때) → 카메라 기본 속도 → 0.6(기존 기본값).
  - 카메라 기본 = 워밍 때 받은 프로파일의 `PTZConfiguration.DefaultPTZSpeed`(GetConfiguration 과 같은 값, 추가 ONVIF 호출 없음) ÷
    노드 `PanTiltSpeedSpace`/`ZoomSpeedSpace` 범위(GetConfigurationOptions 속도 범위와 같은 값) → 0.1..1(`PtzController.NormalizeSpeed`).
  - GIS: PreparePtz 응답 `CameraResponse.DefaultPanTiltSpeed/DefaultZoomSpeed` → `CameraStreamPopupViewModel.ApplyCameraDefaultSpeeds`(사용자가 정한 축은 안 건드림).
  - 끌기: `PtzCommand.Speed`(0 = 카메라 기본)로 호스트에 실어 RelativeMove/AbsoluteMove 의 `Speed`(속도 공간으로 펼침) · 시간 제한 연속 속도에 쓴다.
    둘 다 없으면 RelativeMove 는 Speed 를 생략(카메라 기본).
  - 패드 · 휠: 지도 팝업은 원래 vm.PanTiltSpeed/ZoomSpeed 사용. 이벤트 창 타일은 설정 화면이 없어 카메라 기본 속도 → 없으면 0.5(기존 하드코딩 값).
  - 연속 이동 속도는 기존대로 카메라 연속 속도 범위로 스케일(`PtzVelocityMath.ScaleToRange`).
- **이동 중 표시(2-2)**: `PtzMotionTracker` — 이동 명령을 보낸 뒤 250 ms 간격으로 MoveStatus 를 묻고, IDLE → '이동 완료' 1.2초 → 사라짐.
  막 보낸 이동이 아직 IDLE 일 수 있어 MOVING 을 못 봤으면 0.5초 전에는 완료로 치지 않음. MoveStatus 를 안 주면 예상 시간
  (끌기 1.5초 · 휠 0.6초 — 반 화면 RelativeMove ≈ 1초 · 250 ms 펄스+정지)으로 어림. 15초 상한. 새 이동은 앞 판정을 버리고 깜박임 없이 이어감.
  묻기는 이동 중에만(끝나면 멈춤 — 시험 PTZ-DRAG-120/152). 대상 = 끌기 · 휠(이번 작업의 제스처). 패드 누름은 누르는 동안 이미 움직임이 보여 제외.
  GIS 는 새 요청 `CameraRequestKind.GetPtzMoveStatus`(→ `CameraResponse.PtzMoving`)로 호스트에 묻는다(호스트 분리 유지, GIS 는 ONVIF 를 안 부름).
- **디자인**: 영상 위 칩 = 형제 배지와 같은 반투명 바탕 + 밝은 글자(지도 팝업: `#AA000000`/흰 글자 — 기존 PTZ 배지와 동일, 타일: `CameraPopup.VideoChip`/`VideoText` 토큰).
  10 px 원호 회전 아이콘 + 10 pt 글자(작게). 자동화 id: 타일 `{타일}.PtzMoving`, 지도 팝업 `GMaps.CameraPopup.PtzMoving`, 끈 크기 `{타일}.DragAmount` · `CameraPopup.PtzDragAmount`.
- **공개 형식은 더하기만**: 새 오버로드 · 기본 구현 인터페이스 멤버 · 선택 인자 · 끝에 붙인 enum 값/속성. 프로토콜 판(3)은 그대로(새 필드 · 요청은 옛 쪽이 보내지 않음).

## 기존 시험 기대값 갱신(사용자 결정 "가로 1/2 = 화면 한 폭" — 약화 아님, 같은 성질을 새 크기로 확인)
- `PtzDragTests`: 과녁 자리(중심+2×벡터, 방향 유지 가장자리) · 화각 공간 가장자리 = 가로 1/4(예전 1/2) · 일반 공간 가로 1/2 = 60°(예전 30°) · 절대 계획 60°.
- `PtzDragProviderTests`: 가로 1/4 = 30°, 세로 같은 각도/픽셀 식 · 화각 공간 시험은 한 번에 가는 크기(1/8)로(두 번 나눠 가는 경우는 새 시험 130) ·
  절대 시험은 가로 1/4(같은 30° 몫 유지 — 1/2 이면 팬 끝을 넘어 감겨 다른 성질을 재게 됨) · 몰림 시험 기대 이동량 ×2.

## 발견
- 팝업 속도(PanTiltSpeed/ZoomSpeed)는 `CameraStreamPopupViewModel` 메모리 값(기본 0.6, PTZ 탭 슬라이더). appsettings 키 없음.
- 끌기 펄스는 `PtzDragMath.PulseSpeed`(0.6) 하드코딩이었고, 타일 패드는 0.5 하드코딩, 지도 휠은 vm.ZoomSpeed.
- 더미 카메라 `OnvifXml` 프로파일에 DefaultPTZSpeed(1,1) · 속도 공간 0..1 이 있다 → 실창 시험에서 카메라 기본 속도 1.0 이 슬라이더에 들어간다.
- 시험용 FakeOnvifCamera 프로파일에는 PTZConfiguration 이 없다 → 기본 속도 null → Speed 생략(기존 시험 본문 그대로).

## 남은 일
- 실창 확인(대화형 세션): 지도 팝업 · 이벤트 창 타일에서 가로 1/2 끌기 = 한 폭 회전, 과녁/글자, 오른쪽 아래 이동 중 → 이동 완료, 타일 휠 줌.
