﻿using NetFusion.Messaging.Rules;
using RefArch.Api.Messages.RabbitMQ;

namespace RefArch.Subscriber.Services
{
    public class ShortMakeRule : MessageDispatchRule<ExampleTopicEvent>
    {
        protected override bool IsMatch(ExampleTopicEvent message)
        {
            return message.Make.Length == 3;
        }
    }
}