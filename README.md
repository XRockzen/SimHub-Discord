# Discord Control — plugin SimHub

Pilote **Discord** depuis n'importe quel bouton que SimHub sait lire : boîte à boutons, volant, clavier, manette, encodeur rotatif. Le plugin règle le volume de Discord sans toucher au volume global de Windows, et déclenche ses raccourcis (micro, sourdine, push-to-talk, etc.).

Exemple d'usage : un encodeur rotatif monte ou baisse le volume de tes coéquipiers, et un bouton du volant coupe ton micro, sans quitter la course.

## Installation

1. Télécharge **`User.DiscordControl.dll`** dans la [dernière release](https://github.com/XRockzen/SimHub-Discord/releases/latest).
2. Ferme SimHub et copie la DLL dans le dossier de SimHub (souvent `C:\Program Files (x86)\SimHub`).
3. Lance SimHub, accepte d'activer le plugin, puis ouvre **Discord Control** dans le menu de gauche.

À partir de la v1.1.0, le plugin vérifie au démarrage de SimHub s'il existe une version plus récente sur GitHub. Un bandeau propose alors de l'installer ; elle s'active au redémarrage suivant de SimHub. La version installée est affichée en bas de l'onglet.

## Configuration

### 1. Volume

Aucune configuration requise. Le plugin agit sur la ligne Discord du mixeur de volume Windows. Dans l'onglet **Discord Control**, choisis de combien chaque appui change le volume (5 à 25 %) ; les boutons **Vol –**, **Vol +** et **Mute toggle** permettent de tester.

### 2. Raccourcis

Le plugin ne parle pas directement à Discord : il simule la frappe d'un raccourci clavier global. La même combinaison doit donc exister des deux côtés.

1. Dans l'onglet **Discord Control**, choisis une combinaison pour chaque fonction voulue : Ctrl, Shift, Alt (facultatifs) + une touche parmi F1 à F24, A à Z, Num0 à Num9. Une combinaison peu utilisée comme `Ctrl + Shift + F13` évite les conflits avec le jeu.
2. Dans Discord → **Paramètres → Raccourcis clavier**, crée le raccourci correspondant.

Une fonction laissée sur **(aucune)** n'envoie rien. Les réglages sont enregistrés à la fermeture de SimHub.

### 3. Mapper les boutons physiques

Dans SimHub → **Controls and events**, crée un mapping par bouton : choisis l'action (par exemple **`DiscordControlPlugin.DiscordMicToggle`**), puis appuie sur le bouton physique à associer. Un encodeur rotatif apparaît comme deux boutons, un par sens de rotation : mappe l'un sur `DiscordVolumeUp` et l'autre sur `DiscordVolumeDown`.

## Référence

Toutes les actions et propriétés sont préfixées par `DiscordControlPlugin.`

| Action | Effet |
|---|---|
| `DiscordVolumeUp` / `DiscordVolumeDown` | Monte ou baisse le volume de Discord du pas choisi |
| `DiscordVolumeMax` / `DiscordVolumeMin` | Met le volume de Discord à 100 % ou à 0 % |
| `DiscordAppMuteToggle` | Coupe ou rétablit le son de Discord dans le mixeur Windows |
| `DiscordMicToggle` | Envoie le raccourci du micro |
| `DiscordDeafenToggle` | Envoie le raccourci de la sourdine |
| `DiscordPushToTalk` | Maintient le raccourci push-to-talk tant que le bouton est enfoncé |
| `DiscordOverlayToggle` | Envoie le raccourci de l'overlay |
| `DiscordCameraToggle` | Envoie le raccourci de la caméra |
| `DiscordScreenShareToggle` | Envoie le raccourci du partage d'écran |

| Propriété (dashboards, overlays) | Valeur |
|---|---|
| `DiscordVolume` | Volume de Discord, de 0 à 100 |
| `DiscordIsMuted` | `true` si le son de Discord est coupé dans le mixeur |
| `DiscordIsRunning` | `true` si une session audio Discord est trouvée |

Réglages : `PluginsData\Common\DiscordControlPlugin.GeneralSettings.json` dans le dossier SimHub. Les erreurs et actions apparaissent dans les logs SimHub, préfixées `[DiscordControl]`.

### Dépannage

- **« Discord non détecté » alors que Discord est ouvert** : Discord n'a pas de session audio sur la sortie par défaut de Windows. Fais-lui jouer un son (rejoins un salon vocal), vérifie qu'il sort bien sur le périphérique par défaut, puis clique **↺ Actualiser**.
- **Le volume marche mais pas les raccourcis** : vérifie que la combinaison est identique dans le plugin et dans Discord.
- **Un raccourci agit dans le jeu** : choisis une combinaison que le jeu n'utilise pas.

## Développement

```
plugin/                   plugin SimHub (C# 5, .NET Framework 4.8)
plugin/build.ps1          compile avec le MSBuild de Windows (-Deploy : copie dans SimHub)
```

- Aucun Visual Studio nécessaire : `plugin\build.ps1` utilise le MSBuild livré avec Windows. Le chemin de SimHub vient de la variable d'environnement `SIMHUB_INSTALL_PATH` (avec un `\` final).
- `.\build.ps1 -Deploy` copie la DLL dans SimHub, qui doit être fermé.
