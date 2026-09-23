import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../config/api.config';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) private readonly apiBaseUrl: string
  ) {}

  get<TResponse>(path: string): Observable<TResponse> {
    return this.http.get<TResponse>(this.toUrl(path));
  }

  post<TResponse, TRequest>(path: string, body: TRequest): Observable<TResponse> {
    return this.http.post<TResponse>(this.toUrl(path), body);
  }

  postForm<TResponse>(path: string, body: FormData): Observable<TResponse> {
    return this.http.post<TResponse>(this.toUrl(path), body);
  }

  put<TResponse, TRequest>(path: string, body: TRequest): Observable<TResponse> {
    return this.http.put<TResponse>(this.toUrl(path), body);
  }

  delete<TResponse = void>(path: string): Observable<TResponse> {
    return this.http.delete<TResponse>(this.toUrl(path));
  }

  getBlob(path: string): Observable<Blob> {
    return this.http.get(this.toUrl(path), { responseType: 'blob' });
  }

  private toUrl(path: string): string {
    return `${this.apiBaseUrl.replace(/\/+$/, '')}/${path.replace(/^\/+/, '')}`;
  }
}
