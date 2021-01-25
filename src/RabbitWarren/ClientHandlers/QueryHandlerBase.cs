using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RabbitWarren.Messaging;
using MediatR;

namespace RabbitWarren.ClientHandlers
{
    public abstract class QueryHandlerBase<TRequest, TResponse, TModel> : IRequestHandler<TRequest, TResponse>
        where TRequest : Query<TResponse, TModel>
        where TResponse : QueryResult<TModel>
        where TModel : ISerializable, new()
    {
        private readonly RabbitMQConnection _connection;
        private readonly RabbitMQOptions _mqOptions;
        
        protected QueryHandlerBase(RabbitMQConnection connection, RabbitMQOptions mqOptions)
        {
            _connection = connection;
            _mqOptions = mqOptions;
        }

        public virtual async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
        {
            
            TResponse response;
            try
            {
                var consumerId = Guid.NewGuid();
                using var publishChannel = _connection.OpenPublishChannel(_mqOptions.Exchange);
                // response channel is not bound, so we use the default exchange
                using var consumerChannel = _connection.OpenConsumerChannel("",
                    $"response.{consumerId}",
                    true,
                    true);
                var consumer = consumerChannel.RegisterDefaultConsumer();
                consumer.Start();
                var message = await publishChannel.Request(request, _mqOptions.EventQueue, $"response.{consumerId}");
                consumer.Stop();
                consumerChannel.Close();
                publishChannel.Close();
                if (message is ErrorResult error)
                {
                    response = Activator.CreateInstance<TResponse>();
                    response.Error = error.Error;
                    response.Message = error.Message;
                    response.Warning = error.Warning;
                    response.Exception = error.Exception;
                }
                else if (!(message is TResponse))
                {
                    response = Activator.CreateInstance<TResponse>();
                    response.Error = $"Unknown response message type: {message.GetType().FullName}";
                }
                else
                {
                    var result = (TResponse) message;
                    response = result;
                }

                return response;
            }
            catch (Exception ex)
            {
                response = Activator.CreateInstance<TResponse>();
                response.Error = ex.GetBaseException().Message;
            }

            return response;
        }
    }
}