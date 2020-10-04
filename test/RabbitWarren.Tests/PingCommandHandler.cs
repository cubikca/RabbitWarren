using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace RabbitWarren.Tests
{
    public class PingCommandHandler : IRequestHandler<PingCommand, PingCommandResult>
    {
        public Task<PingCommandResult> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            var pong = new PingCommandResult {Sequence = request.Sequence, CorrelationId = request.Id};
            return Task.FromResult(pong);
        }
    }
}