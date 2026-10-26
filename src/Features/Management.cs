using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using SupermarketTrainer.UI;
using Mirror;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    /// <summary>
    /// Expansions, franchise perks, bills and prices. Purchases go through the game's own
    /// commands, which check and take the money on the host: we send the exact cost first, so
    /// the till ends where it started and it also works when you are not the host.
    /// </summary>
    internal static class Management
    {
        private static string _storeName;
        private static readonly Color[] SignColors =
        {
            new Color(0.61f, 1f, 0.04f), new Color(1f, 0.81f, 0.2f), new Color(1f, 0.33f, 0.36f), new Color(1f, 0.55f, 0.1f),
            new Color(0.25f, 0.65f, 1f), new Color(0.75f, 0.4f, 1f), new Color(1f, 0.45f, 0.8f), new Color(1f, 1f, 1f),
        };
        private static readonly string[] SignColorFr = { "Vert", "Jaune", "Rouge", "Orange", "Bleu", "Violet", "Rose", "Blanc" };
        private static readonly string[] SignColorEn = { "Green", "Yellow", "Red", "Orange", "Blue", "Purple", "Pink", "White" };

        private static GameData Gd => GameData.Instance;
        private static UpgradesManager Upgrades => Gd ? Gd.GetComponent<UpgradesManager>() : null;
        private static DebtManager Debts => Gd ? Gd.GetComponent<DebtManager>() : null;

        private static void Fund(float amount)
        {
            if (amount > 0f) Gd.CmdAlterFundsWithoutExperience(Mathf.Ceil(amount));
        }

        private static int Count(bool[] values)
        {
            int n = 0;
            if (values != null) foreach (bool v in values) if (v) n++;
            return n;
        }

        private static int Count(bool[] values, int slots)
        {
            int n = 0;
            for (int i = 0; i < slots; i++) if (values[i]) n++;
            return n;
        }

        /// <summary>
        /// Expansions that really exist in the map. A save can hold more slots than the map has;
        /// the host accepts those, but every client then fails to show them and drops out of the game.
        /// </summary>
        internal static int Slots(UpgradesManager um, int group, bool[] values)
        {
            if (values == null) return 0;
            int n = values.Length;
            foreach (var parent in new[] { um.expansionsParentOBJ, um.expansionsUIParentOBJ, um.expansionsUIButtonsParentOBJ })
            {
                if (!parent || parent.transform.childCount <= group) return 0;
                n = Mathf.Min(n, parent.transform.GetChild(group).childCount);
            }
            return n;
        }

        // ---- Expansions --------------------------------------------------------------------

        /// <summary>Buys every missing store and storage expansion, then every department.</summary>
        internal static int BuyExpansions(bool store, bool storage, bool departments, int limit = int.MaxValue)
        {
            var um = Upgrades;
            if (!um) return 0;
            int bought = 0;
            // Prices rise with each purchase, exactly as the host computes them (UpgradesManager.CmdAddSpace).
            if (store && um.storeSpaceUpgrades != null)
            {
                int owned = Count(um.storeSpaceUpgrades), slots = Slots(um, 0, um.storeSpaceUpgrades);
                for (int i = 0; i < slots && bought < limit; i++)
                {
                    if (um.storeSpaceUpgrades[i]) continue;
                    Fund(250 + owned++ * 250);
                    um.CmdAddSpace(i);
                    bought++;
                }
            }
            if (storage && um.storageSpaceUpgrades != null)
            {
                int owned = Count(um.storageSpaceUpgrades), slots = Slots(um, 1, um.storageSpaceUpgrades);
                for (int i = 0; i < slots && bought < limit; i++)
                {
                    if (um.storageSpaceUpgrades[i]) continue;
                    Fund(2000 + owned++ * 1000);
                    um.CmdAddStorage(i);
                    bought++;
                }
            }
            if (departments)
            {
                int slots = Departments(um);
                for (int i = 0; i < slots && bought < limit; i++)
                {
                    if (um.addonsBought[i]) continue;
                    Fund(um.spaceAcquisitionComponent.departmentsCosts[i]);
                    um.CmdAddAddon(i);
                    bought++;
                }
            }
            return bought;
        }

        internal static int Departments(UpgradesManager um)
        {
            if (!um.spaceAcquisitionComponent || um.spaceAcquisitionComponent.departmentsCosts == null || !um.addonsDepartmentsParentOBJ) return 0;
            return Mathf.Min(Slots(um, 2, um.addonsBought), um.spaceAcquisitionComponent.departmentsCosts.Length,
                um.addonsDepartmentsParentOBJ.transform.childCount);
        }

        /// <summary>Slots of a group the game can show; a department also opens a door in its own parent.</summary>
        internal static int Showable(UpgradesManager um, int group, bool[] values)
        {
            int n = Slots(um, group, values);
            return group == 2 && um.addonsDepartmentsParentOBJ ? Mathf.Min(n, um.addonsDepartmentsParentOBJ.transform.childCount) : n;
        }

        private static GameData _repaired;

        /// <summary>
        /// Version 1.1.0 could buy slots that are missing from the map. The game stops applying a save's
        /// expansions at the first such slot, so the departments after it stay closed for everyone.
        /// The host clears them once per game; nothing changes in the map since they don't exist.
        /// </summary>
        internal static int RepairMissingSlots()
        {
            var um = Upgrades;
            if (!um || !NetworkServer.active) return 0;
            return ClearFrom(um.storeSpaceUpgrades, Showable(um, 0, um.storeSpaceUpgrades), v => um.NetworkstoreSpaceUpgrades = v)
                + ClearFrom(um.storageSpaceUpgrades, Showable(um, 1, um.storageSpaceUpgrades), v => um.NetworkstorageSpaceUpgrades = v)
                + ClearFrom(um.addonsBought, Showable(um, 2, um.addonsBought), v => um.NetworkaddonsBought = v);
        }

        private static int ClearFrom(bool[] values, int from, System.Action<bool[]> set)
        {
            if (values == null) return 0;
            var copy = (bool[])values.Clone();
            int n = 0;
            for (int i = from; i < copy.Length; i++) if (copy[i]) { copy[i] = false; n++; }
            if (n > 0) set(copy);
            return n;
        }

        public static void Tick()
        {
            if (!NetworkServer.active || !Gd || _repaired == Gd || !Upgrades) return;
            _repaired = Gd;
            int n = RepairMissingSlots();
            if (n > 0) Toasts.Show(L.T($"Sauvegarde réparée : {n} agrandissement(s) qui n'existent pas retiré(s). Les joueurs déjà là doivent revenir pour voir les départements.",
                $"Save repaired: {n} expansion(s) that don't exist removed. Players already here must rejoin to see the departments."));
        }

        // ---- Perks -------------------------------------------------------------------------

        /// <summary>The perk tree as the game lays it out; extra slots in the save array have no UI and no effect.</summary>
        internal static int PerkCount(UpgradesManager um) =>
            um.UIPerksParent ? Mathf.Min(um.extraUpgrades.Length, um.UIPerksParent.transform.childCount) : 0;

        internal static int UnlockPerks()
        {
            var um = Upgrades;
            if (!um || um.extraUpgrades == null) return 0;
            int n = 0;
            for (int i = 0; i < PerkCount(um); i++)
            {
                if (um.extraUpgrades[i]) continue;
                um.CmdAcquirePerk(i, 0); // zero franchise points spent
                n++;
            }
            return n;
        }

        // ---- Bills -------------------------------------------------------------------------

        private static bool ParseInvoice(string data, out int type, out float amount)
        {
            type = -1; amount = 0f;
            if (string.IsNullOrEmpty(data)) return false;
            var parts = data.Split('|');
            if (parts.Length < 2 || !int.TryParse(parts[0], out type)) return false;
            // Written with the game's comma decimal separator (see DebtManager.GetInvoiceDataValues).
            return float.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
        }

        internal static void UnpaidBills(out int count, out float total)
        {
            count = 0; total = 0f;
            var dm = Debts;
            if (!dm || dm.currentInvoicesData == null) return;
            foreach (var data in dm.currentInvoicesData)
                if (ParseInvoice(data, out _, out float amount)) { count++; total += amount; }
        }

        internal static int PayBills()
        {
            var dm = Debts;
            if (!dm || dm.currentInvoicesData == null) return 0;
            int n = 0;
            for (int i = 0; i < dm.currentInvoicesData.Length; i++)
            {
                if (!ParseInvoice(dm.currentInvoicesData[i], out _, out float amount)) continue;
                Fund(amount);
                Traverse.Create(dm).Method("CmdPayInvoice", i).GetValue();
                n++;
            }
            return n;
        }

        internal static bool ClearLoan()
        {
            var dm = Debts;
            if (!dm || dm.loanAmount <= 0) return false;
            Fund(dm.loanAmount);
            Traverse.Create(dm).Method("CmdClearLoan").GetValue();
            return true;
        }

        // ---- Prices ------------------------------------------------------------------------

        /// <summary>
        /// Customers walk away when a price is above the market price times a random 2.01 to 2.5
        /// (NPC_Manager), so ×2 is the highest price nobody refuses. Rounded down to the cent.
        /// </summary>
        internal static int SetPrices(float factor)
        {
            var pl = ProductListing.Instance;
            if (!pl || pl.productsData == null) return 0;
            int n = 0;
            for (int id = 0; id < pl.productsData.Length && id < pl.productPlayerPricing.Length; id++)
            {
                var p = pl.productsData[id];
                if (p == null || p.productTier < 0 || p.productTier >= pl.tierInflation.Length) continue;
                float market = p.basePricePerUnit * pl.tierInflation[p.productTier];
                float price = Mathf.Max(0.01f, Mathf.Floor(market * factor * 100f) / 100f);
                if (Mathf.Approximately(pl.productPlayerPricing[id], price)) continue;
                pl.CmdUpdateProductPrice(id, price);
                n++;
            }
            return n;
        }

        // ---- Demolition --------------------------------------------------------------------

        private static DemolishableManager Demolish => Gd ? Gd.GetComponent<DemolishableManager>() : null;

        /// <summary>Group 0 holds the pillars and group 1 the beams, as the demolition achievement counts them.</summary>
        internal static int DemolishGroups(DemolishableManager dm) =>
            dm && dm.demolishableParentRootOBJ && dm.demolishableValues != null && dm.demolishingCosts != null
                ? Mathf.Min(dm.demolishableParentRootOBJ.transform.childCount, dm.demolishableValues.Length, dm.demolishingCosts.Length) : 0;

        internal static void Demolished(DemolishableManager dm, int group, out int done, out int total)
        {
            var parent = dm.demolishableParentRootOBJ.transform.GetChild(group);
            total = parent.childCount;
            done = 0;
            foreach (Transform t in parent) if (!t.gameObject.activeSelf) done++;
        }

        /// <summary>
        /// Demolishes every standing item of a group. The host takes the cost before checking the
        /// item is still standing, so only standing ones are sent, each with its cost put in the till.
        /// </summary>
        internal static int DemolishAll(int group)
        {
            var dm = Demolish;
            if (group >= DemolishGroups(dm)) return 0;
            var parent = dm.demolishableParentRootOBJ.transform.GetChild(group);
            string state = dm.demolishableValues[group] ?? "";
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (!parent.GetChild(i).gameObject.activeSelf || (i < state.Length && state[i] != '0')) continue;
                Fund(dm.demolishingCosts[group]);
                dm.CmdDemolishItem(group, i);
                n++;
            }
            return n;
        }

        internal static int DebrisLeft()
        {
            int n = 0;
            foreach (var d in Object.FindObjectsByType<DemolishDebrisControl>(FindObjectsSortMode.None))
                if (d && d.debrisParentOBJ) foreach (Transform t in d.debrisParentOBJ.transform) if (t.gameObject.activeSelf) n++;
            return n;
        }

        /// <summary>Clears the rubble left by demolitions, piece by piece as the hammer does. Free in the game too.</summary>
        internal static int ClearDebris()
        {
            int n = 0;
            foreach (var d in Object.FindObjectsByType<DemolishDebrisControl>(FindObjectsSortMode.None))
            {
                if (!d || !d.debrisParentOBJ) continue;
                var parent = d.debrisParentOBJ.transform;
                for (int i = 0; i < parent.childCount; i++)
                    if (parent.GetChild(i).gameObject.activeSelf) { d.CmdRemoveDebris(i); n++; }
            }
            return n;
        }

        // ---- UI ----------------------------------------------------------------------------

        public static void Draw(Ui ui)
        {
            bool inGame = Game.InGame, host = Game.IsHost;
            var um = Upgrades;
            if (!inGame || !um)
            {
                _storeName = null;
                ui.Note(L.T("Rejoins ou héberge une partie pour gérer ton magasin.", "Join or host a game to manage your store."));
                return;
            }

            ui.Section(L.T("Agrandissements", "Expansions"));
            int stores = Slots(um, 0, um.storeSpaceUpgrades), storages = Slots(um, 1, um.storageSpaceUpgrades);
            ui.Stat(L.T("Magasin", "Store"), $"{Count(um.storeSpaceUpgrades, stores)} / {stores}");
            ui.Stat(L.T("Entrepôt", "Storage"), $"{Count(um.storageSpaceUpgrades, storages)} / {storages}");
            int deps = Departments(um), depsOwned = 0;
            for (int i = 0; i < deps; i++) if (um.addonsBought[i]) depsOwned++;
            ui.Stat(L.T("Départements", "Departments"), $"{depsOwned} / {deps}");
            int ex = ui.Buttons("mg.expand", false,
                L.T("+1 magasin", "+1 store"), L.T("+1 entrepôt", "+1 storage"),
                new Btn(L.T("Tout acheter", "Buy everything"), ButtonKind.Primary));
            if (ex >= 0)
            {
                int n = ex == 0 ? BuyExpansions(true, false, false, 1)
                    : ex == 1 ? BuyExpansions(false, true, false, 1)
                    : BuyExpansions(true, true, true);
                Toasts.Show(n == 0 ? L.T("Tout est déjà acheté.", "Everything is already bought.")
                    : L.T($"{n} agrandissement(s) acheté(s) gratuitement.", $"{n} expansion(s) bought for free."));
            }
            ui.Note(L.T("Le prix est ajouté à la caisse juste avant chaque achat : ton argent ne bouge pas. Les départements demandent certains agrandissements, c'est pourquoi « Tout acheter » les prend en dernier.",
                "The price is put in the till right before each purchase, so your money stays the same. Departments need some expansions first, which is why \"Buy everything\" takes them last."));

            ui.Section(L.T("Avantages de franchise", "Franchise perks"));
            int perks = PerkCount(um), perksOwned = 0;
            for (int i = 0; i < perks; i++) if (um.extraUpgrades[i]) perksOwned++;
            ui.Stat(L.T("Débloqués", "Unlocked"), $"{perksOwned} / {perks}");
            if (ui.Buttons("mg.perks", false, new Btn(L.T("Débloquer tous les avantages", "Unlock every perk"), ButtonKind.Primary)) == 0)
            {
                int n = UnlockPerks();
                Toasts.Show(n == 0 ? L.T("Tous les avantages sont déjà débloqués.", "Every perk is already unlocked.")
                    : L.T($"{n} avantage(s) débloqué(s), sans dépenser de points.", $"{n} perk(s) unlocked without spending points."));
            }
            ui.Note(L.T("Plus d'employés, employés plus rapides, plus de clients, plus de soldes, horloge de contrôle du temps…",
                "More employees, faster staff, more customers, more sales at once, the time control clock…"));

            ui.Section(L.T("Factures et prêt", "Bills and loan"));
            var dm = Debts;
            UnpaidBills(out int bills, out float billsTotal);
            ui.Stat(L.T("Factures à payer", "Unpaid bills"), bills == 0 ? L.T("aucune", "none") : $"{bills}, {Game.Money(billsTotal)}");
            ui.Stat(L.T("Prêt restant", "Loan left"), dm && dm.loanAmount > 0 ? Game.Money(dm.loanAmount) : L.T("aucun", "none"));
            int debt = ui.Buttons("mg.debt", false,
                new Btn(L.T("Payer toutes les factures", "Pay every bill"), ButtonKind.Primary),
                L.T("Rembourser le prêt", "Pay off the loan"));
            if (debt == 0)
            {
                int n = PayBills();
                Toasts.Show(n == 0 ? L.T("Aucune facture à payer.", "No bills to pay.")
                    : L.T($"{n} facture(s) payée(s) gratuitement.", $"{n} bill(s) paid for free."));
            }
            if (debt == 1)
                Toasts.Show(ClearLoan() ? L.T("Prêt remboursé gratuitement.", "Loan paid off for free.") : L.T("Tu n'as pas de prêt en cours.", "You have no loan."));
            ui.Note(L.T("Payer évite les conséquences des retards : agents de recouvrement, grève des employés.",
                "Paying avoids what late bills bring: debt collectors and striking employees."));

            ui.Section(L.T("Prix", "Prices"));
            int pr = ui.Buttons("mg.prices", false, L.T("Prix du marché", "Market price"), "×1,5", new Btn(L.T("×2, le maximum", "×2, the maximum"), ButtonKind.Primary));
            if (pr >= 0)
            {
                float f = pr == 0 ? 1f : pr == 1 ? 1.5f : 2f;
                int n = SetPrices(f);
                Toasts.Show(L.T($"{n} prix modifié(s).", $"{n} price(s) changed."));
            }
            ui.Note(L.T("Règle le prix de vente de tous les produits d'un coup, par rapport au prix du marché. Au-delà de ×2, des clients trouvent le produit trop cher et le reposent.",
                "Sets the selling price of every product at once, relative to the market price. Above ×2, some customers find it too expensive and leave it on the shelf."));

            var dem = Demolish;
            int groups = DemolishGroups(dem);
            if (groups >= 2)
            {
                ui.Section(L.T("Démolition  ·  en test", "Demolition  ·  testing"));
                Demolished(dem, 0, out int pillarsDone, out int pillars);
                Demolished(dem, 1, out int beamsDone, out int beams);
                ui.Stat(L.T("Piliers démolis", "Pillars demolished"), $"{pillarsDone} / {pillars}");
                ui.Stat(L.T("Poutres démolies", "Beams demolished"), $"{beamsDone} / {beams}");
                int debris = DebrisLeft();
                ui.Stat(L.T("Gravats au sol", "Rubble on the floor"), debris == 0 ? L.T("aucun", "none") : debris.ToString());
                int d = ui.Buttons("mg.demolish", false, new Btn(L.T("Démolir les piliers", "Demolish pillars"), ButtonKind.Primary),
                    L.T("Démolir les poutres", "Demolish beams"), L.T("Enlever les gravats", "Clear the rubble"));
                if (d == 0 || d == 1)
                {
                    int n = DemolishAll(d);
                    Toasts.Show(n == 0 ? L.T("Tout est déjà démoli.", "Everything is already demolished.")
                        : L.T($"{n} élément(s) démoli(s) gratuitement. Les gravats tombent dans la seconde.", $"{n} item(s) demolished for free. The rubble lands within a second."));
                }
                if (d == 2)
                {
                    int n = ClearDebris();
                    Toasts.Show(n == 0 ? L.T("Il n'y a pas de gravats au sol.", "There's no rubble on the floor.")
                        : L.T($"{n} morceau(x) de gravats enlevé(s).", $"{n} piece(s) of rubble cleared."));
                }
                ui.Note(L.T("Comme au marteau de démolition, sans payer. Les gravats arrivent juste après la démolition : enlève-les ensuite.",
                    "Like the demolition hammer, without paying. The rubble lands right after the demolition: clear it afterwards."));
            }

            ui.Section(L.T("Enseigne", "Store sign"));
            var spawner = Gd.GetComponent<NetworkSpawner>();
            if (_storeName == null && spawner) _storeName = spawner.SuperMarketName;
            _storeName = ui.TextField("mg.name", L.T("Nom du magasin", "Store name"), _storeName ?? "", 240);
            if (ui.Buttons("mg.rename", !host, new Btn(L.T("Changer le nom", "Rename the store"), ButtonKind.Primary)) == 0 && host && spawner)
            {
                string name = (_storeName ?? "").Trim();
                if (name.Length == 0) Toasts.Show(L.T("Écris d'abord un nom.", "Type a name first."));
                else { spawner.CmdSetSupermarketText(name); Toasts.Show(L.T($"Le magasin s'appelle maintenant {name}.", $"The store is now called {name}.")); }
            }
            var labels = new Btn[4];
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 4; i++) labels[i] = L.T(SignColorFr[row * 4 + i], SignColorEn[row * 4 + i]);
                int c = ui.Buttons("mg.color" + row, !host, labels);
                if (c >= 0 && host && spawner)
                {
                    spawner.CmdSetSupermarketColor(SignColors[row * 4 + c]);
                    Toasts.Show(L.T("Couleur de l'enseigne : ", "Sign colour: ") + labels[c].Label.ToLowerInvariant() + ".");
                }
            }
            ui.Note(L.T("Seules les lettres prises en charge par le jeu s'affichent sur l'enseigne. Les lettres brillent à partir de 19 h.",
                "Only letters the game supports show up on the sign. The letters light up from 7 PM."));
        }
    }

    /// <summary>
    /// Skips expansion slots that are missing from the map when the game applies a save, instead of
    /// throwing there and leaving every later expansion and department unapplied.
    /// </summary>
    [HarmonyPatch]
    internal static class MissingSlotPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UpgradesManager), "SetSingleStoreSpace");
            yield return AccessTools.Method(typeof(UpgradesManager), "SetSingleStorageSpace");
            yield return AccessTools.Method(typeof(UpgradesManager), "SetSingleAddonSpace");
        }

        private static bool Prefix(UpgradesManager __instance, MethodBase __originalMethod, int spaceIndex,
            GameObject spaceParentOBJ, GameObject spaceUIParentOBJ, GameObject spaceUIButtonParentOBJ)
        {
            int n = Mathf.Min(spaceParentOBJ.transform.childCount, spaceUIParentOBJ.transform.childCount, spaceUIButtonParentOBJ.transform.childCount);
            if (__originalMethod.Name == "SetSingleAddonSpace" && __instance.addonsDepartmentsParentOBJ)
                n = Mathf.Min(n, __instance.addonsDepartmentsParentOBJ.transform.childCount);
            return spaceIndex >= 0 && spaceIndex < n;
        }
    }
}
