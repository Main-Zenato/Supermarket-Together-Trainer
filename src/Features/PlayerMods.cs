using Cinemachine;
using HarmonyLib;
using StarterAssets;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    /// <summary>Local movement tweaks. Everything here only touches your own character.</summary>
    internal static class PlayerMods
    {
        public static bool Noclip;
        public static float NoclipSpeed = 10f;
        public static float SpeedMult = 1f;
        public static float JumpMult = 1f;
        public static float GravityMult = 1f;
        public static bool AirJump;
        public static bool FovOverride;
        public static float Fov = 75f;

        private static FirstPersonController _tuned;
        private static float _move, _sprint, _crouch, _air, _jump, _gravity;
        private static bool _rbKinematic, _noclipApplied;
        private static CinemachineVirtualCamera _vcam;
        private static float _baseFov = -1f;

        public static void Tick()
        {
            var fpc = Game.Fpc;
            if (!fpc) { _tuned = null; _noclipApplied = false; _vcam = null; _baseFov = -1f; return; }

            CaptureBase(fpc);
            fpc.MoveSpeed = _move * SpeedMult;
            fpc.SprintSpeed = _sprint * SpeedMult;
            fpc.CrouchSpeed = _crouch * SpeedMult;
            fpc.airSpeed = _air * SpeedMult;
            fpc.JumpHeight = _jump * JumpMult;
            fpc.Gravity = _gravity * GravityMult;

            ApplyNoclipState(fpc);
            if (Noclip) FlyStep(fpc);
            else if (AirJump) AirJumpStep(fpc);
        }

        public static void LateTick()
        {
            if (!Game.Fpc) return;
            if (!_vcam && Camera.main)
            {
                var ccc = Camera.main.GetComponent<CustomCameraController>();
                if (ccc) _vcam = ccc.cineVCamera;
            }
            if (!_vcam) return;
            if (FovOverride)
            {
                if (_baseFov < 0f) _baseFov = _vcam.m_Lens.FieldOfView;
                _vcam.m_Lens.FieldOfView = Fov;
            }
            else if (_baseFov > 0f)
            {
                _vcam.m_Lens.FieldOfView = _baseFov;
                _baseFov = -1f;
            }
        }

        private static void CaptureBase(FirstPersonController fpc)
        {
            if (_tuned == fpc) return;
            _tuned = fpc;
            _move = fpc.MoveSpeed; _sprint = fpc.SprintSpeed; _crouch = fpc.CrouchSpeed;
            _air = fpc.airSpeed; _jump = fpc.JumpHeight; _gravity = fpc.Gravity;
        }

        private static void ApplyNoclipState(FirstPersonController fpc)
        {
            if (Noclip == _noclipApplied) return;
            _noclipApplied = Noclip;
            var cc = fpc.GetComponent<CharacterController>();
            var rb = fpc.GetComponent<Rigidbody>();
            if (Noclip)
            {
                if (cc) cc.enabled = false;
                if (rb) { _rbKinematic = rb.isKinematic; rb.isKinematic = true; }
            }
            else
            {
                if (cc) cc.enabled = true;
                if (rb) rb.isKinematic = _rbKinematic;
                fpc.allowPlayerInput = true;
                Traverse.Create(fpc).Field("_verticalVelocity").SetValue(0f);
            }
        }

        private static void FlyStep(FirstPersonController fpc)
        {
            var input = Game.Input;
            var cam = Camera.main ? Camera.main.transform : fpc.transform;
            if (input == null || Menu.IsOpen) return;

            bool down = input.GetButton("Crouch");
            // Keep the crouch toggle, poses and item drops from firing while descending.
            fpc.allowPlayerInput = !down;

            var dir = cam.forward * input.GetAxisRaw("MoveV") + cam.right * input.GetAxisRaw("MoveH");
            if (input.GetButton("Jump")) dir += Vector3.up;
            if (down) dir -= Vector3.up;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            float speed = NoclipSpeed * (input.GetButton("Sprint") ? 2.5f : 1f);
            fpc.transform.position += dir * (speed * Time.deltaTime);
        }

        private static void AirJumpStep(FirstPersonController fpc)
        {
            var input = Game.Input;
            if (input == null || fpc.Grounded || !fpc.allowPlayerInput || Menu.IsOpen) return;
            if (!input.GetButtonDown("Jump")) return;
            Traverse.Create(fpc).Field("_verticalVelocity").SetValue(Mathf.Sqrt(fpc.JumpHeight * -2f * fpc.Gravity));
        }

        public static void Reset()
        {
            Noclip = false; AirJump = false; FovOverride = false;
            SpeedMult = 1f; JumpMult = 1f; GravityMult = 1f;
        }

        public static void Draw(Ui ui)
        {
            ui.Section(L.T("Déplacement", "Movement"));
            Noclip = ui.Toggle("noclip", "Noclip",
                L.T("Vole à travers les murs. Saut pour monter, accroupi pour descendre, sprint pour accélérer.",
                    "Fly through walls. Jump to rise, crouch to sink, sprint to go faster."), Noclip);
            NoclipSpeed = ui.Slider("noclipSpeed", L.T("Vitesse du noclip", "Noclip speed"), NoclipSpeed, 2f, 40f, v => v.ToString("0") + " m/s", step: 1f);
            SpeedMult = ui.Slider("speed", L.T("Vitesse de marche", "Walking speed"), SpeedMult, 0.5f, 5f, v => "×" + v.ToString("0.0"), step: 0.1f);

            ui.Section(L.T("Saut et gravité", "Jumping and gravity"));
            JumpMult = ui.Slider("jump", L.T("Hauteur de saut", "Jump height"), JumpMult, 0.5f, 8f, v => "×" + v.ToString("0.0"), step: 0.1f);
            GravityMult = ui.Slider("gravity", L.T("Gravité", "Gravity"), GravityMult, 0.1f, 2f, v => "×" + v.ToString("0.00"), step: 0.05f);
            AirJump = ui.Toggle("airjump", L.T("Saut infini", "Infinite jump"),
                L.T("Ressaute en plein vol autant que tu veux.", "Jump again mid-air as often as you like."), AirJump);

            ui.Section(L.T("Caméra", "Camera"));
            FovOverride = ui.Toggle("fov", L.T("Champ de vision personnalisé", "Custom field of view"),
                L.T("Remplace le réglage du jeu tant que c'est activé.", "Overrides the game setting while switched on."), FovOverride);
            Fov = ui.Slider("fovValue", L.T("Angle de vue", "View angle"), Fov, 40f, 120f, v => v.ToString("0") + "°", step: 1f);

            if (ui.Buttons("player.reset", false, L.T("Remettre les valeurs par défaut", "Restore default values")) == 0)
            {
                Reset();
                Toasts.Show(L.T("Déplacements remis par défaut.", "Movement restored to defaults."));
            }
        }
    }

    [HarmonyPatch(typeof(FirstPersonController), "Update")]
    internal static class NoclipUpdatePatch
    {
        // The controller would otherwise apply gravity and collide while flying.
        private static bool Prefix(FirstPersonController __instance) => !(PlayerMods.Noclip && __instance == Game.Fpc);
    }
}
