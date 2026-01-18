import { createContext, useContext, useState, useCallback } from 'react';
import PropTypes from 'prop-types';
import { ApiClient } from '../utils/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [serverUrl, setServerUrl] = useState(() => {
    return localStorage.getItem('switchboard_server_url') || '';
  });
  const [token, setToken] = useState(() => {
    return localStorage.getItem('switchboard_token') || '';
  });
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);
  const [apiClient, setApiClient] = useState(null);
  const [currentUser, setCurrentUser] = useState(null);
  const [isAdmin, setIsAdmin] = useState(false);

  const connect = useCallback(async (url, authToken) => {
    setIsLoading(true);
    setError(null);

    try {
      const client = new ApiClient(url, authToken);
      const isValid = await client.validateToken();

      if (isValid) {
        // Get current user info
        try {
          const user = await client.getMe();
          setCurrentUser(user);
          setIsAdmin(user.isAdmin === true);
        } catch {
          // If /me fails, still allow login but assume non-admin
          setCurrentUser(null);
          setIsAdmin(false);
        }

        localStorage.setItem('switchboard_server_url', url);
        localStorage.setItem('switchboard_token', authToken);
        setServerUrl(url);
        setToken(authToken);
        setApiClient(client);
        setIsAuthenticated(true);
        return true;
      } else {
        setError('Invalid token or server unavailable');
        return false;
      }
    } catch (err) {
      setError(err.message || 'Failed to connect to server');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const disconnect = useCallback(() => {
    localStorage.removeItem('switchboard_token');
    setToken('');
    setIsAuthenticated(false);
    setApiClient(null);
    setCurrentUser(null);
    setIsAdmin(false);
  }, []);

  const value = {
    serverUrl,
    token,
    isAuthenticated,
    isLoading,
    error,
    apiClient,
    currentUser,
    isAdmin,
    connect,
    disconnect,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

AuthProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

export default AuthContext;
