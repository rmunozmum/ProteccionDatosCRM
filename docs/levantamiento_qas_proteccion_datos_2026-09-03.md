# Levantamiento QAS - Protección de Datos CRM

Fecha de revisión: 2026-09-03  
Alcance: revisión exclusivamente en modo lectura de Power Apps / Dynamics 365 QAS y contraste con el repositorio `rmunozmum/ProteccionDatosCRM`.

## 1. Alcance y método

Se revisó el entorno accesible desde `https://qas-umayor.crm2.dynamics.com/` usando la sesión ya autenticada en el navegador. No se usaron credenciales pegadas en el chat y no se ejecutaron acciones de modificación, publicación, guardado, eliminación, exportación ni pruebas funcionales contra endpoints.

La revisión se hizo principalmente desde Power Apps Maker sobre el entorno con id `19b450a6-c83d-4aa1-9914-320b87e018ab`. La interfaz muestra el nombre de entorno `DEV Universidad Mayor`, aunque la URL Dataverse revisada corresponde a `qas-umayor.crm2.dynamics.com`; esto debe quedar documentado o corregido para evitar confusión operativa.

También se contrastó contra el repositorio público:

`https://github.com/rmunozmum/ProteccionDatosCRM`

## 2. Soluciones visibles relacionadas

### Ley de Protección de Datos

- Nombre visible: `Ley de Protección de Datos`
- Nombre único: `LeydeProtecciondeDatos`
- Id de solución: `82e0616b-20d3-419c-bbd4-27cf97e42845`
- Tipo: no administrada
- Versión en QAS: `1.0.0.4`
- Publicador: `UM`
- Fecha de creación visible: `25 de ago. de 2026 10:23`
- Última modificación visible: hace 6 días
- Solution checker: no ejecutado
- Control de código fuente: no conectado a Git

### Pagina_Leyproteccion

- Nombre visible y único: `Pagina_Leyproteccion`
- Id de solución: `66545ae2-7805-4e06-b056-954611c4b83c`
- Tipo: no administrada
- Versión: `1.0.0.1`
- Publicador: `W-IT`
- Última modificación visible: hace 2 días
- Solution checker: no ejecutado
- Control de código fuente: no conectado a Git
- Objetos: 3 páginas

### CustomConnector_LeyProteccion

- Nombre visible y único: `CustomConnector_LeyProteccion`
- Id de solución: `f7f7075a-e120-4b57-ae71-a60a7ade8ab5`
- Tipo: no administrada
- Versión: `1.0.0.1`
- Publicador: `W-IT`
- Última modificación visible: hace 6 días
- Solution checker: no ejecutado
- Control de código fuente: no conectado a Git
- Objetos: 1 conector personalizado

### C-06794-G3Q1_leyprotecc

- Nombre visible: `C-06794-G3Q1_leyprotecc`
- Nombre único: `C06794G3Q1_leyprotecc`
- Id de solución: `77a9626d-3ba6-f111-aaac-6045bd3a7119`
- Tipo: no administrada
- Versión: `1.0.0.2`
- Publicador: `W-it`
- Última modificación visible: hace 20 horas
- Solution checker: no ejecutado
- Control de código fuente: no conectado a Git

## 3. Componentes de la solución principal

La solución `LeydeProtecciondeDatos` muestra 17 objetos.

Distribución por tipo:

- Agentes: 0
- Aplicaciones: 2
- Configuración de capacidad de IA: 5
- DVTableSearch: 1
- DVTableSearchEntity: 1
- Espacios de trabajo de datos: 0
- Flujos de nube: 0
- Mapas de sitio: 1
- Opciones: 1
- Páginas: 3
- Tablas: 3
- Tarjetas: 0

Objetos visibles:

