import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HeaderComponent } from './header.component';

describe('HeaderComponent', () => {
  let component: HeaderComponent;
  let fixture: ComponentFixture<HeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HeaderComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title and subtitle', () => {
    component.title = 'Route Optimizer';
    component.subtitle = 'ACO · OSM';
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Route Optimizer');
    expect(el.textContent).toContain('ACO · OSM');
  });

  it('should NOT render back button when showBackButton is false (default)', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.btn-back');
    expect(btn).toBeNull();
  });

  it('should render back button when showBackButton is true', () => {
    component.showBackButton = true;
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.btn-back');
    expect(btn).not.toBeNull();
  });

  it('should emit backClick when back button is clicked', () => {
    component.showBackButton = true;
    fixture.detectChanges();

    let count = 0;
    component.backClick.subscribe(() => count++);

    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-back');
    btn.click();

    expect(count).toBe(1);
  });

  it('should render authorCredit when provided', () => {
    component.authorCredit = 'André Quintela';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('André Quintela');
  });

  it('should NOT render authorCredit section when undefined', () => {
    component.authorCredit = undefined;
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('.header-made-by');
    expect(el).toBeNull();
  });
});
