import { Component, Input, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent {
  @Input() showBackButton = false;
  @Input() iconClass = 'bi-diagram-3-fill';
  @Input() title = '';
  @Input() subtitle = '';
  @Input() authorCredit?: string;
  @Output() backClick = new EventEmitter<void>();

  onBackClick(): void {
    this.backClick.emit();
  }
}
