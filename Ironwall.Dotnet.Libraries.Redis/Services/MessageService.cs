using StackExchange.Redis;
using System.Threading.Tasks;
using System.Threading;
using System;
using Ironwall.Dotnet.Libraries.Redis.Models;
using Autofac.Core.Registration;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Redis.Services
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/2/2024 2:26:07 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public abstract class MessageService<T> : IMessageService<T>
    {
        #region - Ctors -
        protected MessageService()
        {
        }
        protected MessageService(ILogService log) => _log = log;
        #endregion
        #region - Implementation of Interface -
        public async Task ExecuteAsync(CancellationToken token = default)
        {
            await Task.Run(delegate { RegisterSubscribers(); }, token);
        }

        public async Task StopAsync(CancellationToken token = default)
        {
            await Task.Run(delegate { UnegisterSubscribers(); }, token);
        }

        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        /// <summary>
        /// RegisterSubscribers는 이벤트를 등록하는 로직을 포함하고 있는 메소드
        /// </summary>
        protected virtual void RegisterSubscribers()
        {
            try
            {
                // RedisChannel with Pattern
                _patternChannel = RedisChannel.Pattern($"{_channelName}*");
                _log?.Info($"A subscription was made to the patterned {_channelName}*.");
                Subscriber?.Subscribe(_patternChannel, CommandFlags.PreferMaster ).OnMessage(async channelMessage =>
                {

                    // RedisSubscribeEvent?.Invoke(this, new MessageArgsModel(channelMessage.Channel, channelMessage.SubscriptionChannel, @channelMessage.Model));

                    /// 해당 코드는 OnRedisSubscribeEventAsync라는 비동기 메서드를 호출하고 있으며, 
                    /// 이 메서드는 MessageArgsModel 객체를 인자로 받습니다. 
                    /// await 키워드는 이 메서드가 비동기로 실행되며, 메서드의 실행이 완료될 때까지
                    /// 현재의 실행 흐름을 일시적으로 멈추고 기다리는 것을 나타냅니다. 
                    /// 
                    /// ##자세한 설명은 다음과 같습니다:
                    /// 
                    /// 1. 비동기 메서드 호출: OnRedisSubscribeEventAsync는 비동기 메서드로,
                    /// await 키워드와 함께 사용되어 메서드의 실행이 완료될 때까지 현재 스레드의 실행을
                    /// 중지시킵니다. 이렇게 하면 메서드 내의 비동기 작업이 완료될 때까지 다음 코드로 
                    /// 넘어가지 않습니다.
                    /// 
                    /// 2. MessageArgsModel 객체 생성: new MessageArgsModel(...)는 MessageArgsModel 클래스의
                    /// 인스턴스를 생성합니다.이 객체는 channelMessage.Channel, channelMessage.SubscriptionChannel,
                    /// channelMessage.Message와 같은 정보를 포함합니다. 
                    /// 이 정보들은 Redis 채널에서 수신된 메시지와 관련된 데이터일 것입니다.
                    /// 
                    /// 3. 메서드 인자: 생성된 MessageArgsModel 객체는 OnRedisSubscribeEventAsync 메서드의 인자로
                    /// 전달됩니다.이 객체를 통해 메서드는 수신된 메시지에 대한 정보를 처리할 수 있습니다.
                    /// 
                    /// 4. 비동기 처리: await 키워드의 사용은 해당 호출이 비동기적으로 처리됨을 의미합니다. 
                    /// 즉, OnRedisSubscribeEventAsync 메서드 내에서 수행되는 비동기 작업(예: 네트워크 I/ O, 
                    /// 데이터베이스 쿼리 등)이 완료될 때까지 현재 메서드의 진행이 일시 중단됩니다. 
                    /// 이는 애플리케이션의 응답성을 향상시키는 데 도움이 됩니다.
                    /// 
                    /// 5. 이벤트 처리 로직: OnRedisSubscribeEventAsync 메서드 내에서는 RedisSubscribeEventAsync 
                    /// 이벤트에 등록된 모든 비동기 이벤트 핸들러를 호출하고, 각 핸들러가 처리하는 로직을 실행할
                    /// 것입니다. 이러한 비동기 이벤트 처리는 효율적인 리소스 사용과 더 나은 성능을 위해 사용됩니다.

                    await OnRedisSubscribeEventAsync(new MessageArgsModel(channelMessage.Channel!, channelMessage.SubscriptionChannel!, channelMessage.Message!));

                    _log?.Info("Received raw message: " + channelMessage.Message);
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 이 코드 조각은 OnRedisSubscribeEventAsync라는 비동기 메서드의 구현을 보여주며, 
        /// MessageArgsModel 타입의 인자를 받아 RedisSubscribeEventAsync 이벤트에 등록된 모든 핸들러를 
        /// 비동기적으로 실행합니다. 자세한 설명은 다음과 같습니다:
        /// 
        /// 메서드 선언:
        /// 1. protected virtual async Task OnRedisSubscribeEventAsync(MessageArgsModel e)는 protected와 
        /// virtual 키워드를 사용하여 상속받은 클래스에서 재정의할 수 있는 비동기 메서드를 선언합니다.
        /// 
        /// 2. async 키워드는 이 메서드 내에서 비동기 연산을 수행한다는 것을 나타냅니다.
        /// Task 반환 타입은 메서드가 비동기 작업을 완료할 때까지 대기할 수 있음을 의미합니다.
        /// MessageArgsModel e는 이벤트 데이터를 담고 있는 인자입니다.
        /// 
        /// 3. 이벤트 핸들러 실행 확인:
        /// 3-1. if (RedisSubscribeEventAsync != null)은 RedisSubscribeEventAsync 이벤트에 적어도 하나 이상의 
        /// 핸들러가 등록되어 있는지 확인합니다.
        /// 
        /// 4. 이벤트 핸들러 실행:
        /// 4-1. foreach (var handler in RedisSubscribeEventAsync.GetInvocationList())는 
        /// RedisSubscribeEventAsync에 등록된 모든 이벤트 핸들러를 순회합니다.
        /// 4-2. GetInvocationList() 메서드는 이벤트에 연결된 모든 델리게이트를 배열로 반환합니다.
        /// 
        /// 5. 각 핸들러에 대한 비동기 호출:
        /// await ((Func<MessageArgsModel, Task>) handler)(e); 는 각 핸들러를 비동기적으로 호출합니다.
        /// await 키워드는 각 핸들러의 작업이 완료될 때까지 현재 메서드의 실행을 중지시킵니다.
        /// Func<MessageArgsModel, Task> 는 이벤트 핸들러가 MessageArgsModel을 인자로 받고 Task를 반환하는 
        /// 델리게이트 타입을 나타냅니다.
        /// handler(e)는 실제 이벤트 핸들러를 호출하며, 여기서 e는 이벤트 데이터를 나타냅니다.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        protected virtual async Task OnRedisSubscribeEventAsync(MessageArgsModel e)
        {
            if (RedisSubscribeEventAsync != null)
            {
                foreach (var handler in RedisSubscribeEventAsync.GetInvocationList())
                {
                    await ((Func<MessageArgsModel, Task>)handler)(e);
                }
            }
        }

        /// <summary>
        /// 프로그램 종료 시, 혹은 서비스 이용 종료 시, Redis 기능을 Dispose 하고 Redis의 소켓을 
        /// 닫는 종료 메커니즘을 포함하고 있는 메소드이다.
        /// </summary>
        protected void UnegisterSubscribers()
        {
            try
            {
                if (Subscriber == null) return;

                if (Subscriber.IsConnected() && Subscriber.Multiplexer.IsConnected)
                {
                    Subscriber?.UnsubscribeAllAsync();
                    Subscriber?.Multiplexer?.Close();
                    Subscriber?.Multiplexer?.Dispose();
                }
            }
            catch (ComponentNotRegisteredException ex)
            {
                _log?.Error(ex.Message);
                throw;
            }
            catch (ObjectDisposedException ex)
            {
                _log?.Error(ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 연결 로직에 관한 상세한 메소드를 자식 클래스에서 override 하여 구현한다.
        /// </summary>
        /// <param name="setupModel"></param>
        /// <returns></returns>
        public abstract T? Connect(IRedisSetupModel setupModel);

        /// <summary>
        /// 연결 로직에 관한 메소드 이며, Async 타입으로 Task 기반의 메소드로 구현한다. 
        /// 이 메소드 또한 자식 클래스에서 override 하여 구현한다.
        /// </summary>
        /// <param name="setupModel"></param>
        /// <returns></returns>
        public abstract Task<T?> ConnectAsync(IRedisSetupModel setupModel);

        /// <summary>
        /// Redis 채널로 메시지를 전송하는 메소드로 Task 기반의 메소드로 구현한다.
        /// 이 메소드 또한 자식 클래스에서 override 하여 구현한다.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public abstract Task PublishAsync(string channel, string msg);
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        protected ISubscriber? Subscriber { get; set; }
        #endregion
        #region - Attributes -
        public event EventHandler<MessageArgsModel>? RedisSubscribeEvent;
        
        // 새로운 비동기 이벤트
        /// <summary>
        /// event Func<MessageArgsModel, Task> RedisSubscribeEventAsync; 
        /// 이 코드는 C#에서 비동기 이벤트를 선언하는 방법을 나타냅니다. 
        /// 여기서 Func<MessageArgsModel, Task>는 이벤트 핸들러의 타입을 나타내며, 
        /// MessageArgsModel 타입의 인자를 받고 Task를 반환하는 함수(delegate)를 의미합니다. 
        /// 이 이벤트는 비동기적인 작업을 수행하는 이벤트 핸들러와 함께 사용됩니다.
        /// 
        /// 자세한 설명은 다음과 같습니다:
        /// Func<T, TResult> 델리게이트: Func<T, TResult>는 입력 타입 T를 받고 결과 타입 TResult를 반환하는 델리게이트 타입입니다. 
        /// 여기서 T는 MessageArgsModel, TResult는 Task입니다.
        /// MessageArgsModel: 이벤트 핸들러가 받는 인자의 타입입니다. 이 타입은 이벤트가 발생할 때 필요한 데이터를 포함합니다. 
        /// 예를 들어, 메시지 내용, 채널 정보 등이 이 타입에 포함될 수 있습니다.
        /// Task 반환 타입: 이벤트 핸들러가 Task를 반환한다는 것은 해당 핸들러가 비동기 작업을 수행할 수 있음을 의미합니다. 
        /// 즉, 이벤트 핸들러 내에서 await 키워드를 사용하여 비동기적으로 실행되는 코드를 포함할 수 있습니다.
        /// 이벤트 선언: event 키워드는 이 코드가 이벤트를 선언한다는 것을 나타냅니다. 
        /// 이를 통해 다른 클래스나 메서드에서 이 이벤트에 핸들러를 등록하거나 해제할 수 있습니다.
        /// 비동기 이벤트 처리: 이 이벤트는 메시지가 수신되었을 때 비동기적으로 처리할 작업을 등록하기 위해 사용됩니다. 
        /// 예를 들어, 메시지를 처리하는 로직이 데이터베이스 접근이나 파일 입출력과 같은 비동기 작업을 포함할 수 있습니다.
        /// </summary>
        public event Func<MessageArgsModel, Task>? RedisSubscribeEventAsync;
        private RedisChannel _patternChannel;
        protected string _channelName = string.Empty;
        protected ILogService? _log;
        #endregion
    }
}
