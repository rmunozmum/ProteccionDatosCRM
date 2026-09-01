# Despliegue controlado por ambientes

## Artefacto

DEV, QAS y PROD deben recibir el mismo paquete generado desde un commit y una versión identificables. Las diferencias entre ambientes se mantienen exclusivamente en la configuración del App Service.

## Variables obligatorias

| Variable | DEV | QAS | PROD |
|---|---|---|---|
| `Dataverse__Url` | URL DEV | URL QAS | URL PROD |
| `Dataverse__TenantId` | Secreto/configuración DEV | Secreto/configuración QAS | Secreto/configuración PROD |
| `Dataverse__ClientId` | Identidad DEV | Identidad QAS | Identidad PROD |
| `Dataverse__ClientSecret` | Secreto DEV | Secreto QAS | Secreto PROD |
| `Safety__RequireEnvironmentContains` | Fragmento inequívoco DEV | Fragmento inequívoco QAS | Fragmento inequívoco PROD |
| `Safety__DeletionEnabled` | `false` inicialmente | `false` inicialmente | `false` hasta aprobación |
| `MassProcessing__DatabasePath` | Ruta persistente DEV | Ruta persistente QAS | Ruta persistente PROD |
| `MassProcessing__Enabled` | Según prueba | `true` | Tras aprobación |
| `MassProcessing__MaxAttempts` | `3` | `3` | `3` |

Los secretos no deben quedar en el repositorio ni en paquetes de publicación.

## Puertas de promoción

### QAS

- Compilación y arranque correctos.
- Consulta individual sin regresiones.
- Simulación masiva con duplicados y RUT/pasaportes.
- Pausa y reanudación.
- Reinicio del App Service durante un lote.
- Reintento de fallas transitorias.
- Eliminación de una nómina controlada.
- Verificación de auditoría por titular.

### DEV homologado

- Mismo artefacto aprobado en QAS.
- Variables y permisos propios de DEV.
- Base de estado independiente.
- Pruebas de humo y de seguridad.

### PROD

- Versión inmutable identificada.
- Respaldo vigente.
- Secreto rotado y almacenado fuera del código.
- Aprobación institucional de la ejecución.
- Eliminación inicialmente deshabilitada.
- Nómina pequeña de validación antes de lotes mayores.
- Monitoreo y plan de detención.

## Reversión

Revertir el despliegue restaura la aplicación, pero no recupera registros ya eliminados. Por eso la protección principal es preventiva: simulación, lote controlado, pausa, evidencia y habilitación explícita por ambiente.
