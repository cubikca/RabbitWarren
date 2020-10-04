using System.Runtime.Serialization;
using MediatR;

namespace RabbitWarren.Messaging
{
    public abstract class Query<TResult, TModel> : Message, IQuery<TResult, TModel>, IRequest<TResult>
        where TResult : QueryResult<TModel>
        where TModel : ISerializable, new()
    {
    }
}