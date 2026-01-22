// SignalR connection
let connection = null;
let services = [];
let filteredServices = [];
let treeView = false;
let groupTree = {};

// Initialize SignalR connection
function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/healthhub")
        .withAutomaticReconnect()
        .build();

    // Connection status handlers
    connection.onreconnecting(() => {
        updateConnectionStatus('reconnecting', 'Reconnecting...');
    });

    connection.onreconnected(() => {
        updateConnectionStatus('connected', 'Connected');
        // Request all services after reconnection
        fetchServices();
    });

    connection.onclose(() => {
        updateConnectionStatus('disconnected', 'Disconnected');
    });

    // Status update handler
    connection.on("StatusUpdate", (service) => {
        updateService(service);
        updateSummary();
    });

    // All statuses handler
    connection.on("AllStatuses", (data) => {
        services = data.services || [];
        filteredServices = [...services];
        renderServices();
        updateSummary();
    });

    // Summary update handler
    connection.on("Summary", (summary) => {
        updateSummaryDisplay(summary);
    });

    // Start connection
    connection.start()
        .then(() => {
            updateConnectionStatus('connected', 'Connected');
            fetchServices();
        })
        .catch(err => {
            console.error("SignalR connection error:", err);
            updateConnectionStatus('disconnected', 'Connection Error');
        });
}

// Update connection status UI
function updateConnectionStatus(status, text) {
    const statusDot = document.getElementById('statusDot');
    const statusText = document.getElementById('statusText');
    
    statusDot.className = 'status-dot ' + status;
    statusText.textContent = text;
}

// Fetch all services from API
async function fetchServices() {
    try {
        const response = await fetch('/api/health/services');
        const data = await response.json();
        services = data.services || [];
        filteredServices = [...services];
        renderServices();
        updateSummary();
    } catch (error) {
        console.error("Failed to fetch services:", error);
    }
}

// Update single service
function updateService(updatedService) {
    const index = services.findIndex(s => s.path === updatedService.path);
    if (index !== -1) {
        services[index] = updatedService;
    } else {
        services.push(updatedService);
    }
    
    // Update filtered services if this service matches current filters
    const searchValue = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusFilter').value;
    const protocolFilter = document.getElementById('protocolFilter').value;
    
    if (matchesFilters(updatedService, searchValue, statusFilter, protocolFilter)) {
        const filteredIndex = filteredServices.findIndex(s => s.path === updatedService.path);
        if (filteredIndex !== -1) {
            filteredServices[filteredIndex] = updatedService;
        } else {
            filteredServices.push(updatedService);
        }
    } else {
        // Remove from filtered if doesn't match
        filteredServices = filteredServices.filter(s => s.path !== updatedService.path);
    }
    
    renderServices();
}

// Check if service matches filters
function matchesFilters(service, search, statusFilter, protocolFilter) {
    // Search filter
    if (search) {
        const searchLower = search.toLowerCase();
        if (!service.name?.toLowerCase().includes(searchLower) &&
            !service.protocol?.toLowerCase().includes(searchLower) &&
            !service.path?.toLowerCase().includes(searchLower)) {
            return false;
        }
    }
    
    // Status filter
    if (statusFilter !== 'all' && service.status !== statusFilter) {
        return false;
    }
    
    // Protocol filter
    if (protocolFilter !== 'all' && service.protocol?.toLowerCase() !== protocolFilter.toLowerCase()) {
        return false;
    }
    
    return true;
}

// Apply filters
function applyFilters() {
    const searchValue = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusFilter').value;
    const protocolFilter = document.getElementById('protocolFilter').value;
    
    filteredServices = services.filter(service => 
        matchesFilters(service, searchValue, statusFilter, protocolFilter)
    );
    
    renderServices();
}

// Build tree structure from services
function buildGroupTree(servicesList) {
    const tree = { _groups: {}, _services: [] };
    
    servicesList.forEach(service => {
        const parts = service.serviceId.split('.');
        
        if (parts.length === 1) {
            // Top-level service (no groups)
            tree._services.push(service);
        } else {
            // Service belongs to groups
            let current = tree._groups;
            
            // Build group path (all parts except last)
            for (let i = 0; i < parts.length - 1; i++) {
                const part = parts[i];
                if (!current[part]) {
                    current[part] = { _groups: {}, _services: [] };
                }
                current = current[part]._groups;
            }
            
            // Add service to the deepest group
            let targetGroup = tree._groups;
            for (let i = 0; i < parts.length - 1; i++) {
                targetGroup = targetGroup[parts[i]];
            }
            targetGroup._services.push(service);
        }
    });
    
    return tree;
}

// Render tree view
function renderTreeView() {
    const container = document.getElementById('treeContainer');
    if (!container) return;
    
    if (filteredServices.length === 0) {
        container.innerHTML = '<div class="empty" style="padding: 40px; text-align: center; color: #64748b;">No services matching filters</div>';
        return;
    }
    
    groupTree = buildGroupTree(filteredServices);
    container.innerHTML = renderTreeGroup(groupTree, '');
}

