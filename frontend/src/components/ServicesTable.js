import React from 'react';
import './ServicesTable.css';

function ServicesTable({ services }) {
  const getStatusText = (status) => {
    const statusMap = {
      'alive': 'Alive',
      'dead': 'Dead',
      'pending': 'Checking...'
    };
    return statusMap[status] || status;
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
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
  };

  const formatAddress = (service) => {
    const hostPort = service.host && service.port ? `${service.host}:${service.port}` : (service.host || '-');
    const protocolLower = (service.protocol || '').toLowerCase();

    if (protocolLower === 'http' || protocolLower === 'https') {
      const pathDisplay = service.fullHttpPath || service.path || '-';
      return (
        <div className="path-container">
          <div className="host-block">{hostPort}</div>
          <div className="path-block"><code>{pathDisplay}</code></div>
        </div>
      );
    } else if (protocolLower === 'mysql' || protocolLower === 'postgresql' || protocolLower === 'ssh') {
      return (
        <div className="path-container">
          <div className="host-block">{hostPort}</div>
          {service.additionalInfo && (
            <div className="path-block"><code>{service.additionalInfo}</code></div>
          )}
        </div>
      );
    } else {
      return (
        <div className="path-container">
          <div className="host-block">{hostPort}</div>
        </div>
      );
    }
  };

  if (services.length === 0) {
    return (
      <div className="empty">No services matching filters</div>
    );
  }

  return (
    <table className="services-table">
      <thead>
        <tr>
          <th>Status</th>
          <th>Name</th>
          <th>Protocol</th>
          <th>Target</th>
          <th>Response Time</th>
          <th>Last Check</th>
          <th>Error</th>
        </tr>
      </thead>
      <tbody>
        {services.map((service, index) => (
          <tr key={service.path || index}>
            <td>
              <span className={`status-badge ${service.status}`}>
                {getStatusText(service.status)}
              </span>
            </td>
            <td><strong>{service.name || service.serviceId}</strong></td>
            <td>
              <span className="protocol-badge">
                {service.protocol?.toUpperCase() || 'N/A'}
              </span>
            </td>
            <td>{formatAddress(service)}</td>
            <td className="duration">
              {service.duration ? `${Math.round(service.duration)} ms` : 'N/A'}
            </td>
            <td className="last-check">{formatDate(service.lastCheck)}</td>
            <td>
              {service.error ? (
                <span className="error-text" title={service.error}>
                  {service.error}
                </span>
              ) : (
                '-'
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default ServicesTable;
