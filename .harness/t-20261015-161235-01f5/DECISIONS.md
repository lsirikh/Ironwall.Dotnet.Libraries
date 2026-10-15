# DECISIONS — G7-4 제어 명령 requested_by · requested_client_id

## 1. 기존 requested_by(회전요청 · 문 개폐 · 카메라 팝업)의 값을 표시이름에서 login_id 로 바꿀지
- options: (a) 표시이름(user.name) 우선 유지 (b) login_id 로 통일
- chosen: (b) login_id → JWT sub(=login_id) 폴백
- why: 목표가 "운영자 계정(login_id) · 이미 있는 명령도 같은 값으로 맞춘다"로 명시.
- effect: MapEdit 문 개폐 시나리오 시험 기대값을 "시험관리자" → login_id 로 갱신. 되돌리려면 `MapViewModel.CurrentRequestedBy()` 한 곳만 고치면 된다.

## 2. 미로그인일 때 requested_by 를 어떻게 할지
- options: (a) Windows 사용자명 폴백 유지 (b) 싣지 않음(null)
- chosen: (b) 단, PTZ_AIM_LOCATION 은 기존 계약대로 빈 문자열 유지
- why: 선택 칸이며 Windows 사용자명은 운영자 계정이 아니다. PTZ_AIM_LOCATION 빈 문자열은 기존 시험이 고정한 계약.
- effect: 미로그인 명령에 requested_by 가 빠진다. `ControlCommandRequester.LoginId` 에서 폴백을 추가하면 되돌릴 수 있다.

## 3. 공통 칸을 어떻게 붙일지
- options: (a) DTO 마다 두 속성 복붙 (b) 추상 기반 `ControlCommandBodyDto` 상속
- chosen: (b), requested_by 를 이미 가진 DoorSetBodyDto · CameraAimLocationBodyDto 는 requested_client_id 만 직접 추가
- why: 22종에 같은 정의 — 기반 클래스 속성은 Newtonsoft 가 뒤에 직렬화하므로 기존 키 순서 불변.
- effect: 기반 클래스 하나로 일괄 관리.

## 4. 카메라 팝업(CAMERA_POPUP_OPEN)에 requested_client_id 를 실을지
- options: (a) 추가 (b) requested_by 값만 맞추고 추가 안 함
- chosen: (b)
- why: 목표의 명령 목록에서 팝업은 "requested_by 정렬" 대상으로만 나오고, 같은 단말 값이 이미 target_client_id 로 실린다.
- effect: 필요하면 팝업 DTO 에 같은 속성 한 줄 추가.

## 5. requested_by 소스를 서비스에 어떻게 주입할지
- options: (a) 필수 생성자 인자 (b) 선택 인자 `IPermissionService? permission = null`
- chosen: (b)
- why: 기존 생성 지점 · 시험을 깨지 않고 Autofac 이 등록돼 있으면 채운다.
- effect: 미등록 환경에선 requested_by 가 빠진다.
