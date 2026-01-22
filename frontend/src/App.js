import React from 'react';
import './App.css';
import Dashboard from './components/Dashboard';
import { SignalRProvider } from './hooks/useSignalR';

function App() {
  return (
    <SignalRProvider>
      <Dashboard />
    </SignalRProvider>
  );
}

export default App;
