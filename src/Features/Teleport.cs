using System.Globalization;
using BepInEx.Configuration;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    internal static class Teleport
    {
        private const int Slots = 5;
        private static ConfigEntry<string>[] _slots;

        public static void Bind(ConfigFile cfg)
        {
            _slots = new ConfigEntry<string>[Slots];
            for (int i = 0; i < Slots; i++)
                _slots[i] = cfg.Bind("Téléportation", "Emplacement" + (i + 1), "", "Position sauvegardée (x;y;z)");
        }

        public static void ToCrosshair()
        {
            var cam = Camera.main;
            if (!cam || !Game.Player) return;
            var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 300f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit? pick = null;
            foreach (var h in hits)
            {
                if (h.transform.root == Game.Player.root) continue;
                if (h.distance < best) { best = h.distance; pick = h; }
            }
            if (pick == null) { Toasts.Show(L.T("Aucune surface visée à moins de 300 m.", "No surface in sight within 300 m.")); return; }
            var hit = pick.Value;
            // Stand on the surface, pushed slightly away from walls.
            var pos = hit.point + hit.normal * 0.45f;
            if (hit.normal.y < 0.5f) pos.y = Mathf.Max(pos.y, hit.point.y);
            Game.Teleport(pos + Vector3.up * 0.1f);
            Toasts.Show(L.T("Téléporté au viseur.", "Teleported to crosshair."));
        }

        private static bool TryGet(int i, out Vector3 v)
        {
            v = default;
            var parts = _slots[i].Value.Split(';');
            return parts.Length == 3
                   && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x)
                   && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y)
                   && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z);
        }

        public static void Draw(Ui ui)
        {
            bool inGame = Game.InGame;

            ui.Section(L.T("Rapide", "Quick"));
            int quick = ui.Buttons("tp.quick", false, new Btn(L.T("Au viseur", "To crosshair"), ButtonKind.Primary),
                L.T("Point d'apparition", "Spawn point"));
            if (quick == 0 && inGame) ToCrosshair();
            if (quick == 1 && inGame)
            {
                var spawn = GameData.Instance.playerSpawnPosition;
                if (spawn == Vector3.zero) Toasts.Show(L.T("Ce magasin n'a pas de point d'apparition connu.", "This store has no known spawn point."));
                else { Game.Teleport(spawn + Vector3.up * 0.2f); Toasts.Show(L.T("Retour au point d'apparition.", "Back at the spawn point.")); }
            }
            ui.Note(L.T("Astuce : la touche du viseur se règle dans l'onglet Réglages.", "Tip: the crosshair key can be changed in the Settings tab."));

            ui.Section(L.T("Emplacements sauvegardés", "Saved spots"));
            if (Game.Player)
            {
                var p = Game.Player.position;
                ui.Stat(L.T("Position actuelle", "Current position"), $"{p.x:0.0}  {p.y:0.0}  {p.z:0.0}");
            }
            for (int i = 0; i < Slots; i++)
            {
                bool has = TryGet(i, out var pos);
                string caption = has ? $"{pos.x:0.0}  {pos.y:0.0}  {pos.z:0.0}" : L.T("Vide", "Empty");
                int b = ui.ListRow("tp.slot" + i, L.T("Emplacement ", "Spot ") + (i + 1), caption, false,
                    new Btn(L.T("Y aller", "Go"), has ? ButtonKind.Primary : ButtonKind.Secondary), L.T("Enregistrer", "Save"));
                if (!inGame) continue;
                if (b == 0)
                {
                    if (has) { Game.Teleport(pos); Toasts.Show(L.T($"Téléporté à l'emplacement {i + 1}.", $"Teleported to spot {i + 1}.")); }
                    else Toasts.Show(L.T($"L'emplacement {i + 1} est vide. Enregistre d'abord ta position.", $"Spot {i + 1} is empty. Save your position first."));
                }
                if (b == 1)
                {
                    var p = Game.Player.position;
                    _slots[i].Value = string.Join(";", p.x.ToString(CultureInfo.InvariantCulture),
                        p.y.ToString(CultureInfo.InvariantCulture), p.z.ToString(CultureInfo.InvariantCulture));
                    Toasts.Show(L.T($"Position enregistrée dans l'emplacement {i + 1}.", $"Position saved to spot {i + 1}."));
                }
            }
            ui.Note(L.T("Les emplacements sont communs à toutes tes parties : une position enregistrée dans un magasin peut tomber dans le vide dans un autre.",
                "Spots are shared across all your games: a position saved in one store may land you in mid-air in another."));
        }
    }
}