| Objeto | Nombre lógico / único | Tipo | Estado / notas |
|---|---|---|---|
| DVTableSearchEntity | `-` | DVTableSearchEntity | Desactivado, owner `ADM GRAL CRM` |
| Consola de Proceso Masivo | `um_massexecution` | Tabla | No administrada, personalizada |
| Detalle de Proceso Masivo | `um_massexecutiondetail` | Tabla | No administrada, personalizada |
| Ejecutar Proceso Individual | `um_ejecutarproceso_9b8d5` | Página | Modificada hace 2 días, owner `ADM GRAL CRM` |
| Ejecutar Proceso Masivo | `um_ejecutarprocesomasivo_2a9a1` | Página | Modificada hace 2 días, owner `ADM GRAL CRM` |
| Estado Proceso | `um_estadoproceso` | Opción | No administrada, personalizada |
| Ley de Protección de datos | `um_leydeproteccindedatos_bdc6e` | Aplicación de lienzo | Modificada hace 6 días, owner `Admin CRM 2021` |
| Ley de Protección de Datos / Derechos ARCO | `um_LeydeProtecciondeDatosDerechosARCO` | Mapa del sitio | Modificado hace 2 horas |
| Ley de Protección de Datos / Derechos ARCO | `um_LeydeProtecciondeDatosDerechosARCO` | Aplicación basada en modelo | Activada, modificada hace 1 hora |
| M365_Primary_model_um_LeydeProtecciondeDatosDerechosARCO | `-` | DVTableSearch | Activado, owner `ADM GRAL CRM` |
| Registro de operación de privacidad | `um_privacyoperationlog` | Tabla | No administrada, personalizada |
| Reportes | `um_reportes_81c37` | Página | Modificada hace 2 días, owner `ADM GRAL CRM` |
| um_privacyoperationlog - Error Message Full Optar por no usar el rellenado de formularios | `-` | Configuración de capacidad de IA | Desactivado |
| um_privacyoperationlog - Post Matrix JSON Full Optar por no usar el rellenado de formularios | `-` | Configuración de capacidad de IA | Desactivado |
| um_privacyoperationlog - Pre Matrix JSON Full Optar por no usar el rellenado de formularios | `-` | Configuración de capacidad de IA | Desactivado |
| um_privacyoperationlog - Request JSON Full Optar por no usar el rellenado de formularios | `-` | Configuración de capacidad de IA | Desactivado |
| um_privacyoperationlog - Response JSON Full Optar por no usar el rellenado de formularios | `-` | Configuración de capacidad de IA | Desactivado |

## 4. Tablas Dataverse

### 4.1 `um_massexecution`

Nombre visible: `Consola de Proceso Masivo`  
Tipo: estándar  
Columna principal: `Nombre Ejecución`

Columnas visibles:

| Nombre visible | Nombre lógico | Tipo | Requerido |
|---|---|---|---|
| Autor | `CreatedBy` | Búsqueda | No |
| Autor (delegado) | `CreatedOnBehalfBy` | Búsqueda | No |
| Consola de Proceso Masivo | `um_massexecutionId` | Identificador único | Sí |
| Código de zona horaria de conversión UTC | `UTCConversionTimeZoneCode` | Número entero | No |
| Equipo propietario | `OwningTeam` | Búsqueda | No |
| Errores | `um_errores` | Línea de texto única | No |
| Estado | `statecode` | Opción | Sí |
| Estado | `um_Estado` | Opción | Sí |
| Exitosos | `um_exitosos` | Número entero | No |
| Fecha de creación | `CreatedOn` | Fecha y hora | No |
| Fecha de creación del registro | `OverriddenCreatedOn` | Solo fecha | No |
| Fecha de modificación | `ModifiedOn` | Fecha y hora | No |
| Fecha Inicio | `um_inicio` | Fecha y hora | No |
| Fecha Término | `um_termino` | Fecha y hora | No |
| Inválidos | `um_invalidos` | Línea de texto única | No |
| Modificado por | `ModifiedBy` | Búsqueda | No |
| Modificado por (delegado) | `ModifiedOnBehalfBy` | Búsqueda | No |
| Motivo | `um_Motivo` | Línea de texto única | Sí |
| No Encontrados | `um_noencontrados` | Número entero | No |
| Nombre Ejecución | `um_name` | Línea de texto única | Sí |
| Número de secuencia de importación | `ImportSequenceNumber` | Número entero | No |
| Número de versión | `VersionNumber` | Número entero grande | No |
| Número de versión de regla de zona horaria | `TimeZoneRuleVersionNumber` | Número entero | No |
| Procesados | `um_procesados` | Número entero | No |
| Propietario | `OwnerId` | Propietario | Sí |
| Razón para el estado | `statuscode` | Opción | No |
| Requiere Conciliación | `um_requiereconciliacion` | Número entero | No |
| Solicitado por (Email) | `um_requestedbyemail` | Línea de texto única | No |
| Total de Registros | `um_totalregistros` | Número entero | No |
| Tratamiento | `um_Tratamiento` | Opción | Sí |

