import { Directive, ElementRef, Input, Renderer2 } from '@angular/core';

@Directive({
  selector: 'img',
  standalone: true,
})
export class DefaultImageDirective
{
  @Input({ required: true }) src: string | null | undefined = null

  constructor(
    private imageElementRef: ElementRef,
    private renderer: Renderer2
  ) { }

  ngOnChanges()
  {
    this.initImage();
  }

  initImage()
  {
    this.renderer.addClass(this.imageElementRef.nativeElement, 'default-img');
    const img = new Image();
    img.onload = () => this.onLoad();
    img.onerror = () => this.onError();
    img.src = this.getImageSrc(this.src);
  }

  setSrcToImage(src: any)
  {
    this.imageElementRef.nativeElement.setAttribute('src', src);
  }

  private getImageSrc(src: any): string
  {
    if (!src)
    {
      return "images/default.jpg";
    }
    return src;
  }

  onLoad()
  {
    this.setSrcToImage(this.getImageSrc(this.src));
    this.renderer.removeClass(this.imageElementRef.nativeElement, 'default-img');
  }

  onError()
  {
    this.setSrcToImage('images/default.jpg');
    this.renderer.removeClass(this.imageElementRef.nativeElement, 'default-img');
  }
}
