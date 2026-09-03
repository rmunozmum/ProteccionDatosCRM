# UM — Ley de Protección de Datos Personales (Derechos ARCO)

Plataforma empresarial en .NET 8 para consultar, anonimizar y ejecutar operaciones de eliminación profunda de datos personales en **Microsoft Dynamics 365 (Dataverse)** por RUT o Pasaporte, con auditoría reglamentaria inmutable, reportería y respaldos criptográficos en **Azure Blob Storage**.

---

## 📚 Índice Rápido de Documentación

| Documento | Descripción | Enlace Directo |
| :--- | :--- | :---: |
| 🔑 **Variables de Entorno Azure** | **Referencia exhaustiva** de Application Settings verificadas en DEV, QA y PROD | [**README_ENV_VARIABLES.md**](./README_ENV_VARIABLES.md) |
| 🚀 **Guía de Despliegue por Ambientes** | Procedimiento operativo por ambientes, puertas de paso y 4to entorno | [**DEPLOYMENT.md**](./DEPLOYMENT.md) |
| 📋 **Levantamiento QAS Power Platform** | Inventario en modo lectura de soluciones, tablas, páginas, conector, dependencias y brechas contra Git | [**Levantamiento QAS**](./docs/levantamiento_qas_proteccion_datos_2026-09-03.md) |
| ⚙️ **Skill Despliegue Azure** | Guía de comandos Azure CLI para App Service, Functions y Storage | [**SKILL Despliegue**](./.agents/skills/despliegue-azure-umayor/SKILL.md) |
| 🛡️ **Skill Protección de Datos** | Arquitectura integral, modelo de entidades Dataverse y seguridad | [**SKILL Arquitectura**](./.agents/skills/proteccion-datos-umayor/SKILL.md) |

---

## 1. Arquitectura de la Solución


La solución consta de tres componentes en .NET 8 y frontend/Power Apps integrados:

1. **`Umayor.Dynamics.DeletePoc` (App Service / Web API)**:
   - Endpoints REST para ejecución individual (`/api/execute-single`), lotes directos (`/api/execute-batch`), carga masiva por archivo (`/api/mass/upload`), control de lotes (`/api/mass/start/{id}`, `/api/mass/status/{id}`), reportería (`/api/reports/...`) y diagnóstico (`/api/diagnostics/build`).
   - Servidor web de la interfaz de usuario en `wwwroot/`.
   - Hosted worker de auto-recuperación (`OutboxRecoveryWorker`).

2. **`Umayor.Dynamics.DeletePoc.Functions` (Azure Function App - .NET 8 Isolated)**:
   - Worker elástico desencadenado por la cola `privacy-mass-executions` (`QueueProcessorFunction.cs`).
   - Procesa particiones en paralelo con control de leases y reconciliación de estados ambiguos.
   - Genera respaldos SHA-256 en Azure Blob Storage antes de cualquier borrado.
   - Sanea y anonimiza entidades restringidas por Dataverse (ej. `customeraddress`).

3. **`Umayor.Dynamics.DeletePoc.Shared` (Biblioteca de Clases Compartida)**:
   - `MatrixDeletionService.cs`: Motor de dependencias relacionales sobre más de 20 entidades.
   - `BlobStorageBackupService.cs`: Snapshots JSON comprimidos con firma SHA-256.
   - `PrivacyOperationLogService.cs`: Auditoría inmutable en `um_privacyoperationlog`.
   - `DataverseConnectionFactory.cs`: Gestión optimizada de conexiones ServiceClient.

4. **Frontend & Power Platform**:
   - `wwwroot/`: Portal web estático integrado con tema oscuro y glassmorphism.
   - `AppSource/` y `AppSolution/`: Solución y Canvas App de Power Apps.
   - `swagger_custom_connector_mass.yaml`: Conector personalizado Swagger 2.0.

---

## 2. Mapa Integral de Documentación (`*.md`)

El repositorio cuenta con una suite de documentación técnica organizada según el rol y la tarea a realizar:

### 📄 [README.md](./README.md) — *Manual Central del Repositorio*
- **Audiencia:** Todo el equipo (Desarrolladores, QA, DevOps, Líderes Técnicos).
- **Contenido:**
  - Visión general del proyecto, propósito y marco legal (Ley ARCO).
  - Arquitectura de componentes (.NET 8 Web API, Azure Functions y Shared Library).
  - Guía de inicio y ejecución local segura.
  - **Guía para Desarrolladores:** Flujo de trabajo con Git, ramas, reglas de seguridad y procedimiento para hacer `git push`.
  - Autoría corporativa y créditos institucionales.

---

