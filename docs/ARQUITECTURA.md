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

- **Barra superior** con botón de menú y título. La navegación va en un **menú lateral** (`MainLayout`) que se alimenta de `Navigation/AppNavigation.cs`. Las secciones `reglas`, `mapa` y `partida` aparecen como "pronto"; para activar una, crea la página con esa ruta en `Pages/<Sección>/` y pon `Available: true`.
- **Barra de contexto** (`.context-bar`): selector de la banda o facción activa con una píldora encima y un botón ⋮ (`ActionsMenu`) con acciones secundarias.
- **Pestañas** a todo el ancho (`PageTabs`) bajo la cabecera.
- **Contenido**: fichas `datacard` (cabecera de chapa con recuadros de atributos `.statbox`, cuerpo de papel con `.data-table`), o cajas de texto con título (`.rule-grid` y `.rule-box`).
- La acción principal de cada pantalla es un botón flotante (`.btn.btn--blood.fab`).

## Tema visual

`wwwroot/css/punk-theme.css` define las variables (`--punk-*`) y las clases comunes. Importa `punk-textures.css`, con las texturas generadas como SVG (`--punk-tex-grain`, `--punk-tex-grime`, `--punk-tex-paper`, `--punk-tex-fibre`, `--punk-tex-metal`, `--punk-tex-speckle`, bordes rasgados `--punk-edge-torn-*` y brochazos `--punk-brush-*`).

Superficies: `card` (chapa oscura) y `paper` (papel de arena con borde rasgado). Utilidades: `tape` (cinta adhesiva), `stamp` (sello de goma), `distress` (letras gastadas), `scribble` (nota a rotulador). Otras clases: `btn` (`--primary`, `--blood`, `--ghost`, `--icon`), `chip`, `tag`, `stepper`, `stat-strip`, `notice`, `field`, `section-title`, `page-title`, `page-kicker`, `punk-empty`.

Las secciones nuevas deben reutilizarlas en lugar de definir colores o texturas propias.

Componentes reutilizables en `Components/`: `Icon`, `MiniPortrait`, `MemberCard`, `PageTabs`, `ActionsMenu`, `Stepper`, `TagEditor`, `ConfirmButton`, `PhotoPicker`, `PlaceholderNotice`.

## Fotos

`PhotoPicker` usa `InputFile` con `capture="environment"` (abre la cámara en el móvil) y reduce la imagen en el navegador a 640 px en JPEG antes de guardarla. Los retratos genéricos están en `wwwroot/img/stock/` (`StockImages`).
