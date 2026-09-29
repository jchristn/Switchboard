// Client-side mirror of the server's URL pattern rules (UrlMatcher 3.1 UrlPattern), so the dashboard can
// flag an invalid route or rewrite pattern before it is sent. The server validates again and is the
// authority; this only gives faster, localized feedback.
//
// {name} matches one segment. {*name} as the entire final segment is a catch-all that matches zero or
// more remaining segments. {} and {*} are literals, as are segments without braces such as * or **.

function parseSegment(text) {
  const start = text.indexOf('{');
  if (start === -1) return { type: 'literal' };

  const end = text.indexOf('}', start);
  if (end === -1 || end <= start + 1) return { type: 'literal' };

  const name = text.substring(start + 1, end);
  if (name[0] !== '*') return { type: 'parameter' };
  if (name.length === 1) return { type: 'literal' };

  if (start !== 0 || end !== text.length - 1) {
    return { type: 'error', code: 'catchAllPartial', segment: text.substring(start, end + 1) };
  }

  return { type: 'catchAll', segment: text };
}

// Validate a pattern. Returns null when valid, otherwise { code, segment } where code is one of
// 'required', 'catchAllPartial', 'catchAllMultiple', or 'catchAllNotLast'.
export function validateRoutePattern(pattern) {
  if (typeof pattern !== 'string' || pattern.trim() === '') return { code: 'required', segment: '' };

  const parts = pattern.split('/').filter((p) => p.length > 0);
  const catchAlls = [];

  for (let i = 0; i < parts.length; i += 1) {
    const seg = parseSegment(parts[i]);
    if (seg.type === 'error') return { code: seg.code, segment: seg.segment };
    if (seg.type === 'catchAll') catchAlls.push({ index: i, segment: seg.segment });
  }

  if (catchAlls.length > 1) return { code: 'catchAllMultiple', segment: catchAlls[1].segment };
  if (catchAlls.length === 1 && catchAlls[0].index !== parts.length - 1) {
    return { code: 'catchAllNotLast', segment: catchAlls[0].segment };
  }

  return null;
}

// True when the pattern is valid and ends in a catch-all segment such as /api/{*rest}.
export function isCatchAllPattern(pattern) {
  if (validateRoutePattern(pattern) !== null) return false;
  const parts = pattern.split('/').filter((p) => p.length > 0);
  if (parts.length === 0) return false;
  return parseSegment(parts[parts.length - 1]).type === 'catchAll';
}

// Map a validation result to a translated message. Returns null for a valid pattern.
export function routePatternError(t, pattern) {
  const result = validateRoutePattern(pattern);
  if (!result) return null;
  if (result.code === 'required') return t('endpoints.urlPatternRequired');
  return t(`routePattern.${result.code}`, { segment: result.segment });
}
