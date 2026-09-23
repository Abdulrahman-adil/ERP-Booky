import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { FeedbackOutletComponent } from './shared/feedback/feedback-outlet.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, FeedbackOutletComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'ERP';
}
