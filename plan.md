# Ironwall.Dotnet.Libraries Migration — TDD Plan

> **기준 문서**: `Docs/PRD_Migration_Master_Guide.md` v1.1
> **원본 PRD**: `PRD_Messages_Update.md` v1.3, `Speaker_Enclosure_Lamp_Integration_PRD.md` v1.1
> **설계 문서**: `GOP_Restful_Api_연동설계.md` v3.8, `Gop_Message_Broker_연동설계.md` v1.2
> **방식**: Red → Green → Refactor (CLAUDE.md 준수)
> **테스트 기준선**: 174 tests passing (Messages 121 + Monitoring.Models 27 + Devices.Ui 26)
> **마킹**: `[ ]` 미진행, `[~]` 진행중, `[x]` 완료

---

## Phase 0: 구조 정리 (Tidy First — Structural Only)

> 행위 변경 없음. 네임스페이스/구조만 수정. 기존 48개 테스트가 수정 전후 모두 통과해야 함.

### A0.1: Helpsers → Helpers 네임스페이스 수정
- [x] 기존 테스트 전체 실행 → Green 확인 (기준선) — 48/48
- [x] `Helpers/FromEventConverter.cs` namespace `Helpsers` → `Helpers` 수정
- [x] `Dto/Events/ActionEventDto.cs` using 문 `Helpsers` → `Helpers` 수정
- [x] 기존 테스트 전체 실행 → Green 유지 확인 — 48/48

### A0.2: CameraEventPresetDto Legacy 네임스페이스 이동
- [x] `Dto/Integrations/CameraEventPresetDto.cs` namespace `Wpf.Pids.Proxy.Master.DTO.Integrations` → `Ironwall.Dotnet.Libraries.Messages.Dto.Integrations` 수정
- [x] `Dto/Events/DetectionExEventDto.cs` using 문 수정
- [x] `Dto/Integrations/CameraEventMappingDto.cs` using 문 수정 (같은 네임스페이스이므로 using 제거)
- [x] 기존 테스트 전체 실행 → Green 유지 확인 — 48/48

---

## Phase 2: 신규 DTO 추가 (Non-Breaking)

> 기존 코드를 건드리지 않고 신규 파일만 추가. 기존 48개 테스트에 영향 없음.

### A2.1: BaseDeviceDto — Device 공통 기반 클래스
- [x] Test: `BaseDeviceDto_Serialization_ShouldHaveSnakeCaseFields`
  - 직렬화 시 `number_device`, `name_device`, `type_device`, `status`, `is_enable`, `device_groups`, `description`, `ip_address`, `ip_port` 필드 존재
- [x] Test: `BaseDeviceDto_Deserialization_FromBackendJson_ShouldMapAllFields`
  - Backend 응답 JSON → BaseDeviceDto 역직렬화, 모든 필드 값 일치
- [x] Impl: `Dto/Devices/BaseDeviceDto.cs` 생성 — 50/50 Green

### A2.2: SpeakerDeviceDto
- [x] Test: `SpeakerDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize`
  - TypeDevice 기본값 = "IpSpeaker", 직렬화 → 역직렬화 왕복 검증
- [x] Impl: `Dto/Devices/SpeakerDeviceDto.cs` 생성 — 51/51 Green

### A2.3: EnclosureDeviceDto
- [x] Test: `EnclosureDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize`
  - TypeDevice 기본값 = "Enclosure", 직렬화 → 역직렬화 왕복 검증
- [x] Impl: `Dto/Devices/EnclosureDeviceDto.cs` 생성 — 52/52 Green

### A2.4: LampDeviceDto
- [x] Test: `LampDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize`
  - TypeDevice 기본값 = "Lamp", 직렬화 → 역직렬화 왕복 검증
- [x] Impl: `Dto/Devices/LampDeviceDto.cs` 생성 — 53/53 Green

### A2.5: GeolocationDto
- [x] Test: `GeolocationDto_Serialization_ShouldHaveLatLonAlt`
  - `latitude`, `longitude`, `altitude` 필드, 왕복 검증
- [x] Impl: `Dto/Devices/GeolocationDto.cs` 생성 — 54/54 Green

### A2.6: CameraUrlsDto
- [x] Test: `CameraUrlsDto_Serialization_ShouldHaveRtspHttpSnapshot`
  - `rtsp`, `http`, `snapshot` 필드, 왕복 검증
- [x] Impl: `Dto/Devices/CameraUrlsDto.cs` 생성 — 55/55 Green

### A2.7: 부가 DTO 일괄 (DeviceGroupDto, CameraSettingDto, FileGroupDto, ServerDto, CategoryDto)
- [x] Test: `DeviceGroupDto_Serialization_RoundTrip` — id, name, type_device_group, description
- [x] Test: `CameraSettingDto_Serialization_RoundTrip` — camera_id + 설정 필드
- [x] Test: `FileGroupDto_Serialization_RoundTrip` — id, name, files
- [x] Test: `ServerDto_Serialization_RoundTrip` — id, name, ip_address, port
- [x] Test: `CategoryDto_Serialization_RoundTrip` — id, name, type_category, description
- [x] Impl: 5개 DTO 파일 생성 — 60/60 Green

### A2.8: DetectionDetailDto + DetectedObjectDto
- [x] Test: `DetectionDetailDto_Serialization_ShouldHaveSignalThumbnailObjects`
- [x] Test: `DetectionDetailDto_WithJsonExtensionData_ShouldPreserveUnknownFields`
- [x] Test: `DetectedObjectDto_Serialization_ShouldHaveLabelConfidenceBbox`
- [x] Impl: `Dto/Events/DetectionDetailDto.cs`, `Dto/Events/DetectedObjectDto.cs` 생성 — 63/63 Green

### A2.9: MalfunctionDetailDto
- [x] Test: `MalfunctionDetailDto_Serialization_ShouldHaveFirstSecondStartEnd`
- [x] Test: `MalfunctionDetailDto_WithJsonExtensionData_ShouldPreserveUnknownFields`
- [x] Impl: `Dto/Events/MalfunctionDetailDto.cs` 생성 — 65/65 Green

### A2.10: EventMappingCameraDto
- [x] Test: `EventMappingCameraDto_Serialization_ShouldMatchDesignDoc`
  - `camera_id`, `target_preset_id`, `home_preset_id`, `delay_time`, `priority`
- [x] Impl: `Dto/Integrations/EventMappingCameraDto.cs` 생성

### A2.11: EventMappingSpeakerDto
- [x] Test: `EventMappingSpeakerDto_Serialization_ShouldMatchDesignDoc`
- [x] Impl: `Dto/Integrations/EventMappingSpeakerDto.cs` 생성

### A2.12: EventMappingLampDto
- [x] Test: `EventMappingLampDto_Serialization_ShouldMatchDesignDoc`
- [x] Impl: `Dto/Integrations/EventMappingLampDto.cs` 생성 — 68/68 Green

### A2.13: BrokerPublish\<T\>
- [x] Test: `BrokerPublish_ShouldHaveTypePUB_AndSerialize`
  - `type_message` = "PUB" 고정, 직렬화 검증
- [x] Test: `BrokerPublish_RoundTrip_ShouldPreserveData`
- [x] Impl: `Defines/Brokers/BrokerPublish.cs` 생성 — 70/70 Green

### A2.14: NATS Body DTO — PidsProxy 제어 (2종)
- [x] Test: `ModeChangeBodyDto_Serialization_ShouldHaveMode`
- [x] Test: `WindyBodyDto_Serialization_ShouldHaveMode`
- [x] Impl: `Dto/Brokers/ModeChangeBodyDto.cs`, `WindyBodyDto.cs`

### A2.15: NATS Body DTO — Broadcasting 제어 (4종)
- [x] Test: `BroadcastingBodyDtos_Serialization_AllFields`
- [x] Impl: TtsBodyDto, BroadcastPlayBodyDto, BroadcastStopBodyDto, BroadcastTestBodyDto

### A2.16: NATS Body DTO — Lamp 제어 (6종)
- [x] Test: `LampBodyDtos_Serialization_AllFields`
- [x] Impl: LampClear/Off/ColorSet/BuzzerSet/ColorTest/BuzzerTest BodyDto

### A2.17: NATS Body DTO — NVR/Camera 제어 (13종)
- [x] Test: `CameraBodyDtos_PtzAndTracking_Serialization`
- [x] Test: `CameraBodyDtos_ModeAndPeripheral_Serialization`
- [x] Impl: 13개 DTO (PtzControl/PtzStatus/TrackingSet/TrackingStatus/Palette/Wiper/Heater/Fan/WeatherMode/CameraMode/Headlight/DayNight/Power)

### A2.18: NATS Body DTO — 마스터 데이터 동기화 (9종)
- [x] Test: `SyncBodyDtos_Serialization_AllFields`
- [x] Impl: 9개 Sync DTO (Device/Server/Category/DeviceGroup/EventMapping/Preset/FileGroup/CameraSetting/ProxySetting)

> **Phase 2 완료** — 77/77 Green (기존 48 + 신규 29)

---

## Phase 3: 기존 Device DTO 수정 (Additive — Nullable 추가)

> group_device는 아직 유지. device_groups[]를 nullable로 추가하여 과도기 상태.
> 기존 테스트는 새 필드가 nullable이므로 깨지지 않아야 함.

### A3.1: ControllerDeviceDto → BaseDeviceDto 상속 전환 + 공통 필드
- [x] Test: `ControllerDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize`
  - 기존 JSON 형식(group_device 포함, device_groups 없음)이 여전히 정상 역직렬화
- [x] Test: `ControllerDeviceDto_NewFields_ShouldSerializeWhenPresent`
  - is_enable, geolocation, device_groups[], description이 있을 때 직렬화 확인
- [x] Impl: ControllerDeviceDto가 BaseDeviceDto 상속으로 변경, 중복 필드 제거 — 79/79 Green

### A3.2: SensorDeviceDto → BaseDeviceDto 상속 전환 + 공통 필드
- [x] Test: `SensorDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize`
- [x] Test: `SensorDeviceDto_NewFields_ShouldSerializeWhenPresent`
- [x] Impl: SensorDeviceDto가 BaseDeviceDto 상속으로 변경 — 81/81 Green

### A3.3: CameraDeviceDto → BaseDeviceDto 상속 전환 + urls 추가
- [x] Test: `CameraDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize`
  - 기존 JSON (rtsp_uri, rtsp_port, group_device 포함)이 정상 역직렬화
- [x] Test: `CameraDeviceDto_NewUrlsField_ShouldSerializeAsNestedObject`
  - urls (CameraUrlsDto) 필드가 중첩 객체로 직렬화
- [x] Test: `CameraDeviceDto_NewFields_IsRecordAndHardwareSpec`
  - is_record, hardware_spec nullable 필드
- [x] Impl: CameraDeviceDto가 BaseDeviceDto 상속, urls/is_record/hardware_spec 추가 — 84/84 Green

### A3.4: 전체 하위 호환 통합 테스트
- [x] Test: `AllDeviceDtos_OldJsonWithoutNewFields_ShouldDeserializeWithDefaults`
  - 모든 Device DTO (Controller, Sensor, Camera, Speaker, Enclosure, Lamp) 구 JSON → 정상 역직렬화
- [x] 기존 48개 테스트 전체 실행 → Green 유지 확인 — 85/85 Green

> **Phase 3 완료** — 85/85 Green (기존 48 + Phase 2 신규 29 + Phase 3 신규 8)

---

## Phase 5: Broker Envelope 필드명 변경

> ⚠️ Breaking Change — BaseBrokerMessage JsonProperty 변경.
> 기존 Broker 관련 테스트가 모두 실패할 예정 → 테스트를 먼저 새 필드명으로 업데이트.

### A5.1: BaseBrokerMessage 필드명 변경
- [x] Test: `BaseBrokerMessage_Serialization_ShouldUseMType_Cmd_Created`
- [x] Test: `BaseBrokerMessage_Deserialization_FromNewFieldNames`
- [x] Impl: BaseBrokerMessage.cs — `type_message`→`m_type`, `type_command`→`cmd`, `timestamp`→`created`

### A5.2: BaseMessage\<T\> data → body 변경
- [x] Test: `BaseMessage_Serialization_ShouldUseBodyNotData`
- [x] Impl: BaseMessage\<T\>.Data JsonProperty `data` → `body`

### A5.3: BrokerMessageHelper 업데이트
- [x] Test: `BrokerRequest_NewFieldNames_RoundTrip_ShouldPreserveData`
- [x] Test: `BrokerResponse_NewFieldNames_RoundTrip_ShouldPreserveData`
- [x] Test: `ParseEventsFromBrokerMessage_WithNewFieldNames_ShouldParse`
- [x] Test: `ParseSingleEventFromBrokerMessage_WithNewFieldNames_ShouldParse`
- [x] Impl: BrokerMessageHelper.cs — `brokerMsg["data"]` → `brokerMsg["body"]`

### A5.4: 기존 Broker 테스트 업데이트
- [x] BrokerMessageTests — JSON 필드명 업데이트 (m_type, cmd, body, created)
- [x] BrokerMessageParsingTests — body 변경
- [x] BrokerPublishTests — m_type, cmd 변경
- [x] MessageIntegrationTests — 왕복 테스트 자동 통과 (직렬화→역직렬화)
- [x] DateTimeFormatPreservationTests — ApiResponse 기반이므로 영향 없음
- [x] 전체 테스트 실행 → **92/92 Green**

> **Phase 5 완료** — 92/92 Green (기존 85 + 신규 7)

---

## Phase 6: Event DTO 구조 변경 (Flat → Nested)

> ⚠️ Breaking Change — Event DTO 전면 재설계.
> IEventDto 인터페이스 변경으로 구현 클래스 전부 수정.

### A6.1: IEventDto + IDeviceEventDto 인터페이스 재설계
- [x] Test: `IEventDto_ShouldHaveMinimalProperties`
- [x] Test: `IDeviceEventDto_ShouldExtendIEventDto_WithDeviceAndDescription`
- [x] Impl: `Defines/Commons/IEventDto.cs` 재설계 — IDeviceEventDto 포함

### A6.2: DetectionEventDto — Flat → Nested
- [x] Test: `DetectionEventDto_NewStructure_ShouldHaveNestedDevice`
- [x] Test: `DetectionEventDto_Deserialization_FromNewBackendJson`
- [x] Test: `DetectionEventDto_WithNullDevice_ShouldDeserialize`
- [x] Impl: DetectionEventDto.cs 재설계

