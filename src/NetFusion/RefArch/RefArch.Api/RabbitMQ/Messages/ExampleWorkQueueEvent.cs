﻿using NetFusion.Common.Extensions;
using NetFusion.Messaging;
using NetFusion.RabbitMQ;
using RefArch.Api.RabbitMQ.Models;
using System;

namespace RefArch.Api.RabitMQ.Messages
{
    public class ExampleWorkQueueEvent : DomainEvent
    {
        public string Make { get; private set; }
        public string Model { get; private set; }
        public int Year { get; private set; }

        public ExampleWorkQueueEvent() { }

        public ExampleWorkQueueEvent(Car car)
        {
            this.CurrentDateTime = DateTime.UtcNow;
            this.Make = car.Make;
            this.Model = car.Model;
            this.Year = car.Year;

            this.SetRouteKey(car.Make.InSet("VW", "BMW") ? "Process_Sale" : "Process_Service");
        }

        public DateTime CurrentDateTime { get; private set; }
    }
}
