import { NgxSliderModule, Options as SliderOptions } from '@angular-slider/ngx-slider';
import { Component, Input, output, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IFilters } from '../../../models/filters.model';

@Component({
  selector: 'app-category-side-bar',
  imports: [NgxSliderModule, FormsModule],
  templateUrl: './side-bar.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './side-bar.component.css'
})
export class SideBarComponent
{
  @Input({ required: true }) options!: SliderOptions;

  protected filters: IFilters = {};
  minValue: number = 0;
  maxValue: number = 0;
  unitsInStock: boolean = false;
  discounted: boolean = false;

  filtersApplied = output<IFilters>();

  ngOnChanges()
  {
    this.minValue = this.options.floor!;
    this.maxValue = this.options.ceil!;
  }

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
        this.minValue = this.options.floor!;
        inputElement.value = this.minValue.toString();
      }
      else
      {
        newValue = newValue.replace(/[^\d]/g, '');
        console.log(newValue);
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
        this.maxValue = this.options.floor!;
        inputElement.value = this.maxValue.toString();
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