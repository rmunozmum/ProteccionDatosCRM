# Guía de Despliegue por Ambientes — Sistema de Protección de Datos (ARCO)

## 1. Arquitectura de Despliegue

El sistema opera bajo una arquitectura distribuida desacoplada compuesta por dos componentes de cómputo en Azure y servicios de soporte:

1. **Web API / Backend Orquestador (`Umayor.Dynamics.DeletePoc`)**:
   - **Plataforma:** Azure App Service (.NET 8).
   - **Responsabilidad:** Endpoints REST (individual, masivo, reportes, diagnósticos) y servicio de archivos estáticos (`wwwroot/`).
2. **Workers de Procesamiento Asíncrono (`Umayor.Dynamics.DeletePoc.Functions`)**:
   - **Plataforma:** Azure Function App (.NET 8 Isolated Worker, Consumption o App Service Plan).
   - **Responsabilidad:** Desencadenado por Azure Storage Queue (`privacy-mass-executions`), procesamiento elástico de particiones de datos, ejecución de la matriz de borrado, saneamiento de residuos y respaldos criptográficos.
3. **Servicios de Almacenamiento y Datos**:
   - **Azure Storage Account:** Contenedor Blob (`privacy-backups`) y Cola (`privacy-mass-executions`).
   - **Microsoft Dynamics 365 (Dataverse):** Tablas operativas (`um_massexecution`, `um_massexecutiondetail`, `um_privacyoperationlog`) y entidades del dominio universitario a consultar/purgar.

---

## 2. Inventario de Recursos por Ambiente

| Componente | DEV | QA (Actual) | PROD |
| :--- | :--- | :--- | :--- |
| **Resource Group** | `admincrm2021_rg_0225` | `admincrm2021_rg_0225` | `admincrm2021_rg_0225` (o RG Prod) |
| **App Service (API)** | `um-ley-proteccion-datos-dev` | `um-ley-proteccion-datos-qa` | `um-ley-proteccion-datos-prod` |
| **Function App (Workers)** | `um-ley-proteccion-datos-dev-fun` | `um-ley-proteccion-datos-qa-fun` | `um-ley-proteccion-datos-prod-fun` |
| **Dataverse URL** | DEV CRM URL | `https://qas-umayor.crm2.dynamics.com` | PROD CRM URL |
| **Storage Account** | Storage DEV | Storage QA (`admincrm...`) | Storage PROD |
| **Blob Container** | `privacy-backups` | `privacy-backups` | `privacy-backups` |
| **Storage Queue** | `privacy-mass-executions` | `privacy-mass-executions` | `privacy-mass-executions` |

---

## 3. Matriz de Configuración (Application Settings)

### 3.1. Web API (Azure App Service)

| Configuración | DEV | QA | PROD | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `Dataverse__Url` | URL Dataverse DEV | `https://qas-umayor.crm2.dynamics.com` | URL Dataverse PROD | Instancia CRM objetivo |
| `Dataverse__TenantId` | Tenant ID Azure | Tenant ID Azure | Tenant ID Azure | Directorio Microsoft Entra |
| `Dataverse__ClientId` | App Reg Client ID | App Reg Client ID | App Reg Client ID | Identidad de servicio |
| `Dataverse__ClientSecret` | Secreto DEV | Secreto QA | Secreto PROD | Credencial protegida (Key Vault o AppSetting) |
| `Safety__RequireEnvironmentContains` | `dev` | `qa` | `crm` (o identificador seguro prod) | Evita ejecuciones cruzadas entre ambientes |
| `Safety__RequireConfirmationText` | `ELIMINAR` | `ELIMINAR` | `ELIMINAR` | Palabra de confirmación obligatoria |
| `Safety__DeletionEnabled` | `false` (o `true` controlado) | `false` (activar `true` solo para purgas reales) | `false` (bloqueo preventivo) | Conmutador global de borrado |
| `AzureStorage__ConnectionString` | Connection String DEV | Connection String QA | Connection String PROD | Acceso a Colas y Blobs |
| `AzureStorage__QueueName` | `privacy-mass-executions` | `privacy-mass-executions` | `privacy-mass-executions` | Nombre de la cola de particiones |
| `AzureStorage__ContainerName` | `privacy-backups` | `privacy-backups` | `privacy-backups` | Contenedor de respaldos SHA-256 |
| `MassOrchestration__MaxUploadRows` | `50000` | `50000` | `50000` | Límite máximo de filas por nómina |
| `MassOrchestration__PartitionSize` | `25` o `500` | `25` o `500` | `500` | Tamaño del fragmento por mensaje en cola |

