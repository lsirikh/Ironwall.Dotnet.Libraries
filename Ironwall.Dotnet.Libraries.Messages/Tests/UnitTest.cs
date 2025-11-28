using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/****************************************************************************
   Purpose      : Messages 라이브러리 단위 테스트
   Created By   : GHLee
   Created On   : 2025-11-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Description  : API 및 Broker 메시지 생성, 직렬화, 역직렬화 테스트
****************************************************************************/

public class ApiResponseTests
{
    #region - ApiResponse<T> 테스트 -

    [Fact]
    [Trait("Category", "API")]
    public void CreateSuccessResponse_ShouldReturnValidResponse()
    {
        // Arrange
        var testData = new ControllerDeviceDto
        {
            Id = 1,
            NameDevice = "Controller-A",
            TypeDevice = "Controller",
            Status = "ACTIVATED"
        };

        // Act
        var response = ApiResponse<ControllerDeviceDto>.CreateSuccess(
            testData,
            "Controller retrieved successfully"
        );

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("Controller retrieved successfully", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.Id);
        Assert.Equal("Controller-A", response.Data.NameDevice);
        Assert.NotNull(response.Meta);
        Assert.NotNull(response.Meta.Timestamp);
    }

    [Fact]
    [Trait("Category", "API")]
    public void CreateErrorResponse_ShouldReturnValidErrorResponse()
    {
        // Arrange & Act
        var response = ApiResponse<ControllerDeviceDto>.CreateError(
            "NOT_FOUND",
            "Controller not found",
            "No controller exists with the specified ID"
        );

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("NOT_FOUND", response.Error.Code);
        Assert.Equal("Controller not found", response.Error.Message);
        Assert.Equal("No controller exists with the specified ID", response.Error.Details);
    }

    [Fact]
    [Trait("Category", "API")]
    public void ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var testData = new SensorDeviceDto
        {
            Id = 101,
            NameDevice = "Sensor-A-1",
            TypeDevice = "Multi",
            Status = "ACTIVATED"
        };
        var response = ApiResponse<SensorDeviceDto>.CreateSuccess(testData);

        // Act
        var json = response.ToJson();

        // Assert
        Assert.NotNull(json);

        Assert.True(json?.Contains("\"success\":true"));
        Assert.True(json?.Contains("\"name_device\":\"Sensor-A-1\""));
        Assert.True(json?.Contains("\"type_device\":\"Multi\""));
    }

    [Fact]
    [Trait("Category", "API")]
    public void FromJsonResponse_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""message"": ""Operation successful"",
            ""data"": {
                ""id"": 1,
                ""name_device"": ""Test-Controller"",
                ""type_device"": ""Controller"",
                ""status"": ""ACTIVATED""
            }
        }";

        // Act
        var response = ApiMessageHelper.FromJsonResponse<ControllerDeviceDto>(json);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("Operation successful", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.Id);
        Assert.Equal("Test-Controller", response.Data.NameDevice);
    }

    #endregion

    #region - ApiListResponse<T> 테스트 -

    [Fact]
    [Trait("Category", "API")]
    public void CreateSuccessListResponse_ShouldReturnValidResponse()
    {
        // Arrange
        var testData = new List<SensorDeviceDto>
        {
            new SensorDeviceDto { Id = 101, NameDevice = "Sensor-1", TypeDevice = "Multi" },
            new SensorDeviceDto { Id = 102, NameDevice = "Sensor-2", TypeDevice = "Fence" }
        };
        var pagination = new PaginationDto
        {
            Page = 1,
            Limit = 20,
            Total = 2,
            TotalPages = 1
        };

        // Act
        var response = ApiListResponse<SensorDeviceDto>.CreateSuccess(
            testData,
            pagination,
            "2 sensors retrieved"
        );

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data?.Count);
        Assert.NotNull(response.Pagination);
        Assert.Equal(1, response.Pagination.Page);
        Assert.Equal(20, response.Pagination.Limit);
        Assert.Equal(2, response.Pagination.Total);
        Assert.Equal(1, response.Pagination.TotalPages);
    }

    [Fact]
    [Trait("Category", "API")]
    public void ListResponse_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var testData = new List<CameraDeviceDto>
        {
            new CameraDeviceDto { Id = 201, NameDevice = "Camera-1", Mode = "ONVIF", Category = "PTZ" }
        };
        var pagination = new PaginationDto { Page = 1, Limit = 10, Total = 1, TotalPages = 1 };
        var response = ApiListResponse<CameraDeviceDto>.CreateSuccess(testData, pagination);

        // Act
        var json = response.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.True(json?.Contains("\"success\":true"));
        Assert.True(json?.Contains("\"pagination\""));
        Assert.True(json?.Contains("\"page\":1"));
        Assert.True(json?.Contains("\"mode\":\"ONVIF\""));
    }

    [Fact]
    [Trait("Category", "API")]
    public void FromJsonListResponse_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""message"": ""2 items retrieved"",
            ""data"": [
                { ""id"": 1, ""name_device"": ""Sensor-1"" },
                { ""id"": 2, ""name_device"": ""Sensor-2"" }
            ],
            ""pagination"": {
                ""page"": 1,
                ""limit"": 20,
                ""total"": 2,
                ""total_pages"": 1
            }
        }";

        // Act
        var response = ApiMessageHelper.FromJsonListResponse<SensorDeviceDto>(json);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data.Count);
        Assert.Equal("Sensor-1", response.Data[0].NameDevice);
        Assert.Equal(1, response.Pagination?.Page);
    }

    #endregion
}


