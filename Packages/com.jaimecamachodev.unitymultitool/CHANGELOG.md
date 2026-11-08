# Changelog

Todos los cambios relevantes de este paquete.
El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [SemVer](https://semver.org/lang/es/).

## [1.3.2] - 2026-11-08

- El paquete pasa de ~139 MB a unos 5 MB: se saca de `Editor/Resources` un humo de prueba horneado con Alembic → VAT (`SmokeSurface_*`, dos texturas de 67 MB) que se coló en noviembre de 2025. Nada del paquete lo usaba; sigue en el proyecto de ejemplo del repositorio.

## [1.3.1] - 2026-11-06

- ffmpeg ya no se espera dentro del paquete (nunca llegó a publicarse con él, así que las herramientas de imagen y vídeo fallaban en otros equipos). Se busca en la ruta elegida a mano, el PATH y una copia descargada; si no hay ninguna, en Windows ofrece descargar ffmpeg 8.0.1 y comprueba su SHA-256.
- Nuevo menú **Tools → JaimeCamachoDev → Multitool → Elegir ffmpeg…**.
- Quitados del paquete los `.meta` huérfanos de `ffmpeg.exe`, `ffplay.exe` y `ffprobe.exe` y la documentación de ffmpeg.

## [1.3.0] - 2026-10-22

- Declara las dependencias com.unity.editorcoroutines y com.unity.formats.alembic: sin ellas el paquete no compilaba en proyectos que no las tuvieran.
- README, LICENSE y CHANGELOG incluidos en el paquete.
