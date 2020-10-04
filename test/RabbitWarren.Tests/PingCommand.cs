using RabbitWarren.Messaging;

namespace RabbitWarren.Tests
{
    public class PingCommand : Command<PingCommandResult>
    {
        public int Sequence { get; set; }
    }
}