let allData = [];
let currentMode = 'records'; // 'records' or 'matrices'

document.addEventListener('DOMContentLoaded', () => {
    const btnSelectFolder = document.getElementById('btnSelectFolder');
    const searchInput = document.getElementById('searchInput');
    const modal = document.getElementById('detailsModal');
    const btnCloseModal = document.getElementById('btnCloseModal');
    const tabBtns = document.querySelectorAll('.modal-body .tab-btn');
    
    const btnViewRecords = document.getElementById('btnViewRecords');
    const btnViewMatrices = document.getElementById('btnViewMatrices');
    const btnViewAudits = document.getElementById('btnViewAudits');
    const btnViewSingle = document.getElementById('btnViewSingle');
    const btnViewBatch = document.getElementById('btnViewBatch');
    const btnViewMass = document.getElementById('btnViewMass');

    btnSelectFolder.addEventListener('click', loadFolder);
    searchInput.addEventListener('input', (e) => filterTable(e.target.value));
    
    btnViewRecords.addEventListener('click', () => setMode('records'));
    btnViewMatrices.addEventListener('click', () => setMode('matrices'));
    btnViewAudits.addEventListener('click', () => setMode('audits'));
    btnViewSingle.addEventListener('click', () => setMode('single'));
    btnViewBatch.addEventListener('click', () => setMode('batch'));
    btnViewMass.addEventListener('click', () => setMode('mass'));

    btnCloseModal.addEventListener('click', () => {
        modal.style.display = 'none';
    });

    window.addEventListener('click', (e) => {
        if (e.target === modal) {
            modal.style.display = 'none';
        }
    });

    tabBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            tabBtns.forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            
            btn.classList.add('active');
            document.getElementById(btn.dataset.target).classList.add('active');
        });
    });

    // Default UI state
    setMode('single');
});

async function loadFolder() {
    try {
        if (!window.showDirectoryPicker) {
            alert('Tu navegador no soporta la API de File System Access. Por favor usa un navegador moderno como Chrome o Edge.');
            return;
        }

        // El parÃ¡metro 'id' le dice al navegador que recuerde cuÃ¡l fue la Ãºltima carpeta elegida
        const dirHandle = await window.showDirectoryPicker({
            id: 'dynamicsPocBackups',
            mode: 'read'
        });
        
        allData = [];

        for await (const entry of dirHandle.values()) {
            if (entry.kind === 'file' && entry.name.endsWith('.json')) {
                const file = await entry.getFile();
                const text = await file.text();
                try {
                    const json = JSON.parse(text);
                    json._filename = entry.name;
                    allData.push(json);
                } catch (e) {
                    console.error(`Error parsing JSON from ${entry.name}`, e);
                }
            }
        }

        allData.sort((a, b) => new Date(b.retrievedAt) - new Date(a.retrievedAt));

        if (allData.length > 0) {
            document.getElementById('emptyState').style.display = 'none';
            document.getElementById('viewControls').style.display = 'flex';
            document.getElementById('searchBarContainer').style.display = 'block';
            
            // Auto switch to matrix view if there are any matrix files
            const hasMatrices = allData.some(d => d.isMatrix);
            setMode(hasMatrices ? 'matrices' : 'records');
        } else {
            document.getElementById('emptyState').innerHTML = '<p>No se encontraron archivos JSON en la carpeta seleccionada.</p>';
        }

    } catch (error) {
        if (error.name !== 'AbortError') {
            console.error('Error loading folder:', error);
            alert('OcurriÃ³ un error al intentar leer la carpeta.');
        }
    }
}

