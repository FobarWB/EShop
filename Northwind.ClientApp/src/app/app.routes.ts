import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { PrivacyComponent } from './components/privacy/privacy.component';
import { ProductPageComponent } from './components/product-page/product-page.component';

export const routes: Routes = [
    { path: '', component: HomeComponent },
    { path: 'privacy', component: PrivacyComponent },
    { path: 'products/category/:id', component: ProductPageComponent },
    //{ path: '**', redirectTo: '/home' },
];