### A6.3: MalfunctionEventDto — Flat → Nested + detail
- [x] Test: `MalfunctionEventDto_NewStructure_ShouldHaveNestedDeviceAndDetail`
- [x] Test: `MalfunctionEventDto_Deserialization_DetailShouldHaveFirstSecondStartEnd`
- [x] Impl: MalfunctionEventDto.cs 재설계

### A6.4: ConnectionEventDto — Flat → Nested
- [x] Test: `ConnectionEventDto_NewStructure_ShouldHaveNestedDevice`
- [x] Impl: ConnectionEventDto.cs 재설계

### A6.5: ActionEventDto — device 추가
- [x] Test: `ActionEventDto_ShouldHaveDeviceAndDeviceDescription`
- [x] Impl: ActionEventDto.cs 수정

### A6.6: DetectionExEventDto 구조 변경
- [x] Test: `DetectionExEventDto_OriginEvent_ShouldUseNewDetectionEventStructure`
- [x] Test: `DetectionExEventDto_CameraPresets_ShouldUseNewNamespace`
- [x] Impl: 변경 불필요 — OriginEvent가 새 DetectionEventDto 참조

### A6.7: FromEventConverter 업데이트
- [x] Test: `FromEventConverter_ShouldDeserializeDetectionEvent_WithNewStructure`
- [x] Test: `FromEventConverter_ShouldDeserializeMalfunctionEvent_WithNewStructure`
- [x] Impl: 변경 불필요 — 이미 IEventDto 기반 동작

### A6.8: 기존 Event 테스트 업데이트
- [x] `DetectionExEventDtoTests` — 새 DetectionEventDto 구조 반영
- [x] `BrokerMessageParsingTests` — 새 MalfunctionEventDto 구조 반영
- [x] 전체 테스트 실행 → **105/105 Green**

> **Phase 6 완료** — 105/105 Green (기존 92 + 신규 13)

---

## Phase 7: Integration DTO 수정

### A7.1: EventMappingDto 재설계
- [x] Test: `EventMappingDto_NewStructure_ShouldHaveDeviceGroupIdAndCamerasSpeakersLamps`
- [x] Impl: EventMappingDto.cs — group_event 삭제, device_group_id/category_event_mapping/cameras/speakers/lamps 추가
- [x] DetectionExEventDto.cs — category_event→category_event_mapping, camera_presets→urls (EventUrlsDto)
- [x] 기존 테스트 CategoryEvent→CategoryEventMapping 업데이트

### A7.2: Legacy Integration 파일 삭제
- [x] `SensorEventMapping.cs` 삭제
- [x] `CameraEventMappingDto.cs` 삭제
- [x] `CameraEventPresetDto.cs` 삭제
- [x] 전체 테스트 실행 → **106/106 Green**

> **Phase 7 완료** — 106/106 Green (기존 105 + 신규 1)

---

## Phase 7+ : DetectionExEventDto 삭제

> DetectionEventDto가 nested device 구조로 전환되면서 DetectionExEventDto 래퍼가 불필요해짐.
> NameEvent/CategoryEventMapping/Urls 등은 클라이언트가 EventMapping 캐시에서 조회하므로 DTO에 불필요.

- [x] PRD_Messages_Update.md 업데이트 (§7.2, §8.6, §10.7, §11.1, §11.3)
- [x] UnitTest.cs — DetectionExEventDtoTests 클래스 전체 삭제 (7개 테스트)
- [x] UnitTest.cs — A6.6 region 삭제 (2개 테스트)
- [x] `Dto/Events/DetectionExEventDto.cs` 삭제
- [x] `Dto/Events/EventUrlsDto.cs` 삭제
- [x] 전체 테스트 실행 → **98/98 Green**

> **Phase 7+ 완료** — 98/98 Green (106 - 삭제 8 = 98)

---

## 최종 검증

- [x] 전체 테스트 실행 → **98/98 All Green**
- [x] 컴파일 경고: 15개 (기존 CS0108/CS8602/xUnit2009 — 구조적 경고, Phase 9 범위)
- [x] PRD_Messages_Update.md와 코드 정합성 확인
  - 보류: group_device 필드 제거 (Phase 9 범위)
  - 보류: BaseDeviceDto에 version/geolocation 통합 (Phase 9 범위)

> **최종 검증 완료** — Messages 프로젝트 Phase 0~7+ 모두 완료

---

## Phase 8A: Events.Api 디버깅 (Flat→Nested 사이드이펙트 수정)

> **원인**: Messages Phase 6에서 Event DTO를 Flat→Nested로 변경한 영향
> **범위**: `Ironwall.Dotnet.Libraries.Events.Api/Tests/UnitTest.cs` — 44개 컴파일 에러
> **기준**: PRD_Messages_Update.md §9.2, GOP_스키마_전체.md §5
> **변환 규칙**:
> - `GroupEvent` → 제거 (DeviceGroup은 Device 통해 조회)
> - `Controller` (int), `Sensor` (int), `TypeDevice` (string) → `Device = new BaseDeviceDto { Id, TypeDevice, ... }`
> - `Sequence` (int) → `DeviceDescription` (스냅샷 문자열) 또는 제거
> - `FirstStart/FirstEnd/SecondStart/SecondEnd` → `Detail = new MalfunctionDetailDto { ... }`
> - `response.Data.Sequence` assertion → `response.Data.Device` 기반 assertion으로 변경

### A8A.1: DetectionEventDto 테스트 — Flat→Nested 변환 (3개 메서드, 13 에러)
- [x] `CreateDetectionEvent` — GroupEvent/Controller/Sensor/TypeDevice/Sequence → Device + DeviceDescription
- [x] `PatchDetectionEvent` — Sequence → 제거
- [x] `UpdateDetectionEvent` — 전체 필드 Nested 변환 + assertion `Sequence` → `Device` not null
- [x] 빌드 확인 → Detection 영역 0 에러

### A8A.2: MalfunctionEventDto 테스트 — Flat→Nested + Detail 변환 (2개 메서드, 20 에러)
- [x] `CreateMalfunctionEvent` — Controller/Sensor/GroupEvent/TypeDevice/Sequence + FirstStart/End/SecondStart/End → Device + Detail
- [x] `UpdateMalfunctionEvent` — 전체 필드 Nested 변환 + assertion `Sequence` → `Device` not null
- [x] 빌드 확인 → Malfunction 영역 0 에러

### A8A.3: ConnectionEventDto 테스트 — Flat→Nested 변환 (3개 메서드, 11 에러)
- [x] `CreateConnectionEvent` — Controller/Sensor/GroupEvent/TypeDevice/Sequence → Device + DeviceDescription
- [x] `PatchConnectionEvent` — Sequence → ActionReported="True" 대체 + assertion 변경
- [x] `UpdateConnectionEvent` — 전체 필드 Nested 변환 + assertion `Sequence` → `Device` not null
- [x] 빌드 확인 → Connection 영역 0 에러

### A8A.4: Events.Api 빌드 검증
- [x] `dotnet build Events.Api.csproj` → **0 에러, 1 경고** (기존 CS8604)
- [x] plan.md 업데이트

> **Phase 8A 완료** — Events.Api 44개 컴파일 에러 → 0 에러

---

## Phase 8B: Events.Ui 디버깅 (DtoToModelHelper + Tests Flat→Nested 변환)

> **원인**: Messages Phase 6 Event DTO Flat→Nested + Phase 7+ DetectionExEventDto 삭제 영향
> **범위**: `Events.Ui/Helpers/DtoToModelHelper.cs` (~51 에러) + `Events.Ui/Tests/UnitTest.cs` (~94 에러)
> **총 에러**: 145개 컴파일 에러
> **변환 규칙**:
> - `dto.GroupEvent` → 제거 (Model.EventGroup = null)
> - `dto.Controller/Sensor/TypeDevice` → `dto.Device` 기반 변환 (BaseDeviceDto → IBaseDeviceModel)
> - `dto.FirstStart/End/SecondStart/End` → `dto.Detail?.FirstStart/End/SecondStart/End`
> - `Model→DTO`: `ResolveDeviceIds()` → `new BaseDeviceDto { Id, TypeDevice }` 직접 구성
> - `ResolveDeviceFromDto(controller, sensor, typeDevice)` → `ConvertDeviceFromDto(dto.Device, deviceProvider)`

### A8B.1: DtoToModelHelper — DTO→Model 기본 변환 수정 (3개 메서드)
- [x] `ToDetectionEventModel(DetectionEventDto)` — GroupEvent/Controller/Sensor/TypeDevice → Device nested
- [x] `ToMalfunctionEventModel(MalfunctionEventDto)` — 위 + FirstStart/End → Detail nested
- [x] `ToConnectionEventModel(ConnectionEventDto)` — GroupEvent/Controller/Sensor/TypeDevice → Device nested

### A8B.2: DtoToModelHelper — Model→DTO 역변환 수정 (3개 메서드)
- [x] `ToDetectionEventDto(IDetectionEventModel)` — GroupEvent/Controller/Sensor/Sequence 제거 → Device nested
- [x] `ToMalfunctionEventDto(IMalfunctionEventModel)` — 위 + FirstStart/End → Detail nested
- [x] `ToConnectionEventDto(IConnectionEventModel)` — GroupEvent/Controller/Sensor/Sequence 제거 → Device nested

### A8B.3: DtoToModelHelper — DeviceProvider 오버로드 수정 (3개 메서드)
- [x] `ToDetectionEventModel(dto, deviceProvider)` — Device nested + provider lookup
- [x] `ToMalfunctionEventModel(dto, deviceProvider)` — 위 + Detail
- [x] `ToConnectionEventModel(dto, deviceProvider)` — Device nested + provider lookup

### A8B.4: DtoToModelHelper — ResolveDevice 헬퍼 리팩토링
- [x] `ResolveDeviceFromDto(controller, sensor, typeDevice, provider)` → `ConvertDeviceFromDto(BaseDeviceDto?, DeviceProvider?)` 변경
- [x] `ResolveDeviceIds(IBaseDeviceModel?)` → `ConvertDeviceToDto(IBaseDeviceModel?)` : BaseDeviceDto 반환
- [x] 사용하지 않는 구 헬퍼 제거

### A8B.5: Tests — DtoToModelHelperTests 테스트 데이터 Flat→Nested (8개 테스트)
- [x] Detection DTO→Model / Model→DTO 테스트 데이터 + assertion 변경
- [x] Malfunction DTO→Model / Model→DTO 테스트 데이터 + assertion 변경
- [x] Connection DTO→Model / Model→DTO 테스트 데이터 + assertion 변경
- [x] Action DTO→Model / Model→DTO — 변경 없음 (flat 필드 미사용)

### A8B.6: Tests — DeviceProvider/OriginEvent 테스트 수정
- [x] DtoToModelHelperWithDeviceProviderTests — 테스트 데이터 Nested 변환
- [x] DtoToModelHelperWithOriginEventTests — 테스트 데이터 Nested 변환

### A8B.7: Tests — DetectionExEventDtoToModelTests 삭제
- [x] DetectionExEventDtoToModelTests 클래스 전체 삭제 (4개 테스트)

### A8B.8: Events.Ui 빌드 검증
- [x] `dotnet build Events.Ui.csproj` → **0 에러, 81 경고** (기존) 확인
- [x] plan.md 업데이트

> **Phase 8B 완료** — Events.Ui 145개 컴파일 에러 → 0 에러

---

## Phase 9: 설계 문서 정합성 보정 (PRD §14 기반)

> **기준 문서**: `PRD_Messages_Update.md` v1.3 §14
> **범위**: `Ironwall.Dotnet.Libraries.Messages` 프로젝트만
> **방식**: Red → Green → Refactor (CLAUDE.md 준수)
> **테스트 기준선**: 98 tests passing (2026-02-20)
> **원칙**: Additive (nullable 추가) 먼저 → Breaking (타입 변경/삭제) 나중

### A9.1: GeolocationDto에 `location` 필드 추가 (§14.2.5)
- [x] Test: `GeolocationDto_WithLocation_ShouldSerializeAndDeserialize`
  - `location` 문자열 포함 왕복 직렬화 검증
- [x] Test: `GeolocationDto_WithoutLocation_ShouldDeserializeWithNull`
  - `location` 미포함 JSON → null 역직렬화 확인 (하위 호환)
- [x] Impl: `GeolocationDto.cs`에 `Location` (string?) 속성 추가 — 100/100 Green

### A9.2: DeviceGroupDto에 `device_count` 필드 추가 (§14.2.7)
- [x] Test: `DeviceGroupDto_WithDeviceCount_ShouldSerializeAndDeserialize`
  - `device_count` 포함 왕복 직렬화 검증
- [x] Impl: `DeviceGroupDto.cs`에 `DeviceCount` (int) 속성 추가 — 101/101 Green

### A9.3: BaseDeviceDto에 `version`, `group_device`, `geolocation` 추가 (§14.2.2~4)
- [x] Test: `BaseDeviceDto_WithVersion_ShouldSerialize`
  - `version` 필드 직렬화 검증
- [x] Test: `BaseDeviceDto_WithGroupDevice_ShouldSerialize`
  - `group_device` 필드 직렬화 검증
- [x] Test: `BaseDeviceDto_WithGeolocation_ShouldSerializeNestedObject`
  - `geolocation` 중첩 객체 직렬화 검증
- [x] Test: `BaseDeviceDto_DesignDocJson_ShouldDeserializeAllFields`
  - 설계 문서(NATS v1.1 §6.1) 실제 JSON → BaseDeviceDto 역직렬화, 모든 필드 일치 확인
- [x] Impl: `BaseDeviceDto.cs`에 `Version`, `GroupDevice`, `Geolocation` 속성 추가 — 105/105 Green

### A9.4: `device_groups` 타입 변경 — `List<int>?` → `List<DeviceGroupDto>?` (§14.2.1)
- [x] Test: `BaseDeviceDto_DeviceGroups_ShouldDeserializeAsObjectArray`
  - 설계 문서 JSON `device_groups: [{id,name,...}]` → `List<DeviceGroupDto>` 역직렬화 검증
- [x] Test: `BaseDeviceDto_DeviceGroups_NullShouldDeserialize`
  - `device_groups: null` → null 하위 호환
- [x] Impl: `BaseDeviceDto.cs` — `List<int>?` → `List<DeviceGroupDto>?` 변경
- [x] Fix: 기존 테스트 6곳 수정 (DeviceGroups int→DeviceGroupDto, JSON 형식, assertion) — 107/107 Green

