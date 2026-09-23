import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_BASE_URL } from '../../core/config/api.config';
import { ApiService } from '../../core/services/api.service';
import { FinancialReportsService } from './financial-reports.service';

describe('FinancialReportsService', () => {
  let service: FinancialReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [FinancialReportsService, ApiService, provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '/api' }] });
    service = TestBed.inject(FinancialReportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests a trial balance with its selected reporting filters', () => {
    service.trialBalance({ fromDate: '2026-01-01', toDate: '2026-08-31', includeZeroBalances: true }).subscribe();

    const request = http.expectOne('/api/financial-reports/trial-balance?fromDate=2026-01-01&toDate=2026-08-31&includeZeroBalances=true');
    expect(request.request.method).toBe('GET');
    request.flush({ rows: [] });
  });

  it('requests a binary export using the same filters', () => {
    service.export('balance-sheet', { fromDate: '', toDate: '2026-08-31', includeZeroBalances: false }, 'Pdf').subscribe();

    const request = http.expectOne('/api/financial-reports/balance-sheet/export?toDate=2026-08-31&format=Pdf');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['%PDF']));
  });
});
