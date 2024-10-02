import { Directive, ElementRef, HostListener, Input } from '@angular/core';

@Directive({
  selector: 'img[default]',
  standalone: true,
})
export class DefaultImageDirective
{
  @Input() default: string = '';


  constructor(private imageElementRef: ElementRef) { }

  @HostListener('error') onError()
  {
    const imageElement = this.imageElementRef.nativeElement as HTMLImageElement;
    imageElement.src = this.default;
  }
}