public class BrokerMessageTests
{
    #region - BrokerRequest<T> 생성 테스트 -

    [Fact]
    [Trait("Category", "Broker")]
    public void ToBrokerRequest_WithManualCommand_ShouldCreateValidRequest()
    {
        // Arrange
        var dto = new EventCallDto
        {
            EventName = "Detection-001",
            State = "ACTIVE"
        };

        // Act
        var request = dto.ToBrokerRequest("EVENT_CALL", "monitoring-service");

        // Assert
        Assert.NotNull(request);
        Assert.Equal("REQ", request.TypeMessage);
        Assert.Equal("EVENT_CALL", request.Command);
        Assert.Equal("monitoring-service", request.From);
        Assert.NotNull(request.Id);
        Assert.NotNull(request.Data);
        Assert.Equal("Detection-001", request.Data.EventName);
        Assert.NotNull(request.Timestamp);
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void ToBrokerRequest_WithAutoCommand_ShouldGenerateCorrectCommand()
    {
        // Arrange
        var dto = new DetectionEventDto
        {
            Id = 1001,
            TypeEvent = "Intrusion",
            Result = "PIR_SENSOR"
        };

        // Act
        var request = dto.ToBrokerRequest("client-001");

        // Assert
        Assert.NotNull(request);
        Assert.Equal("DETECTIONEVENT", request.Command);
        Assert.Equal("client-001", request.From);
        Assert.NotNull(request.Data);
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void CreateRequest_ShouldCreateValidRequest()
    {
        // Arrange
        var dto = new MalfunctionEventDto
        {
            Id = 2001,
            TypeEvent = "Fault",
            Reason = "FAULT_CONTROLLER"
        };

        // Act
        var request = BrokerMessageHelper.CreateRequest(dto, "MALFUNCTION", "server");

        // Assert
        Assert.NotNull(request);
        Assert.Equal("MALFUNCTION", request.Command);
        Assert.Equal("server", request.From);
        Assert.NotNull(request.Data);
        Assert.Equal(2001, request.Data.Id);
    }

    #endregion

    #region - BrokerResponse<T> 생성 테스트 -

    [Fact]
    [Trait("Category", "Broker")]
    public void CreateResponse_ShouldCreateValidSuccessResponse()
    {
        // Arrange
        var resultDto = new EventCallDto
        {
            EventName = "Result-001",
            State = "PROCESSED"
        };
        var requestId = Guid.NewGuid().ToString();

        // Act
        var response = BrokerMessageHelper.CreateResponse(
            resultDto,
            requestId,
            "gop-service",
            "EVENT_CALL",
            "Processed successfully"
        );

        // Assert
        Assert.NotNull(response);
        Assert.Equal("RSP", response.TypeMessage);
        Assert.Equal(requestId, response.RequestId);
        Assert.True(response.Success);
        Assert.Equal("Processed successfully", response.Message);
        Assert.Equal("gop-service", response.From);
        Assert.NotNull(response.Data);
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void CreateErrorResponse_ShouldCreateValidErrorResponse()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();

        // Act
        var response = BrokerMessageHelper.CreateErrorResponse<EventCallDto>(
            requestId,
            "error-service",
            "Invalid event name",
            "EVENT_CALL"
        );

        // Assert
        Assert.NotNull(response);
        Assert.Equal("RSP", response.TypeMessage);
        Assert.False(response.Success);
        Assert.Equal("Invalid event name", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void CreateResponseFor_ShouldCreateResponseWithRequestContext()
    {
        // Arrange
        var requestDto = new EventCallDto { EventName = "Test", State = "PENDING" };
        var request = requestDto.ToBrokerRequest("EVENT_CALL", "client");
        var responseDto = new EventCallDto { EventName = "Test", State = "COMPLETED" };

        // Act
        var response = request.CreateResponseFor(responseDto, "server", "Completed");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Id, response.RequestId);
        Assert.Equal(request.Command, response.Command);
        Assert.True(response.Success);
        Assert.Equal("Completed", response.Message);
    }

    #endregion

    #region - BrokerMessage JSON 직렬화/역직렬화 테스트 -

    [Fact]
    [Trait("Category", "Broker")]
    public void BrokerRequest_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var dto = new DetectionEventDto
        {
            Id = 1001,
            GroupEvent = "group_001",
            TypeEvent = "Intrusion",
            Result = "PIR_SENSOR"
        };
        var request = dto.ToBrokerRequest("DETECTION", "sensor-service");

        // Act
        var json = request.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.True(json?.Contains("\"type_message\":\"REQ\""));
        Assert.True(json?.Contains("\"type_command\":\"DETECTION\""));
        Assert.True(json?.Contains("\"from\":\"sensor-service\""));
        Assert.True(json?.Contains("\"type_event\":\"Intrusion\""));
        Assert.True(json?.Contains("\"result\":\"PIR_SENSOR\""));
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void FromJsonRequest_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""id"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""type_message"": ""REQ"",
            ""type_command"": ""EVENT_CALL"",
            ""from"": ""client-001"",
            ""data"": {
                ""event_name"": ""Detection-001"",
                ""state"": ""ACTIVE""
            },
            ""timestamp"": ""2025-11-18T10:30:00.000Z""
        }";

        // Act
        var request = BrokerMessageHelper.FromJsonRequest<EventCallDto>(json);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("REQ", request.TypeMessage);
        Assert.Equal("EVENT_CALL", request.Command);
        Assert.Equal("client-001", request.From);
        Assert.NotNull(request.Data);
        Assert.Equal("Detection-001", request.Data.EventName);
        Assert.Equal("ACTIVE", request.Data.State);
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void BrokerResponse_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var resultDto = new EventCallDto { EventName = "Result", State = "DONE" };
        var response = BrokerMessageHelper.CreateResponse(
            resultDto,
            "req-123",
            "server",
            "EVENT_CALL",
            "Success"
        );

        // Act
        var json = response.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.True(json?.Contains("\"type_message\":\"RSP\""));
        Assert.True(json?.Contains("\"success\":true"));
        Assert.True(json?.Contains("\"req_id\":\"req-123\""));
        Assert.True(json?.Contains("\"event_name\":\"Result\""));
    }

    [Fact]
    [Trait("Category", "Broker")]
    public void FromJsonResponse_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""id"": ""550e8401-e29b-41d4-a716-446655440000"",
            ""type_message"": ""RSP"",
            ""command"": ""EVENT_CALL"",
            ""from"": ""server"",
            ""request_id"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""success"": true,
            ""message"": ""Processed"",
            ""data"": {
                ""event_name"": ""Response-001"",
                ""state"": ""COMPLETED""
            },
            ""timestamp"": ""2025-11-18T10:30:01.000Z""
        }";

