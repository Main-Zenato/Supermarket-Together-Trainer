using HarmonyLib;
using Mirror;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    /// <summary>Clock and store upkeep. The clock is a server SyncVar, so host only.</summary>
    internal static class World
    {
        public static bool FreezeTime;
        public static float TimeSpeed = 1f;
        public static bool NoTrash;
        public static bool NoBreakdowns;

        internal static float FrozenAt;

        public static void Reset()
        {
            FreezeTime = false; TimeSpeed = 1f; NoTrash = false; NoBreakdowns = false;
        }

        public static string Clock(float t)
        {
            t = Mathf.Repeat(t, 24f);
            int h = (int)t, m = (int)((t - h) * 60f);
            return L.T($"{h} h {m:00}", $"{h}:{m:00}");
        }

        private static void CleanTrash()
        {
            var root = GameData.Instance.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(6);
            int n = root.childCount;
            for (int i = n - 1; i >= 0; i--) NetworkServer.Destroy(root.GetChild(i).gameObject);
            Toasts.Show(n > 0 ? L.T($"{n} déchets ramassés.", $"{n} pieces of trash picked up.")
                : L.T("Le magasin est déjà propre.", "The store is already clean."));
        }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, lk = !host, inGame = Game.InGame;
            var gd = GameData.Instance;
            if (!host) ui.Note(L.T("L'heure et l'entretien du magasin sont gérés par l'hôte. Ces réglages sont grisés quand tu as rejoint une autre partie.",
                "The clock and store upkeep are run by the host. These settings are greyed out when you join someone else's game."), Theme.Price);

            ui.Section(L.T("Horloge", "Clock"));
            float now = inGame ? gd.timeOfDay : 8f;
            float set = ui.Slider("world.time", L.T("Heure", "Time"), now, 0f, 23.99f, Clock, lk, 0.25f);
            if (inGame && host && Mathf.Abs(set - now) > 0.01f)
            {
                gd.NetworktimeOfDay = set;
                FrozenAt = set;
            }
            bool wasFrozen = FreezeTime;
            FreezeTime = ui.Toggle("world.freeze", L.T("Figer l'heure", "Freeze time"),
                L.T("Le temps s'arrête. Les clients continuent d'entrer si le magasin est ouvert.",
                    "Time stops. Customers keep coming in while the store is open."), FreezeTime, lk);
            if (FreezeTime && !wasFrozen && inGame) FrozenAt = gd.timeOfDay;
            TimeSpeed = ui.Slider("world.speed", L.T("Vitesse du temps", "Time speed"), TimeSpeed, 0.25f, 5f, v => "×" + v.ToString("0.00"), lk, 0.25f);
            int jump = ui.Buttons("world.jump", lk, Clock(8f), Clock(12f), Clock(18f), Clock(22f));
            if (jump >= 0 && inGame && host)
            {
                float t = new[] { 8f, 12f, 18f, 22f }[jump];
                gd.NetworktimeOfDay = t;
                FrozenAt = t;
                Toasts.Show(L.T($"Il est maintenant {Clock(t)}.", $"It is now {Clock(t)}."));
            }

            ui.Section(L.T("Entretien", "Upkeep"));
            NoTrash = ui.Toggle("world.notrash", L.T("Aucun déchet", "No trash"),
                L.T("Plus aucun déchet n'apparaît par terre.", "No more trash shows up on the floor."), NoTrash, lk);
            NoBreakdowns = ui.Toggle("world.nobreak", L.T("Aucune panne", "No breakdowns"),
                L.T("Les caisses et les meubles ne tombent plus en panne.", "Checkouts and furniture no longer break down."), NoBreakdowns, lk);
            if (ui.Buttons("world.clean", lk, new Btn(L.T("Ramasser tous les déchets", "Pick up all trash"), ButtonKind.Primary)) == 0 && inGame && host)
                CleanTrash();
        }
    }

    [HarmonyPatch(typeof(GameData), "FixedUpdate")]
    internal static class ClockPatch
    {
        private static void Prefix(GameData __instance, out float __state) => __state = __instance.timeOfDay;

        private static void Postfix(GameData __instance, float __state)
        {
            if (!__instance.isServer) return;
            if (World.FreezeTime)
            {
                __instance.NetworktimeOfDay = World.FrozenAt;
                return;
            }
            float delta = __instance.timeOfDay - __state;
            if (delta > 0f && delta < 0.5f && !Mathf.Approximately(World.TimeSpeed, 1f))
                __instance.NetworktimeOfDay = __state + delta * World.TimeSpeed;
        }
    }

    [HarmonyPatch(typeof(GameData), "TrashManager")]
    internal static class TrashPatch
    {
        private static bool Prefix() => !World.NoTrash;
    }

    [HarmonyPatch(typeof(GameData), "BreakingManager")]
    internal static class BreakdownPatch
    {
        private static bool Prefix() => !World.NoBreakdowns;
    }
}
