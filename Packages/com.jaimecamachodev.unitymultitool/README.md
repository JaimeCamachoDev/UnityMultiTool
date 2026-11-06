<header>

![Banner](https://github.com/user-attachments/assets/5b933a56-0ece-452a-99c0-1a641485a6b9)

# **MultiTool**

_**Herramienta con diversos usos para unity**_

</header>

## 📦 Instalación (UnityMultiTool vía Package Manager)

Esta tool se distribuye como paquete de Unity Package Manager con el id `com.jaimecamachodev.unitymultitool`, publicado en npm.

Para instalarla en **otro proyecto de Unity**, añade un scoped registry a tu `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "JaimeCamachoDevs",
      "url": "https://registry.npmjs.org",
      "scopes": [
        "com.jaimecamachodev"
      ]
    }
  ],
  "dependencies": {
    "com.jaimecamachodev.unitymultitool": "1.3.1"
  }
}
```

También puedes hacerlo desde el Editor:
1. `Edit > Project Settings > Package Manager`.
2. En **Scoped Registries**, pulsa `+` y añade:
   - **Name:** `JaimeCamachoDevs`
   - **URL:** `https://registry.npmjs.org`
   - **Scope(s):** `com.jaimecamachodev`
3. Abre `Window > Package Manager`, cambia el desplegable a **My Registries** y busca **UnityMultiTool** para instalarla.

---

## 🎞️ ffmpeg (herramientas de imagen y vídeo)

*Split texture into channels*, *Merge channels* y *Video to frames* usan ffmpeg, que no va dentro del paquete (pesa ~100 MB). La herramienta lo busca en este orden:

1. El ejecutable elegido en **Tools → JaimeCamachoDev → Multitool → Elegir ffmpeg…**
2. El `PATH` del sistema (en macOS también `/opt/homebrew/bin` y `/usr/local/bin`).
3. La copia descargada por la propia herramienta.

Si no lo encuentra, en Windows ofrece descargar ffmpeg 8.0.1 (build *essentials* de gyan.dev, ~100 MB). Comprueba su SHA-256 y lo guarda en `%LOCALAPPDATA%\JaimeCamachoDev\ffmpeg-8.0.1`, donde lo comparten todos los proyectos. En macOS y Linux, instálalo con `brew install ffmpeg` o con el gestor de paquetes del sistema.
