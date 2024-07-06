import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyPhraseComponent } from './daily-phrase.component';

describe('DailyPhraseComponent', () => {
  let component: DailyPhraseComponent;
  let fixture: ComponentFixture<DailyPhraseComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DailyPhraseComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DailyPhraseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