### 🔑 [README_ENV_VARIABLES.md](./README_ENV_VARIABLES.md) — *Referencia de Variables de Entorno Azure*
- **Audiencia:** DevOps, Cloud Engineers, Administradores de Infraestructura Azure.
- **Contenido:**
  - Inventario de los 6 servicios en Azure (`admincrm2021_rg_0225`) en DEV, QA y PROD.
  - Matriz de valores reales verificados para cada ambiente.
  - Explicación técnica de variables de conexión Dataverse (`Dataverse__Url`, `ClientId`, `AuthType`).
  - **Mecanismos de Fail-Safe y Kill-Switch:** `Safety__RequireEnvironmentContains` (anti-ejecución cruzada) y `Safety__DeletionEnabled`.
  - Configuración de Azure Storage Queues (`privacy-mass-executions`) y Blob Containers (`privacy-backups`).
  - Parámetros de dimensionamiento de lotes masivos (`MassOrchestration__*`).

---

### 🚀 [DEPLOYMENT.md](./DEPLOYMENT.md) — *Guía de Despliegue y Puertas de Promoción*
- **Audiencia:** Responsables de Release, DevOps, QA Lead.
- **Contenido:**
  - Arquitectura de despliegue y separación de cómputo (Web API vs. Workers Asíncronos).
  - Puertas de promoción y criterios de aceptación entre DEV, QA y PROD.
  - **Incorporación de un 4to Entorno:** Checklist de requisitos y procedimiento para dar de alta un nuevo ambiente (`custom`, `staging`, etc.).
  - Protocolo de reversión (*Rollback*) de código e integridad de respaldos de datos.

---

### 📋 [docs/levantamiento_qas_proteccion_datos_2026-09-03.md](./docs/levantamiento_qas_proteccion_datos_2026-09-03.md) — *Levantamiento QAS Power Platform*
- **Audiencia:** Arquitectos Dynamics 365, responsables ALM, QA Lead y documentación técnica.
- **Contenido:**
  - Inventario en modo lectura de las soluciones `LeydeProtecciondeDatos`, `Pagina_Leyproteccion` y `CustomConnector_LeyProteccion`.
  - Componentes visibles en QAS: tablas, páginas, Model Driven App, sitemap, Canvas App, conector personalizado y dependencias.
  - Diccionario preliminar de columnas y relaciones de `um_privacyoperationlog`, `um_massexecution` y `um_massexecutiondetail`.
  - Contraste contra el repositorio y brechas documentales para cerrar antes de consolidar ALM y documentación técnica.

---

### ⚙️ [.agents/skills/despliegue-azure-umayor/SKILL.md](./.agents/skills/despliegue-azure-umayor/SKILL.md) — *Skill Operativa de Despliegue Azure*
- **Audiencia:** Desarrolladores, DevOps y Asistentes de IA (Copilot / Antigravity).
- **Contenido:**
  - Secuencia de comandos en PowerShell y Azure CLI para compilar y desplegar vía `ZipDeploy`.
  - Comandos para aprovisionamiento inicial de Storage (Colas y Blobs) con Azure CLI.
  - Instrucciones para actualizar el Conector Personalizado (*Custom Connector*) en Power Apps con `swagger_custom_connector_mass.yaml`.
  - Batería de Smoke Tests post-despliegue (`/api/diagnostics/build`, `/api/execute-single`, monitoreo de colas).
  - Diagnóstico y resolución de fallas comunes (errores MFA, HTTP 502/503, bloqueos de Dataverse).

---

### 🛡️ [.agents/skills/proteccion-datos-umayor/SKILL.md](./.agents/skills/proteccion-datos-umayor/SKILL.md) — *Skill de Arquitectura y Dominio Dataverse*
- **Audiencia:** Desarrolladores de Backend, Arquitectos de Software, Consultores Dynamics 365.
- **Contenido:**
  - Diagrama de flujo de extremo a extremo desde Power Apps hasta Azure Functions.
  - Definición de los tres modos de operación (`EliminarTodo`, `EliminarTodoMenosContacto`, `Consultar`).
  - Matriz relacional de más de 20 entidades de Dataverse (`contact`, `lead`, `incident`, actividades, etc.).
  - Mecanismo de desvinculación automática (*unlinking fallback*) y saneamiento de `customeraddress`.
  - Pipeline criptográfico de snapshots JSON comprimidos firmados con **SHA-256** en Blob Storage.
  - Esquema detallado de las tablas de Dataverse: `um_massexecution`, `um_massexecutiondetail` y `um_privacyoperationlog`.
  - Optimizaciones de rendimiento (reducción de tiempos de purga a ~5.5 segundos por registro).



---

## 3. Despliegue Rápido a Azure (PowerShell)

Para compilar, empaquetar y desplegar automáticamente tanto el App Service como la Function App:

```powershell
# Despliegue a QA con Smoke Tests automáticos
.\deploy_azure.ps1 -Environment qa

# Despliegue a DEV
.\deploy_azure.ps1 -Environment dev

# Despliegue a PROD (requiere confirmación y autorización institucional)
.\deploy_azure.ps1 -Environment prod
```

