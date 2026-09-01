# Recovery log - 2026-08-04

## Alcance

Correcciones controladas sobre la deteccion de eliminaciones incompletas en `EliminarTodoMenosContacto`, especialmente cuando quedan registros `customeraddress` relacionados por `parentid`.

## Archivos modificados

- `Program.cs`
  - Se actualizo `ApiBuild` a `mass-orchestration-v1-20260804-residual-validation`.
  - Se agrego `GetBlockingResiduals`.
  - `ExecuteBatch` ahora valida la matriz posterior.
  - En `EliminarTodoMenosContacto` solo se permite que quede `contact`.
  - Si queda `customeraddress` u otra entidad con `CantidadTotal > 0`, el estado pasa a `Error`.

- `Umayor.Dynamics.DeletePoc.Shared/Services/MatrixDeletionService.cs`
  - Las fallas al borrar `customeraddress` dejaron de registrarse solo como advertencia.
  - Toda falla de borrado ahora suma a `Errors`.
  - Se agrego `GetEntitiesToDeleteSnapshot` para exponer la coleccion respaldable sin reflexion.

- `Umayor.Dynamics.DeletePoc.Functions/QueueProcessorFunction.cs`
  - Se elimino el respaldo Blob preliminar basado en reflexion antes de recopilar entidades.
  - El respaldo Blob ahora usa la coleccion real recopilada por `MatrixDeletionService`.
  - Se agrego validacion post-eliminacion de residuos para el flujo masivo.
  - Si quedan residuos no autorizados, el detalle masivo se marca como error.

## Validacion local

- `dotnet build Umayor.Dynamics.DeletePoc.sln --no-restore`
- Resultado: compilacion correcta, 0 advertencias, 0 errores.
- `dotnet publish .\Umayor.Dynamics.DeletePoc.csproj -c Release -o .\publish`
- `Compress-Archive -Path .\publish\* -DestinationPath .\publish.zip -Force`
- Paquete generado: `publish.zip`.

## Despliegue QA

- App Service: `um-ley-proteccion-datos-qa`
- Resource Group: `admincrm2021_rg_0225`
- Metodo: Azure CLI ZipDeploy.
- Deployment ID: `35f05a1b30874116b86747526815ad9e`.
- Resultado Azure: `Succeeded`.
- Verificacion `/api/diagnostics/build`:
  - `apiBuild`: `mass-orchestration-v1-20260804-residual-validation`
  - `dataverseUrl`: `https://qas-umayor.crm2.dynamics.com`
  - `startedAtUtc`: `2026-08-04T16:29:27.5443490Z`

## Correccion adicional: customeraddress

- Se confirmo que Dataverse no permite borrar directamente `customeraddress` asociado a un contacto conservado.
- Error observado: `Customer Address can not be deleted because it is associated with another object`.
- Se implemento saneamiento de direcciones estandar:
  - Si `customeraddress` no se puede borrar en `EliminarTodoMenosContacto`, se limpian sus campos de direccion/contactabilidad.
  - Se limpian tambien campos `address1_*`, `address2_*`, `address3_*` del contacto cuando existan.
  - `customeraddress` solo se permite como residuo si el reporte indica que fue saneado.
- Nuevo `apiBuild`: `mass-orchestration-v1-20260804-address-scrub`.

## Despliegue QA adicional

- Deployment ID: `132a6e58304849eaa3e3fd6adc758c79`.
- Resultado Azure: `Succeeded`.
- Verificacion `/api/diagnostics/build`:
  - `apiBuild`: `mass-orchestration-v1-20260804-address-scrub`
  - `dataverseUrl`: `https://qas-umayor.crm2.dynamics.com`
  - `startedAtUtc`: `2026-08-04T16:55:36.1925921Z`

## Prueba controlada QA

- RUT: `13664433`.
- Modo: `EliminarTodoMenosContacto`.
- Execution ID: `ab06d3557e4045949ac6377c43432feb`.
- Resultado: `EliminadoMenosContacto`.
- `customeraddress`:
  - Registros tecnicos residuales: `3`.
  - Registros saneados: `3`.
  - Entidad residual permitida solo por saneamiento: `customeraddress`.
- Total errores: `0`.
- Advertencias registradas: `3`, una por cada `customeraddress` que Dataverse no permitio borrar directamente.

## Pendiente antes de desplegar

