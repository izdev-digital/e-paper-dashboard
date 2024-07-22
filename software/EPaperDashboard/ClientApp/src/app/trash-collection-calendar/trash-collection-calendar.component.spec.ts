import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TrashCollectionCalendarComponent } from './trash-collection-calendar.component';

describe('TrashCollectionCalendarComponent', () => {
  let component: TrashCollectionCalendarComponent;
  let fixture: ComponentFixture<TrashCollectionCalendarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ TrashCollectionCalendarComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TrashCollectionCalendarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
