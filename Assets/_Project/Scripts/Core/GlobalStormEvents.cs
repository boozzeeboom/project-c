using UnityEngine;

namespace ProjectC.Core
{
    public static class GlobalStormEvents
    {
        public static event System.Action<float> OnStormIntensityChanged;

        public static void BroadcastStormIntensity(float intensity)
        {
            OnStormIntensityChanged?.Invoke(intensity);
        }

        public static void Subscribe(System.Action<float> handler)
        {
            OnStormIntensityChanged += handler;
        }

        public static void Unsubscribe(System.Action<float> handler)
        {
            OnStormIntensityChanged -= handler;
        }
    }
}