- Ejecutar prueba controlada en QA con `Safety__DeletionEnabled=false` para consulta.
- Habilitar eliminacion solo para un RUT controlado cuando Rogelio lo apruebe.
- Validar que si queda `customeraddress`, el endpoint responda `Error` y registre el residuo.
- Validar que el respaldo Blob del masivo tenga registros y no quede vacio.

## Evolucion masiva por archivo

- Nuevo `apiBuild`: `mass-orchestration-v1-20260804-file-mass`.
- Se agrego entrada oficial por archivo:
  - `POST /api/mass/upload`
  - Recibe `multipart/form-data` con archivo `.csv` o `.txt`.
  - Acepta identificadores separados por salto de linea, coma, punto y coma o tab.
  - Guarda el archivo original en Blob Storage bajo `mass-executions/{executionId}/source/`.
  - Devuelve referencia Blob y hash SHA-256 del archivo fuente.
- Se agrego soporte para lotes grandes sin enviar miles de identificadores desde pantalla:
  - `MassOrchestration:MaxUploadRows` default `50000`.
  - `MassOrchestration:PartitionSize` default `500`.
  - `POST /api/mass/start/{executionId}` ahora encola particiones con grupos de `detailId`.
  - Si no hay pendientes validos, se encola una verificacion de cierre para evitar lotes estancados.
  - La Function conserva compatibilidad con mensajes antiguos y procesa solo los detalles de la particion cuando vienen informados.
- Se agrego monitoreo masivo mas liviano:
  - `GET /api/mass/list` para listar ultimos lotes.
  - `GET /api/mass/details/{executionId}?page=1&pageSize=200&status=...` para detalle paginado.
  - Sin parametros de paginacion, el endpoint mantiene compatibilidad con la respuesta anterior.
- Se actualizo la consola web estatica:
  - Permite seleccionar CSV/TXT.
  - Si hay archivo, usa `/api/mass/upload`.
  - Si no hay archivo, mantiene la carga manual por `/api/mass/create`.
  - El polling consulta detalle paginado para no descargar miles de filas cada 3 segundos.

## Validacion local masivo por archivo

- `dotnet build .\Umayor.Dynamics.DeletePoc.sln`
- Resultado: compilacion correcta, 0 advertencias, 0 errores.

## Pendiente funcional masivo por archivo

- Probar carga CSV/TXT con lotes de 10, 100, 500, 2000 y 10000 registros.
- Confirmar en Azure Storage que el archivo fuente queda guardado con hash.
- Confirmar que la cola recibe multiples particiones por lote.
- Confirmar que el dashboard avanza por resumen sin descargar el lote completo.
- Definir si la vista final sera Power Apps institucional o consola web del App Service.

## Despliegue QA masivo por archivo

- Fecha: 2026-08-04.
- API App Service: `um-ley-proteccion-datos-qa`.
- Deployment ID API: `c86a425023b44ba9b8cffa39a4cc3d6d`.
- Function App: `um-ley-proteccion-datos-qa-fun`.
- Deployment ID Function: `c0f69189b0104a8a9dba67cf99037ae9`.
- Verificacion `/api/diagnostics/build`:
  - `apiBuild`: `mass-orchestration-v1-20260804-file-mass`
  - `dataverseUrl`: `https://qas-umayor.crm2.dynamics.com`
- Verificacion Function App:
  - Estado: `Running`.
  - Funcion publicada: `QueueProcessorFunction` con `queueTrigger`.
- Verificacion endpoint nuevo:
  - `GET /api/mass/list?top=3` respondio correctamente.

## Correcciones QA durante prueba masiva

- Lote probado: `4beace4561094acdba787e39ae7613fc`.
- Se detecto que `GET /api/mass/status` fallaba con 500 por contadores numericos/texto heredados.
- Se cambio lectura de contadores de cabecera a `ReadIntAttribute`.
- Se agrego recuperacion controlada:
  - `POST /api/mass/recover-stuck/{executionId}`.
  - Recupera detalles `EnProceso` y errores no controlados del worker para devolverlos a `Pendiente`.
- Se corrigio el worker masivo:
  - No consulta columnas de auditoria inexistentes (`um_backupreference`, etc.) en `um_privacyoperationlog`.
  - Los contadores se actualizan respetando si el campo Dataverse viene como texto o entero.
  - Errores no controlados del worker se registran en el detalle en vez de dejarlo indefinidamente `EnProceso`.
  - El cierre de cabecera usa lectura tolerante de contadores.
