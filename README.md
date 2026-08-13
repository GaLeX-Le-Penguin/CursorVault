# CursorVault

<div align="center">

🇫🇷 **Français** · [🇬🇧 English](README_EN.md) · [🇪🇸 Español](README_ES.md) · [🇩🇪 Deutsch](README_DE.md) · [🇮🇹 Italiano](README_IT.md)

### Gestionnaire moderne de curseurs pour Windows

Centralisez, importez, appliquez, sauvegardez et organisez vos packs de curseurs depuis une seule application.

[![Latest release](https://img.shields.io/github/v/release/GaLeX-Le-Penguin/CursorVault?display_name=tag&sort=semver&label=version)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/GaLeX-Le-Penguin/CursorVault/total?label=downloads)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![Architecture](https://img.shields.io/badge/architecture-x64-informational)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)

### [⬇️ Télécharger CursorVault](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

[Dernière version](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Signaler un problème](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>

---

## À propos

**CursorVault** est une application Windows développée par **GLX** permettant de centraliser et gérer facilement ses curseurs Windows.

Elle permet d'importer des fichiers `.cur` et `.ani`, d'organiser des packs complets, d'appliquer rapidement un nouveau thème de curseurs et de gérer les schémas déjà présents dans Windows.

L'objectif est simple : proposer une alternative moderne et pratique à la configuration manuelle des curseurs depuis le Panneau de configuration Windows.

---

## Aperçu

### Accueil

![Accueil CursorVault](docs/screenshots/home.png)

### Bibliothèque

![Bibliothèque CursorVault](docs/screenshots/library.png)

### Curseurs Windows

![Curseurs Windows](docs/screenshots/windows.png)

### Diagnostic

![Diagnostic CursorVault](docs/screenshots/Diagnostic.png)

### Paramètres

![Paramètres CursorVault](docs/screenshots/settings.png)

---

## Fonctionnalités

### 📚 Bibliothèque de curseurs

- Bibliothèque locale de packs
- Favoris
- Recherche par nom, créateur et description
- Filtres pour les packs complets, incomplets, animés, statiques et favoris
- Tri par favoris, nom, créateur, complétude ou date d'ajout
- Détection des variantes Light / Dark
- Sélection aléatoire d'un pack
- Affichage du créateur original

### 📥 Importation

- Import de dossiers
- Import d'archives ZIP
- Import de fichiers `.cur`
- Import de fichiers `.ani`
- Glisser-déposer directement dans CursorVault
- Installation locale automatique
- Détection des doublons
- Export d'un pack en ZIP

### 🛠️ Création et analyse

- Créateur de packs intégré
- Renommage des packs
- Conservation du crédit du créateur original
- Analyse des rôles Windows disponibles
- Détection des fichiers manquants
- Détection des fichiers invalides
- Détection des doublons
- Validation des fichiers CUR et ANI
- Réparation des références cassées
- Génération d'un fichier `install.inf`

### 🖱️ Intégration Windows

- Application automatique d'un pack complet
- Gestion des rôles de curseurs Windows
- Affichage des schémas déjà installés dans Windows
- Détection du schéma actuellement actif
- Application directe d'un schéma Windows
- Accès aux paramètres de pointeurs Windows
- Accès au dossier système des curseurs
- Synchronisation possible avec le thème clair / sombre de Windows

### ⭐ Automatisation

- Rotation automatique des packs
- Rotation au démarrage
- Rotation horaire
- Rotation quotidienne
- Rotation limitée aux favoris
- Démarrage avec Windows
- Réduction dans la zone de notification

### 💾 Sauvegarde

- Sauvegarde du schéma Windows actuel
- Sauvegarde complète au format `.cvb`
- Restauration des paramètres
- Restauration des packs
- Restauration des favoris
- Mode portable
- Gestion de l'espace de stockage
- Nettoyage du cache

### 🎨 Personnalisation

- Thème sombre
- Thème clair
- Couleur personnalisable
- Choix de la police parmi celles installées dans Windows
- Interface compacte, normale ou grande
- Réinitialisation des paramètres

### 🌍 Langues

CursorVault peut automatiquement utiliser la langue de Windows.

Langues disponibles :

- Français
- English
- Español
- Deutsch
- Italiano

Une langue peut également être sélectionnée manuellement dans les paramètres.

### 🔍 Diagnostic

- Page Diagnostic intégrée
- Informations système
- Informations sur la configuration CursorVault
- Copie du diagnostic dans le presse-papiers
- Informations de stockage
- Nettoyage du cache

---

## Installation

1. Ouvrez la [dernière version de CursorVault](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
2. Téléchargez **`CursorVault.zip`**
3. Extrayez complètement l'archive
4. Lancez **`CursorVault.exe`**

```text
CursorVault/
├── CursorVault.exe
└── CursorVault_Data/
```

Aucune installation séparée de .NET n'est nécessaire avec la version officielle.

---

## Configuration requise

- Windows 10 ou Windows 11
- Architecture x64
- Aucune installation séparée de .NET requise

---

## Mises à jour

CursorVault peut vérifier automatiquement si une nouvelle version est disponible via **GitHub Releases**.

### [⬇️ Télécharger la dernière version](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

---

## Données utilisateur

CursorVault conserve localement les données nécessaires à son fonctionnement, notamment les packs importés, favoris, paramètres, sauvegardes et fichiers de cache.

Un **mode portable** permet également de conserver les données avec l'application.

---

## Formats pris en charge

- `.cur`
- `.ani`

Les packs sont analysés avant leur application afin de détecter les fichiers manquants ou invalides.

---

## Crédits

CursorVault peut inclure ou prendre en charge des packs de curseurs créés par différents auteurs.

Le créateur original d'un pack reste indiqué dans l'application lorsqu'il est connu.

Les packs tiers restent la propriété de leurs auteurs respectifs.

CursorVault et son identité visuelle sont développés par **GLX**.

---

## Signaler un problème

Vous avez trouvé un bug ou souhaitez proposer une amélioration ?

Utilisez les [Issues GitHub](https://github.com/GaLeX-Le-Penguin/CursorVault/issues).

Indiquez si possible la version de CursorVault, votre version de Windows, les étapes permettant de reproduire le problème, le message d'erreur rencontré, une capture d'écran et le diagnostic CursorVault si nécessaire.

---

## Compatibilité

| Système | Support |
|---|---|
| Windows 11 x64 | ✅ |
| Windows 10 x64 | ✅ |
| Windows ARM | Non testé |
| Linux | ❌ |
| macOS | ❌ |

---

## Copyright

**CursorVault**  
Copyright © 2026 GLX

CursorVault et son identité visuelle sont développés par **GLX**.

Les créations tierces et packs de curseurs restent la propriété de leurs auteurs respectifs.

---

<div align="center">

### CursorVault

Développé par **GLX** pour Windows.

[⬇️ Télécharger](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>