        // Act
        var response = BrokerMessageHelper.FromJsonResponse<EventCallDto>(json);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("RSP", response.TypeMessage);
        Assert.True(response.Success);
        Assert.Equal("Processed", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("Response-001", response.Data.EventName);
    }

    #endregion

    #region - 왕복 변환 테스트 (Round-trip) -

    [Fact]
    [Trait("Category", "Broker")]
    public void BrokerRequest_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalDto = new DetectionEventDto
        {
            Id = 1001,
            GroupEvent = "group_test",
            TypeEvent = "Intrusion",
            Controller = 1,
            Sensor = 2,
            TypeDevice = "Multi",
            Result = "PIR_SENSOR",
            ActionReported = "False"
        };

        // Act
        var request = originalDto.ToBrokerRequest("DETECTION", "test-service");
        var json = request.ToJson();
        var deserializedRequest = BrokerMessageHelper.FromJsonRequest<DetectionEventDto>(json);

        // Assert
        Assert.NotNull(deserializedRequest);
        Assert.Equal(request.Command, deserializedRequest.Command);
        Assert.Equal(request.From, deserializedRequest.From);
        Assert.NotNull(deserializedRequest.Data);
        Assert.Equal(originalDto.Id, deserializedRequest.Data.Id);
        Assert.Equal(originalDto.GroupEvent, deserializedRequest.Data.GroupEvent);
        Assert.Equal(originalDto.TypeEvent, deserializedRequest.Data.TypeEvent);
        Assert.Equal(originalDto.Result, deserializedRequest.Data.Result);
    }

    [Fact]
    [Trait("Category", "API")]
    public void ApiResponse_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalData = new CameraDeviceDto
        {
            Id = 201,
            NumberDevice = 109,
            GroupDevice = 1,
            NameDevice = "Camera-109",
            TypeDevice = "IpCamera",
            Status = "ACTIVATED",
            Mode = "ONVIF",
            Category = "PTZ",
            IpAddress = "192.168.1.109",
            IpPort = 80
        };
        var originalResponse = ApiResponse<CameraDeviceDto>.CreateSuccess(
            originalData,
            "Camera retrieved successfully"
        );

        // Act
        var json = originalResponse.ToJson();
        var deserializedResponse = ApiMessageHelper.FromJsonResponse<CameraDeviceDto>(json);

        // Assert
        Assert.NotNull(deserializedResponse);
        Assert.True(deserializedResponse.Success);
        Assert.Equal(originalResponse.Message, deserializedResponse.Message);
        Assert.NotNull(deserializedResponse.Data);
        Assert.Equal(originalData.Id, deserializedResponse.Data.Id);
        Assert.Equal(originalData.NameDevice, deserializedResponse.Data.NameDevice);
        Assert.Equal(originalData.Mode, deserializedResponse.Data.Mode);
        Assert.Equal(originalData.Category, deserializedResponse.Data.Category);
    }

    #endregion
}


