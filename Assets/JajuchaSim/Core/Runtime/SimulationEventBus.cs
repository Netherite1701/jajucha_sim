using System;
using System.Collections.Generic;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Tiny typed pub/sub bus for important simulation events.
    ///
    /// This is intentionally small: Subscribe / Unsubscribe / Publish / Clear.
    /// No priorities, reflection, async, history, or networking. It is a
    /// struct-event channel, not a high-frequency sensor channel.
    /// </summary>
    public sealed class SimulationEventBus
    {
        private readonly Dictionary<Type, object> _subscribers = new Dictionary<Type, object>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_subscribers.TryGetValue(typeof(T), out object list))
            {
                list = new List<Action<T>>();
                _subscribers[typeof(T)] = list;
            }
            ((List<Action<T>>)list).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_subscribers.TryGetValue(typeof(T), out object list))
                ((List<Action<T>>)list).Remove(handler);
        }

        public void Publish<T>(in T evt) where T : struct
        {
            if (_subscribers.TryGetValue(typeof(T), out object list))
            {
                // Snapshot to allow handlers to unsubscribe during publish.
                var handlers = (List<Action<T>>)list;
                List<Action<T>> copy = null;
                try
                {
                    copy = new List<Action<T>>(handlers);
                }
                catch (Exception)
                {
                    copy = null;
                }
                if (copy != null)
                {
                    for (int i = 0; i < copy.Count; i++)
                        copy[i](evt);
                }
                else
                {
                    for (int i = 0; i < handlers.Count; i++)
                        handlers[i](evt);
                }
            }
        }

        public int SubscriberCount<T>() where T : struct
        {
            return _subscribers.TryGetValue(typeof(T), out object list)
                ? ((List<Action<T>>)list).Count
                : 0;
        }

        public void Clear() => _subscribers.Clear();
    }
}