Relaciones visibles:

- `lk_um_massexecution_createdby` -> Usuario, varios a uno
- `lk_um_massexecution_createdonbehalfby` -> Usuario, varios a uno
- `um_massexecutiondetail_massexecutionid_um_massexecution` -> Detalle de Proceso Masivo, uno a varios
- `team_um_massexecution` -> Equipo, varios a uno
- `um_massexecution_UserEntityInstanceDatas` -> Datos de instancia de entidad de usuario, uno a varios
- `um_massexecution_MailboxTrackingFolders` -> Carpeta de seguimiento automático del buzón, uno a varios
- `um_massexecution_PrincipalObjectAttributeAccesses` -> Uso compartido de campos, uno a varios
- `um_privacyoperationlog_massexecutionid_um_massexecution` -> Registro de operación de privacidad, uno a varios
- `lk_um_massexecution_modifiedby` -> Usuario, varios a uno
- `lk_um_massexecution_modifiedonbehalfby` -> Usuario, varios a uno
- `um_massexecution_BulkDeleteFailures` -> Error de eliminación en masa, uno a varios
- `um_massexecution_DeletedItemReferences` -> Referencia de registro eliminado, uno a varios
- `owner_um_massexecution` -> Propietario, varios a uno
- `um_massexecution_AsyncOperations` -> Trabajo del sistema, uno a varios
- `um_massexecution_ProcessSession` -> Sesión de proceso, uno a varios
- `um_massexecution_SyncErrors` -> Error de sincronización, uno a varios
- `business_unit_um_massexecution` -> Unidad de negocio, varios a uno
- `user_um_massexecution` -> Usuario, varios a uno

### 4.2 `um_massexecutiondetail`

Nombre visible: `Detalle de Proceso Masivo`  
Columna principal visible: `name`

Columnas visibles:

| Nombre visible | Nombre lógico | Tipo | Requerido |
|---|---|---|---|
| Autor | `CreatedBy` | Búsqueda | No |
| Autor (delegado) | `CreatedOnBehalfBy` | Búsqueda | No |
| Consola Proceso Masivo | `um_massexecutionid` | Búsqueda | Sí |
| Código de zona horaria de conversión UTC | `UTCConversionTimeZoneCode` | Número entero | No |
| Detalle de Proceso Masivo | `um_massexecutiondetailId` | Identificador único | Sí |
| Equipo propietario | `OwningTeam` | Búsqueda | No |
| Estado | `statecode` | Opción | Sí |
| Estado | `um_Estado` | Opción | Sí |
| Fecha de creación | `CreatedOn` | Fecha y hora | No |
| Fecha de creación del registro | `OverriddenCreatedOn` | Solo fecha | No |
| Fecha de modificación | `ModifiedOn` | Fecha y hora | No |
| Fecha Respaldo | `um_backupdate` | Fecha y hora | No |
| Hash Respaldo (SHA-256) | `um_backuphash` | Línea de texto única | No |
| ID Worker Lease | `um_workerleaseid` | Línea de texto única | No |
| Identificador | `um_identificador` | Línea de texto única | Sí |
| Lease Expirado En | `um_leaseduntil` | Fecha y hora | No |
| Mensaje de Error | `um_errormessage` | Línea de texto única | No |
| Modificado por | `ModifiedBy` | Búsqueda | No |
| Modificado por (delegado) | `ModifiedOnBehalfBy` | Búsqueda | No |
| name | `um_name` | Línea de texto única | Sí |
| Número de secuencia de importación | `ImportSequenceNumber` | Número entero | No |
| Número de versión | `VersionNumber` | Número entero grande | No |
| Número de versión de regla de zona horaria | `TimeZoneRuleVersionNumber` | Número entero | No |
| Propietario | `OwnerId` | Propietario | Sí |
| Razón para el estado | `statuscode` | Opción | No |
| Referencia Respaldo | `um_backupreference` | Línea de texto única | No |
| Resultado | `um_resultado` | Línea de texto única | No |
| Tamaño Respaldo (Bytes) | `um_backupsize` | Número entero | No |
| Tipo de Identificador | `um_tipoidentificador` | Línea de texto única | Sí |
| Unidad de negocio propietaria | `OwningBusinessUnit` | Búsqueda | No |
| Usuario propietario | `OwningUser` | Búsqueda | No |

