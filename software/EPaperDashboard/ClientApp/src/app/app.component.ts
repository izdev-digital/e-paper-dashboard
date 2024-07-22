import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'app-root',
  styleUrls: ['./app.component.css'],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  public generationTime?: Date;

  ngOnInit(): void {
    this.generationTime = new Date();
  }
}