public class MessageIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void BrokerRequestResponse_Integration_ShouldWorkCorrectly()
    {
        // Arrange - 요청 생성
        var requestDto = new EventCallDto
        {
            EventName = "Integration-Test",
            State = "PENDING"
        };
        var request = requestDto.ToBrokerRequest("EVENT_CALL", "integration-client");
        var requestJson = request.ToJson();

        // Act - 요청 수신 및 처리
        var receivedRequest = BrokerMessageHelper.FromJsonRequest<EventCallDto>(requestJson);
        Assert.NotNull(receivedRequest);

        // 응답 생성
        var responseDto = new EventCallDto
        {
            EventName = receivedRequest.Data.EventName,
            State = "COMPLETED"
        };
        var response = receivedRequest.CreateResponseFor(
            responseDto,
            "integration-server",
            "Request processed successfully"
        );
        var responseJson = response.ToJson();

        // 응답 수신
        var receivedResponse = BrokerMessageHelper.FromJsonResponse<EventCallDto>(responseJson);

        // Assert
        Assert.NotNull(receivedResponse);
        Assert.Equal(request.Id, receivedResponse.RequestId);
        Assert.True(receivedResponse.Success);
        Assert.Equal("COMPLETED", receivedResponse.Data.State);
        Assert.Equal("integration-server", receivedResponse.From);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MultipleEventTypes_ShouldSerializeCorrectly()
    {
        // Arrange & Act & Assert - DetectionEvent
        var detectionDto = new DetectionEventDto
        {
            Id = 1001,
            TypeEvent = "Intrusion",
            Result = "THERMAL_SENSOR"
        };
        var detectionRequest = detectionDto.ToBrokerRequest("client");
        var detectionJson = detectionRequest.ToJson();
        var deserializedDetection = BrokerMessageHelper.FromJsonRequest<DetectionEventDto>(detectionJson);
        Assert.Equal("THERMAL_SENSOR", deserializedDetection?.Data.Result);

        // Arrange & Act & Assert - MalfunctionEvent
        var malfunctionDto = new MalfunctionEventDto
        {
            Id = 2001,
            TypeEvent = "Fault",
            Reason = "FAULT_FENCE",
            ActionReported = "True"
        };
        var malfunctionRequest = malfunctionDto.ToBrokerRequest("client");
        var malfunctionJson = malfunctionRequest.ToJson();
        var deserializedMalfunction = BrokerMessageHelper.FromJsonRequest<MalfunctionEventDto>(malfunctionJson);
        Assert.Equal("FAULT_FENCE", deserializedMalfunction?.Data.Reason);

        // Arrange & Act & Assert - ActionEvent
        var actionDto = new ActionEventDto
        {
            Id = 3001,
            TypeEvent = "Action",
            Content = "침입 탐지 확인",
            User = "operator_test"
        };
        var actionRequest = actionDto.ToBrokerRequest("client");
        var actionJson = actionRequest.ToJson();
        var deserializedAction = BrokerMessageHelper.FromJsonRequest<ActionEventDto>(actionJson);
        Assert.Equal("침입 탐지 확인", deserializedAction?.Data.Content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NullHandling_ShouldWorkCorrectly()
    {
        // Arrange
        var dto = new EventCallDto
        {
            EventName = "Test",
            State = null // Null 값
        };

        // Act
        var request = dto.ToBrokerRequest("client");
        var json = request.ToJson();

        // Assert - Null 값은 JSON에 포함되지 않아야 함 (NullValueHandling.Ignore)
        Assert.False(json.Contains("\"state\":null"));

        // Deserialize
        var deserialized = BrokerMessageHelper.FromJsonRequest<EventCallDto>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("Test", deserialized.Data.EventName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Timestamp_ShouldBeISO8601Format()
    {
        // Arrange & Act
        var dto = new EventCallDto { EventName = "Test" };
        var request = dto.ToBrokerRequest("client");

        // Assert - ISO 8601 형식 검증 (yyyy-MM-ddTHH:mm:ss.fffZ)
        Assert.NotNull(request.Timestamp);
        Assert.True(request.Timestamp.Contains("T"));
        Assert.True(request.Timestamp.EndsWith("Z"));

        // DateTime 파싱 가능 여부 확인
        var parsed = DateTime.TryParse(request.Timestamp, out var timestamp);
        Assert.True(parsed);
    }
}


public class DetectionExEventDtoTests
{
    #region - EventUrlsDto 테스트 -

    [Fact]
    [Trait("Category", "DTO")]
    public void EventUrlsDto_Serialization_ShouldWorkCorrectly()
    {
        // Arrange
        var urlsDto = new EventUrlsDto
        {
            Live = "rtsp://192.168.1.100:554/live",
            Record = "rtsp://192.168.1.100:554/playback?start=20250118T100000&end=20250118T101000"
        };

        // Act
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(urlsDto);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"live\":\"rtsp://192.168.1.100:554/live\"", json);
        Assert.Contains("\"record\":\"rtsp://192.168.1.100:554/playback", json);
    }

    [Fact]
    [Trait("Category", "DTO")]
    public void EventUrlsDto_Deserialization_ShouldWorkCorrectly()
    {
        // Arrange
        var json = @"{
            ""live"": ""rtsp://192.168.1.101:554/live/camera1"",
            ""record"": ""rtsp://192.168.1.101:554/record/camera1""
        }";

        // Act
        var urlsDto = Newtonsoft.Json.JsonConvert.DeserializeObject<EventUrlsDto>(json);

        // Assert
        Assert.NotNull(urlsDto);
        Assert.Equal("rtsp://192.168.1.101:554/live/camera1", urlsDto.Live);
        Assert.Equal("rtsp://192.168.1.101:554/record/camera1", urlsDto.Record);
    }

    #endregion

    #region - DetectionExEventDto 테스트 -

    [Fact]
    [Trait("Category", "DTO")]
    public void DetectionExEventDto_Deserialization_ShouldWorkCorrectly()
    {
        // Arrange
        var json = @"{
            ""name_event"": ""침입탐지-카메라연동"",
            ""category_event"": ""DETECT_SENSOR_WITH_CAMERA"",
            ""origin_event"": {
                ""id"": 1001,
                ""group_event"": ""group_test"",
                ""type_event"": ""Intrusion"",
                ""controller"": 1,
                ""sensor"": 5,
                ""type_device"": ""Multi"",
                ""sequence"": 123,
                ""action_reported"": ""False"",
                ""result"": ""PIR_SENSOR""
            }
        }";

        // Act
        var detectionExDto = Newtonsoft.Json.JsonConvert.DeserializeObject<DetectionExEventDto>(json);

        // Assert
        Assert.NotNull(detectionExDto);
        Assert.Equal("침입탐지-카메라연동", detectionExDto.NameEvent);
        Assert.Equal("DETECT_SENSOR_WITH_CAMERA", detectionExDto.CategoryEvent);

        Assert.NotNull(detectionExDto.OriginEvent);
        Assert.Equal(1001, detectionExDto.OriginEvent.Id);
        Assert.Equal("Intrusion", detectionExDto.OriginEvent.TypeEvent);
        Assert.Equal(1, detectionExDto.OriginEvent.Controller);
        Assert.Equal(5, detectionExDto.OriginEvent.Sensor);
        Assert.Equal("Multi", detectionExDto.OriginEvent.TypeDevice);
        Assert.Equal(123, detectionExDto.OriginEvent.Sequence);
        Assert.Equal("PIR_SENSOR", detectionExDto.OriginEvent.Result);
    }

    [Fact]
    [Trait("Category", "DTO")]
    public void DetectionExEventDto_Serialization_ShouldWorkCorrectly()
    {
        // Arrange
        var detectionExDto = new DetectionExEventDto
        {
            NameEvent = "테스트이벤트",
            CategoryEvent = "DETECT_SENSOR_WITH_CAMERA",
            OriginEvent = new DetectionEventDto
            {
                Id = 2001,
                GroupEvent = "group_001",
                TypeEvent = "Intrusion",
                Controller = 2,
                Sensor = 10,
                TypeDevice = "Fence",
                Sequence = 456,
                ActionReported = "True",
                Result = "THERMAL_SENSOR"
            }
        };

        // Act
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(detectionExDto, Newtonsoft.Json.Formatting.Indented);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"name_event\":", json);
        Assert.Contains("\"테스트이벤트\"", json);
        Assert.Contains("\"category_event\":", json);
        Assert.Contains("\"DETECT_SENSOR_WITH_CAMERA\"", json);
        Assert.Contains("\"origin_event\":", json);
        Assert.Contains("\"controller\": 2", json);
        Assert.Contains("\"type_device\": \"Fence\"", json);
        Assert.Contains("\"sequence\": 456", json);
    }

    [Fact]
    [Trait("Category", "DTO")]
    public void DetectionExEventDto_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var detectionExDto = new DetectionExEventDto();

        // Assert
        Assert.NotNull(detectionExDto);
        Assert.Equal(string.Empty, detectionExDto.NameEvent);
        Assert.Equal(string.Empty, detectionExDto.CategoryEvent);
        Assert.NotNull(detectionExDto.OriginEvent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DetectionExEventDto_FullNatsMessage_ShouldWorkCorrectly()
    {
        // Arrange - 완전한 NATS 메시지 Body 시뮬레이션
        var detectionExDto = new DetectionExEventDto
        {
            NameEvent = "침입탐지-001",
            CategoryEvent = "DETECT_SENSOR_WITH_CAMERA",
            OriginEvent = new DetectionEventDto
            {
                Id = 3001,
                GroupEvent = "security_zone_1",
                TypeEvent = "Intrusion",
                Controller = 3,
                Sensor = 15,
                TypeDevice = "Underground",
                Sequence = 789,
                ActionReported = "False",
                Result = "VIBRATION_SENSOR"
            }
        };

        // Act - 직렬화 및 역직렬화 (왕복 테스트)
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(detectionExDto);
        var deserializedDto = Newtonsoft.Json.JsonConvert.DeserializeObject<DetectionExEventDto>(json);

        // Assert - 모든 데이터가 보존되는지 확인
        Assert.NotNull(deserializedDto);
        Assert.Equal(detectionExDto.NameEvent, deserializedDto.NameEvent);
        Assert.Equal(detectionExDto.CategoryEvent, deserializedDto.CategoryEvent);

        Assert.Equal(detectionExDto.OriginEvent.Id, deserializedDto.OriginEvent.Id);
        Assert.Equal(detectionExDto.OriginEvent.Controller, deserializedDto.OriginEvent.Controller);
        Assert.Equal(detectionExDto.OriginEvent.Sensor, deserializedDto.OriginEvent.Sensor);
        Assert.Equal(detectionExDto.OriginEvent.TypeDevice, deserializedDto.OriginEvent.TypeDevice);
        Assert.Equal(detectionExDto.OriginEvent.Sequence, deserializedDto.OriginEvent.Sequence);
        Assert.Equal(detectionExDto.OriginEvent.Result, deserializedDto.OriginEvent.Result);
    }

    #endregion
}

/// <summary>
/// BrokerMessageHelper 파싱 테스트
/// Phase 8: Single Event Message Handling
/// </summary>
public class BrokerMessageParsingTests
{
    #region - Test 8.2.1: 단일 MalfunctionEventDto 파싱 -
    [Fact(DisplayName = "TEST-8.2.1: ParseEventsFromBrokerMessage - 단일 MalfunctionEventDto escaped string")]
    public void ParseEventsFromBrokerMessage_WithSingleMalfunctionEvent_ShouldReturnOneItem()
    {
        // Arrange - 실제 수신된 메시지
        var json = @"{
            ""id"": ""6cf7e2dc-d530-4328-aeaf-1eaefbae6fbc"",
            ""type_message"": ""REQ"",
            ""type_command"": ""Fault"",
            ""from"": ""proxyManager"",
            ""data"": ""{\""id\"":0,\""group_event\"":\""1\"",\""type_event\"":\""Fault\"",\""controller\"":1,\""sensor\"":1,\""type_device\"":\""Fence\"",\""sequence\"":42,\""action_reported\"":\""False\"",\""reason\"":\""FAULT_FENCE\"",\""first_start\"":0,\""first_end\"":0,\""second_start\"":0,\""second_end\"":0,\""created_at\"":\""2025-11-27T01:45:53.019Z\"",\""updated_at\"":null}"",
            ""timestamp"": ""2025-11-27T01:45:53.019Z""
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].Id);
        Assert.Equal("1", result[0].GroupEvent);
        Assert.Equal("FAULT_FENCE", result[0].Reason);
        Assert.Equal(1, result[0].Controller);
        Assert.Equal(1, result[0].Sensor);
    }
    #endregion

    #region - Test 8.2.2: 배열 MalfunctionEventDto 파싱 -
    [Fact(DisplayName = "TEST-8.2.2: ParseEventsFromBrokerMessage - 배열 형태 escaped string")]
    public void ParseEventsFromBrokerMessage_WithArrayEvents_ShouldReturnMultipleItems()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""type_message"": ""REQ"",
            ""type_command"": ""Fault"",
            ""from"": ""proxyManager"",
            ""data"": ""[{\""id\"":1,\""reason\"":\""FAULT_FENCE\""},{\""id\"":2,\""reason\"":\""FAULT_CONTROLLER\""}]"",
            ""timestamp"": ""2025-11-27T01:45:53.019Z""
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
    }
    #endregion

    #region - Test 8.2.3: 직접 객체 data 파싱 -
    [Fact(DisplayName = "TEST-8.2.3: ParseEventsFromBrokerMessage - data가 직접 객체인 경우")]
    public void ParseEventsFromBrokerMessage_WithDirectObject_ShouldParse()
    {
        // Arrange - data가 escaped string이 아닌 직접 객체
        var json = @"{
            ""id"": ""xxx"",
            ""type_message"": ""REQ"",
            ""type_command"": ""Fault"",
            ""from"": ""proxyManager"",
            ""data"": {""id"":0,""group_event"":""1"",""reason"":""FAULT_FENCE""},
            ""timestamp"": ""2025-11-27T01:45:53.019Z""
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].Id);
        Assert.Equal("FAULT_FENCE", result[0].Reason);
    }
    #endregion

    #region - Test 8.2.4: ParseSingleEventFromBrokerMessage -
    [Fact(DisplayName = "TEST-8.2.4: ParseSingleEventFromBrokerMessage - 단일 객체 파싱")]
    public void ParseSingleEventFromBrokerMessage_WithValidMessage_ShouldReturnDto()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""type_message"": ""REQ"",
            ""type_command"": ""Fault"",
            ""from"": ""proxyManager"",
            ""data"": ""{\""id\"":123,\""reason\"":\""FAULT_FENCE\""}"",
            ""timestamp"": ""2025-11-27T01:45:53.019Z""
        }";

        // Act
        var result = BrokerMessageHelper.ParseSingleEventFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("FAULT_FENCE", result.Reason);
    }
    #endregion

    #region - Test 8.2.5: null data 처리 -
    [Fact(DisplayName = "TEST-8.2.5: ParseEventsFromBrokerMessage - data가 null인 경우")]
    public void ParseEventsFromBrokerMessage_WithNullData_ShouldReturnEmptyList()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""type_message"": ""REQ"",
            ""data"": null
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "TEST-8.2.5-2: ParseSingleEventFromBrokerMessage - data가 null인 경우")]
    public void ParseSingleEventFromBrokerMessage_WithNullData_ShouldReturnNull()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""type_message"": ""REQ"",
            ""data"": null
        }";

        // Act
        var result = BrokerMessageHelper.ParseSingleEventFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Null(result);
    }
    #endregion
}

