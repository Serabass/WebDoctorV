import React, { useState, useEffect } from 'react';
import { useSignalR } from '../hooks/useSignalR';
import ServicesTable from './ServicesTable';
import ServicesTree from './ServicesTree';
import Summary from './Summary';
import Filters from './Filters';
import './Dashboard.css';

function Dashboard() {
  const { connection, connectionStatus } = useSignalR();
  const [services, setServices] = useState([]);
  const [filteredServices, setFilteredServices] = useState([]);
  const [summary, setSummary] = useState({
    total: 0,
    alive: 0,
    dead: 0,
    pending: 0,
    uptimePercent: 0,
    averageDurationMs: 0
  });
  const [filters, setFilters] = useState({
    search: '',
    status: 'all',
    protocol: 'all'
  });
  const [treeView, setTreeView] = useState(false);

  // Fetch services from API
  const fetchServices = async () => {
    try {
      const response = await fetch('/api/health/services');
      const data = await response.json();
      const servicesList = data.services || [];
      setServices(servicesList);
      applyFilters(servicesList, filters);
      updateSummary(servicesList);
    } catch (error) {
      console.error('Failed to fetch services:', error);
    }
  };

  // Update summary from services
  const updateSummary = (servicesList) => {
    const total = servicesList.length;
    const alive = servicesList.filter(s => s.status === 'alive').length;
    const dead = servicesList.filter(s => s.status === 'dead').length;
    const pending = servicesList.filter(s => s.status === 'pending').length;

    const avgDuration = servicesList
      .filter(s => s.duration)
      .reduce((sum, s) => sum + s.duration, 0) / (servicesList.filter(s => s.duration).length || 1);

    const uptimePercent = total > 0 ? (alive / total * 100) : 0;

    setSummary({
      total,
      alive,
      dead,
      pending,
      uptimePercent: Math.round(uptimePercent * 100) / 100,
      averageDurationMs: Math.round(avgDuration)
    });
  };

  // Check if service matches filters
  const matchesFilters = (service, filters) => {
    if (filters.search) {
      const searchLower = filters.search.toLowerCase();
      if (!service.name?.toLowerCase().includes(searchLower) &&
          !service.protocol?.toLowerCase().includes(searchLower) &&
          !service.path?.toLowerCase().includes(searchLower)) {
        return false;
      }
    }

    if (filters.status !== 'all' && service.status !== filters.status) {
      return false;
    }

    if (filters.protocol !== 'all' && service.protocol?.toLowerCase() !== filters.protocol.toLowerCase()) {
      return false;
    }

    return true;
  };

  // Apply filters
  const applyFilters = (servicesList, currentFilters) => {
    const filtered = servicesList.filter(service => matchesFilters(service, currentFilters));
    setFilteredServices(filtered);
  };

  // Handle filter changes
  const handleFilterChange = (newFilters) => {
    const updatedFilters = { ...filters, ...newFilters };
    setFilters(updatedFilters);
    applyFilters(services, updatedFilters);
  };

  // Update single service
  const updateService = (updatedService) => {
    setServices(prev => {
      const index = prev.findIndex(s => s.path === updatedService.path);
      let updated;
      if (index !== -1) {
        updated = [...prev];
        updated[index] = updatedService;
      } else {
        updated = [...prev, updatedService];
      }
      // Update summary with new services list
      updateSummary(updated);
      return updated;
    });

    // Update filtered services if matches
    if (matchesFilters(updatedService, filters)) {
      setFilteredServices(prev => {
        const index = prev.findIndex(s => s.path === updatedService.path);
        if (index !== -1) {
          const updated = [...prev];
          updated[index] = updatedService;
          return updated;
        } else {
          return [...prev, updatedService];
        }
      });
    } else {
      setFilteredServices(prev => prev.filter(s => s.path !== updatedService.path));
    }
  };

  // Setup SignalR listeners
  useEffect(() => {
    if (!connection) return;

    const handleStatusUpdate = (service) => {
      updateService(service);
    };

    const handleAllStatuses = (data) => {
      const servicesList = data.services || [];
      setServices(servicesList);
      applyFilters(servicesList, filters);
      updateSummary(servicesList);
    };

    const handleSummary = (summaryData) => {
      setSummary(summaryData);
    };

    connection.on('StatusUpdate', handleStatusUpdate);
    connection.on('AllStatuses', handleAllStatuses);
    connection.on('Summary', handleSummary);

    // Initial fetch
    fetchServices();

    // Fallback refresh every 30 seconds
    const interval = setInterval(fetchServices, 30000);

    return () => {
      connection.off('StatusUpdate', handleStatusUpdate);
      connection.off('AllStatuses', handleAllStatuses);
      connection.off('Summary', handleSummary);
      clearInterval(interval);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connection]);

  const getStatusText = () => {
    switch (connectionStatus) {
      case 'connected': return 'Connected';
      case 'reconnecting': return 'Reconnecting...';
      case 'disconnected': return 'Disconnected';
      default: return 'Connecting...';
    }
  };

  return (
    <div className="container">
      <header>
        <h1>🏥 WebdoctorV Dashboard</h1>
        <div className="status-indicator">
          <span className={`status-dot ${connectionStatus}`}></span>
          <span>{getStatusText()}</span>
        </div>
      </header>

      <Summary summary={summary} />

      <Filters
        filters={filters}
        onFilterChange={handleFilterChange}
        treeView={treeView}
        onToggleView={() => setTreeView(!treeView)}
      />

      <div className="services-container">
        {treeView ? (
          <ServicesTree services={filteredServices} />
        ) : (
          <ServicesTable services={filteredServices} />
        )}
      </div>
    </div>
  );
}

export default Dashboard;
