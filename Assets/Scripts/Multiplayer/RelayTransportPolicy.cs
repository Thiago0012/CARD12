using Unity.Services.Multiplayer;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Chooses the Relay transport used by every supported client.
    /// Master Duel 2 Plus Ultra is a low-frequency turn-based game, so WSS is
    /// the safest common denominator for carrier networks, public Wi-Fi and
    /// desktop firewalls that may reject Relay's UDP/DTLS port range.
    /// </summary>
    public static class RelayTransportPolicy
    {
        public const string DisplayName = "WSS seguro";

        public static RelayProtocol Select(RuntimePlatform _)
        {
            return RelayProtocol.WSS;
        }

        public static bool RequiresWebSockets(RelayProtocol protocol)
        {
            return protocol == RelayProtocol.WSS;
        }

        public static void ApplyTo(UnityTransport transport, RelayProtocol protocol)
        {
            if (transport == null)
                return;

            transport.UseWebSockets = RequiresWebSockets(protocol);
        }
    }
}
