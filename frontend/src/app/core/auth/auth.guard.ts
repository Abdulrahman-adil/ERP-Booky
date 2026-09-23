import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.restoreSession().pipe(
    take(1),
    map((user) => user
      ? true
      : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } }))
  );
};

export const loginGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.restoreSession().pipe(
    take(1),
    map((user) => user ? router.createUrlTree(['/dashboard']) : true)
  );
};
