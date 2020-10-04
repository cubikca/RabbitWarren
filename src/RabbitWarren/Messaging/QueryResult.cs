using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RabbitWarren.Messaging
{
    public class QueryResult<T> : Result where T : ISerializable, new()
    {
        public IList<T> Results { get; set; }
    }
}