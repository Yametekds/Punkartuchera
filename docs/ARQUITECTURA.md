# Arquitectura

## Capas

```
Punkartuchera.Web  ──>  Punkartuchera.UI  ──>  Punkartuchera.Core  ──>  Punkartuchera.Relay.Contracts
   (host PWA)            (Razor, tema, IndexedDB)    (modelo y lógica)        (código de partida, rutas, límites)
                                                                                      ▲
                                                     Punkartuchera.Relay  ────────────┘
                                                     (Azure Functions: relé de partidas en línea)
```

- **Core** no conoce Blazor ni el navegador. Todo lo que dependa de la plataforma se expresa como interfaz.
- **UI** es una Razor Class Library: contiene el router (`Routes.razor`), el layout, las páginas y la implementación local de las interfaces. Una futura app .NET MAUI Blazor Hybrid solo tendría que renderizar `<Routes />` y llamar a `AddPunkartucheraUi()`.
- **Web** es un host mínimo: `index.html`, manifiesto, service worker e iconos.

## Puntos de extensión

| Interfaz | Implementación actual | Para qué |
| --- | --- | --- |
| `IBandRepository` | `IndexedDbBandRepository` (UI), `InMemoryBandRepository` (Core, tests) | Guardar bandas. Una versión en la nube implementa esta interfaz. |
| `IPhotoStore` | `IndexedDbPhotoStore`, `InMemoryPhotoStore` | Fotos de miniaturas, separadas de la banda. |
| `IRulesCatalog` | `RulebookCatalog` | Facciones, atributos y tipos de miembro, sacados del reglamento (`Rulebook`). |
| `ITerrainCatalog` | `DefaultTerrainCatalog` | Tipos de escenografía (huella en cm, tamaño 1-3, material por defecto), materiales con su grado de Cobertura y mesas del reglamento (90×90 y 120×120, despliegue de 15 y 30 cm). |
| `IMatchService` | `MatchService` | Partida activa, en un móvil o en dos unidos por código. |
| `IMatchRelay` | `SignalRMatchRelay` (UI), `DisabledMatchRelay` (sin relé configurado) | Cliente del relé en línea. |
| `ISavedMatchStore` | `IndexedDbSavedMatchStore`, `InMemorySavedMatchStore` | Guarda las partidas a medias (con mapa, fotos de la mesa y notas) y cuál está en pantalla, para seguirlas otro día. |

Los servicios se registran en `ServiceCollectionExtensions.AddPunkartucheraUi()`.

## Datos

- `Band` → `Miniature` → `Equipment`, `PhotoRef`.
- Los atributos se guardan como diccionario `clave → valor`; las claves vienen de `IRulesCatalog.Stats`, así cambiar el reglamento no rompe datos guardados.
- `Band.SchemaVersion` permite migrar datos antiguos.
- `Band.Ownership`: `Own` (editable) o `Rival` (solo lectura; `BandService` rechaza cambios).
- `Miniature.Ammo` son las balas; la exportación a rivales las pone a cero.
- `Band.SourceId`: en las bandas rivales, el identificador que tenía la banda en el dispositivo del rival.
- `BandSerializer` es el formato JSON único (camelCase, enums como texto, generación de código para el recortado de WebAssembly). Úsalo también para exportar.

### Pasar la banda al rival

`BandExchangeService` (Core, `Sharing/`) exporta una banda propia de dos maneras, un archivo `.punkband` (con fotos) o un QR (sin fotos), y las importa en el otro dispositivo:

- El archivo es un `BandPackage` en JSON: `format` (`punkartuchera-band`), `version`, la banda y las fotos propias en base64 (`photos`, por clave). Los retratos genéricos viajan como referencia.
- Al exportar se quitan las balas (`Ammo = 0`) y las notas de la banda. Las bandas rivales no se pueden volver a exportar.
- Al importar se guarda como `Rival` (solo lectura) con un `Id` nuevo y `SourceId` = el `Id` original; si llega otra vez la misma banda, sustituye a la copia anterior y borra sus fotos. Se descartan fotos que no sean JPEG, PNG o WebP y se rechazan archivos de otro formato o de una versión más nueva.

