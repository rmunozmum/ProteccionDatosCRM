/*==========================================================
 Framework LPD v2.0 - Ley N° 21.719
 Universidad Mayor - Microsoft Dynamics 365 CRM

 Archivo : LPD-R01 – Informe de Datos Personales.sql
 Versión : 2.1
 Cambio  : Se incorpora PASAPORTE en la salida de datos y búsqueda principal del titular.

 Catálogo v2.0 - Identificadores personales incluidos:
 - RUT / RUN / Cédula de identidad
 - Pasaporte / Passport / PassportNumber
 - Número de pasaporte / Nro pasaporte / Num pasaporte
 - DNI / Documento de identidad / Documento extranjero
 - Licencia de conducir / Driver License

 Nota técnica:
 Cuando el reporte utiliza metadata, la detección se realiza mediante
 nombres de columnas que contengan las palabras clave anteriores.
 Cuando el reporte contiene relaciones operacionales específicas, se
 conserva la estructura original y se documenta el criterio v2.0.
==========================================================*/

DECLARE @Rut NVARCHAR(50) = '';
DECLARE @UsuarioEjecutor NVARCHAR(200) = 'Operador ARCO UMayor';
DECLARE @CorreoEjecutor NVARCHAR(300) = '';
DECLARE @AreaEjecutor NVARCHAR(200) = '';
DECLARE @Motivo NVARCHAR(500) = 'Solicitud de Acceso del Titular';
DECLARE @RutNormalizado NVARCHAR(50) = UPPER(REPLACE(REPLACE(REPLACE(@Rut, '.', ''), '-', ''), ' ', ''));
DECLARE @PasaporteNormalizado NVARCHAR(100) = UPPER(REPLACE(REPLACE(REPLACE(@Rut, '.', ''), '-', ''), ' ', ''));
DECLARE @ContactId UNIQUEIDENTIFIER;
DECLARE @Nombre NVARCHAR(300);
DECLARE @RutRegistrado NVARCHAR(50);
DECLARE @Pasaporte NVARCHAR(100) = 'PASAPORTE_EJEMPLO';
DECLARE @Correo NVARCHAR(300);
DECLARE @Telefono NVARCHAR(100);
DECLARE @FechaCreacion DATETIME;
DECLARE @FechaModificacion DATETIME;
DECLARE @FechaConsulta DATETIME = GETDATE();

SELECT TOP 1
    @ContactId = contactid,
    @Nombre = fullname,
    @RutRegistrado = wit_rut,
    @Pasaporte = wit_pasaporte,
    @Correo = emailaddress1,
    @Telefono = mobilephone,
    @FechaCreacion = createdon,
    @FechaModificacion = modifiedon
FROM contact
WHERE
       UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
    OR UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_pasaporte, ''), '.', ''), '-', ''), ' ', '')) = @PasaporteNormalizado
ORDER BY modifiedon DESC;

SELECT 'INFORME DE ACCESO A DATOS PERSONALES - LEY 21.719' AS campo, '' AS valor
UNION ALL SELECT 'Empresa', 'Universidad Mayor'
UNION ALL SELECT 'Sistema', 'Dynamics 365 CRM'
UNION ALL SELECT 'Tipo informe', 'Derecho de Acceso ARCO'
UNION ALL SELECT 'Version informe', '2.1'
UNION ALL SELECT 'Numero informe', 'ARCO-' + CONVERT(NVARCHAR(8), @FechaConsulta, 112) + '-' + REPLACE(CONVERT(NVARCHAR(8), @FechaConsulta, 108), ':', '')
UNION ALL SELECT 'Nombre titular', ISNULL(@Nombre, '')
UNION ALL SELECT 'RUT', ISNULL(@RutRegistrado, @Rut)
UNION ALL SELECT 'Pasaporte', ISNULL(@Pasaporte, '')
UNION ALL SELECT 'Correo principal', ISNULL(@Correo, '')
UNION ALL SELECT 'Telefono principal', ISNULL(@Telefono, '')
UNION ALL SELECT 'Fecha consulta', CONVERT(NVARCHAR(30), @FechaConsulta, 120)
UNION ALL SELECT 'Fecha creacion contacto', ISNULL(CONVERT(NVARCHAR(30), @FechaCreacion, 120), '')
UNION ALL SELECT 'Fecha ultima modificacion contacto', ISNULL(CONVERT(NVARCHAR(30), @FechaModificacion, 120), '')
UNION ALL SELECT 'Usuario ejecutor', ISNULL(@UsuarioEjecutor, '')
UNION ALL SELECT 'Correo ejecutor', ISNULL(@CorreoEjecutor, '')
UNION ALL SELECT 'Area ejecutor', ISNULL(@AreaEjecutor, '')
UNION ALL SELECT 'Motivo consulta', ISNULL(@Motivo, '')
UNION ALL SELECT 'Origen', 'Dataverse TDS Endpoint';