- Despliegues correctivos:
  - API: `b596a925477643cead1e6bed78ce3191`, `37bd382c719e4f998cda2e830a01ef95`, `2d10d79249a949e781aefdf638c41548`.
  - Function: `7726e4c9eecd494e8aedf7d0b67d3123`, `6ccc2837530b4728a457fa41be521377`, `cbe73d8144b543afb76a1635722041f5`.
- Resultado final lote `4beace4561094acdba787e39ae7613fc`:
  - Estado: `Completado`.
  - Total: `8`.
  - Procesados: `8`.
  - Exitosos: `8`.
  - Errores: `0`.
  - Requiere conciliacion: `0`.

## Ordenamiento de nombres y detalle masivo

- Hallazgo: los endpoints masivos nuevos creaban `um_massexecutiondetail` sin `um_name`, por eso en Dataverse aparecian registros de detalle sin nombre o poco legibles.
- Correccion:
  - `POST /api/mass/create` y `POST /api/mass/upload` ahora crean cada detalle con `um_name`.
  - El formato usado es `{Tipo} {Identificador} - {Estado}`, por ejemplo `RUT 7168280 - Pendiente`.
  - La Function actualiza el `um_name` cuando el detalle termina, por ejemplo `RUT 7168280 - Consultado`.
  - Se agrego `POST /api/mass/backfill-names/{executionId}` para sanear nombres de lotes ya creados.
- Despliegue QA:
  - API Deployment ID: `7ccec3abb41f4d1ba7d2be5f5401e4f9`.
  - Function Deployment ID: `77eec8e7d246450f9539dbea129a1ef2`.
- Backfill ejecutado:
  - Lote: `4beace4561094acdba787e39ae7613fc`.
  - Detalles actualizados: `8`.
- Pendiente visual/model-driven:
  - Si desde la cabecera `um_massexecution` el panel Relacionados no muestra detalles, falta ajustar la app/model-driven form o subgrid de Dataverse/Power Apps para exponer la relacion hacia `um_massexecutiondetail`.

## Correccion RUT sin DV

- Hallazgo: `7168280` existia en Dataverse, pero la Power App lo informaba como no encontrado.
- Causa: la autodeteccion del endpoint single interpretaba RUT numericos de 7 digitos sin DV como pasaporte.
- Correccion:
  - `ExecuteSingle` ahora trata valores numericos de 7 u 8 digitos como cuerpo de RUT valido.
  - `InputValidator` tambien acepta RUTs sin DV en cargas masivas/manuales, calculando DV solo como metadata.
- Despliegue QA:
  - API Deployment ID: `decde01d14184c7cb69440e045dca9c2`.
  - Function Deployment ID: `478bcb75cf5846cda59e709a181b5779`.
- Verificacion:
  - `POST /api/execute-single` con `rut=7168280` devolvio `Consultado`.
  - Contacto encontrado: `SOFIA LORENA QUIDENAO`.
  - `POST /api/mass/create` con `7168280` y `16632938` devolvio 2 validos, 0 invalidos.
  - Lote tecnico creado solo para validacion, no iniciado: `e82ee9b626ab4c878b34cda2f762018f`.

## Correccion EliminarTodo con customeraddress

- Hallazgo: un lote `EliminarTodo` con RUTs `16632938` y `7168280` termino como `CompletadoConErrores` porque Dataverse no permite borrar `customeraddress` directamente cuando la direccion esta asociada al contacto.
- Evidencia: los mensajes de error indicaban `Customer Address can not be deleted because it is associated with another object`.
- Diagnostico: la eliminacion real del contacto si ocurrio para ambos RUTs, pero el detalle quedo marcado como error por el intento previo de borrar `customeraddress`.
- Correccion:
  - En `EliminarTodo`, `MatrixDeletionService` ahora respalda `customeraddress` pero no lo borra directamente; deja que Dataverse lo administre con la eliminacion del contacto.
  - En `EliminarTodoMenosContacto` se mantiene el saneamiento de `customeraddress`, porque en ese modo el contacto se conserva.
  - `QueueProcessorFunction` dejo de escribir campos antiguos de respaldo en `um_privacyoperationlog` y usa los campos existentes `um_backupcreated` y `um_backupfilename`.
- Despliegue QA:
  - API Deployment ID: `0456f1804f05470eb28abeab5052dff7`.
  - Function Deployment ID: `4df98fb7e7954aa68905fdbb7d3381eb`.