function setMode(mode) {
    currentMode = mode;
    const btnViewRecords = document.getElementById('btnViewRecords');
    const btnViewMatrices = document.getElementById('btnViewMatrices');
    const btnViewAudits = document.getElementById('btnViewAudits');
    const btnViewSingle = document.getElementById('btnViewSingle');
    const btnViewBatch = document.getElementById('btnViewBatch');
    const btnViewMass = document.getElementById('btnViewMass');
    
    const recordsTable = document.getElementById('recordsTableContainer');
    const matrixTable = document.getElementById('matrixTableContainer');
    const auditTable = document.getElementById('auditTableContainer');
    const singleContainer = document.getElementById('singleExecuteContainer');
    const batchContainer = document.getElementById('batchExecuteContainer');
    const massContainer = document.getElementById('massExecuteContainer');
    const searchBarContainer = document.getElementById('searchBarContainer');
    const emptyState = document.getElementById('emptyState');

    btnViewRecords.classList.remove('active');
    btnViewMatrices.classList.remove('active');
    btnViewAudits.classList.remove('active');
    btnViewSingle.classList.remove('active');
    btnViewBatch.classList.remove('active');
    btnViewMass.classList.remove('active');

    recordsTable.style.display = 'none';
    matrixTable.style.display = 'none';
    auditTable.style.display = 'none';
    singleContainer.style.display = 'none';
    batchContainer.style.display = 'none';
    massContainer.style.display = 'none';
    searchBarContainer.style.display = 'none';
    
    // Detener polling activo si se sale del modo masivo
    if (mode !== 'mass') {
        stopMassPolling();
    }

    // Default visibility for emptyState based on data
    if (allData.length > 0) {
        emptyState.style.display = 'none';
    } else {
        emptyState.style.display = 'block';
    }

    if (mode === 'records') {
        btnViewRecords.classList.add('active');
        recordsTable.style.display = 'block';
        searchBarContainer.style.display = 'block';
    } else if (mode === 'matrices') {
        btnViewMatrices.classList.add('active');
        matrixTable.style.display = 'block';
        searchBarContainer.style.display = 'block';
    } else if (mode === 'audits') {
        btnViewAudits.classList.add('active');
        auditTable.style.display = 'block';
        searchBarContainer.style.display = 'block';
    } else if (mode === 'single') {
        btnViewSingle.classList.add('active');
        singleContainer.style.display = 'block';
        emptyState.style.display = 'none';
    } else if (mode === 'batch') {
        btnViewBatch.classList.add('active');
        batchContainer.style.display = 'block';
        emptyState.style.display = 'none';
    } else if (mode === 'mass') {
        btnViewMass.classList.add('active');
        massContainer.style.display = 'block';
        emptyState.style.display = 'none';
    }
    
    // Clear search and render
    document.getElementById('searchInput').value = '';
    filterTable('');
}

// --------------------------------------------------------
// EXECUTION LOGIC: API Integration
// --------------------------------------------------------

document.getElementById('btnExecuteSingle').addEventListener('click', async () => {
    const rut = document.getElementById('singleRutInput').value.trim();
    const mode = document.getElementById('singleModeSelect').value;
    
    if (!rut) {
        alert("Por favor ingrese un RUT válido.");
        return;
    }

    if (mode.includes('Eliminar')) {
        const confirmWord = prompt("Atención: Operación Destructiva.\nEscriba la palabra ELIMINAR para proceder:");
        if (confirmWord !== "ELIMINAR") {
            alert("Operación cancelada.");
            return;
        }
    }

    const btn = document.getElementById('btnExecuteSingle');
    const resBox = document.getElementById('singleResultBox');
    btn.disabled = true;
    btn.innerText = "EJECUTANDO...";
    resBox.style.display = 'none';

    try {
        const response = await fetch('/api/execute-single', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ rut, mode })
        });
        const result = await response.json();
        
        resBox.style.display = 'block';
        const hasError = result.results[0].status === 'Error';
        const isConsult = mode === 'Consultar';
        const infoMessage = isConsult 
            ? 'Por favor actualice la carpeta y revise la pestaña de <strong>Consultas de Matriz</strong> para ver el reporte detallado.'
            : 'Por favor actualice la carpeta y revise la pestaña de <strong>Historial de Auditoría</strong> para ver el resultado de la eliminación.';
            
        resBox.innerHTML = `
            <h3 style="color:${hasError ? '#f87171' : '#4ade80'}; margin-top:0;">Operación Completada</h3>
            <p><strong>Ejecución ID:</strong> ${result.executionId}</p>
            <p><strong>Estado:</strong> ${result.results[0].status}</p>
            ${hasError && result.results[0].error ? `<p style="color: #f87171; font-weight: bold; font-family: monospace;">Error: ${result.results[0].error}</p>` : ''}
            <p style="font-size: 0.9em; margin-top: 10px; color: #94a3b8;">${infoMessage}</p>
        `;
    } catch (e) {
        alert("Error de comunicación con el servidor local: " + e.message);
    } finally {
        btn.disabled = false;
        btn.innerText = "EJECUTAR OPERACIÓN";
    }
});

let parsedBatchRuts = [];

