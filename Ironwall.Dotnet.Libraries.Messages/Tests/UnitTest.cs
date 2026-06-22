using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Messages.Dto.RtspPopups;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

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
            TypeEvent = "Intrusion",
            Result = "PIR_SENSOR"
        };
        var request = dto.ToBrokerRequest("DETECTION", "sensor-service");

        // Act
        var json = request.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.True(json?.Contains("\"m_type\":\"REQ\""));
        Assert.True(json?.Contains("\"cmd\":\"DETECTION\""));
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
            ""m_type"": ""REQ"",
            ""cmd"": ""EVENT_CALL"",
            ""from"": ""client-001"",
            ""body"": {
                ""event_name"": ""Detection-001"",
                ""state"": ""ACTIVE""
            },
            ""created"": ""2025-11-18T10:30:00.000Z""
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
        Assert.True(json?.Contains("\"m_type\":\"RSP\""));
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
            ""m_type"": ""RSP"",
            ""cmd"": ""EVENT_CALL"",
            ""from"": ""server"",
            ""request_id"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""success"": true,
            ""message"": ""Processed"",
            ""body"": {
                ""event_name"": ""Response-001"",
                ""state"": ""COMPLETED""
            },
            ""created"": ""2025-11-18T10:30:01.000Z""
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
            TypeEvent = "Intrusion",
            Device = new BaseDeviceDto { Id = 2, TypeDevice = "Multi" },
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
        // Arrange - 실제 수신된 메시지 (nested device 구조)
        var json = @"{
            ""id"": ""6cf7e2dc-d530-4328-aeaf-1eaefbae6fbc"",
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": ""{\""id\"":0,\""type_event\"":\""Fault\"",\""device\"":{\""id\"":1,\""type_device\"":\""Fence\""},\""device_description\"":\""Fence device\"",\""action_reported\"":\""False\"",\""reason\"":\""FAULT_FENCE\"",\""created_at\"":\""2025-11-27T01:45:53.019Z\"",\""updated_at\"":null}"",
            ""created"": ""2025-11-27T01:45:53.019Z""
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].Id);
        Assert.Equal("FAULT_FENCE", result[0].Reason);
        Assert.NotNull(result[0].Device);
        Assert.Equal(1, result[0].Device!.Id);
        Assert.Equal("Fence", result[0].Device.TypeDevice);
    }
    #endregion

    #region - Test 8.2.2: 배열 MalfunctionEventDto 파싱 -
    [Fact(DisplayName = "TEST-8.2.2: ParseEventsFromBrokerMessage - 배열 형태 escaped string")]
    public void ParseEventsFromBrokerMessage_WithArrayEvents_ShouldReturnMultipleItems()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": ""[{\""id\"":1,\""reason\"":\""FAULT_FENCE\""},{\""id\"":2,\""reason\"":\""FAULT_CONTROLLER\""}]"",
            ""created"": ""2025-11-27T01:45:53.019Z""
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
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": {""id"":0,""group_event"":""1"",""reason"":""FAULT_FENCE""},
            ""created"": ""2025-11-27T01:45:53.019Z""
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
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": ""{\""id\"":123,\""reason\"":\""FAULT_FENCE\""}"",
            ""created"": ""2025-11-27T01:45:53.019Z""
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
            ""m_type"": ""REQ"",
            ""body"": null
        }";

        // Act
        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "TEST-8.2.5-2: ParseSingleEventFromBrokerMessage - body가 null인 경우")]
    public void ParseSingleEventFromBrokerMessage_WithNullData_ShouldReturnNull()
    {
        // Arrange
        var json = @"{
            ""id"": ""xxx"",
            ""m_type"": ""REQ"",
            ""body"": null
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

#region - Phase 2: 신규 DTO 테스트 -
/// <summary>
/// BaseDeviceDto 테스트 — Device 공통 기반 클래스
/// </summary>
public class BaseDeviceDtoTests
{
    [Fact(DisplayName = "A2.1-1: BaseDeviceDto 직렬화 시 snake_case 필드가 존재해야 한다")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_Serialization_ShouldHaveSnakeCaseFields()
    {
        // Arrange
        var dto = new BaseDeviceDto
        {
            Id = 1,
            NumberDevice = 101,
            NameDevice = "TestDevice-001",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IsEnable = true,
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 }, new() { Id = 2 } }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);

        // Assert
        Assert.Contains("\"number_device\":101", json);
        Assert.Contains("\"name_device\":\"TestDevice-001\"", json);
        Assert.Contains("\"type_device\":\"Controller\"", json);
        Assert.Contains("\"status\":\"ACTIVATED\"", json);
        Assert.Contains("\"is_enable\":true", json);
        Assert.Contains("\"device_groups\":[{", json);
        // BaseDeviceDto 레벨에는 ip_address/ip_port 없음 (서브클래스에만 존재)
        Assert.DoesNotContain("\"ip_address\":", json);
        Assert.DoesNotContain("\"ip_port\":", json);
    }

    [Fact(DisplayName = "A2.1-2: BaseDeviceDto Backend JSON 역직렬화 시 모든 필드가 매핑되어야 한다")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_Deserialization_FromBackendJson_ShouldMapAllFields()
    {
        // Arrange — Backend 응답 시뮬레이션
        var json = @"{
            ""id"": 42,
            ""number_device"": 109,
            ""name_device"": ""Camera-109"",
            ""type_device"": ""IpCamera"",
            ""status"": ""ACTIVATED"",
            ""is_enable"": true,
            ""device_groups"": [{""id"":1,""name"":""G1""},{""id"":3,""name"":""G3""},{""id"":5,""name"":""G5""}],
            ""description"": ""정문 카메라"",
            ""ip_address"": ""10.0.0.50"",
            ""ip_port"": 554,
            ""created_at"": ""2025-12-01T10:00:00.000+09:00"",
            ""updated_at"": ""2025-12-01T12:00:00.000+09:00""
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(42, dto.Id);
        Assert.Equal(109, dto.NumberDevice);
        Assert.Equal("Camera-109", dto.NameDevice);
        Assert.Equal("IpCamera", dto.TypeDevice);
        Assert.Equal("ACTIVATED", dto.Status);
        Assert.True(dto.IsEnable);
        Assert.NotNull(dto.DeviceGroups);
        Assert.Equal(3, dto.DeviceGroups.Count);
        Assert.Contains(dto.DeviceGroups, g => g.Id == 3);
        // 설계 문서 원칙: nested device 객체에서 created_at/updated_at 제외 → 역직렬화 시도해도 무시됨
    }

    #region A9.3: BaseDeviceDto version/group_device/geolocation 추가 (§14.2.2~4)

    [Fact(DisplayName = "A9.3-1: BaseDeviceDto version 필드 직렬화")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_WithVersion_ShouldSerialize()
    {
        var dto = new BaseDeviceDto { Id = 1, Version = "v1.5.0" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        Assert.Contains("\"version\":\"v1.5.0\"", json);
        Assert.NotNull(d);
        Assert.Equal("v1.5.0", d.Version);
    }

    [Fact(DisplayName = "A9.3-2: BaseDeviceDto device_groups 필드 직렬화")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_WithDeviceGroups_ShouldSerialize()
    {
        var dto = new BaseDeviceDto
        {
            Id = 1,
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 3 } }
        };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        Assert.Contains("\"device_groups\":", json);
        Assert.NotNull(d);
        Assert.NotNull(d.DeviceGroups);
        Assert.Single(d.DeviceGroups);
        Assert.Equal(3, d.DeviceGroups[0].Id);
    }

    [Fact(DisplayName = "A9.3-3: BaseDeviceDto geolocation 중첩 객체 직렬화")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_WithGeolocation_ShouldSerializeNestedObject()
    {
        var dto = new BaseDeviceDto
        {
            Id = 1,
            Geolocation = new GeolocationDto
            {
                Location = "GOP 1구역",
                Latitude = 38.1234,
                Longitude = 127.5678,
                Altitude = 0
            }
        };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        Assert.Contains("\"geolocation\":", json);
        Assert.Contains("\"location\":\"GOP 1구역\"", json);
        Assert.NotNull(d?.Geolocation);
        Assert.Equal("GOP 1구역", d.Geolocation.Location);
        Assert.Equal(38.1234, d.Geolocation.Latitude);
    }

    [Fact(DisplayName = "A9.3-4: 설계 문서 NATS v1.1 §6.1 device JSON → BaseDeviceDto 전체 필드 역직렬화")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_DesignDocJson_ShouldDeserializeAllFields()
    {
        // Arrange — NATS v1.1 §6.1 Detection Event의 nested device
        var json = """
        {
          "id": 101,
          "number_device": 1,
          "group_device": 1,
          "name_device": "Sensor-A-1",
          "type_device": "Multi",
          "version": "v1.5.0",
          "status": "ACTIVATED",
          "is_enable": true,
          "controller_id": 1,
          "geolocation": null,
          "device_groups": [{"id":1},{"id":3},{"id":5}],
          "ip_address": "192.168.1.101",
          "ip_port": 5000
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(101, dto.Id);
        Assert.Equal(1, dto.NumberDevice);
        Assert.Equal("Sensor-A-1", dto.NameDevice);
        Assert.Equal("Multi", dto.TypeDevice);
        Assert.Equal("v1.5.0", dto.Version);
        Assert.Equal("ACTIVATED", dto.Status);
        Assert.True(dto.IsEnable);
        Assert.Null(dto.Geolocation);
    }

    #endregion

    #region A9.4: device_groups 타입 변경 (§14.2.1)

    [Fact(DisplayName = "A9.4-1: device_groups 객체 배열 JSON → List<DeviceGroupDto> 역직렬화")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_DeviceGroups_ShouldDeserializeAsObjectArray()
    {
        // Arrange — 설계 문서 NATS v1.1 §6.1 device_groups 형식
        var json = """
        {
          "id": 101,
          "type_device": "Multi",
          "device_groups": [
            {"id": 1, "name": "A구역 센서그룹", "description": "A구역 센서 장비 그룹", "device_count": 5},
            {"id": 3, "name": "B구역 센서그룹", "description": "B구역 센서 장비 그룹", "device_count": 3}
          ]
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.NotNull(dto.DeviceGroups);
        Assert.Equal(2, dto.DeviceGroups.Count);
        Assert.Equal(1, dto.DeviceGroups[0].Id);
        Assert.Equal("A구역 센서그룹", dto.DeviceGroups[0].Name);
        Assert.Equal(5, dto.DeviceGroups[0].DeviceCount);
        Assert.Equal(3, dto.DeviceGroups[1].Id);
    }

    [Fact(DisplayName = "A9.4-2: device_groups null → null 하위 호환")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_DeviceGroups_NullShouldDeserialize()
    {
        var json = """{"id":1,"type_device":"Multi","device_groups":null}""";
        var dto = JsonConvert.DeserializeObject<BaseDeviceDto>(json);

        Assert.NotNull(dto);
        Assert.Null(dto.DeviceGroups);
    }

    #endregion

    #region A9.5: BaseDeviceDto nested 직렬화 시 created_at/updated_at 제외

    [Fact(DisplayName = "A9.5: BaseDeviceDto 직렬화 시 created_at/updated_at 제외 (설계 문서 Nested 객체 원칙)")]
    [Trait("Category", "DTO")]
    public void BaseDeviceDto_Serialization_ShouldNotIncludeCreatedAtUpdatedAt()
    {
        var dto = new BaseDeviceDto
        {
            Id = 101,
            TypeDevice = "Fence",
            NameDevice = "Sensor-A-1",
            Status = "ACTIVATED"
        };

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("created_at", json);
        Assert.DoesNotContain("updated_at", json);
        Assert.Contains("\"id\":101", json);
    }

    #endregion
}

/// <summary>
/// SpeakerDeviceDto 테스트
/// </summary>
public class SpeakerDeviceDtoTests
{
    [Fact(DisplayName = "A2.2: SpeakerDeviceDto는 BaseDeviceDto를 상속하고 TypeDevice 기본값이 IpSpeaker이다")]
    [Trait("Category", "DTO")]
    public void SpeakerDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize()
    {
        // Arrange
        var dto = new SpeakerDeviceDto
        {
            Id = 301,
            NumberDevice = 50,
            NameDevice = "Speaker-050",
            Status = "ACTIVATED"
        };

        // Assert — 기본값 확인
        Assert.Equal("IpSpeaker", dto.TypeDevice);
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);

        // Act — 왕복 직렬화
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<SpeakerDeviceDto>(json);

        // Assert — 왕복 검증
        Assert.NotNull(deserialized);
        Assert.Equal(301, deserialized.Id);
        Assert.Equal(50, deserialized.NumberDevice);
        Assert.Equal("Speaker-050", deserialized.NameDevice);
        Assert.Equal("IpSpeaker", deserialized.TypeDevice);
        Assert.Equal("ACTIVATED", deserialized.Status);
    }
}

/// <summary>
/// EnclosureDeviceDto 테스트
/// </summary>
public class EnclosureDeviceDtoTests
{
    [Fact(DisplayName = "A2.3: EnclosureDeviceDto는 BaseDeviceDto를 상속하고 TypeDevice 기본값이 Enclosure이다")]
    [Trait("Category", "DTO")]
    public void EnclosureDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize()
    {
        // Arrange
        var dto = new EnclosureDeviceDto
        {
            Id = 401,
            NumberDevice = 60,
            NameDevice = "Enclosure-060",
            Status = "ACTIVATED"
        };

        // Assert — 기본값 확인
        Assert.Equal("Enclosure", dto.TypeDevice);
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);

        // Act — 왕복 직렬화
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnclosureDeviceDto>(json);

        // Assert — 왕복 검증
        Assert.NotNull(deserialized);
        Assert.Equal(401, deserialized.Id);
        Assert.Equal(60, deserialized.NumberDevice);
        Assert.Equal("Enclosure-060", deserialized.NameDevice);
        Assert.Equal("Enclosure", deserialized.TypeDevice);
        Assert.Equal("ACTIVATED", deserialized.Status);
    }
}

/// <summary>
/// LampDeviceDto 테스트
/// </summary>
public class LampDeviceDtoTests
{
    [Fact(DisplayName = "A2.4: LampDeviceDto는 BaseDeviceDto를 상속하고 TypeDevice 기본값이 Lamp이다")]
    [Trait("Category", "DTO")]
    public void LampDeviceDto_ShouldInheritBaseDeviceDto_AndSerialize()
    {
        // Arrange
        var dto = new LampDeviceDto
        {
            Id = 501,
            NumberDevice = 70,
            NameDevice = "Lamp-070",
            Status = "ACTIVATED"
        };

        // Assert — 기본값 확인
        Assert.Equal("Lamp", dto.TypeDevice);
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);

        // Act — 왕복 직렬화
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<LampDeviceDto>(json);

        // Assert — 왕복 검증
        Assert.NotNull(deserialized);
        Assert.Equal(501, deserialized.Id);
        Assert.Equal(70, deserialized.NumberDevice);
        Assert.Equal("Lamp-070", deserialized.NameDevice);
        Assert.Equal("Lamp", deserialized.TypeDevice);
        Assert.Equal("ACTIVATED", deserialized.Status);
    }
}

/// <summary>
/// GeolocationDto 테스트
/// </summary>
public class GeolocationDtoTests
{
    [Fact(DisplayName = "A2.5: GeolocationDto 직렬화 시 latitude, longitude, altitude 필드 왕복 검증")]
    [Trait("Category", "DTO")]
    public void GeolocationDto_Serialization_ShouldHaveLatLonAlt()
    {
        // Arrange
        var dto = new GeolocationDto
        {
            Latitude = 37.5665,
            Longitude = 126.9780,
            Altitude = 85.5
        };

        // Act — 왕복 직렬화
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<GeolocationDto>(json);

        // Assert — snake_case 필드 확인
        Assert.Contains("\"latitude\":", json);
        Assert.Contains("\"longitude\":", json);
        Assert.Contains("\"altitude\":", json);

        // Assert — 왕복 검증
        Assert.NotNull(deserialized);
        Assert.Equal(37.5665, deserialized.Latitude);
        Assert.Equal(126.9780, deserialized.Longitude);
        Assert.Equal(85.5, deserialized.Altitude);
    }

    #region A9.1: GeolocationDto location 필드 추가 (§14.2.5)

    [Fact(DisplayName = "A9.1: GeolocationDto location 포함 왕복 직렬화")]
    [Trait("Category", "DTO")]
    public void GeolocationDto_WithLocation_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var dto = new GeolocationDto
        {
            Location = "GOP 1구역 전방 초소",
            Latitude = 38.1234,
            Longitude = 127.5678,
            Altitude = 0
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<GeolocationDto>(json);

        // Assert
        Assert.Contains("\"location\":", json);
        Assert.Contains("GOP 1구역 전방 초소", json);
        Assert.NotNull(deserialized);
        Assert.Equal("GOP 1구역 전방 초소", deserialized.Location);
        Assert.Equal(38.1234, deserialized.Latitude);
        Assert.Equal(127.5678, deserialized.Longitude);
    }

    [Fact(DisplayName = "A9.1: GeolocationDto location 없는 JSON → null 역직렬화 (하위 호환)")]
    [Trait("Category", "DTO")]
    public void GeolocationDto_WithoutLocation_ShouldDeserializeWithNull()
    {
        // Arrange — location 없는 기존 형식 JSON
        var json = """{"latitude":37.5665,"longitude":126.978,"altitude":85.5}""";

        // Act
        var deserialized = JsonConvert.DeserializeObject<GeolocationDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Location);
        Assert.Equal(37.5665, deserialized.Latitude);
    }

    #endregion
}

/// <summary>
/// CameraUrlsDto 테스트
/// </summary>
public class CameraUrlsDtoTests
{
    [Fact(DisplayName = "A2.6: CameraUrlsDto 직렬화 시 rtsp, http, snapshot 필드 왕복 검증")]
    [Trait("Category", "DTO")]
    public void CameraUrlsDto_Serialization_ShouldHaveRtspHttpSnapshot()
    {
        // Arrange — nested 구조
        var dto = new CameraUrlsDto
        {
            Homepage = new CameraHomepageDto { Url = "http://192.168.1.100:80/video" },
            Onvif = new CameraOnvifDto { DeviceService = "http://192.168.1.100:8000/onvif/device_service" },
            Streams = new CameraStreamsDto
            {
                Rtsp = new CameraRtspDto
                {
                    Main = "rtsp://192.168.1.100:554/stream1",
                    Sub = "rtsp://192.168.1.100:554/stream2"
                },
                Webrtc = new CameraWebrtcDto { Main = "https://192.168.1.100/webrtc/main" }
            },
            Snapshot = new CameraSnapshotDto { Ch1 = "http://192.168.1.100:80/snapshot.jpg" }
        };

        // Act — 왕복 직렬화
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<CameraUrlsDto>(json);

        // Assert — nested 구조 확인
        Assert.Contains("\"homepage\":{", json);
        Assert.Contains("\"streams\":{", json);
        Assert.Contains("\"snapshot\":{", json);

        // Assert — 왕복 검증
        Assert.NotNull(deserialized);
        Assert.Equal("rtsp://192.168.1.100:554/stream1", deserialized.Streams!.Rtsp!.Main);
        Assert.Equal("http://192.168.1.100:80/video", deserialized.Homepage!.Url);
        Assert.Equal("http://192.168.1.100:80/snapshot.jpg", deserialized.Snapshot!.Ch1);
    }
}

/// <summary>
/// A2.7 부가 DTO 일괄 테스트
/// </summary>
public class AuxiliaryDtoTests
{
    [Fact(DisplayName = "A2.7-1: DeviceGroupDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void DeviceGroupDto_Serialization_RoundTrip()
    {
        var dto = new DeviceGroupDto { Id = 1, Name = "경계구역-A", Description = "동측 펜스" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<DeviceGroupDto>(json);

        Assert.Contains("\"name\":", json);
        Assert.Contains("\"description\":", json);
        Assert.NotNull(d);
        Assert.Equal(1, d.Id);
        Assert.Equal("경계구역-A", d.Name);
        Assert.Equal("동측 펜스", d.Description);
    }

    [Fact(DisplayName = "A9.2: DeviceGroupDto device_count 포함 왕복 직렬화")]
    [Trait("Category", "DTO")]
    public void DeviceGroupDto_WithDeviceCount_ShouldSerializeAndDeserialize()
    {
        // Arrange — 설계 문서 NATS v1.1 §6.1 device_groups[] 항목
        var json = """{"id":1,"name":"A구역 센서그룹","description":"A구역 센서 장비 그룹","device_count":5}""";

        // Act
        var dto = JsonConvert.DeserializeObject<DeviceGroupDto>(json);
        var reserialized = JsonConvert.SerializeObject(dto);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("A구역 센서그룹", dto.Name);
        Assert.Equal(5, dto.DeviceCount);
        Assert.Contains("\"device_count\":5", reserialized);
    }

    [Fact(DisplayName = "A2.7-2: CameraSettingDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void CameraSettingDto_Serialization_RoundTrip()
    {
        var dto = new CameraSettingDto { CameraId = 109, Palette = "WHITE_HOT", Heater = "on", Fan = "on" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<CameraSettingDto>(json);

        Assert.Contains("\"camera_id\":109", json);
        Assert.Contains("\"palette\":", json);
        Assert.NotNull(d);
        Assert.Equal(109, d.CameraId);
        Assert.Equal("WHITE_HOT", d.Palette);
        Assert.Equal("on", d.Heater);
        Assert.Equal("on", d.Fan);
    }

    [Fact(DisplayName = "A2.7-3: FileGroupDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void FileGroupDto_Serialization_RoundTrip()
    {
        var dto = new FileGroupDto { Id = 10, Name = "경고방송", Files = new List<string> { "alert1.mp3", "alert2.wav" }, Description = "경고 방송 파일" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<FileGroupDto>(json);

        Assert.Contains("\"files\":", json);
        Assert.NotNull(d);
        Assert.Equal(10, d.Id);
        Assert.Equal("경고방송", d.Name);
        Assert.Equal(2, d.Files.Count);
        Assert.Contains("alert1.mp3", d.Files);
    }

    [Fact(DisplayName = "A2.7-4: ServerDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void ServerDto_Serialization_RoundTrip()
    {
        var dto = new ServerDto { Id = 1, CategoryId = 5, Name = "DB-Server", Status = "NORMAL", IpAddress = "10.0.0.1", Port = 5432 };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<ServerDto>(json);

        Assert.Contains("\"category_id\":5", json);
        Assert.Contains("\"ip_address\":", json);
        Assert.Contains("\"port\":", json);
        Assert.Contains("\"status\":\"NORMAL\"", json);
        Assert.NotNull(d);
        Assert.Equal("DB-Server", d.Name);
        Assert.Equal(5, d.CategoryId);
        Assert.Equal(5432, d.Port);
    }

    [Fact(DisplayName = "A2.7-5: CategoryDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void CategoryDto_Serialization_RoundTrip()
    {
        var dto = new CategoryDto { Id = 5, Name = "침입탐지", TypeServer = "EVENT", Description = "침입 탐지 카테고리", SortOrder = 2 };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<CategoryDto>(json);

        Assert.Contains("\"type_server\":", json);
        Assert.Contains("\"sort_order\":2", json);
        Assert.NotNull(d);
        Assert.Equal(5, d.Id);
        Assert.Equal("침입탐지", d.Name);
        Assert.Equal("EVENT", d.TypeServer);
        Assert.Equal(2, d.SortOrder);
    }
}

/// <summary>
/// DetectionDetailDto + DetectedObjectDto 테스트
/// </summary>
public class DetectionDetailDtoTests
{
    [Fact(DisplayName = "A2.8-1: DetectionDetailDto 직렬화 시 signal, thumbnail, objects, model, inference_ms 필드")]
    [Trait("Category", "DTO")]
    public void DetectionDetailDto_Serialization_ShouldHaveSignalThumbnailObjects()
    {
        // Arrange
        var dto = new DetectionDetailDto
        {
            Signal = 85,
            Thumbnail = "https://cdn.example.com/thumb/001.jpg",
            Model = "yolov8n",
            InferenceMs = 42,
            Objects = new List<DetectedObjectDto>
            {
                new DetectedObjectDto { Label = "person", Confidence = 0.95, Bbox = new List<int> { 100, 200, 50, 80 } },
                new DetectedObjectDto { Label = "vehicle", Confidence = 0.87, Bbox = new List<int> { 300, 150, 120, 60 } }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<DetectionDetailDto>(json);

        // Assert
        Assert.Contains("\"signal\":85", json);
        Assert.Contains("\"thumbnail\":", json);
        Assert.Contains("\"model\":\"yolov8n\"", json);
        Assert.Contains("\"inference_ms\":42", json);
        Assert.Contains("\"objects\":", json);
        Assert.NotNull(d);
        Assert.Equal(85, d.Signal);
        Assert.Equal("yolov8n", d.Model);
        Assert.Equal(2, d.Objects?.Count);
        Assert.Equal("person", d.Objects![0].Label);
        Assert.Equal(0.95, d.Objects[0].Confidence);
    }

    [Fact(DisplayName = "A2.8-2: DetectionDetailDto JsonExtensionData로 미지 필드 보존")]
    [Trait("Category", "DTO")]
    public void DetectionDetailDto_WithJsonExtensionData_ShouldPreserveUnknownFields()
    {
        // Arrange — Backend에서 추가된 미지 필드
        var json = @"{
            ""signal"": 90,
            ""thumbnail"": ""url"",
            ""model"": ""yolov8n"",
            ""future_field_1"": ""hello"",
            ""future_field_2"": 42
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<DetectionDetailDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(90, dto.Signal);
        Assert.NotNull(dto.AdditionalData);
        Assert.True(dto.AdditionalData.ContainsKey("future_field_1"));
        Assert.Equal("hello", dto.AdditionalData["future_field_1"]?.ToString());

        // 재직렬화 시 미지 필드 유지
        var reJson = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"future_field_1\":", reJson);
        Assert.Contains("\"future_field_2\":", reJson);
    }

    [Fact(DisplayName = "A2.8-3: DetectedObjectDto 직렬화 시 label, confidence, bbox 왕복 검증")]
    [Trait("Category", "DTO")]
    public void DetectedObjectDto_Serialization_ShouldHaveLabelConfidenceBbox()
    {
        // Arrange
        var dto = new DetectedObjectDto
        {
            Label = "person",
            Confidence = 0.92,
            Bbox = new List<int> { 10, 20, 30, 40 }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<DetectedObjectDto>(json);

        // Assert
        Assert.Contains("\"label\":\"person\"", json);
        Assert.Contains("\"confidence\":0.92", json);
        Assert.Contains("\"bbox\":[10,20,30,40]", json);
        Assert.NotNull(d);
        Assert.Equal("person", d.Label);
        Assert.Equal(0.92, d.Confidence);
        Assert.Equal(4, d.Bbox?.Count);
    }
}

/// <summary>
/// MalfunctionDetailDto 테스트
/// </summary>
public class MalfunctionDetailDtoTests
{
    [Fact(DisplayName = "A2.9-1: MalfunctionDetailDto 직렬화 시 first_start, first_end, second_start, second_end")]
    [Trait("Category", "DTO")]
    public void MalfunctionDetailDto_Serialization_ShouldHaveFirstSecondStartEnd()
    {
        var dto = new MalfunctionDetailDto
        {
            FirstStart = 10,
            FirstEnd = 50,
            SecondStart = 60,
            SecondEnd = 100
        };

        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<MalfunctionDetailDto>(json);

        Assert.Contains("\"first_start\":10", json);
        Assert.Contains("\"first_end\":50", json);
        Assert.Contains("\"second_start\":60", json);
        Assert.Contains("\"second_end\":100", json);
        Assert.NotNull(d);
        Assert.Equal(10, d.FirstStart);
        Assert.Equal(100, d.SecondEnd);
    }

    [Fact(DisplayName = "A2.9-2: MalfunctionDetailDto JsonExtensionData로 미지 필드 보존")]
    [Trait("Category", "DTO")]
    public void MalfunctionDetailDto_WithJsonExtensionData_ShouldPreserveUnknownFields()
    {
        var json = @"{ ""first_start"": 5, ""unknown_field"": ""value"" }";
        var dto = JsonConvert.DeserializeObject<MalfunctionDetailDto>(json);

        Assert.NotNull(dto);
        Assert.Equal(5, dto.FirstStart);
        Assert.NotNull(dto.AdditionalData);
        Assert.True(dto.AdditionalData.ContainsKey("unknown_field"));

        var reJson = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"unknown_field\":", reJson);
    }
}

/// <summary>
/// EventMapping DTO 테스트 (Camera, Speaker, Lamp)
/// </summary>
public class EventMappingDtoTests
{
    [Fact(DisplayName = "A2.10: EventMappingCameraDto 직렬화 검증")]
    [Trait("Category", "DTO")]
    public void EventMappingCameraDto_Serialization_ShouldMatchDesignDoc()
    {
        var dto = new Dto.Integrations.EventMappingCameraDto
        {
            CameraId = 109,
            TargetPresetId = 3,
            HomePresetId = 1,
            DelayTime = 5,
            Priority = 1
        };

        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<Dto.Integrations.EventMappingCameraDto>(json);

        Assert.Contains("\"camera_id\":109", json);
        Assert.Contains("\"target_preset_id\":3", json);
        Assert.Contains("\"home_preset_id\":1", json);
        Assert.Contains("\"delay_time\":5", json);
        Assert.Contains("\"priority\":1", json);
        Assert.NotNull(d);
        Assert.Equal(109, d.CameraId);
        Assert.Equal(3, d.TargetPresetId);
    }

    [Fact(DisplayName = "A2.11: EventMappingSpeakerDto 직렬화 검증")]
    [Trait("Category", "DTO")]
    public void EventMappingSpeakerDto_Serialization_ShouldMatchDesignDoc()
    {
        var dto = new Dto.Integrations.EventMappingSpeakerDto
        {
            SpeakerId = 50,
            FileGroupId = 10,
            RepeatCount = 3,
            Priority = 2
        };

        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<Dto.Integrations.EventMappingSpeakerDto>(json);

        Assert.Contains("\"speaker_id\":50", json);
        Assert.Contains("\"file_group_id\":10", json);
        Assert.Contains("\"repeat_count\":3", json);
        Assert.Contains("\"priority\":2", json);
        Assert.NotNull(d);
        Assert.Equal(50, d.SpeakerId);
    }

    [Fact(DisplayName = "A2.12: EventMappingLampDto 직렬화 검증")]
    [Trait("Category", "DTO")]
    public void EventMappingLampDto_Serialization_ShouldMatchDesignDoc()
    {
        var dto = new Dto.Integrations.EventMappingLampDto
        {
            LampId = 70,
            Color = "Red",
            LightMode = "steady",
            BuzzerSound = "PI-PI-PI",
            BuzzerTime = 5,
            Priority = 1
        };

        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<Dto.Integrations.EventMappingLampDto>(json);

        Assert.Contains("\"lamp_id\":70", json);
        Assert.Contains("\"color\":\"Red\"", json);
        Assert.Contains("\"light_mode\":\"steady\"", json);
        Assert.Contains("\"buzzer_sound\":\"PI-PI-PI\"", json);
        Assert.Contains("\"priority\":1", json);
        Assert.NotNull(d);
        Assert.Equal(70, d.LampId);
        Assert.Equal("Red", d.Color);
    }
}

/// <summary>
/// BrokerPublish 테스트
/// </summary>
public class BrokerPublishTests
{
    [Fact(DisplayName = "A2.13-1: BrokerPublish TypeMessage는 PUB이고 직렬화 검증")]
    [Trait("Category", "Broker")]
    public void BrokerPublish_ShouldHaveTypePUB_AndSerialize()
    {
        var pub = new BrokerPublish<EventCallDto>();
        pub.Command = "EVENT_NOTIFY";
        pub.From = "gop-service";
        pub.Data = new EventCallDto { EventName = "Detection-001", State = "ACTIVE" };

        Assert.Equal("PUB", pub.TypeMessage);

        var json = JsonConvert.SerializeObject(pub);
        Assert.Contains("\"m_type\":\"PUB\"", json);
        Assert.Contains("\"cmd\":\"EVENT_NOTIFY\"", json);
        Assert.Contains("\"event_name\":\"Detection-001\"", json);
    }

    [Fact(DisplayName = "A2.13-2: BrokerPublish 왕복 직렬화 검증")]
    [Trait("Category", "Broker")]
    public void BrokerPublish_RoundTrip_ShouldPreserveData()
    {
        var pub = new BrokerPublish<EventCallDto>();
        pub.Id = Guid.NewGuid().ToString();
        pub.Command = "STATUS_CHANGE";
        pub.From = "test-service";
        pub.Data = new EventCallDto { EventName = "Test-Event", State = "COMPLETED" };

        var json = JsonConvert.SerializeObject(pub);
        var deserialized = JsonConvert.DeserializeObject<BrokerPublish<EventCallDto>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("PUB", deserialized.TypeMessage);
        Assert.Equal(pub.Id, deserialized.Id);
        Assert.Equal("STATUS_CHANGE", deserialized.Command);
        Assert.Equal("test-service", deserialized.From);
        Assert.Equal("Test-Event", deserialized.Data?.EventName);
        Assert.Equal("COMPLETED", deserialized.Data?.State);
    }
}

/// <summary>
/// NATS Body DTO 테스트 — A2.14~A2.18
/// </summary>
public class NatsBodyDtoTests
{
    #region A2.14: PidsProxy 제어 (2종)
    [Fact(DisplayName = "A2.14-1: ModeChangeBodyDto 직렬화")]
    [Trait("Category", "DTO")]
    public void ModeChangeBodyDto_Serialization_ShouldHaveMode()
    {
        var dto = new Dto.Brokers.ModeChangeBodyDto { Mode = "NORMAL" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<Dto.Brokers.ModeChangeBodyDto>(json);
        Assert.Contains("\"mode\":\"NORMAL\"", json);
        Assert.NotNull(d);
        Assert.Equal("NORMAL", d.Mode);
    }

    [Fact(DisplayName = "A2.14-2: WindyBodyDto 직렬화")]
    [Trait("Category", "DTO")]
    public void WindyBodyDto_Serialization_ShouldHaveMode()
    {
        var dto = new Dto.Brokers.WindyBodyDto { Mode = "STRONG" };
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<Dto.Brokers.WindyBodyDto>(json);
        Assert.Contains("\"mode\":\"STRONG\"", json);
        Assert.NotNull(d);
        Assert.Equal("STRONG", d.Mode);
    }
    #endregion

    #region A2.15: Broadcasting 제어 (4종)
    [Fact(DisplayName = "A2.15: Broadcasting Body DTOs 직렬화")]
    [Trait("Category", "DTO")]
    public void BroadcastingBodyDtos_Serialization_AllFields()
    {
        // TtsBodyDto
        var tts = new Dto.Brokers.TtsBodyDto { SpeakerIds = new List<int> { 1, 2 }, Message = "경고방송" };
        var ttsJson = JsonConvert.SerializeObject(tts);
        Assert.Contains("\"speaker_ids\":[1,2]", ttsJson);
        Assert.Contains("\"message\":\"경고방송\"", ttsJson);

        // BroadcastPlayBodyDto
        var play = new Dto.Brokers.BroadcastPlayBodyDto { SpeakerIds = new List<int> { 3 }, FileGroupId = 10, Repeat = 2 };
        var playJson = JsonConvert.SerializeObject(play);
        Assert.Contains("\"file_group_id\":10", playJson);
        Assert.Contains("\"repeat\":2", playJson);

        // BroadcastStopBodyDto
        var stop = new Dto.Brokers.BroadcastStopBodyDto { SpeakerIds = new List<int> { 3 } };
        var stopJson = JsonConvert.SerializeObject(stop);
        Assert.Contains("\"speaker_ids\":[3]", stopJson);

        // BroadcastTestBodyDto
        var test = new Dto.Brokers.BroadcastTestBodyDto { SpeakerIds = new List<int> { 5 }, FileGroupId = 1, DurationSec = 30 };
        var testJson = JsonConvert.SerializeObject(test);
        Assert.Contains("\"duration_sec\":30", testJson);
    }
    #endregion

    #region A2.16: Lamp 제어 (6종)
    [Fact(DisplayName = "A2.16: Lamp Body DTOs 직렬화")]
    [Trait("Category", "DTO")]
    public void LampBodyDtos_Serialization_AllFields()
    {
        var clear = new Dto.Brokers.LampClearBodyDto { LampIds = new List<int> { 1, 2 } };
        Assert.Contains("\"lamp_ids\":[1,2]", JsonConvert.SerializeObject(clear));

        var off = new Dto.Brokers.LampOffBodyDto { LampIds = new List<int> { 1 } };
        Assert.Contains("\"lamp_ids\":[1]", JsonConvert.SerializeObject(off));

        var colorSet = new Dto.Brokers.LampColorSetBodyDto { LampIds = new List<int> { 1 }, Color = "RED", Mode = "FLASH" };
        var csJson = JsonConvert.SerializeObject(colorSet);
        Assert.Contains("\"color\":\"RED\"", csJson);
        Assert.Contains("\"mode\":\"FLASH\"", csJson);

        var buzzerSet = new Dto.Brokers.LampBuzzerSetBodyDto { LampIds = new List<int> { 1 }, Buzzer = "ALARM" };
        Assert.Contains("\"buzzer\":\"ALARM\"", JsonConvert.SerializeObject(buzzerSet));

        var colorTest = new Dto.Brokers.LampColorTestBodyDto { LampIds = new List<int> { 1 }, Color = "GREEN", Mode = "STEADY", DurationSec = 10 };
        Assert.Contains("\"duration_sec\":10", JsonConvert.SerializeObject(colorTest));

        var buzzerTest = new Dto.Brokers.LampBuzzerTestBodyDto { LampIds = new List<int> { 1 }, Buzzer = "BEEP", DurationSec = 5 };
        Assert.Contains("\"duration_sec\":5", JsonConvert.SerializeObject(buzzerTest));
    }
    #endregion

    #region A2.17: NVR/Camera 제어 (13종)
    [Fact(DisplayName = "A2.17-1: Camera PTZ/Tracking Body DTOs 직렬화")]
    [Trait("Category", "DTO")]
    public void CameraBodyDtos_PtzAndTracking_Serialization()
    {
        var ptz = new Dto.Brokers.PtzControlBodyDto { CameraId = 109, PanTiltSpeed = 5, TimeoutMs = 3000 };
        var ptzJson = JsonConvert.SerializeObject(ptz);
        Assert.Contains("\"camera_id\":109", ptzJson);
        Assert.Contains("\"pan_tilt_speed\":5", ptzJson);
        Assert.Contains("\"timeout_ms\":3000", ptzJson);

        var ptzStatus = new Dto.Brokers.PtzStatusBodyDto { CameraId = 109, Pan = 1000, Tilt = 5000, Zoom = 2000 };
        var ptzStatusJson = JsonConvert.SerializeObject(ptzStatus);
        Assert.Contains("\"pan\":1000", ptzStatusJson);
        Assert.Contains("\"tilt\":5000", ptzStatusJson);
        Assert.Contains("\"zoom\":2000", ptzStatusJson);

        // A2.17-1b: PtzStatusBodyDto pan/tilt/zoom는 int 타입 (설계 문서 §8.3.1)
        var deserialized = JsonConvert.DeserializeObject<Dto.Brokers.PtzStatusBodyDto>(
            "{\"camera_id\":201,\"pan\":1000,\"tilt\":5000,\"zoom\":2000}");
        Assert.Equal(typeof(int), deserialized!.Pan.GetType());
        Assert.Equal(typeof(int), deserialized.Tilt.GetType());
        Assert.Equal(typeof(int), deserialized.Zoom.GetType());

        var trackSet = new Dto.Brokers.TrackingSetBodyDto { CameraId = 109, Tracking = "on" };
        Assert.Contains("\"tracking\":\"on\"", JsonConvert.SerializeObject(trackSet));

        var trackStatus = new Dto.Brokers.TrackingStatusBodyDto { CameraId = 109, Tracking = "active" };
        Assert.Contains("\"tracking\":\"active\"", JsonConvert.SerializeObject(trackStatus));
    }

    [Fact(DisplayName = "A2.17-2: Camera Mode/Peripheral Body DTOs 직렬화")]
    [Trait("Category", "DTO")]
    public void CameraBodyDtos_ModeAndPeripheral_Serialization()
    {
        Assert.Contains("\"palette\":\"WHITE_HOT\"", JsonConvert.SerializeObject(new Dto.Brokers.PaletteSetBodyDto { CameraId = 1, Palette = "WHITE_HOT" }));
        Assert.Contains("\"wiper\":\"on\"", JsonConvert.SerializeObject(new Dto.Brokers.WiperSetBodyDto { CameraId = 1, Wiper = "on" }));
        Assert.Contains("\"heater\":\"on\"", JsonConvert.SerializeObject(new Dto.Brokers.HeaterSetBodyDto { CameraId = 1, Heater = "on" }));
        Assert.Contains("\"fan\":\"on\"", JsonConvert.SerializeObject(new Dto.Brokers.FanSetBodyDto { CameraId = 1, Fan = "on" }));
        Assert.Contains("\"weather_mode\":\"FOG\"", JsonConvert.SerializeObject(new Dto.Brokers.WeatherModeSetBodyDto { CameraId = 1, WeatherMode = "FOG" }));
        Assert.Contains("\"camera_mode\":\"DAY\"", JsonConvert.SerializeObject(new Dto.Brokers.CameraModeSetBodyDto { CameraId = 1, CameraMode = "DAY" }));
        Assert.Contains("\"headlight\":\"off\"", JsonConvert.SerializeObject(new Dto.Brokers.HeadlightSetBodyDto { CameraId = 1, Headlight = "off" }));
        Assert.Contains("\"day_night_mode\":\"AUTO\"", JsonConvert.SerializeObject(new Dto.Brokers.DayNightSetBodyDto { CameraId = 1, DayNightMode = "AUTO" }));
        Assert.Contains("\"power\":\"on\"", JsonConvert.SerializeObject(new Dto.Brokers.PowerSetBodyDto { CameraId = 1, Power = "on" }));
    }
    #endregion

    #region A2.18: 마스터 데이터 동기화 (9종)
    [Fact(DisplayName = "A2.18: Sync Body DTOs 직렬화")]
    [Trait("Category", "DTO")]
    public void SyncBodyDtos_Serialization_AllFields()
    {
        var syncDev = new Dto.Brokers.SyncDeviceBodyDto { Action = "CREATE", TypeDevice = "Controller", ResourceId = 1 };
        var json = JsonConvert.SerializeObject(syncDev);
        Assert.Contains("\"action\":\"CREATE\"", json);
        Assert.Contains("\"type_device\":\"Controller\"", json);
        Assert.Contains("\"resource_id\":1", json);

        Assert.Contains("\"action\":\"UPDATE\"", JsonConvert.SerializeObject(new Dto.Brokers.SyncServerBodyDto { Action = "UPDATE", ResourceId = 2 }));
        Assert.Contains("\"action\":\"DELETE\"", JsonConvert.SerializeObject(new Dto.Brokers.SyncCategoryBodyDto { Action = "DELETE", ResourceId = 3 }));
        Assert.Contains("\"resource_id\":4", JsonConvert.SerializeObject(new Dto.Brokers.SyncDeviceGroupBodyDto { Action = "CREATE", ResourceId = 4 }));
        Assert.Contains("\"resource_id\":5", JsonConvert.SerializeObject(new Dto.Brokers.SyncEventMappingBodyDto { Action = "UPDATE", ResourceId = 5 }));
        Assert.Contains("\"camera_id\":109", JsonConvert.SerializeObject(new Dto.Brokers.SyncPresetBodyDto { Action = "CREATE", ResourceId = 6, CameraId = 109 }));
        Assert.Contains("\"resource_id\":7", JsonConvert.SerializeObject(new Dto.Brokers.SyncFileGroupBodyDto { Action = "UPDATE", ResourceId = 7 }));
        Assert.Contains("\"camera_id\":110", JsonConvert.SerializeObject(new Dto.Brokers.SyncCameraSettingBodyDto { Action = "UPDATE", CameraId = 110 }));
        Assert.Contains("\"server_id\":1", JsonConvert.SerializeObject(new Dto.Brokers.SyncProxySettingBodyDto { Action = "UPDATE", ServerId = 1 }));
    }
    #endregion
}
#endregion

#region - Phase 3: 기존 Device DTO 수정 테스트 -
/// <summary>
/// Phase 3: 기존 Device DTO BaseDeviceDto 상속 전환 + 하위 호환
/// </summary>
public class DeviceDtoMigrationTests
{
    [Fact(DisplayName = "A3.1-1: ControllerDeviceDto 기존 JSON 하위 호환 역직렬화")]
    [Trait("Category", "DTO")]
    public void ControllerDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize()
    {
        // Arrange — 기존 형식 JSON (group_device 포함, device_groups 없음)
        var json = @"{
            ""id"": 1,
            ""number_device"": 101,
            ""group_device"": 1,
            ""name_device"": ""Controller-A"",
            ""type_device"": ""Controller"",
            ""version"": ""1.0.0"",
            ""status"": ""ACTIVATED"",
            ""ip_address"": ""192.168.1.1"",
            ""ip_port"": 8080,
            ""created_at"": ""2025-12-01T10:00:00.000+09:00""
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<ControllerDeviceDto>(json);

        // Assert — 기존 필드 모두 정상 매핑
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Equal(101, dto.NumberDevice);
        Assert.Equal("Controller-A", dto.NameDevice);
        Assert.Equal("Controller", dto.TypeDevice);
        Assert.Equal("1.0.0", dto.Version);
        Assert.Equal("ACTIVATED", dto.Status);
        Assert.Equal("192.168.1.1", dto.IpAddress);
        Assert.Equal(8080, dto.IpPort);

        // Assert — 신규 필드는 null/default
        Assert.Null(dto.DeviceGroups);
        Assert.False(dto.IsEnable);

        // Assert — BaseDeviceDto 상속 확인
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);
    }

    [Fact(DisplayName = "A3.1-2: ControllerDeviceDto 신규 필드 직렬화")]
    [Trait("Category", "DTO")]
    public void ControllerDeviceDto_NewFields_ShouldSerializeWhenPresent()
    {
        var dto = new ControllerDeviceDto
        {
            Id = 1,
            NumberDevice = 101,
            NameDevice = "Controller-A",
            Version = "2.0.0",
            Status = "ACTIVATED",
            IsEnable = true,
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 }, new() { Id = 3 } }
        };

        var json = JsonConvert.SerializeObject(dto);

        // 기존 필드 유지
        Assert.Contains("\"version\":\"2.0.0\"", json);

        // 신규 필드 존재
        Assert.Contains("\"is_enable\":true", json);
        Assert.Contains("\"device_groups\":", json);

        // TypeDevice 기본값
        Assert.Equal("Controller", dto.TypeDevice);
    }

    [Fact(DisplayName = "A3.2-1: SensorDeviceDto 기존 JSON 하위 호환 역직렬화")]
    [Trait("Category", "DTO")]
    public void SensorDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize()
    {
        // Arrange — 기존 형식 JSON (group_device 포함, device_groups/ip_address/ip_port 없음)
        var json = @"{
            ""id"": 10,
            ""number_device"": 201,
            ""group_device"": 1,
            ""name_device"": ""Sensor-A"",
            ""type_device"": ""Fence"",
            ""version"": ""1.0.0"",
            ""status"": ""ACTIVATED"",
            ""controller_id"": 101,
            ""created_at"": ""2025-12-01T10:00:00.000+09:00""
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<SensorDeviceDto>(json);

        // Assert — 기존 필드 모두 정상 매핑
        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal(201, dto.NumberDevice);
        Assert.Equal("Sensor-A", dto.NameDevice);
        Assert.Equal("Fence", dto.TypeDevice);
        Assert.Equal("1.0.0", dto.Version);
        Assert.Equal("ACTIVATED", dto.Status);
        Assert.Equal(101, dto.ControllerId);

        // Assert — 신규 필드는 null/default
        Assert.Null(dto.DeviceGroups);
        Assert.False(dto.IsEnable);

        // Assert — BaseDeviceDto 상속 확인
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);
    }

    [Fact(DisplayName = "A3.2-2: SensorDeviceDto 신규 필드 직렬화")]
    [Trait("Category", "DTO")]
    public void SensorDeviceDto_NewFields_ShouldSerializeWhenPresent()
    {
        var dto = new SensorDeviceDto
        {
            Id = 10,
            NumberDevice = 201,
            NameDevice = "Sensor-A",
            TypeDevice = "Fence",
            Version = "2.0.0",
            Status = "ACTIVATED",
            ControllerId = 101,
            IsEnable = true,
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 }, new() { Id = 2 } }
        };

        var json = JsonConvert.SerializeObject(dto);

        // 기존 필드 유지
        Assert.Contains("\"version\":\"2.0.0\"", json);
        Assert.Contains("\"controller_id\":101", json);

        // 신규 필드 존재
        Assert.Contains("\"is_enable\":true", json);
        Assert.Contains("\"device_groups\":[{", json);
    }

    [Fact(DisplayName = "A3.3-1: CameraDeviceDto 기존 JSON 하위 호환 역직렬화")]
    [Trait("Category", "DTO")]
    public void CameraDeviceDto_BackwardCompat_ExistingJsonShouldStillDeserialize()
    {
        // Arrange — 기존 형식 JSON (rtsp_uri, rtsp_port, group_device 포함, urls/is_record 없음)
        var json = @"{
            ""id"": 20,
            ""number_device"": 301,
            ""group_device"": 2,
            ""name_device"": ""PTZ-Camera-01"",
            ""type_device"": ""IpCamera"",
            ""version"": ""1.0.0"",
            ""status"": ""ACTIVATED"",
            ""ip_address"": ""192.168.1.100"",
            ""ip_port"": 80,
            ""user_name"": ""admin"",
            ""user_password"": ""pass123"",
            ""rtsp_uri"": ""rtsp://192.168.1.100:554/stream1"",
            ""rtsp_port"": 554,
            ""mode"": ""ONVIF"",
            ""category"": ""PTZ"",
            ""created_at"": ""2025-12-01T10:00:00.000+09:00""
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<CameraDeviceDto>(json);

        // Assert — 기존 필드 모두 정상 매핑
        Assert.NotNull(dto);
        Assert.Equal(20, dto.Id);
        Assert.Equal(301, dto.NumberDevice);
        Assert.Equal("PTZ-Camera-01", dto.NameDevice);
        Assert.Equal("IpCamera", dto.TypeDevice);
        Assert.Equal("192.168.1.100", dto.IpAddress);
        Assert.Equal(80, dto.IpPort);
        Assert.Equal("admin", dto.UserName);
        Assert.Equal("pass123", dto.UserPassword);
        Assert.Equal("rtsp://192.168.1.100:554/stream1", dto.RtspUri);
        Assert.Equal(554, dto.RtspPort);
        Assert.Equal("ONVIF", dto.Mode);
        Assert.Equal("PTZ", dto.Category);

        // Assert — 신규 필드는 null/default
        Assert.Null(dto.DeviceGroups);
        Assert.Null(dto.Urls);
        Assert.Null(dto.IsRecord);
        Assert.Null(dto.HardwareSpec);
        Assert.False(dto.IsEnable);

        // Assert — BaseDeviceDto 상속 확인
        Assert.IsAssignableFrom<BaseDeviceDto>(dto);
    }

    [Fact(DisplayName = "A3.3-2: CameraDeviceDto urls 중첩 객체 직렬화")]
    [Trait("Category", "DTO")]
    public void CameraDeviceDto_NewUrlsField_ShouldSerializeAsNestedObject()
    {
        var dto = new CameraDeviceDto
        {
            Id = 20,
            NumberDevice = 301,
            NameDevice = "PTZ-Camera-01",
            Urls = new CameraUrlsDto
            {
                Homepage = new CameraHomepageDto { Url = "http://192.168.1.100/live" },
                Streams = new CameraStreamsDto
                {
                    Rtsp = new CameraRtspDto { Main = "rtsp://192.168.1.100:554/stream1" }
                },
                Snapshot = new CameraSnapshotDto { Ch1 = "http://192.168.1.100/snapshot" }
            }
        };

        var json = JsonConvert.SerializeObject(dto);

        // urls가 중첩 객체로 직렬화
        Assert.Contains("\"urls\":{", json);
        Assert.Contains("\"homepage\":{", json);
        Assert.Contains("\"streams\":{", json);
        Assert.Contains("\"snapshot\":{", json);
    }

    [Fact(DisplayName = "A3.3-3: CameraDeviceDto is_record, hardware_spec 필드")]
    [Trait("Category", "DTO")]
    public void CameraDeviceDto_NewFields_IsRecordAndHardwareSpec()
    {
        var dto = new CameraDeviceDto
        {
            Id = 20,
            NumberDevice = 301,
            IsRecord = true,
            HardwareSpec = new HardwareSpecDto { Name = "Thermal Camera", Model = "640x480" }
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"is_record\":true", json);
        Assert.Contains("\"hardware_spec\":{", json);
        Assert.Contains("\"name\":\"Thermal Camera\"", json);

        // 역직렬화 왕복
        var restored = JsonConvert.DeserializeObject<CameraDeviceDto>(json);
        Assert.NotNull(restored);
        Assert.True(restored.IsRecord);
        Assert.NotNull(restored.HardwareSpec);
        Assert.Equal("Thermal Camera", restored.HardwareSpec.Name);
        Assert.Equal("640x480", restored.HardwareSpec.Model);
    }

    /// <summary>
    /// A3.4: 모든 Device DTO가 신규 필드 없는 구 JSON에서도 정상 역직렬화 (통합 테스트)
    /// </summary>
    [Fact(DisplayName = "A3.4: 전체 Device DTO 구 JSON 하위 호환 통합 검증")]
    [Trait("Category", "DTO")]
    public void AllDeviceDtos_OldJsonWithoutNewFields_ShouldDeserializeWithDefaults()
    {
        // Controller — 구 JSON (is_enable, device_groups, description 없음)
        var controllerJson = @"{ ""id"":1, ""number_device"":101, ""group_device"":1, ""name_device"":""Ctrl-A"", ""type_device"":""Controller"", ""version"":""1.0"", ""status"":""ACTIVATED"", ""ip_address"":""10.0.0.1"", ""ip_port"":8080 }";
        var ctrl = JsonConvert.DeserializeObject<ControllerDeviceDto>(controllerJson)!;
        Assert.Equal(101, ctrl.NumberDevice);
        Assert.Equal("Controller", ctrl.TypeDevice);
        Assert.Null(ctrl.DeviceGroups);
        Assert.False(ctrl.IsEnable);

        // Sensor — 구 JSON
        var sensorJson = @"{ ""id"":10, ""number_device"":201, ""group_device"":1, ""name_device"":""Sensor-A"", ""type_device"":""Fence"", ""version"":""1.0"", ""status"":""ACTIVATED"", ""controller_id"":101 }";
        var sensor = JsonConvert.DeserializeObject<SensorDeviceDto>(sensorJson)!;
        Assert.Equal(201, sensor.NumberDevice);
        Assert.Equal("Fence", sensor.TypeDevice);
        Assert.Null(sensor.DeviceGroups);
        Assert.False(sensor.IsEnable);

        // Camera — 구 JSON (urls, is_record, hardware_spec 없음)
        var cameraJson = @"{ ""id"":20, ""number_device"":301, ""group_device"":2, ""name_device"":""Cam-01"", ""type_device"":""IpCamera"", ""version"":""1.0"", ""status"":""ACTIVATED"", ""ip_address"":""192.168.1.100"", ""ip_port"":80, ""user_name"":""admin"", ""user_password"":""pass"", ""rtsp_uri"":""rtsp://192.168.1.100:554/s"", ""rtsp_port"":554, ""mode"":""ONVIF"", ""category"":""PTZ"" }";
        var cam = JsonConvert.DeserializeObject<CameraDeviceDto>(cameraJson)!;
        Assert.Equal(301, cam.NumberDevice);
        Assert.Equal("IpCamera", cam.TypeDevice);
        Assert.Null(cam.Urls);
        Assert.Null(cam.IsRecord);
        Assert.Null(cam.HardwareSpec);
        Assert.Null(cam.DeviceGroups);
        Assert.False(cam.IsEnable);

        // Speaker — 구 JSON (BaseDeviceDto 필드만)
        var speakerJson = @"{ ""id"":30, ""number_device"":401, ""name_device"":""Speaker-A"", ""ip_address"":""10.0.0.50"", ""ip_port"":9000 }";
        var spk = JsonConvert.DeserializeObject<SpeakerDeviceDto>(speakerJson)!;
        Assert.Equal(401, spk.NumberDevice);
        Assert.Equal("IpSpeaker", spk.TypeDevice);
        Assert.Null(spk.DeviceGroups);

        // Enclosure — 구 JSON
        var enclosureJson = @"{ ""id"":40, ""number_device"":501, ""name_device"":""Enclosure-A"" }";
        var enc = JsonConvert.DeserializeObject<EnclosureDeviceDto>(enclosureJson)!;
        Assert.Equal(501, enc.NumberDevice);
        Assert.Equal("Enclosure", enc.TypeDevice);

        // Lamp — 구 JSON
        var lampJson = @"{ ""id"":50, ""number_device"":601, ""name_device"":""Lamp-A"" }";
        var lamp = JsonConvert.DeserializeObject<LampDeviceDto>(lampJson)!;
        Assert.Equal(601, lamp.NumberDevice);
        Assert.Equal("Lamp", lamp.TypeDevice);

        // 모든 DTO가 BaseDeviceDto 상속 확인
        Assert.IsAssignableFrom<BaseDeviceDto>(ctrl);
        Assert.IsAssignableFrom<BaseDeviceDto>(sensor);
        Assert.IsAssignableFrom<BaseDeviceDto>(cam);
        Assert.IsAssignableFrom<BaseDeviceDto>(spk);
        Assert.IsAssignableFrom<BaseDeviceDto>(enc);
        Assert.IsAssignableFrom<BaseDeviceDto>(lamp);
    }
}
#endregion

#region - Phase 5: Broker Envelope 필드명 변경 테스트 -
/// <summary>
/// Phase 5: BaseBrokerMessage/BaseMessage 필드명 변경 검증
/// type_message→m_type, type_command→cmd, timestamp→created, data→body
/// </summary>
public class BrokerEnvelopeFieldRenameTests
{
    [Fact(DisplayName = "A5.1-1: BaseBrokerMessage 직렬화 시 m_type, cmd, created 사용")]
    [Trait("Category", "Broker")]
    public void BaseBrokerMessage_Serialization_ShouldUseMType_Cmd_Created()
    {
        var dto = new EventCallDto { EventName = "Test", State = "ACTIVE" };
        var request = dto.ToBrokerRequest("EVENT_CALL", "client-001");

        var json = request.ToJson();

        // 새 필드명 존재
        Assert.Contains("\"m_type\":\"REQ\"", json);
        Assert.Contains("\"cmd\":\"EVENT_CALL\"", json);
        Assert.Contains("\"created\":", json);

        // 구 필드명 없음
        Assert.DoesNotContain("\"type_message\"", json);
        Assert.DoesNotContain("\"type_command\"", json);
        Assert.DoesNotContain("\"timestamp\"", json);
    }

    [Fact(DisplayName = "A5.1-2: BaseBrokerMessage 새 필드명 역직렬화")]
    [Trait("Category", "Broker")]
    public void BaseBrokerMessage_Deserialization_FromNewFieldNames()
    {
        var json = @"{
            ""id"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""m_type"": ""REQ"",
            ""cmd"": ""EVENT_CALL"",
            ""from"": ""client-001"",
            ""body"": {
                ""event_name"": ""Detection-001"",
                ""state"": ""ACTIVE""
            },
            ""created"": ""2025-11-18T10:30:00.000Z""
        }";

        var request = BrokerMessageHelper.FromJsonRequest<EventCallDto>(json);

        Assert.NotNull(request);
        Assert.Equal("REQ", request.TypeMessage);
        Assert.Equal("EVENT_CALL", request.Command);
        Assert.Equal("client-001", request.From);
        Assert.NotNull(request.Data);
        Assert.Equal("Detection-001", request.Data.EventName);
    }

    [Fact(DisplayName = "A5.2: BaseMessage 직렬화 시 body 사용 (data 아님)")]
    [Trait("Category", "Broker")]
    public void BaseMessage_Serialization_ShouldUseBodyNotData()
    {
        var dto = new EventCallDto { EventName = "Test" };
        var request = dto.ToBrokerRequest("CMD", "from");

        var json = request.ToJson();

        Assert.Contains("\"body\":{", json);
        Assert.DoesNotContain("\"data\":", json);
    }

    [Fact(DisplayName = "A5.3-1: BrokerRequest 새 필드명 왕복 테스트")]
    [Trait("Category", "Broker")]
    public void BrokerRequest_NewFieldNames_RoundTrip_ShouldPreserveData()
    {
        var originalDto = new DetectionEventDto
        {
            Id = 1001,
            TypeEvent = "Intrusion",
            Result = "PIR_SENSOR"
        };
        var request = originalDto.ToBrokerRequest("DETECTION", "test-service");
        var json = request.ToJson();
        var deserialized = BrokerMessageHelper.FromJsonRequest<DetectionEventDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("REQ", deserialized.TypeMessage);
        Assert.Equal("DETECTION", deserialized.Command);
        Assert.NotNull(deserialized.Data);
        Assert.Equal(1001, deserialized.Data.Id);
        Assert.Equal("PIR_SENSOR", deserialized.Data.Result);
    }

    [Fact(DisplayName = "A5.3-2: BrokerResponse 새 필드명 왕복 테스트")]
    [Trait("Category", "Broker")]
    public void BrokerResponse_NewFieldNames_RoundTrip_ShouldPreserveData()
    {
        var resultDto = new EventCallDto { EventName = "Result", State = "DONE" };
        var response = BrokerMessageHelper.CreateResponse(resultDto, "req-123", "server", "EVENT_CALL");
        var json = response.ToJson();
        var deserialized = BrokerMessageHelper.FromJsonResponse<EventCallDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("RSP", deserialized.TypeMessage);
        Assert.True(deserialized.Success);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("Result", deserialized.Data.EventName);
    }

    [Fact(DisplayName = "A5.3-3: ParseEventsFromBrokerMessage 새 필드명(body) 파싱")]
    [Trait("Category", "Broker")]
    public void ParseEventsFromBrokerMessage_WithNewFieldNames_ShouldParse()
    {
        var json = @"{
            ""id"": ""xxx"",
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": ""{\""id\"":0,\""reason\"":\""FAULT_FENCE\""}"",
            ""created"": ""2025-11-27T01:45:53.019Z""
        }";

        var result = BrokerMessageHelper.ParseEventsFromBrokerMessage<MalfunctionEventDto>(json);

        Assert.Single(result);
        Assert.Equal(0, result[0].Id);
        Assert.Equal("FAULT_FENCE", result[0].Reason);
    }

    [Fact(DisplayName = "A5.3-4: ParseSingleEventFromBrokerMessage 새 필드명(body) 파싱")]
    [Trait("Category", "Broker")]
    public void ParseSingleEventFromBrokerMessage_WithNewFieldNames_ShouldParse()
    {
        var json = @"{
            ""id"": ""xxx"",
            ""m_type"": ""REQ"",
            ""cmd"": ""Fault"",
            ""from"": ""proxyManager"",
            ""body"": ""{\""id\"":123,\""reason\"":\""FAULT_FENCE\""}"",
            ""created"": ""2025-11-27T01:45:53.019Z""
        }";

        var result = BrokerMessageHelper.ParseSingleEventFromBrokerMessage<MalfunctionEventDto>(json);

        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("FAULT_FENCE", result.Reason);
    }
}
#endregion

#region - Phase 6: Event DTO Flat→Nested 구조 변경 테스트 -
/// <summary>
/// Phase 6: Event DTO 구조 변경 (Flat→Nested device)
/// </summary>
public class EventDtoRestructureTests
{
    #region A6.1: IEventDto + IDeviceEventDto 인터페이스
    [Fact(DisplayName = "A6.1-1: IEventDto 최소 속성만 포함")]
    [Trait("Category", "Event")]
    public void IEventDto_ShouldHaveMinimalProperties()
    {
        // IEventDto는 Id, TypeEvent, CreatedAt, UpdatedAt만 포함 (ActionReported는 IActionReportableEventDto로 분리)
        var props = typeof(IEventDto).GetProperties();
        var propNames = props.Select(p => p.Name).ToHashSet();

        Assert.Contains("Id", propNames);
        Assert.Contains("TypeEvent", propNames);
        Assert.Contains("CreatedAt", propNames);
        Assert.Contains("UpdatedAt", propNames);
        Assert.DoesNotContain("ActionReported", propNames);

        // 레거시 필드 제거 확인
        Assert.DoesNotContain("GroupEvent", propNames);
        Assert.DoesNotContain("Controller", propNames);
        Assert.DoesNotContain("Sensor", propNames);
        Assert.DoesNotContain("TypeDevice", propNames);
        Assert.DoesNotContain("Sequence", propNames);
    }

    [Fact(DisplayName = "A6.1-2: IDeviceEventDto는 IEventDto 확장 + Device, DeviceDescription")]
    [Trait("Category", "Event")]
    public void IDeviceEventDto_ShouldExtendIEventDto_WithDeviceAndDescription()
    {
        // IDeviceEventDto가 IEventDto를 상속
        Assert.True(typeof(IEventDto).IsAssignableFrom(typeof(IDeviceEventDto)));

        var props = typeof(IDeviceEventDto).GetProperties();
        var propNames = props.Select(p => p.Name).ToHashSet();

        Assert.Contains("Device", propNames);
        Assert.Contains("DeviceDescription", propNames);
    }
    #endregion

    #region A6.2: DetectionEventDto Flat→Nested
    [Fact(DisplayName = "A6.2-1: DetectionEventDto 새 구조 nested device")]
    [Trait("Category", "Event")]
    public void DetectionEventDto_NewStructure_ShouldHaveNestedDevice()
    {
        var dto = new DetectionEventDto
        {
            Id = 1001,
            TypeEvent = "Intrusion",
            Device = new BaseDeviceDto
            {
                Id = 2,
                NumberDevice = 2,
                NameDevice = "센서_1",
                TypeDevice = "Sensor",
                Status = "ACTIVATED"
            },
            DeviceDescription = "1구역 센서 2번",
            ActionReported = "False",
            Result = "THERMAL_SENSOR"
        };

        var json = JsonConvert.SerializeObject(dto);

        Assert.Contains("\"device\":{", json);
        Assert.Contains("\"name_device\":\"센서_1\"", json);
        Assert.Contains("\"device_description\":\"1구역 센서 2번\"", json);
        Assert.Contains("\"result\":\"THERMAL_SENSOR\"", json);

        // 레거시 필드 없음
        Assert.DoesNotContain("\"group_event\"", json);
        Assert.DoesNotContain("\"controller\":", json);
        Assert.DoesNotContain("\"sensor\":", json);
        Assert.DoesNotContain("\"sequence\":", json);

        // IDeviceEventDto 구현 확인
        Assert.IsAssignableFrom<IDeviceEventDto>(dto);
    }

    [Fact(DisplayName = "A6.2-2: DetectionEventDto Backend JSON 역직렬화")]
    [Trait("Category", "Event")]
    public void DetectionEventDto_Deserialization_FromNewBackendJson()
    {
        var json = @"{
            ""id"": 1001,
            ""type_event"": ""Intrusion"",
            ""device"": {
                ""id"": 2,
                ""number_device"": 2,
                ""name_device"": ""센서_1"",
                ""type_device"": ""Sensor"",
                ""status"": ""ACTIVATED"",
                ""device_groups"": [{""id"":1},{""id"":3}]
            },
            ""device_description"": ""1구역 센서 2번"",
            ""action_reported"": ""False"",
            ""result"": ""THERMAL_SENSOR"",
            ""detail"": null,
            ""created_at"": ""2025-12-01T10:00:00.000+09:00""
        }";

        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(json);

        Assert.NotNull(dto);
        Assert.Equal(1001, dto.Id);
        Assert.Equal("Intrusion", dto.TypeEvent);
        Assert.NotNull(dto.Device);
        Assert.Equal(2, dto.Device.Id);
        Assert.Equal("센서_1", dto.Device.NameDevice);
        Assert.Equal("1구역 센서 2번", dto.DeviceDescription);
        Assert.Equal("THERMAL_SENSOR", dto.Result);
        Assert.Null(dto.Detail);
    }

    [Fact(DisplayName = "A6.2-3: DetectionEventDto device null 역직렬화")]
    [Trait("Category", "Event")]
    public void DetectionEventDto_WithNullDevice_ShouldDeserialize()
    {
        var json = @"{ ""id"": 1, ""type_event"": ""Intrusion"", ""device"": null, ""result"": ""PIR"" }";
        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(json);

        Assert.NotNull(dto);
        Assert.Null(dto.Device);
        Assert.Equal("PIR", dto.Result);
    }
    #endregion

    #region A6.3: MalfunctionEventDto Flat→Nested + detail
    [Fact(DisplayName = "A6.3-1: MalfunctionEventDto nested device + detail")]
    [Trait("Category", "Event")]
    public void MalfunctionEventDto_NewStructure_ShouldHaveNestedDeviceAndDetail()
    {
        var dto = new MalfunctionEventDto
        {
            Id = 2001,
            TypeEvent = "Fault",
            Device = new BaseDeviceDto { Id = 5, TypeDevice = "Sensor" },
            DeviceDescription = "3구역 센서 1번",
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto
            {
                FirstStart = 1,
                FirstEnd = 5,
                SecondStart = 0,
                SecondEnd = 0
            }
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"device\":{", json);
        Assert.Contains("\"device_description\":\"3구역 센서 1번\"", json);
        Assert.Contains("\"reason\":\"FAULT_FENCE\"", json);
        Assert.Contains("\"detail\":{", json);
        Assert.Contains("\"first_start\":1", json);

        // 레거시 top-level 필드 없음
        Assert.DoesNotContain("\"group_event\"", json);
        Assert.DoesNotContain("\"controller\":", json);

        Assert.IsAssignableFrom<IDeviceEventDto>(dto);
    }

    [Fact(DisplayName = "A6.3-2: MalfunctionEventDto detail 역직렬화")]
    [Trait("Category", "Event")]
    public void MalfunctionEventDto_Deserialization_DetailShouldHaveFirstSecondStartEnd()
    {
        var json = @"{
            ""id"": 2001,
            ""type_event"": ""Fault"",
            ""device"": { ""id"": 5, ""type_device"": ""Sensor"" },
            ""reason"": ""FAULT_FENCE"",
            ""detail"": { ""first_start"": 1, ""first_end"": 5, ""second_start"": 0, ""second_end"": 0 },
            ""action_reported"": ""False""
        }";

        var dto = JsonConvert.DeserializeObject<MalfunctionEventDto>(json);
        Assert.NotNull(dto);
        Assert.NotNull(dto.Detail);
        Assert.Equal(1, dto.Detail.FirstStart);
        Assert.Equal(5, dto.Detail.FirstEnd);
    }
    #endregion

    #region A6.4: ConnectionEventDto Flat→Nested
    [Fact(DisplayName = "A6.4: ConnectionEventDto nested device")]
    [Trait("Category", "Event")]
    public void ConnectionEventDto_NewStructure_ShouldHaveNestedDevice()
    {
        var dto = new ConnectionEventDto
        {
            Id = 3001,
            TypeEvent = "Connection",
            Device = new BaseDeviceDto { Id = 10, TypeDevice = "Controller" },
            DeviceDescription = "정문 컨트롤러"
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"device\":{", json);
        Assert.Contains("\"device_description\":\"정문 컨트롤러\"", json);
        Assert.DoesNotContain("\"group_event\"", json);
        Assert.DoesNotContain("\"controller\":", json);
        Assert.DoesNotContain("\"sensor\":", json);

        Assert.IsAssignableFrom<IDeviceEventDto>(dto);
    }
    #endregion

    #region A6.5: ActionEventDto 설계 문서 기준 필드 검증
    [Fact(DisplayName = "A6.5: ActionEventDto는 설계 문서 기준 필드만 포함 (device/device_description 제외)")]
    [Trait("Category", "Event")]
    public void ActionEventDto_ShouldNotHaveDeviceOrDeviceDescription()
    {
        var dto = new ActionEventDto
        {
            Id = 4001,
            TypeEvent = "Action",
            Content = "침입 탐지 확인",
            User = "operator_01"
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.DoesNotContain("\"device\":", json);
        Assert.DoesNotContain("\"device_description\"", json);
        Assert.Contains("\"content\":\"침입 탐지 확인\"", json);
    }
    #endregion

    #region A6.7: FromEventConverter
    [Fact(DisplayName = "A6.7-1: FromEventConverter Detection 새 구조 역직렬화")]
    [Trait("Category", "Event")]
    public void FromEventConverter_ShouldDeserializeDetectionEvent_WithNewStructure()
    {
        var json = @"{
            ""success"": true,
            ""message"": ""OK"",
            ""data"": {
                ""id"": 100,
                ""type_event"": ""Action"",
                ""content"": ""확인"",
                ""user"": ""op1"",
                ""from_event"": {
                    ""id"": 200,
                    ""type_event"": ""Intrusion"",
                    ""device"": { ""id"": 5, ""type_device"": ""Sensor"" },
                    ""result"": ""PIR_SENSOR"",
                    ""action_reported"": ""False""
                }
            }
        }";

        var result = ApiMessageHelper.FromJsonResponse<ActionEventDto>(json);
        Assert.NotNull(result?.Data?.FromEvent);
        Assert.IsType<DetectionEventDto>(result.Data.FromEvent);

        var detection = (DetectionEventDto)result.Data.FromEvent;
        Assert.Equal("PIR_SENSOR", detection.Result);
        Assert.NotNull(detection.Device);
        Assert.Equal("Sensor", detection.Device.TypeDevice);
    }

    [Fact(DisplayName = "A6.7-2: FromEventConverter Malfunction 새 구조 역직렬화")]
    [Trait("Category", "Event")]
    public void FromEventConverter_ShouldDeserializeMalfunctionEvent_WithNewStructure()
    {
        var json = @"{
            ""success"": true,
            ""message"": ""OK"",
            ""data"": {
                ""id"": 101,
                ""type_event"": ""Action"",
                ""content"": ""장애 확인"",
                ""user"": ""op1"",
                ""from_event"": {
                    ""id"": 201,
                    ""type_event"": ""Fault"",
                    ""device"": { ""id"": 10, ""type_device"": ""Controller"" },
                    ""reason"": ""FAULT_CONTROLLER"",
                    ""action_reported"": ""False""
                }
            }
        }";

        var result = ApiMessageHelper.FromJsonResponse<ActionEventDto>(json);
        Assert.NotNull(result?.Data?.FromEvent);
        Assert.IsType<MalfunctionEventDto>(result.Data.FromEvent);

        var malfunction = (MalfunctionEventDto)result.Data.FromEvent;
        Assert.Equal("FAULT_CONTROLLER", malfunction.Reason);
    }
    #endregion
}
#endregion

#region Phase 7: Integration DTO 수정
/// <summary>
/// Phase 7: EventMappingDto 재설계 + Legacy 삭제
/// </summary>
public class IntegrationDtoRestructureTests
{
    #region A7.1: EventMappingDto 재설계
    [Fact(DisplayName = "A7.1-1: EventMappingDto 새 구조 - device_group_id, category_event_mapping, cameras/speakers/lamps")]
    [Trait("Category", "Integration")]
    public void EventMappingDto_NewStructure_ShouldHaveDeviceGroupIdAndCamerasSpeakersLamps()
    {
        // Arrange
        var dto = new EventMappingDto
        {
            Id = 1,
            NameEvent = "침입감지 매핑",
            DeviceGroupId = 10,
            CategoryEventMapping = "DETECT_SENSOR_WITH_CAMERA",
            Description = "센서-카메라 연동",
            Status = true,
            Cameras = new List<EventMappingCameraDto>
            {
                new EventMappingCameraDto { CameraId = 1, TargetPresetId = 5, HomePresetId = 0, DelayTime = 3, Priority = 1 }
            },
            Speakers = new List<EventMappingSpeakerDto>
            {
                new EventMappingSpeakerDto { SpeakerId = 2, FileGroupId = 1, RepeatCount = 3, Priority = 1 }
            },
            Lamps = new List<EventMappingLampDto>
            {
                new EventMappingLampDto { LampId = 3, Color = "Red", LightMode = "steady", BuzzerSound = "PI-PI-PI", Priority = 1 }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

        // Assert - 새 필드 존재
        Assert.Contains("\"device_group_id\": 10", json);
        Assert.Contains("\"category_event_mapping\": \"DETECT_SENSOR_WITH_CAMERA\"", json);
        Assert.Contains("\"cameras\":", json);
        Assert.Contains("\"speakers\":", json);
        Assert.Contains("\"lamps\":", json);

        // Assert - 레거시 필드 없음
        Assert.DoesNotContain("\"group_event\"", json);
        Assert.DoesNotContain("\"category_event\":", json);

        // Assert - 왕복 검증
        var deserialized = JsonConvert.DeserializeObject<EventMappingDto>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(10, deserialized.DeviceGroupId);
        Assert.Equal("DETECT_SENSOR_WITH_CAMERA", deserialized.CategoryEventMapping);
        Assert.Single(deserialized.Cameras!);
        Assert.Single(deserialized.Speakers!);
        Assert.Single(deserialized.Lamps!);
        Assert.Equal(1, deserialized.Cameras![0].CameraId);
        Assert.Equal(2, deserialized.Speakers![0].SpeakerId);
        Assert.Equal(3, deserialized.Lamps![0].LampId);
    }
    #endregion
}
#endregion

#region Phase 9: 설계 문서 정합성 보정
/// <summary>
/// Phase 9: IEventDto 분리 + ConnectionEventDto action_reported 제거
/// </summary>
public class EventDtoInterfaceSplitTests
{
    #region A9.6: ConnectionEventDto action_reported 제거 + IActionReportableEventDto 분리

    [Fact(DisplayName = "A9.6-1: ConnectionEventDto 직렬화에 action_reported 없음")]
    [Trait("Category", "Event")]
    public void ConnectionEventDto_ShouldNotHaveActionReported()
    {
        // Arrange
        var dto = new ConnectionEventDto
        {
            Id = 3001,
            TypeEvent = "Connection",
            Device = new BaseDeviceDto { Id = 10, TypeDevice = "Controller" },
            DeviceDescription = "정문 컨트롤러"
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);

        // Assert — action_reported 키가 없어야 함
        Assert.DoesNotContain("action_reported", json);
    }

    [Fact(DisplayName = "A9.6-2: Detection/Malfunction → IActionReportableEventDto 캐스팅")]
    [Trait("Category", "Event")]
    public void IActionReportableEventDto_DetectionAndMalfunction_ShouldHaveActionReported()
    {
        // Arrange
        var detection = new DetectionEventDto { ActionReported = "True" };
        var malfunction = new MalfunctionEventDto { ActionReported = "False" };

        // Assert — IActionReportableEventDto 캐스팅 가능
        Assert.IsAssignableFrom<IActionReportableEventDto>(detection);
        Assert.IsAssignableFrom<IActionReportableEventDto>(malfunction);

        // Assert — ConnectionEventDto는 IActionReportableEventDto 아님
        var connection = new ConnectionEventDto();
        Assert.False(connection is IActionReportableEventDto);
    }

    #endregion
}
#endregion

#region Phase 13: Server/Category 보정 + Speaker/Enclosure/Lamp 필드 추가

/// <summary>
/// Phase 13 테스트 — DTO 필드 보정 및 신규 서브클래스 필드
/// </summary>
public class Phase13_DtoFieldTests
{
    #region A13.1: ServerDto 보정

    [Fact(DisplayName = "A13.1-1: ServerDto API 설계 문서 JSON 역직렬화")]
    [Trait("Category", "DTO")]
    public void ServerDto_ApiDesignDoc_ShouldDeserializeAllFields()
    {
        var json = """
        {
            "id": 1,
            "category_id": 10,
            "name": "방송서버-01",
            "status": "NORMAL",
            "ip_address": "192.168.1.100",
            "port": 8080,
            "hostname": "bcast-srv-01",
            "user_name": "admin",
            "user_password": "password123",
            "threshold_config": { "cpu": { "warning": 80, "critical": 95 }, "ram": { "warning": 75, "critical": 90 }, "disk": { "warning": 80, "critical": 95 }, "network": { "warning_mbps": 800, "critical_mbps": 950 } }
        }
        """;

        var dto = JsonConvert.DeserializeObject<ServerDto>(json);

        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Equal(10, dto.CategoryId);
        Assert.Equal("방송서버-01", dto.Name);
        Assert.Equal("NORMAL", dto.Status);
        Assert.Equal("192.168.1.100", dto.IpAddress);
        Assert.Equal(8080, dto.Port);
        Assert.Equal("bcast-srv-01", dto.Hostname);
        Assert.Equal("admin", dto.UserName);
        Assert.Equal("password123", dto.UserPassword);
        Assert.NotNull(dto.ThresholdConfig);
    }

    [Fact(DisplayName = "A13.1-2: ServerDto 직렬화에 type_server 없음")]
    [Trait("Category", "DTO")]
    public void ServerDto_ShouldNotHaveTypeServer()
    {
        var dto = new ServerDto { Id = 1, Name = "Test", IpAddress = "10.0.0.1", Port = 5432 };
        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("\"type_server\"", json);
        Assert.Contains("\"category_id\":", json);
        Assert.Contains("\"status\":", json);
    }

    #endregion

    #region A13.2: CategoryDto 보정

    [Fact(DisplayName = "A13.2-1: CategoryDto type_server, sort_order 필드")]
    [Trait("Category", "DTO")]
    public void CategoryDto_ApiDesignDoc_ShouldHaveTypeServerAndSortOrder()
    {
        var json = """
        {
            "id": 10,
            "name": "방송서버",
            "type_server": "BROADCASTING",
            "description": "방송 서비스 카테고리",
            "sort_order": 1
        }
        """;

        var dto = JsonConvert.DeserializeObject<CategoryDto>(json);

        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal("방송서버", dto.Name);
        Assert.Equal("BROADCASTING", dto.TypeServer);
        Assert.Equal("방송 서비스 카테고리", dto.Description);
        Assert.Equal(1, dto.SortOrder);

        // 왕복 직렬화
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"type_server\":\"BROADCASTING\"", serialized);
        Assert.Contains("\"sort_order\":1", serialized);
    }

    #endregion

    #region A13.3: SpeakerDeviceDto 고유 필드

    [Fact(DisplayName = "A13.3-1: SpeakerDeviceDto speaker_type, description, server 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void SpeakerDeviceDto_ApiDesignDoc_ShouldHaveSpeakerTypeDescriptionServer()
    {
        var dto = new SpeakerDeviceDto
        {
            Id = 301,
            NumberDevice = 50,
            NameDevice = "Speaker-050",
            SpeakerType = "NORMAL",
            Description = "정문 스피커"
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"speaker_type\":\"NORMAL\"", json);
        Assert.Contains("\"description\":\"정문 스피커\"", json);

        var restored = JsonConvert.DeserializeObject<SpeakerDeviceDto>(json);
        Assert.NotNull(restored);
        Assert.Equal("NORMAL", restored.SpeakerType);
        Assert.Equal("정문 스피커", restored.Description);
    }

    [Fact(DisplayName = "A13.3-2: SpeakerDeviceDto Server nested 역직렬화")]
    [Trait("Category", "DTO")]
    public void SpeakerDeviceDto_ServerNested_ShouldDeserializeFromApiJson()
    {
        var json = """
        {
            "id": 301,
            "number_device": 50,
            "name_device": "Speaker-050",
            "type_device": "IpSpeaker",
            "status": "ACTIVATED",
            "speaker_type": "ADMIN",
            "description": "정문 스피커",
            "server": {
                "id": 1,
                "category_id": 10,
                "name": "방송서버-01",
                "status": "NORMAL",
                "ip_address": "192.168.1.100",
                "port": 8080,
                "hostname": "bcast-srv-01"
            }
        }
        """;

        var dto = JsonConvert.DeserializeObject<SpeakerDeviceDto>(json);

        Assert.NotNull(dto);
        Assert.Equal("IpSpeaker", dto.TypeDevice);
        Assert.Equal("ADMIN", dto.SpeakerType);
        Assert.Equal("정문 스피커", dto.Description);
        Assert.NotNull(dto.Server);
        Assert.Equal(1, dto.Server.Id);
        Assert.Equal(10, dto.Server.CategoryId);
        Assert.Equal("방송서버-01", dto.Server.Name);
        Assert.Equal("NORMAL", dto.Server.Status);
        Assert.Equal("192.168.1.100", dto.Server.IpAddress);
        Assert.Equal(8080, dto.Server.Port);
        Assert.Equal("bcast-srv-01", dto.Server.Hostname);
    }

    #endregion

    #region A13.4: EnclosureDeviceDto 고유 필드

    [Fact(DisplayName = "A13.4-1: EnclosureDeviceDto door_status, threshold_config, heater/fan 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void EnclosureDeviceDto_ApiDesignDoc_ShouldHaveDoorStatusThresholdHeaterFan()
    {
        var json = """
        {
            "id": 401,
            "number_device": 60,
            "name_device": "Enclosure-060",
            "type_device": "Enclosure",
            "status": "ACTIVATED",
            "door_status": "CLOSED",
            "threshold_config": { "temp_high": 40.0, "temp_low": -10.0, "humidity_high": 85.0, "current_high": 10.0, "voltage_low": 180.0, "vibration_high": 5 },
            "heater_enabled": true,
            "fan_enabled": false
        }
        """;

        var dto = JsonConvert.DeserializeObject<EnclosureDeviceDto>(json);

        Assert.NotNull(dto);
        Assert.Equal("Enclosure", dto.TypeDevice);
        Assert.Equal("CLOSED", dto.DoorStatus);
        Assert.NotNull(dto.ThresholdConfig);
        Assert.True(dto.HeaterEnabled);
        Assert.False(dto.FanEnabled);

        // 왕복 직렬화
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"door_status\":\"CLOSED\"", serialized);
        Assert.Contains("\"heater_enabled\":true", serialized);
        Assert.Contains("\"fan_enabled\":false", serialized);
    }

    #endregion

    #region A13.5: LampDeviceDto 고유 필드

    [Fact(DisplayName = "A13.5-1: LampDeviceDto ip_address, ip_port, user_name, user_password, description 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void LampDeviceDto_ApiDesignDoc_ShouldHaveIpAddressIpPortUserNameUserPasswordDescription()
    {
        var dto = new LampDeviceDto
        {
            Id = 501,
            NumberDevice = 70,
            NameDevice = "Lamp-070",
            IpAddress = "192.168.1.70",
            IpPort = 502,
            UserName = "admin",
            UserPassword = "lamp123",
            Description = "정문 경고등"
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"ip_address\":\"192.168.1.70\"", json);
        Assert.Contains("\"ip_port\":502", json);
        Assert.Contains("\"user_name\":\"admin\"", json);
        Assert.Contains("\"user_password\":\"lamp123\"", json);
        Assert.Contains("\"description\":\"정문 경고등\"", json);

        var restored = JsonConvert.DeserializeObject<LampDeviceDto>(json);
        Assert.NotNull(restored);
        Assert.Equal("192.168.1.70", restored.IpAddress);
        Assert.Equal(502, restored.IpPort);
        Assert.Equal("admin", restored.UserName);
        Assert.Equal("lamp123", restored.UserPassword);
        Assert.Equal("정문 경고등", restored.Description);
    }

    #endregion
}

#endregion

#region Phase 19: Camera DTO → Model → ViewModel API 매칭

public class Phase19_CameraDtoMatchingTests
{
    #region A19.1: HardwareSpecDto

    [Fact(DisplayName = "A19.1-1: HardwareSpecDto 9개 필드 직렬화")]
    [Trait("Category", "DTO")]
    public void HardwareSpecDto_Serialization_ShouldHaveAllNineFields()
    {
        var dto = new HardwareSpecDto
        {
            Name = "GOP 1구역 PTZ 카메라",
            Location = "GOP 1구역 전방 초소",
            Manufacturer = "Hanwha Vision",
            Model = "XNP-6320RH",
            Hardware = "PTZ 32x Optical Zoom",
            Firmware = "2.41.01",
            DeviceId = "HWV-XNP-001",
            MacAddress = "00:09:18:AB:CD:EF",
            OnvifVersion = "2.4.2"
        };

        var json = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"location\":", json);
        Assert.Contains("\"manufacturer\":", json);
        Assert.Contains("\"model\":", json);
        Assert.Contains("\"hardware\":", json);
        Assert.Contains("\"firmware\":", json);
        Assert.Contains("\"device_id\":", json);
        Assert.Contains("\"mac_address\":", json);
        Assert.Contains("\"onvif_version\":", json);

        // 왕복 검증
        var restored = JsonConvert.DeserializeObject<HardwareSpecDto>(json);
        Assert.NotNull(restored);
        Assert.Equal("Hanwha Vision", restored.Manufacturer);
        Assert.Equal("XNP-6320RH", restored.Model);
        Assert.Equal("00:09:18:AB:CD:EF", restored.MacAddress);
    }

    [Fact(DisplayName = "A19.1-2: HardwareSpecDto API JSON 역직렬화")]
    [Trait("Category", "DTO")]
    public void HardwareSpecDto_Deserialization_FromApiJson()
    {
        var json = """
        {
            "name": "GOP 1구역 PTZ 카메라",
            "location": "GOP 1구역 전방 초소",
            "manufacturer": "Hanwha Vision",
            "model": "XNP-6320RH",
            "hardware": "PTZ 32x Optical Zoom",
            "firmware": "2.41.01",
            "device_id": "HWV-XNP-001",
            "mac_address": "00:09:18:AB:CD:EF",
            "onvif_version": "2.4.2"
        }
        """;

        var dto = JsonConvert.DeserializeObject<HardwareSpecDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("GOP 1구역 PTZ 카메라", dto.Name);
        Assert.Equal("GOP 1구역 전방 초소", dto.Location);
        Assert.Equal("Hanwha Vision", dto.Manufacturer);
        Assert.Equal("XNP-6320RH", dto.Model);
        Assert.Equal("PTZ 32x Optical Zoom", dto.Hardware);
        Assert.Equal("2.41.01", dto.Firmware);
        Assert.Equal("HWV-XNP-001", dto.DeviceId);
        Assert.Equal("00:09:18:AB:CD:EF", dto.MacAddress);
        Assert.Equal("2.4.2", dto.OnvifVersion);
    }

    #endregion

    #region A19.2: CameraDeviceDto.HardwareSpec 타입 변경

    [Fact(DisplayName = "A19.2-1: CameraDeviceDto hardware_spec 중첩 객체 역직렬화")]
    [Trait("Category", "DTO")]
    public void CameraDeviceDto_HardwareSpec_ShouldDeserializeAsObject()
    {
        var json = """
        {
            "id": 201,
            "number_device": 10,
            "name_device": "PTZ Camera 1",
            "type_device": "IpCamera",
            "ip_address": "192.168.1.109",
            "ip_port": 80,
            "hardware_spec": {
                "name": "PTZ Camera",
                "location": "Gate 1",
                "manufacturer": "Hanwha Vision",
                "model": "XNP-6320RH",
                "hardware": "PTZ 32x",
                "firmware": "2.41.01",
                "device_id": "HWV-001",
                "mac_address": "00:09:18:AB:CD:EF",
                "onvif_version": "2.4.2"
            }
        }
        """;

        var dto = JsonConvert.DeserializeObject<CameraDeviceDto>(json);
        Assert.NotNull(dto);
        Assert.NotNull(dto.HardwareSpec);
        Assert.Equal("Hanwha Vision", dto.HardwareSpec.Manufacturer);
        Assert.Equal("XNP-6320RH", dto.HardwareSpec.Model);
        Assert.Equal("00:09:18:AB:CD:EF", dto.HardwareSpec.MacAddress);
    }

    #endregion

    #region A19.3: CameraUrlsDto 재설계

    [Fact(DisplayName = "A19.3-1: CameraUrlsDto 중첩 구조 역직렬화")]
    [Trait("Category", "DTO")]
    public void CameraUrlsDto_ApiDesignDoc_ShouldDeserializeNestedUrls()
    {
        var json = """
        {
            "homepage": {
                "url": "http://192.168.1.109/"
            },
            "onvif": {
                "device_service": "http://192.168.1.109:8000/onvif/device_service"
            },
            "streams": {
                "rtsp": {
                    "main": "rtsp://192.168.1.109:554/Streaming/Channels/101",
                    "sub": "rtsp://192.168.1.109:554/Streaming/Channels/102"
                },
                "webrtc": {
                    "main": "https://192.168.1.109/webrtc/main"
                }
            },
            "snapshot": {
                "ch1": "http://192.168.1.109/cgi-bin/snapshot.cgi"
            }
        }
        """;

        var dto = JsonConvert.DeserializeObject<CameraUrlsDto>(json);
        Assert.NotNull(dto);
        Assert.NotNull(dto.Homepage);
        Assert.Equal("http://192.168.1.109/", dto.Homepage.Url);
        Assert.NotNull(dto.Onvif);
        Assert.Equal("http://192.168.1.109:8000/onvif/device_service", dto.Onvif.DeviceService);
        Assert.NotNull(dto.Streams);
        Assert.NotNull(dto.Streams.Rtsp);
        Assert.Equal("rtsp://192.168.1.109:554/Streaming/Channels/101", dto.Streams.Rtsp.Main);
        Assert.Equal("rtsp://192.168.1.109:554/Streaming/Channels/102", dto.Streams.Rtsp.Sub);
        Assert.NotNull(dto.Streams.Webrtc);
        Assert.Equal("https://192.168.1.109/webrtc/main", dto.Streams.Webrtc.Main);
        Assert.NotNull(dto.Snapshot);
        Assert.Equal("http://192.168.1.109/cgi-bin/snapshot.cgi", dto.Snapshot.Ch1);
    }

    #endregion

    #region A19.4: CameraSettingDto 재설계

    [Fact(DisplayName = "A19.4-1: CameraSettingDto API 설계 문서 12개 필드")]
    [Trait("Category", "DTO")]
    public void CameraSettingDto_ApiDesignDoc_ShouldHaveAll12Fields()
    {
        var json = """
        {
            "id": 1,
            "camera_id": 201,
            "weather_mode": "NORMAL",
            "camera_mode": "NORMAL",
            "heater": "off",
            "fan": "off",
            "headlight": "off",
            "day_night_mode": "AUTO",
            "focus_mode": "AUTO",
            "iris_mode": "AUTO",
            "tracking": "IDLE",
            "palette": null
        }
        """;

        var dto = JsonConvert.DeserializeObject<CameraSettingDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Equal(201, dto.CameraId);
        Assert.Equal("NORMAL", dto.WeatherMode);
        Assert.Equal("NORMAL", dto.CameraMode);
        Assert.Equal("off", dto.Heater);
        Assert.Equal("off", dto.Fan);
        Assert.Equal("off", dto.Headlight);
        Assert.Equal("AUTO", dto.DayNightMode);
        Assert.Equal("AUTO", dto.FocusMode);
        Assert.Equal("AUTO", dto.IrisMode);
        Assert.Equal("IDLE", dto.Tracking);
        Assert.Null(dto.Palette);

        // 왕복 직렬화
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"weather_mode\":\"NORMAL\"", serialized);
        Assert.Contains("\"camera_mode\":\"NORMAL\"", serialized);
        Assert.Contains("\"headlight\":\"off\"", serialized);
        Assert.Contains("\"day_night_mode\":\"AUTO\"", serialized);
        Assert.Contains("\"focus_mode\":\"AUTO\"", serialized);
        Assert.Contains("\"iris_mode\":\"AUTO\"", serialized);
        Assert.Contains("\"tracking\":\"IDLE\"", serialized);
    }

    #endregion
}

#endregion

#region ===== Step 1: NATS Enum + Body DTO =====

/// <summary>
/// Phase 22: NATS 제어 Enum 12종 테스트
/// </summary>
public class Phase22_NatsControlEnumTests
{
    [Fact(DisplayName = "A22.1: EnumNatsMessageType — PUB, REQ, RSP")]
    [Trait("Category", "Enum")]
    public void EnumNatsMessageType_ShouldHave_PUB_REQ_RSP()
    {
        var values = Enum.GetValues<EnumNatsMessageType>();
        Assert.Equal(3, values.Length);
        Assert.Contains(EnumNatsMessageType.PUB, values);
        Assert.Contains(EnumNatsMessageType.REQ, values);
        Assert.Contains(EnumNatsMessageType.RSP, values);

        Assert.Equal("PUB", EnumNatsMessageType.PUB.ToString());
        Assert.Equal("REQ", EnumNatsMessageType.REQ.ToString());
        Assert.Equal("RSP", EnumNatsMessageType.RSP.ToString());
    }

    [Fact(DisplayName = "A22.2: EnumSubsystem — 8개 서브시스템")]
    [Trait("Category", "Enum")]
    public void EnumSubsystem_ShouldHave_8_Subsystems()
    {
        var values = Enum.GetValues<EnumSubsystem>();
        Assert.Equal(8, values.Length);
        Assert.Contains(EnumSubsystem.Central, values);
        Assert.Contains(EnumSubsystem.GIS, values);
        Assert.Contains(EnumSubsystem.DBApi, values);
        Assert.Contains(EnumSubsystem.PidsProxy, values);
        Assert.Contains(EnumSubsystem.NVRManager, values);
        Assert.Contains(EnumSubsystem.VMS, values);
        Assert.Contains(EnumSubsystem.BroadcastingManager, values);
        Assert.Contains(EnumSubsystem.AiAnalysis, values);
    }

    [Fact(DisplayName = "A22.3: EnumSyncAction — CREATED, UPDATED, DELETED")]
    [Trait("Category", "Enum")]
    public void EnumSyncAction_ShouldHave_CREATED_UPDATED_DELETED()
    {
        var values = Enum.GetValues<EnumSyncAction>();
        Assert.Equal(3, values.Length);
        Assert.Contains(EnumSyncAction.CREATED, values);
        Assert.Contains(EnumSyncAction.UPDATED, values);
        Assert.Contains(EnumSyncAction.DELETED, values);
    }

    [Fact(DisplayName = "A22.4: EnumOnOff — On, Off")]
    [Trait("Category", "Enum")]
    public void EnumOnOff_ShouldHave_On_Off()
    {
        var values = Enum.GetValues<EnumOnOff>();
        Assert.Equal(2, values.Length);
        Assert.Contains(EnumOnOff.On, values);
        Assert.Contains(EnumOnOff.Off, values);
    }

    [Fact(DisplayName = "A22.5: EnumLightMode — Steady, Blinking")]
    [Trait("Category", "Enum")]
    public void EnumLightMode_ShouldHave_Steady_Blinking()
    {
        var values = Enum.GetValues<EnumLightMode>();
        Assert.Equal(2, values.Length);
        Assert.Contains(EnumLightMode.Steady, values);
        Assert.Contains(EnumLightMode.Blinking, values);
    }

    [Fact(DisplayName = "A22.6: EnumBuzzerSound — 5종 부저")]
    [Trait("Category", "Enum")]
    public void EnumBuzzerSound_ShouldHave_5_Sounds()
    {
        var values = Enum.GetValues<EnumBuzzerSound>();
        Assert.Equal(5, values.Length);
        Assert.Contains(EnumBuzzerSound.FireAWang, values);
        Assert.Contains(EnumBuzzerSound.Emergency, values);
        Assert.Contains(EnumBuzzerSound.Ambulance, values);
        Assert.Contains(EnumBuzzerSound.PiPiPi, values);
        Assert.Contains(EnumBuzzerSound.PiContinue, values);
    }

    [Fact(DisplayName = "A22.7: EnumLampColor — 5색")]
    [Trait("Category", "Enum")]
    public void EnumLampColor_ShouldHave_5_Colors()
    {
        var values = Enum.GetValues<EnumLampColor>();
        Assert.Equal(5, values.Length);
        Assert.Contains(EnumLampColor.Red, values);
        Assert.Contains(EnumLampColor.Orange, values);
        Assert.Contains(EnumLampColor.Green, values);
        Assert.Contains(EnumLampColor.Blue, values);
        Assert.Contains(EnumLampColor.White, values);
    }

    [Fact(DisplayName = "A22.8: EnumOperationMode — REGISTER, NORMAL")]
    [Trait("Category", "Enum")]
    public void EnumOperationMode_ShouldHave_REGISTER_NORMAL()
    {
        var values = Enum.GetValues<EnumOperationMode>();
        Assert.Equal(2, values.Length);
        Assert.Contains(EnumOperationMode.REGISTER, values);
        Assert.Contains(EnumOperationMode.NORMAL, values);
    }

    [Fact(DisplayName = "A22.9: EnumPalette — 4종 팔레트")]
    [Trait("Category", "Enum")]
    public void EnumPalette_ShouldHave_4_Palettes()
    {
        var values = Enum.GetValues<EnumPalette>();
        Assert.Equal(4, values.Length);
        Assert.Contains(EnumPalette.WHITE_HOT, values);
        Assert.Contains(EnumPalette.BLACK_HOT, values);
        Assert.Contains(EnumPalette.RAINBOW, values);
        Assert.Contains(EnumPalette.IRONBOW, values);
    }

    [Fact(DisplayName = "A22.10: EnumWeatherMode — 7종 악천후")]
    [Trait("Category", "Enum")]
    public void EnumWeatherMode_ShouldHave_7_Modes()
    {
        var values = Enum.GetValues<EnumWeatherMode>();
        Assert.Equal(7, values.Length);
        Assert.Contains(EnumWeatherMode.NORMAL, values);
        Assert.Contains(EnumWeatherMode.FOG, values);
        Assert.Contains(EnumWeatherMode.SEA_FOG, values);
        Assert.Contains(EnumWeatherMode.YELLOW_DUST, values);
        Assert.Contains(EnumWeatherMode.RAIN, values);
        Assert.Contains(EnumWeatherMode.SNOW, values);
        Assert.Contains(EnumWeatherMode.HEAT_HAZE, values);
    }

    [Fact(DisplayName = "A22.11: EnumDayNightMode — AUTO, DAY, NIGHT")]
    [Trait("Category", "Enum")]
    public void EnumDayNightMode_ShouldHave_AUTO_DAY_NIGHT()
    {
        var values = Enum.GetValues<EnumDayNightMode>();
        Assert.Equal(3, values.Length);
        Assert.Contains(EnumDayNightMode.AUTO, values);
        Assert.Contains(EnumDayNightMode.DAY, values);
        Assert.Contains(EnumDayNightMode.NIGHT, values);
    }

    [Fact(DisplayName = "A22.12: EnumCameraVideoMode — 4종 영상모드")]
    [Trait("Category", "Enum")]
    public void EnumCameraVideoMode_ShouldHave_4_Modes()
    {
        var values = Enum.GetValues<EnumCameraVideoMode>();
        Assert.Equal(4, values.Length);
        Assert.Contains(EnumCameraVideoMode.NORMAL, values);
        Assert.Contains(EnumCameraVideoMode.STABILIZATION, values);
        Assert.Contains(EnumCameraVideoMode.BLC, values);
        Assert.Contains(EnumCameraVideoMode.NIGHT_ENHANCE, values);
    }

    [Fact(DisplayName = "A22.13: EnumTrackingStatus — Active, Lost, Idle")]
    [Trait("Category", "Enum")]
    public void EnumTrackingStatus_ShouldHave_Active_Lost_Idle()
    {
        var values = Enum.GetValues<EnumTrackingStatus>();
        Assert.Equal(3, values.Length);
        Assert.Contains(EnumTrackingStatus.Active, values);
        Assert.Contains(EnumTrackingStatus.Lost, values);
        Assert.Contains(EnumTrackingStatus.Idle, values);
    }
}

/// <summary>
/// Phase 23: EnumWindyMode 보정 — wind0~wind3 (NATS v1.2 §5.1)
/// </summary>
public class Phase23_EnumWindyModeTests
{
    [Fact(DisplayName = "A23.1: EnumWindyMode — wind0, wind1, wind2, wind3")]
    [Trait("Category", "Enum")]
    public void EnumWindyMode_ShouldHave_wind0_wind1_wind2_wind3()
    {
        var values = Enum.GetValues<EnumWindyMode>();
        Assert.Equal(4, values.Length);
        Assert.Contains(EnumWindyMode.wind0, values);
        Assert.Contains(EnumWindyMode.wind1, values);
        Assert.Contains(EnumWindyMode.wind2, values);
        Assert.Contains(EnumWindyMode.wind3, values);

        // NATS 직렬화 시 문자열 일치 검증
        Assert.Equal("wind0", EnumWindyMode.wind0.ToString());
        Assert.Equal("wind3", EnumWindyMode.wind3.ToString());
    }
}

/// <summary>
/// Phase 24: Body DTO bool→string 보정 (6종, NATS v1.2 §6 on/off = string)
/// </summary>
public class Phase24_BodyDtoBoolToStringTests
{
    [Fact(DisplayName = "A24.1: WiperSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void WiperSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "wiper": "on"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.WiperSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(201, dto.CameraId);
        Assert.Equal("on", dto.Wiper);
    }

    [Fact(DisplayName = "A24.2: HeaterSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void HeaterSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "heater": "on"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.HeaterSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("on", dto.Heater);
    }

    [Fact(DisplayName = "A24.3: FanSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void FanSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "fan": "on"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.FanSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("on", dto.Fan);
    }

    [Fact(DisplayName = "A24.4: TrackingSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void TrackingSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "tracking": "on"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.TrackingSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("on", dto.Tracking);
    }

    [Fact(DisplayName = "A24.5: HeadlightSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void HeadlightSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "headlight": "off"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.HeadlightSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("off", dto.Headlight);
    }

    [Fact(DisplayName = "A24.6: PowerSetBodyDto — string on/off")]
    [Trait("Category", "DTO")]
    public void PowerSetBodyDto_Deserialization_FromNatsJson_ShouldMapStringOnOff()
    {
        var json = """{"camera_id": 201, "power": "on"}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.PowerSetBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal("on", dto.Power);
    }
}

/// <summary>
/// Phase 25: PtzControlBodyDto 구조 재설계 (NATS v1.2 §6)
/// </summary>
public class Phase25_PtzControlBodyDtoTests
{
    [Fact(DisplayName = "A25.1-1: PtzControlBodyDto — Continuous Movement")]
    [Trait("Category", "DTO")]
    public void PtzControlBodyDto_Deserialization_ContinuousMovement()
    {
        var json = """{"camera_id": 201, "pan_tilt_speed": 50, "zoom_speed": 30, "timeout_ms": 3000}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.PtzControlBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(201, dto.CameraId);
        Assert.Equal(50, dto.PanTiltSpeed);
        Assert.Equal(30, dto.ZoomSpeed);
        Assert.Equal(3000, dto.TimeoutMs);
    }

    [Fact(DisplayName = "A25.1-2: PtzControlBodyDto — Preset Move")]
    [Trait("Category", "DTO")]
    public void PtzControlBodyDto_Deserialization_PresetMove()
    {
        var json = """{"camera_id": 201, "preset": 3, "pan_tilt_speed": 80, "zoom_speed": 50}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.PtzControlBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(3, dto.Preset);
        Assert.Equal(80, dto.PanTiltSpeed);
        Assert.Equal(50, dto.ZoomSpeed);
        Assert.Null(dto.TimeoutMs);
    }

    [Fact(DisplayName = "A25.1-3: PtzControlBodyDto — Absolute Position")]
    [Trait("Category", "DTO")]
    public void PtzControlBodyDto_Deserialization_AbsolutePosition()
    {
        var json = """{"camera_id": 201, "pan": 1000, "tilt": 5000, "zoom": 2000, "pan_tilt_speed": 70, "zoom_speed": 50}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.PtzControlBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(1000, dto.Pan);
        Assert.Equal(5000, dto.Tilt);
        Assert.Equal(2000, dto.Zoom);
        Assert.Equal(70, dto.PanTiltSpeed);
    }

    [Fact(DisplayName = "A25.1-4: PtzControlBodyDto — Center Click")]
    [Trait("Category", "DTO")]
    public void PtzControlBodyDto_Deserialization_CenterClick()
    {
        var json = """{"camera_id": 201, "x": 5000, "y": 5000, "pan_tilt_speed": 50}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.PtzControlBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(5000, dto.X);
        Assert.Equal(5000, dto.Y);
        Assert.Equal(50, dto.PanTiltSpeed);
    }
}

#endregion

#region Phase 26: TrackingStatusBodyDto 구조 재설계

[Collection("Phase26")]
public class Phase26_TrackingStatusBodyDtoTests
{
    [Fact(DisplayName = "A26.1-1: TrackingStatusBodyDto — Active Tracking (full)")]
    [Trait("Category", "DTO")]
    public void TrackingStatusBodyDto_Deserialization_ActiveTracking()
    {
        var json = """
        {
            "camera_id": 201,
            "tracking": "active",
            "target": {
                "label": "person",
                "confidence": 0.92,
                "bbox": [150, 220, 60, 120],
                "thumbnail": "http://192.168.1.50:8080/tracking/frame_001.jpg"
            },
            "target_location": {
                "latitude": 38.1235,
                "longitude": 127.5680,
                "distance_m": 120.5
            }
        }
        """;
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.TrackingStatusBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(201, dto.CameraId);
        Assert.Equal("active", dto.Tracking);

        // target (DetectedObjectDto with thumbnail)
        Assert.NotNull(dto.Target);
        Assert.Equal("person", dto.Target.Label);
        Assert.Equal(0.92, dto.Target.Confidence);
        Assert.NotNull(dto.Target.Bbox);
        Assert.Equal(4, dto.Target.Bbox.Count);
        Assert.Equal("http://192.168.1.50:8080/tracking/frame_001.jpg", dto.Target.Thumbnail);

        // target_location (TrackingTargetLocationDto)
        Assert.NotNull(dto.TargetLocation);
        Assert.Equal(38.1235, dto.TargetLocation.Latitude);
        Assert.Equal(127.5680, dto.TargetLocation.Longitude);
        Assert.Equal(120.5, dto.TargetLocation.DistanceM);
    }

    [Fact(DisplayName = "A26.1-2: TrackingStatusBodyDto — Idle Tracking (null target)")]
    [Trait("Category", "DTO")]
    public void TrackingStatusBodyDto_Deserialization_IdleTracking()
    {
        var json = """{"camera_id": 201, "tracking": "idle", "target": null, "target_location": null}""";
        var dto = JsonConvert.DeserializeObject<Dto.Brokers.TrackingStatusBodyDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(201, dto.CameraId);
        Assert.Equal("idle", dto.Tracking);
        Assert.Null(dto.Target);
        Assert.Null(dto.TargetLocation);
    }
}

#endregion

#region - Phase 56: Server API DTO Serialization Tests -

public class ServerApiDtoSerializationTests
{
    [Fact(DisplayName = "A56.1: CategoryDetailDto — servers 리스트 포함 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void CategoryDetailDto_Serialization_ShouldIncludeServersList()
    {
        // Arrange
        var dto = new CategoryDetailDto
        {
            Id = 1,
            Name = "VMS 서버",
            TypeServer = "VMS",
            Description = "Video Management System",
            SortOrder = 1,
            Servers = new System.Collections.Generic.List<ServerDto>
            {
                new() { Id = 10, CategoryId = 1, Name = "VMS-01", IpAddress = "192.168.1.10", Port = 8080 },
                new() { Id = 11, CategoryId = 1, Name = "VMS-02", IpAddress = "192.168.1.11", Port = 8080 }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<CategoryDetailDto>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized.Id);
        Assert.Equal("VMS 서버", deserialized.Name);
        Assert.Equal("VMS", deserialized.TypeServer);
        Assert.Equal("Video Management System", deserialized.Description);
        Assert.Equal(1, deserialized.SortOrder);
        Assert.NotNull(deserialized.Servers);
        Assert.Equal(2, deserialized.Servers.Count);
        Assert.Equal("VMS-01", deserialized.Servers[0].Name);
        Assert.Equal("192.168.1.11", deserialized.Servers[1].IpAddress);

        // JSON 필드명 검증
        Assert.Contains("\"servers\"", json);
        Assert.Contains("\"type_server\"", json);
    }

    [Fact(DisplayName = "A56.2: ServerMetricDto — 메트릭 필드 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void ServerMetricDto_Serialization_ShouldMapAllMetricFields()
    {
        // Arrange
        var json = """
        {
            "id": 100,
            "server_id": 5,
            "cpu_usage": 75.5,
            "ram_usage": 82.3,
            "ram_total_gb": 64.0,
            "ram_used_gb": 52.7,
            "disk_usage": 45.0,
            "disk_total_gb": 1000.0,
            "disk_used_gb": 450.0,
            "network_in_mbps": 120.5,
            "network_out_mbps": 80.2,
            "process_count": 256,
            "detail": {"gpu_usage": 35.0},
            "collected_at": "2026-02-24T10:30:00+09:00",
            "threshold_exceeded": {"cpu": {"level": "warning", "value": 75.5, "threshold": 80}}
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<ServerMetricDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(100, dto.Id);
        Assert.Equal(5, dto.ServerId);
        Assert.Equal(75.5, dto.CpuUsage);
        Assert.Equal(82.3, dto.RamUsage);
        Assert.Equal(64.0, dto.RamTotalGb);
        Assert.Equal(52.7, dto.RamUsedGb);
        Assert.Equal(45.0, dto.DiskUsage);
        Assert.Equal(1000.0, dto.DiskTotalGb);
        Assert.Equal(450.0, dto.DiskUsedGb);
        Assert.Equal(120.5, dto.NetworkInMbps);
        Assert.Equal(80.2, dto.NetworkOutMbps);
        Assert.Equal(256, dto.ProcessCount);
        Assert.NotNull(dto.Detail);
        Assert.Equal(35.0, (double)dto.Detail!["gpu_usage"]!);
        Assert.Equal("2026-02-24T10:30:00+09:00", dto.CollectedAt);
        Assert.NotNull(dto.ThresholdExceeded);

        // 왕복 검증
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"cpu_usage\"", serialized);
        Assert.Contains("\"network_in_mbps\"", serialized);
        Assert.Contains("\"collected_at\"", serialized);
    }

    [Fact(DisplayName = "A56.3: ServerMetricLatestDto — server_id + server_name + latest_metrics 래핑 구조")]
    [Trait("Category", "DTO")]
    public void ServerMetricLatestDto_Serialization_ShouldWrapLatestMetrics()
    {
        // Arrange
        var json = """
        {
            "server_id": 5,
            "server_name": "VMS-01",
            "latest_metrics": {
                "id": 200,
                "server_id": 5,
                "cpu_usage": 90.0,
                "ram_usage": 85.0,
                "collected_at": "2026-02-24T11:00:00+09:00"
            }
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<ServerMetricLatestDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(5, dto.ServerId);
        Assert.Equal("VMS-01", dto.ServerName);
        Assert.NotNull(dto.LatestMetrics);
        Assert.Equal(200, dto.LatestMetrics.Id);
        Assert.Equal(5, dto.LatestMetrics.ServerId);
        Assert.Equal(90.0, dto.LatestMetrics.CpuUsage);

        // 왕복 검증
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"server_id\"", serialized);
        Assert.Contains("\"latest_metrics\"", serialized);
    }

    [Fact(DisplayName = "A56.4: EnclosureMetricDto — 환경 모니터링 필드 (string 타입 확인)")]
    [Trait("Category", "DTO")]
    public void EnclosureMetricDto_Serialization_ShouldMapEnvironmentalFields()
    {
        // Arrange
        var json = """
        {
            "id": 300,
            "enclosure_id": 10,
            "temperature": "25.5",
            "humidity": "60.2",
            "current": "1.5",
            "voltage": "220.0",
            "vibration": 15,
            "ups_battery_level": 95,
            "ups_charging": true,
            "detail": {"door_open_count": 3},
            "created_at": "2026-02-24T12:00:00+09:00"
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<EnclosureMetricDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(300, dto.Id);
        Assert.Equal(10, dto.EnclosureId);
        Assert.Equal("25.5", dto.Temperature);
        Assert.Equal("60.2", dto.Humidity);
        Assert.Equal("1.5", dto.Current);
        Assert.Equal("220.0", dto.Voltage);
        Assert.Equal(15, dto.Vibration);
        Assert.Equal(95, dto.UpsBatteryLevel);
        Assert.True(dto.UpsCharging);
        Assert.NotNull(dto.Detail);

        // string 타입 확인 (설계 문서: 서버가 string으로 반환)
        Assert.IsType<string>(dto.Temperature);
        Assert.IsType<string>(dto.Humidity);
    }

    [Fact(DisplayName = "A56.5: MetricDeleteResultDto — deleted_count 필드")]
    [Trait("Category", "DTO")]
    public void MetricDeleteResultDto_Serialization_ShouldMapDeletedCount()
    {
        // Arrange
        var json = """{"deleted_count": 42}""";

        // Act
        var dto = JsonConvert.DeserializeObject<MetricDeleteResultDto>(json);
        Assert.NotNull(dto);
        Assert.Equal(42, dto.DeletedCount);

        // 왕복 검증
        var serialized = JsonConvert.SerializeObject(dto);
        Assert.Contains("\"deleted_count\"", serialized);
        var roundtrip = JsonConvert.DeserializeObject<MetricDeleteResultDto>(serialized);
        Assert.NotNull(roundtrip);
        Assert.Equal(42, roundtrip.DeletedCount);
    }

    [Fact(DisplayName = "A56.6: EnclosureMetricSaveResponseDto — threshold_exceeded top-level 구조")]
    [Trait("Category", "DTO")]
    public void EnclosureMetricSaveResponseDto_Serialization_ShouldMapThresholdExceededAtTopLevel()
    {
        // Arrange — threshold_exceeded가 data와 동일 레벨 (§5.5.9 특수 구조)
        var json = """
        {
            "success": true,
            "message": "Enclosure metric saved successfully",
            "data": {
                "id": 500,
                "enclosure_id": 10,
                "temperature": "38.5",
                "humidity": "75.0",
                "current": "2.1",
                "voltage": "220.5",
                "vibration": 30,
                "ups_battery_level": 80,
                "ups_charging": false,
                "created_at": "2026-02-24T14:00:00+09:00"
            },
            "threshold_exceeded": [
                {
                    "field": "temperature",
                    "value": 38.5,
                    "threshold": 35.0,
                    "type": "HIGH"
                },
                {
                    "field": "humidity",
                    "value": 75.0,
                    "threshold": 70.0,
                    "type": "HIGH"
                }
            ]
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<EnclosureMetricSaveResponseDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.True(dto.Success);
        Assert.Equal("Enclosure metric saved successfully", dto.Message);

        // data 검증
        Assert.NotNull(dto.Data);
        Assert.Equal(500, dto.Data.Id);
        Assert.Equal(10, dto.Data.EnclosureId);
        Assert.Equal("38.5", dto.Data.Temperature);

        // threshold_exceeded 검증 (top-level, data와 동일 레벨)
        Assert.NotNull(dto.ThresholdExceeded);
        Assert.Equal(2, dto.ThresholdExceeded.Count);
        Assert.Equal("temperature", dto.ThresholdExceeded[0].Field);
        Assert.Equal(38.5, dto.ThresholdExceeded[0].Value);
        Assert.Equal(35.0, dto.ThresholdExceeded[0].Threshold);
        Assert.Equal("HIGH", dto.ThresholdExceeded[0].Type);
        Assert.Equal("humidity", dto.ThresholdExceeded[1].Field);
    }

    #region - Camera Preset / ROI / Point DTO 테스트 -

    [Fact(DisplayName = "A57.1: CameraPresetDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void CameraPresetDto_Serialization_RoundTrip()
    {
        // Arrange
        var dto = new CameraPresetDto
        {
            PresetIndex = 1,
            PresetName = "Home",
            TouringTime = 15
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<CameraPresetDto>(json);

        // Assert — Create 시 불필요 필드 미포함
        Assert.DoesNotContain("\"id\":", json);  // DefaultValueHandling.Ignore
        Assert.DoesNotContain("\"camera_id\":", json);  // DefaultValueHandling.Ignore
        Assert.DoesNotContain("\"camera_name\":", json);  // NullValueHandling.Ignore
        Assert.DoesNotContain("\"roi_count\":", json);  // DefaultValueHandling.Ignore
        Assert.DoesNotContain("\"rois\":", json);  // NullValueHandling.Ignore
        Assert.DoesNotContain("\"created_at\":", json);  // NullValueHandling.Ignore
        Assert.Contains("\"preset_index\":1", json);
        Assert.Contains("\"preset_name\":\"Home\"", json);
        Assert.Contains("\"touring_time\":15", json);

        Assert.NotNull(d);
        Assert.Equal(1, d.PresetIndex);
        Assert.Equal("Home", d.PresetName);
        Assert.Equal(15, d.TouringTime);
    }

    [Fact(DisplayName = "A57.2: CameraPresetDto 서버 응답 역직렬화")]
    [Trait("Category", "DTO")]
    public void CameraPresetDto_Deserialization_FromServerResponse()
    {
        // Arrange — 서버 응답 JSON
        var json = """
        {
            "id": 10,
            "camera_id": 3,
            "camera_name": "PTZ-CAM-01",
            "preset_index": 1,
            "preset_name": "Home",
            "touring_time": 10,
            "roi_count": 2,
            "created_at": "2026-02-24T10:00:00+09:00",
            "updated_at": "2026-02-24T10:05:00+09:00"
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<CameraPresetDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal(3, dto.CameraId);
        Assert.Equal("PTZ-CAM-01", dto.CameraName);
        Assert.Equal(1, dto.PresetIndex);
        Assert.Equal("Home", dto.PresetName);
        Assert.Equal(10, dto.TouringTime);
        Assert.Equal(2, dto.RoiCount);
        Assert.Null(dto.Rois);  // 목록 응답에서는 rois 미포함
        Assert.NotNull(dto.CreatedAt);
        Assert.NotNull(dto.UpdatedAt);
    }

    [Fact(DisplayName = "A57.3: RoiDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void RoiDto_Serialization_RoundTrip()
    {
        // Arrange — Create 용 DTO (points 포함)
        var dto = new RoiDto
        {
            Name = "ROI-A",
            ResolutionWidth = 1920.0,
            ResolutionHeight = 1080.0,
            IsEnable = true,
            Points = new List<XyPointDto>
            {
                new() { X = 0.1, Y = 0.2, PointOrder = 1 },
                new() { X = 0.5, Y = 0.8, PointOrder = 2 },
                new() { X = 0.9, Y = 0.2, PointOrder = 3 }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<RoiDto>(json);

        // Assert
        Assert.DoesNotContain("\"id\":", json);
        Assert.DoesNotContain("\"preset_id\":", json);
        Assert.Contains("\"name\":\"ROI-A\"", json);
        Assert.Contains("\"resolution_width\":1920.0", json);
        Assert.Contains("\"points\":", json);

        Assert.NotNull(d);
        Assert.Equal("ROI-A", d.Name);
        Assert.Equal(1920.0, d.ResolutionWidth);
        Assert.Equal(1080.0, d.ResolutionHeight);
        Assert.True(d.IsEnable);
        Assert.NotNull(d.Points);
        Assert.Equal(3, d.Points.Count);
        Assert.Equal(0.1, d.Points[0].X);
        Assert.Equal(0.2, d.Points[0].Y);
        Assert.Equal(1, d.Points[0].PointOrder);
    }

    [Fact(DisplayName = "A57.4: XyPointDto 직렬화 왕복 검증")]
    [Trait("Category", "DTO")]
    public void XyPointDto_Serialization_RoundTrip()
    {
        // Arrange
        var dto = new XyPointDto { X = 0.5, Y = 0.7, PointOrder = 2 };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<XyPointDto>(json);

        // Assert
        Assert.DoesNotContain("\"id\":", json);
        Assert.DoesNotContain("\"roi_id\":", json);
        Assert.Contains("\"x\":0.5", json);
        Assert.Contains("\"y\":0.7", json);
        Assert.Contains("\"order\":2", json);

        Assert.NotNull(d);
        Assert.Equal(0.5, d.X);
        Assert.Equal(0.7, d.Y);
        Assert.Equal(2, d.PointOrder);
    }

    [Fact(DisplayName = "A57.5: XyPointBulkDto 직렬화 검증")]
    [Trait("Category", "DTO")]
    public void XyPointBulkDto_Serialization_ShouldContainPointsArray()
    {
        // Arrange
        var dto = new XyPointBulkDto
        {
            Points = new List<XyPointDto>
            {
                new() { X = 0.0, Y = 0.0, PointOrder = 1 },
                new() { X = 1.0, Y = 0.0, PointOrder = 2 },
                new() { X = 0.5, Y = 1.0, PointOrder = 3 }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(dto);
        var d = JsonConvert.DeserializeObject<XyPointBulkDto>(json);

        // Assert
        Assert.Contains("\"points\":", json);
        Assert.NotNull(d);
        Assert.Equal(3, d.Points.Count);
    }

    [Fact(DisplayName = "A57.6: PresetListDataDto 역직렬화 — data.items + total")]
    [Trait("Category", "DTO")]
    public void PresetListDataDto_Deserialization_FromServerListResponse()
    {
        // Arrange — 서버 목록 응답 JSON
        var json = """
        {
            "success": true,
            "message": "2 presets retrieved",
            "data": {
                "items": [
                    { "id": 10, "camera_id": 3, "preset_index": 1, "preset_name": "Home", "touring_time": 10, "roi_count": 1 },
                    { "id": 11, "camera_id": 3, "preset_index": 2, "preset_name": "Gate", "touring_time": 5, "roi_count": 0 }
                ],
                "total": 2
            }
        }
        """;

        // Act
        var response = JsonConvert.DeserializeObject<ApiResponse<PresetListDataDto>>(json);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Total);
        Assert.Equal(2, response.Data.Items.Count);
        Assert.Equal("Home", response.Data.Items[0].PresetName);
        Assert.Equal("Gate", response.Data.Items[1].PresetName);
    }

    #endregion
}

#endregion

#region - FromEventConverter 테스트 -

public class FromEventConverterTests
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        DateParseHandling = DateParseHandling.None
    };

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_NullTypeEvent_ReturnsNull()
    {
        // Arrange — from_event에 type_event가 없는 경우
        var json = """
        {
            "id": 1,
            "type_event": "Action",
            "content": "test",
            "user": "admin",
            "from_event": {
                "id": 100,
                "created_at": "2025-01-01T00:00:00Z"
            }
        }
        """;

        // Act — throw 없이 역직렬화 성공
        var dto = JsonConvert.DeserializeObject<ActionEventDto>(json, _settings);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Null(dto.FromEvent);
    }

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_UnknownTypeEvent_ReturnsNull()
    {
        // Arrange — from_event에 알 수 없는 type_event
        var json = """
        {
            "id": 2,
            "type_event": "Action",
            "content": "test",
            "user": "admin",
            "from_event": {
                "id": 200,
                "type_event": "Connection",
                "created_at": "2025-01-01T00:00:00Z"
            }
        }
        """;

        // Act — "Connection"은 매핑 대상이 아니지만 throw 하지 않음
        var dto = JsonConvert.DeserializeObject<ActionEventDto>(json, _settings);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(2, dto.Id);
        Assert.Null(dto.FromEvent);
    }

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_Intrusion_ReturnsDetectionEventDto()
    {
        // Arrange
        var json = """
        {
            "id": 3,
            "type_event": "Action",
            "content": "침입 확인",
            "user": "operator",
            "from_event": {
                "id": 1001,
                "type_event": "Intrusion",
                "action_reported": "True",
                "device": {
                    "id": 5,
                    "type_device": "Fence",
                    "name_device": "Sensor-A"
                },
                "result": "PIR_SENSOR",
                "created_at": "2025-01-10T10:00:00Z"
            }
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<ActionEventDto>(json, _settings);

        // Assert
        Assert.NotNull(dto);
        Assert.NotNull(dto.FromEvent);
        Assert.IsType<DetectionEventDto>(dto.FromEvent);
        var detection = (DetectionEventDto)dto.FromEvent;
        Assert.Equal(1001, detection.Id);
        Assert.Equal("Intrusion", detection.TypeEvent);
    }

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_Fault_ReturnsMalfunctionEventDto()
    {
        // Arrange
        var json = """
        {
            "id": 4,
            "type_event": "Action",
            "content": "장애 확인",
            "user": "operator",
            "from_event": {
                "id": 2001,
                "type_event": "Fault",
                "action_reported": "True",
                "device": {
                    "id": 10,
                    "type_device": "Multi",
                    "name_device": "Sensor-B"
                },
                "reason": "FAULT_ETC",
                "created_at": "2025-01-10T10:00:00Z"
            }
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<ActionEventDto>(json, _settings);

        // Assert
        Assert.NotNull(dto);
        Assert.NotNull(dto.FromEvent);
        Assert.IsType<MalfunctionEventDto>(dto.FromEvent);
        var malfunction = (MalfunctionEventDto)dto.FromEvent;
        Assert.Equal(2001, malfunction.Id);
        Assert.Equal("Fault", malfunction.TypeEvent);
    }

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_NullFromEvent_ReturnsNull()
    {
        // Arrange — from_event가 null
        var json = """
        {
            "id": 5,
            "type_event": "Action",
            "content": "test",
            "user": "admin",
            "from_event": null
        }
        """;

        // Act
        var dto = JsonConvert.DeserializeObject<ActionEventDto>(json, _settings);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(5, dto.Id);
        Assert.Null(dto.FromEvent);
    }

    [Fact]
    [Trait("Category", "Converter")]
    public void FromEventConverter_ApiListResponse_WithMixedFromEvents_DeserializesAll()
    {
        // Arrange — 실제 API 응답 형식: 일부는 정상, 일부는 null type_event
        var json = """
        {
            "success": true,
            "message": "3 action events retrieved",
            "data": [
                {
                    "id": 10,
                    "type_event": "Action",
                    "content": "정상 조치",
                    "user": "admin",
                    "from_event": {
                        "id": 100,
                        "type_event": "Intrusion",
                        "created_at": "2025-01-01T00:00:00Z"
                    }
                },
                {
                    "id": 11,
                    "type_event": "Action",
                    "content": "type_event 없는 조치",
                    "user": "admin",
                    "from_event": {
                        "id": 101,
                        "created_at": "2025-01-01T00:00:00Z"
                    }
                },
                {
                    "id": 12,
                    "type_event": "Action",
                    "content": "from_event null 조치",
                    "user": "admin",
                    "from_event": null
                }
            ],
            "pagination": {
                "page": 1,
                "limit": 100,
                "total": 3,
                "total_pages": 1
            }
        }
        """;

        // Act — 전체 응답 역직렬화 성공 (이전에는 두 번째 항목에서 throw)
        var response = JsonConvert.DeserializeObject<ApiListResponse<ActionEventDto>>(json, _settings);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(3, response.Data.Count);

        // 첫 번째: 정상 from_event
        Assert.NotNull(response.Data[0].FromEvent);
        Assert.IsType<DetectionEventDto>(response.Data[0].FromEvent);

        // 두 번째: type_event 없음 → null (이전에는 여기서 throw → 전체 실패)
        Assert.Null(response.Data[1].FromEvent);

        // 세 번째: from_event null → null
        Assert.Null(response.Data[2].FromEvent);
    }
}

#endregion

#region - EventStatistics DTO 테스트 -

public class EventStatisticsDtoTests
{
    #region - EventTrendItemDto -

    [Fact]
    [Trait("Category", "API")]
    public void EventTrendItemDto_Deserialization_ShouldMapAllFields()
    {
        // Arrange
        var json = @"{
            ""time_bucket"": ""2025-01-15 10"",
            ""sensor_detection"": 3,
            ""camera_detection"": 1,
            ""malfunction"": 30,
            ""connection"": 0,
            ""action"": 2
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<EventTrendItemDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("2025-01-15 10", dto.TimeBucket);
        Assert.Equal(3, dto.SensorDetection);
        Assert.Equal(1, dto.CameraDetection);
        Assert.Equal(30, dto.Malfunction);
        Assert.Equal(0, dto.Connection);
        Assert.Equal(2, dto.Action);
    }

    #endregion

    #region - EventTrendDto -

    [Fact]
    [Trait("Category", "API")]
    public void EventTrendDto_Deserialization_ShouldMapSeriesArray()
    {
        // Arrange
        var json = @"{
            ""interval"": ""hour"",
            ""start_date"": ""2025-01-15T00:00:00"",
            ""end_date"": ""2025-01-16T00:00:00"",
            ""series"": [
                {
                    ""time_bucket"": ""2025-01-15 00"",
                    ""sensor_detection"": 3,
                    ""camera_detection"": 1,
                    ""malfunction"": 30,
                    ""connection"": 0,
                    ""action"": 2
                },
                {
                    ""time_bucket"": ""2025-01-15 01"",
                    ""sensor_detection"": 0,
                    ""camera_detection"": 5,
                    ""malfunction"": 28,
                    ""connection"": 0,
                    ""action"": 0
                }
            ]
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<EventTrendDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("hour", dto.Interval);
        Assert.Equal("2025-01-15T00:00:00", dto.StartDate);
        Assert.Equal("2025-01-16T00:00:00", dto.EndDate);
        Assert.Equal(2, dto.Series.Count);
        Assert.Equal("2025-01-15 00", dto.Series[0].TimeBucket);
        Assert.Equal(3, dto.Series[0].SensorDetection);
        Assert.Equal("2025-01-15 01", dto.Series[1].TimeBucket);
        Assert.Equal(5, dto.Series[1].CameraDetection);
    }

    #endregion

    #region - EventSummaryDto -

    [Fact]
    [Trait("Category", "API")]
    public void EventSummaryDto_Deserialization_ShouldMapNestedObjects()
    {
        // Arrange
        var json = @"{
            ""start_date"": ""2025-01-15T00:00:00"",
            ""end_date"": ""2025-01-22T00:00:00"",
            ""days_in_range"": 7,
            ""total"": 275,
            ""sensor_detection"": 150,
            ""camera_detection"": 30,
            ""malfunction"": 45,
            ""connection"": 30,
            ""action"": 20,
            ""daily_averages"": {
                ""sensor_detection"": 21.4,
                ""camera_detection"": 4.3,
                ""malfunction"": 6.4,
                ""connection"": 4.3,
                ""action"": 2.9
            },
            ""active_devices"": {
                ""sensors"": 25,
                ""cameras"": 15,
                ""controllers"": 5
            }
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<EventSummaryDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(275, dto.Total);
        Assert.Equal(150, dto.SensorDetection);
        Assert.Equal(30, dto.CameraDetection);
        Assert.Equal(45, dto.Malfunction);
        Assert.Equal(7, dto.DaysInRange);

        Assert.NotNull(dto.DailyAverages);
        Assert.Equal(21.4, dto.DailyAverages.SensorDetection);
        Assert.Equal(4.3, dto.DailyAverages.CameraDetection);

        Assert.NotNull(dto.ActiveDevices);
        Assert.Equal(25, dto.ActiveDevices.Sensors);
        Assert.Equal(15, dto.ActiveDevices.Cameras);
        Assert.Equal(5, dto.ActiveDevices.Controllers);
    }

    #endregion

    #region - EventByDeviceDto -

    [Fact]
    [Trait("Category", "API")]
    public void EventByDeviceDto_Deserialization_ShouldMapControllerAndCamera()
    {
        // Arrange
        var json = @"{
            ""start_date"": ""2025-01-15T00:00:00"",
            ""end_date"": ""2025-01-16T00:00:00"",
            ""controllers"": [
                {
                    ""controller_id"": 1,
                    ""controller_name"": ""Controller-A"",
                    ""controller_number"": 1,
                    ""sensor_detection"": 45,
                    ""malfunction"": 12,
                    ""connection"": 3
                },
                {
                    ""controller_id"": 2,
                    ""controller_name"": ""Controller-B"",
                    ""controller_number"": 2,
                    ""sensor_detection"": 30,
                    ""malfunction"": 8,
                    ""connection"": 1
                }
            ],
            ""cameras"": [
                {
                    ""camera_id"": 101,
                    ""camera_name"": ""AI-Camera-Front"",
                    ""camera_number"": 10,
                    ""camera_detection"": 25
                }
            ]
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<EventByDeviceDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(2, dto.Controllers.Count);
        Assert.Equal(1, dto.Controllers[0].ControllerId);
        Assert.Equal("Controller-A", dto.Controllers[0].ControllerName);
        Assert.Equal(45, dto.Controllers[0].SensorDetection);
        Assert.Equal(12, dto.Controllers[0].Malfunction);
        Assert.Equal(30, dto.Controllers[1].SensorDetection);

        Assert.Single(dto.Cameras);
        Assert.Equal(101, dto.Cameras[0].CameraId);
        Assert.Equal("AI-Camera-Front", dto.Cameras[0].CameraName);
        Assert.Equal(25, dto.Cameras[0].CameraDetection);
    }

    #endregion

    #region - EventDashboardDto -

    [Fact]
    [Trait("Category", "API")]
    public void EventDashboardDto_Deserialization_ShouldMapAllSections()
    {
        // Arrange
        var json = @"{
            ""summary"": {
                ""total"": 275,
                ""days_in_range"": 7,
                ""sensor_detection"": 150,
                ""camera_detection"": 30,
                ""malfunction"": 45,
                ""connection"": 30,
                ""action"": 20,
                ""daily_averages"": { ""sensor_detection"": 21.4, ""camera_detection"": 4.3, ""malfunction"": 6.4, ""connection"": 4.3, ""action"": 2.9 },
                ""active_devices"": { ""sensors"": 25, ""cameras"": 15, ""controllers"": 5 }
            },
            ""trend"": {
                ""interval"": ""hour"",
                ""start_date"": ""2025-01-15T00:00:00"",
                ""end_date"": ""2025-01-16T00:00:00"",
                ""series"": [
                    { ""time_bucket"": ""2025-01-15 00"", ""sensor_detection"": 3, ""camera_detection"": 1, ""malfunction"": 30, ""connection"": 0, ""action"": 2 }
                ]
            },
            ""by_device"": {
                ""start_date"": ""2025-01-15T00:00:00"",
                ""end_date"": ""2025-01-16T00:00:00"",
                ""controllers"": [
                    { ""controller_id"": 1, ""controller_name"": ""Controller-A"", ""controller_number"": 1, ""sensor_detection"": 45, ""malfunction"": 12, ""connection"": 3 }
                ],
                ""cameras"": [
                    { ""camera_id"": 101, ""camera_name"": ""AI-Camera-Front"", ""camera_number"": 10, ""camera_detection"": 25 }
                ]
            }
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<EventDashboardDto>(json);

        // Assert
        Assert.NotNull(dto);

        // Summary
        Assert.Equal(275, dto.Summary.Total);
        Assert.Equal(150, dto.Summary.SensorDetection);
        Assert.Equal(21.4, dto.Summary.DailyAverages.SensorDetection);
        Assert.Equal(25, dto.Summary.ActiveDevices.Sensors);

        // Trend
        Assert.Equal("hour", dto.Trend.Interval);
        Assert.Single(dto.Trend.Series);
        Assert.Equal("2025-01-15 00", dto.Trend.Series[0].TimeBucket);

        // ByDevice
        Assert.Single(dto.ByDevice.Controllers);
        Assert.Equal(45, dto.ByDevice.Controllers[0].SensorDetection);
        Assert.Single(dto.ByDevice.Cameras);
        Assert.Equal(25, dto.ByDevice.Cameras[0].CameraDetection);
    }

    #endregion

    #region - ApiResponse<EventDashboardDto> 통합 -

    [Fact]
    [Trait("Category", "API")]
    public void ApiResponse_EventDashboardDto_FullRoundtrip()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""message"": ""Event dashboard statistics retrieved"",
            ""data"": {
                ""summary"": {
                    ""total"": 275,
                    ""days_in_range"": 7,
                    ""sensor_detection"": 150,
                    ""camera_detection"": 30,
                    ""malfunction"": 45,
                    ""connection"": 30,
                    ""action"": 20,
                    ""daily_averages"": { ""sensor_detection"": 21.4, ""camera_detection"": 4.3, ""malfunction"": 6.4, ""connection"": 4.3, ""action"": 2.9 },
                    ""active_devices"": { ""sensors"": 25, ""cameras"": 15, ""controllers"": 5 }
                },
                ""trend"": {
                    ""interval"": ""hour"",
                    ""start_date"": ""2025-01-15T00:00:00"",
                    ""end_date"": ""2025-01-16T00:00:00"",
                    ""series"": [
                        { ""time_bucket"": ""2025-01-15 10"", ""sensor_detection"": 3, ""camera_detection"": 1, ""malfunction"": 30, ""connection"": 0, ""action"": 2 }
                    ]
                },
                ""by_device"": {
                    ""start_date"": ""2025-01-15T00:00:00"",
                    ""end_date"": ""2025-01-16T00:00:00"",
                    ""controllers"": [
                        { ""controller_id"": 1, ""controller_name"": ""Controller-A"", ""controller_number"": 1, ""sensor_detection"": 45, ""malfunction"": 12, ""connection"": 3 }
                    ],
                    ""cameras"": [
                        { ""camera_id"": 101, ""camera_name"": ""AI-Camera-Front"", ""camera_number"": 10, ""camera_detection"": 25 }
                    ]
                }
            }
        }";

        // Act
        var response = ApiMessageHelper.FromJsonResponse<EventDashboardDto>(json);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("Event dashboard statistics retrieved", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal(275, response.Data.Summary.Total);
        Assert.Equal("hour", response.Data.Trend.Interval);
        Assert.Single(response.Data.Trend.Series);
        Assert.Equal(45, response.Data.ByDevice.Controllers[0].SensorDetection);
    }

    #endregion
}

#endregion

#region - WindyMode NATS 연동 테스트 -
/// <summary>
/// WindyMode NATS 연동: Enum, BrokerRequest/Response, ProxySettingDto 테스트
/// </summary>
public class WindyModeNatsTests
{
    #region - Phase 1: Enum + BrokerResponse -

    [Fact(DisplayName = "1.2: EnumGopCommand.WINDY 값 = 5 보존")]
    [Trait("Category", "Enum")]
    public void EnumGopCommand_WINDY_Equals5()
    {
        Assert.Equal(5, (int)EnumGopCommand.WINDY);
    }

    [Fact(DisplayName = "1.3: TryParse(\"WINDY\") → EnumGopCommand.WINDY 매칭")]
    [Trait("Category", "Enum")]
    public void EnumGopCommand_TryParse_WINDY_Matches()
    {
        var result = Enum.TryParse<EnumGopCommand>("WINDY", ignoreCase: true, out var cmd);
        Assert.True(result);
        Assert.Equal(EnumGopCommand.WINDY, cmd);
    }

    [Fact(DisplayName = "1.4: BrokerResponse<WindyBodyDto> RSP JSON 역직렬화")]
    [Trait("Category", "Broker")]
    public void BrokerResponse_WindyBodyDto_RSP_Deserialization()
    {
        // Arrange — 설계 문서 §7.1.2 RSP 형식
        var json = @"{
            ""id"": ""uuid-v4"",
            ""m_type"": ""RSP"",
            ""cmd"": ""WINDY"",
            ""from"": ""PidsProxy"",
            ""body"": { ""mode"": ""wind2"" },
            ""success"": true,
            ""message"": ""풍량 모드 변경 완료"",
            ""req_id"": ""original-request-uuid"",
            ""created"": ""2026-02-05T10:30:00.100Z""
        }";

        // Act
        var rsp = JsonConvert.DeserializeObject<BrokerResponse<Dto.Brokers.WindyBodyDto>>(json);

        // Assert
        Assert.NotNull(rsp);
        Assert.True(rsp!.Success);
        Assert.Equal("풍량 모드 변경 완료", rsp.Message);
        Assert.Equal("original-request-uuid", rsp.RequestId);
        Assert.Equal("RSP", rsp.TypeMessage);
        Assert.Equal("WINDY", rsp.Command);
        Assert.Equal("PidsProxy", rsp.From);
        Assert.NotNull(rsp.Data);
        Assert.Equal("wind2", rsp.Data!.Mode);
    }

    #endregion

    #region - Phase 2: ProxySettingDto -

    [Fact(DisplayName = "2.1: ProxySettingDto §8.8.1 응답 JSON 역직렬화")]
    [Trait("Category", "DTO")]
    public void ProxySettingDto_Deserialization()
    {
        // Arrange — REST API §8.8.1 응답 data
        var json = @"{
            ""id"": 1,
            ""server_id"": 1,
            ""operation_mode"": ""NORMAL"",
            ""windy_mode"": ""wind2"",
            ""created_at"": ""2026-02-06T12:00:00.000Z"",
            ""updated_at"": ""2026-02-06T12:30:00.150Z""
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<ProxySettingDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.Id);
        Assert.Equal(1, dto.ServerId);
        Assert.Equal("NORMAL", dto.OperationMode);
        Assert.Equal("wind2", dto.WindyMode);
        Assert.Equal("2026-02-06T12:00:00.000Z", dto.CreatedAt);
        Assert.Equal("2026-02-06T12:30:00.150Z", dto.UpdatedAt);
    }

    [Fact(DisplayName = "2.1b: ProxySettingDto 기본값 확인")]
    [Trait("Category", "DTO")]
    public void ProxySettingDto_DefaultValues()
    {
        var dto = new ProxySettingDto();
        Assert.Equal("NORMAL", dto.OperationMode);
        Assert.Equal("wind0", dto.WindyMode);
    }

    #endregion

    #region - Phase 4: BrokerRequest + WindyBodyDto -

    [Fact(DisplayName = "4.1: BrokerRequest<WindyBodyDto> REQ JSON 구조 검증")]
    [Trait("Category", "Broker")]
    public void BrokerRequest_WindyBodyDto_REQ_Format()
    {
        // Arrange
        var req = new BrokerRequest<Dto.Brokers.WindyBodyDto>
        {
            Id = "test-uuid",
            Command = EnumGopCommand.WINDY.ToString(),
            From = "GIS",
            Data = new Dto.Brokers.WindyBodyDto { Mode = "wind2" }
        };

        // Act
        var json = JsonConvert.SerializeObject(req);

        // Assert
        Assert.Contains("\"m_type\":\"REQ\"", json);
        Assert.Contains("\"cmd\":\"WINDY\"", json);
        Assert.Contains("\"from\":\"GIS\"", json);
        Assert.Contains("\"mode\":\"wind2\"", json);
    }

    [Fact(DisplayName = "4.2: WindyBodyDto wind0~wind3 전체 직렬화/역직렬화")]
    [Trait("Category", "DTO")]
    public void WindyBodyDto_AllModes_Serialization()
    {
        var modes = new[] { "wind0", "wind1", "wind2", "wind3" };
        foreach (var mode in modes)
        {
            var dto = new Dto.Brokers.WindyBodyDto { Mode = mode };
            var json = JsonConvert.SerializeObject(dto);
            Assert.Contains($"\"mode\":\"{mode}\"", json);

            var roundTrip = JsonConvert.DeserializeObject<Dto.Brokers.WindyBodyDto>(json);
            Assert.NotNull(roundTrip);
            Assert.Equal(mode, roundTrip!.Mode);
        }
    }

    #endregion
}
#endregion

/// <summary>
/// (Phase v2.9.1) ApiMessageHelper 에러 응답 변환 — 422 본문 보존/StatusCode 회귀 잠금.
/// 기존 결함: FastAPI {"detail":[...]} 가 MissingMemberHandling.Ignore로 빈 객체가 되어 본문이 폐기됐다.
/// </summary>
public class ApiMessageHelperErrorTests
{
    private static System.Net.Http.HttpResponseMessage MakeResponse(System.Net.HttpStatusCode code, string body)
        => new System.Net.Http.HttpResponseMessage(code)
        {
            Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    [Fact]
    [Trait("Category", "API")]
    public async Task ToApiResponseAsync_FastApi422Detail_PreservesBodyAndStatusCode()
    {
        // Arrange — FastAPI HTTPValidationError(성공/메시지/에러 키 없음)
        var body = "{\"detail\":[{\"loc\":[\"body\",\"type_device\"],\"msg\":\"value is not a valid enumeration member\",\"type\":\"type_error.enum\"}]}";
        var resp = MakeResponse(System.Net.HttpStatusCode.UnprocessableEntity, body);

        // Act
        var result = await resp.ToApiResponseAsync<ControllerDeviceDto>();

        // Assert — 본문 폐기되지 않고 Error.Details에 보존 + StatusCode=422
        Assert.False(result.Success);
        Assert.Equal(422, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.Details);
        Assert.Contains("type_device", result.Error.Details!);
    }

    [Fact]
    [Trait("Category", "API")]
    public async Task ToApiResponseAsync_StandardErrorEnvelope_ReturnsEnvelopeWithStatusCode()
    {
        // Arrange — 서버 표준 에러 envelope는 그대로 보존되어야 한다
        var body = "{\"success\":false,\"message\":\"Controller not found\",\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"Controller not found\"}}";
        var resp = MakeResponse(System.Net.HttpStatusCode.NotFound, body);

        // Act
        var result = await resp.ToApiResponseAsync<ControllerDeviceDto>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Controller not found", result.Message);
        Assert.NotNull(result.Error);
        Assert.Equal("NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    [Trait("Category", "API")]
    public async Task ToApiResponseAsync_Success_ReturnsData_NoRegression()
    {
        // Arrange — 성공 경로 무영향 확인
        var body = "{\"success\":true,\"message\":\"ok\",\"data\":{\"id\":7,\"name_device\":\"Ctrl-7\",\"type_device\":\"Controller\",\"status\":\"ACTIVATED\"}}";
        var resp = MakeResponse(System.Net.HttpStatusCode.OK, body);

        // Act
        var result = await resp.ToApiResponseAsync<ControllerDeviceDto>();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(7, result.Data!.Id);
    }
}