- El QR lo genera `BandQrCodec`: `PKB1:` + Base45( CRC-32 + JSON compacto comprimido con Deflate ). Base45 usa justo el alfabeto del modo alfanumérico de los QR. El JSON lleva nombres de propiedad de una letra y los atributos como lista en el orden de `IRulesCatalog.Stats`; los retratos genéricos van por clave y las fotos propias no van. Si la suma de comprobación no cuadra, la lectura se rechaza. Una banda normal ocupa unos 400 caracteres; el máximo es `BandQrCodec.MaxLength` (2400): si se pasa, primero se quitan las notas y, si aún no cabe, se pide mandar el archivo.
- Todo lo que llega del rival se trata como no fiable y pasa por `RivalBandSanitizer` antes de guardarse: se quitan nulos, se recortan textos y listas (`MaxMembers`, `MaxTextLength`, `MaxListItems`...), los atributos se limitan a los del reglamento y a su rango, los miembros reciben identificadores nuevos, y las fotos solo se aceptan si sus bytes son de verdad JPEG, PNG o WebP (`IsImage`) y no pasan de `MaxPhotoBytes`. Los retratos genéricos tienen que existir en `StockPortraits` y las fotos oficiales tienen que estar en el catálogo (`IRulesCatalog.IsOfficialPhoto`); si no, se descartan, para que una banda manipulada no pueda hacer que la app cargue imágenes de otra web.
- El archivo se lee con tope (`MaxFileBytes`) aunque quien llame no lo limite. El QR se rechaza si es más largo de lo que cabe en un QR (`MaxReadLength`) o si descomprimido pasa de `MaxJsonBytes`.
- La banda exportada lleva `PlayerName`, el nickname de quien la pasa (sección Usuario, o `PringaoSinNickname`); en el QR va en la propiedad `u`. En los torneos se propone como nombre del jugador.
- Una banda que llega por QR sustituye a la copia que ya hubiera de esa banda y conserva las fotos de los miembros que siguen llamándose igual (por ejemplo, si antes llegó el archivo).

En la interfaz:

- "Pasar al rival" (menú ⋮ de la banda) abre `RivalShareSheet`: el QR (`QrCode`, dibujado en SVG con QRCoder) y el botón para enviar el archivo. `FileSharer` (`wwwroot/js/punkShare.js`) abre el menú de compartir del sistema (Web Share API) y, si el navegador no deja compartir archivos, lo descarga.
- "Cargar rival" (lista de bandas) abre `RivalImportSheet`: enciende la cámara y lee el QR (`wwwroot/js/punkQr.js`, con `BarcodeDetector` si existe y si no con jsQR, en `wwwroot/js/vendor/`, que solo se descarga cuando hace falta), o abre el archivo.

### IndexedDB

Base `punkartuchera`, versión 3, almacenes `bands`, `photos`, `tournaments` (un torneo por entrada, con su Id como clave) y `matches` (una entrada por partida guardada, con su Id como clave, y `current` con el Id de la que está en pantalla) con registros `{ key, value }` donde `value` es texto (JSON de la banda o data URL de la foto). Acceso en `wwwroot/js/punkDb.js` a través de `KeyValueDb`.

Para añadir un almacén nuevo: añádelo a `STORES` y sube `DB_VERSION` en `punkDb.js`.

## Partidas

`MatchService` lleva la ronda y, por miembro, si ya ha actuado y si es baja. Los miembros se copian al empezar (`ActiveMatch.Own` y `Rival`), así editar la banda durante la partida no descoloca nada.

Puede haber varias partidas guardadas a la vez: empezar otra deja la de pantalla para luego, y la lista de `/partida` deja seguir o borrar cualquiera. Cada una guarda también el mapa en el que se juega (`MatchMap`: el código y la consulta del generador, que con el mismo código dan el mismo mapa), fotos de la mesa (en el almacén de fotos, reducidas a 1600 px como mucho) y notas. Eso último no se manda al rival.

- **En un móvil**: las dos bandas están en el dispositivo y se marcan las dos.
- **En línea**: uno crea la partida y el otro entra con el código. Cada móvil manda su banda en el formato del QR y solo marca a los suyos; el estado viaja por el relé (`Punkartuchera.Relay`, Azure Functions + Azure SignalR Service). La dirección del relé va en `wwwroot/appsettings.json` (`Relay:BaseUrl`); vacía, la app solo ofrece la partida en un móvil.

Protocolo, seguridad, coste y despliegue en [PARTIDAS-EN-LINEA.md](PARTIDAS-EN-LINEA.md).

## Torneos

`TournamentService` (Core/Tournaments) lleva torneos por sistema suizo desde un solo móvil; cada mesa juega en el suyo.

