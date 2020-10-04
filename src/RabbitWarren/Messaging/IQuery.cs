using System.Runtime.Serialization;

namespace RabbitWarren.Messaging
{
    public interface IQuery : IMessage
    {
    }

    public interface IQuery<TResult, TModel> : IQuery where TResult : QueryResult<TModel> where TModel : ISerializable, new()
    {
    }
}