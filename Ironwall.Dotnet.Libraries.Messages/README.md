# Ironwall.Dotnet.Libraries.Messages

**버전**: 1.1.0
**생성자**: 이기호 (GHLEE)
**소속**: Sensorway Co., Ltd.
**생성일**: 2025-11-12
**최종 수정일**: 2025-01-18

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [프로젝트 구조](#2-프로젝트-구조)
3. [활용 방법](#3-활용-방법)
   - [3.1 RESTful API 메시지](#31-restful-api-메시지)
   - [3.2 Message Broker 메시지](#32-message-broker-메시지)
   - [3.3 DetectionExEventDto 확장 이벤트](#33-detectionexeventdto-확장-이벤트)
4. [사전정의된 타입](#4-사전정의된-타입)
5. [외부 시스템 통합 DTO](#5-외부-시스템-통합-dto)
   - [5.1 Camera_SPG (PTZ 제어)](#51-camera_spg-ptz-제어)
   - [5.2 NVR_emstone (Emstone NVR 통합)](#52-nvr_emstone-emstone-nvr-통합)
6. [업데이트 정보](#6-업데이트-정보)

---

## 1. 프로젝트 개요

**Ironwall.Dotnet.Libraries.Messages**는 GOP 통제시스템에서 사용되는 **모든 메시지 구조를 정의**하는 핵심 라이브러리입니다.

### 주요 목적

- ✅ **RESTful API 통신**: HTTP 기반 API 요청/응답 구조 제공
- ✅ **Message Broker 통신**: NATS, Redis 등 메시지 브로커 메시지 구조 제공
- ✅ **DTO 중심 설계**: 데이터 전송 객체(DTO)를 중심으로 일관된 메시지 생성
- ✅ **타입 안정성**: 제네릭과 강타입을 활용한 타입 안전성 보장
- ✅ **JSON 직렬화**: Newtonsoft.Json 기반 표준화된 직렬화 지원
- ✅ **외부 시스템 통합**: 카메라 PTZ, NVR 시스템 등 외부 장비 통합 지원

### 설계 원칙

1. **DTO-Only 패턴**: Concrete 클래스 없이 DTO만으로 메시지 생성
2. **Helper 패턴**: Extension Method를 활용한 직관적인 메시지 생성
3. **일관성**: API와 Broker 메시지에 동일한 설계 패턴 적용
4. **확장성**: 제네릭 타입을 활용한 유연한 확장 가능

---

## 2. 프로젝트 구조

```
Ironwall.Dotnet.Libraries.Messages/
│
├── Defines/                          # 핵심 메시지 구조 정의
│   ├── Apis/                         # RESTful API 메시지 정의
│   │   ├── ApiResponse.cs            # 단일 응답 래퍼
│   │   ├── ApiListResponse.cs        # 목록 응답 래퍼 (Pagination 포함)
│   │   ├── ApiError.cs               # 에러 상세 정보
│   │   ├── MetaDto.cs                # 메타데이터 (timestamp, request_id)
│   │   └── PaginationDto.cs          # 페이징 정보
│   │
│   ├── Brokers/                      # Message Broker 메시지 정의
│   │   ├── BaseBrokerMessage.cs      # Broker 메시지 기본 클래스
│   │   ├── BrokerRequest.cs          # Broker 요청 메시지
│   │   └── BrokerResponse.cs         # Broker 응답 메시지
│   │
│   └── Commons/                      # 공통 인터페이스
│       └── IEventDto.cs              # 이벤트 DTO 공통 인터페이스
│
├── Dto/                              # 데이터 전송 객체 (DTO)
│   ├── Bases/                        # 기본 DTO
│   │   └── BaseDto.cs                # 공통 기본 DTO
│   │
│   ├── Devices/                      # 디바이스 DTO
│   │   ├── CameraDeviceDto.cs        # 카메라 디바이스
│   │   ├── ControllerDeviceDto.cs    # 제어기 디바이스
│   │   └── SensorDeviceDto.cs        # 센서 디바이스
│   │
│   ├── Events/                       # 이벤트 DTO
│   │   ├── ActionEventDto.cs         # 조치 이벤트
│   │   ├── ActionEventCreateDto.cs   # 조치 이벤트 생성용
│   │   ├── ConnectionEventDto.cs     # 연결 이벤트
│   │   ├── DetectionEventDto.cs      # 탐지 이벤트
│   │   ├── DetectionExEventDto.cs    # 확장 탐지 이벤트 (v1.1.0)
│   │   ├── EventUrlsDto.cs           # 이벤트 URL 정보 (v1.1.0)
│   │   └── MalfunctionEventDto.cs    # 장애 이벤트
│   │
│   ├── Integrations/                 # 통합 DTO
│   │   └── EventMappingDto.cs        # 이벤트 매핑
│   │
│   ├── RtspPopups/                   # RTSP 팝업 DTO
│   │   └── EventCallDto.cs           # 이벤트 호출
│   │
│   ├── Camera_SPG/                   # SPG 카메라 PTZ 제어 (v1.1.0)
│   │   └── PTZDTO.cs                 # PTZ 제어 명령
│   │
│   └── NVR_emstone/                  # Emstone NVR 통합 (v1.1.0)
│       └── Camera/
│           ├── CameraDto.cs          # 카메라 정보
│           ├── CameraSourceDto.cs    # 카메라 소스 정보
│           ├── CameraOSDDto.cs       # OSD 설정
│           └── CameraPTZTourDto.cs   # PTZ 투어 설정
│
├── Helpers/                          # 메시지 생성 Helper 클래스
│   ├── ApiMessageHelper.cs           # API 메시지 변환 Helper
│   ├── BrokerMessageHelper.cs        # Broker 메시지 생성 Helper
│   ├── DetectionExEventDtoHelper.cs  # DetectionExEventDto 전용 Helper (v1.1.0)
│   └── FromEventConverter.cs         # 이벤트 변환기
│
└── Models/                           # 구체적인 메시지 모델 (레거시)
    └── Brokers/
        └── EventCallRequestMessage.cs  # (Deprecated) Concrete 타입
```

### 폴더별 역할

| 폴더 | 역할 | 설명 |
|------|------|------|
| **Defines/Apis** | API 메시지 구조 | RESTful API 응답 표준 구조 정의 |
| **Defines/Brokers** | Broker 메시지 구조 | NATS, Redis 등 메시지 브로커 메시지 구조 |
| **Dto** | 데이터 전송 객체 | 실제 비즈니스 데이터 구조 |
| **Dto/Camera_SPG** | SPG 카메라 통합 | SPG 카메라 PTZ 제어 DTO |
| **Dto/NVR_emstone** | Emstone NVR 통합 | Emstone NVR 시스템 연동 DTO |
| **Helpers** | 메시지 생성 도구 | DTO → 메시지 변환 Extension Methods |
| **Models** | (Deprecated) | 기존 Concrete 클래스 (Helper 패턴으로 대체됨) |

---

## 3. 활용 방법

### 3.1 RESTful API 메시지

#### 3.1.1 기본 개념

RESTful API 메시지는 HTTP 통신에서 사용되며, **표준화된 응답 구조**를 제공합니다.

**핵심 구조**:
- `ApiResponse<T>`: 단일 데이터 응답
- `ApiListResponse<T>`: 목록 데이터 응답 (Pagination 포함)
- `ApiError`: 에러 상세 정보
- `MetaDto`: 타임스탬프, 요청 ID 등 메타데이터
- `PaginationDto`: 페이지 번호, 총 개수 등 페이징 정보

#### 3.1.2 단일 응답 구조 (ApiResponse<T>)

**성공 응답 예제**:
```json
{
  "success": true,
  "message": "Controller retrieved successfully",
  "data": {
    "id": 1,
    "name_device": "Controller-A",
    "type_device": "Controller",
    "status": "ACTIVATED",
    "ip_address": "192.168.1.100"
  },
  "meta": {
    "timestamp": "2025-01-10T10:30:00.000Z",
    "request_id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

**에러 응답 예제**:
```json
{
  "success": false,
  "error": {
    "code": "NOT_FOUND",
    "message": "Controller not found with Id=999",
    "details": "No controller exists with the specified ID"
  },
  "meta": {
    "timestamp": "2025-01-10T10:30:00.000Z",
    "request_id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

#### 3.1.3 목록 응답 구조 (ApiListResponse<T>)

```json
{
  "success": true,
  "message": "25 items retrieved",
  "data": [
    { "id": 1, "name_device": "Sensor-1" },
    { "id": 2, "name_device": "Sensor-2" }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 25,
    "total_pages": 2
  },
  "meta": {
    "timestamp": "2025-01-10T10:30:00.000Z",
    "request_id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

#### 3.1.4 ApiMessageHelper 사용법

`ApiMessageHelper`는 HTTP 응답을 자동으로 `ApiResponse` 또는 `ApiListResponse`로 변환합니다.

**HttpResponseMessage → ApiResponse 변환**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

public async Task<ApiResponse<ControllerDeviceDto>> GetControllerAsync(int id)
{
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync($"http://api.server.com/api/devices/controllers/{id}");

    // Extension Method로 자동 변환
    var apiResponse = await response.ToApiResponseAsync<ControllerDeviceDto>();

    if (apiResponse.Success)
    {
        Console.WriteLine($"Controller: {apiResponse.Data.NameDevice}");
    }
    else
    {
        Console.WriteLine($"Error: {apiResponse.Error?.Message}");
    }

    return apiResponse;
}
```

**HttpResponseMessage → ApiListResponse 변환**:
```csharp
public async Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync()
{
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("http://api.server.com/api/devices/sensors?page=1&limit=20");

    // Extension Method로 자동 변환
    var apiResponse = await response.ToApiListResponseAsync<SensorDeviceDto>();

    if (apiResponse.Success)
    {
        Console.WriteLine($"Total: {apiResponse.Pagination?.Total}");
        foreach (var sensor in apiResponse.Data)
        {
            Console.WriteLine($"- {sensor.NameDevice}");
        }
    }

    return apiResponse;
}
```

**JSON 문자열 직접 변환**:
```csharp
// JSON → ApiResponse
var jsonString = "{\"success\":true,\"data\":{\"id\":1}}";
var apiResponse = ApiMessageHelper.FromJsonResponse<ControllerDeviceDto>(jsonString);

// ApiResponse → JSON
var response = ApiResponse<ControllerDeviceDto>.CreateSuccess(new ControllerDeviceDto());
var json = response.ToJson();
```

#### 3.1.5 성공/에러 응답 생성 (서버 측)

```csharp
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

// 성공 응답 생성
var successResponse = ApiResponse<ControllerDeviceDto>.CreateSuccess(
    data: controllerDto,
    message: "Controller retrieved successfully"
);

// 에러 응답 생성
var errorResponse = ApiResponse<ControllerDeviceDto>.CreateError(
    code: "NOT_FOUND",
    message: "Controller not found",
    details: "No controller exists with the specified ID"
);

// 목록 응답 생성
var listResponse = ApiListResponse<SensorDeviceDto>.CreateSuccess(
    data: sensorList,
    page: 1,
    limit: 20,
    total: 25,
    message: "25 sensors retrieved"
);
```

---

### 3.2 Message Broker 메시지

#### 3.2.1 기본 개념

Message Broker 메시지는 **NATS, Redis** 등 메시지 브로커 시스템에서 사용됩니다.

**핵심 구조**:
- `BrokerRequest<T>`: 요청 메시지
- `BrokerResponse<T>`: 응답 메시지
- `BaseBrokerMessage`: 공통 기본 클래스

**Subject 기반 라우팅**:
- NATS는 Subject를 사용하여 메시지 라우팅을 수행합니다
- `Command` 필드는 선택적 메타데이터로 사용됩니다

#### 3.2.2 요청 메시지 구조 (BrokerRequest<T>)

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type_message": "REQ",
  "command": "EVENT_CALL",
  "from": "CLIENT_001",
  "data": {
    "event_name": "Detection-001",
    "state": "ACTIVE"
  },
  "timestamp": "2025-01-10T10:30:00.000Z"
}
```

#### 3.2.3 응답 메시지 구조 (BrokerResponse<T>)

**성공 응답**:
```json
{
  "id": "550e8401-e29b-41d4-a716-446655440000",
  "type_message": "RSP",
  "command": "EVENT_CALL",
  "from": "SERVER",
  "request_id": "550e8400-e29b-41d4-a716-446655440000",
  "success": true,
  "message": "Event processed successfully",
  "data": {
    "result": "SUCCESS"
  },
  "timestamp": "2025-01-10T10:30:01.000Z"
}
```

**에러 응답**:
```json
{
  "id": "550e8402-e29b-41d4-a716-446655440000",
  "type_message": "RSP",
  "command": "EVENT_CALL",
  "from": "SERVER",
  "request_id": "550e8400-e29b-41d4-a716-446655440000",
  "success": false,
  "message": "Invalid event name",
  "data": null,
  "timestamp": "2025-01-10T10:30:01.000Z"
}
```

#### 3.2.4 BrokerMessageHelper 사용법

`BrokerMessageHelper`는 DTO를 `BrokerRequest` 또는 `BrokerResponse`로 쉽게 변환합니다.

**요청 메시지 생성 (Extension Method)**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;

var eventCallDto = new EventCallDto
{
    EventName = "Detection-001",
    State = "ACTIVE"
};

// Extension Method 패턴 (자동 Command 생성)
var request = eventCallDto.ToBrokerRequest(from: "CLIENT_001");
// Command 자동 설정: "EVENT_CALL" (EventCallDto → EVENT_CALL)

// Extension Method 패턴 (수동 Command 지정)
var request2 = eventCallDto.ToBrokerRequest(
    command: "EVENT_CALL",
    from: "CLIENT_001"
);

// JSON 직렬화
var json = request.ToJson();

// NATS로 발행
await natsConnection.PublishAsync("event.call", json);
```

**정적 팩토리 메서드 패턴**:
```csharp
// 정적 메서드로 생성
var request = BrokerMessageHelper.CreateRequest(
    data: eventCallDto,
    command: "EVENT_CALL",
    from: "CLIENT_001"
);
```

**응답 메시지 생성**:
```csharp
// 성공 응답 생성
var response = BrokerMessageHelper.CreateResponse(
    data: resultDto,
    requestId: "550e8400-e29b-41d4-a716-446655440000",
    from: "SERVER",
    command: "EVENT_CALL",
    message: "Success"
);

// 에러 응답 생성
var errorResponse = BrokerMessageHelper.CreateErrorResponse<ResultDto>(
    requestId: "550e8400-e29b-41d4-a716-446655440000",
    from: "SERVER",
    errorMessage: "Invalid event name",
    command: "EVENT_CALL"
);

// JSON 직렬화
var json = response.ToJson();
```

**원본 요청에 대한 응답 생성**:
```csharp
// 요청 메시지 수신
var receivedJson = await natsConnection.SubscribeAsync("event.call");
var request = BrokerMessageHelper.FromJsonRequest<EventCallDto>(receivedJson);

// 원본 요청에 대한 응답 생성 (확장 메서드)
var response = request.CreateResponseFor(
    responseData: new ResultDto { Status = "OK" },
    from: "SERVER",
    message: "Processed successfully"
);
// request.Id가 자동으로 response.RequestId에 설정됨
// request.Command가 자동으로 response.Command에 복사됨

await natsConnection.PublishAsync("event.call.response", response.ToJson());
```

**JSON 역직렬화**:
```csharp
// JSON → BrokerRequest
var receivedJson = "{\"id\":\"...\",\"command\":\"EVENT_CALL\",\"data\":{...}}";
var request = BrokerMessageHelper.FromJsonRequest<EventCallDto>(receivedJson);

// JSON → BrokerResponse
var responseJson = "{\"id\":\"...\",\"success\":true,\"data\":{...}}";
var response = BrokerMessageHelper.FromJsonResponse<ResultDto>(responseJson);
```

#### 3.2.5 NATS 통합 예제

**메시지 발행 (Publisher)**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
using NATS.Client.Core;

public async Task PublishEventCallAsync(NatsConnection nats)
{
    var dto = new EventCallDto
    {
        EventName = "Detection-001",
        State = "ACTIVE"
    };

    // DTO → BrokerRequest → JSON
    var request = dto.ToBrokerRequest(from: "NvrManager");
    var json = request.ToJson();

    // NATS Subject로 발행
    await nats.PublishAsync("event.call", json);

    Console.WriteLine($"Published to 'event.call': {json}");
}
```

**메시지 구독 (Subscriber)**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;

public class EventCallService : MessageService<EventCallService>
{
    protected override async Task RegisterSubscribers(CancellationToken token = default)
    {
        _defaultSubject = "event.call";
        await base.RegisterSubscribers(token);

        // 비동기 이벤트 핸들러 등록
        NatsSubscribeEventAsync += OnEventCallReceivedAsync;
    }

    private async Task OnEventCallReceivedAsync(MessageArgsModel args)
    {
        try
        {
            // JSON → BrokerRequest 역직렬화
            var request = BrokerMessageHelper.FromJsonRequest<EventCallDto>(args.Data);

            if (request?.Data != null)
            {
                Console.WriteLine($"Received EventCall: {request.Data.EventName}");

                // 비즈니스 로직 처리
                var result = await ProcessEventCallAsync(request.Data);

                // 응답 생성
                var response = request.CreateResponseFor(
                    responseData: new ResultDto { Status = "OK" },
                    from: "EventProcessor",
                    message: "Event processed successfully"
                );

                // 응답 발행
                await Connection.PublishAsync("event.call.response", response.ToJson());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event call: {ex.Message}");
        }
    }
}
```

---

### 3.3 DetectionExEventDto 확장 이벤트

#### 3.3.1 개요

**DetectionExEventDto**는 기본 `DetectionEventDto`를 확장하여 **NATS 메시지 브로커 전송에 최적화된 구조**를 제공합니다.

**주요 특징**:
- 이벤트 명칭 및 카테고리 추가
- RTSP URL (실시간/녹화) 정보 포함
- Composition 패턴 사용 (상속 대신 포함)
- NATS 메시지 Body 구조에 맞는 설계

**구조**:
```csharp
public class DetectionExEventDto
{
    [JsonProperty("name_event", Order = 1)]
    public string NameEvent { get; set; } = string.Empty;  // 이벤트 명칭

    [JsonProperty("category_event", Order = 2)]
    public string CategoryEvent { get; set; } = string.Empty;  // 이벤트 카테고리

    [JsonProperty("origin_event", Order = 3)]
    public DetectionEventDto OriginEvent { get; set; } = new();  // 원본 이벤트

    [JsonProperty("urls", Order = 4)]
    public EventUrlsDto Urls { get; set; } = new();  // RTSP URL 정보
}

public class EventUrlsDto
{
    [JsonProperty("live", Order = 1)]
    public string Live { get; set; } = string.Empty;  // 실시간 RTSP URL

    [JsonProperty("record", Order = 2)]
    public string Record { get; set; } = string.Empty;  // 녹화 RTSP URL
}
```

#### 3.3.2 NATS 메시지 구조 예제

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type_message": "REQ",
  "command": "DETECTION_EX_EVENT",
  "from": "SENSOR_MANAGER",
  "data": {
    "name_event": "침입탐지-카메라연동",
    "category_event": "DETECT_SENSOR_WITH_CAMERA",
    "origin_event": {
      "id": 123,
      "name_device": "Sensor-A-001",
      "type_device": "Multi",
      "type_event": "Intrusion",
      "result": "PIR_SENSOR",
      "action_reported": "True",
      "message_event": "PIR 센서 침입 탐지",
      "datetime_event": "2025-01-18T10:30:00.000Z"
    },
    "urls": {
      "live": "rtsp://192.168.1.100:554/live",
      "record": "rtsp://192.168.1.100:554/playback?start=20250118T103000"
    }
  },
  "timestamp": "2025-01-18T10:30:00.500Z"
}
```

#### 3.3.3 DetectionExEventDtoHelper 사용법

`DetectionExEventDtoHelper`는 `DetectionEventDto`를 `DetectionExEventDto`로 쉽게 변환하고 NATS 메시지로 생성합니다.

**기본 변환 (Extension Method)**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;

var detectionEvent = new DetectionEventDto
{
    Id = 123,
    NameDevice = "Sensor-A-001",
    TypeDevice = "Multi",
    TypeEvent = "Intrusion",
    Result = "PIR_SENSOR",
    ActionReported = "True",
    MessageEvent = "PIR 센서 침입 탐지",
    DatetimeEvent = "2025-01-18T10:30:00.000Z"
};

// DetectionEventDto → DetectionExEventDto 변환
var detectionExEvent = detectionEvent.ToDetectionExEvent(
    eventName: "침입탐지-카메라연동",
    category: "DETECT_SENSOR_WITH_CAMERA",
    liveUrl: "rtsp://192.168.1.100:554/live",
    recordUrl: "rtsp://192.168.1.100:554/playback?start=20250118T103000"
);

Console.WriteLine($"Event Name: {detectionExEvent.NameEvent}");
Console.WriteLine($"Category: {detectionExEvent.CategoryEvent}");
Console.WriteLine($"Live URL: {detectionExEvent.Urls.Live}");
Console.WriteLine($"Record URL: {detectionExEvent.Urls.Record}");
```

**URL 없이 변환**:
```csharp
// URL 정보 없이 변환 (Urls는 빈 문자열로 설정됨)
var detectionExEvent = detectionEvent.ToDetectionExEvent(
    eventName: "침입탐지-일반",
    category: "DETECT_SENSOR_ONLY"
);
// Urls.Live = string.Empty
// Urls.Record = string.Empty
```

**EventUrlsDto 직접 생성**:
```csharp
// EventUrlsDto 헬퍼 메서드로 생성
var urls = DetectionExEventDtoHelper.CreateEventUrls(
    liveUrl: "rtsp://192.168.1.100:554/live",
    recordUrl: "rtsp://192.168.1.100:554/playback?start=20250118T103000"
);

var detectionExEvent = new DetectionExEventDto
{
    NameEvent = "침입탐지",
    CategoryEvent = "DETECT_SENSOR_WITH_CAMERA",
    OriginEvent = detectionEvent,
    Urls = urls
};
```

**NATS BrokerRequest로 변환**:
```csharp
// DetectionExEventDto → BrokerRequest<DetectionExEventDto> 변환
var brokerRequest = detectionExEvent.ToBrokerRequest(
    from: "SENSOR_MANAGER",
    command: "DETECTION_EX_EVENT"
);

// 기본 Command 사용 (기본값: "DETECTION_EX_EVENT")
var brokerRequest2 = detectionExEvent.ToBrokerRequest(from: "SENSOR_MANAGER");

// JSON 직렬화 후 NATS로 발행
var json = brokerRequest.ToJson();
await natsConnection.PublishAsync("detection.ex.event", json);
```

**전체 워크플로우 예제**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using NATS.Client.Core;

public async Task PublishDetectionExEventAsync(
    NatsConnection nats,
    DetectionEventDto detectionEvent,
    string liveUrl,
    string recordUrl)
{
    // Step 1: DetectionEventDto → DetectionExEventDto 변환
    var detectionExEvent = detectionEvent.ToDetectionExEvent(
        eventName: "침입탐지-카메라연동",
        category: "DETECT_SENSOR_WITH_CAMERA",
        liveUrl: liveUrl,
        recordUrl: recordUrl
    );

    // Step 2: DetectionExEventDto → BrokerRequest 변환
    var request = detectionExEvent.ToBrokerRequest(from: "SENSOR_MANAGER");

    // Step 3: JSON 직렬화 및 NATS 발행
    var json = request.ToJson();
    await nats.PublishAsync("detection.ex.event", json);

    Console.WriteLine($"Published DetectionExEvent: {detectionExEvent.NameEvent}");
}
```

#### 3.3.4 이벤트 카테고리 예시

**일반적인 카테고리 값**:
- `"DETECT_SENSOR_ONLY"`: 센서 단독 탐지
- `"DETECT_SENSOR_WITH_CAMERA"`: 센서 + 카메라 연동 탐지
- `"DETECT_CAMERA_ONLY"`: 카메라 단독 탐지 (AI 분석)
- `"DETECT_COMBINED"`: 복합 탐지 (센서 + 카메라 + 기타)

**사용 예제**:
```csharp
// 센서 + 카메라 연동 탐지
var detectionEx1 = detectionEvent.ToDetectionExEvent(
    eventName: "침입탐지-001",
    category: "DETECT_SENSOR_WITH_CAMERA",
    liveUrl: "rtsp://192.168.1.100:554/live"
);

// 센서 단독 탐지 (카메라 없음)
var detectionEx2 = detectionEvent.ToDetectionExEvent(
    eventName: "침입탐지-002",
    category: "DETECT_SENSOR_ONLY"
);
```

---

## 4. 사전정의된 타입

이 섹션에서는 DTO에서 `string` 타입으로 정의되었지만, 실제로는 **Enum 값의 ToString()**으로 사용되는 타입들을 정리합니다.

> **참조**: `Ironwall.Dotnet.Libraries.Enums` 프로젝트에 정의된 Enum 타입들입니다.

### 4.1 디바이스 관련 Enum

#### 4.1.1 EnumDeviceType (디바이스 타입)

**사용 위치**: `type_device` 속성
**사용 DTO**: `CameraDeviceDto`, `ControllerDeviceDto`, `SensorDeviceDto`, `DetectionEventDto`, `ConnectionEventDto`, `MalfunctionEventDto`

**가능한 문자열 값**:
```
"NONE"           - 없음
"Controller"     - 제어기
"Multi"          - 복합 센서
"Fence"          - 펜스 센서
"Underground"    - 지중 센서
"Contact"        - 접점 센서
"PIR"            - PIR 센서
"IoController"   - IO 제어기
"Laser"          - 레이저 센서
"Cable"          - 케이블 센서
"IpCamera"       - IP 카메라
"SmartSensor"    - 스마트 센서
"SmartSensor2"   - 스마트 센서2
"SmartCompound"  - 스마트 복합
"IpSpeaker"      - IP 스피커
"Radar"          - 레이더
"OpticalCable"   - 광케이블
"Fence_Group"    - 펜스 그룹
```

**예제**:
```csharp
var controller = new ControllerDeviceDto
{
    TypeDevice = "Controller"  // EnumDeviceType.Controller.ToString()
};

var sensor = new SensorDeviceDto
{
    TypeDevice = "Multi"  // EnumDeviceType.Multi.ToString()
};

var camera = new CameraDeviceDto
{
    TypeDevice = "IpCamera"  // EnumDeviceType.IpCamera.ToString()
};
```

---

#### 4.1.2 EnumDeviceStatus (디바이스 상태)

**사용 위치**: `status` 속성
**사용 DTO**: `CameraDeviceDto`, `ControllerDeviceDto`, `SensorDeviceDto`

**가능한 문자열 값**:
```
"ACTIVATED"      - 활성화 상태
"ERROR"          - 에러 상태
"DEACTIVATED"    - 비활성화 상태
```

**예제**:
```csharp
var controller = new ControllerDeviceDto
{
    Status = "ACTIVATED"  // EnumDeviceStatus.ACTIVATED.ToString()
};
```

---

#### 4.1.3 EnumCameraMode (카메라 모드)

**사용 위치**: `mode` 속성
**사용 DTO**: `CameraDeviceDto`

**가능한 문자열 값**:
```
"NONE"           - 없음
"ONVIF"          - ONVIF 프로토콜
"EMSTONE_API"    - Emstone API
"INNODEP_API"    - Innodep API
"ETC"            - 기타
```

**예제**:
```csharp
var camera = new CameraDeviceDto
{
    Mode = "ONVIF"  // EnumCameraMode.ONVIF.ToString()
};
```

---

#### 4.1.4 EnumCameraType (카메라 타입)

**사용 위치**: `category` 속성
**사용 DTO**: `CameraDeviceDto`

**가능한 문자열 값**:
```
"NONE"           - 없음
"FIXED"          - 고정 카메라
"PTZ"            - Pan-Tilt-Zoom 카메라
"FISHEYES"       - 어안 카메라
"THERMAL"        - 열화상 카메라
```

**예제**:
```csharp
var camera = new CameraDeviceDto
{
    Category = "PTZ"  // EnumCameraType.PTZ.ToString()
};
```

---

### 4.2 이벤트 관련 Enum

#### 4.2.1 EnumEventType (이벤트 타입)

**사용 위치**: `type_event` 속성
**사용 DTO**: `ActionEventCreateDto`, `ActionEventDto`, `ConnectionEventDto`, `DetectionEventDto`, `MalfunctionEventDto`

**가능한 문자열 값**:
```
"None"           - 없음 (값: 0)
"Intrusion"      - 침입 탐지 (값: 90, 0x5A)
"ContactOn"      - 접점 켜기 (값: 86, 0x56)
"ContactOff"     - 접점 끄기 (값: 102, 0x66)
"Connection"     - 연결 보고 (값: 104, 0x68)
"Action"         - 조치 보고 (값: 192, 0xC0)
"Fault"          - 장애 보고 (값: 115, 0x73)
"WindyMode"      - 풍량 모드 (값: 118, 0x76)
```

**예제**:
```csharp
var detectionEvent = new DetectionEventDto
{
    TypeEvent = "Intrusion"  // EnumEventType.Intrusion.ToString()
};

var actionEvent = new ActionEventDto
{
    TypeEvent = "Action"  // EnumEventType.Action.ToString()
};

var connectionEvent = new ConnectionEventDto
{
    TypeEvent = "Connection"  // EnumEventType.Connection.ToString()
};

var malfunctionEvent = new MalfunctionEventDto
{
    TypeEvent = "Fault"  // EnumEventType.Fault.ToString()
};
```

---

#### 4.2.2 EnumTrueFalse (참/거짓)

**사용 위치**: `action_reported`, `status` 속성
**사용 DTO**: `DetectionEventDto`, `MalfunctionEventDto`

**가능한 문자열 값**:
```
"False"          - 거짓
"True"           - 참
```

**예제**:
```csharp
var detectionEvent = new DetectionEventDto
{
    ActionReported = "True"  // EnumTrueFalse.True.ToString()
};

var malfunctionEvent = new MalfunctionEventDto
{
    Status = "False",         // EnumTrueFalse.False.ToString()
    ActionReported = "False"  // EnumTrueFalse.False.ToString()
};
```

---

#### 4.2.3 EnumDetectionType (탐지 타입)

**사용 위치**: `result` 속성
**사용 DTO**: `DetectionEventDto`

**가능한 문자열 값**:
```
"NONE"               - 없음 (값: 0)
"CABLE_CUTTING"      - 케이블 절단 (값: 1)
"CABLE_CONNECTED"    - 케이블 연결 (값: 2)
"PIR_SENSOR"         - PIR 센서 (값: 3)
"THERMAL_SENSOR"     - 열화상 센서 (값: 5)
"VIBRATION_SENSOR"   - 진동 센서 (값: 6)
"CONTACT_SENSOR"     - 접점 센서 (값: 10)
"DISTANCE_SENSOR"    - 거리 센서 (값: 11)
```

**예제**:
```csharp
var detectionEvent = new DetectionEventDto
{
    Result = "PIR_SENSOR"  // EnumDetectionType.PIR_SENSOR.ToString()
};
```

---

#### 4.2.4 EnumFaultType (장애 타입)

**사용 위치**: `reason` 속성
**사용 DTO**: `MalfunctionEventDto`

**가능한 문자열 값**:
```
"FAULT_CONTROLLER"      - 제어기 장애 (값: 1)
"FAULT_FENCE"           - 펜스 장애 (값: 2)
"FAULT_MULTI"           - 복합 장애 (값: 3)
"FAULT_CABLE_CUTTING"   - 케이블 절단 장애 (값: 4)
"FAULT_ETC"             - 기타 장애 (값: 5)
```

**예제**:
```csharp
var malfunctionEvent = new MalfunctionEventDto
{
    Reason = "FAULT_CONTROLLER"  // EnumFaultType.FAULT_CONTROLLER.ToString()
};
```

---

### 4.3 Enum 타입 요약 테이블

| Enum 타입 | JSON 속성명 | 사용 DTO 개수 | 주요 용도 |
|-----------|------------|---------------|-----------|
| EnumDeviceType | `type_device` | 6 | 디바이스 타입 식별 |
| EnumDeviceStatus | `status` | 3 | 디바이스 작동 상태 |
| EnumEventType | `type_event` | 5 | 이벤트 분류 |
| EnumCameraMode | `mode` | 1 | 카메라 통신 모드 |
| EnumCameraType | `category` | 1 | 카메라 하드웨어 타입 |
| EnumDetectionType | `result` | 1 | 탐지 센서 타입 |
| EnumFaultType | `reason` | 1 | 장애/고장 원인 |
| EnumTrueFalse | `action_reported`, `status` | 3 | 불린 유사 값 |

---

## 5. 외부 시스템 통합 DTO

이 섹션에서는 외부 카메라 시스템 및 NVR과의 통합을 위한 DTO를 설명합니다.

### 5.1 Camera_SPG (PTZ 제어)

#### 5.1.1 개요

SPG 카메라 시스템의 **PTZ (Pan-Tilt-Zoom) 제어** 명령을 위한 DTO입니다.

**위치**: `Dto/Camera_SPG/PTZDTO.cs`

#### 5.1.2 구조

```csharp
public class PTZDTO : BaseDto
{
    /// <summary>
    /// 카메라 ID
    /// </summary>
    [JsonProperty("cameraId", Order = 2)]
    public int CameraId { get; set; }

    /// <summary>
    /// Pan 각도 (좌우 회전)
    /// </summary>
    [JsonProperty("p", Order = 3)]
    public int P { get; set; }

    /// <summary>
    /// Tilt 각도 (상하 회전)
    /// </summary>
    [JsonProperty("t", Order = 4)]
    public int T { get; set; }

    /// <summary>
    /// Zoom 레벨 (확대/축소)
    /// </summary>
    [JsonProperty("z", Order = 5)]
    public int Z { get; set; }
}
```

#### 5.1.3 JSON 예제

```json
{
  "cameraId": 101,
  "p": 45,
  "t": -30,
  "z": 2
}
```

#### 5.1.4 사용 예제

```csharp
using Ironwall.Dotnet.Libraries.Messages.Dto.Camera_SPG;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

// PTZ 제어 명령 생성
var ptzCommand = new PTZDTO
{
    CameraId = 101,
    P = 45,   // Pan: 45도 우측 회전
    T = -30,  // Tilt: 30도 하향
    Z = 2     // Zoom: 레벨 2
};

// BrokerRequest로 변환 후 NATS로 발행
var request = ptzCommand.ToBrokerRequest(
    command: "PTZ_CONTROL",
    from: "CAMERA_MANAGER"
);

await natsConnection.PublishAsync("camera.spg.ptz", request.ToJson());
```

---

### 5.2 NVR_emstone (Emstone NVR 통합)

#### 5.2.1 개요

Emstone NVR 시스템과의 통합을 위한 카메라 정보 및 설정 DTO입니다.

**위치**: `Dto/NVR_emstone/Camera/`

#### 5.2.2 CameraDto (카메라 정보)

**전체 카메라 정보를 담는 주요 DTO**입니다.

**구조**:
```csharp
public class CameraDto : BaseDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;  // 카메라 ID

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;  // 카메라 이름

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;  // IP 주소

    [JsonProperty("location")]
    public string Location { get; set; } = string.Empty;  // 설치 위치

    [JsonProperty("source")]
    public int Source { get; set; }  // 네트워크 카메라 소스 ID

    [JsonProperty("channel")]
    public int Channel { get; set; }  // 채널 ID

    [JsonProperty("connected")]
    public bool IsConnected { get; set; }  // 연결 여부

    [JsonProperty("has_signal")]
    public bool HasSignal { get; set; }  // 신호 유효 여부

    [JsonProperty("has_ptz")]
    public bool HasPtz { get; set; }  // PTZ 기능 지원 여부

    [JsonProperty("recording")]
    public bool IsRecording { get; set; }  // 녹화 상태

    [JsonProperty("force_recording")]
    public bool IsForceRecording { get; set; }  // 강제 녹화 상태

    [JsonProperty("ptz_presets")]
    public List<Dictionary<string, Dictionary<string, string>>> PtzPresets { get; set; }
        = new List<Dictionary<string, Dictionary<string, string>>>();  // PTZ 프리셋 리스트

    [JsonProperty("ptz_tours")]
    public List<Dictionary<string, List<CameraPTZTourDto>>> PtzTours { get; set; }
        = new List<Dictionary<string, List<CameraPTZTourDto>>>();  // PTZ 투어 설정

    [JsonProperty("streaming")]
    public bool IsStreaming { get; set; }  // 스트리밍 활성화 여부

    [JsonProperty("http_url")]
    public string HttpUrl { get; set; } = string.Empty;  // HTTP URL

    [JsonProperty("note")]
    public string Note { get; set; } = string.Empty;  // 비고

    [JsonProperty("osd")]
    public CameraOSDDto OsdSettings { get; set; } = new CameraOSDDto();  // OSD 설정

    [JsonProperty("ptz_type")]
    public string PtzType { get; set; } = "NONE";  // PTZ 타입 (AUTO/NONE/PTZ/ZOOM)

    [JsonProperty("purpose")]
    public string Purpose { get; set; } = string.Empty;  // 설치 목적

    [JsonProperty("shape")]
    public string Shape { get; set; } = string.Empty;  // 외형 (BULLET/DOME/BOX/PTZ)

    [JsonProperty("dewap")]  // JSON 키 오타 유지 (호환성)
    public string Dewarp { get; set; } = string.Empty;  // 왜곡 보정
}
```

**JSON 예제**:
```json
{
  "id": "CAM-101",
  "name": "Front Gate Camera",
  "address": "192.168.1.101",
  "location": "Building A - Front Gate",
  "source": 1,
  "channel": 1,
  "connected": true,
  "has_signal": true,
  "has_ptz": true,
  "recording": true,
  "force_recording": false,
  "streaming": true,
  "http_url": "http://192.168.1.101/viewer",
  "ptz_type": "PTZ",
  "purpose": "Entrance Monitoring",
  "shape": "DOME",
  "osd": {
    "text": "Front Gate",
    "size": 16,
    "color": "#FFFFFF",
    "location": "top-left"
  }
}
```

#### 5.2.3 CameraSourceDto (카메라 소스 정보)

**네트워크 카메라 소스 정보를 담는 DTO**입니다.

**구조**:
```csharp
public class CameraSourceDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;  // 카메라 이름

    [JsonProperty("address", Order = 3)]
    public string Address { get; set; } = string.Empty;  // IP 주소

    [JsonProperty("mac", Order = 4)]
    public string Mac { get; set; } = string.Empty;  // MAC 주소

    [JsonProperty("location", Order = 5)]
    public string Location { get; set; } = string.Empty;  // 위치

    [JsonProperty("channels", Order = 6)]
    public List<Dictionary<string, int>> Channels { get; set; }
        = new List<Dictionary<string, int>>();  // 채널 리스트
}
```

**JSON 예제**:
```json
{
  "name": "NVR Camera Source 1",
  "address": "192.168.1.100",
  "mac": "00:11:22:33:44:55",
  "location": "Server Room",
  "channels": [
    { "channel_1": 1 },
    { "channel_2": 2 },
    { "channel_3": 3 }
  ]
}
```

#### 5.2.4 CameraOSDDto (OSD 설정)

**카메라 화면에 표시되는 OSD (On-Screen Display) 설정 DTO**입니다.

**구조**:
```csharp
public class CameraOSDDto
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;  // OSD 텍스트

    [JsonProperty("size")]
    public int Size { get; set; }  // 폰트 크기

    [JsonProperty("color")]
    public string Color { get; set; } = string.Empty;  // 폰트 색상 (예: "#FFFFFF")

    [JsonProperty("location")]
    public string Location { get; set; } = string.Empty;
    // 위치: "top-left", "top-center", "top-right",
    //       "bottom-left", "bottom-center", "bottom-right"

    [JsonProperty("date_format")]
    public string DataFormat { get; set; } = string.Empty;  // 날짜 형식

    [JsonProperty("time_format")]
    public string TimeFormat { get; set; } = string.Empty;  // 시간 형식
}
```

**JSON 예제**:
```json
{
  "text": "Main Entrance",
  "size": 16,
  "color": "#FFFFFF",
  "location": "top-left",
  "date_format": "YYYY-MM-DD",
  "time_format": "HH:mm:ss"
}
```

#### 5.2.5 CameraPTZTourDto (PTZ 투어 설정)

**PTZ 자동 투어 설정을 담는 DTO**입니다.

**구조**:
```csharp
public class CameraPTZTourDto
{
    [JsonProperty("preset")]
    public int Preset { get; set; }  // 프리셋 번호

    [JsonProperty("duration")]
    public int Duration { get; set; }  // 지속 시간 (초)

    [JsonProperty("speed")]
    public int Speed { get; set; }  // 이동 속도
}
```

**JSON 예제**:
```json
{
  "preset": 1,
  "duration": 10,
  "speed": 50
}
```

**PTZ 투어 시퀀스 예제**:
```json
{
  "tour_1": [
    { "preset": 1, "duration": 10, "speed": 50 },
    { "preset": 2, "duration": 15, "speed": 30 },
    { "preset": 3, "duration": 10, "speed": 50 }
  ]
}
```

#### 5.2.6 사용 예제

**Emstone NVR 카메라 정보 조회**:
```csharp
using Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

public async Task<CameraDto> GetCameraInfoAsync(string cameraId)
{
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync($"http://nvr.server.com/api/cameras/{cameraId}");

    var apiResponse = await response.ToApiResponseAsync<CameraDto>();

    if (apiResponse.Success && apiResponse.Data != null)
    {
        var camera = apiResponse.Data;
        Console.WriteLine($"Camera: {camera.Name}");
        Console.WriteLine($"Connected: {camera.IsConnected}");
        Console.WriteLine($"Has PTZ: {camera.HasPtz}");
        Console.WriteLine($"Recording: {camera.IsRecording}");

        return camera;
    }

    return null;
}
```

**PTZ 투어 설정**:
```csharp
var tour = new List<CameraPTZTourDto>
{
    new CameraPTZTourDto { Preset = 1, Duration = 10, Speed = 50 },
    new CameraPTZTourDto { Preset = 2, Duration = 15, Speed = 30 },
    new CameraPTZTourDto { Preset = 3, Duration = 10, Speed = 50 }
};

var camera = new CameraDto
{
    Id = "CAM-101",
    Name = "Main Camera",
    HasPtz = true,
    PtzTours = new List<Dictionary<string, List<CameraPTZTourDto>>>
    {
        new Dictionary<string, List<CameraPTZTourDto>>
        {
            { "patrol_tour", tour }
        }
    }
};
```

---

## 6. 업데이트 정보

### v1.1.0 (2025-01-18)

#### 새로운 기능

1. **DetectionExEventDto 확장 이벤트 (신규)**
   - ✅ `DetectionExEventDto`: NATS 메시지 브로커 전송 최적화 구조
   - ✅ `EventUrlsDto`: RTSP URL (실시간/녹화) 정보 포함
   - ✅ `DetectionExEventDtoHelper`: 확장 이벤트 생성 Helper
     - `ToDetectionExEvent()`: DetectionEventDto → DetectionExEventDto 변환
     - `CreateEventUrls()`: EventUrlsDto 생성
     - `ToBrokerRequest()`: DetectionExEventDto → BrokerRequest 변환

2. **외부 시스템 통합 DTO (신규)**
   - ✅ **Camera_SPG**: SPG 카메라 PTZ 제어 DTO
     - `PTZDTO`: Pan, Tilt, Zoom 제어 명령
   - ✅ **NVR_emstone**: Emstone NVR 시스템 통합 DTO
     - `CameraDto`: 전체 카메라 정보 (20+ 속성)
     - `CameraSourceDto`: 네트워크 카메라 소스 정보
     - `CameraOSDDto`: OSD (화면 표시) 설정
     - `CameraPTZTourDto`: PTZ 자동 투어 설정

#### 문서 업데이트

- ✅ Section 3.3: DetectionExEventDto 사용법 및 예제 추가
- ✅ Section 5: 외부 시스템 통합 DTO 문서화
- ✅ 프로젝트 구조 업데이트 (Camera_SPG, NVR_emstone 폴더 추가)

#### 테스트

- ✅ DetectionExEventDtoHelper 단위 테스트 7개 추가
  - `ToDetectionExEvent_WithAllParameters_ShouldCreateCorrectly`
  - `ToDetectionExEvent_WithMinimalParameters_ShouldCreateWithEmptyUrls`
  - `CreateEventUrls_WithBothUrls_ShouldCreateCorrectly`
  - `CreateEventUrls_WithNullUrls_ShouldCreateWithEmptyStrings`
  - `ToBrokerRequest_ShouldCreateValidRequest`
  - `ToBrokerRequest_WithCustomCommand_ShouldUseCustomCommand`
  - `Integration_FullWorkflow_ShouldWorkCorrectly`
- ✅ 전체 테스트: 36/36 통과

---

### v1.0.0 (2025-11-12)

#### 초기 릴리스
- ✅ RESTful API 메시지 구조 정의 (`ApiResponse`, `ApiListResponse`)
- ✅ Message Broker 메시지 구조 정의 (`BrokerRequest`, `BrokerResponse`)
- ✅ 디바이스 DTO (`CameraDeviceDto`, `ControllerDeviceDto`, `SensorDeviceDto`)
- ✅ 이벤트 DTO (`DetectionEventDto`, `ActionEventDto`, `ConnectionEventDto`, `MalfunctionEventDto`)
- ✅ Helper 패턴 도입 (`ApiMessageHelper`, `BrokerMessageHelper`)
- ✅ DTO 중심 설계로 Concrete 클래스 제거
- ✅ Extension Method를 활용한 직관적인 메시지 생성
- ✅ Newtonsoft.Json 기반 JSON 직렬화 표준화

#### 주요 기능
1. **ApiMessageHelper**
   - `ToApiResponseAsync<T>()`: HttpResponse → ApiResponse 변환
   - `ToApiListResponseAsync<T>()`: HttpResponse → ApiListResponse 변환
   - `FromJsonResponse<T>()`: JSON 역직렬화
   - `ToJson()`: JSON 직렬화

2. **BrokerMessageHelper**
   - `ToBrokerRequest<T>()`: DTO → BrokerRequest 변환 (Extension Method)
   - `CreateResponse<T>()`: 성공 응답 생성
   - `CreateErrorResponse<T>()`: 에러 응답 생성
   - `CreateResponseFor<TRequest, TResponse>()`: 원본 요청에 대한 응답 생성
   - `ToJson()`, `FromJsonRequest<T>()`, `FromJsonResponse<T>()`: JSON 변환

3. **메시지 구조**
   - ISO 8601 타임스탬프 표준 (`yyyy-MM-ddTHH:mm:ss.fffZ`)
   - GUID 기반 메시지 ID
   - 제네릭 타입 안정성 (`where T : class`)
   - Pagination 지원 (page, limit, total, total_pages)

#### 설계 변경
- ❌ **Deprecated**: `EventCallRequestMessage` 등 Concrete 메시지 클래스
- ✅ **New**: Helper 패턴으로 DTO만으로 메시지 생성
- ✅ **Unified**: API와 Broker에 동일한 Helper 패턴 적용

---

## 라이선스

Copyright © 2025 Sensorway Co., Ltd. All rights reserved.
