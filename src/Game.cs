using HarmonyLib;
using HutongGames.PlayMaker;
using Mirror;
using Rewired;
using StarterAssets;
using UnityEngine;

namespace SupermarketTrainer
{
    /// <summary>Thin accessors over the game's singletons, all null-safe across scene loads.</summary>
    internal static class Game
    {
        public static bool InGame => GameData.Instance && FirstPersonController.Instance;
        public static bool IsHost => NetworkServer.active;
        public static bool IsClient => NetworkClient.active;

        public static FirstPersonController Fpc => FirstPersonController.Instance;
        public static Transform Player => Fpc ? Fpc.transform : null;
        public static PlayerNetwork Net => Fpc ? Fpc.GetComponent<PlayerNetwork>() : null;

        public static Player Input => ReInput.isReady ? ReInput.players.GetPlayer(0) : null;

        public static string SessionLabel => !InGame ? L.T("Hors partie", "Not in a game") : IsHost ? L.T("Hôte", "Host") : "Client";

        public static bool InOptions
        {
            get => FsmVariables.GlobalVariables.FindFsmBool("InOptions")?.Value ?? false;
            set
            {
                var v = FsmVariables.GlobalVariables.FindFsmBool("InOptions");
                if (v != null) v.Value = value;
            }
        }

        public static void Teleport(Vector3 position)
        {
            if (!Player) return;
            var cc = Player.GetComponent<CharacterController>();
            bool was = cc && cc.enabled;
            if (cc) cc.enabled = false;
            Player.position = position;
            Physics.SyncTransforms();
            if (cc) cc.enabled = was && !Features.PlayerMods.Noclip;
            Traverse.Create(Fpc).Field("_verticalVelocity").SetValue(0f);
        }

        public static string Money(float v) =>
            "$" + v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
    }
}