function renderTreeGroup(group, prefix) {
    let html = '';
    
    // Render services in this group
    if (group._services && group._services.length > 0) {
        group._services.forEach(service => {
            html += renderTreeService(service);
        });
    }
    
    // Render nested groups
    if (group._groups) {
        Object.keys(group._groups).forEach(key => {
            const groupData = group._groups[key];
            const groupId = prefix ? `${prefix}.${key}` : key;
            const groupName = key.charAt(0).toUpperCase() + key.slice(1).replace(/-/g, ' ');
            
            // Calculate stats for group
            const allServices = getAllServicesInGroup(groupData);
            const alive = allServices.filter(s => s.status === 'alive').length;
            const dead = allServices.filter(s => s.status === 'dead').length;
            const total = allServices.length;
            
            html += `
                <div class="tree-group" data-group-id="${groupId}">
                    <div class="tree-group-header" onclick="toggleGroup('${groupId}')">
                        <span class="tree-toggle">▼</span>
                        <span class="tree-group-name">${groupName}</span>
                        <div class="tree-group-stats">
                            <span class="alive">${alive} alive</span>
                            <span class="dead">${dead} dead</span>
                            <span>${total} total</span>
                        </div>
                    </div>
                    <div class="tree-group-children" id="group-${groupId}">
                        ${renderTreeGroup(groupData, groupId)}
                    </div>
                </div>
            `;
        });
    }
    
    return html;
}

function getAllServicesInGroup(groupData) {
    let services = [...(groupData._services || [])];
    if (groupData._groups) {
        Object.keys(groupData._groups).forEach(key => {
            services = services.concat(getAllServicesInGroup(groupData._groups[key]));
        });
    }
    return services;
}

function renderTreeService(service) {
    const statusBadge = `<span class="status-badge ${service.status}">${getStatusText(service.status)}</span>`;
    const protocolBadge = `<span class="protocol-badge">${service.protocol?.toUpperCase() || 'N/A'}</span>`;
    const duration = service.duration ? `${Math.round(service.duration)} ms` : 'N/A';
    const lastCheck = service.lastCheck ? formatDate(new Date(service.lastCheck)) : 'N/A';
    const error = service.error ? `<span class="error-text" title="${service.error}">${service.error}</span>` : '-';
    
    // Format target
    const hostPort = service.host && service.port ? `${service.host}:${service.port}` : (service.host || '-');
    let targetDisplay = '';
    
    const protocolLower = (service.protocol || '').toLowerCase();
    if (protocolLower === 'http' || protocolLower === 'https') {
        const pathDisplay = service.fullHttpPath || service.path || '-';
        targetDisplay = `${hostPort}<br><code>${pathDisplay}</code>`;
    } else if (protocolLower === 'mysql' || protocolLower === 'postgresql') {
        targetDisplay = `${hostPort}${service.additionalInfo ? '<br><code>' + service.additionalInfo + '</code>' : ''}`;
    } else if (protocolLower === 'ssh') {
        targetDisplay = `${hostPort}${service.additionalInfo ? '<br><code>' + service.additionalInfo + '</code>' : ''}`;
    } else {
        targetDisplay = hostPort;
    }
    
    return `
        <div class="tree-service ${service.status}">
            <div>${statusBadge}</div>
            <div>
                <div class="tree-service-name">${service.name || service.serviceId}</div>
                <div class="tree-service-details">
                    ${protocolBadge}
                </div>
            </div>
            <div class="tree-service-target">${targetDisplay}</div>
            <div class="tree-service-duration">${duration}</div>
            <div class="tree-service-last-check">${lastCheck}</div>
            <div class="tree-service-error">${error}</div>
        </div>
    `;
}

// Make toggleGroup globally accessible
window.toggleGroup = function(groupId) {
    const element = document.getElementById(`group-${groupId}`);
    if (!element) return;
    
    const header = element.previousElementSibling;
    element.classList.toggle('collapsed');
    header.classList.toggle('collapsed');
};

