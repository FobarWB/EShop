import { NgComponentOutlet } from '@angular/common';
import { Component, EventEmitter, Input, Output, Type, ViewChild } from '@angular/core';

@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [NgComponentOutlet],
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.css'
})
export class ModalComponent
{
  @ViewChild(NgComponentOutlet, { static: true }) private contentOutlet!: NgComponentOutlet;

  @Input() content!: Type<any>;
  @Input() title? = 'Modal title';

  @Output() closeEvent = new EventEmitter();
  @Output() submitEvent = new EventEmitter();
  @Output() contentInstance!: any;

  constructor() { }

  ngAfterViewInit()
  {
    this.contentInstance = this.contentOutlet['_componentRef'].instance;
  }

  close(): void
  {
    this.closeEvent.emit();
  }

  submit(): void
  {
    this.submitEvent.emit();
  }
}
