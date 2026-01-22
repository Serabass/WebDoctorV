import React, { useState } from 'react';
import './ServicesTree.css';

function ServicesTree({ services }) {
  const [collapsedGroups, setCollapsedGroups] = useState(new Set());

  const buildGroupTree = (servicesList) => {
    const tree = { _groups: {}, _services: [] };

    servicesList.forEach(service => {
      const parts = service.serviceId.split('.');

      if (parts.length === 1) {
        tree._services.push(service);
      } else {
        let current = tree._groups;

        for (let i = 0; i < parts.length - 1; i++) {
          const part = parts[i];
          if (!current[part]) {
            current[part] = { _groups: {}, _services: [] };
          }
          current = current[part]._groups;
        }

        let targetGroup = tree._groups;
        for (let i = 0; i < parts.length - 1; i++) {
          targetGroup = targetGroup[parts[i]];
        }
        targetGroup._services.push(service);
      }
    });

    return tree;
  };

  const getAllServicesInGroup = (groupData) => {
    let servicesList = [...(groupData._services || [])];
    if (groupData._groups) {
      Object.keys(groupData._groups).forEach(key => {
        servicesList = servicesList.concat(getAllServicesInGroup(groupData._groups[key]));
      });
    }
    return servicesList;
  };

  const toggleGroup = (groupId) => {
    setCollapsedGroups(prev => {
      const newSet = new Set(prev);
      if (newSet.has(groupId)) {
        newSet.delete(groupId);
      } else {
        newSet.add(groupId);
      }
      return newSet;
    });
  };

  const renderTreeGroup = (group, prefix) => {
    let elements = [];

    if (group._services && group._services.length > 0) {
      group._services.forEach((service, index) => {
        elements.push(
          <TreeService key={`service-${service.path || index}`} service={service} />
        );
      });
    }

    if (group._groups) {
      Object.keys(group._groups).forEach(key => {
        const groupData = group._groups[key];
        const groupId = prefix ? `${prefix}.${key}` : key;
        const groupName = key.charAt(0).toUpperCase() + key.slice(1).replace(/-/g, ' ');

        const allServices = getAllServicesInGroup(groupData);
        const alive = allServices.filter(s => s.status === 'alive').length;
        const dead = allServices.filter(s => s.status === 'dead').length;
        const total = allServices.length;
        const isCollapsed = collapsedGroups.has(groupId);

        elements.push(
          <div key={`group-${groupId}`} className="tree-group" data-group-id={groupId}>
            <div
              className={`tree-group-header ${isCollapsed ? 'collapsed' : ''}`}
              onClick={() => toggleGroup(groupId)}
            >
              <span className="tree-toggle">{isCollapsed ? '▶' : '▼'}</span>
              <span className="tree-group-name">{groupName}</span>
              <div className="tree-group-stats">
                <span className="alive">{alive} alive</span>
                <span className="dead">{dead} dead</span>
                <span>{total} total</span>
              </div>
            </div>
            {!isCollapsed && (
              <div className="tree-group-children">
                {renderTreeGroup(groupData, groupId)}
              </div>
            )}
          </div>
        );
      });
    }

    return elements;
  };

  if (services.length === 0) {
    return (
      <div className="empty">No services matching filters</div>
    );
  }

  const tree = buildGroupTree(services);
  const elements = renderTreeGroup(tree, '');

  return <div className="tree-container">{elements}</div>;
}

function TreeService({ service }) {
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

  const formatTarget = (service) => {
    const hostPort = service.host && service.port ? `${service.host}:${service.port}` : (service.host || '-');
    const protocolLower = (service.protocol || '').toLowerCase();

    if (protocolLower === 'http' || protocolLower === 'https') {
      const pathDisplay = service.fullHttpPath || service.path || '-';
      return (
        <div>
          {hostPort}
          <br />
          <code>{pathDisplay}</code>
        </div>
      );
    } else if (protocolLower === 'mysql' || protocolLower === 'postgresql' || protocolLower === 'ssh') {
      return (
        <div>
          {hostPort}
          {service.additionalInfo && (
            <>
              <br />
              <code>{service.additionalInfo}</code>
            </>
          )}
        </div>
      );
    } else {
      return hostPort;
    }
  };

  return (
    <div className={`tree-service ${service.status}`}>
      <div>
        <span className={`status-badge ${service.status}`}>
          {getStatusText(service.status)}
        </span>
      </div>
      <div>
        <div className="tree-service-name">{service.name || service.serviceId}</div>
        <div className="tree-service-details">
          <span className="protocol-badge">
            {service.protocol?.toUpperCase() || 'N/A'}
          </span>
        </div>
      </div>
      <div className="tree-service-target">{formatTarget(service)}</div>
      <div className="tree-service-duration">
        {service.duration ? `${Math.round(service.duration)} ms` : 'N/A'}
      </div>
      <div className="tree-service-last-check">{formatDate(service.lastCheck)}</div>
      <div className="tree-service-error">
        {service.error ? (
          <span className="error-text" title={service.error}>
            {service.error}
          </span>
        ) : (
          '-'
        )}
      </div>
    </div>
  );
}

export default ServicesTree;