Relación funcional confirmada desde la cabecera:

- `um_massexecutiondetail_massexecutionid_um_massexecution`: detalle N:1 hacia `um_massexecution`, cabecera 1:N hacia detalles.

### 4.3 `um_privacyoperationlog`

Nombre visible: `Registro de operación de privacidad`  
Tipo: estándar  
Columna principal: `Execution ID`

Columnas visibles:

| Nombre visible | Nombre lógico | Tipo | Requerido |
|---|---|---|---|
| Autor | `CreatedBy` | Búsqueda | No |
| Autor (delegado) | `CreatedOnBehalfBy` | Búsqueda | No |
| Confirmación Proporcionada | `um_confirmationprovided` | Sí/No | No |
| Contacto Eliminado | `um_contactdeleted` | Sí/No | No |
| Código de zona horaria de conversión UTC | `UTCConversionTimeZoneCode` | Número entero | No |
| Detalle de Lote Masivo | `um_massexecutiondetailid` | Búsqueda | No |
| Duración (ms) | `um_durationms` | Número entero | No |
| Dígito Verificador | `um_dv` | Línea de texto única | No |
| Eliminación Habilitada | `um_deletionenabled` | Sí/No | No |
| Equipo propietario | `OwningTeam` | Búsqueda | No |
| Error Message Full | `um_errormessagefull` | Varias líneas de texto | No |
| Estado | `statecode` | Opción | Sí |
| Estado de operación | `um_operationstatus` | Opción | No |
| Execution ID | `um_ExecutionID` | Línea de texto única | No |
| Fecha de creación | `CreatedOn` | Fecha y hora | No |
| Fecha de creación del registro | `OverriddenCreatedOn` | Solo fecha | No |
| Fecha de modificación | `ModifiedOn` | Fecha y hora | No |
| Finalización | `um_finishedat` | Fecha y hora | No |
| ID de Contacto | `um_contactidtext` | Línea de texto única | No |
| Inicio | `um_startedat` | Fecha y hora | No |
| JSON de Respuesta | `um_responsejson` | Área de texto | No |
| JSON de Solicitud | `um_requestjson` | Área de texto | No |
| JSON Post Matriz | `um_postmatrixjson` | Área de texto | No |
| JSON Pre Matriz | `um_prematrixjson` | Área de texto | No |
| Lote de Proceso Masivo | `um_massexecutionid` | Búsqueda | No |
| Mensaje de Error | `um_errormessage` | Área de texto | No |
| Modificado por | `ModifiedBy` | Búsqueda | No |
| Modificado por (delegado) | `ModifiedOnBehalfBy` | Búsqueda | No |
| Nombre Completo del Contacto | `um_contactfullname` | Línea de texto única | No |
| Nombre del Archivo de Respaldo | `um_backupfilename` | Línea de texto única | No |
| Número de secuencia de importación | `ImportSequenceNumber` | Número entero | No |
| Número de versión | `VersionNumber` | Número entero grande | No |
| Número de versión de regla de zona horaria | `TimeZoneRuleVersionNumber` | Número entero | No |
| Origen | `um_source` | Opción | No |
| Pasaporte | `um_pasaporte` | Línea de texto única | No |
| Post Matrix JSON Full | `um_postmatrixjsonfull` | Varias líneas de texto | No |
| Pre Matrix JSON Full | `um_prematrixjsonfull` | Varias líneas de texto | No |
| Propietario | `OwnerId` | Propietario | Sí |
| Razón para el estado | `statuscode` | Opción | No |
| Registro de operación de privacidad | `um_privacyoperationlogId` | Identificador único | Sí |
| Request JSON Full | `um_requestjsonfull` | Varias líneas de texto | No |
| Respaldo Creado | `um_backupcreated` | Sí/No | No |
| Response JSON Full | `um_responsejsonfull` | Varias líneas de texto | No |
| RUT Completo | `um_rutcompleto` | Línea de texto única | No |
| RUT Ingresado | `um_rutingresado` | Línea de texto única | No |
| RUT Normalizado | `um_rutnormalizado` | Línea de texto única | No |
| Solicitado Por (Email) | `um_requestedbyemail` | Línea de texto única | No |
| Solicitado Por (Nombre) | `um_requestedbyname` | Línea de texto única | No |
| Tipo de operación | `um_operationtype` | Opción | No |
| Total de Errores | `um_totalerrors` | Número entero | No |
| Total Eliminado | `um_totaldeleted` | Número entero | No |
| Total Encontrado Antes de Eliminar | `um_totalfoundbeforedelete` | Número entero | No |
| Unidad de negocio propietaria | `OwningBusinessUnit` | Búsqueda | No |
| URL del Entorno | `um_environmenturl` | Línea de texto única | No |
| Usuario propietario | `OwningUser` | Búsqueda | No |

