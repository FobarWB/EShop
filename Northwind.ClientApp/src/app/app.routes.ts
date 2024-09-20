import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { PrivacyComponent } from './components/privacy/privacy.component';

export const routes: Routes = [
    { path: '', component: HomeComponent },
    { path: 'privacy', component: PrivacyComponent }
];