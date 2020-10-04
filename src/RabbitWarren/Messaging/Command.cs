namespace RabbitWarren.Messaging
{
    public abstract class Command<TResult> : Message, ICommand<TResult> where TResult : CommandResult
    {
    }
}