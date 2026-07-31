// Grouped navigation model. Groups follow the DSU "blessed" workflow labels rather than DB-table
// order. `icon` is a key resolved to an Icons.jsx component in the Sidebar; `adminOnly` hides the
// item for read-only principals. Route metadata (`titleKey`/`subtitleKey`) drives the topbar and
// document title per route.

export const NAV_GROUPS = [
  {
    sectionKey: 'nav.groupOperate',
    items: [
      { to: '/dashboard', end: true, labelKey: 'nav.overview', icon: 'Gauge', titleKey: 'overview.title', subtitleKey: 'overview.subtitle' },
      { to: '/dashboard/history', labelKey: 'nav.history', icon: 'History', titleKey: 'history.title', subtitleKey: 'history.subtitle' },
      { to: '/dashboard/observability', labelKey: 'nav.observability', icon: 'Eye', titleKey: 'observability.title', subtitleKey: 'observability.subtitle' },
    ],
  },
  {
    sectionKey: 'nav.groupProvision',
    items: [
      { to: '/dashboard/origins', labelKey: 'nav.origins', icon: 'Server', titleKey: 'origins.title', subtitleKey: 'origins.subtitle' },
      { to: '/dashboard/endpoints', labelKey: 'nav.endpoints', icon: 'Route', titleKey: 'endpoints.title', subtitleKey: 'endpoints.subtitle' },
      { to: '/dashboard/rewrites', labelKey: 'nav.rewrites', icon: 'Shuffle', titleKey: 'nav.rewrites' },
    ],
  },
  {
    sectionKey: 'nav.groupGovern',
    items: [
      { to: '/dashboard/users', labelKey: 'nav.users', icon: 'Users', adminOnly: true, titleKey: 'nav.users' },
      { to: '/dashboard/credentials', labelKey: 'nav.credentials', icon: 'Key', adminOnly: true, titleKey: 'nav.credentials' },
      { to: '/dashboard/blocked-headers', labelKey: 'nav.blockedHeaders', icon: 'Shield', titleKey: 'nav.blockedHeaders' },
    ],
  },
  {
    sectionKey: 'nav.groupSystem',
    items: [
      { to: '/dashboard/settings', labelKey: 'nav.settings', icon: 'Settings', titleKey: 'settings.title', subtitleKey: 'settings.subtitle' },
      { to: '/dashboard/api-explorer', labelKey: 'nav.apiExplorer', icon: 'Terminal', titleKey: 'nav.apiExplorer' },
    ],
  },
];

// Flat lookup for route metadata (topbar title/subtitle + document.title).
export const ROUTE_META = NAV_GROUPS.flatMap((g) => g.items).reduce((acc, item) => {
  acc[item.to] = item;
  return acc;
}, {});

export function metaForPath(pathname) {
  if (ROUTE_META[pathname]) return ROUTE_META[pathname];
  // Longest-prefix match for nested/detail routes.
  const match = Object.keys(ROUTE_META)
    .filter((p) => p !== '/dashboard' && pathname.startsWith(p))
    .sort((a, b) => b.length - a.length)[0];
  return match ? ROUTE_META[match] : ROUTE_META['/dashboard'];
}
