using HarmonyLib;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    /// <summary>
    /// Event tokens, gachapon capsules and the broom skins they contain. Tokens and unlocked
    /// skins live in the local player save (AchievementsManager), so this works for everyone.
    /// </summary>
    internal static class Brooms
    {
        // Same order as the game's token notifications (startkn, moontkn, alientkn, candytkn, snowtkn, eggtkn).
        private static readonly string[] TokenFr = { "étoile", "lune", "alien", "bonbon", "flocon", "œuf" };
        private static readonly string[] TokenEn = { "star", "moon", "alien", "candy", "snowflake", "egg" };

        internal static string TokenName(int i) => i < TokenFr.Length ? L.T(TokenFr[i], TokenEn[i]) : "#" + i;

        private static void GiveTokens(AchievementsManager am, int index, int amount)
        {
            am.AddToken(index, amount);
            am.SavebufferValues();
            Toasts.Show(L.T($"+{amount} jeton{(amount > 1 ? "s" : "")} {TokenName(index)}.",
                $"+{amount} {TokenName(index)} token{(amount > 1 ? "s" : "")}."));
        }

        private static void UnlockAll(AchievementsManager am)
        {
            for (int i = 0; i < am.unlockedArray.Length; i++) am.unlockedArray[i] = true;
            // Re-running the game's unlock on the equipped skin refreshes the collection board.
            am.GetUnlockable(CurrentSkin(am), null);
            am.SavebufferValues();
            Toasts.Show(L.T("Tous les skins de balai sont débloqués.", "Every broom skin is unlocked."));
        }

        private static int CurrentSkin(AchievementsManager am) => Traverse.Create(am).Field("currentUnlockableIndex").GetValue<int>();

        private static void GoToMachine(GameObject machine)
        {
            var spawn = machine.transform.Find("Spawnpoint") ?? machine.transform;
            var am = AchievementsManager.Instance;
            var forward = am.gachaPoint ? am.gachaPoint.forward : machine.transform.forward;
            forward.y = 0f;
            Game.Teleport(spawn.position + forward.normalized * 1.6f + Vector3.up * 0.2f);
        }

        public static void Draw(Ui ui)
        {
            var am = AchievementsManager.Instance;
            if (!Game.InGame || !am)
            {
                ui.Note(L.T("Rejoins ou héberge une partie pour gérer tes jetons et tes balais.", "Join or host a game to manage your tokens and brooms."));
                return;
            }

            ui.Section(L.T("Jetons d'événement", "Event tokens"));
            int tokens = Mathf.Min(am.evtsTkns.Length, TokenFr.Length);
            for (int i = 0; i < tokens; i++)
            {
                int b = ui.ListRow("tok" + i, L.T("Jeton " + TokenFr[i], char.ToUpper(TokenEn[i][0]) + TokenEn[i].Substring(1) + " token"),
                    L.T(am.evtsTkns[i] + " en stock", am.evtsTkns[i] + " owned"), false,
                    new Btn("+1", ButtonKind.Primary), "+10");
                if (b == 0) GiveTokens(am, i, 1);
                if (b == 1) GiveTokens(am, i, 10);
            }
            if (ui.Buttons("tok.all", false, new Btn(L.T("+10 jetons de chaque sorte", "+10 tokens of every kind"), ButtonKind.Primary)) == 0)
            {
                for (int i = 0; i < tokens; i++) am.AddToken(i, 10);
                am.SavebufferValues();
                Toasts.Show(L.T("+10 jetons de chaque sorte.", "+10 tokens of every kind."));
            }
            ui.Note(L.T("Les jetons s'utilisent dans les machines gachapon du magasin, exactement comme ceux gagnés pendant les événements.",
                "Tokens are spent at the store's gachapon machines, exactly like the ones earned during events."));

            ui.Section(L.T("Boules gachapon", "Gachapon capsules"));
            ui.Note(L.T("Fait tomber une boule de la machine choisie sans dépenser de jeton. Ramasse-la pour découvrir le skin qu'elle contient.",
                "Drops a capsule from the chosen machine without spending a token. Pick it up to reveal the skin inside."));
            var machines = am.gachaponOBJsArray;
            for (int i = 0; i < machines.Length; i++)
            {
                if (!machines[i]) continue;
                int b = ui.ListRow("gacha" + i, L.T("Machine ", "Machine ") + (i + 1),
                    L.T($"Skins {1 + 5 * i} à {5 + 5 * i}", $"Skins {1 + 5 * i} to {5 + 5 * i}"), false,
                    new Btn(L.T("Lâcher", "Drop"), ButtonKind.Primary), L.T("Y aller", "Go"));
                if (b == 0)
                {
                    am.CmdRequestGachaponSphere(i);
                    Toasts.Show(L.T($"Une boule tombe de la machine {i + 1}.", $"A capsule drops from machine {i + 1}."));
                }
                if (b == 1)
                {
                    GoToMachine(machines[i]);
                    Toasts.Show(L.T($"Téléporté devant la machine {i + 1}.", $"Teleported in front of machine {i + 1}."));
                }
            }

            ui.Section(L.T("Skins de balai", "Broom skins"));
            int unlocked = 0;
            foreach (bool u in am.unlockedArray) if (u) unlocked++;
            ui.Stat(L.T("Débloqués", "Unlocked"), L.T($"{unlocked} sur {am.unlockedArray.Length}", $"{unlocked} of {am.unlockedArray.Length}"));
            ui.Stat(L.T("Skin équipé", "Equipped skin"), "#" + CurrentSkin(am));
            int cycle = ui.Buttons("broom.cycle", false, L.T("Skin précédent", "Previous skin"), L.T("Skin suivant", "Next skin"));
            if (cycle >= 0)
            {
                am.SetNextUnlockable(cycle == 1);
                Toasts.Show(L.T("Skin de balai n° " + CurrentSkin(am) + " équipé.", "Broom skin #" + CurrentSkin(am) + " equipped."));
            }
            if (unlocked < am.unlockedArray.Length &&
                ui.Buttons("broom.unlock", false, new Btn(L.T("Débloquer tous les skins", "Unlock every skin"), ButtonKind.Primary)) == 0)
                UnlockAll(am);
            ui.Note(L.T("Le skin équipé est visible par les autres joueurs. Si tu es l'hôte, il habille aussi le balai de tes agents de sécurité.",
                "Other players can see your equipped skin. If you are the host, your security guards' brooms use it too."));
        }
    }
}
