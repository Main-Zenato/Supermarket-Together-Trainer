using System.Collections.Generic;
using Mirror;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    /// <summary>
    /// Event tokens (star, moon, alien, candy, snow, egg), spent in the gachapon machines. Every
    /// player keeps their own count in their own save, so other players get theirs as the real
    /// pickups the event characters drop, spawned at their feet by the host.
    /// </summary>
    internal static class Tokens
    {
        private static readonly Dictionary<int, GameObject> Prefabs = new Dictionary<int, GameObject>();
        private static int _type, _amount = 5, _to;

        internal static int Types => AchievementsManager.Instance && AchievementsManager.Instance.evtsTkns != null
            ? AchievementsManager.Instance.evtsTkns.Length : 0;

        internal static string Name(int type)
        {
            string name = Brooms.TokenName(type);
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        internal static int Owned(int type) => AchievementsManager.Instance.evtsTkns[type];

        /// <summary>Adds to our own count and saves it, as picking one up does.</summary>
        internal static void AddToSelf(int type, int amount)
        {
            var am = AchievementsManager.Instance;
            am.AddToken(type, amount);
            am.SavebufferValues();
        }

        /// <summary>The pickup prefab of each token type, among the prefabs every client can spawn.</summary>
        internal static GameObject Prefab(int type)
        {
            if (!Prefabs.ContainsKey(type))
                foreach (var p in NetworkClient.prefabs.Values)
                    if (p && p.TryGetComponent<MisterGift>(out var gift) && !Prefabs.ContainsKey(gift.whichTokenIndex))
                        Prefabs[gift.whichTokenIndex] = p;
            return Prefabs.TryGetValue(type, out var prefab) ? prefab : null;
        }

        /// <summary>Spawns pickups in a small spiral around a player. Host only.</summary>
        internal static int Drop(Transform player, int type, int amount)
        {
            var prefab = Prefab(type);
            if (!prefab || !NetworkServer.active || !player) return 0;
            for (int i = 0; i < amount; i++)
            {
                var offset = Quaternion.Euler(0f, i * 137.5f, 0f) * Vector3.forward * (0.5f + 0.08f * i);
                NetworkServer.Spawn(Object.Instantiate(prefab, player.position + offset + Vector3.up * 0.6f, Quaternion.identity));
            }
            return amount;
        }

        /// <summary>Gives tokens to one player: straight into our count, or as pickups for someone else.</summary>
        internal static bool Give(PlayerObjectController p, int type, int amount)
        {
            if (p.isLocalPlayer) { AddToSelf(type, amount); return true; }
            return Drop(p.transform, type, amount) > 0;
        }

        public static void Draw(Ui ui, List<PlayerObjectController> players)
        {
            ui.Section(L.T("Donner des jetons  ·  en test", "Give tokens  ·  testing"));
            if (!Game.InGame || Types == 0)
            {
                ui.Note(L.T("Rejoins ou héberge une partie pour donner des jetons.", "Join or host a game to give tokens."));
                return;
            }
            bool host = Game.IsHost;
            // Recipients: 0 is us, then the other players, then everyone. Guests can only give to themselves.
            var others = players.FindAll(p => !p.isLocalPlayer);
            int last = host ? others.Count + (others.Count > 0 ? 1 : 0) : 0;
            _to = Mathf.Clamp(_to, 0, last);
            _type = Mathf.Clamp(_type, 0, Types - 1);
            _type = ui.Stepper("tk.type", L.T("Jeton", "Token"), L.T($"Tu en as {Owned(_type)}.", $"You have {Owned(_type)}."),
                _type, 0, Types - 1, Name);
            _amount = ui.Stepper("tk.amount", L.T("Quantité", "Amount"), null, _amount, 1, 50);
            _to = ui.Stepper("tk.to", L.T("Pour", "For"), null, _to, 0, last,
                i => i == 0 ? L.T("Moi", "Me") : i <= others.Count ? Players.NameOf(others[i - 1]) : L.T("Tout le monde", "Everyone"));

            if (ui.Buttons("tk.give", false, new Btn(L.T("Donner", "Give"), ButtonKind.Primary)) == 0)
            {
                var targets = new List<PlayerObjectController>();
                if (_to == 0) targets.AddRange(players.FindAll(p => p.isLocalPlayer));
                else if (_to <= others.Count) targets.Add(others[_to - 1]);
                else targets.AddRange(players);
                int ok = 0;
                foreach (var p in targets) if (p && Give(p, _type, _amount)) ok++;
                string what = $"{_amount} {Name(_type)}";
                Toasts.Show(ok == 0 ? L.T("Ce jeton ne peut pas apparaître dans cette partie.", "This token can't appear in this game.")
                    : _to == 0 ? L.T($"+{what}.", $"+{what}.")
                    : targets.Count == 1 ? L.T($"{what} déposé(s) aux pieds de {Players.NameOf(targets[0])}.", $"{what} dropped at {Players.NameOf(targets[0])}'s feet.")
                    : L.T($"{what} pour {ok} joueur(s).", $"{what} for {ok} player(s)."));
            }
            ui.Note(host
                ? L.T("Les tiens arrivent directement. Les autres joueurs reçoivent de vrais jetons à leurs pieds, à ramasser, car chacun garde ses jetons dans sa propre sauvegarde.",
                    "Yours arrive straight away. Other players get real tokens at their feet to pick up, since everyone keeps their tokens in their own save.")
                : L.T("Tu peux t'en donner à toi. En donner aux autres demande d'être l'hôte, car ils apparaissent comme de vrais jetons à ramasser.",
                    "You can give yourself some. Giving to others needs the host, since they appear as real tokens to pick up."));
        }
    }
}
