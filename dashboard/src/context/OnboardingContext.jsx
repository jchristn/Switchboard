import { createContext, useContext, useState, useCallback } from 'react';
import PropTypes from 'prop-types';

const OnboardingContext = createContext(null);

const SETUP_KEY = 'switchboard_setup_completed';
const TOUR_KEY = 'switchboard_tour_completed';

// Sequences first-run onboarding: an optional spotlight tour and a resource-creating setup wizard,
// each dismissible and independently gated in localStorage so returning users aren't nagged.
export function OnboardingProvider({ children }) {
  const [wizardActive, setWizardActive] = useState(false);
  const [tourActive, setTourActive] = useState(false);
  const [setupCompleted, setSetupCompleted] = useState(
    () => localStorage.getItem(SETUP_KEY) === 'true'
  );
  const [tourCompleted, setTourCompleted] = useState(
    () => localStorage.getItem(TOUR_KEY) === 'true'
  );

  const startWizard = useCallback(() => setWizardActive(true), []);
  const endWizard = useCallback((completed) => {
    setWizardActive(false);
    if (completed) {
      localStorage.setItem(SETUP_KEY, 'true');
      setSetupCompleted(true);
    }
  }, []);

  const startTour = useCallback(() => setTourActive(true), []);
  const endTour = useCallback((completed) => {
    setTourActive(false);
    if (completed) {
      localStorage.setItem(TOUR_KEY, 'true');
      setTourCompleted(true);
    }
  }, []);

  const value = {
    wizardActive,
    tourActive,
    setupCompleted,
    tourCompleted,
    startWizard,
    endWizard,
    startTour,
    endTour,
  };

  return <OnboardingContext.Provider value={value}>{children}</OnboardingContext.Provider>;
}

OnboardingProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

export function useOnboarding() {
  const context = useContext(OnboardingContext);
  if (!context) {
    throw new Error('useOnboarding must be used within an OnboardingProvider');
  }
  return context;
}

export default OnboardingContext;
