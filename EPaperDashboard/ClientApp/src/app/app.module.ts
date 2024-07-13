import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { WeatherComponent } from './weather/weather.component';
import { DailyPhraseComponent } from './daily-phrase/daily-phrase.component';
import { CalendarEventsComponent } from './calendar-events/calendar-events.component';
import { TrashCollectionCalendarComponent } from './trash-collection-calendar/trash-collection-calendar.component';
import { DateComponent } from './date/date.component';
import { WeatherCodeToImagePipe } from './weather/weather-code-to-image.pipe';
import { WeatherCodeToDescriptionPipe } from './weather/weather-code-to-description.pipe';

@NgModule({
  declarations: [
    AppComponent,
    WeatherComponent,
    DailyPhraseComponent,
    CalendarEventsComponent,
    TrashCollectionCalendarComponent,
    DateComponent,
    WeatherCodeToImagePipe,
    WeatherCodeToDescriptionPipe
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([])
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
