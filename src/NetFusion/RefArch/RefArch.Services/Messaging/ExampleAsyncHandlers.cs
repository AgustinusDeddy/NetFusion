﻿using NetFusion.Messaging;
using RefArch.Api.Messages;
using RefArch.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RefArch.Services.Messaging
{
    public class ExampleAsyncHandler1 : IMessageConsumer
    {
        [InProcessHandler]
        public async Task OnEvent(ExampleAsyncDomainEvent evt)
        {
            await Task.Run(() => { Thread.Sleep(evt.Seconds * 1000); });
        }
    }

    public class ExampleAsyncHandler2 : IMessageConsumer
    {
        [InProcessHandler]
        public async Task OnEvent(ExampleAsyncDomainEvent evt)
        {
            await Task.Run(() => { Thread.Sleep(evt.Seconds * 1000); });
        }
    }

    public class ExampleAsyncHandler3 : IMessageConsumer
    {
        [InProcessHandler]
        public async Task OnEvent(ExampleAsyncDomainEventException evt)
        {
            await Task.Run(() => {
                Thread.Sleep(evt.Seconds * 1000);
            });

            if (evt.ThrowEx)
            {
                throw new InvalidOperationException($"Example exception: {Guid.NewGuid()}");
            }
        }
    }

    public class ExampleAsyncHandler4 : IMessageConsumer
    {
        [InProcessHandler]
        public async Task OnEvent(ExampleAsyncDomainEventException evt)
        {
            await Task.Run(() => {
                Thread.Sleep(evt.Seconds * 1000);
            });

            if (evt.ThrowEx)
            {
                throw new InvalidOperationException($"Example exception: {Guid.NewGuid()}");
            }
        }

        [InProcessHandler]
        public async Task<HandlerResponse> OnCommand(ExampleAsyncCommand command)
        {
            await Task.Run(() => {
                Thread.Sleep(command.Seconds * 1000);
            });

            return new HandlerResponse
            {
                ResponseMessage = command.Message + " - with handler response. "
            };
        }
    }
}
