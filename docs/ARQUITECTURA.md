# Arquitectura

## Capas

```
Punkartuchera.Web  ──>  Punkartuchera.UI  ──>  Punkartuchera.Core
   (host PWA)            (Razor, tema, IndexedDB)    (modelo y lógica)
```

- **Core** no conoce Blazor ni el navegador. Todo lo que dependa de la plataforma se expresa como interfaz.
- **UI** es una Razor Class Library: contiene el router (`Routes.razor`), el layout, las páginas y la implementación local de las interfaces. Una futura app .NET MAUI Blazor Hybrid solo tendría que renderizar `<Routes />` y llamar a `AddPunkartucheraUi()`.
- **Web** es un host mínimo: `index.html`, manifiesto, service worker e iconos.

## Puntos de extensión

| Interfaz | Implementación actual | Para qué |
| --- | --- | --- |
| `IBandRepository` | `IndexedDbBandRepository` (UI), `InMemoryBandRepository` (Core, tests) | Guardar bandas. Una versión en la nube implementa esta interfaz. |
| `IPhotoStore` | `IndexedDbPhotoStore`, `InMemoryPhotoStore` | Fotos de miniaturas, separadas de la banda. |
| `IRulesCatalog` | `PlaceholderRulesCatalog` (**provisional**) | Facciones, atributos y tipos de miembro. |
| `ITerrainCatalog` | `DefaultTerrainCatalog` (**provisional**) | Tipos de escenografía (huella en cm, cobertura, si tapa la vista) y tamaños de mesa. |
| `IMatchService` | `LocalMatchService` | Partida en un dispositivo. Los métodos en línea lanzan `OnlineMatchesUnavailableException`. |

Los servicios se registran en `ServiceCollectionExtensions.AddPunkartucheraUi()`.

## Datos

- `Band` → `Miniature` → `Equipment`, `PhotoRef`.
- Los atributos se guardan como diccionario `clave → valor`; las claves vienen de `IRulesCatalog.Stats`, así cambiar el reglamento no rompe datos guardados.
- `Band.SchemaVersion` permite migrar datos antiguos.
- `Band.Ownership`: `Own` (editable) o `Rival` (solo lectura; `BandService` rechaza cambios).
- `Miniature.Ammo` son las balas; la exportación a rivales debe ponerlas a cero.
- `BandSerializer` es el formato JSON único (camelCase, enums como texto, generación de código para el recortado de WebAssembly). Úsalo también para exportar.

### IndexedDB

Base `punkapocalyptic`, versión 1, almacenes `bands` y `photos` con registros `{ key, value }` donde `value` es texto (JSON de la banda o data URL de la foto). Acceso en `wwwroot/js/punkDb.js` a través de `KeyValueDb`.

Para añadir un almacén nuevo: añádelo a `STORES` y sube `DB_VERSION` en `punkDb.js`.

## Navegación y disposición

La disposición sigue el modelo de una hoja de equipo:

- **Barra superior** con botón de menú y título. La navegación va en un **menú lateral** (`MainLayout`) que se alimenta de `Navigation/AppNavigation.cs`. Las secciones que aún no existen aparecen como "pronto"; para activar una, crea la página con esa ruta en `Pages/<Sección>/` y pon `Available: true`.
- **Barra de contexto** (`.context-bar`): selector de la banda o facción activa con una píldora encima y un botón ⋮ (`ActionsMenu`) con acciones secundarias.
- **Pestañas** a todo el ancho (`PageTabs`) bajo la cabecera.
- **Contenido**: fichas `datacard` (cabecera de chapa con recuadros de atributos `.statbox`, cuerpo de papel con `.data-table`), o cajas de texto con título (`.rule-grid` y `.rule-box`).
- La acción principal de cada pantalla es un botón flotante (`.btn.btn--blood.fab`).

## Tema visual

`wwwroot/css/punk-theme.css` define las variables (`--punk-*`) y las clases comunes. Importa `punk-textures.css`, con las texturas generadas como SVG (`--punk-tex-grain`, `--punk-tex-grime`, `--punk-tex-paper`, `--punk-tex-fibre`, `--punk-tex-metal`, `--punk-tex-speckle`, bordes rasgados `--punk-edge-torn-*` y brochazos `--punk-brush-*`).

Superficies: `card` (chapa oscura) y `paper` (papel de arena con borde rasgado). Utilidades: `tape` (cinta adhesiva), `stamp` (sello de goma), `distress` (letras gastadas), `scribble` (nota a rotulador). Otras clases: `btn` (`--primary`, `--blood`, `--ghost`, `--icon`), `chip`, `tag`, `stepper`, `stat-strip`, `notice`, `field`, `section-title`, `page-title`, `page-kicker`, `punk-empty`.

Las secciones nuevas deben reutilizarlas en lugar de definir colores o texturas propias.

Componentes reutilizables en `Components/`: `Icon`, `MiniPortrait`, `MemberCard`, `PageTabs`, `ActionsMenu`, `Stepper`, `TagEditor`, `ConfirmButton`, `PhotoPicker`, `PlaceholderNotice`.

## Mapas

`Core/Maps/MapGenerator` reparte la escenografía elegida sobre la mesa:

- **Simetría central**: cada pieza de la mitad norte tiene su gemela girada 180° en la sur, así las dos bandas tienen el mismo terreno delante. Si un tipo es impar, la pieza que sobra va a la línea central, al centro si cabe.
- Las zonas de despliegue quedan libres y entre piezas hay un hueco mínimo (`DefaultMinGap`, 7 cm). Si no cabe todo, lo vuelve a intentar con menos hueco y, en último caso, devuelve las piezas que no ha podido colocar (`MapLayout.Leftovers`) en lugar de amontonarlas.
- Cada pieza se coloca en el mejor de varios sitios al azar: el que más se aleja de lo ya colocado y de los bordes.
- **Determinista**: usa su propio generador (`SeededRandom`, mulberry32) y un hash FNV-1a del código (`MapSeed`), porque `System.Random` no garantiza la misma secuencia entre versiones de .NET. Mismo código + misma mesa + mismo terreno = mismo mapa en cualquier dispositivo.

La página `/mapa` guarda todo en el enlace (`?codigo=K7Q2XM&mesa=120x120&terreno=barricada-4.ruina-2`), así que compartir o recargar la página da el mismo mapa sin guardar nada. `TerrainSelection` convierte las cantidades a ese formato y al revés. Los estilos del plano están en `wwwroot/css/punk-map.css`.

## Fotos

`PhotoPicker` usa `InputFile` con `capture="environment"` (abre la cámara en el móvil) y reduce la imagen en el navegador a 640 px en JPEG antes de guardarla. Los retratos genéricos están en `wwwroot/img/stock/` (`StockImages`).