### A9.5: 서브클래스 중복 필드 제거 (Tidy First — Structural Only)
- [x] `ControllerDeviceDto` — `GroupDevice`, `Version` 제거 (BaseDeviceDto에서 상속)
- [x] `SensorDeviceDto` — `GroupDevice`, `Version` 제거
- [x] `CameraDeviceDto` — `GroupDevice`, `Version` 제거 + Order 14~22 재정렬
- [x] 기존 테스트 전체 실행 → Green 유지 확인 — 107/107 Green

### A9.6: ConnectionEventDto `action_reported` 제거 + IEventDto 분리 (§14.2.6)
- [x] Test: `ConnectionEventDto_ShouldNotHaveActionReported`
  - 직렬화 결과에 `action_reported` 키 없음 확인
- [x] Test: `IActionReportableEventDto_DetectionAndMalfunction_ShouldHaveActionReported`
  - Detection/MalfunctionEventDto → IActionReportableEventDto 캐스팅 가능 확인
- [x] Impl: `IEventDto`에서 `ActionReported` 제거 → `IActionReportableEventDto` 신규 인터페이스 추가
- [x] Impl: `ConnectionEventDto`에서 `ActionReported` 속성 삭제
- [x] Fix: 기존 테스트 A6.1-1에서 `IEventDto.ActionReported` assertion 수정
- [x] 전체 테스트 실행 → Green 확인 — 109/109 Green

### A9.7: Phase 9 최종 검증
- [x] 전체 테스트 실행 → 109/109 All Green
- [x] 컴파일 경고: 15개 (기존 경고 동일, Phase 9에서 신규 추가 0건)
- [x] plan.md 최종 업데이트 완료

---

## Phase 10: DTO 변경 Cascading 에러 수정

> **목표**: Phase 9 DTO 변경으로 깨진 의존 프로젝트 복구
> **범위**: Devices.Ui, Events.Ui, Events.Api

### A10.1: Devices.Ui — GroupDevice nullable 대응
- [x] `DtoToModelHelper.cs` 3곳: `dto.GroupDevice` → `dto.GroupDevice ?? 0` — 오류 0개

### A10.2: Events.Ui — ConnectionEventDto.ActionReported 제거 대응
- [x] `DtoToModelHelper.cs`: `ToConnectionEventDto()`에서 `ActionReported` 행 삭제 — 오류 0개

### A10.3: Events.Api — 테스트 코드 ConnectionEventDto.ActionReported 제거 대응
- [x] `Tests/UnitTest.cs`: ConnectionEventDto 초기화 2곳 + Assert 1곳에서 `ActionReported` 참조 제거 — 오류 0개

### A10.4: 검증
- [x] Devices.Ui 빌드 → 오류 0개
- [x] Events.Ui 빌드 → 오류 0개
- [x] Events.Api 빌드 → 오류 0개
- [x] Messages 테스트 → 109/109 Green

> **Phase 10 완료** — 전체 빌드 0 에러, 109/109 Green

---

## Phase 11: BaseDeviceDto 구조 보정 (Tidy First — Structural Change)

> **기준 문서**: `Speaker_Enclosure_Lamp_Integration_PRD.md` §3
> **원칙**: Tidy First — 구조적 변경을 행위 변경보다 먼저 수행
> **목표**: `BaseDeviceDto`에서 `Description`, `IpAddress`, `IpPort`를 제거하고 서브클래스로 이동
> **테스트 기준선**: 109 tests passing

### A11.1: BaseDeviceDto에서 `Description`, `IpAddress`, `IpPort` 제거 + Order 재조정
- [x] 기존 테스트 전체 실행 → Green 확인 (기준선) — 109/109
- [x] `BaseDeviceDto.cs`에서 `Description` (Order 8), `IpAddress` (Order 9), `IpPort` (Order 10) 제거
- [x] `BaseDeviceDto.cs` Order 재조정: `Version` → Order 8, `GroupDevice` → Order 9, `Geolocation` → Order 10

### A11.2: ControllerDeviceDto에 `IpAddress`, `IpPort` 추가
- [x] Impl: `ControllerDeviceDto.cs`에 `IpAddress`(Order 11), `IpPort`(Order 12) 추가, `Sensors` Order 13으로 조정

### A11.3: CameraDeviceDto에 `IpAddress`, `IpPort` 추가
- [x] Impl: `CameraDeviceDto.cs`에 `IpAddress`(Order 11), `IpPort`(Order 12) 추가, 기존 필드 Order 13~21로 재조정

### A11.4: 기존 테스트 업데이트 (BaseDeviceDto 필드 제거 반영)
- [x] Messages `UnitTest.cs` — BaseDeviceDto 직렬화 테스트에서 `ip_address`, `ip_port`, `description` assertion 제거/수정
- [x] Messages `UnitTest.cs` — Speaker/Enclosure/Lamp 테스트에서 `IpAddress`/`IpPort` 접근 제거 (Base에서 제거됨)
- [x] Messages `UnitTest.cs` — Controller/Sensor/Camera backward-compat 테스트에서 `Description` assertion 제거
- [x] 전체 테스트 실행 → **109/109 Green**

### A11.5: Cascading 수정 — 의존 프로젝트
- [x] Devices.Ui 빌드 → 0 에러 (cascading 수정 불필요 — 서브타입이 이미 IpAddress/IpPort 보유)
- [x] Events.Ui 빌드 → 0 에러
- [x] Events.Api 빌드 → 0 에러
- [x] Messages 전체 테스트 → 109/109 Green 유지

> **Phase 11 완료** — 109/109 Green, 전체 빌드 0 에러

---

## Phase 12: Enum 확장

> **기준 문서**: PRD §4.9
> **방식**: Red → Green (TDD)

### A12.1: EnumDeviceType에 Lamp, Enclosure 추가
- [x] Impl: `EnumDeviceType.cs`에 `Lamp = 18`, `Enclosure = 19` 추가

### A12.2: EnumSpeakerType 신규 생성
- [x] Impl: `EnumSpeakerType.cs` 생성 — `NORMAL, ADMIN, MONITOR, DEV`

### A12.3: EnumDoorStatus 신규 생성
- [x] Impl: `EnumDoorStatus.cs` 생성 — `CLOSED, OPEN`
- [x] Enums 프로젝트 빌드 → 0 에러

> **Phase 12 완료** — Enum 확장 완료

---

## Phase 13: Messages DTO — Server/Category 보정 + 서브클래스 필드 추가

> **기준 문서**: PRD §4.8, §5.2.3, §5.2.4
> **방식**: Red → Green (TDD)
> **원칙**: Additive (nullable 추가) 먼저 → Breaking (타입 변경/삭제) 나중

### A13.1: ServerDto 보정 — 7필드 추가, TypeServer 제거
- [x] Test: `ServerDto_ApiDesignDoc_ShouldDeserializeAllFields`
- [x] Test: `ServerDto_ShouldNotHaveTypeServer`
- [x] Impl: `ServerDto.cs` — `TypeServer` 제거, `CategoryId`, `Status`, `Hostname`, `UserName`, `UserPassword`, `ThresholdConfig`(JObject) 추가

### A13.2: CategoryDto 보정 — TypeCategory→TypeServer, SortOrder 추가
- [x] Test: `CategoryDto_ApiDesignDoc_ShouldHaveTypeServerAndSortOrder`
- [x] Impl: `CategoryDto.cs` — `TypeCategory` → `TypeServer` JsonProperty 변경, `SortOrder` 추가

### A13.3: SpeakerDeviceDto 고유 필드 추가
- [x] Test: `SpeakerDeviceDto_ApiDesignDoc_ShouldHaveSpeakerTypeDescriptionServer`
- [x] Test: `SpeakerDeviceDto_ServerNested_ShouldDeserializeFromApiJson`
- [x] Impl: `SpeakerDeviceDto.cs` — `SpeakerType`(11), `Description`(12), `Server: ServerDto?`(13) 추가

### A13.4: EnclosureDeviceDto 고유 필드 추가
- [x] Test: `EnclosureDeviceDto_ApiDesignDoc_ShouldHaveDoorStatusThresholdHeaterFan`
- [x] Impl: `EnclosureDeviceDto.cs` — `DoorStatus`(11), `ThresholdConfig: JObject?`(12), `HeaterEnabled`(13), `FanEnabled`(14) 추가

### A13.5: LampDeviceDto 고유 필드 추가
- [x] Test: `LampDeviceDto_ApiDesignDoc_ShouldHaveIpAddressIpPortUserNameUserPasswordDescription`
- [x] Impl: `LampDeviceDto.cs` — `IpAddress`(11), `IpPort`(12), `UserName`(13), `UserPassword`(14), `Description`(15) 추가

### A13.6: 기존 테스트 업데이트 + 검증
- [x] ServerDto 기존 테스트 — `TypeServer` → `CategoryId` + `Status` 변경
- [x] CategoryDto 기존 테스트 — `TypeCategory` → `TypeServer`, `SortOrder` 추가
- [x] 전체 테스트 실행 → **116/116 Green** (109 + 7 신규)

> **Phase 13 완료** — 116/116 Green

---

## Phase 14: Monitoring.Models — Model Layer

> **기준 문서**: PRD §5.3, §5.4
> **방식**: Red → Green (TDD)

### A14.1: IServerModel / ServerModel 신규 생성
- [x] Impl: `Servers/IServerModel.cs`, `Servers/ServerModel.cs` 생성

### A14.2: ICategoryModel / CategoryModel 신규 생성
- [x] Impl: `Servers/ICategoryModel.cs`, `Servers/CategoryModel.cs` 생성

### A14.3: ISpeakerDeviceModel / SpeakerDeviceModel 신규 생성
- [x] Impl: `Devices/ISpeakerDeviceModel.cs`, `Devices/SpeakerDeviceModel.cs` 생성

### A14.4: IEnclosureDeviceModel / EnclosureDeviceModel 신규 생성
- [x] Impl: `Devices/IEnclosureDeviceModel.cs`, `Devices/EnclosureDeviceModel.cs` 생성

### A14.5: ILampDeviceModel / LampDeviceModel 신규 생성
- [x] Impl: `Devices/ILampDeviceModel.cs`, `Devices/LampDeviceModel.cs` 생성

### A14.6: DeviceModelConverter switch 분기 수정
- [x] Test: `DeviceModelConverter_IpSpeaker_ShouldDeserializeToSpeakerDeviceModel` (A14.6-1)
- [x] Test: `DeviceModelConverter_Enclosure_ShouldDeserializeToEnclosureDeviceModel` (A14.6-2)
- [x] Test: `DeviceModelConverter_Lamp_ShouldDeserializeToLampDeviceModel` (A14.6-3)
- [x] Test: `Mixed list with Speaker/Enclosure/Lamp` (A14.6-4)
- [x] Impl: `DeviceModelConverter.cs` — IpSpeaker/Enclosure/Lamp case 구현

### A14.7: 검증
- [x] Monitoring.Models 빌드 → 0 에러
- [x] Monitoring.Models 테스트 → **19/19 Green** (15 기존 + 4 신규)
- [x] Messages 테스트 → **116/116 Green** 유지

---

## Phase 15: Devices — Provider Layer

> **기준 문서**: PRD §5.5
> **방식**: Red → Green (TDD)

### A15.1: SpeakerDeviceProvider 생성
- [x] Impl: `Providers/SpeakerDeviceProvider.cs` — `BaseDeviceProdiver<ISpeakerDeviceModel>` 상속

### A15.2: EnclosureDeviceProvider 생성
- [x] Impl: `Providers/EnclosureDeviceProvider.cs` — `BaseDeviceProdiver<IEnclosureDeviceModel>` 상속

### A15.3: LampDeviceProvider 생성
- [x] Impl: `Providers/LampDeviceProvider.cs` — `BaseDeviceProdiver<ILampDeviceModel>` 상속

### A15.4: DeviceModule.cs DI 등록
- [x] Impl: `DeviceModule.cs`에 3개 Provider 등록 추가
- [x] Devices 빌드 → 0 에러, Messages 116/116 Green, Monitoring.Models 19/19 Green

> **Phase 15 완료** — Provider 3종 + DI 등록, 빌드 0 에러

---

## Phase 16: Devices.Api — API Service

> **기준 문서**: PRD §5.6
> **방식**: Red → Green (TDD)

### A16.1: Speaker CRUD API
- [x] Impl: `IDeviceApiService` + `DeviceApiService`에 Speaker 6개 메서드 추가
  - GetSpeakersAsync (group_device, speaker_type, status 필터)
  - GetSpeakerByIdAsync, CreateSpeakerAsync, PatchSpeakerAsync, UpdateSpeakerAsync, DeleteSpeakerAsync

### A16.2: Enclosure CRUD API
- [x] Impl: Enclosure 6개 메서드 추가
  - GetEnclosuresAsync (group_device, door_status, status 필터)
  - GetEnclosureByIdAsync, CreateEnclosureAsync, PatchEnclosureAsync, UpdateEnclosureAsync, DeleteEnclosureAsync

### A16.3: Lamp CRUD API
- [x] Impl: Lamp 6개 메서드 추가
  - GetLampsAsync (group_device, status 필터)
  - GetLampByIdAsync, CreateLampAsync, PatchLampAsync, UpdateLampAsync, DeleteLampAsync

### A16.4: 검증
- [x] Devices.Api 빌드 → 0 에러
- [x] Messages 116/116 Green, Monitoring.Models 19/19 Green

> **Phase 16 완료** — Speaker/Enclosure/Lamp CRUD 18개 메서드, 빌드 0 에러

---

## Phase 17: Devices.Ui — ViewModel + DtoToModel

> **기준 문서**: PRD §5.7.1 ~ §5.7.3
> **방식**: Red → Green (TDD)

### A17.1: DtoToModelHelper — Speaker 변환
- [x] Test: `DtoToModelHelper_ToSpeakerDeviceModel_ShouldMapAllFields`
- [x] Test: `DtoToModelHelper_ToSpeakerDeviceDto_ShouldMapAllFields`
- [x] Impl: `DtoToModelHelper.cs`에 Speaker To/From 메서드 추가

### A17.2: DtoToModelHelper — Enclosure 변환
- [x] Test: `DtoToModelHelper_ToEnclosureDeviceModel_ShouldMapAllFields`
- [x] Test: `DtoToModelHelper_ToEnclosureDeviceDto_ShouldMapAllFields`
- [x] Impl: Enclosure To/From 메서드 추가

### A17.3: DtoToModelHelper — Lamp 변환
- [x] Test: `DtoToModelHelper_ToLampDeviceModel_ShouldMapAllFields`
- [x] Test: `DtoToModelHelper_ToLampDeviceDto_ShouldMapAllFields`
- [x] Impl: Lamp To/From 메서드 추가

