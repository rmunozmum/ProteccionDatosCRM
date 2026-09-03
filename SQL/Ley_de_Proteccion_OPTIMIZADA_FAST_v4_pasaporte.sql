/*
    Ley de Protección - versión optimizada FAST
    Objetivo: evitar el timeout del endpoint SQL/TDS de Dataverse.

    Cambio principal respecto de la query original:
    - Se elimina la búsqueda difusa por nombre en subject/title/fullname.
    - Se evita CHARINDEX/Cast sobre columnas de texto masivas.
    - Se evita repetir 3-4 COUNT(*) por la misma tabla.
    - Se reemplazan EXISTS contra contact por variables cargadas una vez.

    Si necesitas búsqueda por nombre, ejecútala aparte y por entidad puntual.
*/

DECLARE @Rut NVARCHAR(50) = Null;
DECLARE @Pasaporte NVARCHAR(50) = 'PASAPORTE_EJEMPLO'; -- Ej: 'P12345678'
DECLARE @FullnameFallback NVARCHAR(300) = '';

DECLARE @ContactId UNIQUEIDENTIFIER;
DECLARE @Fullname NVARCHAR(300);
DECLARE @NombreBusqueda NVARCHAR(300);

-- Campos del contacto usados para relaciones inversas.
DECLARE @OriginatingLeadId UNIQUEIDENTIFIER;
DECLARE @WitCaso UNIQUEIDENTIFIER;
DECLARE @WitEventoOrigen UNIQUEIDENTIFIER;
DECLARE @WitColegio UNIQUEIDENTIFIER;
DECLARE @WitTramo UNIQUEIDENTIFIER;
DECLARE @WitIngresoBrutoFamiliar UNIQUEIDENTIFIER;

------------------------------------------------------------
-- 1. RECUPERAR CONTACTO UNA SOLA VEZ
------------------------------------------------------------
SELECT TOP 1
    @ContactId = c.contactid,
    @Fullname = c.fullname,
    @OriginatingLeadId = c.originatingleadid,
    @WitCaso = c.wit_caso,
    @WitEventoOrigen = c.wit_eventoorigen,
    @WitColegio = c.wit_colegio,
    @WitTramo = c.wit_tramo,
    @WitIngresoBrutoFamiliar = c.wit_ingresobrutofamiliar
FROM contact c
WHERE
    (@Rut IS NOT NULL AND c.wit_rut = @Rut)
    OR (@Pasaporte IS NOT NULL AND c.wit_pasaporte = @Pasaporte)
ORDER BY c.modifiedon DESC;

SET @NombreBusqueda = COALESCE(NULLIF(@Fullname, ''), NULLIF(@FullnameFallback, ''));

------------------------------------------------------------
-- 2. VALIDACIÓN DE VARIABLES
------------------------------------------------------------
SELECT
    @Rut AS rut_consultado,
    @Pasaporte AS pasaporte_consultado,
    @ContactId AS contactid_encontrado,
    @Fullname AS fullname_desde_contact,
    @NombreBusqueda AS nombre_usado_para_busqueda,
    @OriginatingLeadId AS originatingleadid,
    @WitCaso AS wit_caso,
    @WitEventoOrigen AS wit_eventoorigen,
    @WitColegio AS wit_colegio,
    @WitTramo AS wit_tramo,
    @WitIngresoBrutoFamiliar AS wit_ingresobrutofamiliar;

------------------------------------------------------------
-- 3. MATRIZ OPTIMIZADA
--    FAST: busca por relaciones directas y RUT exacto.
--    cantidad_por_nombre queda en 0 a propósito.
------------------------------------------------------------
SELECT
    'contact' AS entidad_principal,
    'contact' AS entidad_relacionada,
    'contact.contactid / contact.wit_rut' AS campo_relacion_contacto,
    'contact.fullname' AS campo_nombre_registro,
    'contact.wit_rut' AS campo_rut_registro,
    COUNT(CASE WHEN @ContactId IS NOT NULL AND c.contactid = @ContactId THEN 1 END) AS cantidad_por_relacion,
    CAST(0 AS INT) AS cantidad_por_nombre,
    COUNT(CASE WHEN @Rut IS NOT NULL AND c.wit_rut = @Rut THEN 1 END) AS cantidad_por_rut,
    COUNT(*) AS cantidad_total