// Render services table
function renderServices() {
    if (treeView) {
        renderTreeView();
        return;
    }
    
    const tbody = document.getElementById('servicesTableBody');
    
    if (filteredServices.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty">No services matching filters</td></tr>';
        return;
    }
    
    tbody.innerHTML = filteredServices.map(service => {
        const statusBadge = `<span class="status-badge ${service.status}">${getStatusText(service.status)}</span>`;
        const protocolBadge = `<span class="protocol-badge">${service.protocol?.toUpperCase() || 'N/A'}</span>`;
        const duration = service.duration ? `${Math.round(service.duration)} ms` : 'N/A';
        const lastCheck = service.lastCheck ? formatDate(new Date(service.lastCheck)) : 'N/A';
        const error = service.error ? `<span class="error-text" title="${service.error}">${service.error}</span>` : '-';
        
        // Format address based on protocol
        const hostPort = service.host && service.port ? `${service.host}:${service.port}` : (service.host || '-');
        let addressDisplay = '';
        
        const protocolLower = (service.protocol || '').toLowerCase();
        if (protocolLower === 'http' || protocolLower === 'https') {
            // HTTP/HTTPS: show host:port and path
            const pathDisplay = service.fullHttpPath || service.path || '-';
            addressDisplay = `
                <div class="path-container">
                    <div class="host-block">${hostPort}</div>
                    <div class="path-block"><code>${pathDisplay}</code></div>
                </div>
            `;
        } else if (protocolLower === 'mysql' || protocolLower === 'postgresql') {
            // MySQL/PostgreSQL: show host:port and query if available
            addressDisplay = `
                <div class="path-container">
                    <div class="host-block">${hostPort}</div>
                    ${service.additionalInfo ? `<div class="path-block"><code>${service.additionalInfo}</code></div>` : ''}
                </div>
            `;
        } else if (protocolLower === 'ssh') {
            // SSH: show host:port and command if available
            addressDisplay = `
                <div class="path-container">
                    <div class="host-block">${hostPort}</div>
                    ${service.additionalInfo ? `<div class="path-block"><code>${service.additionalInfo}</code></div>` : ''}
                </div>
            `;
        } else {
            // TCP/UDP and others: just show host:port
            addressDisplay = `
                <div class="path-container">
                    <div class="host-block">${hostPort}</div>
                </div>
            `;
        }
        
        return `
            <tr>
                <td>${statusBadge}</td>
                <td><strong>${service.name || service.serviceId}</strong></td>
                <td>${protocolBadge}</td>
                <td>${addressDisplay}</td>
                <td class="duration">${duration}</td>
                <td class="last-check">${lastCheck}</td>
                <td>${error}</td>
            </tr>
        `;
    }).join('');
}

// Get status text in English
function getStatusText(status) {
    const statusMap = {
        'alive': 'Alive',
        'dead': 'Dead',
        'pending': 'Checking...'
    };
    return statusMap[status] || status;
}

// Format date
function formatDate(date) {
    const now = new Date();
    const diff = now - date;
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    
    if (seconds < 60) {
        return `${seconds} sec ago`;
    } else if (minutes < 60) {
        return `${minutes} min ago`;
    } else if (hours < 24) {
        return `${hours} h ago`;
    } else {
        return date.toLocaleString('en-US');
    }
}

// Update summary from current services
function updateSummary() {
    const total = services.length;
    const alive = services.filter(s => s.status === 'alive').length;
    const dead = services.filter(s => s.status === 'dead').length;
    const pending = services.filter(s => s.status === 'pending').length;
    
    const avgDuration = services
        .filter(s => s.duration)
        .reduce((sum, s) => sum + s.duration, 0) / (services.filter(s => s.duration).length || 1);
    
    const uptimePercent = total > 0 ? (alive / total * 100) : 0;
    
    updateSummaryDisplay({
        total,
        alive,
        dead,
        pending,
        uptimePercent: Math.round(uptimePercent * 100) / 100,
        averageDurationMs: Math.round(avgDuration)
    });
}

// Update summary display
function updateSummaryDisplay(summary) {
    document.getElementById('totalServices').textContent = summary.total || 0;
    document.getElementById('aliveServices').textContent = summary.alive || 0;
    document.getElementById('deadServices').textContent = summary.dead || 0;
    document.getElementById('uptimePercent').textContent = `${summary.uptimePercent || 0}%`;
    document.getElementById('avgDuration').textContent = `${summary.averageDurationMs || 0} ms`;
}

// Toggle view
function toggleView() {
    treeView = !treeView;
    const treeViewDiv = document.getElementById('treeView');
    const tableViewDiv = document.getElementById('tableView');
    const toggleBtn = document.getElementById('viewToggle');
    
    if (treeView) {
        treeViewDiv.style.display = 'block';
        tableViewDiv.style.display = 'none';
        toggleBtn.textContent = 'Table View';
    } else {
        treeViewDiv.style.display = 'none';
        tableViewDiv.style.display = 'block';
        toggleBtn.textContent = 'Tree View';
    }
    
    renderServices();
}

// Event listeners
document.getElementById('searchInput').addEventListener('input', applyFilters);
document.getElementById('statusFilter').addEventListener('change', applyFilters);
document.getElementById('protocolFilter').addEventListener('change', applyFilters);
document.getElementById('viewToggle').addEventListener('click', toggleView);

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    initSignalR();
    
    // Fetch initial data via API as fallback
    fetchServices();
    
    // Refresh data every 30 seconds as fallback
    setInterval(fetchServices, 30000);
});
