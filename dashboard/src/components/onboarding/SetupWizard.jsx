import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  Modal,
  Badge,
  StatusBadge,
  CopyableId,
  EmptyState,
  Icons,
} from '../ui';
import { useOnboarding } from '../../context/OnboardingContext';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import './SetupWizard.css';

// Step indices — keep the order in sync with the stepper labels below.
const STEP_VERIFY = 0;
const STEP_ORIGINS = 1;
const STEP_ENDPOINT = 2;
const STEP_VALIDATE = 3;
const STEP_DONE = 4;
const STEP_COUNT = 5;

const EMPTY_ORIGIN = {
  identifier: '',
  name: '',
  hostname: '',
  port: 80,
  ssl: false,
};

const EMPTY_ENDPOINT = {
  identifier: '',
  name: '',
  loadBalancingMode: 'RoundRobin',
};

// First-run Setup Wizard. Walks a new operator through verifying connectivity and
// creating a first origin + endpoint, then validates the resulting configuration.
// Renders nothing unless the onboarding context has the wizard active.
export default function SetupWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { wizardActive, endWizard } = useOnboarding();
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();

  const [step, setStep] = useState(STEP_VERIFY);

  // Verify
  const [health, setHealth] = useState(null);
  const [healthChecked, setHealthChecked] = useState(false);
  const [healthLoading, setHealthLoading] = useState(false);

  // Origins
  const [originForm, setOriginForm] = useState(EMPTY_ORIGIN);
  const [creatingOrigin, setCreatingOrigin] = useState(false);
  const [origins, setOrigins] = useState([]);

  // Endpoint
  const [endpointForm, setEndpointForm] = useState(EMPTY_ENDPOINT);
  const [creatingEndpoint, setCreatingEndpoint] = useState(false);
  const [endpoint, setEndpoint] = useState(null);

  // Validate
  const [validation, setValidation] = useState(null);
  const [validating, setValidating] = useState(false);

  const healthy = health?.status != null && String(health.status).toLowerCase() !== 'unhealthy';

  const runHealthCheck = useCallback(async () => {
    if (!apiClient) return;
    setHealthLoading(true);
    try {
      const result = await apiClient.getHealth();
      setHealth(result || { status: 'healthy' });
    } catch {
      setHealth(null);
    } finally {
      setHealthChecked(true);
      setHealthLoading(false);
    }
  }, [apiClient]);

  // Reset all internal state whenever the modal (re)opens.
  useEffect(() => {
    if (!wizardActive) return;
    setStep(STEP_VERIFY);
    setHealth(null);
    setHealthChecked(false);
    setHealthLoading(false);
    setOriginForm(EMPTY_ORIGIN);
    setCreatingOrigin(false);
    setOrigins([]);
    setEndpointForm(EMPTY_ENDPOINT);
    setCreatingEndpoint(false);
    setEndpoint(null);
    setValidation(null);
    setValidating(false);
  }, [wizardActive]);

  // Auto-run the health check the first time the Verify step is shown.
  useEffect(() => {
    if (wizardActive && step === STEP_VERIFY && !healthChecked && !healthLoading) {
      runHealthCheck();
    }
  }, [wizardActive, step, healthChecked, healthLoading, runHealthCheck]);

  const setOriginField = (key, value) => setOriginForm((prev) => ({ ...prev, [key]: value }));
  const setEndpointField = (key, value) => setEndpointForm((prev) => ({ ...prev, [key]: value }));

  const createOrigin = async (e) => {
    e.preventDefault();
    if (!apiClient) return;
    setCreatingOrigin(true);
    try {
      const created = await apiClient.createOrigin(originForm);
      const identifier = created?.identifier || originForm.identifier;
      setOrigins((prev) => [
        ...prev,
        { identifier, name: created?.name || originForm.name, guid: created?.guid },
      ]);
      setOriginForm(EMPTY_ORIGIN);
      showSuccess(t('origins.created'));
    } catch (err) {
      showError(`${t('origins.saveError')}: ${err.message}`);
    } finally {
      setCreatingOrigin(false);
    }
  };

  const createEndpoint = async (e) => {
    e.preventDefault();
    if (!apiClient) return;
    setCreatingEndpoint(true);
    try {
      const created = await apiClient.createEndpoint(endpointForm);
      const identifier = created?.identifier || endpointForm.identifier;
      // Auto-attach every origin created in the previous step and add a starter route.
      let attached = 0;
      for (const origin of origins) {
        try {
          await apiClient.createMapping({
            endpointIdentifier: identifier,
            originIdentifier: origin.identifier,
          });
          attached += 1;
        } catch {
          /* a mapping failure shouldn't block onboarding */
        }
      }
      try {
        await apiClient.createRoute({
          endpointIdentifier: identifier,
          httpMethod: 'GET',
          urlPattern: '/',
          requiresAuthentication: false,
        });
      } catch {
        /* route is best-effort */
      }
      setEndpoint({ identifier, name: created?.name || endpointForm.name, guid: created?.guid, attached });
      showSuccess(t('wizard.autoAttached', { origins: attached }));
    } catch (err) {
      showError(`${t('endpoints.saveError')}: ${err.message}`);
    } finally {
      setCreatingEndpoint(false);
    }
  };

  // Client-side fallback that mirrors the validateConfig() response shape.
  const clientValidate = useCallback(async () => {
    const errors = [];
    const warnings = [];
    try {
      const [originList, endpointList] = await Promise.all([
        apiClient.getOrigins(),
        apiClient.getEndpoints(),
      ]);
      const originArr = Array.isArray(originList) ? originList : [];
      const endpointArr = Array.isArray(endpointList) ? endpointList : [];
      if (originArr.length === 0) errors.push(t('wizard.checkNoOrigin'));
      if (endpointArr.length === 0) errors.push(t('wizard.checkNoEndpoint'));
      if (endpoint) {
        const mappings = await apiClient.getMappings().catch(() => []);
        const mappingArr = Array.isArray(mappings) ? mappings : [];
        const hasMapping = mappingArr.some((m) => m.endpointIdentifier === endpoint.identifier);
        if (!hasMapping) warnings.push(t('wizard.checkNoMapping'));
      }
    } catch (err) {
      errors.push(err.message || t('errors.generic'));
    }
    return { valid: errors.length === 0, errors, warnings };
  }, [apiClient, endpoint, t]);

  const runValidate = useCallback(async () => {
    if (!apiClient) return;
    setValidating(true);
    try {
      let result;
      try {
        result = await apiClient.validateConfig();
      } catch (err) {
        // Fall back to a client-side check when the endpoint is missing (404) or errors.
        if (err?.status === 404 || err?.status === 0 || err?.status === 405 || err?.status == null) {
          result = await clientValidate();
        } else {
          throw err;
        }
      }
      setValidation(
        result && typeof result === 'object'
          ? {
              valid: result.valid !== false && (result.errors?.length ?? 0) === 0,
              errors: result.errors || [],
              warnings: result.warnings || [],
            }
          : { valid: true, errors: [], warnings: [] }
      );
    } catch (err) {
      setValidation({ valid: false, errors: [err.message || t('errors.generic')], warnings: [] });
    } finally {
      setValidating(false);
    }
  }, [apiClient, clientValidate, t]);

  // Run validation on entry to the Validate step.
  useEffect(() => {
    if (wizardActive && step === STEP_VALIDATE && validation == null && !validating) {
      runValidate();
    }
  }, [wizardActive, step, validation, validating, runValidate]);

  const canProceed = useMemo(() => {
    switch (step) {
      case STEP_VERIFY:
        return healthChecked && healthy;
      case STEP_ORIGINS:
        return origins.length >= 1;
      case STEP_ENDPOINT:
        return endpoint != null;
      case STEP_VALIDATE:
        return validation != null && validation.valid;
      default:
        return true;
    }
  }, [step, healthChecked, healthy, origins.length, endpoint, validation]);

  const skip = () => endWizard(true);
  const finish = () => endWizard(true);
  const back = () => setStep((s) => Math.max(STEP_VERIFY, s - 1));
  const next = () => setStep((s) => Math.min(STEP_DONE, s + 1));

  const goTo = (path) => {
    endWizard(true);
    navigate(path);
  };

  const stepLabels = [
    t('wizard.stepVerify'),
    t('wizard.stepOrigins'),
    t('wizard.stepEndpoint'),
    t('wizard.stepValidate'),
    t('wizard.stepDone'),
  ];

  if (!wizardActive) return null;

  const footer = (
    <>
      <button type="button" className="sb-btn sb-btn--ghost sw-skip" onClick={skip}>
        {t('wizard.skip')}
      </button>
      <button type="button" className="sb-btn sb-btn--ghost" onClick={back} disabled={step === STEP_VERIFY}>
        {t('common.back')}
      </button>
      {step === STEP_DONE ? (
        <button type="button" className="sb-btn sb-btn--primary" onClick={finish}>
          {t('common.finish')}
        </button>
      ) : (
        <button type="button" className="sb-btn sb-btn--primary" onClick={next} disabled={!canProceed}>
          {t('common.next')}
        </button>
      )}
    </>
  );

  return (
    <Modal
      open={wizardActive}
      onClose={skip}
      size="large"
      closeOnBackdrop={false}
      title={t('wizard.title')}
      subtitle={t('wizard.stepOf', { current: step + 1, total: STEP_COUNT })}
      footer={footer}
      className="sw-modal"
    >
      <ol className="sw-stepper" aria-label={t('wizard.title')}>
        {stepLabels.map((label, i) => {
          const state = i < step ? 'done' : i === step ? 'active' : 'upcoming';
          return (
            <li key={label} className={`sw-step sw-step--${state}`} aria-current={i === step ? 'step' : undefined}>
              <span className="sw-step__dot" aria-hidden="true">
                {i < step ? <Icons.Check size={14} /> : i + 1}
              </span>
              <span className="sw-step__label">{label}</span>
            </li>
          );
        })}
      </ol>

      <div className="sw-body">
        {step === STEP_VERIFY && (
          <section className="sw-panel">
            <h3 className="sw-panel__title">{t('wizard.verifyTitle')}</h3>
            <p className="sw-panel__body">{t('wizard.verifyBody')}</p>
            <div className="sw-verify">
              <button
                type="button"
                className="sb-btn sb-btn--ghost"
                onClick={runHealthCheck}
                disabled={healthLoading}
              >
                <Icons.Refresh size={16} />
                <span>{healthLoading ? t('wizard.checking') : t('wizard.check')}</span>
              </button>
              {healthChecked && !healthLoading && (
                healthy ? (
                  <div className="sw-verify__result sw-verify__result--ok">
                    <StatusBadge status="healthy" label={t('wizard.verifyOk')} />
                    {health?.version && (
                      <span className="sw-verify__version">
                        {t('wizard.serverVersion', { version: health.version })}
                      </span>
                    )}
                  </div>
                ) : (
                  <div className="sw-verify__result">
                    <StatusBadge status="unhealthy" label={t('wizard.verifyFail')} />
                  </div>
                )
              )}
            </div>
          </section>
        )}

        {step === STEP_ORIGINS && (
          <section className="sw-panel">
            <h3 className="sw-panel__title">{t('wizard.originsTitle')}</h3>
            <p className="sw-panel__body">{t('wizard.originsBody')}</p>
            <form id="sw-origin-form" className="sw-form" onSubmit={createOrigin}>
              <div className="sw-form-grid">
                <div className="form-group">
                  <label className="form-label" htmlFor="sw-o-identifier">{t('origins.identifier')}</label>
                  <input
                    id="sw-o-identifier"
                    className="form-input"
                    value={originForm.identifier}
                    onChange={(e) => setOriginField('identifier', e.target.value)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="form-label" htmlFor="sw-o-name">{t('origins.name')}</label>
                  <input
                    id="sw-o-name"
                    className="form-input"
                    value={originForm.name}
                    onChange={(e) => setOriginField('name', e.target.value)}
                  />
                </div>
              </div>
              <div className="sw-form-grid">
                <div className="form-group">
                  <label className="form-label" htmlFor="sw-o-hostname">{t('origins.hostname')}</label>
                  <input
                    id="sw-o-hostname"
                    className="form-input"
                    value={originForm.hostname}
                    onChange={(e) => setOriginField('hostname', e.target.value)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="form-label" htmlFor="sw-o-port">{t('origins.port')}</label>
                  <input
                    id="sw-o-port"
                    type="number"
                    className="form-input"
                    value={originForm.port}
                    onChange={(e) => setOriginField('port', parseInt(e.target.value, 10) || 0)}
                    required
                  />
                </div>
              </div>
              <div className="form-group">
                <label className="sw-check">
                  <input
                    type="checkbox"
                    checked={originForm.ssl}
                    onChange={(e) => {
                      const ssl = e.target.checked;
                      // Default the port to the conventional value for the scheme.
                      setOriginForm((prev) => ({ ...prev, ssl, port: ssl ? 443 : 80 }));
                    }}
                  />
                  <span>{t('origins.useSsl')}</span>
                </label>
              </div>
              <div className="sw-form-actions">
                <button type="submit" className="sb-btn sb-btn--primary" disabled={creatingOrigin}>
                  <Icons.Plus size={16} />
                  <span>{origins.length > 0 ? t('wizard.addAnother') : t('common.create')}</span>
                </button>
              </div>
            </form>

            <div className="sw-created">
              <div className="sw-created__label">{t('wizard.createdOrigins')}</div>
              {origins.length === 0 ? (
                <EmptyState icon={<Icons.Server size={28} />} message={t('origins.empty')} />
              ) : (
                <ul className="sw-created__list">
                  {origins.map((o) => (
                    <li key={o.identifier} className="sw-created__item">
                      <Icons.Server size={16} />
                      <span className="sw-created__name">{o.name || o.identifier}</span>
                      <CopyableId value={o.identifier} />
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </section>
        )}

        {step === STEP_ENDPOINT && (
          <section className="sw-panel">
            <h3 className="sw-panel__title">{t('wizard.endpointTitle')}</h3>
            <p className="sw-panel__body">{t('wizard.endpointBody')}</p>
            {endpoint ? (
              <div className="sw-created">
                <div className="sw-created__label">{t('wizard.createdEndpoint')}</div>
                <ul className="sw-created__list">
                  <li className="sw-created__item">
                    <Icons.Route size={16} />
                    <span className="sw-created__name">{endpoint.name || endpoint.identifier}</span>
                    <CopyableId value={endpoint.identifier} />
                  </li>
                </ul>
                <p className="sw-panel__note">{t('wizard.autoAttached', { origins: endpoint.attached })}</p>
              </div>
            ) : (
              <form id="sw-endpoint-form" className="sw-form" onSubmit={createEndpoint}>
                <div className="sw-form-grid">
                  <div className="form-group">
                    <label className="form-label" htmlFor="sw-e-identifier">{t('endpoints.identifier')}</label>
                    <input
                      id="sw-e-identifier"
                      className="form-input"
                      value={endpointForm.identifier}
                      onChange={(e) => setEndpointField('identifier', e.target.value)}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label className="form-label" htmlFor="sw-e-name">{t('endpoints.name')}</label>
                    <input
                      id="sw-e-name"
                      className="form-input"
                      value={endpointForm.name}
                      onChange={(e) => setEndpointField('name', e.target.value)}
                    />
                  </div>
                </div>
                <div className="form-group">
                  <label className="form-label" htmlFor="sw-e-lb">{t('endpoints.loadBalancing')}</label>
                  <select
                    id="sw-e-lb"
                    className="form-input"
                    value={endpointForm.loadBalancingMode}
                    onChange={(e) => setEndpointField('loadBalancingMode', e.target.value)}
                  >
                    <option value="RoundRobin">{t('endpoints.roundRobin')}</option>
                    <option value="Random">{t('endpoints.random')}</option>
                  </select>
                </div>
                <div className="sw-form-actions">
                  <button type="submit" className="sb-btn sb-btn--primary" disabled={creatingEndpoint}>
                    <Icons.Plus size={16} />
                    <span>{creatingEndpoint ? t('common.saving') : t('common.create')}</span>
                  </button>
                </div>
              </form>
            )}
          </section>
        )}

        {step === STEP_VALIDATE && (
          <section className="sw-panel">
            <h3 className="sw-panel__title">{t('wizard.validateTitle')}</h3>
            <p className="sw-panel__body">{t('wizard.validateBody')}</p>
            <div className="sw-form-actions sw-form-actions--start">
              <button type="button" className="sb-btn sb-btn--ghost" onClick={runValidate} disabled={validating}>
                <Icons.Refresh size={16} />
                <span>{validating ? t('wizard.validating') : t('wizard.revalidate')}</span>
              </button>
            </div>
            {validation && !validating && (
              <div className="sw-validate">
                {validation.valid && validation.warnings.length === 0 ? (
                  <div className="sw-validate__ok">
                    <StatusBadge status="valid" label={t('wizard.validateOk')} />
                  </div>
                ) : (
                  <>
                    {validation.errors.length > 0 && (
                      <div className="sw-issues sw-issues--error">
                        <div className="sw-issues__label">{t('wizard.validateErrors')}</div>
                        <ul>
                          {validation.errors.map((msg, i) => (
                            <li key={`e-${i}`}>{typeof msg === 'string' ? msg : msg?.message || String(msg)}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {validation.warnings.length > 0 && (
                      <div className="sw-issues sw-issues--warning">
                        <div className="sw-issues__label">{t('wizard.validateWarnings')}</div>
                        <ul>
                          {validation.warnings.map((msg, i) => (
                            <li key={`w-${i}`}>{typeof msg === 'string' ? msg : msg?.message || String(msg)}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {validation.valid && validation.warnings.length > 0 && (
                      <p className="sw-panel__note">{t('wizard.validateOk')}</p>
                    )}
                  </>
                )}
              </div>
            )}
          </section>
        )}

        {step === STEP_DONE && (
          <section className="sw-panel">
            <h3 className="sw-panel__title">{t('wizard.doneTitle')}</h3>
            <p className="sw-panel__body">{t('wizard.doneBody')}</p>
            <div className="sw-created">
              <div className="sw-created__label">{t('wizard.doneRecap')}</div>
              <ul className="sw-created__list">
                {origins.map((o) => (
                  <li key={o.identifier} className="sw-created__item">
                    <Icons.Server size={16} />
                    <span className="sw-created__name">{o.name || o.identifier}</span>
                    <Badge tone="success">{t('origins.title')}</Badge>
                  </li>
                ))}
                {endpoint && (
                  <li className="sw-created__item">
                    <Icons.Route size={16} />
                    <span className="sw-created__name">{endpoint.name || endpoint.identifier}</span>
                    <Badge tone="success">{t('endpoints.title')}</Badge>
                  </li>
                )}
              </ul>
            </div>
            <div className="sw-links">
              <button type="button" className="sb-btn sb-btn--ghost" onClick={() => goTo('/dashboard/origins')}>
                <Icons.Server size={16} />
                <span>{t('wizard.goOrigins')}</span>
              </button>
              <button type="button" className="sb-btn sb-btn--ghost" onClick={() => goTo('/dashboard/endpoints')}>
                <Icons.Route size={16} />
                <span>{t('wizard.goEndpoints')}</span>
              </button>
              <button type="button" className="sb-btn sb-btn--ghost" onClick={() => goTo('/dashboard/api-explorer')}>
                <Icons.Terminal size={16} />
                <span>{t('wizard.goApiExplorer')}</span>
              </button>
            </div>
          </section>
        )}
      </div>
    </Modal>
  );
}