- Inscripción: bandas propias y cargadas, cada una con su jugador (`TournamentPlayer`). El nombre se propone desde `Band.PlayerName` (o el nickname del móvil en las propias) y se puede cambiar. Rondas previstas: log2 del número de jugadores, redondeado hacia arriba.
- Primera ronda por sorteo (`SwissPairing.Shuffle`, Fisher-Yates con `SeededRandom` y semilla de `RandomNumberGenerator`, guardada en `Tournament.Seed`). Las siguientes, suizo: por puntos (barajando dentro de cada grupo), con vuelta atrás para no repetir rival ni cruzar dos bandas del mismo jugador; si no hay forma, se relajan primero lo del mismo jugador y luego las revanchas. Con jugadores impares descansa el peor clasificado que no haya descansado.
- Clasificación (`TournamentStandings`): victoria 3, empate 1, derrota 0, descanso 3 (cuando la ronda se ha jugado entera). Desempates: Buchholz, puntos anotados y diferencia.
- Resultados: el móvil de la mesa, con la partida acabada, enseña un QR (`MatchResultCode`): `PKR1:{banda propia}:{banda rival}:{puntos}:{puntos}:{G|P|E}:{crc32}`. Las bandas van por su identificador de origen (`TournamentService.OriginOf`: el `Id` en las propias y el `SourceId` en las cargadas), así el organizador encuentra la mesa (`Locate`) sin preparar nada en la mesa, y no viajan balas ni equipo. El organizador lo repasa antes de apuntarlo; si falla la cámara, lo apunta a mano.

## Usuario y copia de seguridad

- `UserProfile` (Core/Users): nickname (vacío = `PringaoSinNickname`), tamaño de letra (90–150 %, de 10 en 10) y tipografía (`Punk`, `OpenDyslexic`, `Hyperlegible`). `LocalUserProfileStore` lo guarda en localStorage (`punkartuchera.user`) para que el script en línea del `index.html` ponga la letra antes de pintar nada; lo aplica con `font-size` en `html` y el atributo `data-font`, que cambia las variables de tipografía en `css/user.css`. Las letras alternativas van en `wwwroot/fonts/` (licencia OFL junto a ellas) para que funcionen sin conexión.
- `BackupService` (Core/Backup) genera un `BackupFile` en JSON: `format` (`punkartuchera-backup`), `version`, `createdAt`, `nickname`, `settings`, todas las bandas (las propias con balas y notas), las partidas guardadas y las fotos de miniaturas y de la mesa en base64 (`photos`, por clave). El archivo se llama `punkartuchera-<nickname>-<fecha>.json`.
- Restaurar añade lo de la copia: bandas y partidas con el mismo `Id` se sustituyen (y se borran sus fotos viejas), lo demás se queda. Las fotos solo se aceptan si son JPEG, PNG o WebP de verdad, los retratos y fotos oficiales tienen que existir, y las rivales nunca guardan balas. Después la app se recarga entera.

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

Componentes reutilizables en `Components/`: `Icon`, `MiniPortrait`, `MemberCard`, `PageTabs`, `ActionsMenu`, `Stepper`, `TagEditor`, `ConfirmButton`, `PhotoPicker`, `PlaceholderNotice`. Los de la sección de reglas están en `Components/Rules/`.

## Reglas

El reglamento está en datos, no en código: `Core/Rules/Data/core.json` (atributos, reglas básicas, reglas especiales, armas, blindajes y equipo genérico) y un JSON por facción en `Core/Rules/Data/factions/`. Se incrustan en el ensamblado y `RulebookLoader.LoadEmbedded()` los carga en un `Rulebook` (con generación de código de System.Text.Json, así funciona con el recortado de WebAssembly).

- Cada entrada indica `source` y `page`, que apuntan a los PDFs oficiales de `core.json` → `sources`. La interfaz enlaza a esa página del PDF.
- Los textos son un resumen; las tablas, perfiles y costes se copian tal cual. Los perfiles de tropa son texto ("3 (5)", "-", "X") para respetar la notación del libro.
- Para añadir una facción: crea su JSON en `factions/` y añade su id al orden de `RulebookLoader`. Los tests de `RulebookTests` comprueban que los perfiles estén completos, que las reglas especiales citadas existan y que las armas de las opciones estén en el arsenal.
- `RulesSearch` indexa todo (sin tildes ni mayúsculas, todas las palabras obligatorias, título antes que texto) y devuelve enlaces con ancla. `FragmentScroller` lleva la vista al ancla al abrir la página.

