import { Component, OnInit } from '@angular/core';
import { DateService } from '../api/services';
import { first } from 'rxjs';

@Component({
  selector: 'date',
  templateUrl: './date.component.html',
  styleUrls: ['./date.component.css']
})
export class DateComponent implements OnInit {
  public currentDate?: Date;

  public constructor(
    private _dateServce: DateService
  ) { }

  ngOnInit(): void {
    this._dateServce.apiDateGet().pipe(first()).subscribe(dateDto => {
      return this.currentDate = dateDto.currentDate ? new Date(dateDto.currentDate) : undefined;
    })
  }
}
