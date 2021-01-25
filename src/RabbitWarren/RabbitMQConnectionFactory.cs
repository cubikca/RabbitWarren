using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Autofac;
using RabbitMQ.Client;

namespace RabbitWarren
{
    /// <summary>
    ///     Specify the protocol to use for RabbitMQ
    /// </summary>
    public enum RabbitMQProtocol
    {
        /// <summary>
        ///     Specify unencrypted connection using AMQP
        /// </summary>
        AMQP,

        /// <summary>
        ///     Specify AMQP over SSL-enabled connection
        /// </summary>
        AMQPS
    }

    /// <summary>
    ///     A simple factory for creating connections to RabbitMQ
    /// </summary>
    public class RabbitMQConnectionFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        public IContainer ServiceContainer { get; set; }

        /// <summary>
        ///     Create a connection factory given the provided connection parameters
        /// </summary>
        /// <param name="protocol"><c>RabbitMQProtocol.AMQP</c> or the SSL-enabled <c>RabbitMQProtocol.AMQPS</c></param>
        /// <param name="host">The RabbitMQ endpoint</param>
        /// <param name="vhost">The virtual host to connect to</param>
        /// <param name="port">The endpoint TCP port</param>
        /// <param name="cert">
        ///     The certificate to use for SSL handshake. Can also be used for external authentication if no
        ///     username/password provided.
        /// </param>
        /// <param name="container">The AutoFac container for MediatR handler registration</param>
        /// <param name="username">The connection username (not required if using external authentication)</param>
        /// <param name="password">The connection password (not required if using external authentication)</param>
        public RabbitMQConnectionFactory(RabbitMQProtocol protocol, string host, string vhost, int port, X509Certificate2 cert, IContainer container,
            string username = null, string password = null)
        {
            var scheme = "amqp";
            Uri uri;
            SslOption sslOption = null;
            IAuthMechanismFactory[] authMechanisms = {new PlainMechanismFactory()};
            switch (protocol)
            {
                case RabbitMQProtocol.AMQPS:
                    scheme = "amqps";
                    authMechanisms = username == null && password == null
                        ? new IAuthMechanismFactory[] {new ExternalMechanismFactory()}
                        : authMechanisms;
                    // it is a bit of an assumption that the SSL certificate name match the hostname, but it usually should
                    sslOption = new SslOption
                    {
                        Enabled = true,
                        Certs = new X509Certificate2Collection(new[] {cert}),
                        ServerName = host,
                        AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors
                    };
                    break;
            }

            uri = new Uri($"{scheme}://{host}:{port}/");
            _connectionFactory = new ConnectionFactory
            {
                Uri = uri,
                HostName = host,
                VirtualHost = vhost,
                UserName = username,
                Password = password,
                Port = port,
                Ssl = sslOption ?? new SslOption {Enabled = false},
                AuthMechanisms = authMechanisms,
                DispatchConsumersAsync = true
            };
            ServiceContainer = container;
        }

        /// <summary>
        ///     Open a connection to RabbitMQ
        /// </summary>
        /// <returns>The newly created connection</returns>
        public RabbitMQConnection Create()
        {
            var connection = _connectionFactory.CreateConnection();
            return new RabbitMQConnection(connection, ServiceContainer);
        }
    }
}