FROM contact c
WHERE
    (@ContactId IS NOT NULL AND c.contactid = @ContactId)
    OR (@Rut IS NOT NULL AND c.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'lead',
    'lead.customerid / lead.parentcontactid / contact.originatingleadid',
    'lead.fullname / lead.subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM lead l
WHERE
    @ContactId IS NOT NULL
    AND (
        l.customerid = @ContactId
        OR l.parentcontactid = @ContactId
        OR (@OriginatingLeadId IS NOT NULL AND l.leadid = @OriginatingLeadId)
    )

UNION ALL

SELECT
    'contact',
    'incident',
    'incident.customerid / primarycontactid / responsiblecontactid / msa_partnercontactid / contact.wit_caso',
    'incident.title',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM incident i
WHERE
    @ContactId IS NOT NULL
    AND (
        i.customerid = @ContactId
        OR i.primarycontactid = @ContactId
        OR i.responsiblecontactid = @ContactId
        OR i.msa_partnercontactid = @ContactId
        OR (@WitCaso IS NOT NULL AND i.incidentid = @WitCaso)
    )

UNION ALL

SELECT
    'contact',
    'phonecall',
    'phonecall.regardingobjectid',
    'phonecall.subject',
    'phonecall.wit_rut',
    COUNT(CASE WHEN @ContactId IS NOT NULL AND p.regardingobjectid = @ContactId THEN 1 END),
    0,
    COUNT(CASE WHEN @Rut IS NOT NULL AND p.wit_rut = @Rut THEN 1 END),
    COUNT(*)
FROM phonecall p
WHERE
    (@ContactId IS NOT NULL AND p.regardingobjectid = @ContactId)
    OR (@Rut IS NOT NULL AND p.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'email',
    'email.regardingobjectid',
    'email.subject / email.regardingobjectidname',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM email em
WHERE
    @ContactId IS NOT NULL
    AND em.regardingobjectid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_actividadchat',
    'wit_actividadchat.regardingobjectid',
    'wit_actividadchat.subject',
    'wit_actividadchat.wit_rut',
    COUNT(CASE WHEN @ContactId IS NOT NULL AND ac.regardingobjectid = @ContactId THEN 1 END),
    0,
    COUNT(CASE WHEN @Rut IS NOT NULL AND ac.wit_rut = @Rut THEN 1 END),
    COUNT(*)
FROM wit_actividadchat ac
WHERE
    (@ContactId IS NOT NULL AND ac.regardingobjectid = @ContactId)
    OR (@Rut IS NOT NULL AND ac.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'wit_visitaweb',
    'wit_visitaweb.regardingobjectid',
    'wit_visitaweb.subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_visitaweb vw
WHERE
    @ContactId IS NOT NULL
    AND vw.regardingobjectid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_visitapresencial',
    'wit_visitapresencial.regardingobjectid',
    'wit_visitapresencial.regardingobjectidname / subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_visitapresencial vp
WHERE
    @ContactId IS NOT NULL
    AND vp.regardingobjectid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_evento',
    'wit_evento.regardingobjectid / contact.wit_eventoorigen',
    'wit_evento.subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_evento e
WHERE
    @ContactId IS NOT NULL
    AND (
        e.regardingobjectid = @ContactId
        OR (@WitEventoOrigen IS NOT NULL AND e.activityid = @WitEventoOrigen)
    )

UNION ALL

SELECT
    'contact',
    'wit_procesodepostulacion',
    'wit_procesodepostulacion.wit_contacto / wit_referente',
    'wit_procesodepostulacion.wit_name',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_procesodepostulacion pp
WHERE
    @ContactId IS NOT NULL
    AND (
        pp.wit_contacto = @ContactId
        OR pp.wit_referente = @ContactId
    )

UNION ALL

SELECT
    'contact',
    'wit_solicituddeadmisiondirecta',
    'wit_solicituddeadmisiondirecta.wit_postulante',
    'wit_solicituddeadmisiondirecta.wit_name',
    'wit_solicituddeadmisiondirecta.wit_rut',
    COUNT(CASE WHEN @ContactId IS NOT NULL AND sad.wit_postulante = @ContactId THEN 1 END),
    0,
    COUNT(CASE WHEN @Rut IS NOT NULL AND sad.wit_rut = @Rut THEN 1 END),
    COUNT(*)
FROM wit_solicituddeadmisiondirecta sad
WHERE
    (@ContactId IS NOT NULL AND sad.wit_postulante = @ContactId)
    OR (@Rut IS NOT NULL AND sad.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'wit_historicocontactos',
    'wit_historicocontactos.wit_contact / wit_contactorelacionado',
    'wit_historicocontactos.wit_contactname',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_historicocontactos hc
WHERE
    @ContactId IS NOT NULL
    AND (
        hc.wit_contact = @ContactId
        OR hc.wit_contactorelacionado = @ContactId
    )

UNION ALL

SELECT
    'contact',
    'wit_historicodatosdecontactabilidad',
    'wit_historicodatosdecontactabilidad.wit_contacto',
    'wit_historicodatosdecontactabilidad.wit_contactoname / wit_name',
    'wit_historicodatosdecontactabilidad.wit_rut',
    COUNT(CASE WHEN @ContactId IS NOT NULL AND hdc.wit_contacto = @ContactId THEN 1 END),
    0,
    COUNT(CASE WHEN @Rut IS NOT NULL AND hdc.wit_rut = @Rut THEN 1 END),
    COUNT(*)
FROM wit_historicodatosdecontactabilidad hdc
WHERE
    (@ContactId IS NOT NULL AND hdc.wit_contacto = @ContactId)
    OR (@Rut IS NOT NULL AND hdc.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'wit_contactodelsegmentocomercial',
    'wit_contactodelsegmentocomercial.wit_contacto',
    'No aplica',
    'wit_contactodelsegmentocomercial.wit_rut',
    COUNT(CASE WHEN @ContactId IS NOT NULL AND csc.wit_contacto = @ContactId THEN 1 END),
    0,
    COUNT(CASE WHEN @Rut IS NOT NULL AND csc.wit_rut = @Rut THEN 1 END),
    COUNT(*)
FROM wit_contactodelsegmentocomercial csc
WHERE
    (@ContactId IS NOT NULL AND csc.wit_contacto = @ContactId)
    OR (@Rut IS NOT NULL AND csc.wit_rut = @Rut)

UNION ALL

SELECT
    'contact',
    'wit_colegio',
    'contact.wit_colegio / wit_colegio.wit_coordinador / wit_director / wit_encargado / wit_orientador',
    'No aplica',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_colegio co
WHERE
    @ContactId IS NOT NULL
    AND (
        (@WitColegio IS NOT NULL AND co.wit_colegioid = @WitColegio)
        OR co.wit_coordinador = @ContactId
        OR co.wit_director = @ContactId
        OR co.wit_encargado = @ContactId
        OR co.wit_orientador = @ContactId
    )

UNION ALL

SELECT
    'contact',
    'wit_ingresofamiliarbruto',
    'contact.wit_tramo / contact.wit_ingresobrutofamiliar',
    'No aplica',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_ingresofamiliarbruto ifb
WHERE
    @ContactId IS NOT NULL
    AND (
        (@WitTramo IS NOT NULL AND ifb.wit_ingresofamiliarbrutoid = @WitTramo)
        OR (@WitIngresoBrutoFamiliar IS NOT NULL AND ifb.wit_ingresofamiliarbrutoid = @WitIngresoBrutoFamiliar)
    )


UNION ALL

------------------------------------------------------------
-- MSDYN_OCLIVEWORKITEM / CONVERSACIONES OMNICHANNEL
-- FAST: filtra por GUID. No busca por msdyn_customername.
-- Nota: si el endpoint no expone msdyn_ocliveworkitemid,
--       reemplazar por activityid en ambos SELECT internos.
------------------------------------------------------------
SELECT
    'contact' AS entidad_principal,
    'msdyn_ocliveworkitem' AS entidad_relacionada,
    'msdyn_ocliveworkitem.msdyn_customer / socialprofile.customerid vía msdyn_socialprofileid' AS campo_relacion_contacto,
    'msdyn_ocliveworkitem.msdyn_customername' AS campo_nombre_registro,
    'No aplica' AS campo_rut_registro,
    COUNT(*) AS cantidad_por_relacion,
    CAST(0 AS INT) AS cantidad_por_nombre,
    CAST(0 AS INT) AS cantidad_por_rut,
    COUNT(*) AS cantidad_total
FROM (
    SELECT ow.msdyn_ocliveworkitemid
    FROM msdyn_ocliveworkitem ow
    WHERE
        @ContactId IS NOT NULL
        AND ow.msdyn_customer = @ContactId

    UNION

    SELECT ow.msdyn_ocliveworkitemid
    FROM socialprofile sp
    INNER JOIN msdyn_ocliveworkitem ow
        ON ow.msdyn_socialprofileid = sp.socialprofileid
    WHERE
        @ContactId IS NOT NULL
        AND sp.customerid = @ContactId
) oc


UNION ALL

SELECT
    'contact' AS entidad_principal,
    'email_attachment' AS entidad_relacionada,
    'email.activityid -> activitymimeattachment.objectid -> attachment.attachmentid' AS campo_relacion_contacto,
    'email.subject / attachment.filename' AS campo_nombre_registro,
    'No aplica' AS campo_rut_registro,
    COUNT(ama.activitymimeattachmentid) AS cantidad_por_relacion,
    CAST(0 AS INT) AS cantidad_por_nombre,
    CAST(0 AS INT) AS cantidad_por_rut,
    COUNT(ama.activitymimeattachmentid) AS cantidad_total
FROM email em
INNER JOIN activitymimeattachment ama
    ON ama.objectid = em.activityid
INNER JOIN attachment att
    ON att.attachmentid = ama.attachmentid
WHERE
    @ContactId IS NOT NULL
    AND em.regardingobjectid = @ContactId


-- HASTA AQUI

UNION ALL

SELECT
    'contact',
    'socialprofile',
    'socialprofile.customerid',
    'socialprofile.customeridname / profilelink / profilename',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM socialprofile sp
WHERE
    @ContactId IS NOT NULL
    AND sp.customerid = @ContactId

UNION ALL

SELECT
    'contact',
    'msdyn_ocliveworkitem_regardingobjectid',
    'msdyn_ocliveworkitem.regardingobjectid',
    'msdyn_ocliveworkitem.msdyn_customername / subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM msdyn_ocliveworkitem ow
WHERE
    @ContactId IS NOT NULL
    AND ow.regardingobjectid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_historicodatosofertaacademica',
    'wit_historicodatosofertaacademica.wit_contactoid',
    'wit_historicodatosofertaacademica.wit_name / wit_contactoidname',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_historicodatosofertaacademica hdoa
WHERE
    @ContactId IS NOT NULL
    AND hdoa.wit_contactoid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_historicocarreramatriculada',
    'wit_historicocarreramatriculada.wit_contacto',
    'wit_historicocarreramatriculada.wit_name / wit_contactoname',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_historicocarreramatriculada hcm
WHERE
    @ContactId IS NOT NULL
    AND hcm.wit_contacto = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_contactodelacuenta',
    'wit_contactodelacuenta.wit_contacto',
    'wit_contactodelacuenta.wit_contactoname',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_contactodelacuenta cda
WHERE
    @ContactId IS NOT NULL
    AND cda.wit_contacto = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_whatsapp',
    'wit_whatsapp.regardingobjectid',
    'wit_whatsapp.subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_whatsapp wa
WHERE
    @ContactId IS NOT NULL
    AND wa.regardingobjectid = @ContactId

UNION ALL

SELECT
    'contact',
    'wit_sms',
    'wit_sms.regardingobjectid',
    'wit_sms.subject',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM wit_sms sms
WHERE
    @ContactId IS NOT NULL
    AND sms.regardingobjectid = @ContactId


UNION ALL

SELECT
    'contact',
    'activityparty',
    'activityparty.partyid -> activitypointer.activityid',
    'activitypointer.subject / activitypointer.activitytypecode',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM (
    SELECT DISTINCT
        party.activityid
    FROM activityparty party
    WHERE
        @ContactId IS NOT NULL
        AND party.partyid = @ContactId
) act
INNER JOIN activitypointer ap
    ON ap.activityid = act.activityid

UNION ALL

SELECT
    'contact',
    'annotation_contact',
    'annotation.objectid',
    'annotation.subject / annotation.filename',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM annotation an
WHERE
    @ContactId IS NOT NULL
    AND an.objectid = @ContactId

UNION ALL

SELECT
    'contact',
    'customeraddress',
    'customeraddress.parentid',
    'customeraddress.name / line1 / city',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM customeraddress ca
WHERE
    @ContactId IS NOT NULL
    AND ca.parentid = @ContactId



UNION ALL

SELECT
    'contact',
    'annotation_incident',
    'incident.incidentid -> annotation.objectid',
    'incident.title / annotation.subject / annotation.filename',
    'No aplica',
    COUNT(*),
    0,
    0,
    COUNT(*)
FROM incident i
INNER JOIN annotation an
    ON an.objectid = i.incidentid
WHERE
    @ContactId IS NOT NULL
    AND (
        i.customerid = @ContactId
        OR i.primarycontactid = @ContactId
        OR i.responsiblecontactid = @ContactId
        OR i.msa_partnercontactid = @ContactId
        OR (@WitCaso IS NOT NULL AND i.incidentid = @WitCaso)
    )

ORDER BY entidad_relacionada;