SELECT
    tabla,
    criterio_relacion,
    contiene_datos_personales,
    contiene_datos_sensibles,
    CASE
        WHEN contiene_datos_sensibles = 'SI' THEN 'ALTA'
        WHEN contiene_datos_personales = 'SI' THEN 'MEDIA'
        WHEN tabla = 'TOTAL DE REGISTROS' THEN ''
        ELSE 'BAJA'
    END AS criticidad,
    cantidad_registros
FROM (
SELECT 1 AS orden, 'contact' AS tabla, 'contact.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM contact WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado OR UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_pasaporte, ''), '.', ''), '-', ''), ' ', '')) = @PasaporteNormalizado
UNION ALL
SELECT 2 AS orden, 'lead por parentcontactid' AS tabla, 'lead.parentcontactid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM lead WHERE parentcontactid = @ContactId
UNION ALL
SELECT 3 AS orden, 'lead por customerid' AS tabla, 'lead.customerid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM lead WHERE customerid = @ContactId
UNION ALL
SELECT 4 AS orden, 'incident por customerid' AS tabla, 'incident.customerid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE customerid = @ContactId
UNION ALL
SELECT 5 AS orden, 'incident por primarycontactid' AS tabla, 'incident.primarycontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE primarycontactid = @ContactId
UNION ALL
SELECT 6 AS orden, 'incident por responsiblecontactid' AS tabla, 'incident.responsiblecontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE responsiblecontactid = @ContactId
UNION ALL
SELECT 7 AS orden, 'incident por msa_partnercontactid' AS tabla, 'incident.msa_partnercontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE msa_partnercontactid = @ContactId
UNION ALL
SELECT 8 AS orden, 'phonecall por wit_rut' AS tabla, 'phonecall.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM phonecall WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 9 AS orden, 'phonecall por regardingobjectid' AS tabla, 'phonecall.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM phonecall WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 10 AS orden, 'email' AS tabla, 'email.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM email WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 11 AS orden, 'task' AS tabla, 'task.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM task WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 12 AS orden, 'wit_whatsapp' AS tabla, 'wit_whatsapp.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_whatsapp WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 13 AS orden, 'wit_sms' AS tabla, 'wit_sms.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_sms WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 14 AS orden, 'wit_visitaweb' AS tabla, 'wit_visitaweb.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_visitaweb WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 15 AS orden, 'wit_visitapresencial' AS tabla, 'wit_visitapresencial.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_visitapresencial WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 16 AS orden, 'wit_evento' AS tabla, 'wit_evento.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_evento WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 17 AS orden, 'activityparty' AS tabla, 'activityparty.partyid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM activityparty WHERE partyid = @ContactId
UNION ALL
SELECT 18 AS orden, 'annotation contacto' AS tabla, 'annotation.objectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM annotation WHERE objectid = @ContactId
UNION ALL
SELECT 19 AS orden, 'annotation casos' AS tabla, 'annotation.objectid = incident.incidentid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM annotation WHERE objectid IN (SELECT incidentid FROM incident WHERE customerid = @ContactId)
UNION ALL
SELECT 20 AS orden, 'customeraddress' AS tabla, 'customeraddress.parentid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM customeraddress WHERE parentid = @ContactId
UNION ALL
SELECT 21 AS orden, 'socialprofile' AS tabla, 'socialprofile.customerid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM socialprofile WHERE customerid = @ContactId
UNION ALL
SELECT 22 AS orden, 'msdyn_ocliveworkitem cliente' AS tabla, 'msdyn_ocliveworkitem.msdyn_customer = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM msdyn_ocliveworkitem WHERE msdyn_customer = @ContactId
UNION ALL
SELECT 23 AS orden, 'msdyn_ocliveworkitem relacionado' AS tabla, 'msdyn_ocliveworkitem.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM msdyn_ocliveworkitem WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 24 AS orden, 'wit_actividadchat por wit_rut' AS tabla, 'wit_actividadchat.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_actividadchat WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 25 AS orden, 'wit_actividadchat por regardingobjectid' AS tabla, 'wit_actividadchat.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_actividadchat WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 26 AS orden, 'wit_procesodepostulacion' AS tabla, 'wit_procesodepostulacion.wit_contacto = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_procesodepostulacion WHERE wit_contacto = @ContactId
UNION ALL
SELECT 27 AS orden, 'wit_solicituddeadmisiondirecta por postulante' AS tabla, 'wit_solicituddeadmisiondirecta.wit_postulante = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_solicituddeadmisiondirecta WHERE wit_postulante = @ContactId
UNION ALL
SELECT 28 AS orden, 'wit_solicituddeadmisiondirecta por rut' AS tabla, 'wit_solicituddeadmisiondirecta.wit_rut' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_solicituddeadmisiondirecta WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 29 AS orden, 'wit_historicocontactos contacto' AS tabla, 'wit_historicocontactos.wit_contact = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocontactos WHERE wit_contact = @ContactId
UNION ALL
SELECT 30 AS orden, 'wit_historicocontactos relacionado' AS tabla, 'wit_historicocontactos.wit_contactorelacionado = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocontactos WHERE wit_contactorelacionado = @ContactId
UNION ALL
SELECT 31 AS orden, 'wit_historicodatosdecontactabilidad contacto' AS tabla, 'wit_historicodatosdecontactabilidad.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosdecontactabilidad WHERE wit_contacto = @ContactId
UNION ALL
SELECT 32 AS orden, 'wit_historicodatosdecontactabilidad rut' AS tabla, 'wit_historicodatosdecontactabilidad.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosdecontactabilidad WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 33 AS orden, 'wit_contactodelsegmentocomercial contacto' AS tabla, 'wit_contactodelsegmentocomercial.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelsegmentocomercial WHERE wit_contacto = @ContactId
UNION ALL
SELECT 34 AS orden, 'wit_contactodelsegmentocomercial rut' AS tabla, 'wit_contactodelsegmentocomercial.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelsegmentocomercial WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 35 AS orden, 'wit_historicodatosofertaacademica' AS tabla, 'wit_historicodatosofertaacademica.wit_contactoid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosofertaacademica WHERE wit_contactoid = @ContactId
UNION ALL
SELECT 36 AS orden, 'wit_historicocarreramatriculada' AS tabla, 'wit_historicocarreramatriculada.wit_contacto = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocarreramatriculada WHERE wit_contacto = @ContactId
UNION ALL
SELECT 37 AS orden, 'wit_contactodelacuenta' AS tabla, 'wit_contactodelacuenta.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelacuenta WHERE wit_contacto = @ContactId
UNION ALL
SELECT 38 AS orden, 'email_attachment' AS tabla, 'activitymimeattachment.objectid = email.activityid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM activitymimeattachment WHERE objectid IN (SELECT activityid FROM email WHERE regardingobjectid = @ContactId)
UNION ALL
SELECT 999 AS orden, 'TOTAL DE REGISTROS' AS tabla, 'Tablas con registros: ' + CAST(SUM(CASE WHEN cantidad_registros > 0 THEN 1 ELSE 0 END) AS NVARCHAR(20)) AS criterio_relacion, '' AS contiene_datos_personales, '' AS contiene_datos_sensibles, SUM(cantidad_registros) AS cantidad_registros FROM (
SELECT 1 AS orden, 'contact' AS tabla, 'contact.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM contact WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado OR UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_pasaporte, ''), '.', ''), '-', ''), ' ', '')) = @PasaporteNormalizado
UNION ALL
SELECT 2 AS orden, 'lead por parentcontactid' AS tabla, 'lead.parentcontactid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM lead WHERE parentcontactid = @ContactId
UNION ALL
SELECT 3 AS orden, 'lead por customerid' AS tabla, 'lead.customerid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM lead WHERE customerid = @ContactId
UNION ALL
SELECT 4 AS orden, 'incident por customerid' AS tabla, 'incident.customerid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE customerid = @ContactId
UNION ALL
SELECT 5 AS orden, 'incident por primarycontactid' AS tabla, 'incident.primarycontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE primarycontactid = @ContactId
UNION ALL
SELECT 6 AS orden, 'incident por responsiblecontactid' AS tabla, 'incident.responsiblecontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE responsiblecontactid = @ContactId
UNION ALL
SELECT 7 AS orden, 'incident por msa_partnercontactid' AS tabla, 'incident.msa_partnercontactid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM incident WHERE msa_partnercontactid = @ContactId
UNION ALL
SELECT 8 AS orden, 'phonecall por wit_rut' AS tabla, 'phonecall.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM phonecall WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 9 AS orden, 'phonecall por regardingobjectid' AS tabla, 'phonecall.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM phonecall WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 10 AS orden, 'email' AS tabla, 'email.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM email WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 11 AS orden, 'task' AS tabla, 'task.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM task WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 12 AS orden, 'wit_whatsapp' AS tabla, 'wit_whatsapp.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_whatsapp WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 13 AS orden, 'wit_sms' AS tabla, 'wit_sms.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_sms WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 14 AS orden, 'wit_visitaweb' AS tabla, 'wit_visitaweb.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_visitaweb WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 15 AS orden, 'wit_visitapresencial' AS tabla, 'wit_visitapresencial.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_visitapresencial WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 16 AS orden, 'wit_evento' AS tabla, 'wit_evento.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_evento WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 17 AS orden, 'activityparty' AS tabla, 'activityparty.partyid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM activityparty WHERE partyid = @ContactId
UNION ALL
SELECT 18 AS orden, 'annotation contacto' AS tabla, 'annotation.objectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM annotation WHERE objectid = @ContactId
UNION ALL
SELECT 19 AS orden, 'annotation casos' AS tabla, 'annotation.objectid = incident.incidentid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM annotation WHERE objectid IN (SELECT incidentid FROM incident WHERE customerid = @ContactId)
UNION ALL
SELECT 20 AS orden, 'customeraddress' AS tabla, 'customeraddress.parentid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM customeraddress WHERE parentid = @ContactId
UNION ALL
SELECT 21 AS orden, 'socialprofile' AS tabla, 'socialprofile.customerid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM socialprofile WHERE customerid = @ContactId
UNION ALL
SELECT 22 AS orden, 'msdyn_ocliveworkitem cliente' AS tabla, 'msdyn_ocliveworkitem.msdyn_customer = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM msdyn_ocliveworkitem WHERE msdyn_customer = @ContactId
UNION ALL
SELECT 23 AS orden, 'msdyn_ocliveworkitem relacionado' AS tabla, 'msdyn_ocliveworkitem.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM msdyn_ocliveworkitem WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 24 AS orden, 'wit_actividadchat por wit_rut' AS tabla, 'wit_actividadchat.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_actividadchat WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 25 AS orden, 'wit_actividadchat por regardingobjectid' AS tabla, 'wit_actividadchat.regardingobjectid = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_actividadchat WHERE regardingobjectid = @ContactId
UNION ALL
SELECT 26 AS orden, 'wit_procesodepostulacion' AS tabla, 'wit_procesodepostulacion.wit_contacto = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_procesodepostulacion WHERE wit_contacto = @ContactId
UNION ALL
SELECT 27 AS orden, 'wit_solicituddeadmisiondirecta por postulante' AS tabla, 'wit_solicituddeadmisiondirecta.wit_postulante = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_solicituddeadmisiondirecta WHERE wit_postulante = @ContactId
UNION ALL
SELECT 28 AS orden, 'wit_solicituddeadmisiondirecta por rut' AS tabla, 'wit_solicituddeadmisiondirecta.wit_rut' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_solicituddeadmisiondirecta WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 29 AS orden, 'wit_historicocontactos contacto' AS tabla, 'wit_historicocontactos.wit_contact = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocontactos WHERE wit_contact = @ContactId
UNION ALL
SELECT 30 AS orden, 'wit_historicocontactos relacionado' AS tabla, 'wit_historicocontactos.wit_contactorelacionado = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocontactos WHERE wit_contactorelacionado = @ContactId
UNION ALL
SELECT 31 AS orden, 'wit_historicodatosdecontactabilidad contacto' AS tabla, 'wit_historicodatosdecontactabilidad.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosdecontactabilidad WHERE wit_contacto = @ContactId
UNION ALL
SELECT 32 AS orden, 'wit_historicodatosdecontactabilidad rut' AS tabla, 'wit_historicodatosdecontactabilidad.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosdecontactabilidad WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 33 AS orden, 'wit_contactodelsegmentocomercial contacto' AS tabla, 'wit_contactodelsegmentocomercial.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelsegmentocomercial WHERE wit_contacto = @ContactId
UNION ALL
SELECT 34 AS orden, 'wit_contactodelsegmentocomercial rut' AS tabla, 'wit_contactodelsegmentocomercial.wit_rut' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelsegmentocomercial WHERE UPPER(REPLACE(REPLACE(REPLACE(ISNULL(wit_rut, ''), '.', ''), '-', ''), ' ', '')) = @RutNormalizado
UNION ALL
SELECT 35 AS orden, 'wit_historicodatosofertaacademica' AS tabla, 'wit_historicodatosofertaacademica.wit_contactoid = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicodatosofertaacademica WHERE wit_contactoid = @ContactId
UNION ALL
SELECT 36 AS orden, 'wit_historicocarreramatriculada' AS tabla, 'wit_historicocarreramatriculada.wit_contacto = contact.contactid' AS criterio_relacion, 'NO' AS contiene_datos_personales, 'SI' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_historicocarreramatriculada WHERE wit_contacto = @ContactId
UNION ALL
SELECT 37 AS orden, 'wit_contactodelacuenta' AS tabla, 'wit_contactodelacuenta.wit_contacto = contact.contactid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM wit_contactodelacuenta WHERE wit_contacto = @ContactId
UNION ALL
SELECT 38 AS orden, 'email_attachment' AS tabla, 'activitymimeattachment.objectid = email.activityid' AS criterio_relacion, 'SI' AS contiene_datos_personales, 'NO' AS contiene_datos_sensibles, COUNT(1) AS cantidad_registros FROM activitymimeattachment WHERE objectid IN (SELECT activityid FROM email WHERE regardingobjectid = @ContactId)
) total_base
) resultado
ORDER BY orden;