Páginas (`Pages/Rules/`): `/reglas` (buscador e índice), `/reglas/basicas`, `/reglas/especiales`, `/reglas/equipo` y `/reglas/faccion/{id}/{reglas|miembros|equipo}`. Su CSS propio está en `wwwroot/css/rules.css`.

## Mapas

`Core/Maps/MapGenerator` reparte la escenografía elegida sobre la mesa:

- **Simetría central**: cada pieza de la mitad norte tiene su gemela girada 180° en la sur, así las dos bandas tienen el mismo terreno delante. Si un tipo es impar, la pieza que sobra va a la línea central, al centro si cabe.
- Las zonas de despliegue quedan libres y entre piezas hay un hueco mínimo de 10 cm, también con los bordes de la mesa (`DefaultMinGap`, lo que pide el reglamento). Si no cabe todo, lo vuelve a intentar con menos hueco (entre piezas / con los bordes: 10/10, 10/5, 7/5, 5/3, 3/2 y 2/1 cm) y, en último caso, devuelve las piezas que no ha podido colocar (`MapLayout.Leftovers`) en lugar de amontonarlas.
- Cada pieza se coloca en el mejor de varios sitios al azar: el que más se aleja de lo ya colocado y de los bordes.
- **Determinista**: usa su propio generador (`SeededRandom`, mulberry32) y un hash FNV-1a del código (`MapSeed`), porque `System.Random` no garantiza la misma secuencia entre versiones de .NET. Mismo código + misma mesa + mismo terreno = mismo mapa en cualquier dispositivo.

La página `/mapa` guarda todo en el enlace (`?codigo=K7Q2XM&mesa=120x120&terreno=barricada-4.ruina-2`), así que compartir o recargar la página da el mismo mapa sin guardar nada. `TerrainSelection` convierte las cantidades a ese formato y al revés. Los estilos del plano están en `wwwroot/css/punk-map.css`.

## Fotos

`PhotoPicker` abre la cámara (`capture="environment"`) o la galería y, al elegir una foto, muestra un editor de encuadre cuadrado: se arrastra para moverla y se acerca pellizcando, con la rueda o con la barra. Todo ocurre en `wwwroot/js/photoCrop.js` (a través de `PhotoCropper`): la foto se decodifica ya reducida a 2048 px de lado como mucho, con la orientación EXIF aplicada, y el encuadre se guarda como JPEG de 600 × 600 px, así que da igual lo que pese la original. Las fotos HEIC que el navegador no sabe abrir muestran un aviso. Los retratos genéricos están en `wwwroot/img/stock/` (`StockImages`).

## Seguridad

- Blazor escapa todo el texto que pinta: nombres, notas o habilidades del rival se ven tal cual, nunca se ejecutan. No uses `MarkupString` con datos de bandas.
- `PhotoUrlProvider` solo devuelve direcciones de retratos genéricos conocidos y de fotos oficiales del catálogo, también para bandas guardadas antes de que existieran estas comprobaciones.
- `IndexedDbBandRepository` ignora registros dañados en lugar de romper la lista de bandas.
- Al publicar, `build/csp.cs` añade al `index.html` una política de seguridad de contenido (CSP) en una etiqueta `meta`, porque GitHub Pages no deja poner cabeceras. Autoriza por su hash los scripts en línea del propio `index.html` y limita de dónde se cargan scripts, estilos, fuentes e imágenes. Si junto al `index.html` hay un `appsettings.json` con `Relay:BaseUrl`, permite conectar con ese relé y con Azure SignalR Service. Si añades otro servicio externo, añade su dirección a `connect-src` en ese archivo. Para probarla en local: `dotnet publish` y después `dotnet run build/csp.cs -- <carpeta>/wwwroot/index.html`.
- El flujo de publicación da a cada trabajo solo los permisos que usa, fija las acciones por su commit y falla si alguna dependencia tiene vulnerabilidades conocidas.
- Las copias de seguridad que se restauran pasan por `RivalBandSanitizer.LimitContent` (los mismos topes que una banda rival, pero conservando balas y notas de las bandas propias) y sus partidas se recortan igual: marcador, contadores, nombres y enlace del mapa.
- `tests/Punkartuchera.Core.Tests/ImportAttackTests.cs` y `BackupAttackTests.cs` reúnen los ataques simulados contra la importación de bandas y contra copias de seguridad y QR de resultado del torneo (archivos rotos, inflados o manipulados). Si cambias el formato de intercambio, añade ahí el ataque correspondiente.
