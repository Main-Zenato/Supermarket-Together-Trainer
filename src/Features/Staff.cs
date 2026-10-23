using HarmonyLib;
using SupermarketTrainer.UI;
using UnityEngine;
using UnityEngine.AI;

namespace SupermarketTrainer.Features
{
    /// <summary>Customer and employee tweaks. All of it runs server-side, so host only.</summary>
    internal static class Staff
    {
        public static bool CustomerCap;
        public static float CustomerCapValue = 60f;
        public static bool NoThieves;
        public static bool FastEmployees;
        public static float EmployeeSpeedMult = 2f;
        public static bool InstantCheckout;
        public static bool HireCap;
        public static float HireCapValue = 20f;

        private static int _savedCap = -1;
        private static float _savedCheckoutWait = -1f, _savedPlaceWait = -1f;
        private static bool _fastApplied;
        private static float _appliedMult;
        private static int _savedMaxEmployees = -1, _appliedMaxEmployees;
        private static NPC_Manager _hireNpc;

        public static void Tick()
        {
            if (!Game.IsHost || !Game.InGame) return;
            var gd = GameData.Instance;
            var npc = NPC_Manager.Instance;
            if (!npc) return;

            if (CustomerCap)
            {
                if (_savedCap < 0) _savedCap = gd.maxCustomersNPCs;
                gd.maxCustomersNPCs = (int)CustomerCapValue;
            }
            else if (_savedCap >= 0)
            {
                gd.maxCustomersNPCs = _savedCap;
                _savedCap = -1;
            }

            if (NoThieves && npc.customersnpcParentOBJ)
            {
                foreach (Transform c in npc.customersnpcParentOBJ.transform)
                {
                    var info = c.GetComponent<NPC_Info>();
                    if (info && info.isAThief && !info.thiefFleeing) info.isAThief = false;
                }
            }

            if (InstantCheckout)
            {
                if (_savedCheckoutWait < 0f) { _savedCheckoutWait = npc.productCheckoutWait; _savedPlaceWait = npc.employeeItemPlaceWait; }
                npc.productCheckoutWait = 0.05f;
                npc.employeeItemPlaceWait = 0.02f;
            }
            else if (_savedCheckoutWait >= 0f)
            {
                npc.productCheckoutWait = _savedCheckoutWait;
                npc.employeeItemPlaceWait = _savedPlaceWait;
                _savedCheckoutWait = -1f;
            }

            if (npc != _hireNpc) { _hireNpc = npc; _savedMaxEmployees = -1; } // new session: nothing to restore
            if (HireCap)
            {
                if (_savedMaxEmployees < 0) _savedMaxEmployees = npc.maxEmployees;
                // A perk bought meanwhile adds to the value we wrote: carry it over to the original.
                else if (npc.maxEmployees != _appliedMaxEmployees) _savedMaxEmployees += npc.maxEmployees - _appliedMaxEmployees;
                npc.maxEmployees = _appliedMaxEmployees = Mathf.Min((int)HireCapValue, MaxHireSlots(npc));
            }
            else if (_savedMaxEmployees >= 0)
            {
                npc.maxEmployees = _savedMaxEmployees + (npc.maxEmployees - _appliedMaxEmployees);
                _savedMaxEmployees = -1;
            }

            if (FastEmployees != _fastApplied || (FastEmployees && !Mathf.Approximately(_appliedMult, EmployeeSpeedMult)))
            {
                _fastApplied = FastEmployees;
                _appliedMult = EmployeeSpeedMult;
                npc.UpdateEmployeeStats(); // the postfix below rescales the result
            }
        }

        public static void Reset()
        {
            CustomerCap = false; NoThieves = false; FastEmployees = false; InstantCheckout = false; HireCap = false;
        }

        /// <summary>Hired employees are stored in a fixed-size array: no limit can go past it.</summary>
        internal static int MaxHireSlots(NPC_Manager npc) => npc.hiredEmployeesData != null ? npc.hiredEmployeesData.Length : 0;

        /// <summary>New candidates on the hiring board, through the game's reroll with one extra reroll granted.</summary>
        internal static void NewCandidates()
        {
            var npc = NPC_Manager.Instance;
            npc.NetworkrerollTimes = npc.rerollTimes + 1;
            npc.CmdRerollCall();
            Toasts.Show(L.T("Nouveaux candidats sur le tableau d'embauche.", "New candidates on the hiring board."));
        }

