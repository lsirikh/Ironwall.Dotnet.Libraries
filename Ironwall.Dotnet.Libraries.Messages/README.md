# Ironwall.Dotnet.Libraries.Messages

**버전**: 1.0.0  
**생성자**: 이기호 (GHLEE)  
**소속**: Sensorway Co., Ltd.  
**생성일**: 2025-11-12  

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [프로젝트 구조](#2-프로젝트-구조)
3. [활용 방법](#3-활용-방법)
   - [3.1 RESTful API 메시지](#31-restful-api-메시지)
   - [3.2 Message Broker 메시지](#32-message-broker-메시지)
4. [사전정의된 타입](#4-사전정의된-타입)
5. [업데이트 정보](#5-업데이트-정보)

---

## 1. 프로젝트 개요

**Ironwall.Dotnet.Libraries.Messages**는 GOP 통제시스템에서 사용되는 **모든 메시지 구조를 정의**하는 핵심 라이브러리입니다.

### 주요 목적

- ✅ **RESTful API 통신**: HTTP 기반 API 요청/응답 구조 제공
- ✅ **Message Broker 통신**: NATS, Redis 등 메시지 브로커 메시지 구조 제공
- ✅ **DTO 중심 설계**: 데이터 전송 객체(DTO)를 중심으로 일관된 메시지 생성
- ✅ **타입 안정성**: 제네릭과 강타입을 활용한 타입 안전성 보장
- ✅ **JSON 직렬화**: Newtonsoft.Json 기반 표준화된 직렬화 지원

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
│   │   └── MalfunctionEventDto.cs    # 장애 이벤트
│   │
│   ├── Integrations/                 # 통합 DTO
│   │   └── EventMappingDto.cs        # 이벤트 매핑
│   │
│   └── RtspPopups/                   # RTSP 팝업 DTO
│       └── EventCallDto.cs           # 이벤트 호출
│
├── Helpers/                          # 메시지 생성 Helper 클래스
│   ├── ApiMessageHelper.cs           # API 메시지 변환 Helper
│   ├── BrokerMessageHelper.cs        # Broker 메시지 생성 Helper
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

## 5. 업데이트 정보

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

## 참조 문서

- **GOP RESTful API 연동 설계서**: `Docs/GOP_Restful_Api_연동설계.md`
- **Messages 라이브러리 아키텍처 분석**: `Docs/Messages_라이브러리_아키텍처_분석.md`
- **Ironwall.Dotnet.Libraries.Events.Api**: API 사용 예제 참조
- **Ironwall.Dotnet.Libraries.Nats**: NATS 통합 예제 참조

---

## 라이선스

Copyright © 2025 Sensorway Co., Ltd. All rights reserved.