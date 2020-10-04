using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using RabbitWarren.Messaging;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace RabbitWarren
{
    /// <summary>
    ///     A channel for publishing messages to RabbitMQ
    /// </summary>
    public class RabbitMQPublishChannel : RabbitMQChannel
    {
        /// <summary>
        ///     Creates a new channel for publishing. You should not call this constructor directly, but rather call
        ///     <see cref="RabbitMQConnection.OpenPublishChannel(string)" />
        /// </summary>
        /// <param name="connection">The connection associated with this channel</param>
        /// <param name="exchange">The RabbitMQ exchange associated with this channel</param>
        public RabbitMQPublishChannel(RabbitMQConnection connection, string exchange) : base(connection, exchange)
        {
            Model = Connection.Connection.CreateModel();
        }

        private Task<ReadOnlyMemory<byte>> SerializeBody(ISerializable message)
        {
            var jsonSettings = new JsonSerializerSettings {PreserveReferencesHandling = PreserveReferencesHandling.Objects};
            using (var ms = new MemoryStream())
            using (var writer = new JsonTextWriter(new StreamWriter(ms)))
            {
                var serializer = JsonSerializer.Create(jsonSettings);
                serializer.Serialize(writer, message);
                writer.Flush();
                var rom = new ReadOnlyMemory<byte>(ms.ToArray());
                return Task.FromResult(rom);
            }
        }

        /// <summary>
        ///     Publish a message to RabbitMQ
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <param name="queue">(optional) a queue for publishing if different than the channel's underlying queue</param>
        /// <param name="replyTo">(optional) a queue for publishing responses if a response is expected</param>
        /// <returns>a Task to be awaited (this method runs synchronously however)</returns>
        public Task Publish(IMessage message, string queue = null, string replyTo = null)
        {
            if (message.Id == Guid.Empty)
                message.Id = Guid.NewGuid();
            // we want to publish using the default exchange so that we can publish to exclusive response queues
            var address = new PublicationAddress(ExchangeType.Direct, "", queue);
            var type = message.GetType().AssemblyQualifiedName;
            lock (Model)
            {
                var properties = Model.CreateBasicProperties();
                properties.CorrelationId = (message is IResult result ? result.CorrelationId : message.Id).ToString();
                properties.Type = type;
                properties.ReplyTo = replyTo;
                Model.BasicPublish(address, properties, SerializeBody(message).GetAwaiter().GetResult());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Invoke a request via RabbitMQ
        /// </summary>
        /// <param name="message">The request message to send</param>
        /// <param name="queue">The RabbitMQ queue for publishing request messages</param>
        /// <param name="replyTo">The RabbitMQ queue for publishing response messages</param>
        /// <returns>A task returning the response to the provided request</returns>
        public virtual async Task<IMessage> Request(IMessage message, string queue, string replyTo)
        {
            if (message.Id == Guid.Empty)
                message.Id = Guid.NewGuid();
            Connection.Requests.TryAdd(message.Id, message);
            Connection.Responses.TryAdd(message.Id, new TaskCompletionSource<IMessage>());
            var consumerChannel = Connection.OpenConsumerChannel(Exchange, replyTo, exclusive: false, autoDelete: true);
            if (!consumerChannel.Model.IsOpen)
            {
                var consumer = consumerChannel.RegisterDefaultConsumer();
                consumer.Start(consumerChannel.Exclusive, consumerChannel.AutoDelete);
            }

            await Publish(message, queue, replyTo);
            var result = await Connection.Responses[message.Id].Task;
            return result;
        }
    }
}