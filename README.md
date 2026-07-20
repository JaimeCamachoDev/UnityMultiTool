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
    "com.jaimecamachodev.unitymultitool": "1.0.0"
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

<footer>
   
## Después de crear el repositorio desde la plantilla, asegúrate de revisar lo siguiente:

### 📸 Social Preview
- [ ] Sube una imagen `preview.png` personalizada en `Settings → Social Preview`.

### ⚙️ Repository Features
Desactiva funciones que no necesitas en `Settings → Features`:

- [ ] Desactivar **Projects**
- [ ] Desactivar **Wiki**
- [ ] Desactivar **Packages**
- [ ] Desactivar **Environments** (Deployments)
- [ ] Confirmar que **Releases** sigue activado ✅

### 🎨 Personalización visual
- [ ] Cambiar imagen del banner de portada.
- [ ] Dejar Topics necesarios.


</footer>
