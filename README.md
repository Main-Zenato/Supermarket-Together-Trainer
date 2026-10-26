# Supermarket Together Trainer

An in-game trainer for **Supermarket Together**, built as a BepInEx 5 plugin. Open it with
**Insert** while playing: noclip, money, franchise points, full shelves, time control,
host tools, event tokens, broom skins and more, in an English or French menu.

Made by **Zenato**.

> [Version française plus bas.](#version-française)

## Contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
- [Host and client](#host-and-client)
- [Troubleshooting](#troubleshooting)
- [Uninstall](#uninstall)
- [Building from source](#building-from-source)
- [Credits and license](#credits-and-license)

## Features

Features marked *(testing)* are new in 1.2.0 and haven't been checked in a real game yet. They may
not work as described: if one misbehaves, open an issue with `BepInEx\LogOutput.log`.

**Player** (works everywhere)
- Noclip: fly through walls. Jump to rise, crouch to sink, sprint to go faster.
- Walking speed, jump height and gravity multipliers.
- Infinite jump.
- Custom field of view.

**Teleport** (works everywhere)
- Teleport to wherever you aim.
- Back to the store's spawn point.
- 5 saved spots, kept between sessions.

**Store**
- Add money in one click or type any amount. Infinite money keeps the till from going down.
- Set the till to an exact amount *(host)*.
- Franchise points and level ups *(host)*.
- Unlock every product range *(host)*.
- Fill every shelf row that already holds a product, for free *(host)*.
- Open the store, close it right away *(host)*, end the day.
- Free boxes: search any product and drop 1 to 10 full boxes in front of you.

**Management**
- Buy store and storage expansions one at a time, or every expansion and department at once, for free.
- Unlock every franchise perk without spending points.
- Pay every bill and pay off your loan for free.
- Set every selling price at once: market price, ×1.5 or ×2 (the most customers will pay).
- Rename the store and change the sign colour *(host)*.
- *(testing)* Demolish every pillar or every beam for free, then clear the rubble they leave.
- *(testing)* Repairs saves where version 1.1.0's "Buy everything" bought expansions missing from
  the map, which kept the departments closed. With the trainer the departments open again; when the
  host has it, the save itself is fixed for everyone.

**Customers and staff** *(host)*
- Custom limit on how many customers can be in the store at once.
- No thieves.
- Send every customer out.
- Faster employees, near-instant checkout and restocking.
- Custom hiring limit, and new candidates on the hiring board without using up rerolls.

**World** *(host)*
- Set the time, freeze it, or speed it up and slow it down.
- Pick today's weather: sunny, windy, cloudy, rain, storm or snow.
- No trash, no breakdowns, pick up all trash at once.

**Players**
- List of everyone in the game with their distance, and teleport next to them.
- *(testing)* Bring a player to you: the game's own push slides them towards you at 3 m/s until they
  reach you. Works as a client too and for players without the trainer; walls and shelves stop them.
- *(testing)* Give event tokens to anyone. Yours arrive straight away; other players get real tokens
  at their feet to pick up *(host)*.
- Names visible through walls.
- Jail for 30 seconds and kick *(host)*.
- Spot thieves through walls *(host)*.

**Brooms**
- Add event tokens of any kind (star, moon, alien, candy, snowflake, egg).
- Drop a gachapon capsule from any machine without spending a token.
- Unlock every broom skin and switch between them.

**Interface**
- English and French, picked automatically from your Windows language or set in Settings.
- Rebindable keys, adjustable menu size, toast confirmations, an optional strip listing what's active.

## Installation

### Option A: ready-made package (easiest)

1. Download `SupermarketTrainer-vX-full.zip` from the [latest release](../../releases/latest).
2. In Steam, right-click **Supermarket Together** → **Manage** → **Browse local files**.
3. Extract the zip into that folder. You should end up with `winhttp.dll` next to `Supermarket Together.exe`, and a `BepInEx` folder.
4. Start the game.

This package includes BepInEx 5.4.23.5. If you already use BepInEx 5 for other mods, take
`SupermarketTrainer-vX-plugin.zip` instead and extract it into the same game folder.

### Option B: install script (from source)

Needs the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer. Close the game, then:

```powershell
git clone https://github.com/Main-Zenato/Supermarket-Together-Trainer.git
cd Supermarket-Together-Trainer
powershell -ExecutionPolicy Bypass -File install.ps1
```

The script downloads BepInEx if the game doesn't have it yet, builds the plugin, and copies it to
`BepInEx\plugins\SupermarketTrainer\`. If the game isn't in the default Steam library:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Supermarket Together"
```

### Checking that it works

After starting the game once, open `BepInEx\LogOutput.log` in the game folder. It should contain:

```
[Info   :Supermarket Trainer] Supermarket Trainer 1.2.0 by Zenato loaded. Menu key: Insert
```

## Usage

Start or join a game, then press **Insert**.

| Key | Action |
|---|---|
| Insert | Open or close the menu |
| F6 | Toggle noclip |
| F7 | Teleport to where you aim |

All three can be changed in **Settings**, or in `BepInEx\config\zen.supermarkettogether.trainer.cfg`.
On laptops without an Insert key, try **Fn + Insert** or rebind the menu key in that file
(for example `Menu = F8`).

While the menu is open the camera and your character stay still, and the mouse is free. Drag the
window by its title to move it, and scroll to see a whole tab.

## Host and client

Supermarket Together runs the store simulation on the host's machine. Options that change that
simulation (customers, employees, clock, trash, shelves, franchise) only work when **you host the
game**. When you join someone else's game they are greyed out with a *host* badge.

Your own movement, teleports, money top-ups, free boxes, expansions, perks, bills, prices, event
tokens and broom skins work in any game. Free purchases put the exact price in the till right
before the game's own purchase takes it back, so the till ends where it started.

Please be considerate when you join other people's games.

## Troubleshooting

**Nothing happens when I press Insert.**
Check `BepInEx\LogOutput.log` (see above). If the file doesn't exist, BepInEx isn't loading:
make sure `winhttp.dll` sits right next to `Supermarket Together.exe`. Some antivirus tools
quarantine `winhttp.dll`; restore it and add an exception for the game folder.

**The menu opened once, then stopped working after loading a store.**
Open `BepInEx\config\BepInEx.cfg` and make sure it says `HideManagerGameObject = true`.
The release package and the install script set this for you.

**An option is greyed out.**
It's a host option and you joined someone else's game. See [Host and client](#host-and-client).

**Turn everything off at once.**
Settings → **Turn everything off**. Your saved teleport spots are kept.

## Uninstall

- Trainer only: delete `BepInEx\plugins\SupermarketTrainer\` from the game folder.
- Everything: also delete `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version`.

Tokens and broom skins you added stay in your player save after uninstalling.

## Building from source

```powershell
dotnet build -c Release
```

The DLL goes to `build\SupermarketTrainer.dll`. The project references the game's assemblies
straight from its install folder; if your game is elsewhere, pass
`-p:GameDir="D:\...\Supermarket Together"`. BepInEx's own assemblies are expected in
`.tools\bepinex\pkg\BepInEx\core`, which `install.ps1` downloads.

`package.ps1` builds both release zips into `build\release\`.

`dotnet build -c Release -p:SelfTest=true` builds an automated in-game check instead
(`src/SelfTest.cs`). It hosts a local game on the save slot `StoreFile9.es3`, which you copy
from one of your saves beforehand, exercises the features, writes screenshots and a report to
`BepInEx\selftest\`, then quits without saving. Back up your saves before running it.

Project layout:

```
src/
  Plugin.cs          BepInEx entry point, hotkeys, update loop
  Menu.cs            window, sidebar, tabs, scrolling
  Settings.cs        keys, language, interface options
  L.cs               English/French strings
  Game.cs            accessors for the game's singletons
  Features/          one file per tab, with its Harmony patches
  UI/                theme, generated textures, widgets, toasts, active-options strip
```

The interface is drawn with Unity IMGUI. Every texture (rounded corners, the price-tag tab marker,
the receipt edge on notifications) is generated at runtime, so the plugin is a single DLL.

## Credits and license

- Trainer by **Zenato**, released under the [MIT license](LICENSE).
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) loads the plugin and ships with the full release package.
- [HarmonyX](https://github.com/BepInEx/HarmonyX) patches the game's methods.

This project isn't affiliated with or endorsed by the developers of Supermarket Together. It
doesn't contain any game files or code.

---

## Version française

Trainer en jeu pour **Supermarket Together**, sous forme de plugin BepInEx 5. Ouvre-le avec
**Inser** pendant une partie. Par **Zenato**.

### Installation

1. Télécharge `SupermarketTrainer-vX-full.zip` depuis la [dernière release](../../releases/latest).
2. Dans Steam : clic droit sur **Supermarket Together** → **Gérer** → **Parcourir les fichiers locaux**.
3. Extrais le zip dans ce dossier : `winhttp.dll` doit se retrouver à côté de `Supermarket Together.exe`.
4. Lance le jeu, entre dans une partie et appuie sur **Inser**.

Si tu utilises déjà BepInEx 5 pour d'autres mods, prends plutôt `SupermarketTrainer-vX-plugin.zip`.
Depuis les sources : `powershell -ExecutionPolicy Bypass -File install.ps1` (jeu fermé, SDK .NET 8 requis).

### Touches

| Touche | Action |
|---|---|
| Inser | Ouvrir ou fermer le menu |
| F6 | Noclip |
| F7 | Téléportation au viseur |

Elles se changent dans **Réglages**. Le menu s'affiche en français si Windows est en français ;
la langue se choisit aussi dans Réglages.

### Fonctions

- **Joueur** : noclip, vitesse, saut, gravité, saut infini, champ de vision.
- **Téléportation** : au viseur, au point d'apparition, 5 emplacements sauvegardés.
- **Magasin** : argent, argent infini, franchise, déblocage des gammes, remplissage des rayons, ouverture et fin de journée, cartons gratuits de n'importe quel produit.
- **Gestion** : agrandissements et départements gratuits, tous les avantages de franchise sans points, factures et prêt payés gratuitement, tous les prix d'un coup (prix du marché, ×1,5 ou ×2), nom et couleur de l'enseigne, démolition gratuite des piliers et des poutres avec nettoyage des gravats *(en test)*, réparation des sauvegardes abîmées par « Tout acheter » de la 1.1.0 *(en test)*.
- **Clients et employés** : limite de clients, aucun voleur, employés rapides, encaissement éclair, limite d'embauche, nouveaux candidats sans relance.
- **Monde** : heure, heure figée, vitesse du temps, météo du jour, aucun déchet, aucune panne.
- **Joueurs** : liste, téléportation, noms à travers les murs, prison, expulsion, repérage des voleurs, « Amener » un joueur jusqu'à toi *(en test)*, jetons d'événement pour les autres joueurs *(en test, hôte)*.

Les fonctions marquées *en test* sont nouvelles dans la 1.2.0 et n'ont pas encore été vérifiées en
partie réelle. Si l'une d'elles se comporte mal, ouvre une issue avec `BepInEx\LogOutput.log`.
- **Balais** : jetons d'événement, boules gachapon gratuites, déblocage de tous les skins de balai.

Les options qui modifient la simulation du magasin sont réservées à l'**hôte** : elles sont
grisées quand tu rejoins la partie de quelqu'un d'autre.

### Désinstallation

Supprime `BepInEx\plugins\SupermarketTrainer\` dans le dossier du jeu. Pour tout retirer, supprime
aussi `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` et `.doorstop_version`.
