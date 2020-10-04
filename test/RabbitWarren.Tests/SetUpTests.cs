using NUnit.Framework;

namespace RabbitWarren.Tests
{
    [SetUpFixture]
    public class SetUpTests
    {
        [OneTimeSetUp]
        public void SetUp()
        {
        }

        [OneTimeTearDown]
        public void TearDown()
        {
        }
    }
}