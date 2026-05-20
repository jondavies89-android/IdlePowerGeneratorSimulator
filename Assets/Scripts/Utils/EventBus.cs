using System;
using System.Collections.Generic;

namespace IdlePowerGen.Utils
{
    // Generic pub-sub; decouples Simulation, Economy, and Presentation layers.
    // One static instance per app lifetime; clear on scene reload if needed.
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!Handlers.ContainsKey(type))
                Handlers[type] = new List<Delegate>();
            Handlers[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (Handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public static void Publish<T>(T evt)
        {
            if (!Handlers.TryGetValue(typeof(T), out var list)) return;
            // Iterate on a snapshot so handlers can safely unsubscribe during dispatch
            var snapshot = list.ToArray();
            foreach (var h in snapshot)
                ((Action<T>)h)(evt);
        }

        public static void Clear()
        {
            Handlers.Clear();
        }
    }
}
