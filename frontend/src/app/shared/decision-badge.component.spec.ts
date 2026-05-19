import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DecisionBadgeComponent } from './decision-badge.component';

describe('DecisionBadgeComponent', () => {
  let fixture: ComponentFixture<DecisionBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DecisionBadgeComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(DecisionBadgeComponent);
    fixture.componentRef.setInput('decision', 'Review');
    fixture.detectChanges();
  });

  it('renders the decision label', () => {
    expect(fixture.nativeElement.textContent).toContain('Review');
  });
});