---

## 4. Requisitos y Ejecución Local

### Requisitos:
- **.NET SDK 8.0+**
- **Azure CLI** (`az`) con sesión activa (`az login`).
- Acceso autorizado a la organización Dataverse (`ServiceClient` / Entra ID).
- Azurite (o Azure Storage Account) para colas y blobs locales.

### Compilación local:
```powershell
dotnet restore
dotnet build Umayor.Dynamics.DeletePoc.sln
```

### Configuración local segura:
El archivo `appsettings.json` mantiene `Safety__DeletionEnabled = false` por seguridad.
Para desarrollo local con eliminación deshabilitada:
```powershell
$env:Dataverse__Url = "https://qas-umayor.crm2.dynamics.com"
$env:Dataverse__ClientId = "<client-id>"
$env:Dataverse__ClientSecret = "<secret>"
$env:Safety__DeletionEnabled = "false"
$env:AzureStorage__ConnectionString = "UseDevelopmentStorage=true"
dotnet run --project .\Umayor.Dynamics.DeletePoc.csproj
```

---

## 5. Guía para Desarrolladores y Flujo de Contribución (Git & Push)

Si vas a clonar el repositorio, realizar cambios y subirlos, sigue este procedimiento estandarizado:

### Paso 1: Clonar el repositorio
```bash
git clone https://github.com/rmunozmum/ProteccionDatosCRM.git
cd ProteccionDatosCRM
```

### Paso 2: Crear una rama de trabajo
Evita trabajar directamente sobre `main`. Crea una rama descriptiva para tu tarea:
```bash
# Para una nueva funcionalidad
git checkout -b feature/nombre-funcionalidad

# Para una corrección
git checkout -b fix/nombre-del-bug
```

### Paso 3: Reglas de Oro de Seguridad Local
> [!CAUTION]
> - **CERO SECRETOS EN GIT:** Nunca comitees secretos, contraseñas, connection strings ni client secrets en `appsettings.json`.
> - **Eliminación deshabilitada por defecto:** Mantén `Safety__DeletionEnabled = false` en tu configuración local. Para pruebas, utiliza el modo `Consultar`.
> - Utiliza variables de entorno locales (`$env:...`) o archivos `appsettings.Development.json` (que ya están en `.gitignore`).

### Paso 4: Validar y Compilar Localmente
Antes de hacer commit, asegúrate de que toda la solución compila sin errores y ejecuta las pruebas automatizadas:
```powershell
# 1. Compilar toda la solución
dotnet build Umayor.Dynamics.DeletePoc.sln

# 2. Ejecutar pruebas automatizadas locales
.\tests\test_execute_batch_result.ps1
```

### Paso 5: Preparar Commit y Verificar Limpieza
Revisa con `git status` que únicamente se agreguen archivos de código fuente, documentación o pruebas (sin binarios, archivos temporales ni zips):
```bash
git status
git add .
git commit -m "tipo(alcance): descripción clara del cambio"
```
*Tipos recomendados:* `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`.

### Paso 6: Subir los Cambios a GitHub (Push)
GitHub exige autenticación moderna (no admite contraseñas planas de cuenta):
```bash
git push -u origin feature/nombre-funcionalidad
```
* **Git Credential Manager:** Si usas Windows/Mac, tu consola abrirá una pestaña del navegador para autorizar la subida con tu cuenta GitHub en 1 clic.
* **Personal Access Token (PAT):** Si tu entorno no tiene interfaz gráfica, genera un token en GitHub (*Settings > Developer settings > Personal access tokens*) con permiso `repo` y úsalo como contraseña.

### Paso 7: Pull Request y Fusión a `main`
1. Abre un **Pull Request (PR)** en GitHub desde tu rama hacia `main`.
2. Una vez revisado y aprobado el PR, realiza el merge a `main`.

### Paso 8: Desplegar a Azure
Una vez que tus cambios estén fusionados en la rama `main`, despliégalos al ambiente correspondiente:
```powershell
# Despliegue a QA
.\deploy_azure.ps1 -Environment qa

# Despliegue a DEV
.\deploy_azure.ps1 -Environment dev
```

---

## 6. Autoría y Créditos

- **Autor / Ingeniero Responsable:** Rogelio Muñoz (`rogelio.munoz@umayor.cl`)
- **Cuenta GitHub:** [@rmunozmum](https://github.com/rmunozmum)
- **Organización:** Universidad Mayor — Dirección de Tecnologías de la Información (DTI)
- **Repositorio Oficial:** [https://github.com/rmunozmum/ProteccionDatosCRM](https://github.com/rmunozmum/ProteccionDatosCRM)
- **Rama Principal:** `main`


