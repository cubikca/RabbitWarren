using System;
using System.Threading.Tasks;
using RabbitWarren.Messaging;

namespace RabbitWarren
{
    /// <summary>
    ///     A message handler that performs the action specified in <see cref="ProcessMessage" />
    /// </summary>
    /// <typeparam name="T">The response message type</typeparam>
    /// <remarks>
    ///     T does not have to be an instantiable type. It is frequently desirable to simply use <c>IMessage</c> for
    ///     <c>T</c> />
    /// </remarks>
    public class RabbitMQDefaultHandler : IRabbitMQMessageHandler
    {
        /// <summary>
        ///     Instantiate the default handler for the specified consumer
        /// </summary>
        /// <param name="consumer"></param>
        public RabbitMQDefaultHandler(RabbitMQConsumer consumer)
        {
            Consumer = consumer;
        }

        /// <summary>
        ///     An asynchronous method to process the incoming message
        /// </summary>
        /// <remarks>
        ///     The method should return the response message for the input message. See
        ///     <see cref="RabbitMQConsumerChannel{T}.DefaultHandler" /> for an example
        ///     of how to process incoming response messages.
        /// </remarks>
        public Func<RabbitMQChannel, IMessage, Task<Message>> ProcessMessage { get; set; }


        /// <summary>
        ///     The <c>RabbitMQConsumer&lt;T&gt;</c> owning this handler
        /// </summary>
        /// <remarks>
        ///     While this can be updated by application code, it should not be without ensuring that the previous consumer is
        ///     properly cleaned up.
        /// </remarks>
        public RabbitMQConsumer Consumer { get; set; }

        /// <summary>
        ///     Handle the incoming message by calling <see cref="ProcessMessage" />
        /// </summary>
        /// <param name="message">The incoming message to process</param>
        /// <param name="replyTo">A queue for publishing response messages</param>
        /// <returns>The result of processing the message</returns>
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
        public async Task<Message> Handle(Message message, string replyTo)
        {
            var response = await ProcessMessage(Consumer.Channel, message);
            return response;
        }
    }
}