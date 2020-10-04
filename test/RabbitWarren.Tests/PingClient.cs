using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitWarren.Tests
{
    public class PingClient
    {
        private readonly RabbitMQConnectionFactory _factory;

        public PingClient(RabbitMQConnectionFactory factory)
        {
            _factory = factory;
        }

        public Task Run()
        {
            var connection = _factory.Create();
            var publishChannel = connection.OpenPublishChannel("test_exchange");
            var consumerId = Guid.NewGuid();
            var consumerChannel = connection.OpenConsumerChannel("", $"response.{consumerId}");
            var consumer = consumerChannel.RegisterDefaultConsumer();
            consumer.Start(consumerChannel.Exclusive, consumerChannel.AutoDelete);
            return Task.Factory.StartNew(() =>
            {
                var semaphore = new SemaphoreSlim(16);
                Console.WriteLine("[Client] Starting Ping Client");
                var tasks = Enumerable.Range(1, 128).Select(async i =>
                {
                    await semaphore.WaitAsync();
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    var responseMessage = await publishChannel.Request(new PingCommand {Sequence = i},
                        "test_queue",
                        $"response.{consumerId}");
                    stopWatch.Stop();
                    if (responseMessage is PingCommandResult pong)
                        Console.WriteLine($"[Client] Pong received, Sequence = {pong.Sequence}, RTT = {stopWatch.ElapsedMilliseconds}ms");
                    semaphore.Release();
                });
                Task.WhenAll(tasks).Wait();
                publishChannel.Close();
            });
        }
    }
}