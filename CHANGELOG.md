# Changelog

## v1.1.0

- Mise à jour automatique : au démarrage, le plugin compare sa version à la dernière release GitHub et propose de l'installer (fichier vérifié par SHA-256, activation au prochain redémarrage de SimHub).
- Version installée affichée en bas de l'onglet, avec un lien pour vérifier les mises à jour.
- Prototype de l'API locale de Discord (onglet dédié et actions `DiscordRpc*`) : micro, sourdine, volumes de sortie et d'entrée, raccrocher, et à titre expérimental caméra, partage d'écran et soundboard.

## v1.0.0

Première version publiée.

- Volume de l'application Discord dans le mixeur Windows, indépendant du volume global : plus, moins, maximum, minimum, couper le son.
- Raccourcis Discord déclenchés depuis un bouton : micro, sourdine, overlay, caméra, partage d'écran.
- Push-to-talk, actif tant que le bouton est maintenu.
- Combinaison configurable pour chaque raccourci (Ctrl, Maj, Alt + F1 à F24, A à Z, pavé numérique).
- Pas de volume réglable (5 à 25 %).
- Propriétés `DiscordVolume`, `DiscordIsMuted` et `DiscordIsRunning` pour les dashboards et overlays.
