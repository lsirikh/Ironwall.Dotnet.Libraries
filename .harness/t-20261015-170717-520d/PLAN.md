# PLAN — GOP 서버 8.0.8 부품 카탈로그 계약 반영(조립기 · 결선)

## 목표
서버 8.0.8(마이그레이션 v135/v136) 부품 카탈로그 계약을 장비 조립기 · 프리셋 · 결선에 반영한다.
`>= 8.0.8` 에서만 새 규칙, 그 전 서버(6.3.2 · 8.0.6 …)는 지금 동작 그대로.

## 근거
- 회신 문서(`docs/coordinations/GOP_Server_API_GIS_controller_rs485_port_*`)는 이 워크트리에 없다(검색 0건).
  대신 로컬 시험 서버 소스 `C:\workspace_python\api-test-server`(8.0.8 구현)를 정본으로 읽었다:
  - `app/routers/device_spec.py` — 전체 `GET /devices/spec` 은 **모든** component_type 을 `applies_to` 와 함께 준다.
    카테고리 `GET /devices/{type}/spec` 은 기본 즐겨찾기만, `?components=all` 은 전부.
    `applies_to: []` = "어느 카테고리의 즐겨찾기도 아님"(null = 전 카테고리 즐겨찾기).
  - `tests/test_component_terminal_favorites.py` — 즐겨찾기 밖 부품도 201, 모르는 유형만 422.
  - `app/routers/sensors.py` · `app/schemas/device.py` — 센서 `connection.port_key`(정규식 `^[a-z][a-z0-9_]*$`),
    소속 제어기 `hardware_spec.components[].key` 에 없으면 422. ConnectionAxis 는 `extra=forbid` 라 8.0.8 전 서버에 보내면 422.

## 결정
1. 판본 판정: `DeviceQueryPolicy.AttachesComponentsAnywhere` = 축 계약 && `RawVersion >= 8.0.8`(순서 비교).
   기존 `IServerContractProbe.RawVersion` 을 쓴다(새 프로브 · 판본 고정 없음).
2. 카탈로그는 이미 전체 `GET /devices/spec` 을 읽고 있어 모든 유형이 들어온다 → 새 API 호출 없이
   - `ComponentTypeInfo.AttachesAnywhere`(8.0.8 이면 true) + `CanAttachTo(category)` 추가.
     `AppliesToCategory` 는 그대로(= 즐겨찾기 판정)로 두고, 막는 자리(AssemblyBoard 4곳 · PresetRequestBuilder)만 `CanAttachTo` 로.
   - `IComponentCatalog.AllComponentTypes()` 기본 구현 멤버(가짜 구현 안 깨짐) — 팔레트 '전체'.
   - 8.0.8 에서 `applies_to: []` 는 빈 집합(어디에도 즐겨찾기 아님)으로 읽는다(그 전은 종전대로 null).
3. 팔레트: 8.0.8 일 때만 `[즐겨찾기 | 전체]` 토글이 보인다. 기본 = 즐겨찾기(종전 목록과 같다).
4. `PUT /devices/{type}/spec/favorites`(즐겨찾기 편집)는 할 일 목록의 요구(팔레트 · 검증 · 토글)에 없어 이번 범위에서 뺐다.
5. 시드: 8.0.8 이면 표준 제어기 씨앗 = rs485_1 · rs485_2 · nic · ci_1 · ci_2(서버 시험의 GIS 프리셋 그대로).
   Id · 심은 시각은 같게 두어 손대지 않은 씨앗은 서버 판본에 따라 서로 바뀐다(`DevicePresetStore.SerialPortSeeds`).
   `DevicePresetSeeds.All`(종전 3부품)은 그대로 — 기존 시험 단언 유지.
6. 결선: `SensorFacts.PortKey`(init 속성 — 값 비교에 포함돼 바뀐 줄로 세진다) · `ConnectionAxisDto.PortKey` ·
   `IConnectionAxisModel.PortKey`(기본 구현) · `SensorDeviceDto.PortKey`.
   표에 '제어기 포트' 칸 — 고를 수 있는 것은 제어기 부품 key(8.0.8 일 때만 채움, 아니면 '—').
   보내기도 8.0.8 일 때만(`WiringApplyService`), 아니면 port_key 키 자체를 싣지 않는다.
   비우기(포트 해제)는 지원하지 않는다 — null 은 직렬화에서 빠지고 서버 병합으로 지울 길이 없다.

## 진행
- [x] 판본 판정(DeviceQueryPolicy.AttachesComponentsAnywhere)
- [x] 카탈로그 · ComponentTypeInfo(AttachesAnywhere · CanAttachTo) · IComponentCatalog.AllComponentTypes(기본 구현)
- [x] AssemblyBoard 4곳 · PresetRequestBuilder.Validate → CanAttachTo
- [x] 조립기 팔레트 [즐겨찾기 | 전체](AssemblyViewModel · AssemblyView.xaml, Console.Chip 라디오 — 테마 토큰)
- [x] 시드(DevicePresetSeeds.AllFor · DevicePresetStore.SerialPortSeeds · AssemblyLauncher)
- [x] 결선 port_key(ConnectionAxisDto · SensorDeviceDto · IConnectionAxisModel · DeviceAxesMapper · SensorFacts ·
      WiringApplyService 생성/수정/드리프트 · SensorRowViewModel · WiringViewModel · WiringView.xaml '제어기 포트' 칸 · WiringLauncher)
- [x] 시험 `Devices.Ui/Tests/ComponentCatalog808Tests.cs`(31건)
- [x] 검증
  - 고치기 전: 같은 파일의 보드 · 검증 7건 → 8.0.8 쪽 4건 실패, 6.3.2 · 8.0.6 쪽 3건 통과(종전 동작)
  - 고친 뒤: 31건 전부 통과
  - Devices.Ui csproj(창 띄우는 · 실입력 시험 17묶음 제외) 4083건 통과
  - `dotnet build Ironwall.Dotnet.Libraries.sln` 오류 0

## 발견
- 시험 가짜 프로브들의 `RawVersion` 은 `"V8_0"` 같은 열거 이름이라 판본으로 못 읽힌다 → 기존 시험은 전부 종전 동작(8.0.8 아님)으로 돈다.
- 받은 센서 DTO 의 `port_key` 를 `PortKey` 로 되돌려 담으면, 받은 DTO 를 고쳐 보내는 경로가 port_key 를 다시 실어
  서버가 포트를 재검사(이름만 고친 저장도 422 가능)한다 → 읽기는 `ReceivedConnection.PortKey` 로만 한다.
- 결선 PATCH 는 포트가 바뀐 줄에만 port_key 를 싣는다(서버도 보낸 저장에서만 검사).

## 남은 일(범위 밖)
- 즐겨찾기 편집(`PUT /devices/{type}/spec/favorites`) 화면.
- 포트 해제(port_key 비우기) — 서버 병합에서 지울 길이 생기면.
- 로컬 시험 서버(8.0.8)에 붙인 실왕복 확인 — 이번엔 헤드리스 시험만.
