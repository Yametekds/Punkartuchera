# Partidas en línea

Dos jugadores, cada uno con su móvil, llevan la misma partida: uno la crea, le pasa el código de seis caracteres al otro y, al entrar, cada móvil recibe la banda del rival (sin balas ni fotos). A partir de ahí cada uno marca a los suyos (ha actuado, baja) y cualquiera puede pasar de ronda.

La app sigue siendo estática en GitHub Pages. Lo único que vive en la nube es un **relé**: no sabe nada del juego, solo deja que dos móviles con el mismo código se pasen mensajes.

## Piezas

```
Móvil A ──HTTP──>  Function App (relé)  ──REST──>  Azure SignalR Service  ──WebSocket──>  Móvil B
        <─────────────────────────────────── WebSocket ─────────────────────────────────
                         │
                         └── Table Storage (tabla "partidas")
```

| Proyecto o carpeta | Contenido |
| --- | --- |
| `src/Punkartuchera.Relay.Contracts` | Lo que comparten app y relé: `MatchCode` (generar y normalizar códigos), `RelayTicket`, rutas, límites y tipos de mensaje. |
| `src/Punkartuchera.Relay` | Azure Functions (.NET 10, proceso aislado). `MatchRelay` tiene la lógica; `Functions/` solo traduce HTTP. |
| `src/Punkartuchera.Core/Matches` | `MatchService` (la partida, local o en línea), `MatchMessage` (protocolo entre móviles) e `IMatchRelay`. |
| `src/Punkartuchera.UI` | `SignalRMatchRelay` (cliente HTTP + SignalR), `IndexedDbActiveMatchStore` y la página `/partida`. |
| `infra/main.bicep` | Todos los recursos de Azure. |
| `.github/workflows/relay.yml` | Publica el relé desde GitHub (opcional). |

## Cómo funciona

1. **Crear** (`POST api/partidas`): el relé genera un código con un generador criptográfico, una llave de 256 bits para el anfitrión y guarda la partida con la *huella* SHA-256 de la llave, nunca la llave. La partida espera 30 minutos.
2. **Entrar** (`POST api/partidas/{código}/entrar`): si la partida existe, no ha caducado y no tiene rival, se crea la llave del rival. La escritura usa concurrencia optimista (ETag): si dos entran a la vez, solo uno lo consigue. A partir de aquí la partida dura 12 horas.
3. **Conectar** (`POST api/partidas/{código}/conexion`, con `Authorization: Bearer <llave>`): el relé devuelve la URL de SignalR y un token firmado para ese jugador (caduca en una hora; la app pide otro al reconectar). El móvil abre un WebSocket directamente con SignalR.
4. **Mandar** (`POST api/partidas/{código}/mensajes`): el relé comprueba la llave, que el cuerpo sea JSON de como mucho 8 KB y de un tipo permitido (`hello`, `status`, `round`), y se lo pasa al rival por SignalR.
5. **Salir** (`POST api/partidas/{código}/salir`): borra la partida y avisa al rival con `bye`.

Cada hora, `LimpiarPartidas` borra las caducadas.

### Mensajes entre móviles

- `hello`: la banda en el formato del QR, la ronda y el estado de cada miembro. Se manda al conectar o reconectar, así lo que se perdiera sin cobertura se pone al día solo. Con `reply: true` el otro contesta con el suyo.
- `status`: el estado de los miembros propios, en el orden en que se mandó la banda.
- `round`: cambio de ronda, con un reloj de Lamport (`stamp`). Si los dos cambian la ronda a la vez, gana el reloj más alto y, si empatan, la ronda más alta: los dos móviles acaban igual.

La app valida todo lo que llega (`MatchMessage.TryParse`) y no deja que el rival cambie de banda a mitad de partida.

## Seguridad

- **Llaves**: 32 bytes aleatorios; el relé solo guarda su SHA-256 y compara en tiempo constante. El código solo sirve para entrar una vez: después hace falta la llave.
- **Códigos**: 29 caracteres posibles en 6 posiciones (unos 600 millones). Una partida acepta un solo rival y caduca a los 30 minutos si nadie entra.
- **Límites por minuto** (en memoria de cada instancia): 6 creaciones y 12 intentos de entrar por IP, 90 mensajes por jugador.
- **Mensajes**: tamaño máximo, JSON válido, profundidad máxima 8 y solo los tipos permitidos. `bye` solo lo puede mandar el relé.
- **Sin claves en Azure**: la Function App usa una identidad administrada con los roles justos; SignalR y la cuenta de almacenamiento tienen desactivado el acceso por clave.
- **CORS** limitado a la web publicada, HTTPS obligatorio y TLS 1.2 como mínimo.
- **En el móvil**: la llave se guarda en IndexedDB con la partida. Quien tenga el móvil desbloqueado podría usarla hasta que la partida termine.

