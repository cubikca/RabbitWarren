using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitWarren.Tests
{
    public class PingServer
    {
        private readonly RabbitMQConnectionFactory _factory;
        private readonly CancellationTokenSource _tokenSource;

        public PingServer(RabbitMQConnectionFactory factory)
        {
            _factory = factory;
            _tokenSource = new CancellationTokenSource();
        }

        public Task Run()
        {
            Console.WriteLine("[Server] Starting Ping Server");
            var serverConnection = _factory.Create();
            var serverChannel = serverConnection.OpenConsumerChannel(
                "test_exchange",
                "test_queue",
                exclusive: false,
                autoDelete: false);
            var consumer = serverChannel.RegisterMediatRConsumer(Assembly.GetExecutingAssembly());
            consumer.Start(serverChannel.Exclusive, serverChannel.AutoDelete);
            return Task.Factory.StartNew(() =>
            {
                Console.WriteLine("[Server] Ping Server Started");
                try
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        Console.WriteLine("[Server] Heartbeat");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[Server] Cancellation Received, Shutting Down");
                    serverChannel.Consumers[0].Stop();
                    serverConnection.Close();
                }
            }, _tokenSource.Token);
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            Console.WriteLine("[Server] Ping Server Stopped");
        }
    }
}