### 3.2. Worker (Azure Function App)

| Configuración | DEV | QA | PROD | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `AzureWebJobsStorage` | Connection String DEV | Connection String QA | Connection String PROD | Almacenamiento interno del host y QueueTrigger |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | `dotnet-isolated` | `dotnet-isolated` | Runtime aislado .NET 8 |
| `Dataverse__Url` | URL DEV | `https://qas-umayor.crm2.dynamics.com` | URL PROD | Conexión Dataverse para purga y auditoría |
| `Dataverse__TenantId` | Tenant ID | Tenant ID | Tenant ID | Autenticación Entra ID |
| `Dataverse__ClientId` | Client ID | Client ID | Client ID | Identidad de servicio |
| `Dataverse__ClientSecret` | Secreto DEV | Secreto QA | Secreto QA/PROD | Secreto de aplicación |
| `Safety__RequireEnvironmentContains`| `dev` | `qa` | Identificador Prod | Validación de entorno seguro |
| `Safety__DeletionEnabled` | `false` / `true` | `false` / `true` | `false` (inicial) | Habilitación de eliminación en worker |
| `AzureStorage__ConnectionString` | Connection String DEV | Connection String QA | Connection String PROD | Conexión para BlobStorageBackupService |
| `AzureStorage__ContainerName` | `privacy-backups` | `privacy-backups` | `privacy-backups` | Contenedor de respaldo para snapshots |

---

## 4. Puertas de Promoción entre Ambientes

### 4.1. Promoción a QA (Calidad / Staging)
1. Despliegue coordinado de API y Function App con `Safety__DeletionEnabled = false`.
2. Verificación de endpoint de compilación: `GET /api/diagnostics/build`.
3. Smoke Test de consulta individual: `POST /api/execute-single` con RUT/Pasaporte de prueba.
4. Prueba de carga y simulación masiva: `POST /api/mass/upload` y `POST /api/mass/start/{id}` en modo `Consultar`.
5. Verificación de escalamiento de la Function App mediante App Insights o Azure CLI Streaming.
6. Habilitación temporal de `Safety__DeletionEnabled = true` para ejecución con nómina controlada y autorizada.
7. Verificación del snapshot en Blob Storage, validación de residuos (`customeraddress` saneado) y auditoría en `um_privacyoperationlog`.

### 4.2. Promoción a PROD (Producción)
1. Mismo artefacto binario aprobado en QA (mismo hash de commit).
2. Credenciales y secretos propios de producción, almacenados de forma segura.
3. Validación de permisos de Application User en Dataverse de Producción.
4. `Safety__DeletionEnabled = false` obligatorio en el primer despliegue.
5. Aprobación formal institucional por el Delegado de Protección de Datos (DPO) / DTI.
6. Verificación de salud y simulación masiva previa.
7. Habilitación de eliminación bajo ventana de mantenimiento planificada.

### 4.3. Incorporación de un Nuevo Ambiente (4to Entorno / Custom)

Para provisionar y desplegar el sistema en un entorno adicional (ej: `STAGING`, `SANDBOX`, `DR` o `PROD-2`):

1. **Checklist de Parámetros Requeridos:**
   - Resource Group y nombres de App Service, Function App y Storage Account.
   - Instancia de Dataverse y credenciales de Application User en Microsoft Entra ID.
   - Substring de seguridad (`Safety__RequireEnvironmentContains`) y `Safety__DeletionEnabled = false`.
2. **Pre-requisitos en Dataverse:**
   - Importar la solución empaquetada `AppSolution` en la nueva organización CRM.
   - Asignar rol de seguridad al Application User de Entra ID.
3. **Orquestación y Despliegue Automatizado:**
   ```powershell
   .\deploy_azure.ps1 -Environment "custom" `
                      -ResourceGroup "<NOMBRE_RG>" `
                      -ApiAppName "<NOMBRE_APP_SERVICE>" `
                      -FunAppName "<NOMBRE_FUNCTION_APP>" `
                      -StorageAccount "<NOMBRE_STORAGE>" `
                      -ProvisionStorage
   ```

---

## 5. Procedimiento de Reversión (Rollback)

- **Reversión de Código:** Desplegar el paquete ZIP previo inmediatamente o reactivar el Slot anterior de App Service si está configurado.
- **Integridad de Datos:** Revertir el despliegue restaura el software, pero no los datos eliminados en Dataverse. Para recuperar datos purgados, se cuenta con los respaldos inmutables en Azure Blob Storage (`mass-executions/{headerId}/{detailId}_backup.json`), respaldados con firma hash SHA-256.