document.getElementById('batchFileInput').addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (evt) => {
        const text = evt.target.result;
        // Split by new line, remove empty lines and trim
        parsedBatchRuts = text.split(/\r?\n/).map(r => r.trim()).filter(r => r.length > 5);
        
        document.getElementById('batchPreviewBox').style.display = 'block';
        document.getElementById('batchCount').innerText = parsedBatchRuts.length;
        
        const btnExec = document.getElementById('btnExecuteBatch');
        if (parsedBatchRuts.length > 0) {
            btnExec.style.display = 'block';
        } else {
            btnExec.style.display = 'none';
        }
    };
    reader.readAsText(file);
});

document.getElementById('btnExecuteBatch').addEventListener('click', async () => {
    const mode = document.getElementById('batchModeSelect').value;
    
    if (parsedBatchRuts.length === 0) return;

    if (mode.includes('Eliminar')) {
        const confirmWord = prompt(`Atención: Se procesarán ${parsedBatchRuts.length} RUTs de forma destructiva.\nEscriba la palabra ELIMINAR para proceder con el lote:`);
        if (confirmWord !== "ELIMINAR") {
            alert("Operación cancelada.");
            return;
        }
    }

    const btn = document.getElementById('btnExecuteBatch');
    const resBox = document.getElementById('batchResultBox');
    btn.disabled = true;
    btn.innerText = "PROCESANDO LOTE COMPLETO...";
    resBox.style.display = 'none';

    try {
        const response = await fetch('/api/execute-batch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ruts: parsedBatchRuts, mode })
        });
        const result = await response.json();
        
        resBox.style.display = 'block';
        let itemsHtml = result.results.map(r => `<li>${r.rut} - ${r.status} ${r.error ? `(<span style="color:#f87171">${r.error}</span>)` : ''}</li>`).join('');
        
        const isConsult = mode === 'Consultar';
        const infoMessage = isConsult 
            ? 'Por favor actualice la carpeta y revise la pestaña de <strong>Consultas de Matriz</strong> para ver el reporte detallado de cada RUT.'
            : 'Por favor actualice la carpeta y revise la pestaña de <strong>Historial de Auditoría</strong> para ver el resultado JSON de las eliminaciones.';
            
        resBox.innerHTML = `
            <h3 style="color:#4ade80; margin-top:0;">Lote Procesado</h3>
            <p><strong>Ejecución ID:</strong> ${result.executionId}</p>
            <div style="max-height: 200px; overflow-y: auto; background: rgba(0,0,0,0.5); padding: 10px; border-radius: 4px; font-family: monospace;">
                <ul style="margin: 0; padding-left: 20px;">
                    ${itemsHtml}
                </ul>
            </div>
            <p style="font-size: 0.9em; margin-top: 10px; color: #94a3b8;">${infoMessage}</p>
        `;
    } catch (e) {
        alert("Error de comunicación con el servidor local: " + e.message);
    } finally {
        btn.disabled = false;
        btn.innerText = "EJECUTAR LOTE COMPLETO";
    }
});

// --------------------------------------------------------
// FILTERING AND RENDERING
// --------------------------------------------------------

function filterTable(searchTerm) {
    const term = searchTerm.toLowerCase();
    
    const filteredData = allData.filter(item => {
        const isAuditLog = !!item.isAuditLog;
        const isMatrixQuery = !!item.isMatrix;
        
        // Filter by current tab
        if (currentMode === 'audits' && !isAuditLog) return false;
        if (currentMode === 'matrices' && (isAuditLog || !isMatrixQuery)) return false;
        if (currentMode === 'records' && (isAuditLog || isMatrixQuery)) return false;

        if (!term) return true;

        if (isAuditLog) {
            return (item.um_rut || '').toLowerCase().includes(term) ||
                   (item.um_operacion || '').toLowerCase().includes(term) ||
                   (item.um_resultado || '').toLowerCase().includes(term) ||
                   (item.um_usuario || '').toLowerCase().includes(term);
        } else if (isMatrixQuery) {
            return (item.rut || '').toLowerCase().includes(term) ||
                   (item.executionId || '').toLowerCase().includes(term) ||
                   JSON.stringify(item.matrix).toLowerCase().includes(term);
        } else {
            // Respaldos (isMatrixBackup o individuales)
            const rutToSearch = item.rut || (item.attributes && item.attributes.um_rut) || '';
            return rutToSearch.toLowerCase().includes(term) ||
                   (item.recordId || '').toLowerCase().includes(term) || 
                   (item.entityLogicalName || '').toLowerCase().includes(term) ||
                   (item.executionId || '').toLowerCase().includes(term) ||
                   JSON.stringify(item).toLowerCase().includes(term);
        }
    });
    
    if (currentMode === 'records') renderRecordsTable(filteredData);
    else if (currentMode === 'matrices') renderMatrixTable(filteredData);
    else if (currentMode === 'audits') renderAuditTable(filteredData);
}

