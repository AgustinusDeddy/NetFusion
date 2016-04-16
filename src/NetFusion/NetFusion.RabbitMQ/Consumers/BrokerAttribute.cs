﻿using System;

namespace NetFusion.RabbitMQ.Consumers
{
    /// <summary>
    /// Attached to message consumer to specify the broker
    /// to which the message event handlers are associated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class BrokerAttribute : Attribute
    {
        /// <summary>
        /// The name of the broker specified in BrokerSettings.
        /// </summary>
        public string BrokerName { get; }

        /// <summary>
        /// The name of the broker specified in BrokerSettings.
        /// </summary>
        /// <param name="brokerName">The name of the broker.</param>
        public BrokerAttribute(string brokerName)
        {
            this.BrokerName = brokerName;
        }
    }
}