SELECT 'OBSERVACIONES LEGALES' AS campo, '' AS valor
UNION ALL SELECT 'Uso del informe', 'Informe generado para responder una solicitud de acceso del titular conforme a Ley 21.719'
UNION ALL SELECT 'Alcance', 'La informacion corresponde al estado de Dynamics 365 CRM al momento de la consulta'
UNION ALL SELECT 'Restriccion', 'Este informe contiene datos personales y su uso queda restringido al proposito informado';

SELECT 'TRAZABILIDAD' AS campo, '' AS valor
UNION ALL SELECT 'Generado por', ISNULL(@UsuarioEjecutor, '')
UNION ALL SELECT 'Fecha generacion', CONVERT(NVARCHAR(30), @FechaConsulta, 120)
UNION ALL SELECT 'Sistema origen', 'Dynamics 365 CRM'
UNION ALL SELECT 'Version SQL', '2.1 Dataverse compatible - incluye wit_pasaporte'
UNION ALL SELECT 'Fin informe', 'Modulo 1 ARCO finalizado';


/* Nota Framework LPD v2.0:
   Pasaporte se incorpora como Dato Personal de Identificación.
   Si en el modelo Dataverse existe una columna física de pasaporte, debe agregarse
   al bloque de datos del titular y a las reglas de búsqueda equivalentes al RUT.
   Patrones sugeridos: pasaporte, passport, passportnumber, numero_pasaporte,
   nro_pasaporte, num_pasaporte, documento_extranjero, dni.
*/
