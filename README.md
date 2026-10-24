# Punkartuchera

Gestor de bandas para el juego de escaramuzas **Punkapocalyptic**: crea tus bandas, haz fotos a tus miniaturas y consulta sus atributos de un vistazo, también sin conexión.

Es una aplicación web progresiva (PWA) hecha con Blazor WebAssembly. Se instala en el móvil desde el navegador y guarda los datos en el propio dispositivo.

## Funciones

- Bandas propias guardadas en el dispositivo (IndexedDB), con facción, notas y miembros.
- Fichas de miembro: foto con la cámara, de la galería o un retrato genérico; atributos, balas, habilidades y equipo.
- Duplicar y borrar bandas; las fotos se limpian solas.
- Funciona sin conexión una vez cargada.

Generador de mapas: eliges cuánta escenografía de cada tipo hay y la app la reparte de forma simétrica y justa para los dos bandos, con un código para que el rival vea el mismo mapa.

En preparación: consulta de reglas, bandas rivales compartidas y partidas activas.

> Los atributos y tipos de miembro actuales son **provisionales** (ver `PlaceholderRulesCatalog`). Se sustituirán por los del reglamento.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)

## Uso en local

```bash
dotnet run --project src/Punkartuchera.Web
```

Tests:

```bash
dotnet test
```

## Publicación

Cada push a `main` compila, pasa los tests y publica la app en GitHub Pages (`.github/workflows/pages.yml`). En el repositorio, en *Settings → Pages*, la fuente debe ser **GitHub Actions**.

## Estructura

| Proyecto | Contenido |
| --- | --- |
| `src/Punkartuchera.Core` | Modelo, reglas, servicios e interfaces de almacenamiento y partidas. Sin dependencias de interfaz. |
| `src/Punkartuchera.UI` | Páginas, componentes, tema visual y almacenamiento IndexedDB. Reutilizable desde una app MAUI. |
| `src/Punkartuchera.Web` | Host Blazor WebAssembly (PWA). |
| `tests/Punkartuchera.Core.Tests` | Tests de la lógica de negocio. |

Más detalle en [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md).

## Aviso

Proyecto de aficionados sin relación oficial con los autores de Punkapocalyptic. Los nombres del juego y sus facciones pertenecen a sus propietarios.