/// <summary>
/// Phase 9: DateTime Format Issue Fix - FromEvent Deserialization
/// PRD: docs/debugging/DateTime_Format_Issue_PRD.md
/// </summary>
public class DateTimeFormatPreservationTests
{
    #region - Test 9.1.1: FromEvent.CreatedAt ISO 형식 유지 테스트 -
    [Fact(DisplayName = "TEST-9.1.1: FromJsonResponse<ActionEventDto> - FromEvent.CreatedAt은 ISO 형식을 유지해야 함")]
    public void FromJsonResponse_ActionEventDto_ShouldPreserveFromEventDateFormat()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""message"": ""OK"",
            ""data"": {
                ""id"": 123,
                ""group_event"": ""GROUP1"",
                ""type_event"": ""Action"",
                ""sequence"": 1,
                ""status"": ""Complete"",
                ""from_event"": {
                    ""id"": 456,
                    ""type_event"": ""Intrusion"",
                    ""created_at"": ""2025-11-27T16:50:59.905273"",
                    ""updated_at"": ""2025-11-27T16:50:59.905273""
                },
                ""created_at"": ""2025-11-27T20:09:13.123456"",
                ""updated_at"": ""2025-11-27T20:09:13.123456""
            }
        }";

        // Act
        var result = ApiMessageHelper.FromJsonResponse<ActionEventDto>(json);

        // Assert
        Assert.NotNull(result?.Data?.FromEvent);
        Assert.Equal("2025-11-27T16:50:59.905273", result.Data.FromEvent.CreatedAt);
        Assert.Equal("2025-11-27T16:50:59.905273", result.Data.FromEvent.UpdatedAt);
    }
    #endregion

    #region - Test 9.1.2: FromEvent MalfunctionEventDto 테스트 -
    [Fact(DisplayName = "TEST-9.1.2: FromJsonResponse<ActionEventDto> - FromEvent가 MalfunctionEventDto일 때도 ISO 형식 유지")]
    public void FromJsonResponse_ActionEventDto_WithMalfunctionFromEvent_ShouldPreserveDateFormat()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""message"": ""OK"",
            ""data"": {
                ""id"": 789,
                ""from_event"": {
                    ""id"": 101,
                    ""type_event"": ""Fault"",
                    ""reason"": ""FAULT_FENCE"",
                    ""created_at"": ""2025-11-27T10:30:00.000000"",
                    ""updated_at"": ""2025-11-27T10:30:00.000000""
                }
            }
        }";

        // Act
        var result = ApiMessageHelper.FromJsonResponse<ActionEventDto>(json);

        // Assert
        Assert.NotNull(result?.Data?.FromEvent);
        Assert.Equal("2025-11-27T10:30:00.000000", result.Data.FromEvent.CreatedAt);
    }
    #endregion
}