function renderRecordsTable(data) {
    const tbody = document.getElementById('tableBody');
    tbody.innerHTML = '';

    data.forEach((item, index) => {
        const dateObj = new Date(item.retrievedAt);
        
        let rutVal = '-';
        let entityName = item.entityLogicalName;
        let recordId = item.recordId;

        // Si es un respaldo masivo de eliminaciÃ³n de matriz
        if (item.isMatrixBackup) {
            rutVal = item.rut;
            entityName = 'MATRIZ DE BORRADO (' + item.totalRecords + ' registros)';
            recordId = 'BACKUP-MASIVO';
        } else if (item.attributes && item.attributes.um_rut) {
            rutVal = item.attributes.um_rut;
        }
        
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${dateObj.toLocaleString()}</td>
            <td><span style="background: rgba(59, 130, 246, 0.2); color: #93c5fd; padding: 0.2rem 0.5rem; border-radius: 4px; font-size: 0.8rem;">${entityName || '-'}</span></td>
            <td style="font-family: monospace; font-size: 0.85rem;">${recordId || '-'}</td>
            <td><strong>${rutVal}</strong></td>
            <td style="font-family: monospace; font-size: 0.8rem; color: #94a3b8;">${item.executionId}</td>
            <td>
                <button class="view-btn" onclick="showRecordDetails('${item._filename}')">Ver Detalles</button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

function renderMatrixTable(data) {
    const tbody = document.getElementById('matrixTableBody');
    tbody.innerHTML = '';

    data.forEach((item) => {
        const dateObj = new Date(item.retrievedAt);
        const rows = item.matrix || [];
        
        rows.forEach((row, rowIndex) => {
            const tr = document.createElement('tr');
            
            // Only show date and rut on the first row for this matrix
            if (rowIndex === 0) {
                const modeTag = item.operationMode ? `<br><span style="background: rgba(59, 130, 246, 0.2); color: #93c5fd; padding: 0.1rem 0.4rem; border-radius: 4px; font-size: 0.75rem;">Modo: ${item.operationMode}</span>` : '';
                const phaseTag = item.phase ? `<br><span style="color: #cbd5e1; font-size: 0.8rem; font-style: italic;">${item.phase}</span>` : '';

                tr.innerHTML = `
                    <td rowspan="${rows.length}">${dateObj.toLocaleString()}<br><small style="color: #94a3b8;">${item.executionId}</small>${modeTag}${phaseTag}</td>
                    <td rowspan="${rows.length}"><strong>${item.rut}</strong><br><small style="color: #94a3b8;">${item.contactId}</small></td>
                    <td><span style="color: #93c5fd;">${row.EntidadRelacionada}</span></td>
                    <td style="font-family: monospace; font-size: 0.85rem;">${row.CampoRelacion}</td>
                    <td><strong>${row.CantidadTotal}</strong></td>
                    <td rowspan="${rows.length}">
                        <button class="view-btn" onclick="showRecordDetails('${item._filename}')">Ver JSON</button>
                        <button class="primary-btn" style="margin-top: 0.5rem; width: 100%; justify-content: center; font-size: 0.8rem; padding: 0.4rem;" onclick="printMatrix('${item._filename}')">Generar Reporte PDF</button>
                    </td>
                `;
            } else {
                tr.innerHTML = `
                    <td><span style="color: #93c5fd;">${row.EntidadRelacionada}</span></td>
                    <td style="font-family: monospace; font-size: 0.85rem;">${row.CampoRelacion}</td>
                    <td><strong>${row.CantidadTotal}</strong></td>
                `;
            }
            tbody.appendChild(tr);
        });
    });
}

function renderAuditTable(data) {
    const tbody = document.getElementById('auditTableBody');
    tbody.innerHTML = '';

    data.forEach(item => {
        const dateObj = new Date(item.um_fechaejecucion);
        const hasDetails = !!item.um_detalles;
        
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${dateObj.toLocaleString()}</td>
            <td>${item.um_usuario}</td>
            <td><strong>${item.um_rut}</strong></td>
            <td><span style="background: rgba(59, 130, 246, 0.2); color: #93c5fd; padding: 0.2rem 0.5rem; border-radius: 4px; font-size: 0.8rem;">${item.um_operacion}</span></td>
            <td><span style="color: ${item.um_resultado.includes('Success') ? '#4ade80' : '#f87171'}; font-weight: bold;">${item.um_resultado}</span></td>
            <td>
                <span style="font-family: monospace; font-size: 0.75rem; color: #94a3b8; display: block; margin-bottom: 0.3rem;">ID: ${item.um_logeliminacionlegalid}</span>
                ${hasDetails ? `<small style="color: #cbd5e1;">${item.um_detalles}</small>` : '<small style="color: #64748b;">Sin detalles</small>'}
                <button class="view-btn" style="margin-top: 0.4rem;" onclick="showRecordDetails('${item._filename}')">Ver JSON Completo</button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

window.showRecordDetails = function(filename) {
    const data = allData.find(d => d._filename === filename);
    if (!data) return;

    const modal = document.getElementById('detailsModal');
    
    if (data.isMatrix) {
        document.getElementById('formattedValuesTab').innerHTML = '<p style="padding:1rem;">(N/A para matrices)</p>';
        document.getElementById('rawAttributesTab').innerHTML = '<p style="padding:1rem;">(N/A para matrices)</p>';
    } else if (data.isAuditLog) {
        document.getElementById('formattedValuesTab').innerHTML = renderKeyValueList(data);
        document.getElementById('rawAttributesTab').innerHTML = '<p style="padding:1rem;">(N/A para logs de auditorÃ­a)</p>';
    } else {
        document.getElementById('formattedValuesTab').innerHTML = renderKeyValueList(data.formattedValues);
        document.getElementById('rawAttributesTab').innerHTML = renderKeyValueList(data.attributes);
    }

    const jsonViewer = document.getElementById('jsonViewer');
    jsonViewer.textContent = JSON.stringify(data, null, 2);

    modal.style.display = 'flex';
};

function renderKeyValueList(obj) {
    if (!obj || Object.keys(obj).length === 0) {
        return '<p style="color: #94a3b8; padding: 1rem 0;">No hay datos disponibles.</p>';
    }

    let html = '';
    for (const [key, value] of Object.entries(obj)) {
        let displayValue = value;
        if (value === null) displayValue = '<em>null</em>';
        else if (typeof value === 'object') displayValue = JSON.stringify(value);
        
        html += `
            <div class="key-value-row">
                <div class="key">${key}</div>
                <div class="value">${displayValue}</div>
            </div>
        `;
    }
    return html;
}

window.printMatrix = function(filename) {
    const data = allData.find(d => d._filename === filename);
    if (!data) return;

    const dateObj = new Date(data.retrievedAt);
    
    let html = `
        <html>
        <head>
            <title>Reporte de Estructura - RUT ${data.rut}</title>
            <style>
                body { font-family: 'Segoe UI', Arial, sans-serif; color: #333; padding: 40px; line-height: 1.6; }
                h1 { color: #0f172a; border-bottom: 2px solid #cbd5e1; padding-bottom: 10px; margin-bottom: 30px; }
                .info-box { background: #f8fafc; padding: 20px; border-radius: 8px; margin-bottom: 30px; border: 1px solid #e2e8f0; }
                .info-box p { margin: 5px 0; }
                table { width: 100%; border-collapse: collapse; margin-top: 20px; }
                th, td { border: 1px solid #cbd5e1; padding: 12px; text-align: left; }
                th { background-color: #f1f5f9; font-weight: bold; color: #334155; }
                .total { font-weight: bold; background-color: #e2e8f0; }
                .print-btn { display: block; margin-bottom: 20px; background: #3b82f6; color: white; padding: 10px 20px; border: none; border-radius: 5px; cursor: pointer; font-size: 16px; }
                @media print {
                    .print-btn { display: none; }
                    body { padding: 0; }
                }
            </style>
        </head>
        <body>
            <button class="print-btn" onclick="window.print()">ðŸ–¨ï¸ Guardar como PDF / Imprimir</button>
            
            <h1>Reporte de Estructura de Contacto</h1>
            
            <div class="info-box">
                <p><strong>RUT Consultado:</strong> ${data.rut}</p>
                <p><strong>Nombre Contacto:</strong> ${data.fullname || 'No encontrado'}</p>
                <p><strong>ID Dataverse:</strong> ${data.contactId}</p>
                <p><strong>Fecha de Consulta:</strong> ${dateObj.toLocaleString()}</p>
                <p><strong>ID de EjecuciÃ³n:</strong> ${data.executionId}</p>
                ${data.operationMode ? `<p><strong>Modo OperaciÃ³n:</strong> ${data.operationMode}</p>` : ''}
                ${data.phase ? `<p><strong>Fase/Estado:</strong> ${data.phase}</p>` : ''}
            </div>
            
            <table>
                <thead>
                    <tr>
                        <th>Entidad Relacionada</th>
                        <th>Campo de VÃ­nculo</th>
                        <th>Cantidad de Registros</th>
                    </tr>
                </thead>
                <tbody>
    `;

    let totalGlobal = 0;
    data.matrix.forEach(row => {
        totalGlobal += row.CantidadTotal;
        html += `
            <tr>
                <td>${row.EntidadRelacionada}</td>
                <td style="font-family: monospace; font-size: 0.9em;">${row.CampoRelacion}</td>
                <td>${row.CantidadTotal}</td>
            </tr>
        `;
    });

    html += `
                <tr class="total">
                    <td colspan="2" style="text-align: right;">TOTAL DE REGISTROS ASOCIADOS:</td>
                    <td>${totalGlobal}</td>
                </tr>
                </tbody>
            </table>
            
            <p style="margin-top: 50px; font-size: 0.85em; color: #94a3b8; text-align: center;">
                Reporte generado automÃ¡ticamente por UMayor Dynamics Delete POC<br>
                Ambiente: ${data.environmentUrl}
            </p>
        </body>
        </html>
    `;

    const printWin = window.open('', '_blank');
    printWin.document.open();
    printWin.document.write(html);
    printWin.document.close();
}

// ========================================================
// CONSOLE MASIVA (DURABLE) LOGIC
// ========================================================

let massPollInterval = null;
let currentMassExecutionId = null;
let currentMassDetails = [];

// Enlazar eventos de la Consola Masiva
document.addEventListener('DOMContentLoaded', () => {
    const btnCreateMassLote = document.getElementById('btnCreateMassLote');
    const massFilterSelect = document.getElementById('massFilterSelect');
    const btnExportMassCsv = document.getElementById('btnExportMassCsv');
    const massTreatmentSelect = document.getElementById('massTreatmentSelect');

    if (btnCreateMassLote) {
        btnCreateMassLote.addEventListener('click', handleCreateMassLote);
    }
    if (massTreatmentSelect) {
        massTreatmentSelect.addEventListener('change', updateMassTreatmentUi);
        updateMassTreatmentUi();
    }
    if (massFilterSelect) {
        massFilterSelect.addEventListener('change', () => {
            pollMassStatus();
        });
    }
    if (btnExportMassCsv) {
        btnExportMassCsv.addEventListener('click', handleExportMassCsv);
    }
});

function stopMassPolling() {
    if (massPollInterval) {
        clearInterval(massPollInterval);
        massPollInterval = null;
    }
}

function updateMassTreatmentUi() {
    const treatmentSelect = document.getElementById('massTreatmentSelect');
    const confirmBox = document.getElementById('massDeleteConfirmBox');
    const confirmInput = document.getElementById('massDeleteConfirmInput');
    const btnCreateMassLote = document.getElementById('btnCreateMassLote');
    if (!treatmentSelect) return;

    const tratamiento = treatmentSelect.value;
    const isDelete = tratamiento !== 'Consultar';

    if (confirmBox) {
        confirmBox.style.display = isDelete ? 'block' : 'none';
    }
    if (!isDelete && confirmInput) {
        confirmInput.value = '';
    }
    if (btnCreateMassLote) {
        btnCreateMassLote.innerText = isDelete
            ? `CREAR E INICIAR LOTE - ${tratamiento}`
            : 'CREAR E INICIAR LOTE - CONSULTAR';
    }
}

async function handleCreateMassLote() {
    const input = document.getElementById('massIdentifiersInput').value;
    const fileInput = document.getElementById('massFileInput');
    const selectedFile = fileInput && fileInput.files && fileInput.files.length > 0 ? fileInput.files[0] : null;
    const tratamiento = document.getElementById('massTreatmentSelect').value;
    const confirmationText = document.getElementById('massDeleteConfirmInput')?.value || '';
    const motivo = document.getElementById('massMotiveInput').value;
    const isDelete = tratamiento !== 'Consultar';

    if (!selectedFile && !input.trim()) {
        alert("Por favor adjunte un archivo CSV/TXT o ingrese al menos un RUT o Pasaporte.");
        return;
    }

    if (!motivo.trim()) {
        alert("Por favor ingrese la justificación / motivo del proceso masivo.");
        return;
    }

    // Dividir RUTs/Pasaportes por línea o coma
    if (isDelete && confirmationText.trim().toUpperCase() !== 'ELIMINAR') {
        alert("Para iniciar una eliminaciÃ³n masiva debe escribir ELIMINAR en la confirmaciÃ³n.");
        return;
    }

    if (isDelete && !confirm(`Va a iniciar un lote masivo con tratamiento ${tratamiento}. Esta acciÃ³n ejecutarÃ¡ eliminaciÃ³n sobre los registros indicados. Â¿Desea continuar?`)) {
        return;
    }

    const identificadores = input.split(/[\n,]+/)
        .map(i => i.trim())
        .filter(i => i.length > 0);

    const btnCreateMassLote = document.getElementById('btnCreateMassLote');
    btnCreateMassLote.disabled = true;
    btnCreateMassLote.innerText = "CREANDO LOTE...";

    try {
        let createRes;
        if (selectedFile) {
            const formData = new FormData();
            formData.append('file', selectedFile);
            formData.append('tratamiento', tratamiento);
            formData.append('motivo', motivo);
            formData.append('confirmationText', confirmationText);

            createRes = await fetch('/api/mass/upload', {
                method: 'POST',
                body: formData
            });
        } else {
            // 1. Crear cabecera y detalles en Dataverse
            createRes = await fetch('/api/mass/create', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ identificadores, tratamiento, motivo, confirmationText })
            });
        }

        if (!createRes.ok) {
            const err = await createRes.json();
            throw new Error(err.error || "Error al crear el lote en Dataverse.");
        }

        const createData = await createRes.json();
        currentMassExecutionId = createData.executionId;

        // Mostrar el dashboard y resetear valores
        document.getElementById('massDashboardBox').style.display = 'block';
        document.getElementById('massLoteIdBadge').innerText = `Lote: ${currentMassExecutionId}`;
        document.getElementById('lblMassTratamiento').innerText = tratamiento;
        
        // Ocultar formulario de entrada
        document.getElementById('massInputForm').style.display = 'none';

        // 2. Iniciar el procesamiento masivo en cola de Azure
        const startRes = await fetch(`/api/mass/start/${currentMassExecutionId}`, {
            method: 'POST'
        });

        if (!startRes.ok) {
            throw new Error("Lote creado pero no se pudo encolar el procesamiento en segundo plano.");
        }

        // 3. Comenzar polling
        startMassPolling();

    } catch (e) {
        alert("Error: " + e.message);
        btnCreateMassLote.disabled = false;
        btnCreateMassLote.innerText = "CREAR E INICIAR LOTE";
    }
}

function startMassPolling() {
    stopMassPolling();
    
    // Ejecutar inmediatamente
    pollMassStatus();

    // Polling cada 3 segundos
    massPollInterval = setInterval(pollMassStatus, 3000);
}

async function pollMassStatus() {
    if (!currentMassExecutionId) return;

    try {
        // Consultar Cabecera
        const statusRes = await fetch(`/api/mass/status/${currentMassExecutionId}`);
        if (!statusRes.ok) return;
        const statusData = await statusRes.json();

        // Consultar Detalles
        const filterVal = document.getElementById('massFilterSelect').value;
        const detailStatusQuery = filterVal ? `&status=${encodeURIComponent(filterVal)}` : '';
        const detailsRes = await fetch(`/api/mass/details/${currentMassExecutionId}?page=1&pageSize=200${detailStatusQuery}`);
        if (!detailsRes.ok) return;
        const detailsData = await detailsRes.json();
        currentMassDetails = Array.isArray(detailsData) ? detailsData : (detailsData.items || []);

        // Actualizar datos generales
        document.getElementById('lblMassTratamiento').innerText = statusData.tratamiento;
        
        const stateEl = document.getElementById('lblMassEstado');
        stateEl.innerText = statusData.estado;
        if (statusData.estado.includes("Completado")) {
            stateEl.style.color = "#4ade80";
        } else if (statusData.estado.includes("Error")) {
            stateEl.style.color = "#fb7185";
        } else {
            stateEl.style.color = "#60a5fa";
        }

        document.getElementById('lblMassInicio').innerText = statusData.inicio || "Pendiente";
        document.getElementById('lblMassTermino').innerText = statusData.termino || "En Proceso...";
        document.getElementById('lblMassUsuario').innerText = statusData.solicitadoPor;

        // Actualizar métricas
        document.getElementById('metricTotal').innerText = statusData.totalRegistros;
        document.getElementById('metricProcesados').innerText = statusData.procesados;
        document.getElementById('metricExitosos').innerText = statusData.exitosos;
        document.getElementById('metricNoEncontrados').innerText = statusData.noEncontrados;
        document.getElementById('metricErrores').innerText = statusData.errores;
        document.getElementById('metricInvalidos').innerText = statusData.invalidos;
        document.getElementById('metricRequiereConciliacion').innerText = statusData.requiereConciliacion;

        // Progreso bar
        const progressPercentage = statusData.totalRegistros > 0 
            ? Math.round(((statusData.procesados + statusData.invalidos) / statusData.totalRegistros) * 100) 
            : 0;
        
        document.getElementById('massProgressBar').style.width = `${progressPercentage}%`;
        document.getElementById('massProgressBarText').innerText = `${progressPercentage}%`;

        // Renderizar tabla
        renderMassDetailsTable(currentMassDetails, '');

        // Si ya terminó, detener polling
        if (statusData.estado.includes("Completado") || statusData.estado === "Error") {
            stopMassPolling();
        }

    } catch (e) {
        console.error("Error en polling masivo:", e);
    }
}

function renderMassDetailsTable(details, filterStatus) {
    const tbody = document.getElementById('massDetailsTableBody');
    tbody.innerHTML = '';

    const filtered = filterStatus 
        ? details.filter(d => d.estado.toLowerCase() === filterStatus.toLowerCase()) 
        : details;

    if (filtered.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center; padding:1.5rem; color:#94a3b8;">No se encontraron registros con este filtro.</td></tr>`;
        return;
    }

    filtered.forEach(item => {
        let msgHtml = "";
        
        if (item.estado === "Invalido" || item.estado === "Error" || item.estado === "RequiereConciliacion") {
            msgHtml = `<span style="color:#fb7185;">${item.errorMessage || "Error en operación"}</span>`;
        } else if (item.backupReference) {
            msgHtml = `<a href="/api/mass/backup/download?blobReference=${encodeURIComponent(item.backupReference)}" target="_blank" style="color:#60a5fa; text-decoration:underline;">Descargar Respaldo JSON (${item.backupSize} bytes)</a>`;
        } else if (item.resultado) {
            try {
                const parsed = JSON.parse(item.resultado);
                msgHtml = `<span style="color:#cbd5e1;">${parsed.mensaje || "Operación terminada"}</span>`;
            } catch {
                msgHtml = `<span style="color:#cbd5e1;">Operación terminada</span>`;
            }
        } else {
            msgHtml = `<span style="color:#94a3b8;">Sin mensaje de salida</span>`;
        }

        // Color badge por estado
        let color = "#94a3b8";
        if (item.estado === "Eliminado" || item.estado === "Consultado") color = "#4ade80";
        else if (item.estado === "EnProceso") color = "#3b82f6";
        else if (item.estado === "Invalido" || item.estado === "Error") color = "#fb7185";
        else if (item.estado === "NoEncontrado") color = "#f59e0b";
        else if (item.estado === "RequiereConciliacion") color = "#a78bfa";

        tbody.innerHTML += `
            <tr style="border-bottom: 1px solid rgba(255,255,255,0.05);">
                <td style="padding: 0.75rem; font-family: monospace;">${item.identificador}</td>
                <td style="padding: 0.75rem;">${item.tipoIdentificador}</td>
                <td style="padding: 0.75rem;"><span style="color:${color}; font-weight:bold;">${item.estado}</span></td>
                <td style="padding: 0.75rem;">${msgHtml}</td>
            </tr>
        `;
    });
}

function handleExportMassCsv() {
    if (currentMassDetails.length === 0) {
        alert("No hay registros en el detalle para exportar.");
        return;
    }

    let csvContent = "\ufeff"; // BOM para asegurar codificación UTF-8
    csvContent += "Identificador,Tipo,Estado,Error,Referencia Backup,Hash Backup\n";

    currentMassDetails.forEach(item => {
        const errorMsg = (item.errorMessage || "").replace(/"/g, '""');
        const backupRef = (item.backupReference || "").replace(/"/g, '""');
        const backupHash = (item.backupHash || "").replace(/"/g, '""');
        
        csvContent += `"${item.identificador}","${item.tipoIdentificador}","${item.estado}","${errorMsg}","${backupRef}","${backupHash}"\n`;
    });

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `lote_masivo_${currentMassExecutionId || 'export'}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

