# Referencia Completa de Variables de Entorno y Configuración Azure
## Sistema de Protección de Datos Personales (Ley ARCO) — Universidad Mayor

Este documento detalla el inventario exhaustivo, propósito, formato y comportamiento de cada variable de entorno (**Application Setting**) configurada en **Azure App Service** y **Azure Function App** para los tres ambientes activos (**DEV**, **QA**, **PROD**) bajo el grupo de recursos `admincrm2021_rg_0225`.

---

## 1. Inventario de Servicios en Azure (`admincrm2021_rg_0225`)

| Ambiente | Tipo de Servicio | Nombre del Recurso en Azure | URL / Hostname |
| :--- | :--- | :--- | :--- |
| **DEV** | App Service (API Web) | `um-ley-proteccion-datos-dev` | `um-ley-proteccion-datos-dev.azurewebsites.net` |
| **DEV** | Function App (Worker) | `um-ley-proteccion-datos-dev-fun` | `um-ley-proteccion-datos-dev-fun.azurewebsites.net` |
| **QA** | App Service (API Web) | `um-ley-proteccion-datos-qa` | `um-ley-proteccion-datos-qa.azurewebsites.net` |
| **QA** | Function App (Worker) | `um-ley-proteccion-datos-qa-fun` | `um-ley-proteccion-datos-qa-fun.azurewebsites.net` |
| **PROD** | App Service (API Web) | `um-ley-proteccion-datos-prod` | `um-ley-proteccion-datos-prod.azurewebsites.net` |
| **PROD** | Function App (Worker) | `um-ley-proteccion-datos-prod-fun` | `um-ley-proteccion-datos-prod-fun.azurewebsites.net` |

---

## 2. Matriz de Valores Verificados por Ambiente

