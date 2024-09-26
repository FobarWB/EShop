import { Injectable, Type } from '@angular/core';
import { ModalComponent } from '../components/modal/modal.component';
import { Subject } from 'rxjs';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';

@Injectable({
  providedIn: 'root'
})
export class ModalService
{
  private modalRef?: NgbModalRef;
  private modalNotifier?: Subject<string>;
  constructor(private modalService: NgbModal) { }

  open(content: Type<any>, options?: { title?: string; })
  {
    this.modalRef = this.modalService.open(ModalComponent, { centered: true });
    const modalComponent: ModalComponent = this.modalRef.componentInstance;
    modalComponent.title = options?.title;
    modalComponent.content = content;
    modalComponent.closeEvent.subscribe(() => this.closeModal());
    modalComponent.submitEvent.subscribe(() => this.submitModal());
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
    this.modalNotifier?.next("confirm");
    this.closeModal();
  }
}
