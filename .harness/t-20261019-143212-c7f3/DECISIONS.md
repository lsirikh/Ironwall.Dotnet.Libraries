# DECISIONS

## 1. 지도 컨트롤 안의 기울기 판정 · 렌더 엔진을 지울지 남길지
- options: (A) 엔진(IsTiltFeatureEnabled · RequestedTiltDeg DP, TiltMath 판정, 코얼레서, φ_layout 정착기, Tier 프로브, 뷰 변환 합성)까지 전부 삭제 / (B) 엔진은 남기고 사용자 길 · VM · 키 · 진입 메서드만 삭제, 기본 OFF 라 φ 항상 0
- chosen: B
- why: TiltDeg 는 스냅 격자 게이트 · 뷰포트 스냅샷 TiltCos · 카메라 팝업 InnerToOuter · 드로잉 HUD · 보이는 범위 계산에 얽혀 있어 지우면 회귀 위험이 크다. 명세가 "얽힌 엔진은 항상 0 으로 남기는 것 허용". 운영 코드 어디에서도 IsTiltFeatureEnabled 를 켜지 않으므로 φ=0 이 보장된다(시험으로 단언).
- effect: 엔진 단위 시험(MapTiltWiringTests · TiltMath/Coalescer/Settler/OverscanMath 시험 · DrawingHud · ComponentCardZoomTilt)은 엔진이 남아 있어 그대로 둔다. 완전 삭제는 나중에 이 파일들(GMapCustomControl.Tilt.cs · Utils/TiltMath.cs · TiltGateCoalescer · TiltLayoutSettler · RenderTierProbe)과 그 시험을 함께 지우면 된다.

## 2. 저장 설정 모델(MapTiltModel · IGMapSetupModel.MapTilt · 메인 앱 SetupModel.MapTilt)
- options: 속성 · 모델 삭제(설정 스키마 변경, 메인 앱 · 설치 템플릿까지 수정) / 그대로 두고 읽지도 쓰지도 않음
- chosen: 그대로 둠(읽기 · 쓰기 경로만 삭제 — LoadMapTiltFromSettings · SaveMapTiltAsync)
- why: 명세 "옛 키는 남아 있어도 해롭지 않게". MapTiltModel 은 엔진 TiltSettings 형식이라 남아야 하고, 속성을 지워도 사용자에게 보이는 차이가 없다. 저장값 IsEnabled=true · 30° 여도 지도 전환(ConfigureCommonMapSettings) 뒤 평면이고 파일의 옛 키가 그대로임을 시험으로 단언.
- effect: appsettings 의 "MapTilt" 절은 무시된다. 나중에 지우려면 IGMapSetupModel/GMapSetupModel/SetupModel 의 MapTilt 와 Installer/appsettings.template.json 의 절을 함께 지우면 된다.

## 3. 툴바 "?" 도움말 키 이름 Map.Toolbar.RotateTilt
- options: 「Map.Toolbar.Rotate」 로 이름 변경 / 키 유지하고 내용만 회전으로
- chosen: 키 유지, 제목 「지도 회전」 · 기울이기 절 삭제
- why: 키가 UIA id(Help.Map.Toolbar.RotateTilt)로 메인 앱 시험 · 실기 UI 시험 · 매뉴얼에서 쓰인다. 이름만 바꾸면 그쪽이 함께 깨진다.
- effect: 이름을 바꾸려면 MapsHelp · MapView.xaml HelpKey · 메인 앱 MapToolbarScenarioTests(should_attach_a_help_bubble...) · UiTests MapComponentR24 를 함께 고친다.

## 4. 오버스캔 순수 수식(TiltOverscanMath) 의 '컨버터 입력 해석' 함수
- options: 컨버터와 함께 삭제(+ TiltOverscanMathTests 일부 삭제) / 남김
- chosen: 남김(주석만 갱신)
- why: 순수 수식 파일로 InnerToOuter/OuterToInner 등 다른 함수가 계속 쓰이고, 해당 시험은 순수 함수 검증이라 지우면 "시험은 지우지 말 것" 과 충돌한다. 해가 없다.
- effect: HeightOrAuto · MarginOrZero · ParseTarget · TryReadDouble · TiltOverscanTarget 은 운영 코드가 쓰지 않는다. 지우려면 그 시험 몇 개와 함께 지운다.

## 5. 완료 판정 프로젝트의 기울이기와 무관한 기존 실패 2종
- options: 보고만(BLOCKED) / 시험 리그를 현재 제품 구조에 맞게 고침 / 제품 코드에 지연 초기화 추가
- chosen: 시험 리그만 고침 — MapAux DragHeader 가 공용 OverlayHeaderDrag 의 상태를 찾아 주입, MapNav RunMapSwitch 가 생성자 없이 만든 VM 에 _map3DScenes · _map3DAreaMarkers 를 채움(같은 줄의 _mapSwitchLock 선례)
- why: 명세가 그 프로젝트들의 직접 통과를 완료 조건으로 둔다. 제품 동작은 맞고(생성자가 필드를 채움) 시험 장치만 뒤처졌다 — 범위를 넓히지 않는 최소 수정.
- effect: 제품 쪽 지연 초기화로 바꾸고 싶으면 MapViewModel.Map3D*.cs 의 readonly 필드를 ??= 접근자로 바꾸면 시험의 SetField 두 줄을 지울 수 있다.

## 6. 메인 앱 UiTests 의 기울이기 실기 진단(TiltRuntimeDiagTests 423줄)
- options: 파일 삭제 / 그대로 둠(토글 없어 ✖) / "제거 확인" 실기 시험으로 축소
- chosen: 축소 — 화면에 GMaps.Tilt.* 없음 · 줌 18.5 에서 Ctrl+Shift+T · Ctrl+↑ 뒤 배지 없음 · 회전 토글 보임. 순수 판정 함수(ParseBadgeAngle 등)는 MapTiltGuardTests 가 쓰므로 유지. EnvironmentGuard 의 MapTilt 강제 OFF 는 해가 없어 그대로.
- why: 명세 "기울이기 전용 시험은 지우지 말고 제거됨을 단언" · "UiTests 도 같이 정리". 실기 시험은 이 세션에서 돌릴 수 없어 컴파일만 확인.
- effect: 실기 회차에서 TiltRuntimeDiagTests 를 한 번 돌려 확인 필요.
