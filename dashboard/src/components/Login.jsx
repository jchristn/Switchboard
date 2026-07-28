import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import LanguageSelector from './common/LanguageSelector';
import './Login.css';

function Login() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { connect, isLoading, error, isAuthenticated, serverUrl: savedUrl } = useAuth();

  const [serverUrl, setServerUrl] = useState(savedUrl || 'http://localhost:8000');
  const [token, setToken] = useState('');
  const [showToken, setShowToken] = useState(false);

  useEffect(() => {
    if (isAuthenticated) navigate('/dashboard');
  }, [isAuthenticated, navigate]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const success = await connect(serverUrl, token);
    if (success) navigate('/dashboard');
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <img src="/logo.png" alt={t('app.name')} className="login-logo" />
          <h1 className="login-title">{t('app.name')}</h1>
          <p className="login-subtitle">{t('login.title')}</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label" htmlFor="serverUrl">
              {t('login.serverUrl')}
            </label>
            <input
              id="serverUrl"
              type="url"
              className="form-input"
              placeholder="http://localhost:8000"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="token">
              {t('login.token')}
            </label>
            <div className="input-with-icon">
              <input
                id="token"
                type={showToken ? 'text' : 'password'}
                className="form-input"
                value={token}
                onChange={(e) => setToken(e.target.value)}
                required
              />
              <button
                type="button"
                className="visibility-toggle"
                onClick={() => setShowToken(!showToken)}
                tabIndex={-1}
                aria-label={showToken ? t('common.close') : t('common.view')}
              >
                {showToken ? (
                  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/>
                    <line x1="1" y1="1" x2="23" y2="23"/>
                  </svg>
                ) : (
                  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                    <circle cx="12" cy="12" r="3"/>
                  </svg>
                )}
              </button>
            </div>
          </div>

          {error && <div className="login-error">{t('login.failed')}</div>}

          <button type="submit" className="btn btn-primary login-button" disabled={isLoading}>
            {isLoading ? t('login.connecting') : t('login.connect')}
          </button>
        </form>

        <div className="login-footer">
          <p>{t('login.subtitle')}</p>
        </div>

        <div className="login-language">
          <LanguageSelector compact />
        </div>
      </div>
    </div>
  );
}

export default Login;
