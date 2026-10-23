using System.Globalization;
using HarmonyLib;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    internal static class Store
    {
        public static bool MoneyLock;
        private static float _lockFloor;
        private static float _nextLockCheck;
        private static string _amount = "50000";

        public static void Tick()
        {
            if (!MoneyLock || !Game.InGame || Time.unscaledTime < _nextLockCheck) return;
            _nextLockCheck = Time.unscaledTime + 0.5f;
            var gd = GameData.Instance;
            // Top up through the game's own command so it also works when you are not the host.
            if (gd.gameFunds < _lockFloor - 0.5f) gd.CmdAlterFundsWithoutExperience(_lockFloor - gd.gameFunds);
        }

        public static void Reset() => MoneyLock = false;

        private static void AddMoney(float amount)
        {
            if (!Game.InGame || amount <= 0f) return;
            GameData.Instance.CmdAlterFundsWithoutExperience(amount);
            Toasts.Show(L.T($"{Game.Money(amount)} ajoutés à la caisse.", $"{Game.Money(amount)} added to the till."));
        }

        private static void SetMoney(float amount)
        {
            if (!Game.InGame) return;
            GameData.Instance.NetworkgameFunds = Mathf.Clamp(Mathf.Round(amount * 100f) / 100f, 0f, 2.14E+09f);
            Toasts.Show(L.T($"Caisse fixée à {Game.Money(amount)}.", $"Till set to {Game.Money(amount)}."));
        }

        private static void AddFranchisePoints(int n)
        {
            var gd = GameData.Instance;
            gd.NetworkgameFranchisePoints = gd.gameFranchisePoints + n;
            Toasts.Show(L.T($"+{n} points de franchise.", $"+{n} franchise points."));
        }

        private static void LevelUp()
        {
            var gd = GameData.Instance;
            int threshold = 0;
            for (int i = 1; i < 10000; i++)
            {
                threshold += i * 100;
                if (threshold > gd.gameFranchiseExperience) break;
            }
            gd.NetworkgameFranchiseExperience = threshold;
            Toasts.Show(L.T($"Franchise passée au niveau {gd.gameFranchiseLevel}.", $"Franchise is now level {gd.gameFranchiseLevel}."));
        }

        /// <summary>Tops up every shelf row that already holds a product, mirroring how employees restock.</summary>
        private static void FillShelves()
        {
            var npc = NPC_Manager.Instance;
            if (!npc || !npc.shelvesOBJ) return;
            int rows = 0;
            var shelves = npc.shelvesOBJ.transform;
            for (int s = 0; s < shelves.childCount; s++)
            {
                var dc = shelves.GetChild(s).GetComponent<Data_Container>();
                if (!dc || dc.productInfoArray == null) continue;
                var arr = dc.productInfoArray;
                for (int j = 0; j + 1 < arr.Length; j += 2)
                {
                    int pid = arr[j];
                    if (pid < 0 || pid >= ProductListing.Instance.productsData.Length) continue;
                    int max = npc.GetMaxProductsPerRow(s, pid);
                    if (arr[j + 1] >= max) continue;
                    arr[j + 1] = max;
                    Traverse.Create(dc).Method("RpcUpdateObjectOnClients", j, pid, max, pid).GetValue();
                    rows++;
                }
            }
            Toasts.Show(rows > 0 ? L.T($"{rows} rangées remplies.", $"{rows} rows filled.")
                : L.T("Tous les rayons sont déjà pleins.", "Every shelf is already full."));
        }

        private static void UnlockAllProducts()
        {
            var pl = ProductListing.Instance;
            if (!pl || pl.unlockedProductTiers == null) return;
            var all = new bool[pl.unlockedProductTiers.Length];
            for (int i = 0; i < all.Length; i++) all[i] = true;
            pl.NetworkunlockedProductTiers = all;
            pl.updateSkillState();
            pl.updateProductList();
            Toasts.Show(L.T("Toutes les gammes de produits sont débloquées.", "Every product range is unlocked."));
        }

        public static void Draw(Ui ui)
        {
            bool inGame = Game.InGame;
            bool host = Game.IsHost;
            var gd = GameData.Instance;

            if (inGame)
            {
                ui.Section(L.T("Situation", "Overview"));
                ui.Stat(L.T("Caisse", "Till"), Game.Money(gd.gameFunds));
                ui.Stat("Franchise", L.T($"niveau {gd.gameFranchiseLevel}, {gd.gameFranchisePoints} points",
                    $"level {gd.gameFranchiseLevel}, {gd.gameFranchisePoints} points"));
                ui.Stat(L.T("Jour", "Day"), gd.gameDay + (gd.isSupermarketOpen ? L.T(", magasin ouvert", ", store open") : L.T(", magasin fermé", ", store closed")));
            }

            ui.Section(L.T("Argent", "Money"));
            int quick = ui.Buttons("money.quick", false, "+1 000", "+10 000", "+100 000", "+1 000 000");
            if (quick >= 0) AddMoney(Mathf.Pow(10, quick + 3));
            _amount = ui.TextField("money.amount", L.T("Montant libre", "Custom amount"), _amount);
            float.TryParse(_amount.Replace(" ", "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount);
            int act = ui.Buttons("money.act", false, new Btn(L.T("Ajouter ce montant", "Add this amount"), ButtonKind.Primary),
                L.T("Fixer la caisse à ce montant", "Set the till to this amount"));
            if (act == 0) AddMoney(amount);
            if (act == 1)
            {
                if (!host) Toasts.Show(Ui.HostOnlyMessage);
                else if (inGame) SetMoney(amount);
            }
            bool lockWas = MoneyLock;
            MoneyLock = ui.Toggle("money.lock", L.T("Argent infini", "Infinite money"),
                L.T("La caisse ne descend jamais sous son montant au moment de l'activation.",
                    "The till never drops below the amount it held when you switched this on."), MoneyLock);
            if (MoneyLock && !lockWas)
            {
                if (!inGame) MoneyLock = false;
                else _lockFloor = gd.gameFunds;
            }

            ui.Section("Franchise");
            int fr = ui.Buttons("franchise", !host, "+1 point", "+5 points", "+25 points", L.T("Niveau suivant", "Next level"));
            if (inGame && host)
            {
                if (fr == 0) AddFranchisePoints(1);
                if (fr == 1) AddFranchisePoints(5);
                if (fr == 2) AddFranchisePoints(25);
                if (fr == 3) LevelUp();
            }
            if (ui.Buttons("unlock", !host, new Btn(L.T("Débloquer toutes les gammes de produits", "Unlock every product range"), ButtonKind.Primary)) == 0 && inGame && host)
                UnlockAllProducts();

            ui.Section(L.T("Rayons", "Shelves"));
            if (ui.Buttons("shelves", !host, new Btn(L.T("Remplir tous les rayons", "Fill every shelf"), ButtonKind.Primary)) == 0 && inGame && host)
                FillShelves();
            ui.Note(L.T("Complète chaque rangée qui contient déjà un produit, gratuitement. Les rangées vides ne changent pas.",
                "Tops up every row that already holds a product, for free. Empty rows stay as they are."));

            ui.Section(L.T("Journée", "Day"));
            int day = ui.Buttons("day", false, L.T("Ouvrir le magasin", "Open the store"), L.T("Fermer maintenant", "Close now"),
                L.T("Finir la journée", "End the day"));
            if (inGame)
            {
                if (day == 0) { gd.CmdOpenSupermarket(); Toasts.Show(L.T("Ouverture du magasin demandée.", "Store opening requested.")); }
                if (day == 1)
                {
                    if (!host) Toasts.Show(Ui.HostOnlyMessage);
                    else if (!gd.isSupermarketOpen) Toasts.Show(L.T("Le magasin est déjà fermé.", "The store is already closed."));
                    else { gd.NetworktimeOfDay = 22.55f; Toasts.Show(L.T("Il est 22 h 30 passées : le magasin ferme.", "It's past 10:30 PM: the store is closing.")); }
                }
                if (day == 2)
                {
                    if (gd.isSupermarketOpen || gd.timeOfDay <= 22f)
                        Toasts.Show(L.T("Ferme d'abord le magasin : la journée se termine après 22 h.", "Close the store first: the day can only end after 10 PM."));
                    else { gd.CmdEndDayFromButton(); Toasts.Show(L.T("Fin de journée lancée.", "Ending the day.")); }
                }
            }
        }
    }
}
