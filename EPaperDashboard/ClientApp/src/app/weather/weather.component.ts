import { Component, OnInit } from '@angular/core';
import { WeatherService } from '../api/services/weather.service';
import { first } from 'rxjs';
import { convertToDomain, WeatherInfo } from './weather.models';

@Component({
  selector: 'weather',
  templateUrl: './weather.component.html',
  styleUrls: ['./weather.component.css']
})
export class WeatherComponent implements OnInit {
  public weatherInfo?: WeatherInfo;

  constructor(private _weatherService: WeatherService) { }

  ngOnInit(): void {
    this._weatherService.weatherGet().pipe(first()).subscribe((weatherInfo) => this.weatherInfo = convertToDomain(weatherInfo));
  }
}

