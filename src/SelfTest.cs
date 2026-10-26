#if SELFTEST
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using SupermarketTrainer.Features;
using UnityEngine;

namespace SupermarketTrainer
{
    /// <summary>
    /// Automated in-game check, only compiled with -p:SelfTest=true. Hosts a local game on the
    /// throwaway save slot <see cref="SaveName"/> (a copy prepared by the caller),
    /// exercises the new features, saves screenshots and a PASS/FAIL report, then quits without saving.
    /// </summary>
    internal sealed class SelfTest : MonoBehaviour
    {
        private ManualLogSource _log;
        private string _dir;
        private const string SaveName = "StoreFile9.es3";
        private int _pass, _fail, _errors;
        private Language _language;

        public static void Start(GameObject host, ManualLogSource log)
        {
            var t = host.AddComponent<SelfTest>();
            t._log = log;
        }

        private void Start()
        {
            _dir = Path.Combine(Paths.BepInExRootPath, "selftest");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            StartCoroutine(Script());
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if ((stack ?? "").Contains("SupermarketTrainer") || condition.Contains("SupermarketTrainer"))
            {
                _errors++;
                _log.LogError("[selftest] trainer error: " + condition + "\n" + stack);
            }
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            _log.LogInfo($"[selftest] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        // Commands sent by the host are processed by Mirror on a later frame.
        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(0.6f); }

        private static int Owned(bool[] a) => a.Count(b => b);

        private IEnumerator Script()
        {
            string save = SaveName;
            _language = L.Setting.Value;
            _log.LogInfo("[selftest] waiting for the main menu, save " + save);
            yield return Until(() => FindAnyObjectByType<CustomNetworkManager>() && FsmVariables.GlobalVariables.FindFsmString("CurrentFilename") != null, 120f);
            yield return new WaitForSecondsRealtime(8f);
            if (!File.Exists(Path.Combine(Application.persistentDataPath, save))) { Check("test save present", false); Finish(); yield break; }

            L.Setting.Value = Language.Francais;
            Menu.Open();
            Menu.Select(3);
            yield return Shot("00_mainmenu_management");
            Menu.Close();

            // ---- Host the copied save locally, as the menu's "Local host" does ----
            FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value = save;
            var autosave = FsmVariables.GlobalVariables.FindFsmBool("LoadingFromAutosave");
            if (autosave != null) autosave.Value = false;
            FindAnyObjectByType<CustomNetworkManager>().LocalHost();
            yield return Until(() => Game.InGame && Game.IsHost, 120f);
            Check("enter game as host", Game.InGame && Game.IsHost);
            if (!Game.InGame) { Finish(); yield break; }
            yield return new WaitForSecondsRealtime(12f);

            var gd = GameData.Instance;
            var um = gd.GetComponent<UpgradesManager>();
            var dm = gd.GetComponent<DebtManager>();
            var npc = NPC_Manager.Instance;
            var pl = ProductListing.Instance;
            _log.LogInfo($"[selftest] day {gd.gameDay}, funds {gd.gameFunds}, store {Owned(um.storeSpaceUpgrades)}/{um.storeSpaceUpgrades.Length}, storage {Owned(um.storageSpaceUpgrades)}/{um.storageSpaceUpgrades.Length}, perks {Owned(um.extraUpgrades)}/{um.extraUpgrades.Length} ui {Management.PerkCount(um)}, maxEmployees {npc.maxEmployees}, slots {Staff.MaxHireSlots(npc)}");
            _log.LogInfo("[selftest] weather symbols: " + string.Join(", ", gd.weatherSymbols.Select(s => s ? s.name : "null")) + " / objs " + gd.weatherRelatedOBJs.Length + ": " + string.Join(", ", gd.weatherRelatedOBJs.Select(o => o ? o.name : "null")));

            // ---- Menu tabs (font fix and new sections) ----
            Menu.Open();
            for (int i = 0; i < Menu.TabCount; i++)
            {
                Menu.Select(i);
                yield return Shot($"1{i}_tab");
            }
            Menu.Select(2, 99999);
            yield return Shot("20_store_boxes");
            Menu.Select(3, 400);
            yield return Shot("21_management_mid");
            Menu.Select(3, 99999);
            yield return Shot("22_management_bottom");
            Menu.Select(4, 99999);
            yield return Shot("23_staff_hiring");
            Menu.Select(5, 0);
            yield return Shot("24_world_weather");
            L.Setting.Value = Language.English;
            Menu.Select(3);
            yield return Shot("25_english_management");
            L.Setting.Value = Language.Francais;
            Menu.Close();
            yield return new WaitForSecondsRealtime(0.5f);

            // ---- Free boxes ----
            var mb = gd.GetComponent<ManagerBlackboard>();
            int boxesBefore = mb.boxParent.childCount;
            int product = pl.availableProducts.Count > 0 ? pl.availableProducts[0] : 0;
            Store.SpawnBoxes(product, 3);
            yield return Settle();
            int boxesAfter = mb.boxParent.childCount;
            Check("spawn 3 boxes", boxesAfter == boxesBefore + 3, $"{boxesBefore} -> {boxesAfter}");
            var newest = mb.boxParent.GetChild(mb.boxParent.childCount - 1).GetComponent<BoxData>();
            Check("box holds the product, full", newest && newest.productID == product && newest.numberOfProducts == pl.productsData[product].maxItemsPerBox,
                newest ? $"{newest.productID}x{newest.numberOfProducts} ({Store.ProductName(product)})" : "none");
            Check("box in front of player", newest && Vector3.Distance(newest.transform.position, Game.Player.position) < 6f,
                newest ? Vector3.Distance(newest.transform.position, Game.Player.position).ToString("0.0") + " m" : "");
            yield return Shot("30_boxes");

            // ---- Expansions ----
            float funds = gd.gameFunds;
            int store0 = Owned(um.storeSpaceUpgrades);
            Management.BuyExpansions(true, false, false, 1);
            yield return Settle();
            Check("+1 store expansion", Owned(um.storeSpaceUpgrades) == store0 + 1, $"{store0} -> {Owned(um.storeSpaceUpgrades)}");
            Check("expansion was free", Mathf.Abs(gd.gameFunds - funds) <= 1f, $"{funds} -> {gd.gameFunds}");
            // Some saves hold more slots than the map has; buying those dropped every client out of the game.
            int stores = Management.Slots(um, 0, um.storeSpaceUpgrades), storages = Management.Slots(um, 1, um.storageSpaceUpgrades);
            var bigStore = um.storeSpaceUpgrades; System.Array.Resize(ref bigStore, stores + 5); um.storeSpaceUpgrades = bigStore;
            var bigStorage = um.storageSpaceUpgrades; System.Array.Resize(ref bigStorage, storages + 5); um.storageSpaceUpgrades = bigStorage;
            var bigAddons = um.addonsBought; System.Array.Resize(ref bigAddons, bigAddons.Length + 5); um.addonsBought = bigAddons;
            Management.BuyExpansions(true, true, true);
            yield return new WaitForSecondsRealtime(1.5f);
            int deps = Management.Departments(um);
            Check("every store expansion", um.storeSpaceUpgrades.Take(stores).All(b => b), $"{Owned(um.storeSpaceUpgrades)}/{stores}");
            Check("every storage expansion", um.storageSpaceUpgrades.Take(storages).All(b => b), $"{Owned(um.storageSpaceUpgrades)}/{storages}");
            Check("every department", deps > 0 && um.addonsBought.Take(deps).All(b => b), $"{Owned(um.addonsBought)}/{deps}");
            Check("slots missing from the map left alone", !um.storeSpaceUpgrades.Skip(stores).Any(b => b) && !um.storageSpaceUpgrades.Skip(storages).Any(b => b)
                && !um.addonsBought.Skip(deps).Any(b => b), $"store {stores}/{um.storeSpaceUpgrades.Length}, storage {storages}/{um.storageSpaceUpgrades.Length}, departments {deps}/{um.addonsBought.Length}");
            Check("still connected after buying everything", Mirror.NetworkClient.isConnected && Mirror.NetworkServer.active);
            // A save bought with 1.1.0 can hold a slot missing from the map: loading it must still open the departments.
            um.storageSpaceUpgrades[storages + 1] = true;
            string loadError = null;
            try { AccessTools.Method(typeof(UpgradesManager), "InitialSpaceSet").Invoke(um, null); }
            catch (Exception e) { loadError = (e.InnerException ?? e).Message; }
            Check("save with a missing slot still loads", loadError == null, loadError ?? "");
            Check("missing slot repaired", Management.RepairMissingSlots() == 1 && !um.storageSpaceUpgrades.Skip(storages).Any(b => b));
            Check("all expansions were free", Mathf.Abs(gd.gameFunds - funds) <= 25f, $"{funds} -> {gd.gameFunds}");
            Game.Teleport(Game.Player.position + Vector3.up * 0.2f);
            yield return Shot("31_expansions");

            // ---- Perks ----
            int points = gd.gameFranchisePoints, maxEmp = npc.maxEmployees;
            Management.UnlockPerks();
            yield return new WaitForSecondsRealtime(1.5f);
            int perks = Management.PerkCount(um);
            Check("every perk", um.extraUpgrades.Take(perks).All(b => b), $"{Owned(um.extraUpgrades)}/{perks}");
            Check("perks cost no points", gd.gameFranchisePoints == points, $"{points} -> {gd.gameFranchisePoints}");
            Check("perks raise max employees", npc.maxEmployees > maxEmp, $"{maxEmp} -> {npc.maxEmployees}");

            // ---- Bills and loan ----
            int slot = Array.IndexOf(dm.currentInvoicesData, "");
            if (slot >= 0) dm.currentInvoicesData[slot] = $"1|123,45|{gd.gameDay}|{gd.gameDay + 3}|0_0_0_0_0";
            Management.UnpaidBills(out int bills, out float billsTotal);
            Check("unpaid bill read", bills >= 1 && billsTotal >= 123f, $"{bills}, {billsTotal}");
            funds = gd.gameFunds;
            Management.PayBills();
            yield return Settle();
            Management.UnpaidBills(out bills, out _);
            Check("every bill paid", bills == 0, bills.ToString());
            Check("bills were free", gd.gameFunds >= funds - 0.01f && gd.gameFunds - funds <= billsTotal + 1f, $"{funds} -> {gd.gameFunds}");

            Traverse.Create(dm).Method("CmdRequestLoan", 1000, 1200, 120).GetValue();
            yield return Settle();
            Check("test loan taken", dm.loanAmount == 1200, dm.loanAmount.ToString());
            funds = gd.gameFunds;
            Management.ClearLoan();
            yield return Settle();
            Check("loan paid off", dm.loanAmount == 0 && dm.loanPaymentPerDay == 0, dm.loanAmount.ToString());
            Check("loan payoff was free", Mathf.Abs(gd.gameFunds - funds) <= 1f, $"{funds} -> {gd.gameFunds}");

            // ---- Prices ----
            float[] oldPrices = (float[])pl.productPlayerPricing.Clone();
            Management.SetPrices(2f);
            yield return Settle();
            int wrong = 0, checkedPrices = 0;
            for (int id = 0; id < pl.productsData.Length && id < pl.productPlayerPricing.Length; id++)
            {
                var p = pl.productsData[id];
                if (p == null || p.productTier < 0 || p.productTier >= pl.tierInflation.Length) continue;
                float market = p.basePricePerUnit * pl.tierInflation[p.productTier];
                checkedPrices++;
                if (pl.productPlayerPricing[id] > market * 2.01f || pl.productPlayerPricing[id] < market * 2f - 0.011f) wrong++;
            }
            Check("prices at ×2 and under the refusal threshold", wrong == 0 && checkedPrices > 0, $"{checkedPrices} checked, {wrong} wrong; #{product} {oldPrices[product]} -> {pl.productPlayerPricing[product]}");
            Management.SetPrices(1f);
            yield return Settle();
            var p0 = pl.productsData[product];
            float m0 = p0.basePricePerUnit * pl.tierInflation[p0.productTier];
            Check("market price", Mathf.Abs(pl.productPlayerPricing[product] - Mathf.Floor(m0 * 100f) / 100f) < 0.005f, $"{pl.productPlayerPricing[product]} vs {m0}");

            // ---- Store sign ----
            var spawner = gd.GetComponent<NetworkSpawner>();
            string oldName = spawner.SuperMarketName;
            Color oldColor = spawner.SuperMarketColor;
            spawner.CmdSetSupermarketText("ZENATO MARKET");
            spawner.CmdSetSupermarketColor(new Color(0.25f, 0.65f, 1f));
            yield return Settle();
            Check("store renamed", spawner.SuperMarketName == "ZENATO MARKET", spawner.SuperMarketName);
            Check("sign colour", Mathf.Abs(spawner.SuperMarketColor.b - 1f) < 0.01f, spawner.SuperMarketColor.ToString());
            Game.Teleport(Game.Player.position);
            yield return Shot("32_sign");
            spawner.CmdSetSupermarketText(oldName);
            spawner.CmdSetSupermarketColor(oldColor);
            yield return Settle();

            // ---- Hiring ----
            int baseMax = npc.maxEmployees;
            Staff.HireCapValue = 30f;
            Staff.HireCap = true;
            yield return null; yield return null;
            Check("custom hiring limit", npc.maxEmployees == Mathf.Min(30, Staff.MaxHireSlots(npc)), $"{baseMax} -> {npc.maxEmployees} (slots {Staff.MaxHireSlots(npc)})");
            Staff.HireCap = false;
            yield return null; yield return null;
            Check("hiring limit restored", npc.maxEmployees == baseMax, $"{npc.maxEmployees} vs {baseMax}");
            string before = string.Join(";", npc.todaysEmployeesData);
            int rerolls = npc.rerollTimes;
            Staff.NewCandidates();
            yield return Settle();
            Check("new candidates", string.Join(";", npc.todaysEmployeesData) != before, "");
            Check("rerolls not used up", npc.rerollTimes == rerolls, $"{rerolls} -> {npc.rerollTimes}");

            // ---- Weather ----
            int oldWeather = gd.weatherIndex;
            for (int w = 0; w < gd.weatherRelatedOBJs.Length; w++)
            {
                World.SetWeather(w);
                yield return Settle();
                Check("weather " + w, gd.weatherIndex == w, gd.weatherRelatedOBJs[w] ? gd.weatherRelatedOBJs[w].name : "none");
                if (w == 5 || w == 3) yield return Shot("33_weather_" + w);
            }
            World.SetWeather(oldWeather);

            // ---- Demolition ----
            var dem = gd.GetComponent<DemolishableManager>();
            int groups = Management.DemolishGroups(dem);
            Check("demolition groups", groups >= 2, groups.ToString());
            funds = gd.gameFunds;
            int sentP = Management.DemolishAll(0), sentB = Management.DemolishAll(1);
            yield return new WaitForSecondsRealtime(1.5f);
            Management.Demolished(dem, 0, out int pDone, out int pTotal);
            Management.Demolished(dem, 1, out int bDone, out int bTotal);
            Check("every pillar demolished", pTotal > 0 && pDone == pTotal, $"{pDone}/{pTotal}, {sentP} sent");
            Check("every beam demolished", bTotal > 0 && bDone == bTotal, $"{bDone}/{bTotal}, {sentB} sent");
            Check("demolition was free", Mathf.Abs(gd.gameFunds - funds) <= 25f, $"{funds} -> {gd.gameFunds}");
            Check("nothing left to demolish", Management.DemolishAll(0) + Management.DemolishAll(1) == 0);
            int rubble = Management.DebrisLeft();
            Game.Teleport(Game.Player.position + Vector3.up * 0.2f);
            yield return Shot("35_demolished");
            Management.ClearDebris();
            yield return new WaitForSecondsRealtime(1f);
            Check("rubble cleared", rubble > 0 && Management.DebrisLeft() == 0, $"{rubble} -> {Management.DebrisLeft()}");
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("36_rubble_cleared");
            Menu.Open();
            Menu.Select(3, 99999f);
            yield return Shot("37_management_demolition");
            Menu.Close();

            // ---- Event tokens ----
            int types = Tokens.Types;
            Check("token types", types >= 6, types.ToString());
            int t0 = Tokens.Owned(0);
            Tokens.AddToSelf(0, 3);
            Check("tokens for me", Tokens.Owned(0) == t0 + 3, $"{t0} -> {Tokens.Owned(0)}");
            int found = 0;
            for (int i = 0; i < types; i++) if (Tokens.Prefab(i)) found++;
            Check("token pickups found", found == types, $"{found}/{types}");
            int t1 = Tokens.Owned(1), gifts = UnityEngine.Object.FindObjectsByType<MisterGift>(FindObjectsSortMode.None).Length;
            Tokens.Drop(Game.Player, 1, 3);
            yield return new WaitForSecondsRealtime(0.3f);
            int dropped = UnityEngine.Object.FindObjectsByType<MisterGift>(FindObjectsSortMode.None).Length - gifts;
            Check("token pickups dropped", dropped == 3 || Tokens.Owned(1) > t1, $"{dropped} on the floor, owned {t1} -> {Tokens.Owned(1)}");
            Menu.Open();
            Menu.Select(6);
            yield return Shot("34_tokens");
            Menu.Close();
            yield return new WaitForSecondsRealtime(3f);
            var left = UnityEngine.Object.FindObjectsByType<MisterGift>(FindObjectsSortMode.None);
            _log.LogInfo($"[selftest] tokens after 3 s standing on them: owned {t1} -> {Tokens.Owned(1)}, {left.Length - gifts} still on the floor");
            if (left.Length > gifts)
            {
                var g = left[0];
                _log.LogInfo($"[selftest] pickup '{g.name}' components: " + string.Join(", ", g.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name + "@" + c.name)) + $", layer {g.gameObject.layer}, tag {g.tag}");
                g.GetStarToken();
                yield return Settle();
                Check("picking up a token counts it", Tokens.Owned(1) == t1 + 1 + (3 - dropped), $"{t1} -> {Tokens.Owned(1)}");
            }

            // ---- Moving a player from the host (experiment) ----
            // Our own robot runs the same movement code as a remote player's game does for theirs.
            yield return MoveExperiment(gd);

            Menu.Open();
            Menu.Select(3);
            yield return Shot("40_management_after");
            Menu.Close();
            Finish();
        }

