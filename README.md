# Punkartuchera

Gestor de bandas para el juego de escaramuzas **Punkapocalyptic**: crea tus bandas, haz fotos a tus miniaturas y consulta sus atributos de un vistazo, también sin conexión.

Es una aplicación web progresiva (PWA) hecha con Blazor WebAssembly. Se instala en el móvil desde el navegador y guarda los datos en el propio dispositivo.

## Funciones

- Bandas propias guardadas en el dispositivo (IndexedDB), con facción, notas y miembros.
- Fichas de miembro: foto con la cámara o de la galería (con editor para encuadrarla en cuadrado) o un retrato genérico; atributos, balas, habilidades y equipo.
- Duplicar y borrar bandas; las fotos se limpian solas.
- Reglas del reglamento oficial gratuito: buscador, reglas básicas, reglas especiales, armas y equipo, y perfiles de todas las facciones.
- Pasar la banda al rival con un QR (sin fotos) o un archivo `.punkband` (con fotos), siempre sin balas, y cargar la suya como banda rival de solo lectura escaneando su QR o abriendo su archivo.
- Funciona sin conexión una vez cargada.

- Generador de mapas: eliges cuánta escenografía de cada tipo hay y la app la reparte de forma simétrica y justa para los dos bandos, con un código para que el rival vea el mismo mapa.

- Partidas: ronda, quién ha actuado y quién ha caído en cada banda. En un solo móvil o cada uno en el suyo, unidos con un código de seis caracteres (ver [docs/PARTIDAS-EN-LINEA.md](docs/PARTIDAS-EN-LINEA.md)).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)

## Uso en local

```bash
dotnet run --project src/Punkartuchera.Web
```

Se abre en http://localhost:5080 (con el perfil `https`, también en https://localhost:5081). Son los mismos puertos que admite el relé de partidas en línea en local.

Tests:

```bash
dotnet test
```

## Publicación

Cada push a `main` compila, pasa los tests y publica la app en GitHub Pages (`.github/workflows/pages.yml`). En el repositorio, en *Settings → Pages*, la fuente debe ser **GitHub Actions**.

El relé de partidas en línea se despliega aparte en Azure: pasos en [docs/PARTIDAS-EN-LINEA.md](docs/PARTIDAS-EN-LINEA.md).

## Estructura

| Proyecto | Contenido |
| --- | --- |
| `src/Punkartuchera.Core` | Modelo, reglas, servicios e interfaces de almacenamiento y partidas. Sin dependencias de interfaz. |
| `src/Punkartuchera.UI` | Páginas, componentes, tema visual y almacenamiento IndexedDB. Reutilizable desde una app MAUI. |
| `src/Punkartuchera.Web` | Host Blazor WebAssembly (PWA). |
| `src/Punkartuchera.Relay` | Relé de partidas en línea (Azure Functions + Azure SignalR Service). |
| `src/Punkartuchera.Relay.Contracts` | Contratos compartidos entre la app y el relé. |
| `infra` | Recursos de Azure del relé en Bicep. |
| `tests/Punkartuchera.Core.Tests` | Tests de la lógica de negocio. |
| `tests/Punkartuchera.Relay.Tests` | Tests del relé, incluidos intentos de abuso. |

Más detalle en [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md).

## Aviso

Proyecto de aficionados sin relación oficial con los autores de Punkapocalyptic. Los nombres del juego y sus facciones pertenecen a sus propietarios.

Usa [QRCoder](https://github.com/codebude/QRCoder) (MIT) para dibujar los QR y [jsQR](https://github.com/cozmo/jsQR) (Apache 2.0, licencia en `src/Punkartuchera.UI/wwwroot/js/vendor/`) para leerlos en navegadores sin lector propio.
