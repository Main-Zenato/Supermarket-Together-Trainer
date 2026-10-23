using System.Collections.Generic;
using SupermarketTrainer.Features;
using UnityEngine;

namespace SupermarketTrainer.UI
{
    /// <summary>One quiet strip in the top-left corner listing what is switched on.</summary>
    internal static class Hud
    {
        private static readonly List<string> Active = new List<string>();

        public static void Draw()
        {
            if (!Settings.ShowHud.Value || !Game.InGame) return;
            Collect();
            if (Active.Count == 0) return;

            string text = string.Join(", ", Active);
            var size = Theme.Measure(text, 13);
            var r = new Rect(16, 16, size.x + 92, 28);
            Theme.Fill(r, Theme.Ink.WithAlpha(0.85f), 14);
            Theme.DrawTag(new Rect(r.x + 4, r.y + 4, 66, 20), Theme.Price);
            Theme.Label(new Rect(r.x + 22, r.y + 4, 48, 20), L.T("Actif", "Active"), 12, Theme.Ink, true);
            Theme.Label(new Rect(r.x + 80, r.y, size.x + 8, r.height), text, 13, Theme.Receipt);
        }

        private static void Collect()
        {
            Active.Clear();
            if (PlayerMods.Noclip) Active.Add("noclip");
            if (!Mathf.Approximately(PlayerMods.SpeedMult, 1f)) Active.Add(L.T("vitesse ×", "speed ×") + PlayerMods.SpeedMult.ToString("0.0"));
            if (!Mathf.Approximately(PlayerMods.JumpMult, 1f)) Active.Add(L.T("saut ×", "jump ×") + PlayerMods.JumpMult.ToString("0.0"));
            if (!Mathf.Approximately(PlayerMods.GravityMult, 1f)) Active.Add(L.T("gravité ×", "gravity ×") + PlayerMods.GravityMult.ToString("0.00"));
            if (PlayerMods.AirJump) Active.Add(L.T("saut infini", "infinite jump"));
            if (PlayerMods.FovOverride) Active.Add(L.T("champ de vision ", "FOV ") + PlayerMods.Fov.ToString("0") + "°");
            if (Store.MoneyLock) Active.Add(L.T("argent infini", "infinite money"));
            if (Players.PlayerEsp || Players.ThiefEsp) Active.Add(L.T("vision à travers les murs", "see through walls"));
            if (!Game.IsHost) return;
            if (World.FreezeTime) Active.Add(L.T("heure figée", "time frozen"));
            else if (!Mathf.Approximately(World.TimeSpeed, 1f)) Active.Add(L.T("temps ×", "time ×") + World.TimeSpeed.ToString("0.00"));
            if (World.NoTrash) Active.Add(L.T("aucun déchet", "no trash"));
            if (World.NoBreakdowns) Active.Add(L.T("aucune panne", "no breakdowns"));
            if (Staff.CustomerCap) Active.Add(Staff.CustomerCapValue.ToString("0") + L.T(" clients max", " customers max"));
            if (Staff.NoThieves) Active.Add(L.T("aucun voleur", "no thieves"));
            if (Staff.FastEmployees) Active.Add(L.T("employés ×", "staff ×") + Staff.EmployeeSpeedMult.ToString("0.0"));
            if (Staff.InstantCheckout) Active.Add(L.T("encaissement éclair", "lightning checkout"));
        }
    }
}
