﻿using Autofac;
using CoreTests.Messaging.Mocks;
using FluentAssertions;
using NetFusion.Bootstrap.Container;
using NetFusion.Messaging.Modules;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CoreTests.Messaging
{
    /// <summary>
    /// Unit test asserting that the message types and message consumer were
    /// correctly found by the messaging module during the bootstrap process.
    /// </summary>
    public class DiscoveryTests
    {
        /// <summary>
        /// The domain event module will discover all defined domain event classes
        /// within all available plug-ins.  The module's EventTypeDispatchers property
        /// contains all of the meta-data required to dispatch an event at runtime to
        /// the correct consumer handlers.
        /// </summary>
        [Fact(DisplayName = nameof(Discovers_DomainEvent))]
        public void Discovers_DomainEvent()
        {
            DefaultSetup.EventConsumer
                .Test(
                    c => c.Build(), 
                    (MessagingModule m) =>
                    {
                        var dispatchInfo = m.InProcessDispatchers[typeof(MockDomainEvent)]?.FirstOrDefault();
                        dispatchInfo.Should().NotBeNull();

                        // Assert that the domain event and the associated message consumer were found.
                        dispatchInfo.ConsumerType.Should().Be(typeof(MockDomainEventConsumer));
                        dispatchInfo.MessageType.Should().Be(typeof(MockDomainEvent));      
                    });
        }

        /// <summary>
        /// The domain event module will discover all defined domain event consumer classes with methods that 
        /// handle a given domain event.  Application components are scanned for event handler methods during
        /// the bootstrap process by implementing the IMessageConsumer marker interface.
        /// </summary>
        [Fact(DisplayName = nameof(Discovers_DomainEventConsumerHandler))]
        public void Discovers_DomainEventConsumerHandler()
        {
            DefaultSetup.EventConsumer
                .Test(
                    c => c.Build(),
                    (MessagingModule m) =>
                    {
                        var dispatchInfo = m.InProcessDispatchers[typeof(MockDomainEvent)]?.FirstOrDefault();
                        dispatchInfo.Should().NotBeNull();

                        dispatchInfo.MessageType.Should().Be(typeof(MockDomainEvent));
                        dispatchInfo.ConsumerType.Should().Be(typeof(MockDomainEventConsumer));
                        dispatchInfo.MessageHandlerMethod.Should()
                            .Equals(typeof(MockDomainEventConsumer).GetMethod("OnEventHandlerOne"));
                    });
        }

        /// <summary>
        /// All discovered event consumers are registered in the dependency injection container.
        /// When an event is published, the domain event service resolves the consumer type
        /// and obtains a reference from the container using the cached dispatch information.
        /// </summary>
        [Fact(DisplayName = nameof(DomainEventConsumer_Registered))]
        public void DomainEventConsumer_Registered()
        {
            DefaultSetup.EventConsumer
                .Test(
                    c => c.Build(),
                    (IAppContainer c) =>
                    {
                        var consumer = c.Services.Resolve<MockDomainEventConsumer>();
                        consumer.Should().NotBeNull();
                    });
        }
    }
}
