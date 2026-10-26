using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using SupermarketTrainer.Features;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer
{
    [BepInPlugin(Guid, "Supermarket Trainer", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "zen.supermarkettogether.trainer";
        public const string Version = "1.2.0";

        private Harmony _harmony;

        private void Awake()
        {
            Settings.Bind(Config);
            Teleport.Bind(Config);
            Menu.Init();
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"Supermarket Trainer {Version} by Zenato loaded. Menu key: {Settings.MenuKey.Value}");
#if SELFTEST
            SelfTest.Start(gameObject, Logger);
#endif
        }

        private void OnDestroy()
        {
            ResetAll();
            Menu.Close();
            _harmony?.UnpatchSelf();
        }

        public static void ResetAll()
        {
            PlayerMods.Reset();
            Store.Reset();
            Staff.Reset();
            World.Reset();
            Players.Reset();
        }

        private static bool InChat => FsmVariables.GlobalVariables.FindFsmBool("InChat")?.Value ?? false;

        private void Update()
        {
            if (!Settings.Rebinding && !InChat)
            {
                if (Input.GetKeyDown(Settings.MenuKey.Value)) Menu.Toggle();
                if (Game.InGame && !Menu.IsOpen && GUIUtility.keyboardControl == 0)
                {
                    if (Input.GetKeyDown(Settings.NoclipKey.Value))
                    {
                        PlayerMods.Noclip = !PlayerMods.Noclip;
                        Toasts.Show(PlayerMods.Noclip ? L.T("Noclip activé.", "Noclip on.") : L.T("Noclip désactivé.", "Noclip off."));
                    }
                    if (Input.GetKeyDown(Settings.CrosshairKey.Value)) Teleport.ToCrosshair();
                }
            }

            PlayerMods.Tick();
            Store.Tick();
            Staff.Tick();
            Players.Tick();
            Management.Tick();
        }

        private void LateUpdate()
        {
            PlayerMods.LateTick();
            Menu.Enforce();
        }

        private void OnGUI()
        {
            Settings.CaptureKey();
            GUI.depth = -1000;
            Menu.OnGUI();
        }
    }
}
