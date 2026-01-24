import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  WidgetConfig, 
  ColorScheme, 
  HeaderConfig, 
  MarkdownConfig, 
  CalendarConfig, 
  WeatherConfig, 
  GraphConfig, 
  TodoConfig 
} from '../../models/types';

@Component({
  selector: 'app-widget-preview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="widget-preview">
      @switch (widget.type) {
        <!-- Header Widget -->
        @case ('header') {
          <div class="header-widget">
            <h3>{{ asHeaderConfig(widget.config).title }}</h3>
            @if (asHeaderConfig(widget.config).badges?.length) {
              <div class="badges">
                @for (badge of asHeaderConfig(widget.config).badges; track badge.label) {
                  <span class="badge">
                    @if (badge.icon) {
                      <i class="fa {{ badge.icon }}"></i>
                    }
                    {{ badge.label }}
                  </span>
                }
              </div>
            }
          </div>
        }

        <!-- Markdown Widget -->
        @case ('markdown') {
          <div class="markdown-widget">
            <p>{{ asMarkdownConfig(widget.config).content }}</p>
          </div>
        }

        <!-- Calendar Widget -->
        @case ('calendar') {
          <div class="calendar-widget">
            <i class="fa fa-calendar fa-2x"></i>
            <p>Calendar: {{ asCalendarConfig(widget.config).entityId || 'Not configured' }}</p>
            <small>Max events: {{ asCalendarConfig(widget.config).maxEvents }}</small>
          </div>
        }

        <!-- Weather Widget -->
        @case ('weather') {
          <div class="weather-widget">
            <i class="fa fa-cloud-sun fa-2x"></i>
            <p>Weather: {{ asWeatherConfig(widget.config).entityId || 'Not configured' }}</p>
          </div>
        }

        <!-- Weather Forecast Widget -->
        @case ('weather-forecast') {
          <div class="weather-widget">
            <i class="fa fa-cloud-sun-rain fa-2x"></i>
            <p>Forecast: {{ asWeatherConfig(widget.config).entityId || 'Not configured' }}</p>
          </div>
        }

        <!-- Graph Widget -->
        @case ('graph') {
          <div class="graph-widget">
            <i class="fa fa-chart-line fa-2x"></i>
            <p>Graph: {{ asGraphConfig(widget.config).entityId || 'Not configured' }}</p>
            <small>Period: {{ asGraphConfig(widget.config).period }}</small>
          </div>
        }

        <!-- Todo Widget -->
        @case ('todo') {
          <div class="todo-widget">
            <i class="fa fa-list-check fa-2x"></i>
            <p>Todo: {{ asTodoConfig(widget.config).entityId || 'Not configured' }}</p>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .widget-preview {
      height: 100%;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      font-size: 0.9rem;
    }

    .header-widget h3 {
      font-size: 1.2rem;
      margin-bottom: 0.5rem;
    }

    .badges {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
      justify-content: center;
    }

    .badge {
      padding: 2px 8px;
      border: 1px solid currentColor;
      border-radius: 4px;
      font-size: 0.75rem;
    }

    p {
      margin: 0.5rem 0;
      font-size: 0.85rem;
    }

    small {
      font-size: 0.7rem;
      opacity: 0.7;
    }
  `]
})
export class WidgetPreviewComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asHeaderConfig(config: any): HeaderConfig {
    return config as HeaderConfig;
  }

  asMarkdownConfig(config: any): MarkdownConfig {
    return config as MarkdownConfig;
  }

  asCalendarConfig(config: any): CalendarConfig {
    return config as CalendarConfig;
  }

  asWeatherConfig(config: any): WeatherConfig {
    return config as WeatherConfig;
  }

  asGraphConfig(config: any): GraphConfig {
    return config as GraphConfig;
  }

  asTodoConfig(config: any): TodoConfig {
    return config as TodoConfig;
  }
}