Relaciones confirmadas o inferidas desde QAS y el paquete del repositorio:

- `um_privacyoperationlog_massexecutionid_um_massexecution`: logs asociados a cabecera masiva.
- Lookup `um_massexecutiondetailid`: log asociado opcionalmente a un detalle de lote masivo.
- Relaciones estándar desde el paquete: `business_unit_um_privacyoperationlog`, `lk_um_privacyoperationlog_createdby`, `lk_um_privacyoperationlog_modifiedby`, `owner_um_privacyoperationlog`, `team_um_privacyoperationlog`, `user_um_privacyoperationlog`.

## 5. Choices / opciones

Objeto global visible:

- `Estado Proceso` / `um_estadoproceso`

Columnas tipo opción visibles:

- `um_massexecution.um_Estado`
- `um_massexecution.um_Tratamiento`
- `um_massexecutiondetail.um_Estado`
- `um_privacyoperationlog.um_operationstatus`
- `um_privacyoperationlog.um_operationtype`
- `um_privacyoperationlog.um_source`
- Además de los estándar `statecode` y `statuscode`.

Valores funcionales identificados desde los Swagger del repositorio:

- Modos de operación: `Consultar`, `EliminarTodo`, `EliminarTodoMenosContacto`
- Resultado por identificador: `Consultado`, `Eliminado`, `EliminadoMenosContacto`, `NoEncontrado`, `Error`
- Tratamiento masivo: `EliminarTodo`, `EliminarTodoMenosContacto`

Brecha: la revisión visual no permitió confirmar sin abrir pantallas de edición los valores numéricos exactos de los choices Dataverse en QAS. Esos valores deben extraerse desde exportación unmanaged actualizada o Web API Metadata para dejar el documento técnico cerrado.

## 6. Model Driven App, sitemap y páginas

### Model Driven App

- Nombre visible: `Ley de Protección de Datos / Derechos ARCO`
- Nombre único: `um_LeydeProtecciondeDatosDerechosARCO`
- Estado: activada
- Última modificación visible: hace 1 hora

### Sitemap

- Nombre visible: `Ley de Protección de Datos / Derechos ARCO`
- Nombre único: `um_LeydeProtecciondeDatosDerechosARCO`
- Última modificación visible: hace 2 horas

En el repositorio, `AppSolution/customizations.xml` contiene un sitemap anterior con:

