# PLAN — (질문) API 프리셋 정보를 ONVIF 설정에 활용하나?

## 목표
사용자 질문에 답한다: "API 의 프리셋 정보를 활용하긴 한 건가? ONVIF 설정 시 거기서 가져와야 될 것 같은데?"
명세는 질문 + 조사 결과뿐이므로 **답변(조사·근거·제안)이 산출물**. 코드 변경은 하지 않는다.

## 결정
- 코드 수정 안 함. 이유: 질문 형식이고, 프리셋 출처를 바꾸는 일은 서버 계약(토큰 칸 유무)과 엮인 설계 결정 →
  사용자 규칙상 PRD → 검토 → Plan 이 먼저(feedback_prd_before_code). 범위 확장 금지 지시도 있음.

## 확인 결과 (코드 근거)
- [x] 프리셋 저장소가 세 갈래로 서로 연결되지 않음
  1. 서버 API 프리셋 `/devices/cameras/{id}/presets` — 맵핑 워크벤치 · 프리셋 관리 창(MappingPresetManagerViewModel.cs:20-25)만 사용.
     ONVIF SetPreset 은 하지 않는다고 주석에 명시.
  2. 카메라 ONVIF 프리셋(GetPresets) — 지도 더블클릭 팝업의 프리셋 탭(MapViewModel.cs:2305-2341)만 사용. 서버 프리셋은 안 봄.
  3. 로컬 DB(PtzPresetStore, CameraPtzPresets) — 팝업에서 분리됨, 롤백 대비 코드만 남음(MapViewModel.cs:2298).
- [x] 이벤트 PTZ 이동은 서버 preset_index 를 숫자 문자열로 바꿔 ONVIF GotoPreset 토큰으로 사용(EventWindowPlanning.cs:84-90).
  주석에도 "ONVIF 토큰 = 번호인 카메라 기준 — 실기 확인 필요". 서버 DTO 에 토큰 칸 없음.
- 즉 "API 프리셋 정보를 ONVIF 에 쓰긴 쓴다" — 단 번호만, 번호=토큰이라는 가정 위에서. 카메라와 대조 · 동기화는 없음.

## 제안(사용자 결정 대기)
- A안(권장): 프리셋 관리 창에서 카메라 ONVIF GetPresets 목록을 함께 보여 주고, 서버 프리셋을 카메라 프리셋에서 골라 등록
  (preset_index ← 토큰이 숫자면 그 값, 이름 ← ONVIF Name). 이벤트 시 토큰 불일치를 경고. 서버 변경 불필요.
- B안: 서버 CameraPresetDto 에 `onvif_token` 칸 추가 요청(coordinations md) → GotoPreset 은 그 토큰 사용, 없으면 번호 폴백.
- 팝업 프리셋 탭도 서버 프리셋 이름을 겹쳐 보여 주면 두 화면이 같은 이름을 쓰게 됨.

## 남은 일
- 사용자가 A/B 중 고르면 PRD(docs/prds/) 작성부터.
