# PLAN — 가짜 ONVIF 카메라: 기종별 프리셋 토큰 모양 재현

## 목표
tools/dummy-cameras 가짜 카메라 팜이 숫자 토큰("1".."5")만 내놓아, GIS 프리셋 관리 창의
[카메라에서 읽기] · [서버에 담기] · 불일치 경고(번호가 아닌 토큰 = 등록 불가) 경로를 시험할 수 없다.
→ 카메라마다 프리셋 토큰 모양을 고를 수 있게 한다. (실제 카메라 시험 · 실제 계정은 범위 밖 — 코드/파일에 넣지 않는다.)

## 결정
- `PresetTokenStyle` 4종: `numeric`(1..5, 기본·기존 동작 그대로) · `named`(Preset001..Preset005) ·
  `padded`(001..005, 앞자리 0) · `mixed`(1, 2, Preset003, 004, 5 — 등록 가능/불가 섞임).
- 토큰 매칭은 엄격: named/padded 카메라에 GotoPreset("3") 은 실패(ter:NoToken). 실제 카메라와 같아야
  "번호가 아닌 토큰은 서버 preset_index 로 못 담는다"는 GIS 판단을 검증할 수 있다. 기존 "P2"→"2" 별칭은 유지.
- SetPreset 의 새 토큰도 스타일을 따른다(numeric·mixed=숫자, named=PresetNNN, padded=NNN).
- CLI: `--preset-tokens 2=named,3=padded,4=mixed` 또는 `--preset-tokens named`(전 PTZ). 고정 카메라 지정은 오류.
- 제어: `POST /cam/{n}/ptz/presets?style=named` — 프리셋 목록을 그 스타일 기본값으로 갈아끼움(실행 중 전환). `/status` 에 presetTokens 표시.
- Seeder(서버 P1..P5 등록)는 그대로 둔다 — non-numeric 카메라에선 오히려 불일치 경고 시험이 된다.
- dummy-cameras 시험 프로젝트는 솔루션 밖(루트 dotnet test 에 안 잡힘) → 직접 `dotnet test tools/dummy-cameras/tests` 로 확인.

## 진행
- [x] 현황 조사(PtzSimulator · OnvifResponder · Farm · Cli · ControlServer · MappingCameraPreset.Index 규칙)
- [x] PtzSimulator 스타일 지원(PresetTokenStyle · PresetTokens) + ResetPresets
- [x] FarmOptions.PresetTokenStyles / Farm(비 PTZ 지정 거부 · Describe) / Cli --preset-tokens / ControlServer / Overlay 배선
- [x] 시험 tests/PresetTokenStyleTests.cs: 시뮬레이터 단위 + CLI + 우리 ONVIF 클라이언트(GetPresets/GotoPreset) + 제어 엔드포인트 + MappingCameraPreset.Index 분류
- [x] dummy-cameras 시험(프리셋·PTZ·Generator·OnvifClient·OnvifAuth·Seed 66건) 통과 · 솔루션 dotnet test 통과
- 미실행: Live/EventWindow/DragPtzHost 시험(ffmpeg · 호스트 exe · 창) — 이번 변경과 무관, 사용자 데스크톱 방해 방지로 생략

## 남은 일(범위 밖 · 후속 후보)
- 실제 GIS 프리셋 관리 창에서 `--preset-tokens 2=named,3=padded,4=mixed` 팜으로 [카메라에서 읽기]·경고 헤디드 확인(대화형 세션).

## 발견
- MappingCameraPreset.Index: 1 이상 정수이고 정규형("007" 아님)일 때만 번호 → padded 는 전부 등록 불가로 분류된다.