- Área: `Área 1`
- Grupo: `Gestión ARCO`
- Subárea de tabla: `um_privacyoperationlog`, título `Historial de ejecuciones`
- Subárea de página: `um_ejecutarproceso_9b8d5`, título `Ejecutar Proceso`

QAS muestra tres páginas publicadas o incluidas como objetos de página:

- `Ejecutar Proceso Individual` / `um_ejecutarproceso_9b8d5`
- `Ejecutar Proceso Masivo` / `um_ejecutarprocesomasivo_2a9a1`
- `Reportes` / `um_reportes_81c37`

La solución satélite `Pagina_Leyproteccion` contiene exactamente esas tres páginas, todas no administradas, personalizadas, modificadas hace 2 días y con owner `ADM GRAL CRM`.

Brecha: el sitemap del repositorio no refleja completamente lo observado en QAS si la app publicada ya usa páginas de proceso masivo y reportes.

## 7. Custom Pages / Canvas

En la solución principal QAS se observan:

- Aplicación de lienzo `Ley de Protección de datos` / `um_leydeproteccindedatos_bdc6e`, owner `Admin CRM 2021`
- Página `Ejecutar Proceso Individual` / `um_ejecutarproceso_9b8d5`
- Página `Ejecutar Proceso Masivo` / `um_ejecutarprocesomasivo_2a9a1`
- Página `Reportes` / `um_reportes_81c37`

En el repositorio:

- `AppSolution/customizations.xml` solo incluye como root component de tipo página/canvas `um_ejecutarproceso_9b8d5`.
- El Canvas exportado tiene display `Ejecutar Proceso`, versión de app `2026-07-07T14:19:11Z`, estado `Ready`, formato tablet y color de fondo `RGBA(0,176,240,1)`.
- `AppSource/References/DataSources.json` referencia `MotorDynamicsAPI`, pero el WADL embebido solo muestra `ExecuteSingle` y `ExecuteBatch`.

Brecha: el repositorio contiene fuente Canvas parcial respecto de QAS. No se ve fuente de las páginas `Ejecutar Proceso Masivo` y `Reportes` dentro de `AppSolution/solution.xml` ni en la exportación Canvas principal revisada.

## 8. Connection references y custom connectors

En la solución principal QAS no se observaron objetos explícitos de tipo referencia de conexión ni conector personalizado dentro de los 17 objetos.

La solución satélite `CustomConnector_LeyProteccion` contiene:

- Conector personalizado: `MotorDynamicsAPI`
- Nombre único: `um_motordynamicsapi`
- Tipo: `Conector Personalizado`
- Administrado: no
- Personalizado: sí
- Última modificación: hace 3 días
- Owner: `Admin CRM 2021`
- Estado visible: `Desactivado`

En el repositorio:

- `AppSolution/customizations.xml` declara conexión Canvas a `MotorDynamicsAPI`.
- API id: `/providers/microsoft.powerapps/apis/shared_motordynamicsapi-5fa4dd8e0df5d4490a-5fd18e0a7d5c150807`
- `isCustomApiSolutionAware`: `false`
- Acciones en la exportación Canvas: `ExecuteSingle`
- `AppSource/References/DataSources.json` incluye WADL con `ExecuteSingle` y `ExecuteBatch`.
- `swagger_custom_connector_mass.yaml` define además operaciones de reportes y masivo:
  - `GetReportsCatalog`
  - `ExecuteReport`
  - `CreateMassLote`
  - `StartMassLote`
  - `GetMassLoteStatus`
  - `GetMassLoteDetails`

Brecha: QAS tiene el conector empaquetado aparte y el repositorio tiene Swagger masivo actualizado, pero la exportación Canvas revisada no demuestra que las operaciones masivas y de reportes estén ya consumidas desde las páginas actuales.

## 9. Roles de seguridad asociados

En la solución principal no se observaron roles de seguridad incluidos como componentes.

En `AppSolution/customizations.xml`, la Model Driven App contiene dos role maps:

- `{627090ff-40a3-4053-8790-584edc5be201}`
- `{119f245c-3cc8-4b62-b31c-d1a046ced15d}`

