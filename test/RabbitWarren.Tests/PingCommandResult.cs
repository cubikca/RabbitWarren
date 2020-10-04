using RabbitWarren.Messaging;

namespace RabbitWarren.Tests
{
    public class PingCommandResult : CommandResult
    {
        public int Sequence { get; set; }
    }
}