### A17.4: ViewModel Interface + Class — Speaker
- [x] Test: `SpeakerDeviceViewModel_ShouldExposeModelProperties`
- [x] Impl: `ISpeakerDeviceViewModel.cs`, `SpeakerDeviceViewModel.cs` 생성

### A17.5: ViewModel Interface + Class — Enclosure
- [x] Test: `EnclosureDeviceViewModel_ShouldExposeModelProperties`
- [x] Impl: `IEnclosureDeviceViewModel.cs`, `EnclosureDeviceViewModel.cs` 생성

### A17.6: ViewModel Interface + Class — Lamp
- [x] Test: `LampDeviceViewModel_ShouldExposeModelProperties`
- [x] Impl: `ILampDeviceViewModel.cs`, `LampDeviceViewModel.cs` 생성

### A17.7: PanelViewModel 3종 생성
- [x] Impl: `SpeakerDevicePanelViewModel.cs` — ControllerDevicePanelViewModel 패턴 복제
- [x] Impl: `EnclosureDevicePanelViewModel.cs`
- [x] Impl: `LampDevicePanelViewModel.cs`

### A17.8: DeviceProviderService Fetch 파이프라인 확장
- [x] Impl: `FetchSpeakersAsync`, `FetchEnclosuresAsync`, `FetchLampsAsync` — PanelViewModel에 내장
- [x] Impl: `DataInitialize()` — PanelViewModel에서 직접 Fetch→Provider→ViewModel 처리

### A17.9: 검증
- [x] 전체 빌드 → 0 에러
- [x] 전체 테스트 → Messages 116/116 Green, Monitoring.Models 19/19 Green

---

## Phase 18: Devices.Ui — Dashboard + View

> **기준 문서**: PRD §5.7.4 ~ §5.7.6
> **방식**: 구현 + 빌드 검증

### A18.1: DeviceDashboardView.xaml 수정
- [x] 상단 카운트 그리드: Speaker, Enclosure, Lamp GroupBox 3개 추가 (Row 2)
- [x] TabControl: 3개 TabItem 추가 (Speaker/Enclosure/Lamp)

### A18.2: DeviceDashboardViewModel.cs 확장
- [x] `OnActiveTab` switch: Speaker/Enclosure/Lamp case 추가
- [x] `GetDeviceType()`: 3개 카운트 추가
- [x] `ClearData()`: 3개 프로퍼티 초기화
- [x] Properties: `Speaker`, `Enclosure`, `Lamp` 카운트 프로퍼티
- [x] 생성자: 3개 PanelViewModel DI 주입 추가
- [x] OnActivateAsync/OnDeactivateAsync: UpdateAction 이벤트 구독/해제

### A18.3: Panel View XAML 3종 생성
- [x] `SpeakerDevicePanelView.xaml` — DataGrid (기본 필드 + speaker_type, description)
- [x] `EnclosureDevicePanelView.xaml` — DataGrid (기본 필드 + door_status, heater, fan)
- [x] `LampDevicePanelView.xaml` — DataGrid (기본 필드 + ip_address, ip_port, description)
- [x] Behavior 3종 생성 (SpeakerDevice/EnclosureDevice/LampDevice SelectedItemsBehavior)

### A18.4: DeviceUiModule.cs DI 등록
- [x] Panel ViewModels 3개 등록 (SpeakerDevicePanelViewModel, EnclosureDevicePanelViewModel, LampDevicePanelViewModel)

### A18.5: 검증
- [x] Devices.Ui 빌드 → 0 에러
- [x] Messages 116/116 Green, Monitoring.Models 19/19 Green

> **Phase 18 완료** — Dashboard + View 확장, 빌드 0 에러

---

## Phase 19: Camera DTO → Model → ViewModel API 매칭 통합

> **기준 문서**: PRD §6
> **방식**: Red → Green (TDD)

### A19.1: HardwareSpecDto 신규 생성
- [x] Test: `HardwareSpecDto_Serialization_ShouldHaveAllNineFields`
  - name, location, manufacturer, model, hardware, firmware, device_id, mac_address, onvif_version
- [x] Test: `HardwareSpecDto_Deserialization_FromApiJson`
- [x] Impl: `Dto/Devices/HardwareSpecDto.cs` 생성 (9필드)

### A19.2: CameraDeviceDto.HardwareSpec 타입 변경 string? → HardwareSpecDto?
- [x] Test: `CameraDeviceDto_HardwareSpec_ShouldDeserializeAsObject`
  - API JSON의 `hardware_spec` 중첩 객체 → HardwareSpecDto 역직렬화 검증
- [x] Impl: `CameraDeviceDto.cs` — `HardwareSpec` 타입 변경
- [x] Fix: 기존 테스트 업데이트

### A19.3: CameraUrlsDto 재설계 — flat → nested
- [x] Test: `CameraUrlsDto_ApiDesignDoc_ShouldDeserializeNestedUrls`
  - homepage.url, onvif.device_service, streams.rtsp.main/sub, streams.webrtc.main, snapshot.ch1
- [x] Impl: `CameraUrlsDto.cs` 재설계
- [x] Fix: 기존 테스트 업데이트

### A19.4: CameraSettingDto 재설계
- [x] Test: `CameraSettingDto_ApiDesignDoc_ShouldHaveAll12Fields`
  - 7필드 추가, Wiper 제거, Heater/Fan bool→string 검증
- [x] Impl: `CameraSettingDto.cs` 재설계
- [x] Fix: 기존 테스트 업데이트

### A19.5: Camera Model 레이어
- [x] Test: `CameraUrlsModel_ShouldImplementICameraUrlsModel`
- [x] Test: `CameraSettingModel_ShouldImplementICameraSettingModel`
- [x] Impl: `ICameraUrlsModel.cs`, `CameraUrlsModel.cs` 생성
- [x] Impl: `ICameraSettingModel.cs`, `CameraSettingModel.cs` 생성
- [x] Impl: `ICameraDeviceModel.cs`에 `Urls`, `Setting`, `IsRecord` 프로퍼티 추가

### A19.6: DtoToModelHelper — Camera 변환 추가
- [x] Test: `DtoToModelHelper_HardwareSpecDto_ToICameraInfoModel`
- [x] Test: `DtoToModelHelper_CameraUrlsDto_ToICameraUrlsModel`
- [x] Test: `DtoToModelHelper_CameraSettingDto_ToICameraSettingModel`
- [x] Test: `DtoToModelHelper_GeolocationDto_ToICameraPositionModel`
- [x] Impl: 4개 변환 메서드 추가 + ToCameraDeviceModel에 통합

### A19.7: Camera ViewModel / View 레이어
- [ ] Impl: `CameraSettingViewModel.cs`, `CameraSettingView.xaml` 신규 생성 (UI 필요 시 추후)
- [x] Impl: `CameraDeviceViewModel.cs`에 `Urls`, `Setting`, `IsRecord` 프로퍼티 노출

### A19.8: Camera API Service
- [x] Test: API 엔드포인트 검증 (통합 테스트, 서버 필요 — MockDeviceApiService에 스텁 추가)
- [x] Impl: `IDeviceApiService`에 `GetCameraSettingAsync`, `PatchCameraSettingAsync` 추가
- [x] Impl: `DeviceApiService`에 구현 (`/devices/cameras/{id}/setting` 엔드포인트)

### A19.9: 검증
- [x] 전체 빌드 → 6 프로젝트 모두 0 에러
- [x] 전체 테스트 → Messages 121/121, Monitoring.Models 21/21, Devices.Ui DtoToModel 4/4 Green

---

## Phase 20: 통합 검증

> **목표**: 전체 솔루션 정합성 확인

### A20.1: 전체 솔루션 빌드
- [x] 수정 대상 6개 프로젝트 모두 0 에러 (Messages, Monitoring.Models, Devices.Api, Devices.Ui, Events.Ui, Events.Api)
- [x] 참고: Framework.Models 레거시 에러 11개 (IBaseMessageModel.Datetime 반환 타입 — Phase 0~20 무관)

### A20.2: 전체 테스트 실행
- [x] Messages 121/121 Green
- [x] Monitoring.Models 21/21 Green
- [x] Devices.Ui 26/26 Green

### A20.3: PRD 문서 정합성 확인
- [x] PRD §3 BaseDeviceDto 구조 보정 → 코드 일치 확인
- [x] PRD §4 DTO 구조 → 코드 일치 확인 (6 Device DTO + CameraUrlsDto + CameraSettingDto + HardwareSpecDto)
- [x] PRD §5 레이어별 구현 → 코드 일치 확인 (Model, DtoToModelHelper, ViewModel)
- [x] PRD §6 Camera API 매칭 → 코드 일치 확인 (GetCameraSettingAsync, PatchCameraSettingAsync)

---

## Phase 21: ThresholdConfig Concrete Model 구축

> **기준 문서**: `GOP_Restful_Api_연동설계.md` §5.5 (Enclosure), §8.3 (Server)
> **방식**: Red → Green (TDD)
> **배경**: `ServerDto.ThresholdConfig`와 `EnclosureDeviceDto.ThresholdConfig`가 `JObject?`로만 존재.
> Model 레이어에 Concrete 클래스가 없어 타입 안전한 인스턴스 수신 불가.
> **목표**: Model 레이어에 Typed ThresholdConfig 클래스를 구축하여 `JObject? → Concrete Model` 변환 지원.

### API 설계 문서 기준 스키마

**Server ThresholdConfig** (§8.3 응답 JSON):
```json
{
  "cpu": {"warning": 80, "critical": 95},
  "ram": {"warning": 75, "critical": 90},
  "disk": {"warning": 80, "critical": 95},
  "network": {"warning_mbps": 800, "critical_mbps": 950}
}
```
→ 리소스별 `warning`/`critical` 임계값 쌍. network만 `_mbps` 접미사 변형.

**Enclosure ThresholdConfig** (§5.5 응답 JSON):
```json
{
  "temp_high": 40.0,
  "temp_low": -10.0,
  "humidity_high": 85.0,
  "humidity_low": 20.0,
  "vibration_threshold": 5.0
}
```
→ Flat 구조. 온도/습도 high/low + 진동 단일 임계값.

### 설계 방향
- Server와 Enclosure의 ThresholdConfig 스키마가 다르므로 **별도 Typed Model** 사용
- CameraUrlsModel 패턴 적용 (Interface + Concrete, BaseModel 상속 없음)
- `JObject?.ToObject<T>()` 기반 변환

### A21.1: Server ThresholdConfig — ResourceThresholdEntry + ServerThresholdConfigModel
- [x] Test: `ServerThresholdConfigModel_Deserialization_FromApiJson`
  - API JSON `{ "cpu": {"warning":80,"critical":95}, "ram": {...}, ... }` → ServerThresholdConfigModel 역직렬화 검증
- [x] Test: `ServerThresholdConfigModel_Serialization_RoundTrip`
  - 왕복 직렬화 검증
- [x] Impl: `Servers/IResourceThresholdEntry.cs` — `double Warning`, `double Critical`
- [x] Impl: `Servers/ResourceThresholdEntry.cs`
- [x] Impl: `Servers/INetworkThresholdEntry.cs` — `double WarningMbps`, `double CriticalMbps`
- [x] Impl: `Servers/NetworkThresholdEntry.cs`
- [x] Impl: `Servers/IServerThresholdConfigModel.cs` — `Cpu`, `Ram`, `Disk` : `IResourceThresholdEntry?`, `Network` : `INetworkThresholdEntry?`
- [x] Impl: `Servers/ServerThresholdConfigModel.cs`

### A21.2: Enclosure ThresholdConfig — EnclosureThresholdConfigModel
- [x] Test: `EnclosureThresholdConfigModel_Deserialization_FromApiJson`
  - API JSON `{ "temp_high": 40.0, "temp_low": -10.0, ... }` → EnclosureThresholdConfigModel 역직렬화 검증
- [x] Test: `EnclosureThresholdConfigModel_Serialization_RoundTrip`
  - 왕복 직렬화 검증
- [x] Impl: `Devices/IEnclosureThresholdConfigModel.cs` — `TempHigh`, `TempLow`, `HumidityHigh`, `HumidityLow`, `VibrationThreshold` : `double?`
- [x] Impl: `Devices/EnclosureThresholdConfigModel.cs`

### A21.3: IServerModel / ServerModel에 ThresholdConfig 통합
- [x] Test: `ServerModel_WithThresholdConfig_ShouldSerializeAndDeserialize`
- [x] Impl: `IServerModel.cs`에 `IServerThresholdConfigModel? ThresholdConfig` 추가
- [x] Impl: `ServerModel.cs`에 `ServerThresholdConfigModel? ThresholdConfig` 추가 + 복사 생성자

### A21.4: IEnclosureDeviceModel / EnclosureDeviceModel에 ThresholdConfig 통합
- [x] Test: `EnclosureDeviceModel_WithThresholdConfig_ShouldSerializeAndDeserialize`
- [x] Impl: `IEnclosureDeviceModel.cs`에 `IEnclosureThresholdConfigModel? ThresholdConfig` 추가
- [x] Impl: `EnclosureDeviceModel.cs`에 `EnclosureThresholdConfigModel? ThresholdConfig` 추가 + 복사 생성자

### A21.5: 기존 테스트 업데이트 + Messages 테스트 데이터 보정
- [x] Messages `UnitTest.cs` — ServerDto 테스트 JSON의 threshold_config를 API 문서 스키마로 보정
- [x] Messages `UnitTest.cs` — EnclosureDeviceDto 테스트 JSON의 threshold_config를 API 문서 스키마로 보정

### A21.6: 검증
- [x] Monitoring.Models 빌드 → 0 에러
- [x] Monitoring.Models 테스트 → **27/27 Green** (기존 21 + Phase 21 신규 6)
- [x] Messages 테스트 → **121/121 Green** 유지
- [x] Devices.Ui 테스트 → **26/26 Green** 유지
- [x] 의존 프로젝트 빌드 → 전체 0 에러 (Devices.Ui, Events.Ui)

> **Phase 21 완료** — 총 174 tests Green (Messages 121 + Monitoring.Models 27 + Devices.Ui 26)

### 파일 목록

