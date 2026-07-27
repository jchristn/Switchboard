import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import PropTypes from 'prop-types';
import { ApiClient } from '../utils/api';

const AuthContext = createContext(null);

const SERVER_KEY = 'switchboard_server_url';
const TOKEN_KEY = 'switchboard_token';

export function AuthProvider({ children }) {
  const [serverUrl, setServerUrl] = useState(() => localStorage.getItem(SERVER_KEY) || '');
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_KEY) || '');
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  // True while we restore a stored session on first load, so routes don't flash the login screen.
  const [isRestoring, setIsRestoring] = useState(true);
  const [error, setError] = useState(null);
  const [apiClient, setApiClient] = useState(null);
  const [currentUser, setCurrentUser] = useState(null);
  const [isAdmin, setIsAdmin] = useState(false);

  const applySession = useCallback(async (client) => {
    let user = null;
    try {
      user = await client.getMe();
    } catch {
      user = null;
    }
    setCurrentUser(user);
    setIsAdmin(user?.isAdmin === true);
    setApiClient(client);
    setIsAuthenticated(true);
  }, []);

  const disconnect = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    setToken('');
    setIsAuthenticated(false);
    setApiClient(null);
    setCurrentUser(null);
    setIsAdmin(false);
  }, []);

  const connect = useCallback(
    async (url, authToken) => {
      setIsLoading(true);
      setError(null);
      try {
        const client = new ApiClient(url, authToken);
        const isValid = await client.validateToken();
        if (isValid) {
          localStorage.setItem(SERVER_KEY, url);
          localStorage.setItem(TOKEN_KEY, authToken);
          setServerUrl(url);
          setToken(authToken);
          await applySession(client);
          return true;
        }
        setError('Invalid token or server unavailable');
        return false;
      } catch (err) {
        setError(err.message || 'Failed to connect to server');
        return false;
      } finally {
        setIsLoading(false);
      }
    },
    [applySession]
  );

  // Restore a stored session on first mount by validating the token.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (serverUrl && token) {
        try {
          const client = new ApiClient(serverUrl, token);
          if (await client.validateToken()) {
            if (!cancelled) await applySession(client);
          }
        } catch {
          /* fall through to unauthenticated */
        }
      }
      if (!cancelled) setIsRestoring(false);
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // A 401 anywhere means the session is dead — drop it.
  useEffect(() => {
    const handler = () => disconnect();
    window.addEventListener('auth:unauthorized', handler);
    return () => window.removeEventListener('auth:unauthorized', handler);
  }, [disconnect]);

  const value = {
    serverUrl,
    token,
    isAuthenticated,
    isLoading,
    isRestoring,
    error,
    apiClient,
    currentUser,
    isAdmin,
    connect,
    disconnect,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
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
