# CursorVault

<div align="center">

[🇫🇷 Français](README.md) · [🇬🇧 English](README_EN.md) · 🇪🇸 **Español** · [🇩🇪 Deutsch](README_DE.md) · [🇮🇹 Italiano](README_IT.md)

### Gestor moderno de cursores para Windows

Centraliza, importa, aplica, guarda y organiza tus paquetes de cursores desde una sola aplicación.

[![Latest release](https://img.shields.io/github/v/release/GaLeX-Le-Penguin/CursorVault?display_name=tag&sort=semver&label=version)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/GaLeX-Le-Penguin/CursorVault/total?label=downloads)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![Architecture](https://img.shields.io/badge/architecture-x64-informational)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)

### [⬇️ Descargar CursorVault](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

[Última versión](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Versiones](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Informar de un problema](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>

---

## Acerca de

**CursorVault** es una aplicación para Windows desarrollada por **GLX** que permite centralizar y gestionar fácilmente los cursores de Windows.

Permite importar archivos `.cur` y `.ani`, organizar paquetes completos, aplicar rápidamente un nuevo tema de cursores y gestionar los esquemas ya instalados en Windows.

El objetivo es simple: ofrecer una alternativa moderna y práctica a la configuración manual de cursores desde el Panel de control de Windows.

---

## Vista previa

### Inicio

![Inicio de CursorVault](docs/screenshots/home.png)

### Biblioteca

![Biblioteca de CursorVault](docs/screenshots/library.png)

### Cursores de Windows

![Cursores de Windows](docs/screenshots/windows.png)

### Diagnóstico

![Diagnóstico de CursorVault](docs/screenshots/Diagnostic.png)

### Configuración

![Configuración de CursorVault](docs/screenshots/settings.png)

---

## Funciones

### 📚 Biblioteca de cursores

- Biblioteca local de paquetes
- Favoritos
- Búsqueda por nombre, creador y descripción
- Filtros para paquetes completos, incompletos, animados, estáticos y favoritos
- Orden por favoritos, nombre, creador, integridad o fecha de incorporación
- Detección de variantes Light / Dark
- Selección aleatoria de paquetes
- Visualización del creador original

### 📥 Importación

- Importación de carpetas
- Importación de archivos ZIP
- Importación de archivos `.cur`
- Importación de archivos `.ani`
- Arrastrar y soltar directamente en CursorVault
- Instalación local automática
- Detección de duplicados
- Exportación de paquetes en ZIP

### 🛠️ Creación y análisis

- Creador de paquetes integrado
- Cambio de nombre de paquetes
- Conservación del crédito del creador original
- Análisis de los roles de cursor de Windows disponibles
- Detección de archivos ausentes
- Detección de archivos no válidos
- Detección de duplicados
- Validación de CUR y ANI
- Reparación de referencias rotas
- Generación de `install.inf`

### 🖱️ Integración con Windows

- Aplicación automática de paquetes completos
- Gestión de los roles de cursor de Windows
- Visualización de esquemas ya instalados
- Detección del esquema activo
- Aplicación directa de esquemas de Windows
- Acceso a la configuración de punteros
- Acceso a la carpeta de cursores del sistema
- Sincronización opcional con el tema claro / oscuro de Windows

### ⭐ Automatización

- Rotación automática de paquetes
- Rotación al iniciar
- Rotación cada hora
- Rotación diaria
- Rotación limitada a favoritos
- Inicio con Windows
- Minimización al área de notificación

### 💾 Copias de seguridad

- Copia del esquema actual de Windows
- Copia completa en formato `.cvb`
- Restauración de configuración
- Restauración de paquetes
- Restauración de favoritos
- Modo portátil
- Gestión del almacenamiento
- Limpieza de caché

### 🎨 Personalización

- Tema oscuro
- Tema claro
- Color personalizable
- Selección de fuentes instaladas en Windows
- Interfaz compacta, normal o grande
- Restablecimiento de configuración

### 🌍 Idiomas

CursorVault puede utilizar automáticamente el idioma de Windows.

Idiomas disponibles:

- Français
- English
- Español
- Deutsch
- Italiano

También se puede seleccionar un idioma manualmente en la configuración.

### 🔍 Diagnóstico

- Página de diagnóstico integrada
- Información del sistema
- Información de configuración de CursorVault
- Copia del diagnóstico al portapapeles
- Información de almacenamiento
- Limpieza de caché

---

## Instalación

1. Abre la [última versión de CursorVault](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
2. Descarga **`CursorVault.zip`**
3. Extrae completamente el archivo
4. Ejecuta **`CursorVault.exe`**

```text
CursorVault/
├── CursorVault.exe
└── CursorVault_Data/
```

La versión oficial no requiere una instalación separada de .NET.

---

## Requisitos del sistema

- Windows 10 o Windows 11
- Arquitectura x64
- No requiere una instalación separada de .NET

---

## Actualizaciones

CursorVault puede comprobar automáticamente si hay nuevas versiones mediante **GitHub Releases**.

### [⬇️ Descargar la última versión](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

---

## Datos del usuario

CursorVault almacena localmente los datos necesarios para su funcionamiento, incluidos los paquetes importados, favoritos, configuración, copias de seguridad y caché.

También está disponible un **modo portátil** para conservar los datos junto a la aplicación.

---

## Formatos compatibles

- `.cur`
- `.ani`

Los paquetes se analizan antes de aplicarlos para detectar archivos ausentes o no válidos.

---

## Créditos

CursorVault puede incluir o admitir paquetes de cursores creados por distintos autores.

El creador original de un paquete permanece indicado en la aplicación cuando se conoce.

Los paquetes de terceros siguen siendo propiedad de sus respectivos autores.

CursorVault y su identidad visual son desarrollados por **GLX**.

---

## Informar de un problema

¿Has encontrado un error o quieres proponer una mejora?

Utiliza [GitHub Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues).

Cuando sea posible, incluye la versión de CursorVault, la versión de Windows, los pasos para reproducir el problema, el mensaje de error, una captura de pantalla y el diagnóstico de CursorVault.

---

## Compatibilidad

| Sistema | Compatibilidad |
|---|---|
| Windows 11 x64 | ✅ |
| Windows 10 x64 | ✅ |
| Windows ARM | No probado |
| Linux | ❌ |
| macOS | ❌ |

---

## Copyright

**CursorVault**  
Copyright © 2026 GLX

CursorVault y su identidad visual son desarrollados por **GLX**.

Las creaciones y paquetes de cursores de terceros siguen siendo propiedad de sus respectivos autores.

---

<div align="center">

### CursorVault

Desarrollado por **GLX** para Windows.

[⬇️ Descargar](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Versiones](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>
