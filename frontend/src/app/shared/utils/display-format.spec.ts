import { formatAmount, utcDateInput } from './display-format';
import { queryString } from '../../core/services/query-string';

describe('Shared display and query utilities', () => {
  it('preserves two decimal currency formatting without changing values', () => {
    expect(formatAmount(1234.5)).toBe('1,234.50');
    expect(formatAmount(-0.25)).toBe('-0.25');
  });
  it('preserves the existing UTC date convention', () => {
    expect(utcDateInput(new Date('2026-09-19T23:00:00-04:00'))).toBe('2026-09-20');
  });
  it('retains false and zero, encodes input and omits empty filters', () => {
    expect(queryString({ search: 'A&B', page: 0, active: false, empty: '', missing: undefined }))
      .toBe('?search=A%26B&page=0&active=false');
    expect(queryString({ search: '' })).toBe('');
  });
});