- Verificacion:
  - Compilacion completa de la solucion: 0 errores.
  - Consulta posterior de `7168280`: `NoEncontrado`.
  - Consulta posterior de `16632938`: `NoEncontrado`.
- Nota operativa:
  - No reutilizar `7168280` ni `16632938` como casos positivos de existencia en nuevas pruebas, porque ya fueron eliminados en QA.

## Control de tratamiento en consola masiva

- Hallazgo: el usuario selecciono `EliminarTodo`, pero el lote `425ebbfe98ee40b0a5608ebb59d95153` quedo registrado en Dataverse como `Consultar`.
- Evidencia:
  - API `/api/mass/list?top=10` mostro el lote `425ebbfe98ee40b0a5608ebb59d95153` con nombre `Ejecución Consultar 2026-08-04 20:28:10` y tratamiento `Consultar`.
- Correccion:
  - La consola tecnica ahora muestra/actualiza el boton con el tratamiento seleccionado.
  - Para tratamientos destructivos se despliega un campo de confirmacion y exige escribir `ELIMINAR`.
  - El frontend envia `confirmationText` tanto por carga manual como por archivo CSV/TXT.
  - El backend valida estrictamente el tratamiento masivo y rechaza eliminaciones sin confirmacion.
  - El backend ya no convierte silenciosamente un tratamiento vacio a `Consultar` en carga de archivo.
- Despliegue QA:
  - API Deployment ID: `1e3ede35bc8244d2beeb71e1f14e4235`.
  - Function Deployment ID: `b63489e9db2647e3aa76838262043ef6`.
- Verificacion:
  - Compilacion completa de la solucion: 0 errores.
  - `POST /api/mass/create` con `EliminarTodo` y sin confirmacion devolvio HTTP 400.
  - Mensaje esperado: `Para iniciar EliminarTodo masivo debe confirmar escribiendo ELIMINAR.`
  - `index.html` publicado contiene `massDeleteConfirmInput`.

## Correccion EliminarTodo con activitypointer

- Hallazgo: un detalle masivo `EliminarTodo` fallo con el mensaje `The 'Delete' method does not support entities of type 'activitypointer'`.
- Causa: `activitypointer` es la entidad base de actividades en Dataverse y no soporta borrado directo. Deben eliminarse las entidades concretas de actividad cuando corresponda (`email`, `phonecall`, `wit_sms`, `wit_whatsapp`, etc.).
- Correccion:
  - `MatrixDeletionService` conserva `activitypointer` para respaldo/evidencia, pero lo omite en la ejecucion directa de `Delete`.
  - El proceso registra una advertencia `[SKIPPED]` en vez de marcar error por esa entidad.
- Despliegue QA:
  - API Deployment ID: `507b99bbc74d4ba9afc4143db32d0ea4`.
  - Function Deployment ID: `4ac006c71f6945e7bba129615cea8f7f`.
- Verificacion:
  - Compilacion completa de la solucion: 0 errores.

## Homologacion JSON de auditoria en proceso masivo

- Hallazgo: el proceso individual almacena evidencia completa en `um_privacyoperationlog` usando `um_requestjsonfull`, `um_responsejsonfull`, `um_prematrixjsonfull`, `um_postmatrixjsonfull` y `um_errormessagefull`; el worker masivo solo completaba estado, backup y resultado resumido.
- Riesgo: los lotes masivos quedaban con evidencia insuficiente para revision posterior, aun cuando el detalle mostrara `Eliminado`, `Consultado` o `Error`.
- Correccion:
  - `QueueProcessorFunction` ahora crea cada auditoria masiva con `um_requestjsonfull`.
  - En consultas masivas, guarda `um_responsejsonfull` y `um_postmatrixjsonfull`.
  - En eliminaciones masivas, guarda `um_responsejsonfull`, `um_prematrixjsonfull`, `um_postmatrixjsonfull`, metadatos de backup y total eliminado.
  - En errores masivos, guarda respuesta de error, matrices disponibles y `um_errormessagefull`.
  - Se agrego actualizacion con fallback a 4000 caracteres para evitar fallos si Dataverse rechaza campos JSON largos.
- Despliegue QA:
  - Function Deployment ID: `c0b549d61808472483a1ae3312cd05e0`.
- Verificacion:
  - Compilacion del proyecto `Umayor.Dynamics.DeletePoc.Functions`: 0 errores.
