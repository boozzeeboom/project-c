using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Interface for objects that can be interacted with by the player.
    /// Used for trigger-based caching instead of FindObjectsByType.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Unique identifier for this interactable instance.
        /// </summary>
        string InstanceId { get; }
        
        /// <summary>
        /// Display name shown to player when nearby.
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Interaction radius for this object.
        /// </summary>
        float InteractionRadius { get; }
        
        /// <summary>
        /// World position of this interactable.
        /// </summary>
        Vector3 Position { get; }
    }
}