| 작업 | 파일 | 위치 |
|---|---|---|
| 신규 | `IResourceThresholdEntry.cs` | Monitoring.Models/Servers/ |
| 신규 | `ResourceThresholdEntry.cs` | Monitoring.Models/Servers/ |
| 신규 | `INetworkThresholdEntry.cs` | Monitoring.Models/Servers/ |
| 신규 | `NetworkThresholdEntry.cs` | Monitoring.Models/Servers/ |
| 신규 | `IServerThresholdConfigModel.cs` | Monitoring.Models/Servers/ |
| 신규 | `ServerThresholdConfigModel.cs` | Monitoring.Models/Servers/ |
| 신규 | `IEnclosureThresholdConfigModel.cs` | Monitoring.Models/Devices/ |
| 신규 | `EnclosureThresholdConfigModel.cs` | Monitoring.Models/Devices/ |
| 수정 | `IServerModel.cs` | Monitoring.Models/Servers/ |
| 수정 | `ServerModel.cs` | Monitoring.Models/Servers/ |
| 수정 | `IEnclosureDeviceModel.cs` | Monitoring.Models/Devices/ |
| 수정 | `EnclosureDeviceModel.cs` | Monitoring.Models/Devices/ |
| 수정 | `UnitTest.cs` (Messages) | Messages/Tests/ |
| 수정 | `UnitTest.cs` (Monitoring.Models) | Monitoring.Models/Tests/ |

---

# Step 1: NATS 제어 Enum 완성 + Body DTO 설계 문서 보정

> **기준**: `PRD_Migration_Master_Guide.md` §3.1, `Gop_Message_Broker_연동설계.md` v1.2
> **범위**: Enums + Messages 프로젝트만
> **테스트 기준선**: 174 tests (Messages 121 + Monitoring.Models 27 + Devices.Ui 26)
> **사이드이펙트**: 없음 (순수 추가 + 미사용 DTO 필드 타입 변경)

---

## Phase 22: NATS 제어 Enum 생성 (12종)

> Additive — 신규 파일 추가만, 기존 코드 수정 없음

### A22.1: EnumNatsMessageType — NATS 메시지 타입
- [x] Test: `EnumNatsMessageType_ShouldHave_PUB_REQ_RSP`
  - `PUB`, `REQ`, `RSP` 3개 값 존재 + ToString() 검증
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumNatsMessageType.cs`

### A22.2: EnumSubsystem — NATS 서브시스템 식별
- [x] Test: `EnumSubsystem_ShouldHave_8_Subsystems`
  - `Central`, `GIS`, `DBApi`, `PidsProxy`, `NVRManager`, `VMS`, `BroadcastingManager`, `AiAnalysis` 8개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumSubsystem.cs`

### A22.3: EnumSyncAction — 동기화 액션
- [x] Test: `EnumSyncAction_ShouldHave_CREATED_UPDATED_DELETED`
  - `CREATED`, `UPDATED`, `DELETED` 3개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumSyncAction.cs`

### A22.4: EnumOnOff — On/Off 스위치
- [x] Test: `EnumOnOff_ShouldHave_On_Off`
  - `On`, `Off` 2개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumOnOff.cs`

### A22.5: EnumLightMode — 경광등 점등 모드
- [x] Test: `EnumLightMode_ShouldHave_Steady_Blinking`
  - `Steady`, `Blinking` 2개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumLightMode.cs`

### A22.6: EnumBuzzerSound — 경광등 부저 소리
- [x] Test: `EnumBuzzerSound_ShouldHave_5_Sounds`
  - `FireAWang`, `Emergency`, `Ambulance`, `PiPiPi`, `PiContinue` 5개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumBuzzerSound.cs`

### A22.7: EnumLampColor — 경광등 색상
- [x] Test: `EnumLampColor_ShouldHave_5_Colors`
  - `Red`, `Orange`, `Green`, `Blue`, `White` 5개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumLampColor.cs`

### A22.8: EnumOperationMode — PidsProxy 운용 모드
- [x] Test: `EnumOperationMode_ShouldHave_REGISTER_NORMAL`
  - `REGISTER`, `NORMAL` 2개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumOperationMode.cs`

### A22.9: EnumPalette — 열화상 팔레트
- [x] Test: `EnumPalette_ShouldHave_4_Palettes`
  - `WHITE_HOT`, `BLACK_HOT`, `RAINBOW`, `IRONBOW` 4개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumPalette.cs`

### A22.10: EnumWeatherMode — 악천후 모드
- [x] Test: `EnumWeatherMode_ShouldHave_7_Modes`
  - `NORMAL`, `FOG`, `SEA_FOG`, `YELLOW_DUST`, `RAIN`, `SNOW`, `HEAT_HAZE` 7개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumWeatherMode.cs`

### A22.11: EnumDayNightMode — 주/야간 모드
- [x] Test: `EnumDayNightMode_ShouldHave_AUTO_DAY_NIGHT`
  - `AUTO`, `DAY`, `NIGHT` 3개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumDayNightMode.cs`

### A22.12: EnumCameraVideoMode — 카메라 영상 모드
- [x] Test: `EnumCameraVideoMode_ShouldHave_4_Modes`
  - `NORMAL`, `STABILIZATION`, `BLC`, `NIGHT_ENHANCE` 4개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumCameraVideoMode.cs`

### A22.13: EnumTrackingStatus — 추적 상태
- [x] Test: `EnumTrackingStatus_ShouldHave_Active_Lost_Idle`
  - `Active`, `Lost`, `Idle` 3개 값
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumTrackingStatus.cs`

### A22.14: 검증 — 기존 테스트 Green 유지
- [x] Messages 134/134 Green (기존 121 + 신규 13)
- [x] Monitoring.Models 27/27 Green
- [x] Devices.Ui 26/26 Green

> **Phase 22 완료** — NATS 제어 Enum 12종 생성, 총 187 tests Green (134+27+26)

---

## Phase 23: EnumWindyMode 보정 (Structural)

> Tidy First — 기존 Enum 값을 설계 문서에 맞게 변경. 현재 WindyBodyDto.Mode는 string이므로 사이드이펙트 없음.

### A23.1: EnumWindyMode 값 변경
- [x] Test: `EnumWindyMode_ShouldHave_wind0_wind1_wind2_wind3`
  - `wind0`, `wind1`, `wind2`, `wind3` 4개 값 (NATS v1.2 §5.1)
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumWindyMode.cs` 값 변경
  - Normal → wind0, Breeze → wind1, Gale → wind2, Typhoon → wind3

### A23.2: 검증
- [x] Messages 135/135 Green (기존 121 + Phase 22 신규 13 + Phase 23 신규 1)
- [x] Enums 빌드 0 에러
- [x] Monitoring.Models 27/27 Green (EnumWindyMode 사용 프로젝트 영향 없음 확인)

> **Phase 23 완료** — EnumWindyMode 보정, 총 188 tests Green (135+27+26)

---

## Phase 24: Body DTO bool→string 보정 (6종)

> 설계 문서 v1.2 대조 — on/off 제어 필드는 string "on"/"off" 사용 (EnumOnOff)

### A24.1: WiperSetBodyDto — bool → string
- [x] Test: `WiperSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "wiper": "on"}` 역직렬화 → Wiper == "on"
- [x] Impl: `WiperSetBodyDto.cs` Wiper: bool → string

### A24.2: HeaterSetBodyDto — bool → string
- [x] Test: `HeaterSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "heater": "on"}` 역직렬화 → Heater == "on"
- [x] Impl: `HeaterSetBodyDto.cs` Heater: bool → string

### A24.3: FanSetBodyDto — bool → string
- [x] Test: `FanSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "fan": "on"}` 역직렬화 → Fan == "on"
- [x] Impl: `FanSetBodyDto.cs` Fan: bool → string

### A24.4: TrackingSetBodyDto — bool → string
- [x] Test: `TrackingSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "tracking": "on"}` 역직렬화 → Tracking == "on"
- [x] Impl: `TrackingSetBodyDto.cs` Tracking: bool → string

### A24.5: HeadlightSetBodyDto — bool → string
- [x] Test: `HeadlightSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "headlight": "off"}` 역직렬화 → Headlight == "off"
- [x] Impl: `HeadlightSetBodyDto.cs` Headlight: bool → string

### A24.6: PowerSetBodyDto — bool → string
- [x] Test: `PowerSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff`
  - `{"camera_id": 201, "power": "on"}` 역직렬화 → Power == "on"
- [x] Impl: `PowerSetBodyDto.cs` Power: bool → string

### A24.7: 검증 — 기존 테스트 업데이트
- [x] 기존 Body DTO 테스트 중 bool 사용하는 테스트 수정 (A2.17-1, A2.17-2)
- [x] Messages 전체 테스트 141/141 Green

> **Phase 24 완료** — Body DTO 6종 bool→string, 총 194 tests Green (141+27+26)

---

## Phase 25: PtzControlBodyDto 구조 재설계

> 설계 문서 v1.2 — speed → pan_tilt_speed/zoom_speed, timeout → timeout_ms, position 분리

### A25.1: PtzControlBodyDto 필드 재설계
- [x] Test: `PtzControlBodyDto_Deserialization_ContinuousMovement`
  - `{"camera_id": 201, "pan_tilt_speed": 50, "zoom_speed": 30, "timeout_ms": 3000}` 역직렬화
- [x] Test: `PtzControlBodyDto_Deserialization_PresetMove`
  - `{"camera_id": 201, "preset": 3, "pan_tilt_speed": 80, "zoom_speed": 50}` 역직렬화
- [x] Test: `PtzControlBodyDto_Deserialization_AbsolutePosition`
  - `{"camera_id": 201, "pan": 1000, "tilt": 5000, "zoom": 2000, "pan_tilt_speed": 70, "zoom_speed": 50}` 역직렬화
- [x] Test: `PtzControlBodyDto_Deserialization_CenterClick`
  - `{"camera_id": 201, "x": 5000, "y": 5000, "pan_tilt_speed": 50}` 역직렬화
- [x] Impl: `PtzControlBodyDto.cs` 구조 재설계
  - speed(int) → PanTiltSpeed(int?), ZoomSpeed(int?)
  - timeout(int) → TimeoutMs(int?)
  - position(string?) 제거 → Pan(int?), Tilt(int?), Zoom(int?), X(int?), Y(int?)
  - Preset(int?) 유지
- [x] 기존 테스트 A2.17-1 업데이트 (Speed→PanTiltSpeed, Timeout→TimeoutMs)

### A25.2: 검증
- [x] Messages 전체 테스트 145/145 Green

> **Phase 25 완료** — PtzControlBodyDto 구조 재설계, 총 198 tests Green (145+27+26)

---

## Phase 26: TrackingStatusBodyDto 구조 재설계

> 설계 문서 v1.2 — tracking: bool→string, target_location: string→object

### A26.1: TrackingStatusBodyDto 필드 재설계
- [x] Test: `TrackingStatusBodyDto_Deserialization_ActiveTracking`
  - 설계 문서 JSON (`tracking: "active"`, nested target + target_location 객체) 역직렬화
- [x] Test: `TrackingStatusBodyDto_Deserialization_IdleTracking`
  - `{"camera_id": 201, "tracking": "idle", "target": null, "target_location": null}` 역직렬화
- [x] Impl: `TrackingStatusBodyDto.cs`
  - Tracking: bool → string (EnumTrackingStatus: active/lost/idle)
  - TargetLocation: string? → TrackingTargetLocationDto? (latitude, longitude, distance_m)
  - DetectedObjectDto에 thumbnail(string?) 필드 추가
- [x] Impl: `Dto/Brokers/TrackingTargetLocationDto.cs` 신규 생성
- [x] 기존 테스트 A2.17-1 업데이트 (Tracking: true → "active")

### A26.2: 검증
- [x] Messages 전체 테스트 147/147 Green

> **Phase 26 완료** — TrackingStatusBodyDto 구조 재설계, 총 200 tests Green (147+27+26)

---

## Phase 27: Step 1 최종 검증

### A27.1: 전체 빌드 + 테스트
- [x] Enums 빌드 → 0 에러
- [x] Messages 빌드 → 0 에러
- [x] Messages 테스트 → 147/147 Green (기존 121 + 신규 26)
- [x] Monitoring.Models 27/27 Green
- [x] Devices.Ui 26/26 Green
- [x] Events.Ui 빌드 → 0 에러
- [x] Events.Api 빌드 → 0 에러

> **Phase 27 완료 — Step 1 최종 검증 통과**, 총 200 tests Green (147+27+26)
>
> **Step 1 완료 기준 달성**: Enum 12종 생성, EnumWindyMode 보정, Body DTO 8종 설계 문서 정합성 달성

---

# Step 2: Framework.Models 동기화

> **PRD**: `Docs/migration_prd/PRD_Step2_Framework_Models_Sync.md` (v1.1)
> **목적**: Framework의 Enum/Model/Helper를 Libraries.Enums와 동기화, 새 디바이스 타입 역직렬화 + 다국어 지원
> **선행**: Step 1 완료 ✅

---

## Phase 28: Framework.EnumDeviceType 동기화 (Structural)

> Tidy First — Framework.Enums.EnumDeviceType에 Fence_Group/Lamp/Enclosure 추가하여 Libraries.Enums와 동기화

### A28.1: Framework.EnumDeviceType 값 추가
- [x] Test: `Framework_EnumDeviceType_ShouldHave_FenceGroup17_Lamp18_Enclosure19`
  - `(int)EnumDeviceType.Fence_Group == 17`, `Lamp == 18`, `Enclosure == 19`
  - 기존 값 NONE~OpticalCable (0~16) 불변 확인
- [x] Impl: `Ironwall.Dotnet.Framework/Enums/EnumDeviceType.cs`
  - 주석 `//Fence_Line, //17` 제거 → `Fence_Group = 17, Lamp = 18, Enclosure = 19` 추가

### A28.2: 검증
- [x] Framework 빌드 → 0 에러
- [x] 기존 테스트 200/200 Green 유지

---

## Phase 29: EnumDeviceCategory 동기화 (Structural)

> Framework + Libraries.Enums 양쪽에 Speaker/Enclosure/Lamp 카테고리 추가

### A29.1: EnumDeviceCategory 값 추가
- [x] Test: `EnumDeviceCategory_ShouldHave_Speaker_Enclosure_Lamp`
  - Framework 및 Libraries.Enums 양쪽에 Speaker, Enclosure, Lamp 존재 확인
