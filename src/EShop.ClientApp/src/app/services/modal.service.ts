import { Injectable, Type } from '@angular/core';
import { ModalComponent } from '../components/modal/modal.component';
import { Subject } from 'rxjs';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';

@Injectable({
  providedIn: 'root'
})
export class ModalService
{
  private modalRef!: NgbModalRef;
  private modalComponent!: ModalComponent;
  private modalNotifier!: Subject<any>;

  constructor(private modalService: NgbModal) { }

  open(content: Type<any>, options?: { title?: string; data?: any })
  {
    this.modalRef = this.modalService.open(ModalComponent, { centered: true });
    this.modalComponent = this.modalRef.componentInstance;
    this.modalComponent.title = options?.title;
    this.modalComponent.data = options?.data;
    this.modalComponent.content = content;
    this.modalComponent.closeEvent.subscribe(() => this.closeModal());
    this.modalComponent.submitEvent.subscribe(() => this.submitModal());
    this.modalNotifier = new Subject();
    return this.modalNotifier?.asObservable();
  }

  closeModal()
  {
    this.modalRef?.close();
    this.modalNotifier?.complete();
  }

  submitModal()
  {
    this.modalRef?.close();
    this.modalNotifier?.next(this.modalComponent.contentInstance);
    this.closeModal();
  }
}
