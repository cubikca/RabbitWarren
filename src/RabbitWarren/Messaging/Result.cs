using System;
using System.Runtime.Serialization;

namespace RabbitWarren.Messaging
{
    public abstract class Result : Message, IResult
    {
        public Guid CorrelationId { get; set; }
        public string Message { get; set; }
        public string Warning { get; set; }
        public string Error { get; set; }
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public Exception Exception { get; set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Message", Message);
            info.AddValue("Warning", Warning);
            info.AddValue("Error", Error);
            info.AddValue("Exception", Exception);
            info.AddValue("CorrelationId", CorrelationId);
            info.AddValue("Success", Success);
            info.AddValue("StatusCode", StatusCode);
            base.GetObjectData(info, context);
        }
    }
}