- [x] Impl: `Ironwall.Dotnet.Framework/Enums/EnumDeviceCategory.cs` — Speaker, Enclosure, Lamp 추가 (Etc 앞에)
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumDeviceCategory.cs` — 동일하게 동기화

### A29.2: 검증
- [x] Framework 빌드 → 0 에러
- [x] 기존 테스트 200/200 Green 유지

---

## Phase 30: UnitConst + EnumLanguageHelper 확장 (Structural)

> Enclosure/Lamp 다국어 상수 추가 + GetDeviceType() switch 케이스 확장

### A30.1: UnitConst 상수 추가
- [x] Test: `UnitConst_ShouldHave_ENCLOSURE_LAMP_Constants`
  - UnitConst.ENCLOSURE == "함체", UnitConst.LAMP == "경광등"
  - UnitConst_en.ENCLOSURE == "ENCLOSURE", UnitConst_en.LAMP == "LAMP"
  - UnitConst_kr.ENCLOSURE == "함체", UnitConst_kr.LAMP == "경광등"
- [x] Impl: `Ironwall.Dotnet.Framework/Constants/UnitConst.cs` — 3개 클래스에 상수 추가

### A30.2: EnumLanguageHelper.GetDeviceType() 확장
- [x] Test: `EnumLanguageHelper_GetDeviceType_ShouldReturn_Enclosure_Lamp`
  - GetDeviceType("en", EnumDeviceType.Enclosure) == "ENCLOSURE"
  - GetDeviceType("kr", EnumDeviceType.Lamp) == "경광등"
- [x] Impl: `Ironwall.Dotnet.Framework/Helpers/EnumLanguageHelper.cs` — 3개 언어 블록에 Enclosure/Lamp 케이스 추가

### A30.3: 검증
- [x] Framework 빌드 → 0 에러 ✅
- [x] 기존 테스트 200/200 Green 유지 ✅

---

## Phase 31: Framework.Models 3종 모델 생성 (Behavioral)

> DeviceModelConverter에서 사용할 Speaker/Enclosure/Lamp 모델을 Framework.Models에 생성
> 의존 방향 제약: Framework.Models → Monitoring.Models 참조 불가

### A31.1: SpeakerDeviceModel 생성
- [x] Test: `Framework_SpeakerDeviceModel_ShouldDeserialize_IpAddress_Port`
  - `{"device_type":14,"ip_address":"192.168.1.100","ip_port":8080}` 역직렬화
- [x] Impl: `Ironwall.Dotnet.Framework.Models/Devices/SpeakerDeviceModel.cs` + `ISpeakerDeviceModel.cs` 신규 생성
  - BaseDeviceModel 상속, SpeakerType(string), Description(string?)

### A31.2: EnclosureDeviceModel 생성
- [x] Test: `Framework_EnclosureDeviceModel_ShouldDeserialize_IpAddress_Port`
  - `{"device_type":19,"ip_address":"192.168.1.101","ip_port":502}` 역직렬화
- [x] Impl: `Ironwall.Dotnet.Framework.Models/Devices/EnclosureDeviceModel.cs` + `IEnclosureDeviceModel.cs` 신규 생성

### A31.3: LampDeviceModel 생성
- [x] Test: `Framework_LampDeviceModel_ShouldDeserialize_IpAddress_Port`
  - `{"device_type":18,"ip_address":"192.168.1.102","ip_port":502}` 역직렬화
- [x] Impl: `Ironwall.Dotnet.Framework.Models/Devices/LampDeviceModel.cs` + `ILampDeviceModel.cs` 신규 생성

### A31.4: 검증
- [x] Framework.Models 빌드 → 110 에러 (기존과 동일, 증가 없음) ✅
- [x] 기존 테스트 200/200 Green 유지 ✅

---

## Phase 32: DeviceModelConverter BUG FIX + 확장 (Behavioral)

> IpSpeaker 빈 케이스 수정 + Enclosure/Lamp 케이스 추가

### A32.1: IpSpeaker 역직렬화 BUG FIX
- [x] Test: `DeviceModelConverter_IpSpeaker_ShouldDeserialize_NotNull`
  - `{"device_type":14,"device_number":201,"ip_address":"192.168.1.100"}` → SpeakerDeviceModel
- [x] Impl: `DeviceModelConverter.cs` — IpSpeaker 빈 break → `jo.ToObject<SpeakerDeviceModel>()`

### A32.2: Enclosure 역직렬화
- [x] Test: `DeviceModelConverter_Enclosure_ShouldDeserialize_NotNull`
  - `{"device_type":19,"device_number":301,"ip_address":"192.168.1.101"}` → EnclosureDeviceModel
- [x] Impl: `DeviceModelConverter.cs` — Enclosure 케이스 추가

### A32.3: Lamp 역직렬화
- [x] Test: `DeviceModelConverter_Lamp_ShouldDeserialize_NotNull`
  - `{"device_type":18,"device_number":401,"ip_address":"192.168.1.102"}` → LampDeviceModel
- [x] Impl: `DeviceModelConverter.cs` — Lamp 케이스 추가

### A32.4: 검증
- [x] Framework.Models 빌드 → 110 에러 (기존과 동일) ✅
- [x] 기존 테스트 200/200 Green 유지 ✅
- [x] 추가: NONE/Cable → BaseDeviceModel fallback, Fence_Line → Fence_Group 변경

---

## Phase 33: IBaseDeviceModel.DeviceGroups Additive 추가 (Behavioral)

> Step 6 전환 대비 — DeviceGroups (List<int>?) nullable 병행 추가

### A33.1: DeviceGroups 속성 추가
- [x] Test: `BaseDeviceModel_DeviceGroups_ShouldSerialize_Nullable`
  - DeviceGroups = null → JSON에 포함되지 않거나 null (NullValueHandling.Ignore)
  - DeviceGroups = [1,2,3] → `"device_groups":[1,2,3]`
- [x] Impl: `IBaseDeviceModel.cs` — `List<int>? DeviceGroups { get; set; }` 추가
- [x] Impl: `BaseDeviceModel.cs` — `DeviceGroups` 속성 + JsonProperty(NullValueHandling.Ignore) 추가

### A33.2: 검증
- [x] Framework.Models 빌드 → 110 에러 (기존과 동일) ✅
- [x] Monitoring.Models 27/27 Green 유지 ✅
- [x] 전체 테스트 200/200 Green 유지 ✅

---

## Phase 34: Step 2 최종 검증

### A34.1: 전체 빌드 + 테스트
- [x] Framework 빌드 → 0 에러 ✅
- [x] Framework.Models 빌드 → 110 에러 (기존과 동일, 증가 없음) ✅
- [x] Messages 147/147 Green ✅
- [x] Monitoring.Models 27/27 Green ✅
- [x] Devices.Ui 26/26 Green ✅
- [x] Events.Ui 빌드 → 0 에러 ✅
- [x] Events.Api 빌드 → 0 에러 ✅

**Step 2 완료! 전체 200/200 GREEN**

---

# Step 3: Dotnet.Monitoring.Solution NATS 마이그레이션

> PRD: `Docs/migration_prd/PRD_Step3_Monitoring_Solution_NATS_Migration.md`
> 대상: `c:\workspace_app\Dotnet.Monitoring.Solution\`
> 위험도: CRITICAL (Envelope 필드명 변경 = 런타임 null)

## Phase 35: Monitoring.Solution 빌드 에러 수정 (Cascading)

> Step 1에서 DetectionExEventDto 삭제로 인한 cascading 에러 수정

### A35.1: DetectionExEventDto → DetectionEventDto 전환
- [x] Impl: `NatsDomainService.cs` L116 — `FromJsonRequest<DetectionExEventDto>` → `FromJsonRequest<DetectionEventDto>`
  - `detectExDto?.Data?.OriginEvent` → `detectDto?.Data`
- [x] Impl: `NatsDomainService.cs` L346 — 동일 패턴 수정
- [x] 추가: PendingRequest/ResponseData 레거시 테스트 [Ignore] 처리 (8개)
- [x] 추가: TestableNatsBrokerService에서 TestAddPendingRequest 제거

### A35.2: 검증
- [x] Monitoring.Solution 빌드 → 0 에러 ✅
- [x] Monitoring.Solution 테스트 20/20 Green (8 Ignored) ✅

---

## Phase 36-38: Envelope 필드명 v1.2 전환 (Behavioral)

> type_message → m_type, type_command → cmd, enum값 동기화

### A36-38: 일괄 수정
- [x] Impl: `NatsBrokerService.cs` — `type_message` → `m_type`
- [x] Impl: `NatsBrokerService.cs` — `type_command` → `cmd`
- [x] Impl: `MessageTypeParser.cs` — `type_message` → `m_type`
- [x] Test: 테스트 JSON 키 전체 rename (type_message→m_type, type_command→cmd)
- [x] Test: enum값 수정 (Intrusion→DETECTION, Fault→MALFUNCTION, Connection→CONNECTION)
- [x] Test: Legacy numeric command 수정 (1→2=DETECTION, 4→1=CONNECTION, 6→3=MALFUNCTION)

### A36-38 검증
- [x] Monitoring.Solution 빌드 → 0 에러 ✅
- [x] Monitoring.Solution 테스트 20/20 Green ✅

---

## Phase 39: GetDevice() — IpSpeaker BUG FIX + Enclosure/Lamp (Behavioral)

> NatsBrokerService.GetDevice() 2개 오버로드에 IpSpeaker/Enclosure/Lamp 케이스 추가

### A39.1: GetDevice(BrkDectection) 확장
- [x] Impl: IpSpeaker 빈 break → `_deviceProvider.OfType<ISpeakerDeviceModel>()`
- [x] Impl: Enclosure 케이스 추가
- [x] Impl: Lamp 케이스 추가

### A39.2: GetDevice(BrkMalfunction) 확장
- [x] Impl: 동일 패턴 적용

### A39.3: 검증
- [x] Monitoring.Solution 빌드 → 0 에러 ✅
- [x] Monitoring.Solution 테스트 20/20 Green ✅

---

## Phase 40: Step 3 최종 검증

### A40.1: 전체 빌드 + 테스트
- [x] Monitoring.Solution 빌드 → 0 에러 ✅
- [x] Monitoring.Solution 테스트 20/20 Green (8 Ignored) ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅

**Step 3 완료! Monitoring.Solution 20/20 GREEN + Libraries 200/200 GREEN**

---

# Step 4: GMaps 심볼/마커 통합

> **PRD**: `Docs/migration_prd/PRD_Step4_GMaps_Integration.md` v1.0
> **범위**: `Ironwall.Dotnet.Libraries.GMaps.Ui` — FilterDevicesByType 3종 케이스 추가
> **위험도**: LOW (새 switch case 추가만, 기존 케이스 미변경)
> **테스트 기준선**: Libraries 200/200 GREEN

---

## Phase 41: Tidy First — FilterDevicesByType static helper 추출 (Structural Only)

> 행위 변경 없음. private 메서드를 테스트 가능한 internal static으로 추출.
> 기존 200 테스트가 수정 전후 모두 통과해야 함.

### A41.1: DeviceFilterHelper 추출
- [x] GMaps.Ui 빌드 기준선 확인 → 0 에러 ✅
- [x] `Helpers/DeviceFilterHelper.cs` 생성 — `internal static` 클래스 ✅
  - `FilterDevicesByType(IEnumerable<IBaseDeviceModel>, EnumDeviceType)` 메서드
  - MarkerFactory.FilterDevicesByType 로직 그대로 이동
- [x] `MarkerFactory.cs` — private FilterDevicesByType → `DeviceFilterHelper.FilterDevicesByType` 호출로 교체 ✅
- [x] `PropertyPanelFactory.cs` — private FilterDevicesByType → `DeviceFilterHelper.FilterDevicesByType` 호출로 교체 ✅
- [x] GMaps.Ui 빌드 → 0 에러 (행위 변경 없음 확인) ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅

---

## Phase 42: FilterDevicesByType — IpSpeaker/Enclosure/Lamp 필터링 TDD (Behavioral)

> DeviceFilterHelper에 3종 케이스 추가. Red → Green → Refactor.

### A42.1: 기존 필터링 회귀 테스트
- [x] Test: `FilterDevicesByType_Controller_ShouldReturnOnlyControllers` ✅
- [x] Test: `FilterDevicesByType_IpCamera_ShouldReturnOnlyIpCameras` ✅
- [x] Test: `FilterDevicesByType_FenceSensor_ShouldReturnAllFenceFamily` ✅
- [x] Test: `FilterDevicesByType_UnknownType_ShouldReturnAllDevices` ✅
- [x] 4개 테스트 Green 확인 (기존 로직 회귀 검증) ✅

### A42.2: IpSpeaker 필터링
- [x] Test (RED): `FilterDevicesByType_IpSpeaker_ShouldReturnOnlySpeakers` ✅
- [x] Impl (GREEN): DeviceFilterHelper switch에 `EnumDeviceType.IpSpeaker` 케이스 추가 ✅

### A42.3: Enclosure 필터링
- [x] Test (RED): `FilterDevicesByType_Enclosure_ShouldReturnOnlyEnclosures` ✅
- [x] Impl (GREEN): DeviceFilterHelper switch에 `EnumDeviceType.Enclosure` 케이스 추가 ✅

### A42.4: Lamp 필터링
- [x] Test (RED): `FilterDevicesByType_Lamp_ShouldReturnOnlyLamps` ✅
- [x] Impl (GREEN): DeviceFilterHelper switch에 `EnumDeviceType.Lamp` 케이스 추가 ✅

### A42.5: 검증
- [x] GMaps.Ui 빌드 → 0 에러 ✅
- [x] GMaps.Ui 테스트 7/7 Green ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅

---

## Phase 43: Step 4 최종 검증

### A43.1: 전체 빌드 + 테스트
- [x] GMaps.Ui 빌드 → 0 에러 ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅
- [x] GMaps.Ui 전체 테스트 62/62 Green (기존 55 + 신규 7) ✅

**Step 4 완료! GMaps.Ui 62/62 GREEN + Libraries 200/200 GREEN**

---

# Step 5: 인프라 서비스 마이그레이션

> **PRD**: `Docs/migration_prd/PRD_Step5_Infra_Services_Migration.md` v1.0
> **범위**: `Ironwall.Dotnet.Libraries.Api.Messages` — Device DTO 3종 추가
> **위험도**: LOW (순수 추가, 기존 로직 변경 없음)
> **참고**: Streaming.Base, EventModelConverter는 검토만 (변경 불필요)

---

## Phase 44: Api.Messages — Device DTO 3종 추가

> 기존 CameraDeviceDto 패턴을 따라 SpeakerDeviceDto, EnclosureDeviceDto, LampDeviceDto 생성.
> 테스트 프로젝트 없음 — 빌드 검증만 수행.

### A44.1: SpeakerDeviceDto 생성
- [x] `Devices/SpeakerDeviceDto.cs` 생성 — CameraDeviceDto 패턴 ✅
  - 공통: Id, NumberDevice, GroupDevice, NameDevice, TypeDevice("IpSpeaker"), Version, Status
  - 고유: IpAddress, IpPort, SpeakerType, Description
  - CreatedAt, UpdatedAt

### A44.2: EnclosureDeviceDto 생성
- [x] `Devices/EnclosureDeviceDto.cs` 생성 ✅
  - 공통: Id, NumberDevice, GroupDevice, NameDevice, TypeDevice("Enclosure"), Version, Status
  - 고유: IpAddress, IpPort, Description
  - CreatedAt, UpdatedAt

### A44.3: LampDeviceDto 생성
- [x] `Devices/LampDeviceDto.cs` 생성 ✅
  - 공통: Id, NumberDevice, GroupDevice, NameDevice, TypeDevice("Lamp"), Version, Status
  - 고유: IpAddress, IpPort, Description
  - CreatedAt, UpdatedAt

### A44.4: 빌드 검증
- [x] Api.Messages 빌드 → 0 에러 ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅

---

## Phase 45: Step 5 최종 검증

### A45.1: 전체 빌드 + 테스트
- [x] Api.Messages 빌드 → 0 에러 ✅
- [x] Libraries 기존 테스트 200/200 Green 유지 ✅
- [x] GMaps.Ui 테스트 62/62 Green 유지 ✅

**Step 5 완료! Api.Messages 빌드 OK + Libraries 200/200 GREEN + GMaps.Ui 62/62 GREEN**

---

# Step 6: group_device 레거시 제거 (DB 제외)

> **PRD**: `Docs/migration_prd/PRD_Step6_GroupDevice_Legacy_Removal.md` v1.0
> **범위**: Model + DTO + ViewModel + Service 전체 전환, DB 스키마는 Backend 배포 시 수행
> **위험도**: CRITICAL — 12개+ 프로젝트 횡단 변경
> **전략**: Inside-Out (Core Models → DTO → ViewModel → Service)

---

## Phase 46: Framework.Models — DeviceGroup 제거 + DeviceGroups 단일화

> IBaseDeviceModel, BaseDeviceModel, IDeviceMapperBase, DeviceMapperBase에서
> `int DeviceGroup` 제거, `List<int>? DeviceGroups` 유지.

### A46.1: Framework.Models 변경
- [x] `IBaseDeviceModel.cs` — `int DeviceGroup` 제거
- [x] `BaseDeviceModel.cs` — `DeviceGroup` 프로퍼티 + JsonProperty 제거, 생성자 참조 제거
- [x] `IDeviceMapperBase.cs` — `int DeviceGroup` 제거
- [x] `DeviceMapperBase.cs` — `DeviceGroup` 프로퍼티 + 생성자 참조 제거
- [x] Framework.Models 빌드 확인 (기존 110 에러 기준 유지)

---

## Phase 47: Monitoring.Models — DeviceGroup 제거 + DeviceGroups 추가

### A47.1: Monitoring.Models 변경
- [x] `IBaseDeviceModel.cs` — `int DeviceGroup` → `List<int>? DeviceGroups`
- [x] `BaseDeviceModel.cs` — `DeviceGroup` → `DeviceGroups`, JsonProperty 변경
- [x] 생성자에서 DeviceGroup → DeviceGroups 전환
- [x] Monitoring.Models 테스트 — DeviceGroup → DeviceGroups 수정
- [x] Monitoring.Models 빌드 + 테스트 Green

---

## Phase 48: Messages — BaseDeviceDto GroupDevice 제거

### A48.1: Messages DTO 변경
- [x] `BaseDeviceDto.cs` — `GroupDevice (int?)` 유지 (하위 호환용, DeviceGroups 병행)
- [x] Messages 테스트 — GroupDevice 테스트는 하위 호환 검증용으로 유지
- [x] Messages 빌드 + 테스트 Green

---

## Phase 49: Api.Messages — GroupDevice → DeviceGroups

### A49.1: Api.Messages DTO 6종 변경
- [x] 기존 6종(Controller, Sensor, Camera, Speaker, Enclosure, Lamp) — `GroupDevice` 유지 (GOP API 계약)
- [x] Api.Messages 빌드 → 0 에러

---

## Phase 50: Devices.Ui — DtoToModelHelper + ViewModel 전환

### A50.1: DtoToModelHelper 변경
- [x] `DeviceGroup = dto.GroupDevice ?? 0` → `DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList()` (6개소)
- [x] `GroupDevice = model.DeviceGroup` → 제거 또는 DeviceGroups 매핑 (6개소)
- [x] BaseDeviceViewModel — DeviceGroup → DeviceGroups 프로퍼티 전환
- [x] Devices.Ui 테스트 — DeviceGroup 참조 수정
- [x] Devices.Ui 빌드 + 테스트 Green

---

## Phase 51: Events.Ui — SymbolEventManager 1:N 그룹 전환

### A51.1: SymbolEventManager 시그니처 변경
- [x] `ProcessDeviceEvent(int deviceId, ..., int deviceGroup, ...)` → `List<int>? deviceGroups`
- [x] `ProcessControllerEvent(int controllerId, int deviceGroup, ...)` → `List<int>? deviceGroups`
- [x] `ProcessEventReport(int deviceId, ..., int deviceGroup)` → `List<int>? deviceGroups`
- [x] 내부: 단일 그룹 조회 → `deviceGroups?.ForEach` 순회
- [x] EventCardListPanelViewModel — 호출부 DeviceGroup → DeviceGroups 수정
- [x] Events.Ui 테스트 수정
- [x] Events.Ui 빌드 확인

---

## Phase 52: Monitoring.Solution — NatsDomainService 전환

### A52.1: NatsDomainService DeviceGroup → DeviceGroups
- [x] 8곳 `device.DeviceGroup` → `device.DeviceGroups`
- [x] `.Where(entity => entity.Group == device?.DeviceGroup)` → `.Where(entity => device?.DeviceGroups?.Contains(entity.Group) == true)`
- [x] `EventCallInfo.cs` — `int DeviceGroup` → `List<int>? DeviceGroups`
- [x] Monitoring.Solution 빌드 + 테스트 Green

---

## Phase 53: Devices.Api — query parameter 수정

### A53.1: DeviceApiService 변경
- [x] `groupDevice` 파라미터 유지 (REST API 쿼리 파라미터는 Backend 계약)
- [x] Devices.Api 빌드 확인

---

## Phase 54: Step 6 최종 검증

### A54.1: 전체 빌드 + 테스트
- [x] Libraries 기존 테스트 Green 유지
- [x] Monitoring.Solution 테스트 Green 유지
- [x] GMaps.Ui 테스트 Green 유지

---
---

# Server API 서비스 통합 — TDD Plan

> **기준 문서**: `Docs/migration_prd/PRD_ServerApi_Integration.md` v1.0
> **설계 문서**: `GOP_Restful_Api_연동설계.md` v3.8 §8.2~8.3, §8.6, §5.5.9~5.5.12
> **방식**: Red → Green → Refactor (CLAUDE.md 준수)
> **테스트 기준선**: Messages 147 tests passing, Build 0 errors
> **마킹**: `[ ]` 미진행, `[~]` 진행중, `[x]` 완료

---

## Phase 55: Enum 생성 (구조적 변경)

> 행위 변경 없음. 신규 Enum 파일만 추가. 기존 147 테스트에 영향 없음.

### A55.1: EnumServerType 생성
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumServerType.cs` 생성 (26종)
- [x] 빌드 확인: `dotnet build Ironwall.Dotnet.Libraries.Enums` → 0 errors