        private IEnumerator MoveExperiment(GameData gd)
        {
            var me = Game.Player;
            Vector3 start = me.position, dir = Vector3.zero;
            for (int a = 0; a < 360 && dir == Vector3.zero; a += 45)
            {
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                if (!Physics.Raycast(start + Vector3.up, d, 7f)) dir = d;
            }
            _log.LogInfo("[selftest] player components: " + string.Join(", ", me.GetComponents<Component>().Select(c => c.GetType().Name)));
            if (dir == Vector3.zero) { _log.LogInfo("[selftest] move: no open direction"); yield break; }
            var target = start + dir * 5f;
            Func<float> off = () => Vector2.Distance(new Vector2(me.position.x, me.position.z), new Vector2(target.x, target.z));

            Traverse.Create(gd.GetComponent<NetworkSpawner>()).Method("RpcUpdateObjectOnClients", new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion) })
                .GetValue(me.gameObject, target, me.rotation);
            yield return null;
            float a0 = off();
            yield return new WaitForSecondsRealtime(1f);
            _log.LogInfo($"[selftest] move A NetworkSpawner.RpcUpdateObjectOnClients: {a0:0.00} m then {off():0.00} m from target");

            Game.Teleport(start);
            yield return Settle();
            var smooth = me.GetComponent<Smooth.SmoothSyncMirror>();
            if (smooth)
            {
                smooth.RpcNonServerOwnedTeleportFromServer(target, me.eulerAngles, me.localScale);
                yield return null;
                float b0 = off();
                yield return new WaitForSecondsRealtime(1f);
                _log.LogInfo($"[selftest] move B SmoothSync.RpcNonServerOwnedTeleportFromServer: {b0:0.00} m then {off():0.00} m from target, transformSource {smooth.transformSource}");
            }

            Game.Teleport(start);
            yield return Settle();
            // "Bring" asks for the game's push with the command any player may send.
            var mine = Game.Net.GetComponent<PlayerObjectController>();
            Players.Push(mine, dir);
            yield return new WaitForSecondsRealtime(2f);
            float pushed = Vector3.Distance(start, me.position);
            Check("bring: one push moves the player", pushed > 3f, $"{pushed:0.00} m");
            Game.Teleport(start);
            yield return Settle();
        }

        private void Finish()
        {
            L.Setting.Value = _language;
            string summary = $"{_pass} passed, {_fail} failed, {_errors} trainer errors";
            _log.LogInfo("[selftest] DONE " + summary);
            File.WriteAllText(Path.Combine(_dir, "result.txt"), summary + Environment.NewLine);
            StartCoroutine(Quit());
        }

        private static IEnumerator Quit()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }
    }
}
#endif
