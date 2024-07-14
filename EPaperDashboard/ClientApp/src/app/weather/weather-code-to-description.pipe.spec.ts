import { WeatherCodeToDescriptionPipe } from './weather-code-to-description.pipe';

describe('WeatherCodeToDescriptionPipe', () => {
  it('create an instance', () => {
    const pipe = new WeatherCodeToDescriptionPipe();
    expect(pipe).toBeTruthy();
  });
});
