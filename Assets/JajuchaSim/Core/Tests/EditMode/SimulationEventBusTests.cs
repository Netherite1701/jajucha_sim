using JajuchaSim.Core;
using NUnit.Framework;

namespace JajuchaSim.Core.Tests
{
    public class SimulationEventBusTests
    {
        [Test]
        public void Subscribe_Publish_Handler_Called_Once()
        {
            var bus = new SimulationEventBus();
            int calls = 0;
            bus.Subscribe<SimulationStartedEvent>(e => calls++);
            bus.Publish(new SimulationStartedEvent(0.0));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Unsubscribe_Handler_Not_Called()
        {
            var bus = new SimulationEventBus();
            int calls = 0;
            void Handler(SimulationResetEvent e) => calls++;
            bus.Subscribe<SimulationResetEvent>(Handler);
            bus.Unsubscribe<SimulationResetEvent>(Handler);
            bus.Publish(new SimulationResetEvent(0));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Publish_Carries_Event_Payload()
        {
            var bus = new SimulationEventBus();
            double seen = -1.0;
            bus.Subscribe<SimulationStartedEvent>(e => seen = e.StartTime);
            bus.Publish(new SimulationStartedEvent(3.5));
            Assert.AreEqual(3.5, seen);
        }

        [Test]
        public void Multiple_Subscribers_All_Called()
        {
            var bus = new SimulationEventBus();
            int a = 0, b = 0;
            bus.Subscribe<SimulationResetEvent>(e => a++);
            bus.Subscribe<SimulationResetEvent>(e => b++);
            bus.Publish(new SimulationResetEvent(7));
            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void Clear_Removes_All()
        {
            var bus = new SimulationEventBus();
            bus.Subscribe<SimulationStartedEvent>(e => { });
            bus.Subscribe<SimulationResetEvent>(e => { });
            Assert.AreEqual(1, bus.SubscriberCount<SimulationStartedEvent>());
            Assert.AreEqual(1, bus.SubscriberCount<SimulationResetEvent>());
            bus.Clear();
            Assert.AreEqual(0, bus.SubscriberCount<SimulationStartedEvent>());
            Assert.AreEqual(0, bus.SubscriberCount<SimulationResetEvent>());
        }

        [Test]
        public void Unsubscribe_During_Publish_DoesNot_Invalidate_Iteration()
        {
            var bus = new SimulationEventBus();
            int secondCalls = 0;
            bus.Subscribe<SimulationResetEvent>(e => bus.Unsubscribe<SimulationResetEvent>(Handler2));
            void Handler2(SimulationResetEvent e) => secondCalls++;
            bus.Subscribe<SimulationResetEvent>(Handler2);
            // First publish: first handler unsubscribes Handler2 during snapshot
            // iteration. The snapshot already captured Handler2, so it fires once.
            Assert.DoesNotThrow(() => bus.Publish(new SimulationResetEvent(0)));
            Assert.AreEqual(1, secondCalls, "snapshot should have fired Handler2 once");
            // Second publish: Handler2 is now unsubscribed -> must not fire again.
            bus.Publish(new SimulationResetEvent(0));
            Assert.AreEqual(1, secondCalls, "Handler2 should not fire after unsubscribe");
        }

        [Test]
        public void Subscribe_Null_Throws()
        {
            var bus = new SimulationEventBus();
            Assert.Throws<System.ArgumentNullException>(() => bus.Subscribe<SimulationStartedEvent>(null));
        }
    }
}