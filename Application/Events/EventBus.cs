using System;
using System.Collections.Generic;

namespace PharmaChainLite.Application.Events
{
    /// <summary>
    /// Simple in-process event bus (publish-subscribe).
    /// Subscribe returns an IDisposable you can call Dispose() on to unsubscribe.
    /// Thread-safe for basic use (single-process, UI app).
    /// </summary>
    public interface IEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
        void Unsubscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent evt);
    }

    public sealed class InProcessEventBus : IEventBus
    {
        private readonly object _gate = new();
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            lock (_gate)
            {
                var key = typeof(TEvent);
                if (!_handlers.TryGetValue(key, out var list))
                {
                    list = new List<Delegate>();
                    _handlers[key] = list;
                }
                list.Add(handler);
            }
            return new Subscription<TEvent>(this, handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler is null) return;
            lock (_gate)
            {
                var key = typeof(TEvent);
                if (_handlers.TryGetValue(key, out var list))
                {
                    list.RemoveAll(d => d.Equals(handler));
                    if (list.Count == 0) _handlers.Remove(key);
                }
            }
        }

        public void Publish<TEvent>(TEvent evt)
        {
            if (evt == null) return;
            List<Delegate>? snapshot = null;

            lock (_gate)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list) && list.Count > 0)
                    snapshot = new List<Delegate>(list);
            }

            if (snapshot is null) return;

            foreach (var d in snapshot)
            {
                try
                {
                    // Safe cast given we only store Action<TEvent> for the key
                    ((Action<TEvent>)d).Invoke(evt);
                }
                catch
                {
                    // Swallow per-subscriber errors to avoid blocking other handlers.
                    // In a real app, log this.
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable
        {
            private readonly InProcessEventBus _bus;
            private Action<TEvent>? _handler;

            public Subscription(InProcessEventBus bus, Action<TEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                var h = _handler;
                if (h != null)
                {
                    _bus.Unsubscribe(h);
                    _handler = null;
                }
            }
        }
    }
}
