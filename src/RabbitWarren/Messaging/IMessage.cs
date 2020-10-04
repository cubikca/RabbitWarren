using System;
using System.Runtime.Serialization;

namespace RabbitWarren.Messaging
{
    public interface IMessage : ISerializable
    {
        public Guid Id { get; set; }
    }
}