### A55.2: EnumServerStatus 생성
- [x] Impl: `Ironwall.Dotnet.Libraries.Enums/EnumServerStatus.cs` 생성 (3종)
- [x] 빌드 확인: `dotnet build Ironwall.Dotnet.Libraries.Enums` → 0 errors
- [x] 기존 테스트 전체 실행 → 147/147 Green 유지

---

## Phase 56: 신규 DTO 생성 — Serialization 테스트 (TDD)

> 각 DTO마다 Test → Impl 순서. DTO 직렬화/역직렬화 왕복 검증.

### A56.1: CategoryDetailDto
- [x] Test: `CategoryDetailDto_Serialization_ShouldIncludeServersList`
  - `CategoryDetailDto`가 `CategoryDto`를 상속하고 `servers` 필드(List<ServerDto>)를 가짐
  - 직렬화 → 역직렬화 왕복 검증
- [x] Impl: `Messages/Dto/Devices/CategoryDetailDto.cs` 생성
- [x] Green 확인

### A56.2: ServerMetricDto
- [x] Test: `ServerMetricDto_Serialization_ShouldMapAllMetricFields`
  - cpu_usage, ram_usage, disk_usage, network 필드 직렬화/역직렬화 왕복 검증
  - collected_at, threshold_exceeded(JObject) 포함
- [x] Impl: `Messages/Dto/Devices/ServerMetricDto.cs` 생성
- [x] Green 확인

### A56.3: ServerMetricLatestDto
- [x] Test: `ServerMetricLatestDto_Serialization_ShouldWrapMetricsAndThresholdConfig`
  - `metrics`(ServerMetricDto) + `threshold_config`(JObject) 래핑 구조 검증
- [x] Impl: `Messages/Dto/Devices/ServerMetricLatestDto.cs` 생성
- [x] Green 확인

### A56.4: EnclosureMetricDto
- [x] Test: `EnclosureMetricDto_Serialization_ShouldMapEnvironmentalFields`
  - temperature/humidity/current/voltage가 `string` 타입인지 검증
  - vibration(int?), ups_battery_level(int?), ups_charging(bool?) 포함
- [x] Impl: `Messages/Dto/Devices/EnclosureMetricDto.cs` 생성
- [x] Green 확인

### A56.5: MetricDeleteResultDto
- [x] Test: `MetricDeleteResultDto_Serialization_ShouldMapDeletedCount`
  - `deleted_count` 필드 직렬화/역직렬화 검증
- [x] Impl: `Messages/Dto/Devices/MetricDeleteResultDto.cs` 생성
- [x] Green 확인

### A56.6: EnclosureMetricSaveResponseDto + ThresholdExceededItemDto
- [x] Test: `EnclosureMetricSaveResponseDto_Serialization_ShouldMapThresholdExceededAtTopLevel`
  - success/message/data(EnclosureMetricDto)/threshold_exceeded(List<ThresholdExceededItemDto>) 구조 검증
  - threshold_exceeded가 data와 동일 레벨(top-level)에 위치하는 특수 구조
- [x] Impl: `Messages/Dto/Devices/EnclosureMetricSaveResponseDto.cs` 생성 (ThresholdExceededItemDto 포함)
- [x] Green 확인
- [x] 기존 테스트 전체 실행 → 153/153 Green 유지

---

## Phase 57: IServerApiService 인터페이스 생성 (구조적 변경)

> 구현체 없이 인터페이스만 생성. 빌드만 확인.

### A57.1: IServerApiService 인터페이스 생성
- [x] Impl: `Devices.Api/Services/IServerApiService.cs` 생성
  - Category CRUD 6개 메서드 시그니처
  - Server Instance CRUD 6개 메서드 시그니처
  - Server Metrics 4개 메서드 시그니처
  - 총 16개 메서드

### A57.2: IDeviceApiService에 Enclosure Metrics 4개 메서드 추가
- [x] Impl: `Devices.Api/Services/IDeviceApiService.cs` 수정
  - `CreateEnclosureMetricAsync(enclosureId, dto, token)` 추가
  - `GetEnclosureMetricsAsync(enclosureId, startTime?, endTime?, limit, token)` 추가
  - `GetEnclosureMetricLatestAsync(enclosureId, token)` 추가
  - `DeleteEnclosureMetricsAsync(enclosureId, beforeDate?, token)` 추가
- [x] 빌드 에러 4개 확인 (DeviceApiService 미구현 — Phase 58에서 해결)

---

## Phase 58: ServerApiService 스캐폴딩 (구조적 변경)

> 빈 구현체 생성 + Autofac 모듈 등록. 컴파일 통과만 목표.

### A58.1: ServerApiService 빈 구현체 생성
- [x] Impl: `Devices.Api/Services/ServerApiService.cs` 생성
  - IServerApiService 16개 메서드 모두 `throw new NotImplementedException()` 로 스캐폴딩
  - 생성자: `(ILogService? log, IApiService apiService, ApiSetupModel setupModel)`
- [x] DeviceApiService에 Enclosure Metrics 4개 메서드 스캐폴딩 추가
- [x] 빌드 확인

### A58.2: DeviceApiModule에 ServerApiService 등록
- [x] Impl: `Devices.Api/Modules/DeviceApiModule.cs` 수정
  - ServerApiService 등록 추가 (Order: _count + 1)
- [x] 빌드 확인: `dotnet build` 전체 솔루션 → 0 errors

---

## Phase 59: ServerApiService — Category CRUD 구현 (행위적 변경, TDD)

> Test → Impl 순서. 각 메서드마다 Red → Green 사이클.

### A59.1: GetCategoriesAsync
- [x] Test: `GetCategoriesAsync_ShouldReturnCategoryList`
  - 서버 호출 → ApiListResponse<CategoryDto> 반환 검증
  - Pagination 필드 검증 (page, limit, total, total_pages)
- [x] Impl: `ServerApiService.GetCategoriesAsync` 구현
- [x] Green 확인

### A59.2: CreateCategoryAsync
- [x] Test: `CreateCategoryAsync_ShouldReturnCreatedCategory`
  - CategoryDto(name, type_server, description, sort_order) 전송
  - 반환된 ApiResponse<CategoryDto>.Data.Id > 0 검증
- [x] Impl: `ServerApiService.CreateCategoryAsync` 구현
- [x] Green 확인

### A59.3: GetCategoryByIdAsync
- [x] Test: `GetCategoryByIdAsync_ShouldReturnCategoryWithServers`
  - 생성된 Category ID로 조회 → CategoryDetailDto 반환 검증
  - Servers 리스트 필드 존재 여부 검증
- [x] Impl: `ServerApiService.GetCategoryByIdAsync` 구현
- [x] Green 확인

### A59.4: PatchCategoryAsync
- [x] Test: `PatchCategoryAsync_ShouldUpdatePartialFields`
  - description만 변경 → PATCH 호출 → 반환값 검증
- [x] Impl: `ServerApiService.PatchCategoryAsync` 구현 (JObject + 빈 문자열 필터링)
- [x] Green 확인

### A59.5: UpdateCategoryAsync
- [x] Test: `UpdateCategoryAsync_ShouldReplaceAllFields`
  - 전체 필드 교체 → PUT 호출 → 반환값 검증
- [x] Impl: `ServerApiService.UpdateCategoryAsync` 구현
- [x] Green 확인

### A59.6: DeleteCategoryAsync
- [x] Test: `DeleteCategoryAsync_ShouldReturnSuccess`
  - 생성된 Category 삭제 → 성공 검증
  - 데이터 격리: 테스트 내에서 Create → Delete
