using System;

namespace RabbitWarren.Messaging
{
    public interface IResult : IMessage
    {
        Guid CorrelationId { get; set; }
        bool Success { get; set; }
        string Error { get; set; }
        string Warning { get; set; }
        string Message { get; set; }
        Exception Exception { get; set; }
    }
}