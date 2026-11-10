import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { IOrderPlacedState } from '../../models/order-placed.model';

// The confirmation of a guest order. A guest cannot load /orders/:id (it needs a sign-in), so the checkout hands
// the order over in the navigation state. After a reload the state is gone and only a short note is shown:
// the order is personal data and is not kept in the browser's storage.
@Component({
  selector: 'app-order-placed',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './order-placed.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrderPlacedComponent
{
  // Read while the navigation is still running: the component is created during it.
  protected readonly placed =
    (inject(Router).currentNavigation()?.extras.state as IOrderPlacedState | undefined) ?? null;
}
