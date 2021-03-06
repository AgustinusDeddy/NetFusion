﻿using NetFusion.Common;
using NetFusion.RabbitMQ.Configs;

namespace NetFusion.RabbitMQ.Core.Rpc
{
    /// <summary>
    /// Aggregates a configured RPC broker client and the queue that should be
    /// used to make RPC style requests.  Also contains an instance of the client
    /// that makes the actual request to the consumer. 
    /// </summary>
    public class RpcMessagePublisher
    {
        public string BrokerName { get; }
        public IRpcClient Client { get; }

        public string RequestQueueKey { get; }
        public string RequestQueueName { get; }
        public string ContentType { get; }

        public RpcMessagePublisher(
            string brokerName, 
            RpcConsumerSettings settings, 
            IRpcClient client)
        {
            Check.NotNullOrWhiteSpace(brokerName, nameof(brokerName));
            Check.NotNull(settings, nameof(settings));
            Check.NotNull(client, nameof(client));

            this.BrokerName = brokerName;
            this.RequestQueueKey = settings.RequestQueueKey;
            this.RequestQueueName = settings.RequestQueueName;
            this.ContentType = settings.ContentType;
            this.Client = client;
        }
    }
}
