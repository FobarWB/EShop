import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { PrivacyComponent } from './components/privacy/privacy.component';
import { PageComponent as CategoryPageComponent } from './components/category/page/page.component';


export const routes: Routes = [
    { path: '', component: HomeComponent },
    { path: 'privacy', component: PrivacyComponent },
    { path: ':categoryName/:id', component: CategoryPageComponent },
];