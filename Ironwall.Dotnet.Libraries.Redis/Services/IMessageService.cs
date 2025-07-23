using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Redis.Models;
using System;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Redis.Services
{
    public interface IMessageService<T> : IService
    {
        T? Connect(IRedisSetupModel setupModel);
        Task<T?> ConnectAsync(IRedisSetupModel setupModel);
        Task PublishAsync(string channel, string msg);
        //event EventHandler<ChannelMessage> ChannelEventHandler;
        //event EventHandler<ChannelMessage> RedisSubscribeEvent;

        //동기식 수신 이벤트 핸들러
        event EventHandler<MessageArgsModel> RedisSubscribeEvent;
        //비동기식 수신 이벤트 핸들러
        event Func<MessageArgsModel, Task> RedisSubscribeEventAsync;

    }
}