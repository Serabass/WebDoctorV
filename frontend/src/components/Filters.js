import React from 'react';
import './Filters.css';

function Filters({ filters, onFilterChange, treeView, onToggleView }) {
  return (
    <div className="controls">
      <input
        type="text"
        placeholder="Search by name or protocol..."
        value={filters.search}
        onChange={(e) => onFilterChange({ search: e.target.value })}
      />
      <select
        value={filters.status}
        onChange={(e) => onFilterChange({ status: e.target.value })}
      >
        <option value="all">All Statuses</option>
        <option value="alive">Alive</option>
        <option value="dead">Dead</option>
        <option value="pending">Checking...</option>
      </select>
      <select
        value={filters.protocol}
        onChange={(e) => onFilterChange({ protocol: e.target.value })}
      >
        <option value="all">All Protocols</option>
        <option value="http">HTTP</option>
        <option value="https">HTTPS</option>
        <option value="mysql">MySQL</option>
        <option value="postgresql">PostgreSQL</option>
        <option value="tcp">TCP</option>
        <option value="udp">UDP</option>
        <option value="ssh">SSH</option>
      </select>
      <button className="view-toggle-btn" onClick={onToggleView}>
        {treeView ? 'Table View' : 'Tree View'}
      </button>
    </div>
  );
}

export default Filters;
