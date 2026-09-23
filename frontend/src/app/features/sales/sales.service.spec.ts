import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_BASE_URL } from '../../core/config/api.config';
import { ApiService } from '../../core/services/api.service';
import { SalesService } from './sales.service';

describe('SalesService', () => {
  let service: SalesService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SalesService,
        ApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' }
      ]
    });

    service = TestBed.inject(SalesService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTestingController.verify());

  it('lists invoices using the supplied filters and pagination', () => {
    service.listInvoices({ search: 'SI-2026', status: 'Posted', pageNumber: 2, pageSize: 10 }).subscribe((result) => {
      expect(result.totalCount).toBe(0);
      expect(result.pageNumber).toBe(2);
    });

    const request = httpTestingController.expectOne('/api/sales-invoices?search=SI-2026&status=Posted&pageNumber=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });
  });

  it('posts a draft invoice through the dedicated posting endpoint', () => {
    service.postInvoice('invoice-id').subscribe();

    const request = httpTestingController.expectOne('/api/sales-invoices/invoice-id/post');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });
});
