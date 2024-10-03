import { Routes, UrlSegment } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { PrivacyComponent } from './components/privacy/privacy.component';
import { PageComponent as CategoryPageComponent } from './components/category/page/page.component';
import { PageComponent as ProductPageComponent } from './components/product/page/page.component';

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