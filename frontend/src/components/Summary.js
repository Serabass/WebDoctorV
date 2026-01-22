import React from 'react';
import './Summary.css';

function Summary({ summary }) {
  return (
    <div className="summary">
      <div className="summary-card">
        <div className="summary-label">Total Services</div>
        <div className="summary-value">{summary.total || 0}</div>
      </div>
      <div className="summary-card">
        <div className="summary-label">Alive</div>
        <div className="summary-value alive">{summary.alive || 0}</div>
      </div>
      <div className="summary-card">
        <div className="summary-label">Dead</div>
        <div className="summary-value dead">{summary.dead || 0}</div>
      </div>
      <div className="summary-card">
        <div className="summary-label">Uptime</div>
        <div className="summary-value">{summary.uptimePercent || 0}%</div>
      </div>
      <div className="summary-card">
        <div className="summary-label">Average Time</div>
        <div className="summary-value">{summary.averageDurationMs || 0} ms</div>
      </div>
    </div>
  );
}

export default Summary;
