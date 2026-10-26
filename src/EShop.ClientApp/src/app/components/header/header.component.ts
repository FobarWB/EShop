import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterModule],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './header.component.css'
})
export class HeaderComponent
{
  protected readonly auth = inject(AuthService);
}
