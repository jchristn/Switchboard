// Parses the Request History "Status" filter into a predicate over HTTP status codes.
//
// Comma-separated terms:
//   200            exact code
//   403-429        inclusive range
//   4xx            status class (1xx to 5xx)
//   >=200, <300    comparisons; all comparisons together form ONE range (they are AND-ed),
//                  so ">=200,<=299" means 200 through 299
//   !2xx, !404     exclusion; the code must not match any excluded term
//
// A code matches when it matches any exact/range/class term or the comparison range (or there are
// no such terms at all), and matches no exclusion. Examples: "200", "200,201", ">=200,<=299",
// "200,400,401,403-429", "5xx", "!2xx".

const CODE = /^\d{1,3}$/;
const RANGE = /^(\d{1,3})\s*-\s*(\d{1,3})$/;
const CLASS = /^([1-5])xx$/i;
const COMPARE = /^(>=|<=|>|<)\s*(\d{1,3})$/;

function parsePositive(term) {
  if (CODE.test(term)) {
    const n = Number(term);
    return { test: (c) => c === n };
  }
  const range = term.match(RANGE);
  if (range) {
    const lo = Math.min(Number(range[1]), Number(range[2]));
    const hi = Math.max(Number(range[1]), Number(range[2]));
    return { test: (c) => c >= lo && c <= hi };
  }
  const cls = term.match(CLASS);
  if (cls) {
    const lo = Number(cls[1]) * 100;
    return { test: (c) => c >= lo && c <= lo + 99 };
  }
  return null;
}

// Returns { valid, error, invalidTerm, matches(code) }. An empty expression matches everything.
export function parseStatusFilter(text) {
  const all = { valid: true, error: null, invalidTerm: null, matches: () => true };
  if (typeof text !== 'string' || text.trim() === '') return all;

  const alternatives = [];
  const exclusions = [];
  let lower = -Infinity;
  let upper = Infinity;
  let hasComparison = false;

  const terms = text.split(',').map((x) => x.trim()).filter((x) => x.length > 0);
  for (const term of terms) {
    if (term.startsWith('!')) {
      const inner = parsePositive(term.slice(1).trim());
      if (!inner) return { valid: false, error: 'invalid', invalidTerm: term, matches: () => true };
      exclusions.push(inner);
      continue;
    }

    const cmp = term.match(COMPARE);
    if (cmp) {
      hasComparison = true;
      const n = Number(cmp[2]);
      if (cmp[1] === '>=') lower = Math.max(lower, n);
      else if (cmp[1] === '>') lower = Math.max(lower, n + 1);
      else if (cmp[1] === '<=') upper = Math.min(upper, n);
      else upper = Math.min(upper, n - 1);
      continue;
    }

    const positive = parsePositive(term);
    if (!positive) return { valid: false, error: 'invalid', invalidTerm: term, matches: () => true };
    alternatives.push(positive);
  }

  if (hasComparison) {
    const lo = lower;
    const hi = upper;
    alternatives.push({ test: (c) => c >= lo && c <= hi });
  }

  return {
    valid: true,
    error: null,
    invalidTerm: null,
    matches: (code) => {
      const c = Number(code);
      if (!Number.isFinite(c)) return false;
      if (alternatives.length > 0 && !alternatives.some((a) => a.test(c))) return false;
      return !exclusions.some((x) => x.test(c));
    },
  };
}

// The filter the "Failures" card applies: every status except 2xx.
export const NON_2XX_FILTER = '!2xx';
