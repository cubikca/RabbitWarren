using Autofac;
using NUnit.Framework;

namespace RabbitWarren.Tests
{
    public class RabbitMQTests
    {
        private RabbitMQConnectionFactory _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new RabbitMQConnectionFactory(
                RabbitMQProtocol.AMQP,
                "localhost",
                "/",
                5672,
                null,
                new ContainerBuilder().Build(),
                "brian",
                "development"
            );
        }

        [Test]
        public void TestPing()
        {
            var server = new PingServer(_factory);
            var client = new PingClient(_factory);
            var serverTask = server.Run();
            var clientTask = client.Run();
            clientTask.Wait();
            server.Stop();
        }
    }
}