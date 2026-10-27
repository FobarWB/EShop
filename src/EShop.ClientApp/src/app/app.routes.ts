import { Routes, UrlSegment } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { PrivacyComponent } from './components/privacy/privacy.component';
import { PageComponent as CategoryPageComponent } from './components/category/page/page.component';
import { PageComponent as ProductPageComponent } from './components/product/page/page.component';
import { CartComponent } from './components/cart/cart.component';
import { autoLoginPartialRoutesGuard } from 'angular-auth-oidc-client';
import { CheckoutComponent } from './components/checkout/checkout.component';
import { OrderComponent } from './components/order/order.component';

export const routes: Routes = [
    {
        path: '',
        component: HomeComponent
    },
    {
        path: 'privacy',
        component: PrivacyComponent
    },
    {
        path: 'cart',
        component: CartComponent
    },
    // Orders belong to a signed-in user: the guard sends an anonymous visitor to Keycloak
    // and the library brings them back to this route after the sign-in.
    {
        path: 'checkout',
        component: CheckoutComponent,
        canActivate: [autoLoginPartialRoutesGuard]
    },
    {
        path: 'orders/:id',
        component: OrderComponent,
        canActivate: [autoLoginPartialRoutesGuard]
    },
    {
        matcher: (url) =>
        {
            if (url.length === 2 && url[1].path.match(/c\d+/gm))
            {
                return {
                    consumed: url,
                    posParams:
                    {
                        categoryName: new UrlSegment(url[0].path, {}),
                        id: new UrlSegment(url[1].path.slice(1), {})
                    }
                };
            }
            return null;
        },
        component: CategoryPageComponent,
    },
    {
        matcher: (url) => 
        {
            if (url.length === 2 && url[1].path.match(/p\d+/gm))
            {
                return {
                    consumed: url,
                    posParams:
                    {
                        productName: new UrlSegment(url[0].path, {}),
                        id: new UrlSegment(url[1].path.slice(1), {})
                    }
                }
            }
            return null;
        },
        component: ProductPageComponent
    },
    {
        path: '**',
        redirectTo: ''
    }
];