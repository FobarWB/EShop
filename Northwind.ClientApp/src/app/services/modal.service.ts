import { inject, Injectable, TemplateRef, ViewContainerRef } from '@angular/core';
import { ModalComponent } from '../components/modal/modal.component';
import { Subject } from 'rxjs';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

@Injectable({
  providedIn: 'root'
})
export class ModalService
{
  private modalNotifier?: Subject<string>;
  constructor(private modalService: NgbModal) { }

  open(viewContainerRef: ViewContainerRef, content: TemplateRef<any>, options?: { size?: string; title?: string })
  {
    const contentViewRef = content.createEmbeddedView(null);
    const modalComponent = viewContainerRef.createComponent(ModalComponent, { projectableNodes: [contentViewRef.rootNodes] });
    modalComponent.hostView.detectChanges();
    modalComponent.instance.size = options?.size;
    modalComponent.instance.title = options?.size;
    modalComponent.instance.closeEvent.subscribe(() => this.closeModal());
    modalComponent.instance.submitEvent.subscribe(() => this.submitModal());
    this.modalNotifier = new Subject();
    return this.modalNotifier?.asObservable();
  }

  closeModal()
  {
    this.modalNotifier?.complete();
  }

  submitModal()
  {
    this.modalNotifier?.next("confirm");
    this.closeModal();
  }
}
