import { NgxSliderModule, Options } from '@angular-slider/ngx-slider';
import { Component, EventEmitter, output, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Filters
{
  priceRange: {
    min: number;
    max: number;
  };
  unitsInStock: boolean;
  discounted: boolean;
}

@Component({
  selector: 'app-category-side-bar',
  standalone: true,
  imports: [NgxSliderModule, FormsModule],
  templateUrl: './side-bar.component.html',
  styleUrl: './side-bar.component.css'
})
export class SideBarComponent
{
  minValue: number = 10;
  highValue: number = 200;
  unitsInStock: boolean = false;
  discounted: boolean = false;

  options: Options = {
    floor: 0,
    ceil: 300,
  };

  filtersApplied = output<Filters>();

  applyFilters()
  {
    const filters: Filters = {
      priceRange: { min: this.minValue, max: this.highValue },
      unitsInStock: this.unitsInStock,
      discounted: this.discounted
    };

    this.filtersApplied.emit(filters);
  }
}