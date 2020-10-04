using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using RabbitWarren.Messaging;

namespace RabbitWarren
{
    /// <summary>
    ///     A channel for consuming messages
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RabbitMQConsumerChannel : RabbitMQChannel
    {
        private readonly ILifetimeScope _container;

        /// <summary>
        ///     The default response processing handler
        /// </summary>
        public Func<RabbitMQChannel, IMessage, Task<Message>> DefaultHandler = (ch, m) =>
        {
            if (m is Message t && t is Result r)
            {
                var correlationId = r.CorrelationId;
                ch.Connection.Requests.TryRemove(correlationId, out _);
                ch.Connection.Responses[correlationId].TrySetResult(m);
                return Task.FromResult(t);
            }

            return Task.FromResult<Message>(default);
        };

        /// <summary>
        ///     Creates a channel for consuming messages. You should not call this directly, but rather
        ///     <see cref="RabbitMQConnection.OpenConsumerChannel{T}(string, string, bool, bool)" />
        /// </summary>
        /// <param name="connection">The connection associated with this channel</param>
        /// <param name="container">The AutoFac container for storing MediatR registrations</param>
        /// <param name="exchange">The associated RabbitMQ exchange</param>
        /// <param name="queue">The associated RabbitMQ queue</param>
        /// <param name="autoDelete">True if the underlying queue should be deleted when the channel is closed</param>
        /// <param name="exclusive">True if access to the underlying queue should be restricted to the channel</param>
        public RabbitMQConsumerChannel(RabbitMQConnection connection, ILifetimeScope container, string exchange, string queue, bool autoDelete = false,
            bool exclusive = true) : base(connection, exchange)
        {
            _container = container;
            Queue = queue;
            AutoDelete = autoDelete;
            Exclusive = exclusive;
            if (Consumers == null)
                Consumers = new List<RabbitMQConsumer>();
            Model = Connection.Connection.CreateModel();
            if (!string.IsNullOrEmpty(exchange))
                Model.ExchangeDeclare(exchange, "direct", true, false, null);
        }

        /// <summary>
        ///     Track all consumers on this channel
        /// </summary>
        public IList<RabbitMQConsumer> Consumers { get; set; }

        /// <summary>
        ///     The queue this channel is associated with
        /// </summary>
        public string Queue { get; }

        /// <summary>
        ///     Register a default consumer using the optionally provided message processing method
        /// </summary>
        /// <param name="processMessage">
        ///     A method accepting this channel and the incoming message that will process the message and
        ///     return to application
        /// </param>
        /// <returns>The newly created consumer</returns>
        public RabbitMQConsumer RegisterDefaultConsumer(Func<RabbitMQChannel, IMessage, Task<Message>> processMessage = null)
        {
            var consumer = new RabbitMQConsumer(this, _container);
            consumer.AddDefaultHandler(processMessage);
            lock (Consumers)
            {
                Consumers.Add(consumer);
            }

            return consumer;
        }

        /// <summary>
        ///     Register a default consumer using a MediatR to find the correct message processing method
        /// </summary>
        /// <param name="handlerAssembly">The assembly containing MediatR handlers</param>
        /// <returns>The newly created consumer</returns>
        public RabbitMQConsumer RegisterMediatRConsumer(Assembly handlerAssembly)
        {
            var consumer = new RabbitMQConsumer(this, _container);
            consumer.AddMediatRHandler(handlerAssembly);
            lock (Consumers)
            {
                Consumers.Add(consumer);
            }

            return consumer;
        }
    }
}