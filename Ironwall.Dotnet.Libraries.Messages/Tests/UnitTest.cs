using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;

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
        Assert.Equal(2, response.Data.Count);
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
            Status = "True"
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
