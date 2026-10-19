import { NgComponentOutlet } from '@angular/common';
import { ChangeDetectorRef, Component, EventEmitter, Input, Output, Type, ViewChild, ChangeDetectionStrategy } from '@angular/core';
import { EditComponent } from '../category/edit/edit.component';

@Component({
  selector: 'app-modal',
  imports: [NgComponentOutlet],
  templateUrl: './modal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './modal.component.css'
})
export class ModalComponent
{
  @ViewChild(NgComponentOutlet, { static: true }) private contentOutlet!: NgComponentOutlet;

  @Input() content!: Type<any>;
  @Input() title? = 'Modal title';
  @Input() data?: any;
  disabled? = false;

  @Output() closeEvent = new EventEmitter();
  @Output() submitEvent = new EventEmitter();
  @Output() contentInstance!: EditComponent;

  constructor(private changeDetectorRef: ChangeDetectorRef) { }

  ngAfterViewInit()
  {
    this.contentInstance = this.contentOutlet['_componentRef'].instance;
    this.contentInstance.data = this.data;
    this.changeDetectorRef.detectChanges();
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
