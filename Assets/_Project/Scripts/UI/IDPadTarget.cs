using UnityEngine;

namespace Shrink.UI
{
    /// <summary>
    /// Interfaz que permite a DPadController enviar direcciones a cualquier receptor
    /// (PlayerMovement en modo single-player, NetworkPlayer en multiplayer).
    /// </summary>
    public interface IDPadTarget
    {
        void SetDPadDirection(Vector2Int dir);
    }
}
