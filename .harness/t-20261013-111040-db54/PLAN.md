# PLAN — 센서 설치 위치 · 속성 프리셋 (t-20261013-111040-db54)

## 목표
스토리보드/와이어프레임(2026-10-12 초안) + 사용자 스케치 2장(담장 한 칸 붙는 점 · 윤형 철조망)의
센서 설치 위치 · 속성 프리셋 기능을 PRD(`docs/prds/sensor-mount-preset-prd.md`) → 구현 → 헤드리스 시험까지.

## 결정(권장안 확정 — 사용자 지시)
1. 표 = `gmap_tiles_db` 의 SensorModels · SensorMountPresets · SensorAttributePresets. 레이아웃 JSON 센서 자리에는
   `model_id · mount_preset_id · mount_override · attr_preset_id` 만 더한다(기존 panel/spot/height_offset 유지 → 이관은 저장 때 가장 가까운 프리셋 + 차이).
2. 속성 프리셋은 GIS 안에서만(그림 · 알림) — device_config 미사용.
3. 프리셋 수정 = 연결 + 저장 전 W5 확인(기본 [취소]) + [새 프리셋으로 저장].
4. 회사 기본 7종(삭제 불가 · 숨기기 가능) + 사용자 추가.
5. 여러 PC = JSON 내보내기/가져오기. `GopApiSensorPresetRepository` 는 NotSupported 자리만.

## 설계 결정(이 세션)
- 공개 형식은 더하기만: `SensorMountSpec` init 속성 4개 추가 · `FenceWorld.FromLayout` 오버로드 · `FenceSensor.ModelShape` init 속성 · `WiringLauncher` 생성자 오버로드(Autofac 은 인자 많은 쪽을 고름).
- 순수 모델 · 계산 = `Ironwall.Dotnet.Monitoring.Models/Fences/Presets/`(sln 안 시험 프로젝트이기도 해서 순수 시험을 여기 둠).
- GIS DB 구현 = `GMaps.Db/Services/GisDbSensorPresetRepository.cs`(부팅 경로 밖 · 첫 사용 때 표 생성 + 기본 7종 INSERT IGNORE) · `GMapDbModule` 등록.
- 화면 = `Devices.Ui/Consoles/SensorPresets/`(W1 SensorModels · W2 MountPresetEditor + MountBayCanvas · W3 = W1 속성 탭 · W5 PresetSaveConfirm · W4 적용 창) + 결선 `WiringViewModel.Presets.cs` · `WiringView.xaml` "설치 위치" 절.
- 붙는 점 규격(스케치): 기둥 A/B 각 4점(꼭대기 H+0.2 · 레일 H · 중간 0.75H · 아래 0.5H) + 메쉬 가운데 3점(레일 · 0.75H · 0.5H) + 철조망 칸만 코일 위 25/50/75% · 코일 가운데 · 끝 고리 2개 + 바닥 밖/안. 기둥 꼭대기 +0.2 m 는 스토리보드 "기둥 상단 2.6 m"(펜스 2.4)에 맞춤.
- 이관은 열 때가 아니라 **저장할 때**(`MigrateMountForSave`) — 열자마자 "바뀜" 이 뜨지 않게, 자세는 같게(기둥 A/B 쪽은 가로 따로 고친 값으로 보정).
- W5 "따로 고친 값은 그대로 남습니다." 는 틀의 안내 줄(ConsoleDialogFrame.Message)에 — 설명형 문구 계약(HelpContractTests) 준수.

## 진행
- [x] PRD 작성(결과표 §10 포함)
- [x] 순수 모델(모델 · 프리셋 · 붙는 점 · 자리 계산 · 이관 · 쓰임 · 내보내기/가져오기 · 기본 7종)
- [x] 저장소 인터페이스 + 메모리 구현 + GopApi 자리 + GIS DB 구현 + 모듈 등록
- [x] W1 센서 모델 창 VM/View (+ W3 속성 탭)
- [x] W2 위치 프리셋 편집기 VM(끌기 · 키보드 · Esc · Alt · 방향/각도 손잡이) + 세 방향 + 3D 미리보기 그림 컨트롤
- [x] W5 저장 확인 VM/View
- [x] W4 결선맵 설치 위치 절 · 적용 창 · FenceWorld 자리 계산 · 모델 모양(PIR · 광케이블 · 스마트2)
- [x] 시험 MNT-001~035(Models, 23건) · MNT-101~141(Devices.Ui, 22건) 통과 · sln 전체 실패 0 · Devices.Ui 결선/펜스/도움말 1138건 통과
- [ ] 남은 일(아래)

## 발견
- Devices.Ui 는 sln 밖 + 프로젝트 자체가 xunit 시험 프로젝트. Monitoring.Models 도 시험 프로젝트(sln 안).
- 결선 펜스 보기는 Viewport3D 가 아닌 2.5D 투영(FenceWorld/FenceScene) — "입체" 도 이 투영.
- Utils.Tests 계약: RadioButton 등 버튼 계열은 Style 필수(Console.RadioButton) · `*Note/*Hint` 바인딩은 설명형 문구로 걸린다.
- Caliburn `GetValueOrDefault` 확장이 BCL 것과 모호 — TryGetValue 도우미로.
- 이 세션에서 sed · 변수 확장 쓰는 bash 는 거절됨(Edit 로 대신).

## 남은 일
1. 탐지 범위 그림이 속성 프리셋 탐지 거리(`WiringViewModel.PresetRangeOf`)를 읽게 FenceScene 에 잇기(지금은 종류별 20/15 m).
2. 결선 펜스 보기에서 칩을 끌 때 그 칸 붙는 점 표시(W4 규칙표) — 지금은 설치 위치 절 수치 칸 · 콤보.
3. 설정 창 레일 "센서 모델" · 장비 창 [⋯] 입구(지금은 결선 절 [프리셋 관리]만).
4. appsettings `SensorPresets.Source` 읽기(지금은 GisDb 고정 등록).
5. 헤디드(실창) 확인 — PRD §10 표의 "헤디드 남음" 열(끌기 · 빨강 고리 · 세 그림 동시 움직임 · 펜스 보기 모양/코일 위 · 파일 대화상자) + GIS DB 실접속.
