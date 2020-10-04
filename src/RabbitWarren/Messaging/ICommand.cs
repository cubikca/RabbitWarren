using MediatR;

namespace RabbitWarren.Messaging
{
    public interface ICommand : IMessage
    {
    }

    public interface ICommand<TResult> : ICommand, IRequest<TResult> where TResult : CommandResult
    {
    }
}