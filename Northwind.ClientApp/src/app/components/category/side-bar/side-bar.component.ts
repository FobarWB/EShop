import { NgxSliderModule, Options } from '@angular-slider/ngx-slider';
import { Component, EventEmitter, Output } from '@angular/core';

@Component({
  selector: 'app-category-side-bar',
  standalone: true,
  imports: [NgxSliderModule],
  templateUrl: './side-bar.component.html',
  styleUrl: './side-bar.component.css'
})
export class SideBarComponent
{
  minValue: number = 40;
  highValue: number = 200;
  unitsInStock: boolean = false;
  discounted: boolean = false;

  options: Options = {
    floor: 0,
    ceil: 300,
  };

  @Output() filtersApplied = new EventEmitter<any>();

  applyFilters()
  {
    const filters = {
      priceRange: { min: this.minValue, max: this.highValue },
      unitsInStock: this.unitsInStock,
      discounted: this.discounted
    };

    this.filtersApplied.emit(filters);
  }
}