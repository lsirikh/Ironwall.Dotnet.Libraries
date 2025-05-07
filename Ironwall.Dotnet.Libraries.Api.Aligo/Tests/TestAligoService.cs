using Dotnet.Gym.Message.Contacts;
using Dotnet.Gym.Message.Enums;
using Ironwall.Dotnet.Libraries.Api.Aligo.Models;
using Ironwall.Dotnet.Libraries.Api.Aligo.Providers;
using Ironwall.Dotnet.Libraries.Api.Aligo.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Newtonsoft.Json;
using System;
using System.Buffers.Text;
using System.IO;
using System.Net;
using Xunit;
using Xunit.Sdk;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 6:52:14 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TestAligoService
{
    
    #region - Ctors -
    public TestAligoService()
    {
        // 실제 로그 서비스 사용
        _logService = new LogService();

        // 실제 API 서비스 인스턴스 (테스트용 Base URL)
        var apiSetupModel = new ApiSetupModel
        {
            Url = @"https://apis.aligo.in/",
            ApiKey = "API키",
            Username = "고객아이디",
            Phone = "고객번호"
        };
        _sender = "고객번호";
        _apiService = new ApiService(_logService, apiSetupModel);
        _messageProvider = new EmsMessageProvider();
        _aligoService = new AligoService(_logService, _apiService, _messageProvider);
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 실제 API 인스턴스를 사용하여 단일 문자 SMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendSmsMessageAsync()
    {
        _apiService.Initialize();

        // Arrange
        var testMessage = new EmsMessageModel
        {
            Sender = _sender, //송신자는 무조건 API에 설정된 전화번호와 일치해야된다. 필수 설정 정보
            Receiver = "01011112222",
            Message = "테스트 메시지입니다.",
            MsgType = EnumMsgType.SMS,
            SendTime = DateTime.Now,
        };

        // Act
        var response = await _aligoService.SendEmsMessageAsync(testMessage, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    /// <summary>
    /// 실제 API 인스턴스를 사용하여 단일 문자 LMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendLmsMessageAsync()
    {
        _apiService.Initialize();

        // Arrange
        var testMessage = new EmsMessageModel
        {
            Sender = _sender, //송신자는 무조건 API에 설정된 전화번호와 일치해야된다. 필수 설정 정보
            Receiver = "01011112222",
            Message = "테스트 메시지입니다.",
            MsgType = EnumMsgType.LMS,
            Title = "테스트 제목",
            //Reservation = DateTime.Now,
            SendTime = DateTime.Now,
        };

        // Act
        var response = await _aligoService.SendEmsMessageAsync(testMessage, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    /// <summary>
    /// 실제 API 인스턴스를 사용하여 단일 문자 MMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendMmsMessageAsync()
    {
        _apiService.Initialize();

        // 3) 샘플 이미지 읽어서 ImageModel 만들어보기
        string path1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample1.jpg");
        byte[] bytes1 = File.ReadAllBytes(path1);
        string base64_1 = Convert.ToBase64String(bytes1);

        string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample2.jpg");
        byte[] bytes2 = File.ReadAllBytes(path2);
        string base64_2 = Convert.ToBase64String(bytes2);

        var sampleImage1 = new ImageModel
        {
            Base64Image = base64_1,
            FileName = "sample1.jpg",
            ContentType = "image/jpeg"
        };

        var sampleImage2 = new ImageModel
        {
            Base64Image = base64_2,
            FileName = "sample2.jpg",
            ContentType = "image/jpeg"
        };

        // Arrange
        var testMessage = new EmsMessageModel
        {
            Sender = _sender, //송신자는 무조건 API에 설정된 전화번호와 일치해야된다. 필수 설정 정보
            Receiver = "01011112222",
            Message = "테스트 메시지입니다.",
            MsgType = EnumMsgType.MMS,
            Title = "테스트 제목",
            //Reservation = DateTime.Now,
            SendTime = DateTime.Now,
            AttachedImages = new List<ImageModel>
            {
                sampleImage1,
                sampleImage2
            }
        };

        // Act
        var response = await _aligoService.SendEmsMessageAsync(testMessage, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    /// <summary>
    /// 실제 API 인스턴스를 사용하여 복수개의 문자 SMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendMassSmsMessageAsync()
    {
        _apiService.Initialize();

        // Arrange
        var emsList = new List<IEmsMessageModel>();

        var now = DateTime.Now;
        for (int i = 0; i < 500; i++)
        {
            emsList.Add(new EmsMessageModel
            {
                Sender = _sender,
                Receiver = $"0101111{(10000 + i):D5}",
                Message = "테스트 메시지입니다.",
                MsgType = EnumMsgType.SMS,
                //Reservation = DateTime.Now,
                SendTime = now
            });
        }


        // Act
        var response = await _aligoService.SendMassEmsMessageAsync(emsList, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    /// <summary>
    /// 실제 API 인스턴스를 사용하여 복수개의 문자 LMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendMassLmsMessageAsync()
    {
        _apiService.Initialize();

        // Arrange
        var emsList = new List<IEmsMessageModel>();
       
        var now = DateTime.Now;
        for (int i = 0; i < 500; i++)
        {
            emsList.Add(new EmsMessageModel
            {
                Sender = _sender,
                Receiver = $"0101111{(10000+i):D5}",
                Message = "테스트 메시지입니다.",
                MsgType = EnumMsgType.LMS,
                Title = "테스트 제목",
                //Reservation = DateTime.Now,
                SendTime = now
            });
        }


        // Act
        var response = await _aligoService.SendMassEmsMessageAsync(emsList, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    /// <summary>
    /// 실제 API 인스턴스를 사용하여 복수개의 문자 MMS 전송 테스트
    /// </summary>
    [Fact]
    public async Task SendMassMmsMessageAsync()
    {
        _apiService.Initialize();

        // Arrange
        var emsList = new List<IEmsMessageModel>();

        // 3) 샘플 이미지 읽어서 ImageModel 만들어보기
        string path1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample1.jpg");
        byte[] bytes1 = File.ReadAllBytes(path1);
        string base64_1 = Convert.ToBase64String(bytes1);

        string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample2.jpg");
        byte[] bytes2 = File.ReadAllBytes(path2);
        string base64_2 = Convert.ToBase64String(bytes2);

        var sampleImage1 = new ImageModel
        {
            Base64Image = base64_1,
            FileName = "sample1.jpg",
            ContentType = "image/jpeg"
        };

        var sampleImage2 = new ImageModel
        {
            Base64Image = base64_2,
            FileName = "sample2.jpg",
            ContentType = "image/jpeg"
        };

        var now = DateTime.Now;
        for (int i = 0; i < 500; i++)
        {
            emsList.Add(new EmsMessageModel
            {
                Sender = _sender,
                Receiver = $"0101111{(10000 + i):D5}",
                Message = "테스트 메시지입니다.",
                MsgType = EnumMsgType.MMS,
                Title = "테스트 제목",
                //Reservation = DateTime.Now,
                SendTime = now,
                AttachedImages = new List<ImageModel>
            {
                sampleImage1,
                sampleImage2
            }
            });
        }


        // Act
        var response = await _aligoService.SendMassEmsMessageAsync(emsList, testMode: true);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
    }

    [Fact]
    public async Task GetSendMessageListAync()
    {
        _apiService.Initialize();

        var response = await _aligoService.GetSentMessageListAsync(1, 50, DateTime.Now, 7);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendListResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
        Assert.Equal("success", json.Message);
    }

    [Fact]
    public async Task GetSendSepecificMessageAync()
    {
        _apiService.Initialize();

        var response = await _aligoService.GetSentSpecificMessageListAsync(989282861, 1, 50);
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendListResponseModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
        Assert.Equal("success", json.Message);
    }

    [Fact]
    public async Task GetAvailableServerAsync()
    {
        _apiService.Initialize();

        var response = await _aligoService.GetAvailableEmsServiceAsync();
        if (response == null) return;
        var responseBody = await response.Content.ReadAsStringAsync();

        // JSON을 ResponseModel 객체로 변환
        var json = JsonConvert.DeserializeObject<SendAvailableModel>(responseBody);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.ResultCode);
        Assert.Equal("success", json.Message);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private readonly ILogService _logService;
    private readonly string _sender;
    private readonly IApiService _apiService;
    private readonly EmsMessageProvider _messageProvider;
    private readonly AligoService _aligoService;
    #endregion
}