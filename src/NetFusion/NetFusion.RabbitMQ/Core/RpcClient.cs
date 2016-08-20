﻿using NetFusion.Common;
using NetFusion.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NetFusion.RabbitMQ.Core
{
    public class RpcClient : IRpcClient
    {
        private const string DEFAULT_EXCHANGE = "";

        private readonly IModel _channel;
        private readonly string _rpcRequestQueueName;
        private readonly string _replyQueueName;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _futureResults;
        private readonly EventingBasicConsumer _consumer;

        public RpcClient(IModel channel, string rpcRequestQueueName)
        {
            Check.NotNull(channel, nameof(channel));

            _channel = channel;
            _rpcRequestQueueName = rpcRequestQueueName;
            _replyQueueName = _channel. QueueDeclare().QueueName;
            _futureResults = new ConcurrentDictionary<string, TaskCompletionSource<byte[]>>();
            _consumer = new EventingBasicConsumer(channel);

            _consumer.Received += HandleReplyResponse;
        }

        public async Task<byte[]> Invoke(ICommand command, byte[] messageBody)
        {
            Check.NotNull(command, nameof(command));
            Check.NotNull(messageBody, nameof(messageBody));

            // Associate a correlation value with the outgoing message.
            string correlationId = Guid.NewGuid().ToString();
            command.SetCorrelationId(correlationId);

            // Create a future task that can be resolved in the future when the
            // result is received from the queue.
            var futureResult = new TaskCompletionSource<byte[]>();
            _futureResults[correlationId] = futureResult;

            IBasicProperties basicProps = GetBasicProperties(command);

            _channel.BasicPublish(DEFAULT_EXCHANGE,
                             _rpcRequestQueueName,
                             basicProps,
                             messageBody);

            return await futureResult.Task;
        }

        private IBasicProperties GetBasicProperties(ICommand command)
        {
            IBasicProperties props = _channel.CreateBasicProperties();
            props.ReplyTo = _replyQueueName;
            props.CorrelationId = command.GetCorrelationId();
            return props;
        }

        // When a response is received within the queue for a prior request,
        // find the corresponding future-result for the received correlation
        // value and satisfy the result.
        private void HandleReplyResponse(object sender, BasicDeliverEventArgs evt)
        {
            TaskCompletionSource<byte[]> futureResult = null;

            if (_futureResults.TryRemove(evt.BasicProperties.CorrelationId, out futureResult))
            {
                futureResult.SetResult(evt.Body);
            }

            _channel.BasicAck(evt.DeliveryTag, multiple: false);
        }
    }
}
