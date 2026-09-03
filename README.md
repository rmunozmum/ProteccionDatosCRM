# UM — Ley de Protección de Datos Personales (Derechos ARCO)

Plataforma empresarial en .NET 8 para consultar, anonimizar y ejecutar operaciones de eliminación profunda de datos personales en **Microsoft Dynamics 365 (Dataverse)** por RUT o Pasaporte, con auditoría reglamentaria inmutable, reportería y respaldos criptográficos en **Azure Blob Storage**.

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

## 2. Documentación y Skills Especializadas

En la carpeta `.agents/skills/` se encuentran disponibles guías técnicas detalladas:
- **[despliegue-azure-umayor](file:///.agents/skills/despliegue-azure-umayor/SKILL.md)**: Guía operativa paso a paso de compilación, empaquetado, Application Settings en Azure, smoke tests y rollback para App Service y Function App.
- **[proteccion-datos-umayor](file:///.agents/skills/proteccion-datos-umayor/SKILL.md)**: Manual de arquitectura, modelo de entidades Dataverse (`um_massexecution`, `um_massexecutiondetail`), flujos criptográficos de respaldo y optimizaciones.
- **[DEPLOYMENT.md](file:///DEPLOYMENT.md)**: Matriz de variables por ambiente (DEV, QA, PROD) y puertas de promoción.

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


