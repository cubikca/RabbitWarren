using System;
using System.Threading.Tasks;
using RabbitWarren.Messaging;

namespace RabbitWarren
{
    /// <summary>
    ///     An interface for processing incoming messages
    /// </summary>
    /// <remarks>
    ///     This interface is implemented by the two provided message handlers (default response processor and default mediatr
    ///     request processor).
    ///     It should also be implemented by any custom message handlers you write. You can register your custom handler by
    ///     assigning it to
    ///     consumerChannel.Consumer.Handler. You can do this to a running consumer as well.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public interface IRabbitMQMessageHandler
    {
        /// <summary>
        ///     An asynchronous method providing a custom message processor
        /// </summary>
        Func<RabbitMQChannel, IMessage, Task<Message>> ProcessMessage { get; set; }

        /// <summary>
        ///     The consumer that owns this handler
        /// </summary>
        RabbitMQConsumer Consumer { get; }

        /// <summary>
        ///     Handle the incoming message
        /// </summary>
        /// <param name="message">The incoming message</param>
        /// <param name="replyTo">A queue for publishing response messages</param>
        /// <returns>The result of processing the message: the original message for responses, the response for requests</returns>
        Task<Message> Handle(Message message, string replyTo);
    }
}