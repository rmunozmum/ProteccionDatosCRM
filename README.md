# UM — Ley de Protección de Datos

API .NET 8 para consultar y ejecutar operaciones de eliminación de datos personales en Dataverse por RUT o pasaporte, con auditoría y reportería.

Esta carpeta corresponde a la base de código recuperada y saneada del proyecto desplegado en el App Service `um-ley-proteccion-datos-qa`. Se excluyeron binarios, publicaciones, respaldos, logs, archivos temporales y credenciales.

## Estructura

- `Program.cs`: endpoints y configuración de la API.
- `Models/`: contratos y configuración.
- `Services/`: consulta, eliminación, auditoría y reportería.
- `Services/MassExecutionStore.cs`: estado durable de lotes e ítems en SQLite.
- `Services/MassExecutionWorker.cs`: procesador asíncrono de un titular por vez.
- `SQL/`: consulta SQL auxiliar.
- `wwwroot/`: interfaz web estática.
- `AppSource/`: fuente desempaquetada de la Canvas App.
- `AppSolution/`: solución de Power Platform desempaquetada.
- `swagger*.json`: referencias de los contratos publicados.

## Requisitos

- .NET SDK 8.
- Acceso autorizado al entorno de Dataverse.
- Una aplicación registrada en Microsoft Entra ID con los permisos requeridos.

## Configuración local

El archivo `appsettings.json` no contiene secretos y mantiene la eliminación deshabilitada. Para desarrollo local, crea `appsettings.Development.json` con los valores del ambiente correspondiente. Ese archivo está excluido de Git.

También puedes usar variables de entorno:

```powershell
$env:Dataverse__Url = "https://organizacion-qa.crm.dynamics.com"
$env:Dataverse__ClientId = "<client-id>"
$env:Dataverse__ClientSecret = "<secret>"
$env:Safety__DeletionEnabled = "false"
dotnet run
```

Mantén `Safety__DeletionEnabled=false` mientras se ejecuten consultas o pruebas que no deban eliminar datos.

## Procesamiento masivo

La API incorpora una primera orquestación durable para una sola instancia de App Service:

- creación de lotes por RUT o pasaporte;
- simulación masiva usando el modo `Consultar`;
- estado y evidencia por titular;
- pausa, reanudación y cancelación;
- reintentos independientes;
- recuperación de ítems que hayan quedado procesando tras un reinicio;
- idempotencia por identificador dentro de cada lote.

Endpoints:

```text
POST /api/mass-executions
GET  /api/mass-executions/{executionId}
GET  /api/mass-executions/{executionId}/items?skip=0&take=100
POST /api/mass-executions/{executionId}/pause
POST /api/mass-executions/{executionId}/resume
POST /api/mass-executions/{executionId}/cancel
POST /api/mass-executions/{executionId}/retry-failed
```

Ejemplo seguro de simulación:

```json
{
  "ruts": ["11111111-1", "22222222-2"],
  "pasaportes": [],
  "mode": "Consultar",
  "confirmationText": "",
  "instructionReference": "PRUEBA-QAS-001"
}
```

Para QAS en Windows App Service, configura la persistencia fuera del paquete desplegado:

```text
MassProcessing__DatabasePath = C:\home\data\mass-processing\mass-processing.db
MassProcessing__Enabled = true
Safety__RequireEnvironmentContains = qa
Safety__DeletionEnabled = false
```

Cada ambiente debe usar su propia base de estado, sus propias credenciales y su URL de Dataverse. No se debe promover la base SQLite entre DEV, QAS y PROD.

### Estrategia de promoción

1. Publicar el mismo artefacto versionado en QAS con eliminación deshabilitada.
2. Ejecutar simulaciones, reinicios, pausa, reanudación y reintentos.
3. Habilitar eliminación solo para una nómina controlada y verificar auditoría.
4. Homologar el artefacto y las variables en DEV.
5. Promover exactamente el artefacto aprobado a PROD, con aprobación separada y variables propias.

La implementación actual exige una sola instancia del App Service. Antes de escalar horizontalmente se debe reemplazar el almacenamiento/claim local por una cola distribuida, por ejemplo Azure Service Bus, y por un repositorio de estado compartido.

## Compilar

```powershell
dotnet restore
dotnet build
dotnet run
```

## Crear el repositorio

Desde esta carpeta:

```powershell
git init
git add .
git commit -m "Base recuperada y saneada del motor de privacidad"
git branch -M main
git remote add origin https://github.com/ORGANIZACION/NOMBRE-REPOSITORIO.git
git push -u origin main
```

Antes de publicar, confirma con `git status` que no existan archivos locales con secretos, logs, respaldos ni paquetes de despliegue.

## Seguridad pendiente

El secreto que estaba incluido en la copia histórica debe considerarse expuesto. La limpieza de esta carpeta evita volver a versionarlo, pero no reemplaza su rotación en Microsoft Entra ID y la actualización posterior de la configuración protegida del App Service.

## Próxima evolución

La siguiente evolución es desacoplar el procesador mediante Azure Service Bus y mover el estado a almacenamiento compartido cuando el volumen o el escalamiento requieran más de una instancia.
