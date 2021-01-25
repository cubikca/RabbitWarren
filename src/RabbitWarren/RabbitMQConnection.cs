using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using RabbitWarren.Messaging;
using RabbitMQ.Client;

namespace RabbitWarren
{
    /// <summary>
    ///     The fundamental class for communicating with RabbitMQ
    /// </summary>
    /// <remarks>
    ///     To communicate with RabbitMQ you should start with a connection
    ///     Connections are obtained from a <see cref="RabbitMQConnectionFactory" />
    /// </remarks>
    public class RabbitMQConnection : IDisposable
    {
        private readonly IContainer _container;

        /// <summary>
        ///     Create a connection to RabbitMQ.
        ///     You should not call this method directly, but rather <see cref="RabbitMQConnectionFactory.Create" />
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="container"></param>
        public RabbitMQConnection(IConnection connection, IContainer container)
        {
            Connection = connection;
            // IMPORTANT: you need to lock this list before using it
            // ConcurrentBag does not have Remove() method
            Channels = new List<RabbitMQChannel>();
            Requests = new ConcurrentDictionary<Guid, IMessage>();
            Responses = new ConcurrentDictionary<Guid, TaskCompletionSource<IMessage>>();
            _container = container;
        }

        /// <summary>
        ///     The underlying connection interface from RabbitMQ.Client
        /// </summary>
        public IConnection Connection { get; }

        /// <summary>
        ///     Track all currently open channels
        /// </summary>
        public IList<RabbitMQChannel> Channels { get; }

        /// <summary>
        ///     Track all currently processing requests
        /// </summary>
        public ConcurrentDictionary<Guid, IMessage> Requests { get; }

        /// <summary>
        ///     Track all expected responses
        /// </summary>
        public ConcurrentDictionary<Guid, TaskCompletionSource<IMessage>> Responses { get; }

        /// <summary>
        ///     Disconnect from RabbitMQ when disposed
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        ///     Creates a channel for the purpose of consuming messages
        /// </summary>
        /// <typeparam name="T">The type of message to consume</typeparam>
        /// <param name="exchange">The associated RabbitMQ exchange</param>
        /// <param name="queue">The associated RabbitMQ queue</param>
        /// <param name="autoDelete">True if the underlying queue should be deleted when the channel is closed</param>
        /// <param name="exclusive">True if the underlying queue should only be accessible from the new channel</param>
        /// <returns>The opened consumer channel</returns>
        /// <remarks>
        ///     This method actually performs some optimization by realizing that we can have a single channel per RabbitMQ
        ///     endpoint.
        ///     Instead of opening multiple channels to the same queue (or multiple publish channels) we can reuse the existing
        ///     channel.
        ///     This appears to be thread-safe in my testing. Opening and closing channels adds considerable overhead, so resuing
        ///     the
        ///     existing channel adds a considerable performance improvement.
        /// </remarks>
        public RabbitMQConsumerChannel OpenConsumerChannel(string exchange, string queue, bool autoDelete = false, bool exclusive = true)
        {
            // we do not want to create a new channel unless the requested channel does not exist
            var channel = Channels
                .SingleOrDefault(c =>
                    c.AutoDelete == autoDelete &&
                    c.Exclusive == exclusive &&
                    c.GetType().IsAssignableFrom(typeof(RabbitMQConsumerChannel))
                );
            if (channel == null)
            {
                channel = new RabbitMQConsumerChannel(this, _container, exchange, queue, autoDelete, exclusive);
                Channels.Add(channel);
            }

            return (RabbitMQConsumerChannel) channel;
        }

        /// <summary>
        ///     Opens a channel for publishing messages and requests
        /// </summary>
        /// <param name="exchange">The associated RabbitMQ exchange</param>
        /// <returns>The opened publish channel</returns>
        /// <seealso>
        ///     See the remarks in <see cref="OpenConsumerChannel{T}(string, string, bool, bool)" />
        /// </seealso>
        public RabbitMQPublishChannel OpenPublishChannel(string exchange)
        {
            var channel = Channels
                .SingleOrDefault(c =>
                    c.Exchange == exchange &&
                    c.GetType().IsAssignableFrom(typeof(RabbitMQPublishChannel))
                );
            if (channel == null)
            {
                channel = new RabbitMQPublishChannel(this, exchange);
                Channels.Add(channel);
            }

            return (RabbitMQPublishChannel) channel;
        }

        /// <summary>
        ///     Disconnect from RabbitMQ
        /// </summary>
        public void Close()
        {
            foreach (var channel in Channels.ToList())
                channel.Close();
            Connection.Close();
        }
    }
}