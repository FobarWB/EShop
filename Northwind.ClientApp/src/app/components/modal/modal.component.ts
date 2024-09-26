import { NgComponentOutlet } from '@angular/common';
import { Component, ElementRef, EventEmitter, Input, Output, Type } from '@angular/core';

@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [NgComponentOutlet],
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.css'
})
export class ModalComponent
{
  @Input() content!: Type<any>;
  @Input() title? = 'Modal title';

  @Output() closeEvent = new EventEmitter();
  @Output() submitEvent = new EventEmitter();

  constructor() { }

  close(): void
  {
    this.closeEvent.emit();
  }

  submit(): void
  {
    this.submitEvent.emit();
  }
}
