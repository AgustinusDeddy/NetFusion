﻿using ExampleApi.Messages;
using NetFusion.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsumerHost.Services
{
    public class ExampleRpcService : IMessageConsumer
    {
        [InProcessHandler]
        public async Task<ExampleRpcResponse> OnRpcMessage(ExampleRpcCommand rpcCommand)
        {
            Console.WriteLine($"Delay: {rpcCommand.DelayInMs} TestValue: {rpcCommand.InputValue}");

            await Task.Run(() =>
            {
                Thread.Sleep(rpcCommand.DelayInMs);
              //  throw new InvalidOperationException("TEST");
            });

            return new ExampleRpcResponse
            {
                ResponseTestValue = rpcCommand.InputValue + " " + DateTime.UtcNow
            };
        }
    }
}
