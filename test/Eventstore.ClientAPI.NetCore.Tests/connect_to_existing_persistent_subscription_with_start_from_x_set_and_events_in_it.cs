﻿using System;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace Eventstore.ClientAPI.Tests
{
    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it : SpecificationWithConnection
    {
        private readonly string _stream = "$" + Guid.NewGuid();

        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
            .DoNotResolveLinkTos()
            .StartFrom(4);

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;
        
        private const string _group = "startinx2";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                var id = Guid.NewGuid();
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
                if (i == 4) _id = id;
            }
        }

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();

        }

        private bool _set = false;
        private void HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent)
        {
            if (_set) return;
            _set = true;
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_the_written_event_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsNotNull(_firstEvent);
            Assert.AreEqual(4, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_id, _firstEvent.Event.EventId);
        }
    }
}