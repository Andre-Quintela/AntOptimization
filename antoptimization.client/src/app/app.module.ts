import { HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { HeaderComponent } from './components/header/header.component';
import { MapViewComponent } from './components/map-view/map-view.component';
import { PointsListComponent } from './components/points-list/points-list.component';
import { SearchBoxComponent } from './components/search-box/search-box.component';
import { MapPageComponent } from './pages/map-page/map-page.component';
import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';

@NgModule({
  declarations: [
    AppComponent,
    HeaderComponent,
    MapViewComponent,
    PointsListComponent,
    SearchBoxComponent,
    MapPageComponent,
    DashboardPageComponent
  ],
  imports: [
    BrowserModule, HttpClientModule,
    FormsModule,
    AppRoutingModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
