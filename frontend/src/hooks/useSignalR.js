import React, { createContext, useContext, useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

const SignalRContext = createContext(null);

export const SignalRProvider = ({ children }) => {
  const [connection, setConnection] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState('disconnected');
  const connectionRef = useRef(null);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('/healthhub')
      .withAutomaticReconnect()
      .build();

    connectionRef.current = newConnection;
    setConnection(newConnection);

    newConnection.onreconnecting(() => {
      setConnectionStatus('reconnecting');
    });

    newConnection.onreconnected(() => {
      setConnectionStatus('connected');
    });

    newConnection.onclose(() => {
      setConnectionStatus('disconnected');
    });

    newConnection.start()
      .then(() => {
        setConnectionStatus('connected');
      })
      .catch(err => {
        console.error('SignalR connection error:', err);
        setConnectionStatus('disconnected');
      });

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
  }, []);

  return (
    <SignalRContext.Provider value={{ connection, connectionStatus }}>
      {children}
    </SignalRContext.Provider>
  );
};

export const useSignalR = () => {
  const context = useContext(SignalRContext);
  if (!context) {
    throw new Error('useSignalR must be used within SignalRProvider');
  }
  return context;
};