## Coste

Con un grupo de amigos debería ser 0 €:

- SignalR Free: 20 conexiones a la vez (10 partidas) y 20.000 mensajes al día. Solo se permite una instancia Free por suscripción.
- Flex Consumption: incluye una cantidad gratis de ejecuciones y memoria al mes que una partida ni roza.
- Almacenamiento y Application Insights: céntimos. El espacio de Log Analytics está limitado a 1 GB al día.

`infra/main.bicep` crea además un aviso de gasto si le das un correo (`alertEmail`).

## Desplegar

Necesitas una suscripción de Azure y la [CLI de Azure](https://learn.microsoft.com/cli/azure/install-azure-cli). En PowerShell, desde la carpeta del repositorio:

```powershell
az login

# 1. Grupo de recursos en una región con Flex Consumption
az functionapp list-flexconsumption-locations --output table
az group create --name punkartuchera --location westeurope

# 2. Recursos (tarda unos minutos)
az deployment group create `
  --resource-group punkartuchera `
  --template-file infra/main.bicep `
  --parameters alertEmail=tu@correo.com
```

Al acabar muestra dos salidas: `functionAppName` y `relayUrl`. Apúntalas.

```powershell
# 3. Publicar el código del relé
dotnet publish src/Punkartuchera.Relay --configuration Release --output relay
Compress-Archive -Path relay/* -DestinationPath relay.zip -Force
az functionapp deployment source config-zip `
  --resource-group punkartuchera `
  --name <functionAppName> `
  --src relay.zip
```

4. En GitHub: *Settings → Secrets and variables → Actions → Variables* → **New repository variable** `RELAY_URL` con el valor de `relayUrl`.
5. Lanza otra vez *Publicar en GitHub Pages* (pestaña *Actions*). La sección Partida ya ofrece crear y entrar con código.

Comprobación rápida (debe responder 201 con un código):

```powershell
Invoke-RestMethod -Method Post -Uri "<relayUrl>api/partidas" -Headers @{ Origin = "https://yametekds.github.io" }
```

### Publicar el relé desde GitHub (opcional)

`.github/workflows/relay.yml` publica el relé en cada cambio en `main`. Usa OpenID Connect, así que no se guarda ningún secreto:

```powershell
$app = az ad app create --display-name punkartuchera-despliegue --query appId -o tsv
az ad sp create --id $app
az ad app federated-credential create --id $app --parameters '{\"name\":\"relay\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"repo:Yametekds/punkartuchera:environment:relay\",\"audiences\":[\"api://AzureADTokenExchange\"]}'
$scope = az functionapp show -g punkartuchera -n <functionAppName> --query id -o tsv
az role assignment create --assignee $app --role "Website Contributor" --scope $scope
```

Después, en GitHub crea el entorno `relay` (*Settings → Environments*) y las variables `AZURE_CLIENT_ID` (el `$app`), `AZURE_TENANT_ID` (`az account show --query tenantId -o tsv`), `AZURE_SUBSCRIPTION_ID` (`az account show --query id -o tsv`) y `AZURE_FUNCTIONAPP_NAME`.

## Probar en local

1. Instala [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) y [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npm install -g azurite`, y arráncalo con `azurite`).
2. Copia `src/Punkartuchera.Relay/local.settings.example.json` a `local.settings.json` (no se sube al repositorio) y pon el nombre de tu SignalR.
3. Date a ti el rol **SignalR App Server** sobre el SignalR de Azure (en local, `DefaultAzureCredential` usa la sesión de `az login`) y vuelve a desplegar `infra/main.bicep` añadiendo `http://localhost:5080` a `allowedOrigins`.
4. `func start` en `src/Punkartuchera.Relay` y, en `src/Punkartuchera.Web/wwwroot/appsettings.json`, `"BaseUrl": "http://localhost:7071/"`. No subas ese cambio.