| Variable | DEV | QA | PROD | Tipo / Componente |
| :--- | :--- | :--- | :--- | :--- |
| `Dataverse__Url` | `https://desa-umayor.crm2.dynamics.com` | `https://qas-umayor.crm2.dynamics.com` | `https://umayor.crm2.dynamics.com` | Dataverse / CRM |
| `Dataverse__AuthType` | `ClientSecret` | `ClientSecret` | `ClientSecret` | Autenticación Entra ID |
| `Dataverse__TenantId` | `0dc2d1a0-913c-4a0d-b1a7-3e857d4cccdb` | `0dc2d1a0-913c-4a0d-b1a7-3e857d4cccdb` | `0dc2d1a0-913c-4a0d-b1a7-3e857d4cccdb` | Directorio UMayor |
| `Dataverse__ClientId` | `fd7755ac-679f-43fa-96f9-d45cc9aff858` | `cd15fec6-347f-4564-8ab2-4b948c2eb05a` | `b614c96b-7d7b-40aa-901f-14364ff72653` | Application Registration |
| `Dataverse__ClientSecret` | *(Protegido en Azure)* | *(Protegido en Azure)* | *(Protegido en Azure)* | Credencial Secreta |
| `Safety__RequireEnvironmentContains` | `desa` | `qas` | `umayor` | **Fail-Safe de Seguridad** |
| `Safety__RequireConfirmationText` | `ELIMINAR` | `ELIMINAR` | `ELIMINAR` | Frase de Confirmación |
| `Safety__DeletionEnabled` | `true` | `true` | `true` *(o `false` preventivo)* | **Kill-Switch de Purga** |
| `AzureStorage__QueueName` | `privacy-mass-executions` | `privacy-mass-executions` | `privacy-mass-executions` | Cola de Particiones |
| `AzureStorage__ContainerName` | `privacy-backups` | `privacy-backups` | `privacy-backups` | Contenedor SHA-256 |
| `AzureStorage__ConnectionString` | *(Connection String DEV)* | *(Connection String QA)* | *(Connection String PROD)* | Acceso a Storage |
| `AzureWebJobsStorage` | *(Connection String DEV)* | *(Connection String QA)* | *(Connection String PROD)* | Host de Functions |
| `MassOrchestration__MaxBatchSize` | `500` | `500` | `500` | Límite por Lote en Memoria |
| `MassOrchestration__MaxUploadRows` | `50000` | `50000` | `50000` | Límite Máximo Archivo |
| `MassOrchestration__PartitionSize` | `500` | `500` | `500` | Fragmento por Worker |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Development` | `Production` | Entorno de Ejecución .NET |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | `dotnet-isolated` | `dotnet-isolated` | Modelo Aislado Azure Fun |

---

## 3. Detalle Técnico y Propósito de Cada Variable

### 3.1. Grupo Dataverse (Conexión Dynamics 365)

#### `Dataverse__Url`
- **Aplica a:** App Service y Function App.
- **Propósito:** Especifica el endpoint de la organización Dynamics 365 donde se ejecutan las consultas, escaneos y operaciones de purga relacional.
- **Ejemplo:** `https://qas-umayor.crm2.dynamics.com`.
- **Efecto:** La fábrica de conexiones [`DataverseConnectionFactory.cs`](file:///d:/Proyectos/Umayor.Dynamics.DeletePoc.MassOrchestration.v1/Umayor.Dynamics.DeletePoc.Shared/Services/DataverseConnectionFactory.cs) utiliza esta URL para instanciar el cliente `ServiceClient`.

#### `Dataverse__AuthType`
- **Aplica a:** App Service y Function App.
- **Propósito:** Define el mecanismo de autenticación contra Dataverse. El valor configurado es `"ClientSecret"`.
- **Efecto:** Instancia la autenticación Service-to-Service (S2S) de Microsoft Entra ID usando Client ID + Client Secret.

#### `Dataverse__TenantId`
- **Aplica a:** App Service y Function App.
- **Propósito:** Identificador GUID del Tenant de Microsoft Entra ID de la Universidad Mayor (`0dc2d1a0-913c-4a0d-b1a7-3e857d4cccdb`).
- **Efecto:** Define el emisor de los tokens JWT de autorización emitidos para Dataverse.

#### `Dataverse__ClientId`
- **Aplica a:** App Service y Function App.
- **Propósito:** Identificador de la aplicación registrada (Application ID) en Entra ID. Cada ambiente posee una identidad de servicio segregada para evitar cruce de permisos:
  - **DEV:** `fd7755ac-679f-43fa-96f9-d45cc9aff858`
  - **QA:** `cd15fec6-347f-4564-8ab2-4b948c2eb05a`
  - **PROD:** `b614c96b-7d7b-40aa-901f-14364ff72653`
- **Efecto:** Corresponde al *Application User* dentro de Dynamics 365 al cual se le asigna el rol de seguridad con permisos sobre `contact`, actividades y tablas de auditoría.

#### `Dataverse__ClientSecret`
- **Aplica a:** App Service y Function App.
- **Propósito:** Secreto de cliente protegido generado en Microsoft Entra ID.
- **Seguridad:** Nunca debe exponerse en código fuente ni en repositorios. Solo reside de forma cifrada en la configuración de Azure (o en Azure Key Vault).

---

### 3.2. Grupo Safety (Mecanismos de Protección y Salvaguarda Crítica)

#### `Safety__RequireEnvironmentContains` *(Fail-Safe Anti-Ejecución Cruzada)*
- **Aplica a:** App Service y Function App.
- **Valores:**
  - DEV: `"desa"`
  - QA: `"qas"`
  - PROD: `"umayor"`
- **Propósito y Mecanismo:**  
  Es la salvaguarda de mayor jerarquía implementada en [`SafetyValidator.cs`](file:///d:/Proyectos/Umayor.Dynamics.DeletePoc.MassOrchestration.v1/Umayor.Dynamics.DeletePoc.Shared/Services/SafetyValidator.cs).  
  Durante el inicio del servicio (`Main`), el sistema toma la URL de Dataverse y valida que contenga obligatoriamente esta subcadena en minúsculas.  
  > **Ejemplo de protección:** Si por un error humano de configuración, el App Service de QA tuviese asignada la URL de Producción (`https://umayor.crm2.dynamics.com`), la validación falla porque la URL no contiene `"qas"`. El proceso aborta su ejecución inmediatamente antes de aceptar cualquier petición, imposibilitando un borrado no autorizado en el entorno equivocado.

#### `Safety__RequireConfirmationText`
- **Aplica a:** App Service y Function App.
- **Valor:** `"ELIMINAR"`.
- **Propósito:**  
  Palabra clave obligatoria de doble factor humano para peticiones de purga (`EliminarTodo` o `EliminarTodoMenosContacto`).
- **Efecto:**  
  Si una petición destructiva no incluye `confirmationText = "ELIMINAR"`, el servicio rechaza la solicitud de inmediato con error HTTP 400 y registra el intento fallido en la auditoría sin alterar ningún registro.

#### `Safety__DeletionEnabled` *(Kill-Switch Global de Eliminación)*
- **Aplica a:** App Service y Function App.
- **Valores:** `"true"` o `"false"`.
- **Propósito:**  
  Interruptor maestro de borrado.
  - Si es `"false"`: La aplicación bloquea cualquier intento de eliminación en Dynamics 365, forzando todas las operaciones a comportarse en modo seguro de simulación / solo lectura (`Consultar`).
  - Si es `"true"`: Permite la ejecución de borrados efectivos sobre los registros que cumplan las validaciones y respaldos previos.

---

### 3.3. Grupo AzureStorage (Orquestación Distribuida y Respaldo Criptográfico)

#### `AzureStorage__ConnectionString`
- **Aplica a:** App Service y Function App.
- **Propósito:** Cadena de conexión a la cuenta de Azure Storage para acceder a Blobs y Colas.
- **Efecto:** La Web API la utiliza para subir archivos fuente (`/api/mass/upload`), encolar fragmentos en la cola y almacenar los snapshots. El Worker la utiliza para depositar los archivos de respaldo antes de purgar.

#### `AzureWebJobsStorage`
- **Aplica a:** Function App exclusivamente.
- **Propósito:** Cadena de conexión requerida por el host de Azure Functions para gestionar sus metadatos internos, bloqueos de ejecución de workers y el `[QueueTrigger]`.

#### `AzureStorage__QueueName`
- **Aplica a:** App Service y Function App.
- **Valor:** `"privacy-mass-executions"`.
- **Propósito:**  
  Nombre de la cola de Azure Storage Queue que conecta la Web API con los Workers.
- **Efecto:**  
  Cuando el usuario inicia un lote masivo (`POST /api/mass/start/{id}`), la API parte el lote en mensajes JSON de 500 registros y los inserta en esta cola. Las instancias de la Function App se despiertan concurrentemente para procesar cada mensaje.

#### `AzureStorage__ContainerName`
- **Aplica a:** App Service y Function App.
- **Valor:** `"privacy-backups"`.
- **Propósito:**  
  Contenedor privado de Azure Blob Storage donde se resguardan los respaldos inmutables.
- **Estructura de Carpetas en el Blob:**
  - `mass-executions/{loteId}/{detalleId}_backup.json`: Snapshot de cada contacto eliminado.
  - `mass-executions/{loteId}/source/{timestamp}_{archivo}.csv`: Archivo original cargado por el usuario.
- **Garantía Criptográfica:**  
  Cada archivo subido genera un hash **SHA-256** y registra su tamaño exacto en bytes, los cuales se almacenan en el registro de detalle `um_massexecutiondetail` y en la tabla de auditoría `um_privacyoperationlog`.

---

### 3.4. Grupo MassOrchestration (Dimensionamiento y Escala)

#### `MassOrchestration__MaxBatchSize`
- **Valor:** `"500"`.
- **Propósito:** Límite máximo de identificadores aceptados en llamadas síncronas en memoria vía `/api/execute-batch`.

#### `MassOrchestration__MaxUploadRows`
- **Valor:** `"50000"`.
- **Propósito:** Límite máximo de registros permitidos en una nómina masiva cargada por archivo CSV o TXT mediante `/api/mass/upload`. Evita el desbordamiento de memoria en la carga.

#### `MassOrchestration__PartitionSize`
- **Valor:** `"500"`.
- **Propósito:**  
  Define la cantidad de registros empaquetados en cada mensaje de cola. Con un lote de 10.000 registros y `PartitionSize = 500`, se crean 20 mensajes independientes, permitiendo que múltiples instancias de Azure Functions escalen elásticamente y procesen en paralelo.

---

### 3.5. Variables de Runtime y Plataforma Azure

#### `ASPNETCORE_ENVIRONMENT`
- **Valores:** `"Development"` (en DEV y QA) | `"Production"` (en PROD).
- **Propósito:** Controla el comportamiento del framework ASP.NET Core (Swagger habilitado, nivel de detalle de errores, etc.).

#### `FUNCTIONS_WORKER_RUNTIME`
- **Valor:** `"dotnet-isolated"`.
- **Propósito:** Especifica a Azure Functions v4 que el código corre bajo el modelo .NET 8 Isolated Worker (proceso independiente fuera del host de WebJobs).

#### `WEBSITE_RUN_FROM_PACKAGE`
- **Valor:** `"1"`.
- **Propósito:** Obliga al App Service a montar directamente el archivo `.zip` desplegado como sistema de archivos de solo lectura. Evita bloqueos de DLLs en caliente y garantiza despliegues atómicos y tiempos de arranque más veloces.

#### `WEBSITE_HTTPLOGGING_RETENTION_DAYS`
- **Valor:** `"3"`.
- **Propósito:** Días que Azure conserva los logs HTTP del servidor web antes de depurarlos automáticamente.
