using System;
using System.Runtime.Serialization;

namespace RabbitWarren.Messaging
{
    public class Message : IMessage
    {
        public Guid Id { get; set; }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Id", Id);
        }
    }
}