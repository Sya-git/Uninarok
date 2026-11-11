using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Uninarok.Networking 
{ 
    public static partial class UninarokPackageDispatcher
    {
        public delegate void InAction<T>(in T structData) where T : struct;

        [AutoStaticsCleanup]
        private static partial class EventStorage<T> where T : struct
        {
            public static readonly List<InAction<T>> Callbacks = new(4);
        }

        public static void Subscribe<T>(InAction<T> callback) where T : struct
        {
            if (!EventStorage<T>.Callbacks.Contains(callback))
            {
                EventStorage<T>.Callbacks.Add(callback);
            }
        }

        public static void Unsubscribe<T>(InAction<T> callback) where T : struct
        {
            EventStorage<T>.Callbacks.Remove(callback);
        }

        public static void Dispatch<T>(in T dados) where T : struct
        {
            var list = EventStorage<T>.Callbacks;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i](in dados);
            }
        }
    }
}
