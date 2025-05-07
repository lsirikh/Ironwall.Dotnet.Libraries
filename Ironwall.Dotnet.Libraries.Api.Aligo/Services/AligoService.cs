using Dotnet.Gym.Message.Contacts;
using Ironwall.Dotnet.Libraries.Api.Aligo.Providers;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Windows.Interop;
using Dotnet.Gym.Message.Enums;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 6:19:42 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class AligoService : IAligoService
{
    #region - Ctors -
    public AligoService(ILogService log
                        , IApiService apiService
                        , EmsMessageProvider messageProvider
                        )
    {
        _log = log;
        _apiService = apiService;
        _messageProvider = messageProvider;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 🔹 Aligo 단일 문자 전송 API (SMS / LMS / MMS)
    /// </summary>
    public async Task<HttpResponseMessage?> SendEmsMessageAsync(IEmsMessageModel emsMessage, bool isReserve = false, bool testMode = false)
    {
        var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
                { new StringContent( ParseOnlyNumbers(_apiService.Phone)), "sender" },
                { new StringContent(emsMessage.Message ?? ""), "msg" },
                { new StringContent(ParseOnlyNumbers(emsMessage.Receiver)), "receiver" },
                { new StringContent(emsMessage.Destination ?? ""), "destination" },
                { new StringContent(emsMessage.MsgType.ToString()), "msg_type" },
                { new StringContent(testMode ? "Y" : "N"), "testmode_yn" },
            };

        switch (emsMessage.MsgType)
        {
            case EnumMsgType.SMS:
                {

                }
                break;
            case EnumMsgType.LMS:
            case EnumMsgType.MMS:
                {
                    formData.Add(new StringContent(emsMessage.Title ?? ""), "title");
                }
                break;
            default:
                break;
        }

        // scheduled transfer
        if (isReserve)
        {
            formData.Add(new StringContent(emsMessage.Reservation.ToString("yyyyMMdd")), "rdate");
            formData.Add(new StringContent(emsMessage.Reservation.ToString("HHmm")), "rtime");
        }

        // 이미지 첨부 (최대 3개)
        if (emsMessage.AttachedImages != null)
        {
            for (int i = 0; i < emsMessage.AttachedImages.Count && i < 3; i++)
            {
                var image = emsMessage.AttachedImages[i];
                var imageContent = new ByteArrayContent(Convert.FromBase64String(image.Base64Image));
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(image.ContentType);
                formData.Add(imageContent, $"image{i + 1}", image.FileName);
            }
        }

        var ret = await GetFormDataDebugInfoAsync(formData);
        _log.Info(ret.ToString());

        return await _apiService.PostFormDataRequestAsync("send/", formData);
    }

    /// <summary>
    /// Aligo 대량 문자 전송 API (SMS / LMS / MMS)
    /// </summary>
    public async Task<HttpResponseMessage?> SendMassEmsMessageAsync(List<IEmsMessageModel> messages, bool isReserve = false, bool testMode = false)
    {
        try
        {
            if (messages == null || messages.Count == 0) throw new Exception($"{typeof(List<IEmsMessageModel>)} {nameof(messages)} was invalid Parameter...");

            var validMessages = messages
            .Where(msg => !string.IsNullOrWhiteSpace(msg.Receiver))
            .ToList();

            if (!validMessages.Any())
                throw new ArgumentException("No valid receivers found.");

            // receiver & destination 정보 처리
            var receivers = string.Join(",",
                validMessages.Select(msg => ParseOnlyNumbers(msg.Receiver)));

            var destinations = string.Join(",",
                validMessages.Select(msg => msg.Destination));

            var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
                { new StringContent(ParseOnlyNumbers(_apiService.Phone)), "sender" },
                { new StringContent(messages.First().Message ?? ""), "msg" },
                { new StringContent(receivers), "receiver" },
                { new StringContent(destinations ?? ""), "destination" },
                { new StringContent(messages.First().MsgType.ToString()), "msg_type" }, // MMS 필수
                { new StringContent(testMode ? "Y" : "N"), "testmode_yn" },
            };

            switch (messages.First().MsgType)
            {
                case EnumMsgType.SMS:
                    {

                    }
                    break;
                case EnumMsgType.LMS:
                case EnumMsgType.MMS:
                    {
                        formData.Add(new StringContent(messages.First().Title ?? ""), "title");
                    }
                    break;
                default:
                    break;
            }

            // scheduled transfer
            if (isReserve)
            {
                formData.Add(new StringContent(messages.First().Reservation.ToString("yyyyMMdd")), "rdate");
                formData.Add(new StringContent(messages.First().Reservation.ToString("HHmm")), "rtime");
            }


            //int count = 1;
            //foreach (var msg in messages)
            //{
            //    formData.Add(new StringContent(msg.Receiver), $"rec_{count}");
            //    formData.Add(new StringContent(msg.Message ?? ""), $"msg_{count}");
            //    count++;

            //}

            

            // 수신자 이름을 포함하여 보낼 경우 (destination), ReceiverName 필드 필요
            //var destinations = string.Join(",",
            //    validMessages.Select(msg => $"{ParseOnlyNumbers(msg.Receiver)}|{msg.UserId}"));
            //formData.Add(new StringContent(destinations), "destination");


            // 이미지 첨부 (최대 3개)
            var attachedImages = messages.FirstOrDefault()?.AttachedImages;
            if (attachedImages != null)
            {
                for (int i = 0; i < attachedImages.Count && i < 3; i++)
                {
                    var image = attachedImages[i];
                    var imageContent = new ByteArrayContent(Convert.FromBase64String(image.Base64Image));
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(image.ContentType);
                    formData.Add(imageContent, $"image{i + 1}", image.FileName);
                }
            }

            if (messages.Count > 500) throw new Exception("수신자는 500명을 넘을 수 없습니다.");

            var ret = await GetFormDataDebugInfoAsync(formData);
            _log.Info(ret.ToString());

            return await _apiService.PostFormDataRequestAsync("send/", formData);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AligoService)}] SendMassEmsMessageAsync 동작 실패 : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 최근 전송 목록 조회
    /// </summary>
    public async Task<HttpResponseMessage?> GetSentMessageListAsync(int page = 1, int pageSize = 10, DateTime startDate = default, int limitDay = 7)
    {
        try
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
                { new StringContent(page.ToString()), "page" },
                { new StringContent(pageSize.ToString()), "page_size" },
                { new StringContent(startDate.ToString("yyyyMMdd")), "start_date" },
                { new StringContent(limitDay.ToString()), "limit_day" },
            };

            return await _apiService.PostFormDataRequestAsync("list/", formData);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AligoService)}] GetSentMessageListAsync 동작 실패 : {ex.Message}");
            return null;
        }

    }

    /// <summary>
    /// 최근 전송된 메시지의 상세 내용(복수 문자일 수 있어서 Page구분이 있는듯)
    /// </summary>
    /// <param name="mid"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage?> GetSentSpecificMessageListAsync(int mid = 1, int page = 1, int pageSize = 10)
    {
        try
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
                { new StringContent(mid.ToString()), "mid" },
                { new StringContent(page.ToString()), "page" },
                { new StringContent(pageSize.ToString()), "page_size" },
            };

            return await _apiService.PostFormDataRequestAsync("sms_list/", formData);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AligoService)}] GetSentSpecificMessageListAsync 동작 실패 : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 보유한 잔여포인트로 발송가능한 잔여건수를 문자구분(유형)별로 조회하실 수 있습니다.
    /// SMS, LMS, MMS로 발송시 가능한 잔여건수이며 남은 충전금을 문자유형별로 보냈을 경우 가능한 잔여건입니다.
    /// 예를들어 SMS_CNT : 11 , LMS_CNT : 4 인 경우 단문전송시 11건이 가능하고, 장문으로 전송시 4건이 가능합니다.
    /// </summary>
    /// <returns></returns>
    public async Task<HttpResponseMessage?> GetAvailableEmsServiceAsync()
    {
        try
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
            };

            return await _apiService.PostFormDataRequestAsync("remain/", formData);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AligoService)}] GetAvailableEmsServce 동작 실패 : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// send, send_mass API를 통해 예약한 내역을 전송취소할 수 있습니다.
    /// 예약취소는 발송전 5분이전의 문자만 가능합니다.
    /// </summary>
    /// <param name="mid"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage?> CancelReserveMsgAsync(int mid = 1)
    {
        try
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(_apiService.UserId), "user_id" },
                { new StringContent(_apiService.ApiKey), "key" },
                { new StringContent(mid.ToString()), "mid" },
            };

            return await _apiService.PostFormDataRequestAsync("cancel/", formData);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AligoService)}] CancelReserveAsync 동작 실패 : {ex.Message}");
            return null;
        }
    }

    private async Task<string> GetFormDataDebugInfoAsync(MultipartFormDataContent formData)
    {
        var sb = new StringBuilder();

        sb.AppendLine("--------");
        foreach (var content in formData)
        {
            sb.Append($"{content.Headers.ContentDisposition?.Name}=");

            if (content is StringContent stringContent)
            {
                var value = await stringContent.ReadAsStringAsync();
                sb.AppendLine($"{value}");
            }
            else if (content is ByteArrayContent byteContent)
            {
                var fileName = content.Headers.ContentDisposition?.FileName;
                var contentType = content.Headers.ContentType;
                var byteLength = (await byteContent.ReadAsByteArrayAsync()).Length;

                sb.AppendLine($"FileName: {fileName}");
                sb.AppendLine($"ContentType: {contentType}");
                sb.AppendLine($"ByteLength: {byteLength}");
            }

        }
        sb.AppendLine("--------");

        return sb.ToString();
    }

    private string ParseOnlyNumbers(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private readonly ILogService _log;
    private readonly IApiService _apiService;
    private readonly EmsMessageProvider _messageProvider;
    #endregion
}