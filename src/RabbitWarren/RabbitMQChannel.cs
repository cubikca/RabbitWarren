using System;
using RabbitMQ.Client;

namespace RabbitWarren
{
    /// <summary>
    ///     A base class for channels
    /// </summary>
    /// <remarks>
    ///     This library provides two different types of channels: publish ("write") channels and consumer ("read") channels
    ///     This class contains common logic for both channel types
    /// </remarks>
    public abstract class RabbitMQChannel : IDisposable
    {
        /// <summary>
        ///     Create a new channel
        /// </summary>
        /// <param name="connection">The connection used by this channel</param>
        /// <param name="exchange">The RabbitMQ exchange to associate with</param>
        public RabbitMQChannel(RabbitMQConnection connection, string exchange)
        {
            Id = Guid.NewGuid();
            Connection = connection;
            Exchange = exchange;
        }

        /// <summary>
        ///     The connection that owns this channel
        /// </summary>
        public RabbitMQConnection Connection { get; }

        /// <summary>
        ///     A unique identifier for this channel
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        ///     The exchange associated with this channel
        /// </summary>
        public string Exchange { get; }

        /// <summary>
        ///     The low-level model exposed from the RabbitMQ.Client library
        /// </summary>
        public IModel Model { get; protected set; }

        /// <summary>
        ///     Whether or not to delete the underlying queue when closing this channel
        /// </summary>
        public bool AutoDelete { get; set; }

        /// <summary>
        ///     Whether or not to restrict access to the underlying queue to this channel only
        /// </summary>
        public bool Exclusive { get; set; }

        /// <summary>
        ///     Close the channel when it is disposed
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        ///     Close the channel
        /// </summary>
        public void Close()
        {
            if (Model != null)
            {
                Model.Close();
                Model = null;
                lock (Connection.Channels)
                {
                    Connection.Channels.Remove(this);
                }
            }
        }
    }
}