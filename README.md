# FusionBot

Console Windows + bot Discord qui genere des **idees de projets** via Gemini (gratuit).

## A quoi ca sert

Fusion connecte un bot Discord a ton serveur. La commande `/idee` propose des idees de projets (1 a 5), en francais, en message prive. La console permet aussi d'envoyer des messages dans un salon et de gerer le bot (token, cle API, invitation).

Aucune cle n'est fournie : chacun utilise son token Discord et sa cle Gemini.

## Prerequisites

- Windows 10/11 x64
- [Bot Discord](https://discord.com/developers/applications)
- [Cle Gemini gratuite](https://aistudio.google.com/apikey)

## Installation

1. Telecharge `Fusion.exe` dans la derniere [Release](https://github.com/Lysacae/FusionBot/releases).
2. Lance l'exe, colle ton token Discord et ta cle Gemini.
3. Clique **Recharger**.
4. **Copier le lien d'invitation** -> ajoute le bot a ton serveur.

## Commande Discord

```
/idee sujet:<sujet> nombre:<1-5>
```

Reponse privee (ephemere). Modele : `gemini-3.5-flash-lite`.

## Mises a jour

A l'ouverture, Fusion verifie les releases GitHub. Si une version plus recente existe, une popup propose **Installer** ou **Plus tard**. Tu peux aussi verifier depuis **Parametres** (icone engrenage).

## Aide / contact

- GitHub : [Lysacae](https://github.com/Lysacae)
- Discord : `lysacae`

## Compiler

```powershell
dotnet publish src\Fusion\Fusion.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Changelog

### v1.1.0

- Ajout : verification auto des mises a jour GitHub a l'ouverture
- Ajout : popup Installer / Plus tard
- Ajout : Parametres -> verifier les mises a jour
- Ajout : Aide (GitHub Lysacae, Discord lysacae)
- Correction : historique Git sans co-auteur Cursor

### v1.0.0

- Premiere version publique
- Bot `/idee` + console WPF
- Secrets chiffres DPAPI (vierge a l'installation)
- Invitation Admin, tray, alertes Gemini/Discord