#region - Phase 10: KoreaTimeHelper Tests -
/// <summary>
/// KoreaTimeHelper 테스트 클래스
/// <para>한국 시간 ISO 8601 Helper 메서드 검증</para>
/// </summary>
public class KoreaTimeHelperTests
{
    [Fact(DisplayName = "TEST-10.1.1: GetKoreaTimeIso8601 - +09:00 오프셋 포함 형식 반환")]
    public void GetKoreaTimeIso8601_ShouldReturnValidIso8601Format()
    {
        // Act
        var result = KoreaTimeHelper.GetKoreaTimeIso8601();

        // Assert
        Assert.True(result.EndsWith("+09:00"),
            $"Expected +09:00 offset, got: {result}");
        Assert.Contains("T", result);
    }

    [Fact(DisplayName = "TEST-10.1.2: GetKoreaTimeIso8601 - 현재 시간 기준 반환")]
    public void GetKoreaTimeIso8601_ShouldBeCurrentTime()
    {
        // Arrange
        var beforeUtc = DateTime.UtcNow;

        // Act
        var result = KoreaTimeHelper.GetKoreaTimeIso8601();
        var parsed = DateTimeOffset.Parse(result);

        // Assert
        var afterUtc = DateTime.UtcNow;
        Assert.True(parsed.UtcDateTime >= beforeUtc.AddSeconds(-1));
        Assert.True(parsed.UtcDateTime <= afterUtc.AddSeconds(1));
    }

