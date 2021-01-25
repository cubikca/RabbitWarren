using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using RabbitWarren.Messaging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitWarren
{
    /// <summary>
    ///     A consumer for the specified message type
    /// </summary>
    /// <typeparam name="T">An IMessage type to consume</typeparam>
    public class RabbitMQConsumer : AsyncEventingBasicConsumer, IDisposable
    {
        private readonly IContainer _container;

        /// <summary>
        ///     Construct a consumer for the specified channel
        /// </summary>
        /// <param name="channel">The associated channel for this consumer</param>
        /// <param name="container">The AutoFac container for registering MediatR handlers</param>
        public RabbitMQConsumer(RabbitMQConsumerChannel channel, IContainer container) : base(channel.Model)
        {
            Channel = channel;
            _container = container;
            Received += RabbitMQConsumer_Received;
        }

        /// <summary>
        ///     The associated channel for this consumer
        /// </summary>
        public RabbitMQConsumerChannel Channel { get; set; }

        /// <summary>
        ///     The assigned consumer tag
        /// </summary>
        public string ConsumerTag { get; set; }

        /// <summary>
        ///     A handler for incoming messages
        /// </summary>
        public IRabbitMQMessageHandler Handler { get; set; }

        /// <summary>
        ///     Stop this consumer when it is disposed
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        private async Task RabbitMQConsumer_Received(object sender, BasicDeliverEventArgs @event)
        {
            lock (Model)
            {
                Model.BasicAck(@event.DeliveryTag, false);
            }

            var typeName = @event.BasicProperties.Type;
            if (typeName == null)
                return;
            var type = Type.GetType(typeName);
            // reply_to will be set if the incoming message is a request
            var replyTo = @event.BasicProperties.ReplyTo;
            if (replyTo != null)
                await ProcessRequest(@event.Body, type, replyTo);
            else
                await ProcessResponse(@event.Body, type);
        }

        private async Task<Message> ProcessRequest(ReadOnlyMemory<byte> body, Type type, string replyTo)
        {
            using (var ms = new MemoryStream(body.ToArray()))
            using (var reader = new JsonTextReader(new StreamReader(ms)))
            {
                var serializerSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All};
                var serializer = JsonSerializer.Create(serializerSettings);
                var message = (Message) serializer.Deserialize(reader, type);
                Message response;
                try
                {
                    response = await Handler.Handle(message, replyTo);
                }
                catch (Exception ex)
                {
                    response = Activator.CreateInstance<ErrorResult>();
                    if (response is Result result)
                    {
                        result.Error = ex.GetBaseException().Message;
                        result.Exception = ex;
                    }
                }

                var publishChannel = Channel.Connection.OpenPublishChannel(Channel.Exchange);
                await publishChannel.Publish(response, replyTo);
                return response;
            }
        }

        private Task ProcessResponse(ReadOnlyMemory<byte> body, Type type)
        {
            var jsonSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All};
            using (var ms = new MemoryStream(body.ToArray()))
            using (var reader = new JsonTextReader(new StreamReader(ms)))
            {
                var serializer = JsonSerializer.Create(jsonSettings);
                var response = (Message) serializer.Deserialize(reader, type);
                Handler.Handle(response, null);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Adds the default handler to this consumer
        /// </summary>
        /// <param name="handler">An asynchronous method to process incoming messages</param>
        /// <returns>The newly created default handler</returns>
        /// <remarks>
        ///     <para>
        ///         Incoming responses can be distinguished from incoming requests by the <c>replyTo</c> parameter. A request
        ///         will contain the name of the response queue, while a response will be null. Processing a response should
        ///         store the result somewhere for later retrieval and return the original message. Processing a request should
        ///         return
        ///         a response message to be sent back to the original requestor. Requests should be tracked on a per-connection
        ///         basis to allow the response message handler to correlate the response to the original message.
        ///     </para>
        ///     <para>
        ///         More advanced scenarios might include one-way and broadcast communications where the request and response
        ///         semantics don't apply. This handler currently deals exclusively with request-response scenarios though MediatR
        ///         does have facilities to support other messaging patterns and are considered for future development.
        ///     </para>
        /// </remarks>
        public IRabbitMQMessageHandler AddDefaultHandler(Func<RabbitMQChannel, IMessage, Task<Message>> handler = null)
        {
            if (handler != null)
                Handler = new RabbitMQDefaultHandler(this) {ProcessMessage = handler};
            else
                Handler = new RabbitMQDefaultHandler(this)
                {
                    ProcessMessage = (ch, m) =>
                    {
                        if (m is Message t && t is Result r)
                        {
                            if (r.CorrelationId == Guid.Empty)
                                throw new Exception("Cannot process response with no correlation id");
                            ch.Connection.Requests.TryRemove(r.CorrelationId, out _);
                            ch.Connection.Responses[r.CorrelationId].TrySetResult(m);
                            return Task.FromResult(t);
                        }

                        return Task.FromResult<Message>(default);
                    }
                };
            return Handler;
        }

        /// <summary>
        ///     Create a handler that processes messages using the MediatR handler located in the specified assembly
        /// </summary>
        /// <param name="handlerAssembly"></param>
        /// <returns></returns>
        /// <seealso cref="AddDefaultHandler(Func{RabbitMQChannel, IMessage, Task{T}})" />
        public IRabbitMQMessageHandler AddMediatRHandler(Assembly handlerAssembly)
        {
            var handler = new RabbitMQMediatRHandler(this, _container, handlerAssembly);
            Handler = handler;
            return Handler;
        }

        /// <summary>
        ///     Start this consumer
        /// </summary>
        /// <remarks>
        ///     Assigns a consumer tag and associates the consumer with the channel
        /// </remarks>
        /// <param name="exclusive">True if the underlying queue should be exclusive (only accessible from current channel)</param>
        /// <param name="autoDelete">True if the underlying queue should be deleted once the channel is closed</param>
        public void Start(bool exclusive = false, bool autoDelete = true)
        {
            if (ConsumerTag != null)
                Stop();
            if (ConsumerTag == null)
                lock (Model)
                {
                    Model = Channel.Connection.Connection.CreateModel();
                    Model.QueueDeclare(Channel.Queue, exclusive: exclusive, autoDelete: autoDelete);
                    if (Channel.Exchange != "")
                        Model.QueueBind(Channel.Queue, Channel.Exchange, Channel.Queue);
                    ConsumerTag = Model.BasicConsume(Channel.Queue, false, this);
                }
        }

        /// <summary>
        ///     Stop this consumer
        /// </summary>
        /// <remarks>
        ///     Cancels the association between this consumer and the channel and sets ConsumerTag to null
        /// </remarks>
        public void Stop()
        {
            if (ConsumerTag != null)
                lock (Model)
                {
                    Model.BasicCancel(ConsumerTag);
                }

            ConsumerTag = null;
        }
    }
}