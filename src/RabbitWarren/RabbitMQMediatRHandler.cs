using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using RabbitWarren.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace RabbitWarren
{
    /// <summary>
    ///     A message handler that locates the correct handler using <c>MediatR</c>
    /// </summary>
    /// <typeparam name="T">The response message type</typeparam>
    /// <remarks>
    ///     <para>
    ///         Use this handler to replace logic of the type "<c>if (message is Type1) ProcessType1((Type1) message)</c>"
    ///         MediatR automates this process using reflection and dependency injection to match an incoming message type
    ///         to its corresponding handler in the provided assembly. Please have a look at the MediatR project site
    ///         for details on the usage of MediatR in your application.
    ///     </para>
    ///     <para>
    ///         <c>ICommand&lt;T&gt;</c> implements <c>IRequest&lt;T&gt;</c>. This allows MediatR to infer the handler by
    ///         matching the request and response types.
    ///     </para>
    /// </remarks>
    public class RabbitMQMediatRHandler : IRabbitMQMessageHandler
    {
        private readonly RabbitMQConnection _connection;
        private readonly IContainer _container;
        private readonly Assembly _handlerAssembly;

        /// <summary>
        ///     Construct a <c>RabbitMQMediatRHandler</c> for the specified consumer
        /// </summary>
        /// <param name="consumer">The consumer who owns this handler</param>
        /// <param name="container">The AutoFac container for registering MediatR handlers</param>
        /// <param name="handlerAssembly">The assembly containing MediatR handlers</param>
        public RabbitMQMediatRHandler(RabbitMQConsumer consumer, IContainer container, Assembly handlerAssembly)
        {
            _connection = consumer.Channel.Connection;
            Consumer = consumer;
            _container = container;
            _handlerAssembly = handlerAssembly;
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
        public RabbitMQConsumer Consumer { get; }

        /// <summary>
        ///     Handle the incoming message by invoking the correct MediatR <c>Send&lt;T&gt;</c> method
        /// </summary>
        /// <param name="message">The incoming message to process</param>
        /// <param name="replyTo">A queue for publishing response messages, null if no response expected</param>
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
        ///         does have facilities to support other messaging patterns.
        ///     </para>
        /// </remarks>
        public async Task<Message> Handle(Message message, string replyTo)
        {
            using (var scope = _container.BeginLifetimeScope(builder =>
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddMediatR(_handlerAssembly);
                builder.Populate(serviceCollection);
            }))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                Type resultType = null;
                if (message is ICommand command)
                {
                    var intf = command.GetType().GetInterfaces().SingleOrDefault(i => i.IsGenericType && i.IsAssignableTo<ICommand>());
                    if (intf != null)
                        resultType = intf.GetGenericArguments()[0];
                    else
                        resultType = typeof(Result);
                }

                if (message is IQuery)
                {
                    var genericType = message.GetType()
                        .GetInterfaces()
                        .Single(@if =>
                            @if.IsGenericType && @if.Name.StartsWith("IQuery")); //e.g. IQuery<TResult, TModel>
                    var typeArguments = genericType.GetGenericArguments();
                    resultType = typeArguments[0];
                }

                if (resultType == null)
                    throw new Exception("Could not determine response type");
                var mediator = scope.Resolve<IMediator>();
                var method = mediator.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).Single(m => m.Name == "Send" && m.IsGenericMethod);
                var concrete = method.MakeGenericMethod(resultType);
                // this task is a bit weird because the return type of mediator.Send<TResponse>() is Task<TResponse>
                // responseType contains typeof(TResponse) but it wasn't passed as a type argument, so we can't simply cast it
                // dynamic is awaitable and can be cast to any type. Since it is actually a Task<TResponse>, awaiting it
                // produces a result of type TResponse, which can be cast to IResult
                var task = (dynamic) concrete.Invoke(mediator, new object[] {message, CancellationToken.None});
                var result = await task;
                stopwatch.Stop();
                return result;
            }
        }
    }
}