    [Fact(DisplayName = "TEST-10.1.3: ToKoreaTimeIso8601 - UTC DateTime을 KST로 변환 (+9시간)")]
    public void ToKoreaTimeIso8601_ShouldAddNineHours()
    {
        // Arrange
        var utcTime = new DateTime(2025, 11, 28, 9, 30, 0, DateTimeKind.Utc);

        // Act
        var result = KoreaTimeHelper.ToKoreaTimeIso8601(utcTime);

        // Assert
        Assert.StartsWith("2025-11-28T18:30:00", result);
        Assert.EndsWith("+09:00", result);
    }

    [Fact(DisplayName = "TEST-10.1.4: ParseToKoreaTime - UTC ISO 문자열을 KST DateTime으로 파싱")]
    public void ParseToKoreaTime_ShouldParseUtcToKst()
    {
        // Arrange
        var utcIso = "2025-11-28T09:30:00.000Z";

        // Act
        var result = KoreaTimeHelper.ParseToKoreaTime(utcIso);

        // Assert
        Assert.Equal(18, result.Hour);
        Assert.Equal(30, result.Minute);
    }

    [Fact(DisplayName = "TEST-10.1.5: ParseToKoreaTime - KST ISO 문자열을 KST DateTime으로 파싱")]
    public void ParseToKoreaTime_ShouldParseKstOffset()
    {
        // Arrange
        var kstIso = "2025-11-28T18:30:00.000+09:00";

        // Act
        var result = KoreaTimeHelper.ParseToKoreaTime(kstIso);

        // Assert
        Assert.Equal(18, result.Hour);
        Assert.Equal(30, result.Minute);
    }

