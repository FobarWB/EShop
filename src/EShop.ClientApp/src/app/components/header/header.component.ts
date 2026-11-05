import { Component, ChangeDetectionStrategy, computed, effect, inject, untracked } from '@angular/core';
import { RouterLink, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';

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
  protected readonly cartService = inject(CartService);
  protected readonly cartBadge = computed(() =>
  {
    const count = this.cartService.itemCount();
    return count > 99 ? '99+' : `${count}`;
  });
  protected readonly cartLabel = computed(() =>
  {
    const count = this.cartService.itemCount();
    return count > 0 ? `Cart, ${count} items` : 'Cart';
  });

  constructor()
  {
    // Runs at start and again when the user signs in or out. Untracked: signals read further down
    // (by the HTTP interceptors, for example) must not repeat the request.
    effect(() =>
    {
      this.auth.isAuthenticated();
      untracked(() => this.cartService.refreshItemCount());
    });
  }
}
