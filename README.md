<div align="center">

# CursorVault

### Gestionnaire moderne de curseurs pour Windows

Centralisez, importez, appliquez, sauvegardez et organisez vos packs de curseurs depuis une seule application.

[![Latest release](https://img.shields.io/github/v/release/GaLeX-Le-Penguin/CursorVault?display_name=tag&sort=semver&label=version)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/GaLeX-Le-Penguin/CursorVault/total?label=t%C3%A9l%C3%A9chargements)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![Architecture](https://img.shields.io/badge/architecture-x64-informational)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)

[Dernière version](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) · [Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) · [Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>

---

## À propos

**CursorVault** est une application Windows développée par **GLX** pour remplacer la gestion manuelle des curseurs par une bibliothèque centralisée.

Elle permet d'appliquer un pack complet, d'importer des fichiers `.cur` et `.ani`, de gérer les schémas déjà présents dans Windows, de créer ses propres packs, de sauvegarder sa configuration et de restaurer rapidement un état précédent.

L'objectif est simple : gérer les curseurs Windows depuis une seule interface, sans devoir configurer chaque rôle manuellement dans le Panneau de configuration.

---

## Fonctionnalités

### Bibliothèque

- Bibliothèque locale de packs.
- Favoris.
- Recherche par nom, créateur et description.
- Filtres : tous, complets, incomplets, animés, statiques et favoris.
- Tri par favoris, nom, créateur, complétude et date d'ajout.
- Détection des variantes liées Light / Dark.
- Pack aléatoire.
- Affichage du créateur original.

### Importation et création

- Glisser-déposer de dossiers, `.zip`, `.cur` et `.ani`.
- Installation locale automatique des imports.
- Détection des doublons avec choix Remplacer / Conserver les deux / Annuler.
- Créateur de packs intégré.
- Renommage d'un pack sans supprimer le crédit du créateur original.
- Export d'un pack en ZIP.
- Génération d'un `install.inf` Windows.

### Analyse et sécurité des packs

- Validation des fichiers CUR et ANI avant application.
- Vérification des en-têtes binaires.
- Détection des fichiers manquants, invalides ou dupliqués.
- Compteur de rôles configurés sur les 17 rôles Windows pris en charge.
- Réparation des références cassées.
- Gestion configurable des rôles manquants.

### Intégration Windows

- Affichage des schémas de curseurs déjà enregistrés dans Windows.
- Détection du schéma actif.
- Application directe d'un schéma Windows.
- Accès au vrai menu `Propriétés de la souris > Pointeurs`.
- Accès au dossier système des curseurs.
- Démarrage avec Windows, optionnel.
- Réduction dans la zone de notification, optionnelle.
- Synchronisation possible avec le thème clair / sombre de Windows.
- Rotation automatique des packs au démarrage, toutes les heures ou tous les jours.

### Sauvegarde et mode portable

- Sauvegarde du schéma Windows actuel.
- Sauvegarde complète au format `.cvb`.
- Restauration des paramètres et des packs.
- Mode portable avec données à côté de l'exécutable.
- Gestion de l'espace utilisé et nettoyage du cache.

### Personnalisation

- Thème sombre.
- Thème clair.
- Thème translucide.
- Couleur personnalisable.
- Choix de la police parmi celles installées dans Windows.
- Interface compacte, normale ou grande.
- Réinitialisation des paramètres.

### Langues

CursorVault peut détecter automatiquement la langue d'affichage de Windows.

Langues intégrées :

- Français
- English
- Español
- Deutsch
- Italiano

Le choix manuel reste prioritaire jusqu'au retour au mode automatique.

### Diagnostic

- Page Diagnostic intégrée.
- Informations système et configuration CursorVault.
- Copie du diagnostic dans le presse-papiers.
- Informations de stockage.
- Nettoyage du cache temporaire.

---

## Captures d'écran

### Accueil
![Accueil CursorVault](docs/screenshots/home.png)

### Bibliothèque
![Bibliothèque CursorVault](docs/screenshots/library.png)

### Curseurs Windows
![Curseurs Windows](docs/screenshots/windows.png)

### Paramètres
![Paramètres CursorVault](docs/screenshots/settings.png)

---

## Installation

1. Ouvrez la [dernière Release](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest).
2. Téléchargez **`CursorVault.zip`** dans les assets.
3. Extrayez complètement l'archive.
4. Lancez **`CursorVault.exe`**.

Structure attendue :

```text
CursorVault/
├── CursorVault.exe
└── CursorVault_Data/
    └── StarterPacks/
```

La build officielle est publiée en **Windows x64, self-contained et single-file**. Le runtime .NET nécessaire est intégré dans l'exécutable.

> Ne déplacez pas uniquement `CursorVault.exe` si `CursorVault_Data` est fourni avec la Release.

---

## Mises à jour

CursorVault utilise directement **GitHub Releases** pour rechercher les nouvelles versions. Aucun manifeste ou URL n'est demandé à l'utilisateur.

Convention des Releases :

```text
Tag   : v1.1.6, v1.1.7, v1.2.0, ...
Asset : CursorVault.zip
```

Le fichier joint doit garder exactement le nom :

```text
CursorVault.zip
```

---

## Données locales

En mode normal, CursorVault stocke ses données utilisateur dans :

```text
%LocalAppData%\CursorVault
```

Selon les fonctions utilisées, ce dossier peut contenir :

```text
Packs/
Backups/
Cache/
settings.json
```

En mode portable, les données sont enregistrées dans le dossier portable utilisé par CursorVault.

---

## Fonctionnement Windows

CursorVault configure les curseurs du compte Windows courant puis demande au système de les recharger.

Les emplacements système utilisés peuvent inclure :

```text
HKCU\Control Panel\Cursors
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

La seconde clé n'est utilisée que lorsque le démarrage automatique avec Windows est activé.

---

## Compilation depuis les sources

<details>
<summary>Afficher les instructions de compilation</summary>

### Technologies

- C#
- .NET 8
- WPF
- Windows API / DWM
- Registre Windows
- GitHub Releases API

### Prérequis

- Windows 10 ou Windows 11 x64
- Visual Studio 2022
- Charge de travail `Développement Desktop .NET`
- SDK .NET 8

### Développement

Ouvrez :

```text
CursorVault.sln
```

Compilation classique :

```text
Ctrl + Maj + B
```

### Publication finale

Pour générer la version destinée aux utilisateurs :

```text
PUBLIER-GITHUB.bat
```

Le script produit :

```text
Release/
├── CursorVault/
│   ├── CursorVault.exe
│   └── CursorVault_Data/
└── CursorVault.zip
```

`Release/CursorVault.zip` est le fichier à joindre à la GitHub Release.

</details>

---

## Publier une nouvelle version

<details>
<summary>Afficher la procédure de Release</summary>

1. Modifiez la version dans `CursorVault/CursorVault.csproj` :

```xml
<Version>1.1.7</Version>
```

2. Exécutez :

```text
PUBLIER-GITHUB.bat
```

3. Testez `Release/CursorVault.zip` dans un dossier vide.

4. Créez une GitHub Release :

```text
Tag           : v1.1.7
Titre         : CursorVault v1.1.7
Fichier joint : CursorVault.zip
```

Le tag doit contenir une version valide. N'utilisez pas `CursorVault` comme tag.

</details>

---

## Packs tiers et crédits

CursorVault peut distribuer ou importer des packs créés par différents auteurs.

Le créateur original est conservé dans l'application et ne doit pas être remplacé lors du renommage d'un pack.

Les droits et conditions de redistribution des packs tiers restent ceux de leurs créateurs respectifs. Avant d'inclure un pack dans une Release officielle, vérifiez que sa redistribution est autorisée.

Il est recommandé d'ajouter au dépôt :

```text
CREDITS.md
THIRD_PARTY_NOTICES.md
```

Ces fichiers peuvent centraliser les auteurs, sources et licences des packs inclus.

---

## Signaler un bug

Utilisez les [Issues GitHub](https://github.com/GaLeX-Le-Penguin/CursorVault/issues) et fournissez si possible :

- version de CursorVault ;
- version de Windows ;
- étapes pour reproduire le problème ;
- message d'erreur exact ;
- diagnostic CursorVault ;
- capture d'écran si nécessaire.

---

## Contribution

Les corrections et améliorations sont les bienvenues si le dépôt est ouvert aux contributions.

Avant une modification importante, ouvrez une Issue décrivant le changement prévu.

Pour une Pull Request :

1. utilisez une branche dédiée ;
2. conservez l'architecture et le style existants ;
3. vérifiez la compilation Release ;
4. testez les thèmes sombre, clair et translucide ;
5. mettez à jour les traductions pour tout nouveau texte d'interface ;
6. décrivez clairement les changements effectués.

---

## Limites actuelles

- La build officielle cible Windows x64.
- Certains effets translucides dépendent de la version de Windows et de sa composition graphique.
- Un pack incomplet ne peut fournir que les rôles réellement présents dans ses fichiers.
- CursorVault ne transfère aucun droit sur les packs tiers.

---

## Copyright

```text
CursorVault
Copyright © 2026 GLX
```

CursorVault et son identité visuelle sont développés par **GLX**.

Les créations tierces restent la propriété de leurs auteurs respectifs.

---

<div align="center">

Développé par **GLX** pour Windows.

[Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) · [Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>