    [Fact(DisplayName = "TEST-10.1.6: ToKoreaTimeDisplayString - ISO를 표시용 문자열로 변환")]
    public void ToKoreaTimeDisplayString_ShouldFormatCorrectly()
    {
        // Arrange
        var iso = "2025-11-28T18:30:45.123+09:00";

        // Act
        var defaultFormat = KoreaTimeHelper.ToKoreaTimeDisplayString(iso);
        var customFormat = KoreaTimeHelper.ToKoreaTimeDisplayString(iso, "MM-dd HH:mm");

        // Assert
        Assert.Equal("2025-11-28 18:30:45", defaultFormat);
        Assert.Equal("11-28 18:30", customFormat);
    }

    [Fact(DisplayName = "TEST-10.1.7: ToUtcIso8601 - KST ISO를 UTC ISO로 변환")]
    public void ToUtcIso8601_ShouldConvertKstToUtc()
    {
        // Arrange
        var kstIso = "2025-11-28T18:30:00.000+09:00";

        // Act
        var result = KoreaTimeHelper.ToUtcIso8601(kstIso);

        // Assert
        Assert.StartsWith("2025-11-28T09:30:00", result);
        Assert.EndsWith("Z", result);
    }

    [Fact(DisplayName = "TEST-10.1.8: ToKoreaTimeDisplayString - 빈 문자열 입력 시 빈 문자열 반환")]
    public void ToKoreaTimeDisplayString_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result1 = KoreaTimeHelper.ToKoreaTimeDisplayString("");
        var result2 = KoreaTimeHelper.ToKoreaTimeDisplayString(null!);

        // Assert
        Assert.Equal(string.Empty, result1);
        Assert.Equal(string.Empty, result2);
    }

    [Fact(DisplayName = "TEST-10.2.3: BaseDto.CreatedAt - KST +09:00 오프셋 형식 검증")]
    public void BaseDto_CreatedAt_ShouldHaveKstOffset()
    {
        // Arrange
        var baseDto = new Dto.Bases.BaseDto();

        // Assert
        Assert.NotNull(baseDto.CreatedAt);
        Assert.True(baseDto.CreatedAt.EndsWith("+09:00"),
            $"Expected +09:00 offset, got: {baseDto.CreatedAt}");
        Assert.Contains("T", baseDto.CreatedAt);
    }

    [Fact(DisplayName = "TEST-10.2.4: MetaDto.Timestamp - KST +09:00 오프셋 형식 검증")]
    public void MetaDto_Timestamp_ShouldHaveKstOffset()
    {
        // Arrange
        var metaDto = new MetaDto();

        // Assert
        Assert.NotNull(metaDto.Timestamp);
        Assert.True(metaDto.Timestamp.EndsWith("+09:00"),
            $"Expected +09:00 offset, got: {metaDto.Timestamp}");
        Assert.Contains("T", metaDto.Timestamp);
    }

    [Fact(DisplayName = "TEST-10.3.1: BaseDto JSON 직렬화 - KST 오프셋 유지 검증")]
    public void BaseDto_JsonSerialization_ShouldPreserveKstOffset()
    {
        // Arrange
        var baseDto = new Dto.Bases.BaseDto { Id = 1 };

        // Act
        var json = JsonConvert.SerializeObject(baseDto);
        var deserialized = JsonConvert.DeserializeObject<Dto.Bases.BaseDto>(json);

        // Assert
        Assert.NotNull(deserialized?.CreatedAt);
        Assert.True(deserialized.CreatedAt.EndsWith("+09:00"),
            $"Expected +09:00 offset after round-trip, got: {deserialized.CreatedAt}");
        Assert.Contains("+09:00", json);
    }
}
#endregion
