---
name: despliegue-azure-umayor
description: Guía técnica completa y comandos para empaquetar, compilar y desplegar la API backend .NET 8 (Derechos ARCO / LPD - Universidad Mayor) hacia Azure App Service usando ZipDeploy con Azure CLI.
---

# Skill: Despliegue Backend a Azure (App Service) - Derechos ARCO UMayor

## 1. Contexto General del Proyecto
- **Proyecto Backend:** API en .NET 8 (Minimal APIs) para procesamiento de Derechos ARCO y Ley de Protección de Datos (Universidad Mayor).
- **Ruta del Proyecto Local:** `D:\Proyectos\Umayor.Dynamics.DeletePoc.MassOrchestration.v1`
- **Plataforma Objetivo:** Azure App Service (Linux / .NET 8 Runtime).
- **Nombre del App Service en Azure:** `um-ley-proteccion-datos-qa`
- **Grupo de Recursos en Azure:** `admincrm2021_rg_0225`
- **Mecanismo de Despliegue:** Azure CLI via ZipDeploy (`az webapp deployment source config-zip`).

---

## 2. Requisitos Previos e Instrucciones de Autenticación
> [!IMPORTANT]
> **Autenticación Azure CLI:** 
> No ejecutar `az login` directamente en scripts automatizados debido a restricciones de Autenticación de Múltiples Factores (MFA). El despliegue debe utilizar la **sesión activa autorizada** en la consola de PowerShell.

---

## 3. Secuencia Completa de Comandos para Despliegue (PowerShell)

Para publicar cambios en el entorno de QA en Azure, ejecutar la siguiente secuencia desde la carpeta raíz del proyecto (`D:\Proyectos\Umayor.Dynamics.DeletePoc.MassOrchestration.v1`):

```powershell
# Paso 1: Limpiar artefactos y compilaciones previas
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\publish.zip" -Force -ErrorAction SilentlyContinue

# Paso 2: Compilar el proyecto en modo Release hacia la carpeta .\publish
dotnet publish -c Release -o .\publish

# Paso 3: Empaquetar todo el contenido de la carpeta publish en un archivo ZIP
Compress-Archive -Path .\publish\* -DestinationPath .\publish.zip -Force

# Paso 4: Desplegar el paquete ZIP al App Service en Azure utilizando la sesión activa de Azure CLI
az webapp deployment source config-zip --resource-group admincrm2021_rg_0225 --name um-ley-proteccion-datos-qa --src .\publish.zip
```

---

## 4. Estructura del Backend y Comportamiento de los Endpoints

### Endpoint Principal: `/api/execute-single`
- **Verbo HTTP:** `POST`
- **Propósito:** Ejecuta una operación (Consulta o Eliminación) para un único identificador (RUT o Pasaporte).
- **Lógica de lectura de datos:**
  - Lee el cuerpo de la petición (`Body`) en formato JSON.
  - Es **tolerante a transferencia fragmentada (*Chunked Transfer Encoding*)**, sin depender de la cabecera `Content-Length`.
  - Es **case-insensitive** para las propiedades del JSON (`rut`, `pasaporte`, `mode`, `confirmationText`).
  - Posee **fallback automático a Query String** si alguna propiedad no viene en el cuerpo JSON.
  - Realiza **autodetección de identificador**: Si se envía un pasaporte en el campo `rut`, el backend detecta automáticamente que no cumple el formato RUT chileno y lo redirige internamente a la búsqueda por `pasaporte`.
  - Inyecta por defecto `confirmationText = "ELIMINAR"` para modos de purga si no se envía explícitamente, dado que la aplicación Power Apps valida la confirmación visualmente antes del llamado.

### Contrato JSON de Petición (SingleRequest)
```json
{
  "rut": "171752728",
  "pasaporte": "",
  "mode": "Consultar",
  "confirmationText": ""
}
```
*Modos soportados:* `Consultar`, `EliminarTodo`, `EliminarTodoMenosContacto`.

### Contrato JSON de Respuesta (ExecutionResponse)
```json
{
  "executionId": "b182f7c00e5746b19a16f9f2575ce8d9",
  "results": [
    {
      "identifier": "171752728",
      "mode": "Consultar",
      "status": "Consultado",
      "data": {
        "contactId": "b4724a37-...",
        "fullname": "Nombre Contacto",
        "matrix": [ ... ]
      },
      "audit": {
        "created": true,
        "recordId": "956ca925-0d90-f111-8077-000d3ac04541",
        "fullJsonColumnsUsed": true
      }
    }
  ],
  "apiBuild": "mass-orchestration-v1-20260731-namefix"
}
```

---

## 5. Endpoints Adicionales Implementados
- **`POST /api/execute-batch`**: Procesamiento por lotes de arreglos de RUTs o Pasaportes.
- **`GET /api/reports/catalog`**: Catálogo de reportes LPD disponibles.
- **`POST /api/reports/execute`**: Ejecución de reportes específicos LPD.

---

## 6. Archivos Swagger para Integración con Power Apps
- **Swagger 2.0 (YAML):** `swagger_custom_connector.yaml`
- **Swagger 2.0 (JSON):** `swagger.json`

> [!NOTE]
> Power Apps Custom Connectors exige **Swagger 2.0** (no OpenAPI 3.0). El archivo `swagger_custom_connector.yaml` contiene la definición exacta compatible para importar sin errores en el diseñador de conectores personalizados.

---

## 7. Comando de Verificación Post-Despliegue (PowerShell)

Para verificar que el backend en Azure esté vivo y respondiendo correctamente tras un despliegue, ejecutar:

```powershell
$body = @{
    rut = "171752728"
    pasaporte = ""
    mode = "Consultar"
    confirmationText = ""
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://um-ley-proteccion-datos-qa.azurewebsites.net/api/execute-single" `
                  -Method Post `
                  -Body $body `
                  -ContentType "application/json"
```

Si la respuesta devuelve `status = "Consultado"` y `apiBuild`, el despliegue a Azure fue un éxito total.
