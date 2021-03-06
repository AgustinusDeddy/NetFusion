﻿using NetFusion.RabbitMQ.Exchanges;

namespace NetFusion.Tests.Infrastructure.RabbitMQ.Mocks
{
    public class MockFanoutExchange : FanoutExchange<MockDomainEvent>
    {
        protected override void OnDeclareExchange()
        {
            Settings.BrokerName = "MockTestBrokerName";
            Settings.ExchangeName = "MockFanoutExchangeName";
        }
    }
}