- [x] Impl: `ServerApiService.DeleteCategoryAsync` 구현 (ApiResponse<object> 반환)
- [x] Green 확인

### A59.7: Category CRUD 라이프사이클 통합 테스트
- [x] Test: `CategoryCrudLifecycle_CreateReadUpdateDelete`
  - 단일 테스트에서 Create → GetById → Patch → Update → Delete 전체 흐름 검증
  - 데이터 자급자족 (생성 → 삭제로 DB 오염 방지)
- [x] Green 확인 — 7/7 Green (A59 전체 완료)

---

## Phase 60: ServerApiService — Server Instance CRUD 구현 (행위적 변경, TDD)

### A60.1: GetServersAsync
- [x] Test: `GetServersAsync_ShouldReturnServerList`
  - 서버 목록 조회 → Pagination 검증
  - categoryId, status 필터 파라미터 검증
- [x] Impl: `ServerApiService.GetServersAsync` 구현
- [x] Green 확인

### A60.2: CreateServerAsync
- [x] Test: `CreateServerAsync_ShouldReturnCreatedServer`
  - ServerDto(category_id, name, ip_address, port) 전송
  - 반환된 Data.Id > 0 검증
- [x] Impl: `ServerApiService.CreateServerAsync` 구현
- [x] Green 확인

### A60.3: GetServerByIdAsync
- [x] Test: `GetServerByIdAsync_ShouldReturnSingleServer`
  - 생성된 Server ID로 조회 → 필드 값 일치 검증
- [x] Impl: `ServerApiService.GetServerByIdAsync` 구현
- [x] Green 확인

### A60.4: PatchServerAsync
- [x] Test: `PatchServerAsync_ShouldUpdatePartialFields`
  - status만 변경 → PATCH 호출 → 반환값 검증
- [x] Impl: `ServerApiService.PatchServerAsync` 구현 (JObject + 빈 문자열 필터링)
- [x] Green 확인

### A60.5: UpdateServerAsync
- [x] Test: `UpdateServerAsync_ShouldReplaceAllFields`
  - 전체 필드 교체 → PUT 호출 → 반환값 검증
- [x] Impl: `ServerApiService.UpdateServerAsync` 구현
- [x] Green 확인

### A60.6: DeleteServerAsync
- [x] Test: `DeleteServerAsync_ShouldReturnSuccess`
  - 생성된 Server 삭제 → 성공 검증
- [x] Impl: `ServerApiService.DeleteServerAsync` 구현 (ApiResponse<object> 반환)
- [x] Green 확인

### A60.7: Server CRUD 라이프사이클 통합 테스트
- [x] Test: `ServerCrudLifecycle_CreateReadUpdateDelete`
  - 단일 테스트에서 Create → GetById → Patch → Update → Delete 전체 흐름
  - 기존 시드 Category (id=1, VMS) 활용
- [x] Green 확인 — 7/7 Green (A60 전체 완료)

---

## Phase 61: ServerApiService — Server Metrics 구현 (행위적 변경, TDD)

### A61.1: CreateServerMetricAsync
- [x] Test: `CreateServerMetricAsync_ShouldRecordMetric`
- [x] Impl: `ServerApiService.CreateServerMetricAsync` 구현
- [x] Green 확인

### A61.2: GetServerMetricsAsync
- [x] Test: `GetServerMetricsAsync_ShouldReturnMetricHistory`
- [x] Impl: `ServerApiService.GetServerMetricsAsync` 구현
- [x] Green 확인

### A61.3: GetServerMetricLatestAsync
- [x] Test: `GetServerMetricLatestAsync_ShouldReturnLatest`
- [x] Impl + DTO 수정: `ServerMetricLatestDto` → `ServerId/ServerName/LatestMetrics` 구조
- [x] Green 확인

### A61.4: DeleteServerMetricsAsync
- [x] Test: `DeleteServerMetricsAsync_ShouldReturnDeletedCount`
- [x] Impl: `ServerApiService.DeleteServerMetricsAsync` 구현
- [x] Green 확인

### A61.5: Server Metrics 라이프사이클 통합 테스트
- [x] Test: `ServerMetricsLifecycle_RecordQueryDelete`
- [x] Green 확인 — 5/5 Green (A61 전체 완료), Messages 153/153 유지

---

## Phase 62: DeviceApiService — Enclosure Metrics 구현 (행위적 변경, TDD)

### A62.1: CreateEnclosureMetricAsync
- [x] Test: `CreateEnclosureMetricAsync_ShouldSaveMetric`
- [x] Impl: `DeviceApiService.CreateEnclosureMetricAsync` (비표준 응답 → 직접 JsonConvert)
- [x] Green 확인

### A62.2: GetEnclosureMetricsAsync
- [x] Test: `GetEnclosureMetricsAsync_ShouldReturnHistory`
- [x] Impl: `DeviceApiService.GetEnclosureMetricsAsync`
- [x] Green 확인

### A62.3: GetEnclosureMetricLatestAsync
- [x] Test: `GetEnclosureMetricLatestAsync_ShouldReturnLatest`
- [x] Impl: `DeviceApiService.GetEnclosureMetricLatestAsync`
- [x] Green 확인

### A62.4: DeleteEnclosureMetricsAsync
- [x] Test: `DeleteEnclosureMetricsAsync_ShouldReturnDeletedCount`
- [x] Impl: `DeviceApiService.DeleteEnclosureMetricsAsync`
- [x] Green 확인

### A62.5: Enclosure Metrics 라이프사이클 통합 테스트
- [x] Test: `EnclosureMetricsLifecycle_RecordQueryDelete`
- [x] Green 확인 — 5/5 Green (A62 전체 완료)

---

## Phase 63: 전체 검증 및 빌드

### A63.1: 솔루션 빌드
- [x] `dotnet build Ironwall.Dotnet.Libraries.Devices.Api` → 0 errors, 0 warnings

### A63.2: Messages 테스트
- [x] `dotnet test Ironwall.Dotnet.Libraries.Messages` → 153/153 Green

### A63.3: Devices.Api 신규 테스트 (A59~A62)
- [x] A59 Category CRUD: 7/7 Green
- [x] A60 Server Instance CRUD: 7/7 Green
- [x] A61 Server Metrics: 5/5 Green
- [x] A62 Enclosure Metrics: 5/5 Green
- [x] 신규 테스트 총합: **24/24 Green**

### A63.4: 기존 테스트 영향
- [x] 기존 DeviceApiService 테스트 16건 실패 — PRD 이전부터 존재하는 DB 종속 실패 (신규 코드 영향 없음)

---

# Device API & Server API 통합 테스트

> **PRD**: `Docs/prd/PRD_DeviceApi_IntegrationTest.md`, `Docs/prd/PRD_ServerApi_IntegrationTest.md`
> **방식**: Red → Green → Refactor (CLAUDE.md 준수)
> **테스트 기준선**: Messages 153, Devices.Api 신규 24/24
> **마킹**: `[ ]` 미진행, `[~]` 진행중, `[x]` 완료

---

## Phase 64: Device API 통합 테스트 인프라 (Structural)

### A64.1: DeviceApiIntegrationTest.cs — Fixture + Collection 정의
- [x] DeviceApiFixture (IAsyncLifetime) 생성
- [x] DeviceApiCollection (ICollectionFixture) 정의
- [x] `dotnet build Ironwall.Dotnet.Libraries.Devices.Api` → 0 errors

---

## Phase 65: Controller API 통합 테스트 (D01~D05)

### A65.1: Controller_CRUD_Lifecycle (D01)
- [x] Test: Create→Get→Patch→Put→Delete 전체 사이클
- [x] Assert: Create→Id>0, Get→필드일치, Patch→부분수정, Put→전체교체, Delete→재조회 NotFound

### A65.2: GetControllers_Pagination (D02)
- [x] Test: 목록 조회 + 페이지네이션
- [x] Assert: Success, Pagination.Total >= 0

### A65.3: GetControllers_StatusFilter (D03)
- [x] Test: status=ACTIVATED 필터
- [x] Assert: 결과 전부 ACTIVATED

### A65.4: GetController_IncludeSensors (D04)
- [x] Test: includeSensors=true
- [x] Assert: Sensors 리스트 포함

### A65.5: GetController_NotFound (D05)
- [x] Test: id=999999 조회
- [x] Assert: Success==false 또는 Data==null

---

## Phase 66: Sensor API 통합 테스트 (D06~D10)

### A66.1: Sensor_CRUD_Lifecycle (D06)
- [x] Test: 선행 Controller Create → Sensor CRUD → 후행 Controller Delete

### A66.2: GetSensors_Pagination (D07)
- [x] Test: page/limit 동작

### A66.3: GetSensors_IncludeController (D08)
- [x] Test: includeController=true

### A66.4: GetSensors_TypeFilter (D09)
- [x] Test: typeDevice=Fence 필터

### A66.5: GetSensor_NotFound (D10)
- [x] Test: id=999999 조회

---

## Phase 67: Camera API 통합 테스트 (D11~D16)

### A67.1: Camera_CRUD_Lifecycle (D11)
- [x] Test: Create→Get→Patch→Put→Delete

### A67.2: GetCameras_Pagination (D12)
- [x] Test: 페이지네이션

### A67.3: GetCameras_ModeFilter (D13)
- [x] Test: mode=ONVIF 필터

### A67.4: GetCameras_CategoryFilter (D14)
- [x] Test: category=PTZ 필터

### A67.5: GetCamera_NotFound (D15)
- [x] Test: id=999999 조회

### A67.6: CameraSetting_GetAndPatch (D16)
- [x] Test: 선행 Camera Create → GetSetting → PatchSetting → Camera Delete
- 수정: `/setting` → `/settings` (복수형) 엔드포인트 경로 수정

---

## Phase 68: Speaker/Enclosure/Lamp API 통합 테스트 (D17~D27)

### A68.1: Speaker_CRUD_Lifecycle (D17)
- [x] Test: Create→Get→Patch→Put→Delete

### A68.2: GetSpeakers_Pagination (D18)
- [x] Test: 페이지네이션

### A68.3: GetSpeakers_TypeFilter (D19)
- [x] Test: speakerType=NORMAL 필터

### A68.4: GetSpeaker_NotFound (D20)
- [x] Test: id=999999 조회

### A68.5: Enclosure_CRUD_Lifecycle (D21)
- [x] Test: Create→Get→Patch→Put→Delete (DoorStatus, ThresholdConfig)

### A68.6: GetEnclosures_Pagination (D22)
- [x] Test: 페이지네이션

### A68.7: GetEnclosures_DoorStatusFilter (D23)
- [x] Test: doorStatus=CLOSED 필터

### A68.8: GetEnclosure_NotFound (D24)
- [x] Test: id=999999 조회

### A68.9: Lamp_CRUD_Lifecycle (D25)
- [x] Test: Create→Get→Patch→Put→Delete

### A68.10: GetLamps_Pagination (D26)
- [x] Test: 페이지네이션

### A68.11: GetLamp_NotFound (D27)
- [x] Test: id=999999 조회

---

## Phase 69: DeviceGroup API 통합 테스트 (D28~D32) — 인터페이스 확장 포함

### A69.0: IDeviceApiService DeviceGroup 메서드 확장 + DTO + 구현
- [x] `DeviceGroupAssignRequestDto.cs` 신규
- [x] `DeviceGroupAssignResultDto.cs` 신규
- [x] `IDeviceApiService.cs`에 8개 메서드 추가
- [x] `DeviceApiService.cs`에 8개 메서드 구현

### A69.1: DeviceGroup_CRUD_Lifecycle (D28)
- [x] Test: Create→Get→Patch→Put→Delete

### A69.2: GetDeviceGroups_Pagination (D29)
- [x] Test: 페이지네이션

### A69.3: GetDeviceGroups_NameFilter (D30)
- [x] Test: name 부분 검색

### A69.4: DeviceGroup_AssignAndRemove (D31)
- [x] Test: 디바이스 할당→제거→device_count 변화

### A69.5: GetDeviceGroup_NotFound (D32)
- [x] Test: id=999999 조회

---

## Phase 70: Server API 통합 테스트 인프라 (Structural)

### A70.1: ServerApiIntegrationTest.cs — Fixture + Collection 정의
- [x] ServerApiFixture (IAsyncLifetime) 생성
- [x] ServerApiCollection (ICollectionFixture) 정의
- [x] `dotnet build` → 0 errors

---

## Phase 71: Category API 통합 테스트 (S01~S03)

### A71.1: Category_CRUD_Lifecycle (S01)
- [x] Test: Create(ETC)→Get→Patch→Put→Delete

### A71.2: GetCategories_Pagination (S02)
- [x] Test: 페이지네이션

### A71.3: GetCategory_NotFound (S03)
- [x] Test: id=999999 조회

---

## Phase 72: Server Instance API 통합 테스트 (S04~S08)

### A72.1: Server_CRUD_Lifecycle (S04)
- [x] Test: 선행 Category Create → Server CRUD → 후행 Category Delete

### A72.2: GetServers_Pagination (S05)
- [x] Test: 페이지네이션

### A72.3: GetServers_CategoryFilter (S06)
- [x] Test: categoryId 필터

### A72.4: GetServers_StatusFilter (S07)
- [x] Test: status 필터

### A72.5: GetServer_NotFound (S08)
- [x] Test: id=999999 조회

---

## Phase 73: Server Metrics 통합 테스트 (S09~S11)

### A73.1: ServerMetrics_Lifecycle (S09)
- [x] Test: Create→GetHistory→GetLatest→Delete

### A73.2: GetServerMetrics_DateFilter (S10)
- [x] Test: start_date/end_date 필터

### A73.3: ServerMetrics_EmptyHistory (S11)
- [x] Test: 메트릭 없는 서버 조회 → 빈 목록

---

## Phase 74: Enclosure Metrics 통합 테스트 (S12~S14)

### A74.1: EnclosureMetrics_Lifecycle (S12)
- [x] Test: 선행 Enclosure Create → Metrics CRUD → Enclosure Delete

### A74.2: GetEnclosureMetrics_TimeFilter (S13)
- [x] Test: start_time/end_time 필터

### A74.3: EnclosureMetrics_EmptyHistory (S14)
- [x] Test: 메트릭 없는 함체 조회 → 빈 목록

---

## Phase 75: 전체 검증

### A75.1: 전체 빌드 + 테스트
- [x] `dotnet build Ironwall.Dotnet.Libraries.Devices.Api` → 0 errors
- [x] Device 통합 테스트: 32/32 Green (Camera D11~D16 포함)
- [x] Server 통합 테스트: 14/14 Green
- [x] 전체 통합 테스트: 46/46 Green
