import { NgxSliderModule, Options } from '@angular-slider/ngx-slider';
import { Component, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IFilters } from '../../../models/filters.model';

@Component({
  selector: 'app-category-side-bar',
  standalone: true,
  imports: [NgxSliderModule, FormsModule],
  templateUrl: './side-bar.component.html',
  styleUrl: './side-bar.component.css'
})
export class SideBarComponent
{
  protected filters: IFilters = {};
  minValue: number = 0;
  maxValue: number = 300;
  unitsInStock: boolean = false;
  discounted: boolean = false;

  options: Options = {
    floor: 0,
    ceil: 300,
    animate: false
  };

  filtersApplied = output<IFilters>();

  removeAllFilters()
  {
    let key: keyof IFilters;
    for (key in this.filters)
    {
      this.removeFilter(key);
    }
  }

  removeFilter(key: keyof IFilters)
  {
    switch (key)
    {
      case "priceRange":
        {
          this.minValue = 0;
          this.maxValue = 300;
        }
        break;
      case "unitsInStock":
        {
          this.unitsInStock = false;
        }
        break;
      case "discounted":
        {
          this.discounted = false;
        }
        break;
      default:
        break;
    }
    if (key === "priceRange")
    {
      this.minValue = 0;
      this.maxValue = 300;
    }
    this.filters[key] = undefined;
    this.filtersApplied.emit(this.filters);
  }

  minValueChanged(event: Event)
  {
    const inputElement: HTMLInputElement = event.target as HTMLInputElement;
    let newValue: string = inputElement.value;
    if (!newValue.match(/^[0-9]+$/g))
    {
      if (newValue === '')
      {
        inputElement.value = "0";
        this.minValue = 0;
      }
      else
      {
        newValue = newValue.replace(/[^\d]/g, '');
        inputElement.value = newValue;
      }
    }
    this.minValue = +newValue;
  }

  maxValueChanged(event: Event)
  {
    const inputElement: HTMLInputElement = event.target as HTMLInputElement;
    let newValue: string = inputElement.value;
    if (!newValue.match(/^[0-9]+$/g))
    {
      if (newValue === '')
      {
        inputElement.value = "0";
        this.maxValue = 0;
      }
      else
      {
        newValue = newValue.replace(/[^\d]/g, '');
        inputElement.value = newValue;
        this.maxValue = +newValue;
      }
    }
    this.maxValue = +newValue;
  }

  onPriceApplied()
  {
    this.filters.priceRange = { min: this.minValue, max: this.maxValue };
    this.filtersApplied.emit(this.filters);
  }

  onFiltersApplied()
  {
    this.filters.unitsInStock = this.unitsInStock;
    this.filters.discounted = this.discounted;
    this.filtersApplied.emit(this.filters);
  }
}