El nodo `<Roles>` del paquete está vacío, por lo que el repositorio no permite resolver los nombres ni privilegios de esos roles.

Brecha crítica documental y de despliegue: documentar nombre de rol, unidad de negocio, privilegios requeridos sobre `um_privacyoperationlog`, `um_massexecution`, `um_massexecutiondetail`, páginas, app model-driven y conector. Si se espera despliegue ALM, evaluar incluir roles en solución o documentar su prerequisito por ambiente.

## 10. Dependencias relevantes

Dependencias declaradas en `AppSolution/solution.xml` del repositorio:

- Web resource requerido: `msdyn_/Images/AppModule_Default_Icon.png`, solución `AppModuleWebResources (2.5)`, usado por la app `um_LeydeProtecciondeDatosDerechosARCO`.
- SettingDefinition `AppChannel`, solución `msdyn_AppFrameworkInfraExtensions (1.0.0.18)`, paquete `PowerAppsAppFramework_Anchor (1.0.0.25)`.

Dependencias funcionales observadas o inferidas:

- Custom connector `MotorDynamicsAPI` en solución separada.
- API backend QA: `um-ley-proteccion-datos-qa.azurewebsites.net`.
- Azure Web API `.NET 8`, Azure Function App y cola `privacy-mass-executions`, según README.
- Blob container `privacy-backups`, según README.
- Tablas Dataverse nuevas o extendidas: `um_privacyoperationlog`, `um_massexecution`, `um_massexecutiondetail`.
- Choice global `um_estadoproceso`.
- Páginas satélite en `Pagina_Leyproteccion`.

## 11. Contraste con el repositorio

### Diferencias de versión y alcance

- QAS muestra `LeydeProtecciondeDatos` en versión `1.0.0.4`.
- El repositorio `AppSolution/solution.xml` conserva versión `1.0.0.1`.
- QAS contiene 17 objetos; el paquete del repositorio declara solo 4 root components: `um_privacyoperationlog`, app model-driven, sitemap y página `um_ejecutarproceso_9b8d5`.

### Tablas

- QAS incluye tres tablas: `um_privacyoperationlog`, `um_massexecution`, `um_massexecutiondetail`.
- El paquete `AppSolution/customizations.xml` revisado documenta principalmente `um_privacyoperationlog`; no refleja completamente cabecera y detalle masivo en la exportación de solución.

### Páginas y navegación

- QAS incluye páginas de ejecución individual, ejecución masiva y reportes.
- El sitemap en el repositorio solo muestra historial y ejecución individual.
- La solución de páginas `Pagina_Leyproteccion` no aparece consolidada en la exportación principal del repositorio.

### Conector

- QAS separa `MotorDynamicsAPI` en `CustomConnector_LeyProteccion`.
- El repositorio contiene Swagger masivo con endpoints actualizados, pero la fuente Canvas exportada parece apuntar a un WADL antiguo o parcial.

### Seguridad

- La app tiene dos role maps por GUID en el XML.
- No hay roles incluidos en el paquete ni nombres resueltos.

### Documentación

- El README describe una arquitectura más completa que la exportación de Power Platform incluida en el repositorio.
- La documentación de backend, Azure y seguridad está bastante avanzada, pero falta cerrar el mapa exacto de componentes QAS y la trazabilidad entre solución principal, solución de páginas y solución de conector.

## 12. Hallazgos principales

