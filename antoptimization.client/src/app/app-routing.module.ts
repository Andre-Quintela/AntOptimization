import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MapPageComponent } from './pages/map-page/map-page.component';
import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';
import { DatasetPageComponent } from './pages/dataset-page/dataset-page.component';

const routes: Routes = [
  { path: '', component: MapPageComponent },
  { path: 'dashboard', component: DashboardPageComponent },
  { path: 'dataset', component: DatasetPageComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