        private static void RemoveCustomers()
        {
            NPC_Manager.Instance.RemoveCustomers();
            Toasts.Show(L.T("Les clients quittent le magasin dans 3 secondes.", "Customers will leave the store in 3 seconds."));
        }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost;
            bool lk = !host;
            if (!host) ui.Note(L.T("Ces réglages agissent sur la simulation, qui tourne chez l'hôte. Ils sont grisés quand tu as rejoint la partie de quelqu'un d'autre.",
                "These settings act on the simulation, which runs on the host. They are greyed out when you join someone else's game."), Theme.Price);

            ui.Section(L.T("Clients", "Customers"));
            CustomerCap = ui.Toggle("cust.cap", L.T("Limite de clients personnalisée", "Custom customer limit"),
                L.T("Remplace le nombre maximum de clients en même temps dans le magasin.", "Overrides how many customers can be in the store at once."), CustomerCap, lk);
            CustomerCapValue = ui.Slider("cust.capValue", L.T("Clients simultanés", "Customers at once"), CustomerCapValue, 5f, 150f, v => v.ToString("0"), lk, 1f);
            NoThieves = ui.Toggle("cust.thieves", L.T("Aucun voleur", "No thieves"),
                L.T("Les clients ne tentent plus de partir sans payer.", "Customers no longer try to leave without paying."), NoThieves, lk);
            if (ui.Buttons("cust.remove", lk, new Btn(L.T("Faire sortir tous les clients", "Send every customer out"), ButtonKind.Danger)) == 0 && host && Game.InGame)
                RemoveCustomers();

            ui.Section(L.T("Employés", "Staff"));
            FastEmployees = ui.Toggle("emp.fast", L.T("Employés rapides", "Fast staff"),
                L.T("Multiplie la vitesse de déplacement de tes employés.", "Multiplies how fast your employees move."), FastEmployees, lk);
            EmployeeSpeedMult = ui.Slider("emp.mult", L.T("Vitesse des employés", "Staff speed"), EmployeeSpeedMult, 1f, 4f, v => "×" + v.ToString("0.0"), lk, 0.1f);
            InstantCheckout = ui.Toggle("emp.checkout", L.T("Encaissement et rangement éclair", "Lightning checkout and restocking"),
                L.T("Les caissiers scannent et les employés posent les produits presque instantanément.",
                    "Cashiers scan and employees shelve products almost instantly."), InstantCheckout, lk);

            ui.Section(L.T("Embauche", "Hiring"));
            var npc = NPC_Manager.Instance;
            int slots = npc ? MaxHireSlots(npc) : 0;
            if (Game.InGame && npc)
                ui.Stat(L.T("Employés embauchés", "Hired employees"), $"{npc.numberOfHiredEmployees} / {npc.maxEmployees}");
            HireCap = ui.Toggle("emp.hirecap", L.T("Limite d'embauche personnalisée", "Custom hiring limit"),
                L.T("Embauche plus d'employés que tes avantages ne le permettent.", "Hire more employees than your perks allow."), HireCap, lk);
            HireCapValue = ui.Slider("emp.hirecapValue", L.T("Employés au maximum", "Maximum employees"), HireCapValue, 1f,
                Mathf.Max(1f, slots > 0 ? slots : 30f), v => v.ToString("0"), lk, 1f);
            if (ui.Buttons("emp.reroll", lk, new Btn(L.T("Nouveaux candidats", "New candidates"), ButtonKind.Primary)) == 0 && host && Game.InGame)
                NewCandidates();
            ui.Note(L.T("Remplace les candidats du jour sur l'ordinateur d'embauche, sans utiliser tes relances.",
                "Replaces today's candidates on the hiring computer without using up your rerolls."));
        }

        internal static void ScaleEmployees(NPC_Manager npc)
        {
            if (!npc.employeeParentOBJ) return;
            float mult = FastEmployees ? EmployeeSpeedMult : 1f;
            float f = npc.extraEmployeeSpeedFactor;
            foreach (Transform e in npc.employeeParentOBJ.transform)
            {
                var agent = e.GetComponent<NavMeshAgent>();
                if (!agent) continue;
                // Same formulas the game uses when spawning employees, scaled by our multiplier.
                agent.speed *= mult;
                agent.acceleration = (8f + 16f * f) * mult;
                agent.angularSpeed = (120f + 30f * f) * mult;
            }
        }
    }

    [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats))]
    internal static class EmployeeStatsPatch
    {
        private static void Postfix(NPC_Manager __instance) => Staff.ScaleEmployees(__instance);
    }

    [HarmonyPatch(typeof(NPC_Manager), "SpawnEmployeeByIndex")]
    internal static class EmployeeSpawnPatch
    {
        private static void Postfix(NPC_Manager __instance)
        {
            if (Staff.FastEmployees) __instance.UpdateEmployeeStats();
        }
    }
}