1. QAS está por delante del repositorio en Power Platform: versión `1.0.0.4` contra `1.0.0.1`, más páginas, más tablas y más objetos.
2. La solución principal no es autosuficiente: depende de al menos dos soluciones satélite (`Pagina_Leyproteccion` y `CustomConnector_LeyProteccion`).
3. El conector `MotorDynamicsAPI` aparece desactivado en su solución, aunque la app lo referencia. Hay que verificar si el estado visible corresponde al componente o a una condición de publicación/conexión.
4. No hay flujos de nube en la solución principal; la orquestación masiva parece recaer en backend/Function/cola, no Power Automate.
5. Los roles de seguridad asociados a la app están por GUID y no están incluidos como componentes; esto es riesgo ALM y brecha documental.
6. Los valores numéricos de choices Dataverse no quedaron confirmados desde la lectura visual; los valores funcionales sí aparecen en los Swagger.
7. El entorno se presenta como `DEV Universidad Mayor` en Maker mientras la URL revisada es QAS; esto puede confundir soporte, despliegue y auditoría.
8. Solution checker no se ha ejecutado en las soluciones revisadas.
9. Las soluciones revisadas no están conectadas a Git desde Power Apps.
10. Las configuraciones de capacidad de IA asociadas a campos JSON largos aparecen desactivadas; conviene documentar si es una decisión de privacidad.

## 13. Brechas documentales para cerrar

- Inventario ALM definitivo por solución: solución principal, páginas, conector y cualquier solución temporal `C-06794-G3Q1_leyprotecc`.
- Exportación unmanaged actualizada desde QAS para capturar `1.0.0.4`.
- Diccionario de datos con valores numéricos exactos de choices.
- Nombres y privilegios de los dos roles asociados a la app.
- Mapa de navegación publicado real de la Model Driven App, incluyendo si `Ejecutar Proceso Masivo` y `Reportes` ya están en sitemap productivo.
- Fuente actualizada de Custom Pages/Canvas para las tres páginas.
- Matriz de dependencias y orden de despliegue: conector, connection references, tablas, choices, páginas, app model-driven, backend Azure.
- Estado esperado del conector `MotorDynamicsAPI`: activo/inactivo, autenticación, owner, connection references por ambiente.
- Procedimiento de solution checker y criterios mínimos antes de promoción.
- Decisión sobre conectar soluciones a Git o mantener Git como repositorio de código backend y exportaciones manuales.

## 14. Material base para documentos técnicos

### Documento de arquitectura

Debe cubrir:

- App Model Driven `Ley de Protección de Datos / Derechos ARCO`.
- Custom Pages: individual, masivo y reportes.
- API `MotorDynamicsAPI` en Azure App Service.
- Azure Function para procesamiento masivo.
- Cola `privacy-mass-executions`.
- Blob storage `privacy-backups`.
- Tablas de auditoría y control masivo.
- Modos funcionales: consulta, eliminación total, eliminación conservando contacto.

### Documento ALM / despliegue

Debe cubrir:

- Versiones actuales por solución.
- Soluciones satélite y orden de importación.
- Dependencias Microsoft (`AppChannel`, webresource default icon).
- Custom connector y conexión.
- Roles de seguridad prerrequeridos.
- Checklist de solution checker.
- Confirmación de ambiente QAS vs etiqueta DEV en Maker.

### Diccionario Dataverse

Debe cubrir:

- Columnas de las tres tablas levantadas.
- Relaciones principales y estándar.
- Choices y valores numéricos.
- Propietarios, búsqueda y obligatoriedad.
- Uso esperado de cada columna por endpoint/backend.

### Documento de seguridad y auditoría

Debe cubrir:

- Roles asociados y privilegios mínimos.
- Protección de campos JSON y decisión de desactivar AI form fill.
- Kill switch `Safety__DeletionEnabled`.
- Validación `Safety__RequireEnvironmentContains`.
- Registro inmutable en `um_privacyoperationlog`.
- Respaldo SHA-256 por Blob Storage.

### Documento funcional

Debe cubrir:

- Flujo individual por RUT/pasaporte.
- Flujo masivo: creación, inicio, estado, detalle.
- Reportes: catálogo y ejecución.
- Estados de lote, detalle y operación.
- Mensajes de error y conciliación.

## 15. Recomendación inmediata

La siguiente acción recomendada es generar una exportación unmanaged actualizada de QAS, versión `1.0.0.4`, incluyendo solución principal y soluciones satélite. Con eso se puede cerrar el diccionario Dataverse con valores de choices, resolver metadatos de páginas, confirmar el sitemap publicado y preparar los documentos técnicos con trazabilidad completa entre QAS y Git.
