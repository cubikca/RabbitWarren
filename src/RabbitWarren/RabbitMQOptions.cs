namespace RabbitWarren
{
    public class RabbitMQOptions
    {
        public const string RabbitMQ = "RabbitMQ";

        public string Host { get; set; }
        public string VirtualHost { get; set; }
        public int Port { get; set; }
        public string Exchange { get; set; }
        public string EventQueue { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}