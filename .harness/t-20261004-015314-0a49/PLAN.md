# PLAN — API 프리셋과 카메라 ONVIF 프리셋 연결

## 목표
사용자: "API 의 프리셋 정보를 활용하긴 한 건가? ONVIF 설정 시 거기서 가져와야 될 것 같은데?"
→ 서버 프리셋(preset_index)이 카메라 ONVIF 프리셋 토큰과 맞도록, 프리셋 관리 창이 카메라 ONVIF 프리셋을 읽어
  그것에서 골라 서버 프리셋을 만들고, 어긋난 번호를 경고하게 한다.

## 조사 결과 (근거)
- 프리셋 세 갈래가 따로 놀았다: 서버 API(워크벤치 · 프리셋 관리 창) / 카메라 ONVIF GetPresets(지도 더블클릭 팝업, MapViewModel.cs:2305) /
  로컬 DB(PtzPresetStore, 팝업에서 분리 · 롤백용 잔존).
- 이벤트 PTZ 는 서버 preset_index 를 문자열로 바꿔 ONVIF GotoPreset 토큰으로 보낸다(EventWindowPlanning.cs:84-90). 서버 DTO 에 토큰 칸 없음.

## 결정
- 1차 턴에서는 PRD 규칙 때문에 답변만 했으나, 하네스가 "작업하라" 고 재지시 → 사람 없음, **A안(서버 변경 없이 GIS 쪽에서 맞추기)** 으로 구현.
  B안(서버 CameraPresetDto 에 onvif_token 칸)은 서버 계약 변경이라 범위 밖 — 남은 일에 기록.
- 이벤트 경로(preset_index → 토큰)는 그대로 둔다. 대신 서버 프리셋을 **카메라 토큰에서 만들게** 해 가정을 참으로 만든다.
- 토큰이 "1 이상의 정수(앞자리 0 없음)" 일 때만 번호로 쓸 수 있다(그래야 index.ToString() == token). 아니면 등록 거부 + 경고.
- 같은 번호의 서버 프리셋이 이미 있으면 새로 만들지 않고 그것을 고른다(서버 409/중복 방지).
- 카메라를 못 읽으면 불일치 판정을 하지 않는다(못 읽음 ≠ 없음).
- 호스트 요청은 이벤트 창과 같은 제공자 빌더(EventWindowPlanning.BuildProvider) 사용 — GIS 는 ONVIF 를 직접 부르지 않음(§0, 팝업 호스트 격리 원칙).

## 진행
- [x] 계약: MappingCameraPreset(Token, Name, Index) · MappingCameraPresetRow · IMappingCameraPresetReader (MappingContracts.cs)
- [x] 구현: HostMappingCameraPresetReader — 카메라 팝업 호스트 GetPresets, 절대 던지지 않음, 비PTZ/미등록/호스트 없음/시간초과 사유
- [x] 프리셋 관리 창 VM: 열 때 카메라 프리셋 자동 읽기, [카메라에서 읽기], [서버에 담기](UseCameraPreset), 불일치 경고(CameraMismatchText)
- [x] 뷰: 창 아래 "카메라 프리셋 (ONVIF)" 칸(리더 없으면 감춤), 창 높이 560→760
- [x] 배선: 워크벤치 CameraPresetReader → 관리 창, 런처 생성자 optional 인자, EventUiModule DI 등록(FromHost)
- [x] 시험: Tests/MappingCameraPresetTests.cs (토큰→번호 8 · VM 9 · 리더 4). Mapping|EventWindow 필터 277 통과
- [x] 도움말("?") 문구에 카메라 프리셋 · 불일치 경고 설명 2줄 추가(EventsHelp.cs) — 이 변경 뒤 재빌드는 못 함
- [ ] `dotnet test Ironwall.Dotnet.Libraries.sln` — 백그라운드로 돌렸으나 결과 대기 수단이 권한 거부, 하네스 검사에 맡김
      (Events.Ui 는 이 sln 에 없음 — 별도 실행 277 통과로 검증)

## 남은 일 / 후속 제안
- 실기(실제 카메라) 확인: 토큰이 숫자가 아닌 카메라 기종 목록.
- B안: 서버에 onvif_token 칸 요청(coordinations md) — 숫자가 아닌 토큰 카메라 지원에 필요.
- 지도 팝업 프리셋 탭에 서버 프리셋 이름 겹쳐 보이기(선택).
